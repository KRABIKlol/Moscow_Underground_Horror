using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Стрельбище: открывается, когда смена закончена. Игрок идёт к стеллажу,
/// берёт ствол по клавише E — запускается зачёт на время. В конце — таблица результата.
public class ShootingRange : MonoBehaviour
{
    public enum State { Closed, Ready, Countdown, Running, Results }

    [Header("Ссылки (пустые поля находятся сами)")]
    public ShiftManager shift;
    public EventLog log;
    public CheckpointInputLock inputLock;
    public Camera playerCamera;
    [Tooltip("Корневой объект игрока — чтобы луч выстрела не цеплял его самого.")]
    public Transform playerRoot;

    [Header("Оружие и мишени")]
    [Tooltip("Пусто — соберутся все RangeWeapon в сцене.")]
    public List<RangeWeapon> weapons = new List<RangeWeapon>();
    [Tooltip("Родитель мишеней. Каждому прямому потомку сам вешается ShootingTarget и коллайдер.")]
    public Transform targetsRoot;
    public bool autoSetupTargets = true;
    [Tooltip("Свой префаб пробоины. Пусто — рисуется простое тёмное пятно.")]
    public GameObject bulletHolePrefab;

    [Header("Раунд")]
    public float roundDuration = 60f;
    public float countdown = 3f;
    [Tooltip("Открывать тир и после проваленной смены.")]
    public bool openAfterFailedShift = true;
    [Tooltip("Стирать пробоины перед каждым раундом.")]
    public bool clearMarksOnStart = true;

    [Header("Оценка (попаданий за раунд)")]
    public int hitsForS = 45;
    public int hitsForA = 35;
    public int hitsForB = 25;
    public int hitsForC = 15;

    [Header("Взятие оружия")]
    public float pickupRange = 3f;
    public float pickupAngle = 50f;
    public string keyLabel = "E";

    [Header("Отладка")]
    [Tooltip("F5 в игре: досрочно закончить смену и сразу открыть тир, чтобы не ждать всю смену.")]
    public bool debugKeyOpensRange = true;

    [Header("Подгонка оружия в руках (F2 в игре)")]
    public bool allowTweakMode = true;
    public float tweakMoveSpeed = 0.25f;
    public float tweakRotateSpeed = 60f;

    [Header("Интерфейс")]
    public Vector2 reference = new Vector2(1920f, 1080f);
    public int fontSize = 20;
    public float margin = 32f;

    // ===== состояние =====

    public State Current { get; private set; } = State.Closed;

    /// Смена закончилась — на экране итогов можно предложить тир.
    public bool Available { get; private set; }

    /// Идёт тренировка: игрока не надо замораживать, итоги смены прячем.
    public bool PlayerFree => Current != State.Closed;

    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public float TimeLeft { get; private set; }
    public float Accuracy => Shots == 0 ? 0f : (float)Hits / Shots;

    public RangeWeapon HeldWeapon { get; private set; }
    public RangeWeapon Focused { get; private set; }

    const string BestKey = "range_best_score";

    float _countLeft, _hitFlash;
    int _targetCount;
    public bool TweakMode { get; private set; }
    GUIStyle _label, _small, _title, _big, _button, _box, _center;
    bool _styles;

    // ===== жизненный цикл =====

