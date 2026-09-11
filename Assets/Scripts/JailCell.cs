using System.Collections.Generic;
using UnityEngine;

/// Камера для задержанных: держит места и расставляет по ним нарушителей.
public class JailCell : MonoBehaviour
{
    [Tooltip("Leave empty to use this object's children as cell slots, in hierarchy order.")]
    public List<Transform> slots = new List<Transform>();

    [Header("Behaviour")]
    [Tooltip("When every slot is taken, the oldest detainee is removed to free one.")]
    public bool recycleWhenFull = true;
    [Tooltip("Random yaw spread so detainees do not stand like clones, degrees.")]
    public float randomYaw = 25f;
    [Tooltip("Parent detainees to the cell so they travel with it if you move the object.")]
    public bool parentToCell = true;

    readonly List<Transform> _cache = new List<Transform>();
    Visitor[] _occupants;

    public IReadOnlyList<Transform> Slots
    {
        get
        {
            if (slots != null && slots.Count > 0) return slots;

            _cache.Clear();
            for (int i = 0; i < transform.childCount; i++)
                _cache.Add(transform.GetChild(i));
            return _cache;
        }
    }

    public int Occupied
    {
        get
        {
            if (_occupants == null) return 0;
            int n = 0;
            foreach (var v in _occupants) if (v) n++;
            return n;
        }
    }

    /// Посадить задержанного. false - мест нет.
    public bool Put(Visitor v)
    {
        if (!v) return false;

        var s = Slots;
        if (s.Count == 0)
        {
            Debug.LogWarning("[JailCell] Нет мест: добавь пустышки внутрь объекта камеры.", this);
            return false;
        }

        if (_occupants == null || _occupants.Length != s.Count)
            _occupants = new Visitor[s.Count];

        int idx = -1;
        for (int i = 0; i < _occupants.Length; i++)
            if (_occupants[i] == null) { idx = i; break; }

        if (idx < 0)
        {
            if (!recycleWhenFull) return false;
            idx = 0;
            if (_occupants[0]) Destroy(_occupants[0].gameObject);
        }

        var slot = s[idx];
        if (!slot) return false;

        _occupants[idx] = v;

        Quaternion rot = slot.rotation * Quaternion.Euler(0f, Random.Range(-randomYaw, randomYaw), 0f);
        v.Detain(slot.position, rot);

        if (parentToCell) v.transform.SetParent(transform, true);
        return true;
    }

    [ContextMenu("Clear Cell")]
    public void Clear()
    {
        if (_occupants == null) return;
        for (int i = 0; i < _occupants.Length; i++)
        {
            if (_occupants[i]) Destroy(_occupants[i].gameObject);
            _occupants[i] = null;
        }
    }

    void OnDrawGizmos()
    {
        var s = Slots;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        foreach (var t in s)
        {
            if (!t) continue;
            Gizmos.DrawWireSphere(t.position + Vector3.up * 0.9f, 0.28f);
            Gizmos.DrawRay(t.position, t.forward * 0.5f);
        }
    }
}
