using System.Collections.Generic;
using UnityEngine;

/// Дверь: распашная или раздвижная. Открывается по взаимодействию игрока.
public class Door : MonoBehaviour
{
    public enum DoorMode { Rotate, Slide }

    [Header("Motion")]
    public DoorMode mode = DoorMode.Rotate;
    [Tooltip("What actually moves. Leave empty to move this object.")]
    public Transform pivot;
    [Tooltip("Rotate mode: swing angle in degrees.")]
    public float openAngle = 90f;
    [Tooltip("Slide mode: local offset when fully open.")]
    public Vector3 slideOffset = new Vector3(0f, 0f, 1f);
    [Tooltip("Full open takes 1 / speed seconds.")]
    public float speed = 2f;

    [Header("Behaviour")]
    public bool startOpen = false;
    public bool locked = false;
    [Tooltip("Rotate mode: swing away from whoever opens it.")]
    public bool openAwayFromUser = true;
    public bool autoClose = false;
    public float autoCloseDelay = 6f;

    [Header("Linked leaves (double doors)")]
    public List<Door> linked = new List<Door>();

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip lockedClip;

    [Header("Prompt")]
    public string openText = "Открыть";
    public string closeText = "Закрыть";
    public string lockedText = "Заперто";

    public bool IsOpen { get; private set; }

    Transform Body => pivot ? pivot : transform;

    Vector3 _closedPos;
    Quaternion _closedRot;
    float _t;          // 0 закрыта, 1 открыта
    float _sign = 1f;  // в какую сторону распахивается
    float _autoTimer;

    void Awake()
    {
        _closedPos = Body.localPosition;
        _closedRot = Body.localRotation;

        IsOpen = startOpen;
        _t = startOpen ? 1f : 0f;
        Apply();
    }

    void Update()
    {
        float target = IsOpen ? 1f : 0f;

        if (!Mathf.Approximately(_t, target))
        {
            _t = Mathf.MoveTowards(_t, target, speed * Time.deltaTime);
            Apply();
        }

        if (autoClose && IsOpen && _t >= 1f)
        {
            _autoTimer += Time.deltaTime;
            if (_autoTimer >= autoCloseDelay) SetOpen(false, true);
        }
    }

    void Apply()
    {
        float e = Mathf.SmoothStep(0f, 1f, _t);

        if (mode == DoorMode.Rotate)
            Body.localRotation = _closedRot * Quaternion.Euler(0f, openAngle * _sign * e, 0f);
        else
            Body.localPosition = _closedPos + slideOffset * e;
    }

    /// Текст подсказки для интерактора.
    public string PromptText(string keyLabel)
    {
        if (locked) return $"[{keyLabel}]  {lockedText}";
        return $"[{keyLabel}]  {(IsOpen ? closeText : openText)}";
    }

    /// Нажатие игрока. userPosition нужна, чтобы дверь распахнулась от него.
    public void Interact(Vector3 userPosition)
    {
        if (locked)
        {
            Play(lockedClip);
            return;
        }

        if (!IsOpen && mode == DoorMode.Rotate && openAwayFromUser)
        {
            Vector3 toUser = userPosition - Body.position;
            _sign = Vector3.Dot(Body.forward, toUser) > 0f ? -1f : 1f;
        }

        SetOpen(!IsOpen, true);
    }

    public void Toggle() => SetOpen(!IsOpen, true);
    public void Open()   => SetOpen(true, true);
    public void Close()  => SetOpen(false, true);

    public void SetOpen(bool open, bool propagate)
    {
        if (locked && open) { Play(lockedClip); return; }
        if (IsOpen == open) return;

        IsOpen = open;
        _autoTimer = 0f;
        Play(open ? openClip : closeClip);

        if (!propagate) return;

        foreach (var d in linked)
        {
            if (!d || d == this) continue;
            d._sign = -_sign;          // вторая створка распахивается зеркально
            d.SetOpen(open, false);
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }

    [ContextMenu("Toggle (editor test)")]
    void EditorToggle() => Toggle();

    void OnDrawGizmosSelected()
    {
        var b = pivot ? pivot : transform;
        Gizmos.color = locked ? Color.red : Color.green;
        Gizmos.DrawWireSphere(b.position, 0.15f);

        if (mode == DoorMode.Slide)
            Gizmos.DrawLine(b.position, b.position + b.TransformVector(slideOffset));
    }
}
