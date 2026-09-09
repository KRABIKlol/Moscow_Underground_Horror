using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    public string displayName = "Item";
    public bool banned;

    [Tooltip("Does the metal detector react to it. Metal - yes, foil package or powder - no.")]
    public bool triggersDetector = true;

    [Tooltip("3D model laid out on the inspection table. Leave empty to get a placeholder cube.")]
    public GameObject prefab;

    public Sprite icon;
    [TextArea] public string note;
}