    void Awake()
    {
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!log) log = FindFirstObjectByType<EventLog>();
        if (!inputLock) inputLock = FindFirstObjectByType<CheckpointInputLock>();
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera) playerCamera = FindFirstObjectByType<Camera>();

        if (!playerRoot)
        {
            var fps = FindFirstObjectByType<FirstPersonController>();
            if (fps) playerRoot = fps.transform;
            else if (playerCamera) playerRoot = playerCamera.transform.root;
        }

        foreach (var w in FindObjectsByType<RangeWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w && !weapons.Contains(w)) weapons.Add(w);
        weapons.RemoveAll(w => w == null);

        if (bulletHolePrefab) ImpactMarks.customPrefab = bulletHolePrefab;

        if (autoSetupTargets) SetupTargets();

        BestScore = PlayerPrefs.GetInt(BestKey, 0);
    }

    void OnEnable() => ShootingTarget.OnAnyHit += HandleTargetHit;
    void OnDisable() => ShootingTarget.OnAnyHit -= HandleTargetHit;

    void Start()
    {
        if (shift) shift.OnShiftFinished += HandleShiftFinished;
        else Debug.LogWarning("[Тир] ShiftManager не найден — тир не откроется сам после смены.", this);

        if (weapons.Count == 0)
            Debug.LogWarning("[Тир] В сцене нет ни одного RangeWeapon — брать будет нечего. " +
                             "Выдели модели стволов и нажми Tools → Стрельбище → «2. Выделенное — это оружие».", this);

        if (!playerCamera)
            Debug.LogError("[Тир] Не найдена камера игрока — стрелять будет нечем. " +
                           "Заполни поле Player Camera или поставь камере тег MainCamera.", this);

        CountTargets();
        if (_targetCount == 0)
            Debug.LogWarning("[Тир] В сцене нет ни одной ShootingTarget — попадания считаться не будут. " +
                             "Выдели мишени и нажми Tools → Стрельбище → «3. Выделенное — это мишени».", this);
    }

    void OnDestroy()
    {
        if (shift) shift.OnShiftFinished -= HandleShiftFinished;
    }

    void HandleShiftFinished()
    {
        if (shift && shift.Failed && !openAfterFailedShift) return;
        Available = true;
    }

    [ContextMenu("Разметить мишени под Targets Root")]
    public void SetupTargets()
    {
        if (!targetsRoot) return;

        foreach (Transform child in targetsRoot)
        {
            if (!child || child.GetComponentInChildren<Renderer>(true) == null) continue;

            var t = child.GetComponent<ShootingTarget>();
            if (!t)
            {
                t = child.gameObject.AddComponent<ShootingTarget>();
                t.displayName = child.name;
            }
            t.EnsureCollider();
        }
    }

    // ===== переходы =====

    /// Кнопка на экране итогов смены.
    public void Open()
    {
        if (!Available) return;

        Current = State.Ready;
        Shots = Hits = Score = 0;
        CountTargets();
        if (log) log.Add("Стрельбище открыто. Возьми оружие со стойки.");
        ApplyCursor();
    }

    /// Открыть тир прямо сейчас, не дожидаясь конца смены (F5 и контекстное меню компонента).
    [ContextMenu("Открыть тир сейчас")]
    public void DebugOpen()
    {
        if (shift && !shift.Finished) shift.EndShift(false);
        Available = true;
        Open();
    }

    /// Вернуться к итогам смены.
    public void Close()
    {
        DropWeapon();
        Current = State.Closed;
        ApplyCursor();
    }

    /// Игрок жмёт «Начать смену заново» — тир закрывается вместе с итогами.
    public void CloseAndReset()
    {
        DropWeapon();
        Current = State.Closed;
        Available = false;
        ApplyCursor();
    }

    void StartRound(RangeWeapon w)
    {
        if (!w || !playerCamera) return;

        HeldWeapon = w;
        w.Take(playerCamera, playerRoot);
        w.OnShot += HandleShot;
        w.FireEnabled = false;

        if (clearMarksOnStart) ImpactMarks.Clear();
        ResetTargets();

        Shots = Hits = Score = 0;
        TimeLeft = roundDuration;
        _countLeft = Mathf.Max(0f, countdown);
        Current = _countLeft > 0f ? State.Countdown : State.Running;
        if (Current == State.Running) HeldWeapon.FireEnabled = true;

        if (log) log.Add($"Зачёт: {w.displayName}, {Mathf.RoundToInt(roundDuration)} с.");
        ApplyCursor();
    }

    void EndRound(bool aborted)
    {
        if (TweakMode) { PrintHoldValues(); TweakMode = false; }

        if (HeldWeapon) HeldWeapon.FireEnabled = false;
        DropWeapon();

        Current = State.Results;

        if (!aborted && Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }

        if (log)
        {
            log.Add(aborted
                ? "Зачёт прерван."
                : $"Тир: {Hits} попаданий, точность {Mathf.RoundToInt(Accuracy * 100f)} %, оценка {Grade}.");
        }

        ApplyCursor();
    }

    void DropWeapon()
    {
        if (!HeldWeapon) return;
        HeldWeapon.OnShot -= HandleShot;
        HeldWeapon.PutBack();
        HeldWeapon = null;
    }

    void ResetTargets()
    {
        foreach (var t in AllTargets()) t.ResetHits();
    }

    void CountTargets() => _targetCount = AllTargets().Length;

    ShootingTarget[] AllTargets() =>
        FindObjectsByType<ShootingTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    void HandleShot() => Shots++;

    // ===== подгонка оружия в руках =====

    void ToggleTweak()
    {
        if (!allowTweakMode || !HeldWeapon) return;

        TweakMode = !TweakMode;
        HeldWeapon.FireEnabled = !TweakMode && Current == State.Running;

        if (!TweakMode) PrintHoldValues();
    }

    void PrintHoldValues()
    {
        var w = HeldWeapon;
        if (!w) return;

        Debug.Log($"[Тир] {w.displayName}: перенеси эти значения в инспектор префаба/объекта.\n" +
                  $"Hold Position = ({w.holdPosition.x:0.###}, {w.holdPosition.y:0.###}, {w.holdPosition.z:0.###})\n" +
                  $"Hold Rotation = ({w.holdRotation.x:0.#}, {w.holdRotation.y:0.#}, {w.holdRotation.z:0.#})\n" +
                  $"Hold Scale = {w.holdScale:0.###}\n" +
                  $"Orientation Preset = {w.orientationPreset}", w);
    }

    static void NextPreset(RangeWeapon w)
    {
        w.orientationPreset++;
        if (w.orientationPreset >= RangeWeapon.PresetCount) w.orientationPreset = -1;
        w.holdRotation = Vector3.zero;   // поправка мешала бы видеть чистый разворот
    }

    void HandleTweak()
    {
        var w = HeldWeapon;
        if (!w) { TweakMode = false; return; }

        float dt = Time.unscaledDeltaTime;
        float p = tweakMoveSpeed * dt;
        float r = tweakRotateSpeed * dt;
        Vector3 dPos = Vector3.zero, dRot = Vector3.zero;
        float dScale = 0f;
        bool reset = false, flip = false, print = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftShiftKey.isPressed) { p *= 4f; r *= 4f; }
        if (kb.leftCtrlKey.isPressed)  { p *= 0.25f; r *= 0.25f; }

        if (kb.leftArrowKey.isPressed)  dPos.x -= p;
        if (kb.rightArrowKey.isPressed) dPos.x += p;
        if (kb.upArrowKey.isPressed)    dPos.y += p;
        if (kb.downArrowKey.isPressed)  dPos.y -= p;
        if (kb.pageUpKey.isPressed)     dPos.z += p;
        if (kb.pageDownKey.isPressed)   dPos.z -= p;

        if (kb.iKey.isPressed) dRot.x -= r;
        if (kb.kKey.isPressed) dRot.x += r;
        if (kb.jKey.isPressed) dRot.y -= r;
        if (kb.lKey.isPressed) dRot.y += r;
        if (kb.uKey.isPressed) dRot.z -= r;
        if (kb.oKey.isPressed) dRot.z += r;

        if (kb.minusKey.isPressed) dScale -= dt * 0.6f;
        if (kb.equalsKey.isPressed) dScale += dt * 0.6f;

        reset = kb.backspaceKey.wasPressedThisFrame;
        flip  = kb.fKey.wasPressedThisFrame;
        print = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        if (kb.tabKey.wasPressedThisFrame) NextPreset(w);
