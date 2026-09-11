using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Геймплейный интерфейс на Canvas: строка смены, панель проверки, журнал,
/// подсказка взаимодействия и экран итогов. Ссылки расставляет UIBuilder.
public class GameUI : MonoBehaviour
{
    [Header("Логика")]
    public CheckpointController controller;
    public ShiftManager shift;
    public EventLog log;
    public PlayerInteractor interactor;

    [Header("Верхняя строка")]
    public Text clockText;
    public Text timeLeftText;
    public Text checkedText;
    public Text mistakesText;

    [Header("Панель проверки")]
    public GameObject checkPanel;
    public Text panelTitle;

    public GameObject documentBlock;
    public Text todayText;
    public Text nameText;
    public Text birthText;
    public Text idText;
    public Text expiryText;
    public Text purposeText;

    public GameObject itemsBlock;
    public RectTransform itemsContainer;
    public GameObject itemRowTemplate;

    public Text scanStatusText;

    public GameObject documentButtons;
    public Button toScannerButton;
    public Button turnAwayButton;

    public GameObject inspectionButtons;
    public Button passButton;
    public Button detainButton;

    public Text closeHintText;

    [Header("Журнал")]
    public Text logText;

    [Header("Подсказка и прицел")]
    public GameObject promptRoot;
    public Text promptText;
    public GameObject crosshair;

    [Header("Итоги смены")]
    public GameObject resultsPanel;
    public Text resultsTitle;
    public Text resultsStats;
    public Text resultsGrade;
    public Button restartButton;
    public Button menuButton;

    [Header("Тир (необязательно)")]
    public ShootingRange range;
    public Button rangeButton;

    [Header("Сцены")]
    public string mainMenuScene = "MainMenu";

