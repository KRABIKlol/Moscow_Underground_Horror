using System.Collections.Generic;
using UnityEngine;

/// Маршрут: цепочка путевых точек. По умолчанию точками считаются дочерние объекты по порядку.
public class RoutePath : MonoBehaviour
{
    [Tooltip("Leave empty to use this object's children, in hierarchy order.")]
    public List<Transform> points = new List<Transform>();

    [Header("Gizmo")]
    public Color color = new Color(0.3f, 0.9f, 1f, 0.9f);
    public float pointSize = 0.18f;

    readonly List<Transform> _cache = new List<Transform>();

    /// Точки маршрута по порядку.
    public IReadOnlyList<Transform> Points
    {
        get
        {
            if (points != null && points.Count > 0) return points;

            _cache.Clear();
            for (int i = 0; i < transform.childCount; i++)
                _cache.Add(transform.GetChild(i));
            return _cache;
        }
    }

    readonly List<Transform> _reversed = new List<Transform>();

    /// Те же точки в обратном порядке - для пути назад.
    public IReadOnlyList<Transform> PointsReversed
    {
        get
        {
            var pts = Points;
            _reversed.Clear();
            for (int i = pts.Count - 1; i >= 0; i--) _reversed.Add(pts[i]);
            return _reversed;
        }
    }

    void OnDrawGizmos()
    {
        var pts = Points;
        if (pts.Count == 0) return;

        Gizmos.color = color;
        for (int i = 0; i < pts.Count; i++)
        {
            if (!pts[i]) continue;
            Gizmos.DrawWireSphere(pts[i].position, pointSize);

            if (i + 1 < pts.Count && pts[i + 1])
                Gizmos.DrawLine(pts[i].position, pts[i + 1].position);
        }
    }
}
