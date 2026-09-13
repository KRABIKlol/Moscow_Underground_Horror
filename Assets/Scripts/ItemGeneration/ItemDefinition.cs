using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    public string displayName = "Item";
    public bool banned;

   
    public bool triggersDetector = true;

   
    public GameObject prefab;

    public Sprite icon;
    [TextArea] public string note;
}
