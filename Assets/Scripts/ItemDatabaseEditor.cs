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
        if (GUILayout.Button("Fill With Defaults", GUILayout.Height(34)))
        {
            db.FillDefaults();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"ItemDatabase filled: {db.items.Count} items.", db);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(4);
        int banned = db.items.FindAll(i => i != null && i.banned).Count;
        EditorGUILayout.HelpBox(
            $"Total: {db.items.Count}   Allowed: {db.items.Count - banned}   Banned: {banned}",
            MessageType.Info);
    }
}
