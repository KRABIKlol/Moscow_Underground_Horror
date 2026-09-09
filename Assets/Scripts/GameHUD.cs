using UnityEngine;

/// Отладочный интерфейс поста на OnGUI: верхняя строка смены, панель проверки,
/// журнал событий справа, подсказки внизу, экран итогов.
public class GameHUD : MonoBehaviour
{
    [Header("References")]
    public CheckpointController controller;
    public ShiftManager shift;
    public EventLog log;
    [Tooltip("Leave empty to auto-find. Without it the check panel opens automatically, as before.")]
    public PlayerInteractor interactor;

    [Header("Layout")]
    [Tooltip("Design resolution. The whole HUD scales from it.")]
    public Vector2 reference = new Vector2(1920f, 1080f);
    public int fontSize = 20;
    public float margin = 32f;
    public float panelWidth = 560f;
    public float logWidth = 460f;

    [Header("Options")]
    [Tooltip("Bottom bar reminding the checking rules. It never reveals the answer.")]
    public bool showRuleHints = true;

    GUIStyle _label, _title, _small, _button, _box, _prompt;
    bool _ready;

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<CheckpointController>();
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!log) log = FindFirstObjectByType<EventLog>();
        if (!interactor) interactor = FindFirstObjectByType<PlayerInteractor>();
    }

    void BuildStyles()
    {
        _label  = new GUIStyle(GUI.skin.label)  { richText = true, fontSize = fontSize, wordWrap = true };
        _small  = new GUIStyle(_label)          { fontSize = Mathf.RoundToInt(fontSize * 0.85f) };
        _title  = new GUIStyle(_label)          { fontSize = fontSize + 6, fontStyle = FontStyle.Bold };
        _button = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        _box    = new GUIStyle(GUI.skin.box)    { padding = new RectOffset(18, 18, 18, 18) };
        _prompt = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = fontSize + 2 };
        _ready = true;
    }

    void OnGUI()
    {
        if (!controller || !shift) return;
        if (!_ready) BuildStyles();

        float scale = Mathf.Min(Screen.width / reference.x, Screen.height / reference.y);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        if (shift.Finished) DrawResults();
        else
        {
            DrawTopBar();
            DrawLog();
            DrawHints();

            if (PanelVisible) DrawCheckPanel();
            else DrawPromptAndCrosshair();
        }

        GUI.matrix = old;
    }

    /// Панель проверки открыта? Без интерактора - всегда, как раньше.
    bool PanelVisible => interactor == null || interactor.PanelOpen;

    // ===== подсказка взаимодействия =====

    void DrawPromptAndCrosshair()
    {
        // прицел
        float d = 5f;
        var dot = new Rect(reference.x * 0.5f - d * 0.5f, reference.y * 0.5f - d * 0.5f, d, d);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.65f);
        GUI.DrawTexture(dot, Texture2D.whiteTexture);
        GUI.color = old;

        if (interactor == null) return;

        string p = interactor.Prompt;
        if (string.IsNullOrEmpty(p)) return;

        var r = new Rect(reference.x * 0.5f - 300f, reference.y * 0.62f, 600f, 56f);
        GUILayout.BeginArea(r, _box);
        GUILayout.Label(p, _prompt);
        GUILayout.EndArea();
    }

    // ===== верхняя строка =====

    void DrawTopBar()
    {
        var r = new Rect(margin, margin, reference.x - margin * 2f, 64f);
        GUILayout.BeginArea(r, _box);
        GUILayout.BeginHorizontal();

        GUILayout.Label($"СМЕНА  {shift.ClockString}", _label, GUILayout.Width(260));
        GUILayout.Label($"осталось  {shift.TimeLeftString}", _label, GUILayout.Width(260));
        GUILayout.FlexibleSpace();
        GUILayout.Label($"проверено  {shift.checkedCount}", _label, GUILayout.Width(240));

        string col = shift.mistakes == 0 ? "#80ff90"
                   : shift.mistakes < shift.maxMistakes - 1 ? "#ffd070" : "#ff6060";
        GUILayout.Label($"<color={col}>ошибки  {shift.mistakes} / {shift.maxMistakes}</color>",
                        _label, GUILayout.Width(260));

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    // ===== центральная панель проверки =====

    void DrawCheckPanel()
    {
        var r = new Rect(margin, margin + 80f, panelWidth, reference.y - margin * 2f - 180f);
        GUILayout.BeginArea(r, _box);

        var v = controller.Current;
        float h = fontSize * 2.4f;

        switch (controller.CurrentStage)
        {
            case CheckpointController.Stage.Empty:
                GUILayout.Label("Пост свободен", _title);
                GUILayout.Space(8);
                GUILayout.Label("Ожидание следующего посетителя…", _label);
                break;

            case CheckpointController.Stage.Approaching:
                GUILayout.Label("Посетитель подходит", _title);
                break;

            case CheckpointController.Stage.Documents:
                DrawDocument(v, h);
                break;

            case CheckpointController.Stage.Scanning:
                GUILayout.Label("Рамка", _title);
                GUILayout.Space(8);
                GUILayout.Label("Идёт сканирование…", _label);
                break;

            case CheckpointController.Stage.ScanResult:
                GUILayout.Label("Рамка", _title);
                GUILayout.Space(8);
                GUILayout.Label(AlarmText(), _title);
                GUILayout.Space(8);
                GUILayout.Label("Посетитель идёт к столу досмотра…", _label);
                break;

            case CheckpointController.Stage.Inspection:
                DrawInspection(v, h);
                break;
        }

        if (interactor != null && interactor.PanelOpen)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#a0a0a0>[Esc] закрыть панель</color>", _small);
        }

        GUILayout.EndArea();
    }

    void DrawDocument(Visitor v, float h)
    {
        var d = v.document;

        GUILayout.Label("ПРОВЕРКА ДОКУМЕНТА", _title);
        GUILayout.Space(10);
        GUILayout.Label($"Сегодня: {DocumentGenerator.Today}", _small);
        GUILayout.Space(10);

        GUILayout.Label($"ФИО            {d.fullName}", _label);
        GUILayout.Label($"Дата рождения  {d.birthDate}", _label);
        GUILayout.Label($"Номер          {d.documentId}", _label);
        GUILayout.Label($"Годен до       {d.expiryDate}", _label);
        GUILayout.Label($"Цель визита    {d.purpose}", _label);

        GUILayout.Space(16);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("К рамке", _button, GUILayout.Height(h))) controller.SendToScanner();
        if (GUILayout.Button("Развернуть", _button, GUILayout.Height(h))) controller.Reject();
        GUILayout.EndHorizontal();
    }

    void DrawInspection(Visitor v, float h)
    {
        GUILayout.Label("ДОСМОТР", _title);
        GUILayout.Space(8);
        GUILayout.Label(AlarmText(), _label);

        GUILayout.Space(10);
        GUILayout.Label($"Документ: {v.document.fullName}", _small);
        GUILayout.Space(10);
        GUILayout.Label("Вещи выложены на стол:", _label);

        foreach (var it in v.items)
        {
            if (it == null) continue;
            GUILayout.Label($"• {it.displayName}", _label);
        }

        GUILayout.Space(16);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Пропустить", _button, GUILayout.Height(h))) controller.LetThrough();
        if (GUILayout.Button("Задержать",  _button, GUILayout.Height(h))) controller.Reject();
        GUILayout.EndHorizontal();
    }

    string AlarmText() => controller.ScannerAlarm
        ? "<color=#ff4040>РАМКА СРАБОТАЛА</color>"
        : "<color=#60ff80>Рамка чистая</color>";

    // ===== журнал =====

    void DrawLog()
    {
        if (!log) return;

        var r = new Rect(reference.x - margin - logWidth, margin + 80f, logWidth, 520f);
        GUILayout.BeginArea(r, _box);
        GUILayout.Label("ЖУРНАЛ СОБЫТИЙ", _title);
        GUILayout.Space(8);

        foreach (var line in log.Lines)
        {
            bool bad = line.Contains("ОШИБКА") || line.Contains("тревога");
            GUILayout.Label(bad ? $"<color=#ff9090>{line}</color>" : line, _small);
        }

        GUILayout.EndArea();
    }

    // ===== подсказки =====

    void DrawHints()
    {
        if (!showRuleHints) return;

        string hint = controller.CurrentStage switch
        {
            CheckpointController.Stage.Documents =>
                "Сверь срок действия с сегодняшней датой, формат номера МУ-XXXXXX, возраст от 18 лет и цель визита.",
            CheckpointController.Stage.Inspection =>
                "Рамка ловит только металл. Баллончик, свёрток и порошок видно лишь на столе.",
            _ => "Смена ограничена по времени. Лимит ошибок жёсткий."
        };

        var r = new Rect(margin, reference.y - margin - 56f, reference.x - margin * 2f, 56f);
        GUILayout.BeginArea(r, _box);
        GUILayout.Label(hint, _small);
        GUILayout.EndArea();
    }

    // ===== итоги =====

    void DrawResults()
    {
        var r = new Rect(reference.x * 0.5f - 320f, reference.y * 0.5f - 240f, 640f, 480f);
        GUILayout.BeginArea(r, _box);

        GUILayout.Label(shift.Failed ? "<color=#ff6060>СМЕНА ПРОВАЛЕНА</color>" : "СМЕНА ОКОНЧЕНА", _title);
        GUILayout.Space(20);

        GUILayout.Label($"Проверено посетителей     {shift.checkedCount}", _label);
        GUILayout.Label($"Верных решений            {shift.correctDecisions}", _label);
        GUILayout.Label($"Ошибок                    {shift.mistakes} / {shift.maxMistakes}", _label);
        GUILayout.Label($"Точность                  {Mathf.RoundToInt(shift.Accuracy * 100f)} %", _label);

        GUILayout.Space(16);
        GUILayout.Label($"ОЦЕНКА   {shift.Grade}", _title);

        GUILayout.Space(24);
        if (GUILayout.Button("Начать смену заново", _button, GUILayout.Height(fontSize * 2.4f)))
        {
            shift.StartShift();
            if (log) log.Clear();
            controller.SpawnVisitor();
        }

        GUILayout.EndArea();
    }
}
