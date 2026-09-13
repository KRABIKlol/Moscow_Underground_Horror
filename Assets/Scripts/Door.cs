using System.Collections.Generic;
using UnityEngine;
public class Door : MonoBehaviour
{
    public enum DoorMode { Rotate, Slide }    
    public DoorMode mode = DoorMode.Rotate;    
    public Transform pivot;   
    public float openAngle = 90f;    
    public Vector3 slideOffset = new Vector3(0f, 0f, 1f);   
    public float speed = 2f;   
    public bool playerCanUse = true;    
    public bool startOpen = false;
    public bool locked = false;
    public bool openAwayFromUser = true;
    public bool autoClose = false;
    public float autoCloseDelay = 6f;
    public List<Door> linked = new List<Door>();
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip lockedClip;
    public string openText = "Открыть";
    public string closeText = "Закрыть";
    public string lockedText = "Заперто";
    public bool IsOpen { get; private set; }
    Transform Body => pivot ? pivot : transform;
    Vector3 _closedPos;
    Quaternion _closedRot;
    float _t;          
    float _sign = 1f;  
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

    public string PromptText(string keyLabel)
    {
        if (!playerCanUse) return null;
        if (locked) return $"[{keyLabel}]  {lockedText}";
        return $"[{keyLabel}]  {(IsOpen ? closeText : openText)}";
    }

    public void Interact(Vector3 userPosition)
    {
        if (!playerCanUse) return;

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
            d._sign = -_sign;          
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
