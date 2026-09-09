using System.Collections.Generic;
using UnityEngine;

/// Рамка металлодетектора: меняет цвет и свечение по состоянию проверки.
public class MetalDetectorVisual : MonoBehaviour
{
    public enum DetectorState { Idle, Scanning, Clean, Alarm }

    [Header("Renderers To Tint")]
    [Tooltip("Leave empty to grab every Renderer in children.")]
    public List<Renderer> targetRenderers = new List<Renderer>();
    public Light indicatorLight;

    [Header("Colors")]
    public Color idleColor  = new Color(0.15f, 0.45f, 0.75f);
    public Color scanColor  = new Color(1.00f, 0.75f, 0.10f);
    public Color cleanColor = new Color(0.15f, 0.90f, 0.30f);
    public Color alarmColor = new Color(1.00f, 0.10f, 0.10f);

    [Header("Emission")]
    public float idleEmission = 0.6f;
    public float activeEmission = 3f;
    public float alarmBlinkSpeed = 6f;
    public float lightIntensity = 4f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip alarmClip;
    public AudioClip cleanClip;

    public DetectorState State { get; private set; } = DetectorState.Idle;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

    readonly List<Material> _mats = new List<Material>();
    Color _color;
    float _emission;

    void Awake()
    {
        if (targetRenderers.Count == 0)
            targetRenderers.AddRange(GetComponentsInChildren<Renderer>());

        foreach (var r in targetRenderers)
        {
            if (!r) continue;
            _mats.AddRange(r.materials);
        }

        foreach (var m in _mats)
        {
            if (m.HasProperty(EmissionId))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        SetIdle();
    }

    void Update()
    {
        if (State != DetectorState.Alarm) return;
        float k = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * alarmBlinkSpeed));
        Push(_color, _emission * k, k);
    }

    public void SetIdle()     => Switch(DetectorState.Idle,     idleColor, idleEmission);
    public void SetScanning() => Switch(DetectorState.Scanning, scanColor, activeEmission);

    public void SetResult(bool alarm)
    {
        if (alarm)
        {
            Switch(DetectorState.Alarm, alarmColor, activeEmission);
            Play(alarmClip);
        }
        else
        {
            Switch(DetectorState.Clean, cleanColor, activeEmission);
            Play(cleanClip);
        }
    }

    void Switch(DetectorState s, Color c, float emission)
    {
        State = s;
        _color = c;
        _emission = emission;
        Push(c, emission, 1f);
    }

    void Push(Color c, float emission, float lightK)
    {
        foreach (var m in _mats)
        {
            if (!m) continue;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, c * emission);
        }

        if (indicatorLight)
        {
            indicatorLight.color = c;
            indicatorLight.intensity = lightIntensity * lightK * (emission <= idleEmission ? 0.35f : 1f);
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }

    [ContextMenu("Test: Alarm")] void TestAlarm() => SetResult(true);
    [ContextMenu("Test: Clean")] void TestClean() => SetResult(false);
    [ContextMenu("Test: Idle")]  void TestIdle()  => SetIdle();
}
