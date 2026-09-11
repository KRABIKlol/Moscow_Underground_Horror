using UnityEngine;
using UnityEngine.UI;

/// Интерфейс стрельбища на Canvas — в том же виде, что и GameUI.
/// Иерархию и ссылки собирает RangeUIBuilder: Tools ▸ Стрельбище ▸ «4. Собрать интерфейс тира».
public class RangeUI : MonoBehaviour
{
    [Header("Логика")]
    public ShootingRange range;
    public PauseMenu pause;

    [Header("Чужой интерфейс")]
    [Tooltip("GameUI_Canvas: пока тир открыт, он выключается целиком. " +
             "При выходе возвращается ровно в то состояние, в котором был.")]
    public GameObject gameUiRoot;

    [Header("Корень")]
    [Tooltip("Объект, который прячется целиком, когда тир закрыт или открыто меню паузы.")]
    public GameObject root;

    [Header("Верхняя строка")]
    public GameObject topBar;
    public Text titleText;
    public Text timeText;
    public Text hitsText;
    public Text shotsText;
    public Text accuracyText;
    public Text scoreText;

    [Header("Прицел")]
    public GameObject crosshair;
    public Graphic[] crosshairBars;

    [Header("Подсказка над прицелом")]
    public GameObject promptRoot;
    public Text promptText;

    [Header("Патроны")]
    public GameObject ammoRoot;
    public Text ammoText;
    public Text ammoHint;

    [Header("Отсчёт")]
    public GameObject countdownRoot;
    public Text countdownText;

    [Header("Нижняя подсказка")]
    public GameObject hintRoot;
    public Text hintText;

    [Header("Сцена не настроена")]
    public GameObject setupWarnRoot;
    public Text setupWarnText;

    [Header("Итоги зачёта")]
    public GameObject resultsPanel;
    public Text resultsStats;
    public Text resultsGrade;
    public Button againButton;
    public Button backButton;

    [Header("Подгонка оружия (F2)")]
    public GameObject tweakPanel;
    public Text tweakText;

    [Header("Цвета")]
    public Color normalColor = Color.white;
    public Color warnColor = new Color(1f, 0.85f, 0.45f);
    public Color badColor = new Color(1f, 0.4f, 0.4f);
    public Color goodColor = new Color(0.55f, 1f, 0.6f);
    public Color crosshairIdle = new Color(1f, 1f, 1f, 0.65f);
    public Color crosshairHit = new Color(1f, 0.35f, 0.3f, 1f);

    bool _gameUiWasOn, _gameUiHidden, _warnedSelfHide;

    void Awake()
    {
        if (!range) range = FindFirstObjectByType<ShootingRange>();
        if (!pause) pause = FindFirstObjectByType<PauseMenu>();

        if (!gameUiRoot)
        {
            // GameUI может лежать на выключенном канвасе, поэтому ищем и среди неактивных.
            var gameUI = FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (gameUI) gameUiRoot = gameUI.gameObject;
        }

        Bind(againButton, () => { Click(); if (range) range.RestartRound(); });
        Bind(backButton,  () => { Back();  if (range) range.Close(); });

        if (root) root.SetActive(false);
    }

    void Bind(Button b, UnityEngine.Events.UnityAction action)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    void Click() { if (AudioManager.I) AudioManager.I.Click(); }
    void Back()  { if (AudioManager.I) AudioManager.I.Back(); }

    void OnDisable() => RestoreGameUI();

    void Update()
    {
        if (!range) { Show(root, false); RestoreGameUI(); return; }

        // Интерфейс смены уходит на всё время тира, включая паузу внутри него.
        ApplyGameUI(range.PlayerFree);

        // Меню паузы главнее: пока оно открыто, тира на экране нет.
        bool visible = range.PlayerFree && !(pause && pause.IsPaused);
        Show(root, visible);
        if (!visible) return;

        var state = range.Current;
        bool ready    = state == ShootingRange.State.Ready;
        bool counting = state == ShootingRange.State.Countdown;
        bool running  = state == ShootingRange.State.Running;
        bool results  = state == ShootingRange.State.Results;
        bool tweak    = range.TweakMode;

        Show(topBar,        !results);
        Show(crosshair,     !results);
        Show(ammoRoot,      (counting || running) && range.HeldWeapon);
        Show(countdownRoot, counting && !tweak);
        Show(promptRoot,    ready && !string.IsNullOrEmpty(range.Prompt));
        Show(hintRoot,      ready);
        Show(setupWarnRoot, ready && !range.SceneReady);
        Show(resultsPanel,  results);
        Show(tweakPanel,    tweak);

        if (!results) UpdateTopBar(ready);
        if (!results) UpdateCrosshair();

        if (ready)
        {
            if (promptText) promptText.text = range.Prompt;
            if (hintText)
                hintText.text = "Возьми ствол со стойки — сразу пойдёт зачёт на время.   " +
                                "ЛКМ — огонь,   R — перезарядка,   Q — выйти из тира,   Esc — пауза.";
            if (setupWarnRoot && setupWarnRoot.activeSelf) UpdateSetupWarning();
        }

        if (counting && countdownText)
        {
            countdownText.text = range.CountdownLeft > 1f
                ? Mathf.CeilToInt(range.CountdownLeft).ToString()
                : "ОГОНЬ";
        }

        if ((counting || running) && range.HeldWeapon) UpdateAmmo();
        if (results) UpdateResults();
        if (tweak) UpdateTweak();
    }

