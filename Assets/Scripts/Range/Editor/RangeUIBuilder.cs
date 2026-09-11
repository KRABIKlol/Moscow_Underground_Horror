using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Собирает интерфейс стрельбища на Canvas в том же виде, что и GameUI:
/// шрифт, спрайты панелей и кнопок берутся прямо из существующего интерфейса сцены,
/// цвета и размеры — те же, что у строки смены и журнала событий.
public static class RangeUIBuilder
{
    const string Menu = "Tools/Стрельбище/";
    const string RootName = "RangeUI_Canvas";

    // Замерено по GameUI: строка смены, журнал, панель итогов.
    static readonly Color PanelBar   = new Color32(0x19, 0x1C, 0x23, 242);   // 0.95
    static readonly Color PanelDark  = new Color32(0x11, 0x14, 0x19, 235);   // 0.92
    static readonly Color TextMain   = new Color32(0xEA, 0xEF, 0xF4, 255);
    static readonly Color TextDim    = new Color32(0x9E, 0xA8, 0xB7, 255);
    static readonly Color Accent     = new Color32(0x33, 0xA5, 0xF2, 255);
    static readonly Color WarnColor  = new Color32(0xFF, 0xD8, 0x73, 255);
    static readonly Color BadColor   = new Color32(0xFF, 0x66, 0x66, 255);
    static readonly Color GoodColor  = new Color32(0x8C, 0xFF, 0x99, 255);
    static readonly Color BtnNormal  = new Color32(0x28, 0x2D, 0x38, 255);
    static readonly Color BtnHover   = new Color32(0x35, 0x3C, 0x4A, 255);
    static readonly Color BtnPressed = new Color32(0x1B, 0x1F, 0x27, 255);

    const int SizeTitle  = 40;
    const int SizeGrade  = 34;
    const int SizeBig    = 26;
    const int SizeBody   = 24;
    const int SizeSmall  = 18;

    static Font _font;
    static Sprite _panelSprite, _buttonSprite;

    [MenuItem(Menu + "4. Собрать интерфейс тира", false, 4)]
    static void Build()
    {
        var existing = Object.FindFirstObjectByType<RangeUI>(FindObjectsInactive.Include);
        if (existing)
        {
            if (!EditorUtility.DisplayDialog("Тир",
                    "Интерфейс тира в сцене уже есть. Собрать заново?\n\n" +
                    "Старый объект будет удалён вместе с ручными правками.",
                    "Собрать заново", "Отмена"))
                return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var gameUI = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
        var pauseMenu = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);

        CollectStyle(gameUI);

        // Свой канвас, как у GameUI и меню паузы: порядок отрисовки между ними.
        var rootGo = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(rootGo, "Собрать интерфейс тира");

        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PickSortingOrder(gameUI, pauseMenu);

        CopyScaler(rootGo.GetComponent<CanvasScaler>(), gameUI, pauseMenu);
        EnsureEventSystem();

        var rootRt = (RectTransform)rootGo.transform;

        var ui = rootGo.AddComponent<RangeUI>();
        ui.range = Object.FindFirstObjectByType<ShootingRange>(FindObjectsInactive.Include);
        ui.pause = pauseMenu;
        if (gameUI) ui.gameUiRoot = gameUI.gameObject;

        ui.normalColor = TextMain;
        ui.warnColor = WarnColor;
        ui.badColor = BadColor;
        ui.goodColor = GoodColor;
        ui.crosshairIdle = new Color(TextMain.r, TextMain.g, TextMain.b, 0.7f);
        ui.crosshairHit = BadColor;

        var content = Stretch(new GameObject("Content", typeof(RectTransform)), rootRt);
        ui.root = content.gameObject;

        BuildTopBar(ui, content);
        BuildCrosshair(ui, content);
        BuildPrompt(ui, content);
        BuildAmmo(ui, content);
        BuildCountdown(ui, content);
        BuildHint(ui, content);
        BuildSetupWarning(ui, content);
        BuildResults(ui, content);
        BuildTweak(ui, content);

        // В редакторе оставляем видимой основную часть — так разметку видно и можно править.
        // В игре RangeUI прячет Content целиком и показывает нужное по состоянию.
        ui.promptRoot.SetActive(false);
        ui.countdownRoot.SetActive(false);
        ui.setupWarnRoot.SetActive(false);
        ui.resultsPanel.SetActive(false);
        ui.tweakPanel.SetActive(false);

        Selection.activeGameObject = rootGo;
        EditorUtility.SetDirty(ui);
        MarkDirty();

        Debug.Log($"[Тир] Интерфейс собран: канвас «{RootName}», порядок отрисовки {canvas.sortingOrder}, " +
                  $"шрифт «{(_font ? _font.name : "не найден")}». " +
                  (ui.gameUiRoot ? $"На время тира выключается «{ui.gameUiRoot.name}». " : "GameUI в сцене не найден. ") +
                  (ui.range ? "Менеджер тира подключён." : "Менеджера ShootingRange нет — создай его пунктом 1."),
                  rootGo);
    }