#else
        if (Input.GetKey(KeyCode.LeftShift))   { p *= 4f; r *= 4f; }
        if (Input.GetKey(KeyCode.LeftControl)) { p *= 0.25f; r *= 0.25f; }

        if (Input.GetKey(KeyCode.LeftArrow))  dPos.x -= p;
        if (Input.GetKey(KeyCode.RightArrow)) dPos.x += p;
        if (Input.GetKey(KeyCode.UpArrow))    dPos.y += p;
        if (Input.GetKey(KeyCode.DownArrow))  dPos.y -= p;
        if (Input.GetKey(KeyCode.PageUp))     dPos.z += p;
        if (Input.GetKey(KeyCode.PageDown))   dPos.z -= p;

        if (Input.GetKey(KeyCode.I)) dRot.x -= r;
        if (Input.GetKey(KeyCode.K)) dRot.x += r;
        if (Input.GetKey(KeyCode.J)) dRot.y -= r;
        if (Input.GetKey(KeyCode.L)) dRot.y += r;
        if (Input.GetKey(KeyCode.U)) dRot.z -= r;
        if (Input.GetKey(KeyCode.O)) dRot.z += r;

        if (Input.GetKey(KeyCode.Minus)) dScale -= dt * 0.6f;
        if (Input.GetKey(KeyCode.Equals)) dScale += dt * 0.6f;

        reset = Input.GetKeyDown(KeyCode.Backspace);
        flip  = Input.GetKeyDown(KeyCode.F);
        print = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (Input.GetKeyDown(KeyCode.Tab)) NextPreset(w);
