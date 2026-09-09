using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

/// Настройка стрельбища в три клика. Меню Tools ▸ Стрельбище.
public static class ShootingRangeSetup
{
    const string Menu = "Tools/Стрельбище/";

    // ===== 1. менеджер =====

    [MenuItem(Menu + "1. Создать менеджер тира", false, 1)]
    static void CreateManager()
    {
        var range = Object.FindFirstObjectByType<ShootingRange>();

        if (range)
        {
            Select(range.gameObject);
            Debug.Log("[Тир] Менеджер уже есть в сцене.", range);
            return;
        }

        var go = new GameObject("ShootingRange");
        Undo.RegisterCreatedObjectUndo(go, "Создать тир");
        range = Undo.AddComponent<ShootingRange>(go);

        Select(go);
        Dirty();
        Debug.Log("[Тир] Менеджер создан. Дальше: выдели стволы на стеллаже и жми пункт 2.", range);
    }

    // ===== 2. оружие =====

    [MenuItem(Menu + "2. Выделенное — это оружие", true)]
    static bool MakeWeaponsCheck() => Selection.gameObjects.Length > 0;

    [MenuItem(Menu + "2. Выделенное — это оружие", false, 2)]
    static void MakeWeapons()
    {
        int added = 0, already = 0;

        foreach (var go in Selection.gameObjects)
        {
            if (!go || EditorUtility.IsPersistent(go)) continue;

            var w = go.GetComponent<RangeWeapon>();
            if (w) { already++; continue; }

            w = Undo.AddComponent<RangeWeapon>(go);
            w.displayName = go.name;
            added++;
        }

        Dirty();
        Debug.Log($"[Тир] Оружие: добавлено {added}, уже было {already}. " +
                  "Положение в руках подбирается полем Hold Position прямо в Play Mode.");
    }

    // ===== 3. мишени =====

    [MenuItem(Menu + "3. Выделенное — это мишени", true)]
    static bool MakeTargetsCheck() => Selection.gameObjects.Length > 0;

    [MenuItem(Menu + "3. Выделенное — это мишени", false, 3)]
    static void MakeTargets()
    {
        int added = 0, already = 0, skipped = 0, colliders = 0;

        foreach (var go in Selection.gameObjects)
        {
            if (!go || EditorUtility.IsPersistent(go)) continue;

            if (go.GetComponentInChildren<Renderer>(true) == null)
            {
                skipped++;
                continue;
            }

            var t = go.GetComponent<ShootingTarget>();
            if (t) already++;
            else
            {
                t = Undo.AddComponent<ShootingTarget>(go);
                t.displayName = go.name;
                added++;
            }

            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                t.EnsureCollider();
                colliders++;
            }
        }

        Dirty();
        Debug.Log($"[Тир] Мишени: добавлено {added}, уже было {already}, пропущено без моделей {skipped}. " +
                  $"Коллайдеров навешено: {colliders}.");
    }

    // ===== проверка =====

    [MenuItem(Menu + "Проверить настройку", false, 20)]
    static void Check()
    {
        var range = Object.FindFirstObjectByType<ShootingRange>();
        var shift = Object.FindFirstObjectByType<ShiftManager>();
        var hud = Object.FindFirstObjectByType<GameHUD>();
        var weapons = Object.FindObjectsByType<RangeWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var targets = Object.FindObjectsByType<ShootingTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Camera cam = range && range.playerCamera ? range.playerCamera : Camera.main;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== ПРОВЕРКА СТРЕЛЬБИЩА ===");
        sb.AppendLine(Line("Менеджер ShootingRange", range, "нет — пункт 1 меню"));
        sb.AppendLine(Line("ShiftManager", shift, "нет — тир не откроется после смены"));
        sb.AppendLine(Line("GameHUD", hud, "нет — не будет кнопки «Пострелять в тире»"));
        sb.AppendLine(Line("Камера игрока", cam, "нет — поставь тег MainCamera или заполни Player Camera"));
        sb.AppendLine($"Оружия (RangeWeapon): {weapons.Length}" + (weapons.Length == 0 ? "   <-- пункт 2 меню" : "   ок"));
        sb.AppendLine($"Мишеней (ShootingTarget): {targets.Length}" + (targets.Length == 0 ? "   <-- пункт 3 меню" : "   ок"));

        bool ok = range && cam && weapons.Length > 0 && targets.Length > 0;
        sb.AppendLine(ok
            ? "\nВсё на месте. Запускай сцену и жми F5 — смена закончится досрочно и откроется тир."
            : "\nЕсть незакрытые пункты — смотри строки выше.");

        if (ok) Debug.Log(sb.ToString(), range);
        else Debug.LogWarning(sb.ToString(), range);
    }

    static string Line(string what, Object obj, string missing) =>
        obj ? $"{what}: ок ({obj.name})" : $"{what}: {missing}";

    static void Select(GameObject go)
    {
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    static void Dirty()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
    }
}
