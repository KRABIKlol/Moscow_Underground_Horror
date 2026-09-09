using UnityEditor;
using UnityEngine;

/// Постановка оружия в руки мышкой: превью цепляется к камере, ты двигаешь его
/// обычным гизмо и сразу видишь результат в Game View. Никаких чисел вручную.
public static class RangeWeaponPoser
{
    const string Menu = "Tools/Стрельбище/";
    const string PreviewName = "__НАСТРОЙКА ПОЗЫ ОРУЖИЯ__";

    // ===== начать =====

    [MenuItem(Menu + "4. Поставить оружие в руки (мышкой)", true)]
    static bool StartCheck()
    {
        var go = Selection.activeGameObject;
        return go && go.GetComponent<RangeWeapon>() && !EditorUtility.IsPersistent(go);
    }

    [MenuItem(Menu + "4. Поставить оружие в руки (мышкой)", false, 4)]
    static void StartPosing()
    {
        var weapon = Selection.activeGameObject.GetComponent<RangeWeapon>();
        var cam = FindCamera();

        if (!cam)
        {
            EditorUtility.DisplayDialog("Тир",
                "Не найдена камера игрока. Поставь камере тег MainCamera " +
                "или заполни поле Player Camera у ShootingRange.", "Ок");
            return;
        }

        RemovePreview();

        var preview = Object.Instantiate(weapon.gameObject, cam.transform);
        preview.name = PreviewName;
        Undo.RegisterCreatedObjectUndo(preview, "Настройка позы оружия");

        // Всё лишнее с копии снимаем: она только для того, чтобы на неё смотреть.
        foreach (var c in preview.GetComponentsInChildren<RangeWeapon>(true)) Object.DestroyImmediate(c);
        foreach (var c in preview.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var c in preview.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);

        var mark = preview.AddComponent<RangeWeaponPosePreview>();
        mark.target = weapon;

        // Мировой масштаб сохраняем — в игре оружие будет ровно такого же размера.
        Vector3 lossy = weapon.transform.lossyScale;
        Vector3 camScale = cam.transform.lossyScale;
        preview.transform.localScale = new Vector3(
            lossy.x / Mathf.Max(0.0001f, camScale.x),
            lossy.y / Mathf.Max(0.0001f, camScale.y),
            lossy.z / Mathf.Max(0.0001f, camScale.z));

        // Стартовая поза: либо уже сохранённая, либо разумная заготовка перед камерой.
        if (!weapon.autoFit && weapon.holdPosition != Vector3.zero)
        {
            preview.transform.localPosition = weapon.holdPosition;
            preview.transform.localRotation = Quaternion.Euler(weapon.holdRotation);
            preview.transform.localScale *= Mathf.Max(0.01f, weapon.holdScale);
        }
        else
        {
            preview.transform.localPosition = new Vector3(0.2f, -0.18f, 0.5f);
            preview.transform.localRotation = Quaternion.identity;
        }

        Selection.activeGameObject = preview;
        Tools.current = Tool.Move;
        EditorGUIUtility.PingObject(preview);

        Debug.Log("[Тир] Копия оружия висит на камере. Открой Game View рядом со Scene View, " +
                  "двигай и вращай копию гизмо (W — двигать, E — вращать, R — размер), " +
                  "пока ствол не ляжет в руку. Потом: Tools ▸ Стрельбище ▸ «5. Запомнить позу».", preview);
    }

    // ===== сохранить =====

    [MenuItem(Menu + "5. Запомнить позу оружия", true)]
    static bool SaveCheck() => FindPreview() != null;

    [MenuItem(Menu + "5. Запомнить позу оружия", false, 5)]
    static void SavePose()
    {
        var preview = FindPreview();
        if (!preview) return;

        var weapon = preview.target;
        if (!weapon)
        {
            Debug.LogError("[Тир] Превью потеряло ссылку на оружие. Удали объект и начни заново.", preview);
            return;
        }

        Undo.RecordObject(weapon, "Поза оружия");

        Vector3 previewLossy = preview.transform.lossyScale;
        Vector3 weaponLossy = weapon.transform.lossyScale;

        weapon.autoFit = false;                       // поза задана вручную, автоподбор больше не нужен
        weapon.orientationPreset = -1;
        weapon.flipBarrel = false;
        weapon.holdPosition = preview.transform.localPosition;
        weapon.holdRotation = preview.transform.localRotation.eulerAngles;
        weapon.holdScale = previewLossy.x / Mathf.Max(0.0001f, weaponLossy.x);

        EditorUtility.SetDirty(weapon);

        var p = weapon.holdPosition;
        var r = weapon.holdRotation;
        Debug.Log($"[Тир] Поза сохранена для «{weapon.displayName}»:\n" +
                  $"Hold Position = ({p.x:0.###}, {p.y:0.###}, {p.z:0.###})\n" +
                  $"Hold Rotation = ({r.x:0.#}, {r.y:0.#}, {r.z:0.#})\n" +
                  $"Hold Scale = {weapon.holdScale:0.###}", weapon);

        Undo.DestroyObjectImmediate(preview.gameObject);
        Selection.activeGameObject = weapon.gameObject;
    }

    // ===== отменить =====

    [MenuItem(Menu + "Отменить настройку позы", true)]
    static bool CancelCheck() => FindPreview() != null;

    [MenuItem(Menu + "Отменить настройку позы", false, 6)]
    static void Cancel()
    {
        RemovePreview();
        Debug.Log("[Тир] Настройка позы отменена.");
    }

    // ===== мелочи =====

    static RangeWeaponPosePreview FindPreview() =>
        Object.FindFirstObjectByType<RangeWeaponPosePreview>(FindObjectsInactive.Include);

    static void RemovePreview()
    {
        var old = FindPreview();
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
    }

    static Camera FindCamera()
    {
        var range = Object.FindFirstObjectByType<ShootingRange>();
        if (range && range.playerCamera) return range.playerCamera;
        if (Camera.main) return Camera.main;
        return Object.FindFirstObjectByType<Camera>();
    }
}