#endif

        w.holdPosition += dPos;
        w.holdRotation += dRot;
        w.holdScale = Mathf.Max(0.05f, w.holdScale + dScale);

        if (reset) w.ResetHoldOffsets();
        if (flip) w.flipBarrel = !w.flipBarrel;
        if (print) PrintHoldValues();
    }

    void HandleTargetHit(ShootingTarget target, RaycastHit hit)
    {
        if (Current != State.Running) return;
        Hits++;
        Score += Mathf.Max(1, target.points);
        _hitFlash = 0.12f;
    }

    public string Grade
    {
        get
        {
            if (Score >= hitsForS) return "S";
            if (Score >= hitsForA) return "A";
            if (Score >= hitsForB) return "B";
            if (Score >= hitsForC) return "C";
            return "D";
        }
    }

    public string TimeLeftString
    {
        get
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
            return $"{t / 60:00}:{t % 60:00}";
        }
    }

    // ===== апдейт =====

    void Update()
    {
        if (_hitFlash > 0f) _hitFlash -= Time.deltaTime;

        switch (Current)
        {
            case State.Closed:
                if (!Available && shift && shift.Finished) Available = true;
                if (debugKeyOpensRange && DebugOpenPressed()) DebugOpen();
                break;

            case State.Ready:
                Focused = FindWeaponInView();
                if (Focused && InteractPressed()) StartRound(Focused);
                else if (EscapePressed()) Close();
                break;

            case State.Countdown:
                if (TweakPressed()) ToggleTweak();
                if (TweakMode) { HandleTweak(); break; }
                _countLeft -= Time.deltaTime;
                if (_countLeft <= 0f)
                {
                    Current = State.Running;
                    if (HeldWeapon) { HeldWeapon.Refill(); HeldWeapon.FireEnabled = true; }
                }
                else if (EscapePressed()) EndRound(true);
                break;

            case State.Running:
                if (TweakPressed()) ToggleTweak();
                if (TweakMode) { HandleTweak(); break; }   // таймер на паузе, пока подгоняешь
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) { TimeLeft = 0f; EndRound(false); }
                else if (EscapePressed()) EndRound(true);
                break;

            case State.Results:
                if (EscapePressed()) Close();
                break;
        }
    }

    /// Курсор держим сами: FirstPersonController отпускает его по Esc, а в тире это мешает.
    void LateUpdate()
    {
        if (Current == State.Closed) return;
        ApplyCursor();
    }

    void ApplyCursor()
    {
        if (Current == State.Closed)
        {
            // Обычно курсором занимается CheckpointInputLock; если его нет — освобождаем сами.
            if (inputLock) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        bool free = Current == State.Results;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    RangeWeapon FindWeaponInView()
    {
        if (!playerCamera) return null;

        RangeWeapon best = null;
        float bestAngle = pickupAngle;

        foreach (var w in weapons)
        {
            if (!w || w.Held || !w.gameObject.activeInHierarchy) continue;

            Vector3 to = w.transform.position - playerCamera.transform.position;
            if (to.magnitude > pickupRange) continue;

            float angle = Vector3.Angle(playerCamera.transform.forward, to);
            if (angle > bestAngle) continue;

            bestAngle = angle;
            best = w;
        }

        return best;
    }

    // ===== интерфейс =====

    void BuildStyles()
    {
        _label  = new GUIStyle(GUI.skin.label)  { richText = true, fontSize = fontSize, wordWrap = true };
        _small  = new GUIStyle(_label)          { fontSize = Mathf.RoundToInt(fontSize * 0.85f) };
        _title  = new GUIStyle(_label)          { fontSize = fontSize + 6, fontStyle = FontStyle.Bold };
        _big    = new GUIStyle(_label)          { fontSize = fontSize * 4, fontStyle = FontStyle.Bold,
                                                  alignment = TextAnchor.MiddleCenter };
        _center = new GUIStyle(_label)          { alignment = TextAnchor.MiddleCenter, fontSize = fontSize + 2 };
        _button = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        _box    = new GUIStyle(GUI.skin.box)    { padding = new RectOffset(18, 18, 18, 18) };
        _styles = true;
    }

    void OnGUI()
    {
        if (Current == State.Closed) return;
        if (!_styles) BuildStyles();

        float scale = Mathf.Min(Screen.width / reference.x, Screen.height / reference.y);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        switch (Current)
        {
            case State.Ready:     DrawReady();     break;
            case State.Countdown: DrawCountdown(); break;
            case State.Running:   DrawRunning();   break;
            case State.Results:   DrawResults();   break;
        }

        GUI.matrix = old;
    }

    void DrawReady()
    {
        DrawCrosshair();

        var top = new Rect(margin, margin, reference.x - margin * 2f, 64f);
        GUILayout.BeginArea(top, _box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("СТРЕЛЬБИЩЕ", _label, GUILayout.Width(260));
        GUILayout.Label($"зачёт  {Mathf.RoundToInt(roundDuration)} с", _label, GUILayout.Width(220));
        GUILayout.FlexibleSpace();
        GUILayout.Label($"рекорд  {BestScore}", _label, GUILayout.Width(220));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        if (Focused)
        {
            var r = new Rect(reference.x * 0.5f - 300f, reference.y * 0.62f, 600f, 56f);
            GUILayout.BeginArea(r, _box);
            GUILayout.Label($"[{keyLabel}]  Взять {Focused.displayName}", _center);
            GUILayout.EndArea();
        }

        if (weapons.Count == 0 || _targetCount == 0 || !playerCamera)
        {
            var warn = new Rect(reference.x * 0.5f - 400f, reference.y * 0.34f, 800f, 150f);
            GUILayout.BeginArea(warn, _box);
            GUILayout.Label("<color=#ff8080>СЦЕНА НЕ НАСТРОЕНА</color>", _center);
            GUILayout.Space(8);
            if (!playerCamera)
                GUILayout.Label("• Не найдена камера игрока — заполни Player Camera или поставь тег MainCamera.", _small);
            if (weapons.Count == 0)
                GUILayout.Label("• Нет оружия: выдели модели стволов → Tools ▸ Стрельбище ▸ «2. Выделенное — это оружие».", _small);
            if (_targetCount == 0)
                GUILayout.Label("• Нет мишеней: выдели щиты и манекены → Tools ▸ Стрельбище ▸ «3. Выделенное — это мишени».", _small);
            GUILayout.EndArea();
        }

        var hint = new Rect(margin, reference.y - margin - 56f, reference.x - margin * 2f, 56f);
        GUILayout.BeginArea(hint, _box);
        GUILayout.Label("Возьми ствол со стойки — сразу пойдёт зачёт на время. " +
                        "ЛКМ — огонь, R — перезарядка, Esc — вернуться к итогам смены.", _small);
        GUILayout.EndArea();
    }

    void DrawCountdown()
    {
        DrawCrosshair();
        DrawAmmo();
        if (TweakMode) { DrawTweak(); return; }

        string text = _countLeft > 1f ? Mathf.CeilToInt(_countLeft).ToString() : "ОГОНЬ";
        var r = new Rect(reference.x * 0.5f - 200f, reference.y * 0.33f, 400f, 160f);
        GUI.Label(r, text, _big);
    }

    void DrawRunning()
    {
        DrawCrosshair();
        DrawAmmo();
        if (TweakMode) DrawTweak();

        var top = new Rect(margin, margin, reference.x - margin * 2f, 64f);
        GUILayout.BeginArea(top, _box);
        GUILayout.BeginHorizontal();

        string col = TimeLeft <= 10f ? "#ff6060" : "#ffffff";
        GUILayout.Label($"ТИР  <color={col}>{TimeLeftString}</color>", _label, GUILayout.Width(260));
        GUILayout.Label($"попаданий  {Hits}", _label, GUILayout.Width(240));
        GUILayout.Label($"выстрелов  {Shots}", _label, GUILayout.Width(240));
        GUILayout.FlexibleSpace();
        GUILayout.Label($"точность  {Mathf.RoundToInt(Accuracy * 100f)} %", _label, GUILayout.Width(240));
        GUILayout.Label($"очки  {Score}", _label, GUILayout.Width(160));
        if (allowTweakMode) GUILayout.Label("<color=#a0a0a0>F2 — подгонка</color>", _small, GUILayout.Width(180));

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawResults()
    {
        var r = new Rect(reference.x * 0.5f - 320f, reference.y * 0.5f - 240f, 640f, 480f);
        GUILayout.BeginArea(r, _box);

        GUILayout.Label("ЗАЧЁТ ОКОНЧЕН", _title);
        GUILayout.Space(20);

        GUILayout.Label($"Попаданий                 {Hits}", _label);
        GUILayout.Label($"Выстрелов                 {Shots}", _label);
        GUILayout.Label($"Точность                  {Mathf.RoundToInt(Accuracy * 100f)} %", _label);
        GUILayout.Label($"Очки                      {Score}", _label);
        GUILayout.Label($"Рекорд                    {BestScore}", _small);

        GUILayout.Space(16);
        GUILayout.Label($"ОЦЕНКА   {Grade}", _title);

        GUILayout.Space(24);
        float h = fontSize * 2.4f;
        if (GUILayout.Button("Ещё раз", _button, GUILayout.Height(h)))
        {
            Current = State.Ready;
            ApplyCursor();
        }
        GUILayout.Space(8);
        if (GUILayout.Button("К итогам смены", _button, GUILayout.Height(h)))
            Close();

        GUILayout.EndArea();
    }

    void DrawTweak()
    {
        var w = HeldWeapon;
        if (!w) return;

        var r = new Rect(margin, margin + 90f, 640f, 430f);
        GUILayout.BeginArea(r, _box);

        GUILayout.Label("ПОДГОНКА ОРУЖИЯ", _title);
        GUILayout.Space(6);
        GUILayout.Label($"<color=#ffd070>{w.displayName}</color>   длина модели {w.BarrelLength:0.00} м", _small);
        GUILayout.Space(10);

        GUILayout.Label($"Hold Position   {w.holdPosition.x:0.###}   {w.holdPosition.y:0.###}   {w.holdPosition.z:0.###}", _label);
        GUILayout.Label($"Hold Rotation   {w.holdRotation.x:0.#}   {w.holdRotation.y:0.#}   {w.holdRotation.z:0.#}", _label);
        GUILayout.Label($"Hold Scale      {w.holdScale:0.###}", _label);
        GUILayout.Label($"Orientation Preset   <color=#ffd070>" +
                        (w.orientationPreset < 0 ? "авто" : $"{w.orientationPreset} из {RangeWeapon.PresetCount - 1}") +
                        "</color>", _label);

        GUILayout.Space(12);
        GUILayout.Label("<color=#80ff90>TAB — перебрать развороты, пока ствол не встанет прямо.</color> Это главное.", _label);
        GUILayout.Space(6);
        GUILayout.Label("Стрелки — двигать по X/Y,   PageUp/PageDown — по Z", _small);
        GUILayout.Label("I/K — наклон,   J/L — поворот,   U/O — крен (тонкая доводка)", _small);
        GUILayout.Label("- / = — размер,   Backspace — сброс всего", _small);
        GUILayout.Label("Shift — быстрее,   Ctrl — точнее,   Enter — вывести в консоль", _small);
        GUILayout.Space(8);
        GUILayout.Label("<color=#80ff90>F2 — закончить подгонку (значения уйдут в консоль)</color>", _small);
        GUILayout.Space(6);
        GUILayout.Label("Значения нужно перенести в инспектор объекта: после выхода из игры они не сохранятся.", _small);

        GUILayout.EndArea();
    }

    void DrawAmmo()
    {
        if (!HeldWeapon) return;

        var r = new Rect(reference.x - margin - 320f, reference.y - margin - 96f, 320f, 96f);
        GUILayout.BeginArea(r, _box);

        if (HeldWeapon.Reloading)
            GUILayout.Label("<color=#ffd070>ПЕРЕЗАРЯДКА…</color>", _title);
        else
        {
            string col = HeldWeapon.Ammo == 0 ? "#ff6060"
                       : HeldWeapon.Ammo <= HeldWeapon.magazineSize / 4 ? "#ffd070" : "#ffffff";
            GUILayout.Label($"<color={col}>{HeldWeapon.Ammo}</color> / {HeldWeapon.magazineSize}", _title);
        }

        GUILayout.Label($"{HeldWeapon.displayName}   [R] перезарядка", _small);
        GUILayout.EndArea();
    }

    void DrawCrosshair()
    {
        Color old = GUI.color;

        bool flash = _hitFlash > 0f;
        GUI.color = flash ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.65f);

        float cx = reference.x * 0.5f, cy = reference.y * 0.5f;
        float gap = 8f, len = flash ? 16f : 12f, w = 2f;

        GUI.DrawTexture(new Rect(cx - gap - len, cy - w * 0.5f, len, w), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + gap,       cy - w * 0.5f, len, w), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - gap - len, w, len), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - w * 0.5f, cy + gap,       w, len), Texture2D.whiteTexture);

        GUI.color = old;
    }

    // ===== ввод =====

    bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    bool TweakPressed()
    {
        if (!allowTweakMode) return false;
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F2);
#endif
    }

    bool DebugOpenPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F5);
#endif
    }

    bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
