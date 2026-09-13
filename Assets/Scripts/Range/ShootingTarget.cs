using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ShootingTarget : MonoBehaviour
{
    public string displayName = "Мишень";

    public int points = 1;

    public AudioClip hitClip;
    [Range(0f, 1f)] public float volume = 0.8f;
        
    public static event Action<ShootingTarget, RaycastHit> OnAnyHit;

    public int Hits { get; private set; }

    public void ResetHits() => Hits = 0;

    public void ReportHit(RaycastHit hit)
    {
        Hits++;

        if (hitClip)
            AudioSource.PlayClipAtPoint(hitClip, hit.point, volume);

        OnAnyHit?.Invoke(this, hit);
    }
    public void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>(true) != null) return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);

        Vector3 s = transform.lossyScale;
        box.size = new Vector3(
            b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
    }

    void Reset()
    {
        displayName = gameObject.name;
        EnsureCollider();
    }
}
