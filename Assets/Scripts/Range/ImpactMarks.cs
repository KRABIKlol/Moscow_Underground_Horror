using System.Collections.Generic;
using UnityEngine;

/// Пробоины от пуль. Ничего настраивать не нужно: если не задан свой префаб,
/// метка рисуется сгенерированным в рантайме тёмным пятном.
public static class ImpactMarks
{
    /// Сколько отметок висит одновременно; самые старые переиспользуются.
    public static int maxMarks = 80;

    /// Необязательный собственный префаб пробоины. Ставится из ShootingRange.
    public static GameObject customPrefab;

    static Mesh _quad;
    static Material _material;
    static readonly Queue<GameObject> _pool = new Queue<GameObject>();

    public static void Clear()
    {
        while (_pool.Count > 0)
        {
            var go = _pool.Dequeue();
            if (go) Object.Destroy(go);
        }
    }

    public static void Spawn(RaycastHit hit, float size = 0.05f)
    {
        GameObject mark = null;

        if (_pool.Count >= maxMarks)
        {
            mark = _pool.Dequeue();
            if (!mark) mark = Create();
        }
        else
        {
            mark = Create();
        }

        // Метки живут в мире, а не на объекте: мишени в тире не двигаются,
        // зато пробоина не ломается от масштаба родителя.
        mark.transform.SetParent(null, true);
        mark.transform.position = hit.point + hit.normal * 0.006f;
        mark.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
        if (!customPrefab) mark.transform.localScale = Vector3.one * size;   // свой префаб оставляем как есть
        mark.SetActive(true);

        _pool.Enqueue(mark);
    }

    static GameObject Create()
    {
        if (customPrefab)
            return Object.Instantiate(customPrefab);

        EnsureAssets();

        var go = new GameObject("BulletHole");
        go.AddComponent<MeshFilter>().sharedMesh = _quad;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return go;
    }

    static void EnsureAssets()
    {
        if (_quad == null)
        {
            _quad = new Mesh { name = "BulletHoleQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
        }

        if (_material == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Standard");

            _material = new Material(sh) { name = "BulletHoleMat" };

            var tex = MakeHoleTexture(64);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", tex);
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", tex);
            _material.renderQueue = 3050;   // поверх непрозрачной геометрии
        }
    }

    /// Тёмное пятно с мягкими краями — обычная пробоина в бумаге.
    static Texture2D MakeHoleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;

                float alpha = 1f - Mathf.SmoothStep(0.5f, 1f, d);
                float core  = 1f - Mathf.SmoothStep(0f, 0.5f, d);
                float v = Mathf.Lerp(0.30f, 0.02f, core);

                tex.SetPixel(x, y, new Color(v, v, v, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}
