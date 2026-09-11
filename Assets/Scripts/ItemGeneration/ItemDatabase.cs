using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Security Console/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new List<ItemDefinition>();

    readonly List<ItemDefinition> _allowed = new List<ItemDefinition>();
    readonly List<ItemDefinition> _banned  = new List<ItemDefinition>();

    void OnEnable()   => Rebuild();
    void OnValidate() => Rebuild();

    void Rebuild()
    {
        _allowed.Clear();
        _banned.Clear();
        foreach (var i in items)
        {
            if (i == null) continue;
            (i.banned ? _banned : _allowed).Add(i);
        }
    }

    public ItemDefinition RandomAllowed()
    {
        if (_allowed.Count == 0) Rebuild();
        return _allowed.Count == 0 ? null : _allowed[Random.Range(0, _allowed.Count)];
    }

    public ItemDefinition RandomBanned()
    {
        if (_banned.Count == 0) Rebuild();
        return _banned.Count == 0 ? null : _banned[Random.Range(0, _banned.Count)];
    }

    public List<ItemDefinition> RollItems(Vector2Int count, float bannedChance)
    {
        var result = new List<ItemDefinition>();
        int n = Mathf.Max(1, Random.Range(count.x, count.y + 1));
        bool giveBanned = Random.value < bannedChance;
        int bannedSlot = giveBanned ? Random.Range(0, n) : -1;

        for (int i = 0; i < n; i++)
        {
            var item = (i == bannedSlot) ? RandomBanned() : RandomAllowed();
            if (item != null && !result.Contains(item)) result.Add(item);
        }

        if (giveBanned && !result.Exists(i => i.banned))
        {
            var b = RandomBanned();
            if (b != null) result.Add(b);
        }
        return result;
    }

    [ContextMenu("Fill With Defaults")]
    public void FillDefaults()
    {
        items.Clear();

        void Add(string n, bool ban, bool trig) =>
            items.Add(new ItemDefinition { displayName = n, banned = ban, triggersDetector = trig });

        Add("Ключи",             false, false);
        Add("Мобильный телефон", false, false);
        Add("Кошелёк",           false, false);
        Add("Бутылка воды",      false, false);
        Add("Зонт",              false, false);
        Add("Наушники",          false, false);
        Add("Пачка сигарет",     false, false);
        Add("Книга",             false, false);
        Add("Термос",            false, false);
        Add("Проездной",         false, false);

        Add("Складной нож",            true, true);
        Add("Отвёртка",                true, true);
        Add("Монтировка",              true, true);
        Add("Кастет",                  true, true);
        Add("Электрошокер",            true, true);
        Add("Травматический пистолет", true, true);

        Add("Газовый баллончик",  true, false);
        Add("Свёрток в фольге",   true, false);
        Add("Пакет с порошком",   true, false);
        Add("Стеклянная бутылка", true, false);

        Rebuild();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
