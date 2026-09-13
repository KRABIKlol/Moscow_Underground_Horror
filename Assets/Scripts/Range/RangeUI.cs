using UnityEngine;
using UnityEngine.UI;


public class RangeUI : MonoBehaviour
{
    
    public ShootingRange range;
    public PauseMenu pause;

   
    public GameObject gameUiRoot;

    
    public GameObject root;



    public GameObject topBar;
    public Text titleText;
    public Text timeText;
    public Text hitsText;
    public Text shotsText;
    public Text accuracyText;
    public Text scoreText;

    public GameObject crosshair;
    public Graphic[] crosshairBars;

    public GameObject promptRoot;
    public Text promptText;

   
    public GameObject ammoRoot;
    public Text ammoText;
    public Text ammoHint;


    public GameObject countdownRoot;
    public Text countdownText;

   
    public GameObject hintRoot;
    public Text hintText;
   

    public GameObject resultsPanel;
    public Text resultsStats;
    public Text resultsGrade;
    public Button againButton;
    public Button backButton;

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

     
        ApplyGameUI(range.PlayerFree);

      
        bool visible = range.PlayerFree && !(pause && pause.IsPaused);
        Show(root, visible);
        if (!visible) return;

        var state = range.Current;
        bool ready    = state == ShootingRange.State.Ready;
        bool counting = state == ShootingRange.State.Countdown;
        bool running  = state == ShootingRange.State.Running;
        bool results  = state == ShootingRange.State.Results;        

        Show(topBar,        !results);
        Show(crosshair,     !results);
        Show(ammoRoot,      (counting || running) && range.HeldWeapon);
        Show(countdownRoot, counting);
        Show(promptRoot,    ready && !string.IsNullOrEmpty(range.Prompt));
        Show(hintRoot,      ready);        
        Show(resultsPanel,  results);

        if (!results) UpdateTopBar(ready);
        if (!results) UpdateCrosshair();

        if (ready)
        {
            if (promptText) promptText.text = range.Prompt;
            if (hintText)
                hintText.text = "Возьми ствол со стойки — сразу пойдёт зачёт на время.   " +
                                "ЛКМ — огонь,   R — перезарядка,   Q — выйти из тира,   Esc — пауза.";
          
        }

        if (counting && countdownText)
        {
            countdownText.text = range.CountdownLeft > 1f
                ? Mathf.CeilToInt(range.CountdownLeft).ToString()
                : "ОГОНЬ";
        }

        if ((counting || running) && range.HeldWeapon) UpdateAmmo();
        if (results) UpdateResults();        
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
    

   
    void ApplyGameUI(bool rangeOpen)
    {
        if (!gameUiRoot) return;     
       
      

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
