using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вешается на префаб персонажа. При появлении персонажа (Awake) один раз генерирует
/// случайный набор предметов из каталога ItemData и "прикрепляет" их к персонажу
/// (как детей нужных точек крепления). Каждый предмет получает тег "Contraband" или
/// безопасный тег — точно так же, как раньше это делал ItemSpawner, поэтому ваш
/// существующий ScannerDetector (CompareTag("Contraband")) продолжает работать без
/// изменений: когда персонаж с контрабандным предметом проходит через триггер сканера,
/// коллайдер именно этого предмета вызовет срабатывание.
///
/// Никакого спавна по таймеру больше нет — весь набор формируется один раз при старте.
/// </summary>
public class Itemspawner : MonoBehaviour
{
    [Header("Каталог предметов")]
    [Tooltip("Все возможные предметы, которые могут оказаться у персонажа")]
    [SerializeField] private List<ItemData> possibleItems = new List<ItemData>();

    [Header("Сколько предметов у персонажа")]
    [Tooltip("Минимальное количество предметов у одного персонажа")]
    [SerializeField] private int minItems = 1;
    [Tooltip("Максимальное количество предметов у одного персонажа")]
    [SerializeField] private int maxItems = 3;

    [Header("Точки крепления (опционально)")]
    [Tooltip("Куда крепить предметы (рука, карман, сумка и т.д.). " +
             "Если список пуст — все предметы крепятся прямо к персонажу (transform этого объекта).")]
    [SerializeField] private List<Transform> attachPoints = new List<Transform>();

    [Header("Теги")]
    [SerializeField] private string contrabandTag = "Contraband";
    [SerializeField] private string safeTag = "Untagged";

    [Header("Отладка")]
    [SerializeField] private bool logGeneratedLoadout = false;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void Awake()
    {
        GenerateLoadout();
    }

    /// <summary>
    /// Сгенерировать (или перегенерировать) набор предметов у персонажа.
    /// Можно вызвать вручную, например, если персонаж переиспользуется через object pool.
    /// </summary>
    public void GenerateLoadout()
    {
        ClearLoadout();

        int count = Random.Range(minItems, maxItems + 1);

        for (int i = 0; i < count; i++)
        {
            ItemData data = GetRandomWeightedItem();
            if (data == null || data.prefab == null) continue;

            Transform point = GetAttachPoint(i);
            GameObject go = Instantiate(data.prefab, point.position, point.rotation, point);

            go.tag = data.isContraband ? contrabandTag : safeTag;

            // Если на предмете есть Rigidbody — делаем кинематическим,
            // чтобы он не падал/не улетал, а просто "ехал" вместе с персонажем.
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            spawnedItems.Add(go);

            if (logGeneratedLoadout)
                Debug.Log($"[CharacterLoadout] {name} несёт: {data.itemName} (контрабанда: {data.isContraband})");
        }
    }

    /// <summary>
    /// Удаляет ранее заспавненные предметы (используется перед повторной генерацией).
    /// </summary>
    public void ClearLoadout()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null)
                Destroy(item);
        }
        spawnedItems.Clear();
    }

    /// <summary>Есть ли у персонажа сейчас хотя бы один контрабандный предмет.</summary>
    public bool IsCarryingContraband()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null && item.CompareTag(contrabandTag))
                return true;
        }
        return false;
    }

    private Transform GetAttachPoint(int index)
    {
        if (attachPoints.Count == 0)
            return transform;

        return attachPoints[index % attachPoints.Count];
    }

    private ItemData GetRandomWeightedItem()
    {
        float totalWeight = 0f;
        foreach (var item in possibleItems)
        {
            if (item != null)
                totalWeight += item.spawnWeight;
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var item in possibleItems)
        {
            if (item == null) continue;
            cumulative += item.spawnWeight;
            if (roll <= cumulative)
                return item;
        }

        return possibleItems[possibleItems.Count - 1];
    }
}