    /// Шрифт и спрайты берём прямо у GameUI, чтобы тир не отличался ни на пиксель.
    static void CollectStyle(GameUI gameUI)
    {
        _font = null;
        _panelSprite = null;
        _buttonSprite = null;

        if (gameUI)
        {
            if (gameUI.clockText && gameUI.clockText.font) _font = gameUI.clockText.font;
            else if (gameUI.logText && gameUI.logText.font) _font = gameUI.logText.font;

            _panelSprite = SpriteOf(gameUI.resultsPanel) ?? SpriteOf(gameUI.checkPanel) ?? SpriteOf(gameUI.promptRoot);
            if (gameUI.restartButton) _buttonSprite = SpriteOf(gameUI.restartButton.gameObject);
        }

        if (!_font)
            foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t && t.font) { _font = t.font; break; }

        if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!_font) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (!_panelSprite) _panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (!_buttonSprite) _buttonSprite = _panelSprite;
    }

    static Sprite SpriteOf(GameObject go)
    {
        if (!go) return null;
        var img = go.GetComponent<Image>();
        return img ? img.sprite : null;
    }

    // ===== блоки =====

    static void BuildTopBar(RangeUI ui, RectTransform parent)
    {
        // Геометрия один в один со строкой смены из GameUI.
        var bar = Rect("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                       new Vector2(0f, -14f), new Vector2(-60f, 62f));
        Panel(bar, PanelBar);
        ui.topBar = bar.gameObject;

        ui.titleText    = BarLabel(bar, "Title",     30f, 280f, Accent,  "ТИР");
        ui.timeText     = BarLabel(bar, "Time",     320f, 230f, TextMain, "00:47");
        ui.hitsText     = BarLabel(bar, "Hits",     560f, 250f, TextMain, "попаданий  12");
        ui.shotsText    = BarLabel(bar, "Shots",    820f, 250f, TextMain, "выстрелов  18");
        ui.accuracyText = BarLabel(bar, "Accuracy", 1080f, 270f, TextMain, "точность  67 %");

        var score = Rect("Score", bar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                         new Vector2(-30f, 0f), new Vector2(260f, 0f));
        ui.scoreText = Label(score, "очки  12", SizeBody, TextAnchor.MiddleRight, TextMain);
    }

    static Text BarLabel(RectTransform bar, string name, float x, float width, Color color, string sample)
    {
        var rt = Rect(name, bar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                      new Vector2(x, 0f), new Vector2(width, 0f));
        return Label(rt, sample, SizeBody, TextAnchor.MiddleLeft, color);
    }

    static void BuildCrosshair(RangeUI ui, RectTransform parent)
    {
        var root = Rect("Crosshair", parent, Center, Center, Center, Vector2.zero, new Vector2(64f, 64f));
        ui.crosshair = root.gameObject;

        ui.crosshairBars = new Graphic[]
        {
            Bar(root, "Left",  new Vector2(-14f, 0f), new Vector2(12f, 2f)),
            Bar(root, "Right", new Vector2( 14f, 0f), new Vector2(12f, 2f)),
            Bar(root, "Up",    new Vector2(0f,  14f), new Vector2(2f, 12f)),
            Bar(root, "Down",  new Vector2(0f, -14f), new Vector2(2f, 12f)),
        };
    }

    static Graphic Bar(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent, Center, Center, Center, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.7f);
        img.raycastTarget = false;
        return img;
    }

    static void BuildPrompt(RangeUI ui, RectTransform parent)
    {
        // Та же подсказка и на том же месте, что у поста охраны.
        var rt = Rect("Prompt", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                      new Vector2(0f, 200f), new Vector2(560f, 62f));
        Panel(rt, PanelBar);
        ui.promptRoot = rt.gameObject;
        ui.promptText = Label(Inset(rt, 16f), "[E]  Взять АК-74", SizeBig, TextAnchor.MiddleCenter, TextMain);
    }

    static void BuildAmmo(RangeUI ui, RectTransform parent)
    {
        var rt = Rect("Ammo", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                      new Vector2(-30f, 30f), new Vector2(380f, 110f));
        Panel(rt, PanelDark);
        ui.ammoRoot = rt.gameObject;

        var top = Rect("Value", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                       new Vector2(0f, -16f), new Vector2(-40f, 42f));
        ui.ammoText = Label(top, "30 / 30", SizeGrade, TextAnchor.MiddleLeft, TextMain);

        var bottom = Rect("Hint", rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(0f, 18f), new Vector2(-40f, 28f));
        ui.ammoHint = Label(bottom, "АК-74   [R] перезарядка   [Q] прервать",
                            SizeSmall, TextAnchor.MiddleLeft, TextDim);
    }

    static void BuildCountdown(RangeUI ui, RectTransform parent)
    {
        var rt = Rect("Countdown", parent, Center, Center, Center, new Vector2(0f, 180f), new Vector2(700f, 170f));
        ui.countdownRoot = rt.gameObject;
        ui.countdownText = Label(rt, "3", 110, TextAnchor.MiddleCenter, Accent);
    }

    static void BuildHint(RangeUI ui, RectTransform parent)
    {
        var rt = Rect("Hint", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                      new Vector2(0f, 30f), new Vector2(-60f, 56f));
        Panel(rt, PanelBar);
        ui.hintRoot = rt.gameObject;
        ui.hintText = Label(Inset(rt, 20f),
                            "Возьми ствол со стойки — сразу пойдёт зачёт на время.   " +
                            "ЛКМ — огонь,   R — перезарядка,   Q — выйти из тира,   Esc — пауза.",
                            SizeSmall, TextAnchor.MiddleCenter, TextDim);
    }

    static void BuildSetupWarning(RangeUI ui, RectTransform parent)
    {
        var rt = Rect("SetupWarning", parent, Center, Center, Center, new Vector2(0f, 120f), new Vector2(940f, 200f));
        Panel(rt, PanelDark);
        ui.setupWarnRoot = rt.gameObject;
        ui.setupWarnText = Label(Inset(rt, 24f),
                                 "СЦЕНА НЕ НАСТРОЕНА\n\n•  Нет оружия\n•  Нет мишеней",
                                 SizeBody, TextAnchor.MiddleLeft, BadColor);
    }

    static void BuildResults(RangeUI ui, RectTransform parent)
    {
        // Размер как у панели итогов смены.
        var rt = Rect("Results", parent, Center, Center, Center, Vector2.zero, new Vector2(720f, 540f));
        Panel(rt, PanelDark);
        ui.resultsPanel = rt.gameObject;

        var title = Rect("Title", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(0f, -34f), new Vector2(-72f, 52f));
        Label(title, "ЗАЧЁТ ОКОНЧЕН", SizeTitle, TextAnchor.MiddleLeft, TextMain);

        var stats = Rect("Stats", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(0f, -108f), new Vector2(-72f, 190f));
        ui.resultsStats = Label(stats,
                                "Попаданий                 30\n" +
                                "Выстрелов                 30\n" +
                                "Точность                  100 %\n" +
                                "Очки                      30\n" +
                                "Рекорд                    80",
                                SizeBody, TextAnchor.UpperLeft, TextMain);

        var grade = Rect("Grade", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(0f, -308f), new Vector2(-72f, 46f));
        ui.resultsGrade = Label(grade, "ОЦЕНКА   B", SizeGrade, TextAnchor.MiddleLeft, Accent);

        ui.againButton = MakeButton(rt, "AgainButton", "Ещё раз", 88f);
        ui.backButton  = MakeButton(rt, "BackButton",  "К итогам смены", 26f);
    }

    static void BuildTweak(RangeUI ui, RectTransform parent)
    {
        // В том же углу, где панель проверки документов.
        var rt = Rect("Tweak", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                      new Vector2(30f, -96f), new Vector2(700f, 470f));
        Panel(rt, PanelDark);
        ui.tweakPanel = rt.gameObject;
        ui.tweakText = Label(Inset(rt, 24f),
                             "ПОДГОНКА ОРУЖИЯ\nАК-74   длина модели 0.87 м\n\n" +
                             "Hold Position   0   0   0\nHold Rotation   0   0   0\n" +
                             "Hold Scale      1\nРазворот        авто",
                             SizeSmall, TextAnchor.UpperLeft, TextMain);
    }

    // ===== примитивы =====

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
                              Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        return rt;
    }

    static RectTransform Stretch(GameObject go, Transform parent)
    {
        var rt = go.GetComponent<RectTransform>();
        if (!rt) rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Center;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    static RectTransform Inset(RectTransform parent, float pad)
    {
        var rt = Rect("Text", parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    static Image Panel(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = _panelSprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(RectTransform rt, string text, int size, TextAnchor anchor, Color color)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.font = _font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(RectTransform parent, string name, string caption, float y)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                      new Vector2(0f, y), new Vector2(320f, 52f));

        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        img.sprite = _buttonSprite;
        img.type = Image.Type.Sliced;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = BtnNormal;
        colors.highlightedColor = BtnHover;
        colors.pressedColor = BtnPressed;
        colors.selectedColor = BtnNormal;
        colors.disabledColor = new Color32(0x1B, 0x1F, 0x27, 150);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var label = Rect("Label", rt, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        label.offsetMin = Vector2.zero;
        label.offsetMax = Vector2.zero;
        Label(label, caption, SizeBody, TextAnchor.MiddleCenter, TextMain);

        return btn;
    }

    // ===== окружение =====

    /// Между GameUI и меню паузы: тир поверх смены, но под паузой.
    static int PickSortingOrder(GameUI gameUI, PauseMenu pauseMenu)
    {
        int order = 5;

        var below = gameUI ? gameUI.GetComponentInParent<Canvas>() : null;
        if (below) order = Mathf.Max(order, below.sortingOrder + 1);

        var above = pauseMenu ? pauseMenu.GetComponentInParent<Canvas>() : null;
        if (above && above.sortingOrder > (below ? below.sortingOrder : 0))
            order = Mathf.Min(order, above.sortingOrder - 1);

        return order;
    }

    static void CopyScaler(CanvasScaler scaler, GameUI gameUI, PauseMenu pauseMenu)
    {
        CanvasScaler source = null;
        if (gameUI) source = gameUI.GetComponentInParent<CanvasScaler>();
        if (!source && pauseMenu) source = pauseMenu.GetComponentInParent<CanvasScaler>();

        if (source)
        {
            scaler.uiScaleMode = source.uiScaleMode;
            scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode;
            scaler.matchWidthOrHeight = source.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = source.referencePixelsPerUnit;
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include)) return;

        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(es, "Создать EventSystem");
    }

    static void MarkDirty()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
    }
}
