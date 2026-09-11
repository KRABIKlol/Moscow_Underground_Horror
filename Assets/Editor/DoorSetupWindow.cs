using UnityEditor;
using UnityEngine;

/// Пакетная настройка дверей: создаёт пустышку-петлю на краю полотна,
/// делает дверь её дочерней и вешает компонент Door.
public class DoorSetupWindow : EditorWindow
{
    enum HingeSide { Left, Right }

    HingeSide side = HingeSide.Left;
    float openAngle = 90f;
    float speed = 2f;
    bool addCollider = true;
    bool openAwayFromUser = true;

    [MenuItem("Tools/Security Console/Setup Doors On Selection")]
    static void Open() => GetWindow<DoorSetupWindow>("Doors");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Выдели в Hierarchy объекты дверей и нажми кнопку.\n" +
            "Для каждой создастся пустышка-петля на краю полотна, дверь станет её дочерней, " +
            "на петлю повесится компонент Door.",
            MessageType.Info);

        GUILayout.Space(8);
        side = (HingeSide)EditorGUILayout.EnumPopup("Сторона петель", side);
        openAngle = EditorGUILayout.FloatField("Угол открытия", openAngle);
        speed = EditorGUILayout.FloatField("Скорость", speed);
        openAwayFromUser = EditorGUILayout.Toggle("Открывать от игрока", openAwayFromUser);
        addCollider = EditorGUILayout.Toggle("Добавить коллайдер", addCollider);

        GUILayout.Space(12);

        int count = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
        GUI.enabled = count > 0;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button($"Настроить выделенные ({count})", GUILayout.Height(34)))
            Setup();
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    void Setup()
    {
        int done = 0, skipped = 0;

        foreach (var go in Selection.gameObjects)
        {
            if (!go) continue;

            if (go.GetComponentInParent<Door>())
            {
                skipped++;
                continue;
            }

            var rend = go.GetComponentInChildren<Renderer>();
            if (!rend)
            {
                skipped++;
                continue;
            }

            Transform t = go.transform;
            Bounds b = rend.bounds;                     // мировые габариты полотна

            // половина ширины вдоль локальной оси X двери
            Vector3 right = t.right;
            Vector3 absRight = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            float half = Vector3.Dot(b.extents, absRight);

            Vector3 hingePos = b.center + right * (side == HingeSide.Left ? -half : half);
            hingePos.y = b.min.y;

            var hinge = new GameObject(go.name + "_Hinge");
            Undo.RegisterCreatedObjectUndo(hinge, "Create door hinge");
            hinge.transform.SetParent(t.parent, true);
            hinge.transform.SetPositionAndRotation(hingePos, t.rotation);
            hinge.transform.SetSiblingIndex(t.GetSiblingIndex());

            Undo.SetTransformParent(t, hinge.transform, "Parent door to hinge");

            var door = Undo.AddComponent<Door>(hinge);
            door.openAngle = openAngle;
            door.speed = speed;
            door.openAwayFromUser = openAwayFromUser;

            if (addCollider && !go.GetComponentInChildren<Collider>())
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    mc.sharedMesh = mf.sharedMesh;
                }
                else
                {
                    Undo.AddComponent<BoxCollider>(go);
                }
            }

            done++;
        }

        Debug.Log($"[DoorSetup] Настроено дверей: {done}, пропущено: {skipped}.");
    }
}
