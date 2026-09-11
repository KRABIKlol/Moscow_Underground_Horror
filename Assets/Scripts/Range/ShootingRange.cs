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
    [Tooltip("Меню паузы. Пока оно открыто, тир не трогает курсор и не реагирует на Escape.")]
    public PauseMenu pause;
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

    /// Открыто меню паузы — тир замолкает и ни на что не реагирует.
    public bool Paused => pause && pause.IsPaused;

    /// Всё, что нужно интерфейсу и чего он сам знать не может.
    public float CountdownLeft => _countLeft;
    public bool HitFlash => _hitFlash > 0f;
    public int WeaponCount => weapons.Count;
    public int TargetCount => _targetCount;
    public float RoundDuration => roundDuration;
    public bool SceneReady => playerCamera && weapons.Count > 0 && _targetCount > 0;

    /// Подсказка над прицелом: на что смотрит игрок.
    public string Prompt => Current == State.Ready && Focused
        ? $"[{keyLabel}]  Взять {Focused.displayName}"
        : null;

    const string BestKey = "range_best_score";

    float _countLeft, _hitFlash;
    int _targetCount;
    public bool TweakMode { get; private set; }

    // ===== жизненный цикл =====

    void Awake()
    {
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!log) log = FindFirstObjectByType<EventLog>();
        if (!inputLock) inputLock = FindFirstObjectByType<CheckpointInputLock>();
        if (!pause) pause = FindFirstObjectByType<PauseMenu>();
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

        if (!FindFirstObjectByType<RangeUI>())
            Debug.LogError("[Тир] В сцене нет RangeUI — интерфейса тира не будет. " +
                           "Собери его: Tools → Стрельбище → «4. Собрать интерфейс тира».", this);

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

    /// Кнопка «Ещё раз» на экране итогов зачёта.
    public void RestartRound()
    {
        if (Current != State.Results) return;
        Current = State.Ready;
        CountTargets();
        ApplyCursor();
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
        // На паузе Escape принадлежит меню, а курсор — игроку.
        if (Paused) return;

        if (_hitFlash > 0f) _hitFlash -= Time.unscaledDeltaTime;

        switch (Current)
        {
            case State.Closed:
                if (!Available && shift && shift.Finished) Available = true;
                if (debugKeyOpensRange && DebugOpenPressed()) DebugOpen();
                break;

            case State.Ready:
                Focused = FindWeaponInView();
                if (Focused && InteractPressed()) StartRound(Focused);
                else if (LeavePressed()) Close();
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
                else if (LeavePressed()) EndRound(true);
                break;

            case State.Running:
                if (TweakPressed()) ToggleTweak();
                if (TweakMode) { HandleTweak(); break; }   // таймер на паузе, пока подгоняешь
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) { TimeLeft = 0f; EndRound(false); }
                else if (LeavePressed()) EndRound(true);
                break;

            case State.Results:
                if (LeavePressed()) Close();
                break;
        }
    }

    /// Курсор держим сами: FirstPersonController отпускает его по Esc, а в тире это мешает.
    void LateUpdate()
    {
        if (Current == State.Closed || Paused) return;
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

    /// Выход из тира и досрочное завершение зачёта. Escape не трогаем — он открывает меню паузы.
    bool LeavePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }
}