    void UpdateTopBar(bool ready)
    {
        if (titleText) titleText.text = ready ? "СТРЕЛЬБИЩЕ" : "ТИР";

        if (timeText)
        {
            if (ready)
            {
                timeText.text = $"зачёт  {Mathf.RoundToInt(range.RoundDuration)} с";
                timeText.color = normalColor;
            }
            else
            {
                timeText.text = range.TimeLeftString;
                timeText.color = range.TimeLeft <= 10f ? badColor : normalColor;
            }
        }

        if (hitsText)     hitsText.text     = ready ? string.Empty : $"попаданий  {range.Hits}";
        if (shotsText)    shotsText.text    = ready ? string.Empty : $"выстрелов  {range.Shots}";
        if (accuracyText) accuracyText.text = ready ? string.Empty
                                                    : $"точность  {Mathf.RoundToInt(range.Accuracy * 100f)} %";
        if (scoreText)    scoreText.text    = ready ? $"рекорд  {range.BestScore}" : $"очки  {range.Score}";
    }

    void UpdateCrosshair()
    {
        if (crosshairBars == null) return;

        Color c = range.HitFlash ? crosshairHit : crosshairIdle;
        foreach (var bar in crosshairBars)
            if (bar) bar.color = c;
    }

    void UpdateAmmo()
    {
        var w = range.HeldWeapon;

        if (ammoText)
        {
            if (w.Reloading)
            {
                ammoText.text = "ПЕРЕЗАРЯДКА…";
                ammoText.color = warnColor;
            }
            else
            {
                ammoText.text = $"{w.Ammo} / {w.magazineSize}";
                ammoText.color = w.Ammo == 0 ? badColor
                               : w.Ammo <= w.magazineSize / 4 ? warnColor
                               : normalColor;
            }
        }

        if (ammoHint) ammoHint.text = $"{w.displayName}   [R] перезарядка   [Q] прервать";
    }

    void UpdateSetupWarning()
    {
        if (!setupWarnText) return;

        string t = "СЦЕНА НЕ НАСТРОЕНА\n";
        if (!range.playerCamera)
            t += "\n•  Не найдена камера игрока — заполни Player Camera или поставь тег MainCamera.";
        if (range.WeaponCount == 0)
            t += "\n•  Нет оружия: выдели стволы → Tools ▸ Стрельбище ▸ «2. Выделенное — это оружие».";
        if (range.TargetCount == 0)
            t += "\n•  Нет мишеней: выдели щиты и манекенов → Tools ▸ Стрельбище ▸ «3. Выделенное — это мишени».";

        setupWarnText.text = t;
        setupWarnText.color = badColor;
    }

    void UpdateResults()
    {
        if (resultsStats)
            resultsStats.text =
                $"Попаданий                 {range.Hits}\n" +
                $"Выстрелов                 {range.Shots}\n" +
                $"Точность                  {Mathf.RoundToInt(range.Accuracy * 100f)} %\n" +
                $"Очки                      {range.Score}\n" +
                $"Рекорд                    {range.BestScore}";

        if (resultsGrade) resultsGrade.text = "ОЦЕНКА   " + range.Grade;
    }

    void UpdateTweak()
    {
        if (!tweakText) return;

        var w = range.HeldWeapon;
        if (!w) { tweakText.text = "ПОДГОНКА ОРУЖИЯ\n\nОружие не в руках."; return; }

        string preset = w.orientationPreset < 0
            ? "авто"
            : $"{w.orientationPreset} из {RangeWeapon.PresetCount - 1}";

        tweakText.text =
            "ПОДГОНКА ОРУЖИЯ\n" +
            $"{w.displayName}   длина модели {w.BarrelLength:0.00} м\n\n" +
            $"Hold Position   {w.holdPosition.x:0.###}   {w.holdPosition.y:0.###}   {w.holdPosition.z:0.###}\n" +
            $"Hold Rotation   {w.holdRotation.x:0.#}   {w.holdRotation.y:0.#}   {w.holdRotation.z:0.#}\n" +
            $"Hold Scale      {w.holdScale:0.###}\n" +
            $"Разворот        {preset}\n\n" +
            "TAB — перебрать развороты, пока ствол не встанет прямо\n" +
            "Стрелки — двигать по X/Y,   PageUp/PageDown — по Z\n" +
            "I/K — наклон,   J/L — поворот,   U/O — крен\n" +
            "- / = — размер,   Backspace — сброс всего\n" +
            "Shift — быстрее,   Ctrl — точнее\n" +
            "Enter или F2 — вывести значения в консоль";
    }

    /// Прячем GameUI на время тира и возвращаем как было — а не просто включаем.
    void ApplyGameUI(bool rangeOpen)
    {
        if (!gameUiRoot) return;

        // Если сам лежишь внутри того, что собрался гасить, погаснешь вместе с ним —
        // и получится мигание. Такое бывает после старой сборки интерфейса.
        if (transform.IsChildOf(gameUiRoot.transform))
        {
            if (!_warnedSelfHide)
            {
                _warnedSelfHide = true;
                Debug.LogWarning($"[Тир] RangeUI лежит внутри «{gameUiRoot.name}», выключить его нельзя. " +
                                 "Пересобери интерфейс: Tools → Стрельбище → «4. Собрать интерфейс тира».", this);
            }
            return;
        }

        if (rangeOpen && !_gameUiHidden)
        {
            _gameUiWasOn = gameUiRoot.activeSelf;
            _gameUiHidden = true;
            if (_gameUiWasOn) gameUiRoot.SetActive(false);
        }
        else if (!rangeOpen)
        {
            RestoreGameUI();
        }
    }

    void RestoreGameUI()
    {
        if (!_gameUiHidden) return;
        _gameUiHidden = false;
        if (gameUiRoot && _gameUiWasOn) gameUiRoot.SetActive(true);
    }

    static void Show(GameObject go, bool on)
    {
        if (go && go.activeSelf != on) go.SetActive(on);
    }
}
