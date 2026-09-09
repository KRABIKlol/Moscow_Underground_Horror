using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var db = (ItemDatabase)target;

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Заполнить дефолтным списком", GUILayout.Height(34)))
        {
            db.FillDefaults();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"Заполнено: {db.items.Count} предметов.", db);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(4);
        int banned = db.items.FindAll(i => i != null && i.banned).Count;
        EditorGUILayout.HelpBox(
            $"Всего: {db.items.Count}\nРазрешённых: {db.items.Count - banned}\nЗапрещённых: {banned}",
            MessageType.Info);
    }
}