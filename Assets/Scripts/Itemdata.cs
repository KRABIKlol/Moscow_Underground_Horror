using UnityEngine;


[CreateAssetMenu(fileName = "NewItemData", menuName = "Scanner/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Основное")]
    public string itemName = "Item";

    [Tooltip("Префаб, который будет заспавнен в мире")]
    public GameObject prefab;

    [Header("Контрабанда")]
    [Tooltip("Если true — сканер должен показать тревогу при обнаружении этого предмета")]
    public bool isContraband = false;

    [Header("Рандомизация")]
    [Tooltip("Вес при случайном выборе. Чем больше значение, тем чаще выпадает предмет. 0 = никогда не выпадет сам по себе (но можно заспавнить вручную).")]
    [Min(0f)]
    public float spawnWeight = 1f;
}
