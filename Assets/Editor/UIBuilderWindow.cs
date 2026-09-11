using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Собирает Canvas-интерфейс, меню паузы, сцену главного меню и раскладывает звуки.
public class UIBuilderWindow : EditorWindow
{
    // ===== палитра =====
    static readonly Color ColPanel   = new Color(0.07f, 0.08f, 0.10f, 0.92f);
    static readonly Color ColPanelSoft = new Color(0.10f, 0.11f, 0.14f, 0.95f);
    static readonly Color Accent     = new Color(0.20f, 0.65f, 0.95f, 1f);
    static readonly Color BtnNormal  = new Color(0.16f, 0.18f, 0.22f, 1f);
    static readonly Color TextMain   = new Color(0.92f, 0.94f, 0.96f, 1f);
    static readonly Color TextDim    = new Color(0.62f, 0.66f, 0.72f, 1f);

    const string AudioFolder = "Assets/Audio";

    [MenuItem("Tools/Security Console/UI and Sound Setup")]
    static void Open() => GetWindow<UIBuilderWindow>("UI & Sound");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Открой игровую сцену (Demo) и собери геймплейный UI.\n" +
            "2. Создай сцену меню с нуля ИЛИ подключи логику к своей (2b).\n" +
            "3. Разложи звуки по компонентам.",
            MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("1.  Собрать геймплейный UI в этой сцене", GUILayout.Height(34)))
            BuildGameplayUI();

        GUILayout.Space(6);
        if (GUILayout.Button("2.  Создать сцену главного меню", GUILayout.Height(34)))
            BuildMainMenuScene();

        GUILayout.Space(6);
        if (GUILayout.Button("2b. Подключить логику к открытой сцене меню", GUILayout.Height(34)))
            ConfigureCurrentMenuScene();

        GUILayout.Space(6);
        if (GUILayout.Button("3.  Разложить звуки по компонентам", GUILayout.Height(34)))
            AssignSounds();

        GUILayout.Space(14);
        EditorGUILayout.LabelField("Звуки берутся из " + AudioFolder, EditorStyles.miniLabel);
    }

    // =====================================================================
    //  1. ГЕЙМПЛЕЙНЫЙ UI
    // =====================================================================

    static void BuildGameplayUI()
    {
        var old = GameObject.Find("GameUI_Canvas");
        if (old)
        {
            if (!EditorUtility.DisplayDialog("UI уже есть",
                "GameUI_Canvas в сцене уже присутствует. Пересобрать заново?", "Пересобрать", "Отмена"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        EnsureEventSystem();
        var audio = EnsureAudioManager();

        var canvasGO = NewCanvas("GameUI_Canvas", 0);
        var canvas = canvasGO.transform as RectTransform;

        var ui = Undo.AddComponent<GameUI>(canvasGO);

        // ---- верхняя строка ----
        var top = Panel(canvas, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-60f, 62f), ColPanelSoft);

        ui.clockText     = Label(top, "Clock",    "СМЕНА  08:00", 24, TextMain, TextAnchor.MiddleLeft,  new Vector2(28f, 0f),   new Vector2(320f, 40f), new Vector2(0f, 0.5f));
        ui.timeLeftText  = Label(top, "TimeLeft", "осталось  05:00", 24, TextMain, TextAnchor.MiddleLeft, new Vector2(360f, 0f), new Vector2(320f, 40f), new Vector2(0f, 0.5f));
        ui.checkedText   = Label(top, "Checked",  "проверено  0", 24, TextMain, TextAnchor.MiddleRight, new Vector2(-340f, 0f), new Vector2(300f, 40f), new Vector2(1f, 0.5f));
        ui.mistakesText  = Label(top, "Mistakes", "ошибки  0 / 3", 24, TextMain, TextAnchor.MiddleRight, new Vector2(-28f, 0f), new Vector2(300f, 40f), new Vector2(1f, 0.5f));

        // ---- панель проверки ----
        var check = Panel(canvas, "CheckPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(0f, 1f), new Vector2(30f, -96f), new Vector2(640f, 720f), ColPanel);
        ui.checkPanel = check.gameObject;

        ui.panelTitle = Label(check, "Title", "ПРОВЕРКА ДОКУМЕНТА", 30, Accent, TextAnchor.UpperLeft,
                              new Vector2(28f, -24f), new Vector2(580f, 42f), new Vector2(0f, 1f));

        // документ
        var doc = Group(check, "DocumentBlock", new Vector2(28f, -84f), new Vector2(580f, 260f));
        ui.documentBlock = doc.gameObject;
        ui.todayText   = Row(doc, "Today",   "Сегодня: 14.11.2026", 0, TextDim);
        ui.nameText    = Row(doc, "Name",    "ФИО",           1, TextMain);
        ui.birthText   = Row(doc, "Birth",   "Дата рождения", 2, TextMain);
        ui.idText      = Row(doc, "Id",      "Номер",         3, TextMain);
        ui.expiryText  = Row(doc, "Expiry",  "Годен до",      4, TextMain);
        ui.purposeText = Row(doc, "Purpose", "Цель визита",   5, TextMain);

        // статус рамки
        ui.scanStatusText = Label(check, "ScanStatus", "Рамка чистая", 28, TextMain, TextAnchor.UpperLeft,
                                  new Vector2(28f, -84f), new Vector2(580f, 40f), new Vector2(0f, 1f));

        // вещи
        var items = Group(check, "ItemsBlock", new Vector2(28f, -140f), new Vector2(580f, 340f));
        ui.itemsBlock = items.gameObject;
        Label(items, "ItemsCaption", "Вещи на столе:", 22, TextDim, TextAnchor.UpperLeft,
              new Vector2(0f, 0f), new Vector2(580f, 32f), new Vector2(0f, 1f));

        var list = Group(items, "ItemsContainer", new Vector2(0f, -40f), new Vector2(580f, 300f));
        var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;   vlg.childForceExpandWidth = true;
        vlg.spacing = 6f;
        ui.itemsContainer = list;

        var tmpl = Label(list, "ItemRowTemplate", "•  предмет", 22, TextMain, TextAnchor.MiddleLeft,
                         Vector2.zero, new Vector2(560f, 30f), new Vector2(0f, 1f));
        var le = tmpl.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 30f; le.preferredHeight = 30f;
        ui.itemRowTemplate = tmpl.gameObject;
        tmpl.gameObject.SetActive(false);

        // кнопки
        var docBtns = Group(check, "DocumentButtons", new Vector2(28f, 24f), new Vector2(580f, 62f), true);
        ui.documentButtons = docBtns.gameObject;
        ui.toScannerButton = Btn(docBtns, "ToScanner", "К рамке",    new Vector2(0f, 0f),   new Vector2(282f, 58f));
        ui.turnAwayButton  = Btn(docBtns, "TurnAway",  "Развернуть", new Vector2(298f, 0f), new Vector2(282f, 58f));

        var inspBtns = Group(check, "InspectionButtons", new Vector2(28f, 24f), new Vector2(580f, 62f), true);
        ui.inspectionButtons = inspBtns.gameObject;
        ui.passButton   = Btn(inspBtns, "Pass",   "Пропустить", new Vector2(0f, 0f),   new Vector2(282f, 58f));
        ui.detainButton = Btn(inspBtns, "Detain", "Задержать",  new Vector2(298f, 0f), new Vector2(282f, 58f));

        ui.closeHintText = Label(check, "CloseHint", "[Esc] закрыть панель", 18, TextDim, TextAnchor.LowerLeft,
                                 new Vector2(28f, 96f), new Vector2(400f, 28f), new Vector2(0f, 0f));

        // ---- журнал ----
        var logPanel = Panel(canvas, "LogPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                             new Vector2(1f, 1f), new Vector2(-30f, -96f), new Vector2(500f, 560f), ColPanel);
        Label(logPanel, "LogTitle", "ЖУРНАЛ СОБЫТИЙ", 24, Accent, TextAnchor.UpperLeft,
              new Vector2(24f, -20f), new Vector2(450f, 34f), new Vector2(0f, 1f));
        ui.logText = Label(logPanel, "LogText", "", 18, TextDim, TextAnchor.UpperLeft,
                           new Vector2(24f, -62f), new Vector2(452f, 480f), new Vector2(0f, 1f));

        // ---- прицел и подсказка ----
        var cross = NewImage(canvas, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f),
                             new Color(1f, 1f, 1f, 0.6f));
        ui.crosshair = cross.gameObject;

        var prompt = Panel(canvas, "Prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                           new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(560f, 62f), ColPanelSoft);
        ui.promptRoot = prompt.gameObject;
        ui.promptText = Label(prompt, "PromptText", "[E]  Проверить документы", 26, TextMain, TextAnchor.MiddleCenter,
                              Vector2.zero, new Vector2(540f, 46f), new Vector2(0.5f, 0.5f));

        // ---- итоги ----
        var res = Panel(canvas, "ResultsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 540f), ColPanel);
        ui.resultsPanel = res.gameObject;
        ui.resultsTitle = Label(res, "Title", "СМЕНА ОКОНЧЕНА", 40, TextMain, TextAnchor.UpperCenter,
                                new Vector2(0f, -34f), new Vector2(660f, 56f), new Vector2(0.5f, 1f));
        ui.resultsStats = Label(res, "Stats", "", 24, TextMain, TextAnchor.UpperLeft,
                                new Vector2(60f, -120f), new Vector2(600f, 180f), new Vector2(0f, 1f));
        ui.resultsGrade = Label(res, "Grade", "ОЦЕНКА   —", 34, Accent, TextAnchor.MiddleCenter,
                                new Vector2(0f, -330f), new Vector2(660f, 50f), new Vector2(0.5f, 1f));
        ui.rangeButton   = Btn(res, "Range",   "Пострелять в тире",  new Vector2(0f, 180f), new Vector2(400f, 58f), new Vector2(0.5f, 0f));
        ui.restartButton = Btn(res, "Restart", "Начать смену заново", new Vector2(0f, 110f), new Vector2(400f, 58f), new Vector2(0.5f, 0f));
        ui.menuButton    = Btn(res, "ToMenu",  "В главное меню",      new Vector2(0f, 40f),  new Vector2(400f, 58f), new Vector2(0.5f, 0f));
        res.gameObject.SetActive(false);

        // ---- пауза ----
        var pauseGO = NewCanvas("PauseUI_Canvas", 10);
        var pauseRoot = pauseGO.transform as RectTransform;
        var pause = Undo.AddComponent<PauseMenu>(pauseGO);

        var dim = NewImage(pauseRoot, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim);

        var pausePanel = Panel(dim, "PausePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 480f), ColPanel);
        pause.pausePanel = pausePanel.gameObject;
        Label(pausePanel, "Title", "ПАУЗА", 40, Accent, TextAnchor.UpperCenter,
              new Vector2(0f, -30f), new Vector2(460f, 56f), new Vector2(0.5f, 1f));
        pause.resumeButton   = Btn(pausePanel, "Resume",   "Продолжить",    new Vector2(0f, -120f), new Vector2(420f, 62f), new Vector2(0.5f, 1f));
        pause.settingsButton = Btn(pausePanel, "Settings", "Настройки",     new Vector2(0f, -196f), new Vector2(420f, 62f), new Vector2(0.5f, 1f));
        pause.menuButton     = Btn(pausePanel, "ToMenu",   "В главное меню",new Vector2(0f, -272f), new Vector2(420f, 62f), new Vector2(0.5f, 1f));
        pause.quitButton     = Btn(pausePanel, "Quit",     "Выйти из игры", new Vector2(0f, -348f), new Vector2(420f, 62f), new Vector2(0.5f, 1f));

        var settings = BuildSettingsPanel(dim, out var backBtn);
        pause.settingsPanel = settings.gameObject;
        pause.settingsBackButton = backBtn;

        var player = GameObject.Find("Player");
        if (player) pause.objectsToFreeze.Add(player);

        dim.gameObject.SetActive(false);
        pausePanel.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);

        // старый OnGUI-худ выключаем
        var legacy = Object.FindFirstObjectByType<GameHUD>();
        if (legacy)
        {
            Undo.RecordObject(legacy, "Disable legacy HUD");
            legacy.enabled = false;
        }

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[UIBuilder] Геймплейный UI собран. Старый GameHUD выключен.");
    }

    // =====================================================================
    //  2. СЦЕНА ГЛАВНОГО МЕНЮ
    // =====================================================================

    static void BuildMainMenuScene()
    {
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Scenes");
        string path = dir + "/MainMenu.unity";

        if (System.IO.File.Exists(path) &&
            !EditorUtility.DisplayDialog("Сцена есть", "MainMenu.unity уже существует. Перезаписать?", "Перезаписать", "Отмена"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        EnsureEventSystem();
        EnsureAudioManager();

        var canvasGO = NewCanvas("MainMenu_Canvas", 0);
        var root = canvasGO.transform as RectTransform;
        var menu = canvasGO.AddComponent<MainMenu>();

        var bg = NewImage(root, "Background", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                          new Color(0.05f, 0.06f, 0.08f, 1f));
        Stretch(bg);

        var panel = Group(bg, "MenuPanel", Vector2.zero, new Vector2(560f, 620f));
        Center(panel);
        menu.rootPanel = panel.gameObject;

        Label(panel, "Title", "SECURITY CONSOLE", 54, Accent, TextAnchor.UpperCenter,
              new Vector2(0f, -20f), new Vector2(560f, 70f), new Vector2(0.5f, 1f));
        Label(panel, "Subtitle", "симулятор контрольно-пропускного пункта", 22, TextDim, TextAnchor.UpperCenter,
              new Vector2(0f, -90f), new Vector2(560f, 34f), new Vector2(0.5f, 1f));

        menu.playButton     = Btn(panel, "Play",     "Играть",    new Vector2(0f, -220f), new Vector2(420f, 66f), new Vector2(0.5f, 1f));
        menu.settingsButton = Btn(panel, "Settings", "Настройки", new Vector2(0f, -300f), new Vector2(420f, 66f), new Vector2(0.5f, 1f));
        menu.quitButton     = Btn(panel, "Quit",     "Выход",     new Vector2(0f, -380f), new Vector2(420f, 66f), new Vector2(0.5f, 1f));

        var settings = BuildSettingsPanel(bg, out var back);
        menu.settingsPanel = settings.gameObject;
        menu.settingsBackButton = back;
        settings.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene, path);
        AddScenesToBuild(path);

        Debug.Log("[UIBuilder] Сцена главного меню создана: " + path);
    }

    static void AddScenesToBuild(string menuPath)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        void Ensure(string p, int index)
        {
            if (string.IsNullOrEmpty(p) || !System.IO.File.Exists(p)) return;
            int existing = list.FindIndex(s => s.path == p);
            if (existing >= 0) list.RemoveAt(existing);
            list.Insert(Mathf.Clamp(index, 0, list.Count), new EditorBuildSettingsScene(p, true));
        }

        Ensure(menuPath, 0);
        Ensure("Assets/Scenes/Demo.unity", 1);

        EditorBuildSettings.scenes = list.ToArray();
    }

    // =====================================================================
    //  2b. ПОДКЛЮЧИТЬ ЛОГИКУ К ГОТОВОЙ СЦЕНЕ МЕНЮ
    // =====================================================================

    static void ConfigureCurrentMenuScene()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            EditorUtility.DisplayDialog("Нет Canvas",
                "В открытой сцене не найден Canvas. Открой сцену своего меню и повтори.", "Ок");
            return;
        }

        EnsureEventSystem();
        EnsureAudioManager();

        var menu = canvas.GetComponent<MainMenu>();
        if (!menu) menu = Undo.AddComponent<MainMenu>(canvas.gameObject);
        Undo.RecordObject(menu, "Configure main menu");

        // кнопки ищем по имени
        var found = new List<string>();
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
        {
            string n = b.name.ToLowerInvariant();

            if (n.Contains("start") || n.Contains("play") || n.Contains("игр"))
            { menu.playButton = b; found.Add("Играть = " + b.name); }
            else if (n.Contains("option") || n.Contains("setting") || n.Contains("настр"))
            { menu.settingsButton = b; found.Add("Настройки = " + b.name); }
            else if (n.Contains("quit") || n.Contains("exit") || n.Contains("выход"))
            { menu.quitButton = b; found.Add("Выход = " + b.name); }
        }

        // общий контейнер кнопок, если он есть и это не сам Canvas
        if (menu.playButton)
        {
            var parent = menu.playButton.transform.parent;
            menu.rootPanel = (parent && parent != canvas.transform) ? parent.gameObject : null;
        }

        // панель настроек: старую снимаем, строим свежую
        var old = canvas.transform.Find("SettingsPanel");
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        var settings = BuildSettingsPanel(canvas.transform, out var back);
        menu.settingsPanel = settings.gameObject;
        menu.settingsBackButton = back;
        settings.gameObject.SetActive(false);

        EditorUtility.SetDirty(menu);
        AddScenesToBuild(SceneManager.GetActiveScene().path);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string report = found.Count > 0 ? string.Join("\n", found) : "кнопки по именам не опознаны";
        Debug.Log("[UIBuilder] Меню подключено.\n" + report);

        EditorUtility.DisplayDialog("Готово",
            "Логика подключена.\n\n" + report +
            "\n\nЕсли кнопка не опозналась - перетащи её в поле компонента MainMenu на объекте Canvas вручную.",
            "Ок");
    }

    // =====================================================================
    //  3. ЗВУКИ
    // =====================================================================

    static void AssignSounds()
    {
        var am = EnsureAudioManager();
        Undo.RecordObject(am, "Assign sounds");

        am.uiClick      = Clip("ui_click");
        am.uiBack       = Clip("ui_back");
        am.uiHover      = Clip("ui_hover");
        am.stampApprove = Clip("stamp_approve");
        am.stampReject  = Clip("stamp_reject");
        am.mistake      = Clip("mistake");
        am.scanStart    = Clip("scan_start");
        am.scanClean    = Clip("scan_clean");
        am.scanAlarm    = Clip("scan_alarm");
        am.doorOpen     = Clip("door_open");
        am.doorClose    = Clip("door_close");
        am.doorLocked   = Clip("door_locked");
        am.shiftStart   = Clip("shift_start");
        am.shiftEnd     = Clip("shift_end");

        EditorUtility.SetDirty(am);

        int doors = 0;
        foreach (var d in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(d, "Door sounds");

            if (!d.audioSource)
            {
                var src = Undo.AddComponent<AudioSource>(d.gameObject);
                src.playOnAwake = false;
                src.spatialBlend = 1f;       // 3D, слышно рядом с дверью
                src.minDistance = 1.5f;
                src.maxDistance = 18f;
                d.audioSource = src;
            }

            d.openClip   = am.doorOpen;
            d.closeClip  = am.doorClose;
            d.lockedClip = am.doorLocked;

            EditorUtility.SetDirty(d);
            doors++;
        }

        int frames = 0;
        foreach (var m in Object.FindObjectsByType<MetalDetectorVisual>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(m, "Detector sounds");

            if (!m.audioSource)
            {
                var src = Undo.AddComponent<AudioSource>(m.gameObject);
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.minDistance = 2f;
                src.maxDistance = 25f;
                m.audioSource = src;
            }

            m.alarmClip = am.scanAlarm;
            m.cleanClip = am.scanClean;

            EditorUtility.SetDirty(m);
            frames++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[UIBuilder] Звуки разложены. Дверей: {doors}, рамок: {frames}.");
    }

    static AudioClip Clip(string name)
    {
        string path = $"{AudioFolder}/{name}.wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (!clip) Debug.LogWarning("[UIBuilder] Не найден звук " + path);
        return clip;
    }

    // =====================================================================
    //  ХЕЛПЕРЫ
    // =====================================================================

    static Font _font;
    static Font UIFont
    {
        get
        {
            if (_font) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!_font) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>()) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");

        var inputModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModule != null) go.AddComponent(inputModule);
        else go.AddComponent<StandaloneInputModule>();
    }

    static AudioManager EnsureAudioManager()
    {
        var am = Object.FindFirstObjectByType<AudioManager>();
        if (am) return am;

        var go = new GameObject("AudioManager");
        Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
        return go.AddComponent<AudioManager>();
    }

    static GameObject NewCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create canvas");

        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;

        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight = 0.5f;

        return go;
    }

    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform NewImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var rt = NewRect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                               Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var rt = NewRect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Group(Transform parent, string name, Vector2 pos, Vector2 size, bool bottom = false)
    {
        var rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, bottom ? 0f : 1f);
        rt.pivot = new Vector2(0f, bottom ? 0f : 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Text Label(Transform parent, string name, string text, int size, Color color,
                      TextAnchor anchor, Vector2 pos, Vector2 rect, Vector2 pivot)
    {
        var rt = NewRect(parent, name);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = UIFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = true;

        rt.anchorMin = rt.anchorMax = pivot;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = rect;
        return t;
    }

    static Text Row(Transform parent, string name, string text, int index, Color color)
    {
        return Label(parent, name, text, 22, color, TextAnchor.MiddleLeft,
                     new Vector2(0f, -index * 36f - 4f), new Vector2(580f, 32f), new Vector2(0f, 1f));
    }

    static Button Btn(Transform parent, string name, string caption, Vector2 pos, Vector2 size)
        => Btn(parent, name, caption, pos, size, new Vector2(0f, 0f));

    static Button Btn(Transform parent, string name, string caption, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var rt = NewRect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = BtnNormal;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.4f, 1f);
        colors.pressedColor = new Color(0.75f, 0.8f, 0.9f, 1f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        rt.anchorMin = rt.anchorMax = pivot;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var label = Label(rt, "Label", caption, 24, TextMain, TextAnchor.MiddleCenter,
                          Vector2.zero, size, new Vector2(0.5f, 0.5f));
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        return btn;
    }

    static RectTransform BuildSettingsPanel(Transform parent, out Button backButton)
    {
        var panel = Panel(parent, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 520f), ColPanel);

        var sp = panel.gameObject.AddComponent<SettingsPanel>();

        Label(panel, "Title", "НАСТРОЙКИ", 38, Accent, TextAnchor.UpperCenter,
              new Vector2(0f, -28f), new Vector2(580f, 52f), new Vector2(0.5f, 1f));

        sp.masterSlider = SliderRow(panel, "Master", "Общая громкость", -120f, out sp.masterValue);
        sp.musicSlider  = SliderRow(panel, "Music",  "Музыка",          -200f, out sp.musicValue);
        sp.sfxSlider    = SliderRow(panel, "Sfx",    "Эффекты",         -280f, out sp.sfxValue);

        // полноэкранный режим
        var tRect = NewRect(panel, "Fullscreen");
        tRect.anchorMin = tRect.anchorMax = tRect.pivot = new Vector2(0f, 1f);
        tRect.anchoredPosition = new Vector2(40f, -360f);
        tRect.sizeDelta = new Vector2(560f, 40f);

        var toggle = tRect.gameObject.AddComponent<Toggle>();
        var box = NewImage(tRect, "Box", new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(26f, 26f), BtnNormal);
        box.GetComponent<Image>().raycastTarget = true;
        var mark = NewImage(box, "Check", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f), Accent);
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = mark.GetComponent<Image>();
        Label(tRect, "Label", "Полноэкранный режим", 22, TextMain, TextAnchor.MiddleLeft,
              new Vector2(48f, 0f), new Vector2(420f, 32f), new Vector2(0f, 0.5f));
        sp.fullscreenToggle = toggle;

        backButton = Btn(panel, "Back", "Назад", new Vector2(0f, 32f), new Vector2(300f, 58f), new Vector2(0.5f, 0f));
        return panel;
    }

    static Slider SliderRow(Transform parent, string name, string caption, float y, out Text valueLabel)
    {
        var row = NewRect(parent, name);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(40f, y);
        row.sizeDelta = new Vector2(560f, 60f);

        Label(row, "Caption", caption, 22, TextMain, TextAnchor.MiddleLeft,
              new Vector2(0f, 14f), new Vector2(400f, 28f), new Vector2(0f, 0.5f));
        valueLabel = Label(row, "Value", "100 %", 20, TextDim, TextAnchor.MiddleRight,
                           new Vector2(560f, 14f), new Vector2(120f, 28f), new Vector2(1f, 0.5f));

        var sRect = NewRect(row, "Slider");
        sRect.anchorMin = sRect.anchorMax = sRect.pivot = new Vector2(0f, 0.5f);
        sRect.anchoredPosition = new Vector2(0f, -16f);
        sRect.sizeDelta = new Vector2(560f, 20f);

        var slider = sRect.gameObject.AddComponent<Slider>();

        var bg = NewImage(sRect, "Background", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, BtnNormal);
        Stretch(bg);
        bg.GetComponent<Image>().raycastTarget = true;

        var fillArea = NewRect(sRect, "Fill Area");
        Stretch(fillArea);
        var fill = NewImage(fillArea, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Accent);
        Stretch(fill);

        var handleArea = NewRect(sRect, "Handle Slide Area");
        Stretch(handleArea);
        var handle = NewImage(handleArea, "Handle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 30f), TextMain);
        handle.GetComponent<Image>().raycastTarget = true;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        return slider;
    }
}