    readonly List<GameObject> _rows = new List<GameObject>();
    Visitor _lastVisitor;
    CheckpointController.Stage _lastStage = (CheckpointController.Stage)(-1);
    bool _resultsShown;

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<CheckpointController>();
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!log) log = FindFirstObjectByType<EventLog>();
        if (!interactor) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (!range) range = FindFirstObjectByType<ShootingRange>();

        if (itemRowTemplate) itemRowTemplate.SetActive(false);

        Bind(toScannerButton, () => { Sfx(a => a.uiClick); controller.SendToScanner(); });
        Bind(turnAwayButton,  () => { Sfx(a => a.stampReject); controller.Reject(); });
        Bind(passButton,      () => { Sfx(a => a.stampApprove); controller.LetThrough(); });
        Bind(detainButton,    () => { Sfx(a => a.stampReject); controller.Detain(); });

        Bind(rangeButton, () =>
        {
            Sfx(a => a.uiClick);
            if (range) range.Open();
        });

        Bind(restartButton, () =>
        {
            Sfx(a => a.uiClick);
            if (range) range.CloseAndReset();
            _resultsShown = false;
            shift.StartShift();
            if (log) log.Clear();
            controller.SpawnVisitor();
        });

        Bind(menuButton, () =>
        {
            Sfx(a => a.uiBack);
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
        });
    }

    void OnEnable()
    {
        if (!controller) return;
        controller.OnScanFinished += HandleScan;
        controller.OnVisitorHandled += HandleHandled;
        if (shift) shift.OnShiftFinished += HandleShiftEnd;
    }

    void OnDisable()
    {
        if (controller)
        {
            controller.OnScanFinished -= HandleScan;
            controller.OnVisitorHandled -= HandleHandled;
        }
        if (shift) shift.OnShiftFinished -= HandleShiftEnd;
    }

    void HandleScan(Visitor v, bool alarm) => Sfx(a => alarm ? a.scanAlarm : a.scanClean);

    void HandleHandled(Visitor v, bool passed)
    {
        if (v && v.ShouldBeAllowed != passed) Sfx(a => a.mistake);
    }

    void HandleShiftEnd() => Sfx(a => a.shiftEnd);

    void Bind(Button b, UnityEngine.Events.UnityAction action)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    void Sfx(System.Func<AudioManager, AudioClip> pick)
    {
        var am = AudioManager.I;
        if (am) am.PlaySfx(pick(am));
    }

    void Update()
    {
        if (!controller || !shift) return;

        UpdateTopBar();
        UpdateLog();

        if (shift.Finished)
        {
            ShowResults();
            return;
        }

        if (resultsPanel && resultsPanel.activeSelf) resultsPanel.SetActive(false);

        bool panelOpen = interactor == null || interactor.PanelOpen;

        if (checkPanel) checkPanel.SetActive(panelOpen);
        if (promptRoot) promptRoot.SetActive(!panelOpen && interactor && !string.IsNullOrEmpty(interactor.Prompt));
        if (crosshair) crosshair.SetActive(!panelOpen);

        if (!panelOpen)
        {
            if (promptText && interactor) promptText.text = interactor.Prompt;
            return;
        }

        UpdateCheckPanel();
    }

    void UpdateTopBar()
    {
        if (clockText)    clockText.text    = "СМЕНА  " + shift.ClockString;
        if (timeLeftText) timeLeftText.text = "осталось  " + shift.TimeLeftString;
        if (checkedText)  checkedText.text  = "проверено  " + shift.checkedCount;

        if (mistakesText)
        {
            mistakesText.text = $"ошибки  {shift.mistakes} / {shift.maxMistakes}";
            mistakesText.color = shift.mistakes == 0 ? new Color(0.55f, 1f, 0.6f)
                               : shift.mistakes < shift.maxMistakes - 1 ? new Color(1f, 0.85f, 0.45f)
                               : new Color(1f, 0.4f, 0.4f);
        }
    }

    void UpdateLog()
    {
        if (!logText || log == null) return;
        logText.text = string.Join("\n", log.Lines);
    }

    void UpdateCheckPanel()
    {
        var v = controller.Current;
        var stage = controller.CurrentStage;

        bool docs = stage == CheckpointController.Stage.Documents;
        bool insp = stage == CheckpointController.Stage.Inspection;

        if (panelTitle) panelTitle.text = docs ? "ПРОВЕРКА ДОКУМЕНТА"
                                        : insp ? "ДОСМОТР"
                                        : "ПОСТ";

        if (documentBlock)    documentBlock.SetActive(docs && v != null);
        if (itemsBlock)       itemsBlock.SetActive(insp && v != null);
        if (documentButtons)  documentButtons.SetActive(docs);
        if (inspectionButtons) inspectionButtons.SetActive(insp);
        if (closeHintText)    closeHintText.gameObject.SetActive(interactor != null);

        if (scanStatusText)
        {
            bool show = insp;
            scanStatusText.gameObject.SetActive(show);
            if (show)
            {
                scanStatusText.text = controller.ScannerAlarm ? "РАМКА СРАБОТАЛА" : "Рамка чистая";
                scanStatusText.color = controller.ScannerAlarm
                    ? new Color(1f, 0.35f, 0.35f)
                    : new Color(0.45f, 1f, 0.55f);
            }
        }

        if (v == null) return;

        if (docs && v.document != null)
        {
            if (todayText)   todayText.text   = "Сегодня: " + DocumentGenerator.Today;
            if (nameText)    nameText.text    = "ФИО                " + v.document.fullName;
            if (birthText)   birthText.text   = "Дата рождения      " + v.document.birthDate;
            if (idText)      idText.text      = "Номер              " + v.document.documentId;
            if (expiryText)  expiryText.text  = "Годен до           " + v.document.expiryDate;
            if (purposeText) purposeText.text = "Цель визита        " + v.document.purpose;
        }

        if (insp && (v != _lastVisitor || stage != _lastStage))
            RebuildItems(v);

        _lastVisitor = v;
        _lastStage = stage;
    }

    void RebuildItems(Visitor v)
    {
        foreach (var go in _rows) if (go) Destroy(go);
        _rows.Clear();

        if (!itemsContainer || !itemRowTemplate) return;

        foreach (var it in v.items)
        {
            if (it == null) continue;

            var row = Instantiate(itemRowTemplate, itemsContainer);
            row.SetActive(true);

            var t = row.GetComponent<Text>();
            if (t) t.text = "•  " + it.displayName;

            _rows.Add(row);
        }
    }

    void ShowResults()
    {
        if (checkPanel) checkPanel.SetActive(false);
        if (promptRoot) promptRoot.SetActive(false);
        if (crosshair) crosshair.SetActive(false);
        if (!resultsPanel) return;

        // Пока игрок в тире, экран итогов уходит с дороги.
        if (range && range.PlayerFree)
        {
            resultsPanel.SetActive(false);
            return;
        }

        resultsPanel.SetActive(true);
        if (rangeButton) rangeButton.gameObject.SetActive(range && range.Available);
        if (_resultsShown) return;
        _resultsShown = true;

        if (resultsTitle)
        {
            resultsTitle.text = shift.Failed ? "СМЕНА ПРОВАЛЕНА" : "СМЕНА ОКОНЧЕНА";
            resultsTitle.color = shift.Failed ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        if (resultsStats)
            resultsStats.text =
                $"Проверено посетителей     {shift.checkedCount}\n" +
                $"Верных решений            {shift.correctDecisions}\n" +
                $"Ошибок                    {shift.mistakes} / {shift.maxMistakes}\n" +
                $"Точность                  {Mathf.RoundToInt(shift.Accuracy * 100f)} %";

        if (resultsGrade) resultsGrade.text = "ОЦЕНКА   " + shift.Grade;
    }
}
