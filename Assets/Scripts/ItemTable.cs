using System.Collections.Generic;
using UnityEngine;

/// Стол досмотра: раскладывает вещи посетителя и убирает их.
public class ItemTable : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Left edge of the layout. Right axis = along the table, forward = deeper rows.")]
    public Transform origin;
    public float spacing = 0.3f;
    public float rowSpacing = 0.28f;
    public int perRow = 4;
    public float dropHeight = 0.02f;

    [Header("Look")]
    public bool randomYaw = true;
    [Tooltip("Kinematic so items do not roll off the table.")]
    public bool freezePhysics = true;

    [Header("Placeholder")]
    public GameObject fallbackPrefab;
    public Vector3 placeholderSize = new Vector3(0.14f, 0.06f, 0.2f);

    readonly List<GameObject> _spawned = new List<GameObject>();

    Transform Root => origin ? origin : transform;

    public void Show(List<ItemDefinition> items)
    {
        Clear();
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            var def = items[i];
            if (def == null) continue;

            int col = i % Mathf.Max(1, perRow);
            int row = i / Mathf.Max(1, perRow);

            Vector3 pos = Root.position
                        + Root.right   * (col * spacing)
                        + Root.forward * (row * rowSpacing)
                        + Root.up      * dropHeight;

            Quaternion rot = Root.rotation;
            if (randomYaw) rot *= Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);

            GameObject go;
            if (def.prefab)
            {
                go = Instantiate(def.prefab, pos, rot, transform);
            }
            else if (fallbackPrefab)
            {
                go = Instantiate(fallbackPrefab, pos, rot, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform);
                go.transform.SetPositionAndRotation(pos + Vector3.up * placeholderSize.y * 0.5f, rot);
                go.transform.localScale = placeholderSize;
                var rend = go.GetComponent<Renderer>();
                if (rend) rend.material.color = def.banned
                    ? new Color(0.8f, 0.15f, 0.15f)
                    : new Color(0.75f, 0.72f, 0.65f);
            }

            go.name = "Item_" + def.displayName;

            if (freezePhysics)
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
                    rb.isKinematic = true;

            _spawned.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in _spawned)
            if (go) Destroy(go);

        _spawned.Clear();
    }

    void OnDrawGizmos()
    {
        var t = Root;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        for (int i = 0; i < perRow * 2; i++)
        {
            int col = i % Mathf.Max(1, perRow);
            int row = i / Mathf.Max(1, perRow);
            Vector3 p = t.position + t.right * (col * spacing) + t.forward * (row * rowSpacing);
            Gizmos.DrawWireCube(p + t.up * placeholderSize.y * 0.5f, placeholderSize);
        }
    }
}
