using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ShootingRange : MonoBehaviour
{
    public enum State { Closed, Ready, Countdown, Running, Results }

    public ShiftManager shift;
    public EventLog log;
    public CheckpointInputLock inputLock;
   
    public PauseMenu pause;
    public Camera playerCamera;
   
    public Transform playerRoot;

   
    public List<RangeWeapon> weapons = new List<RangeWeapon>();
   
    public Transform targetsRoot;
    public bool autoSetupTargets = true;
   
    public GameObject bulletHolePrefab;
    public float roundDuration = 60f;
    public float countdown = 3f;
   
    private bool openAfterFailedShift = false;
    
    public bool clearMarksOnStart = true;

   
    public int hitsForS = 45;
    public int hitsForA = 35;
    public int hitsForB = 25;
    public int hitsForC = 15;

    public float pickupRange = 3f;
    public float pickupAngle = 50f;
    public string keyLabel = "E";

    public State Current { get; private set; } = State.Closed;

    public bool Available { get; private set; }

    public bool PlayerFree => Current != State.Closed;

    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public float TimeLeft { get; private set; }
    public float Accuracy => Shots == 0 ? 0f : (float)Hits / Shots;

    public RangeWeapon HeldWeapon { get; private set; }
    public RangeWeapon Focused { get; private set; }

    public bool Paused => pause && pause.IsPaused;

    public float CountdownLeft => _countLeft;
    public bool HitFlash => _hitFlash > 0f;
    public int WeaponCount => weapons.Count;
    public int TargetCount => _targetCount;
    public float RoundDuration => roundDuration;
    public bool SceneReady => playerCamera && weapons.Count > 0 && _targetCount > 0;

    public string Prompt => Current == State.Ready && Focused
        ? $"[{keyLabel}]  Взять {Focused.displayName}"
        : null;

    const string BestKey = "range_best_score";

    float _countLeft, _hitFlash;
    int _targetCount;    

    void Awake()
    {
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!log) log = FindFirstObjectByType<EventLog>();
        if (!inputLock) inputLock = FindFirstObjectByType<CheckpointInputLock>();
        if (!pause) pause = FindFirstObjectByType<PauseMenu>();
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera) playerCamera = FindFirstObjectByType<Camera>();

        if (!playerRoot)
        {
            var fps = FindFirstObjectByType<FirstPersonController>();
            if (fps) playerRoot = fps.transform;
            else if (playerCamera) playerRoot = playerCamera.transform.root;
        }

        foreach (var w in FindObjectsByType<RangeWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w && !weapons.Contains(w)) weapons.Add(w);
        weapons.RemoveAll(w => w == null);

        if (bulletHolePrefab) ImpactMarks.customPrefab = bulletHolePrefab;

        if (autoSetupTargets) SetupTargets();

        BestScore = PlayerPrefs.GetInt(BestKey, 0);
    }

    void OnEnable() => ShootingTarget.OnAnyHit += HandleTargetHit;
    void OnDisable() => ShootingTarget.OnAnyHit -= HandleTargetHit;
        

    void OnDestroy()
    {
        if (shift) shift.OnShiftFinished -= HandleShiftFinished;
    }

    void HandleShiftFinished()
    {
        if (shift && shift.Failed && !openAfterFailedShift) return;
        Available = true;
    }
    public void SetupTargets()
    {
        if (!targetsRoot) return;

        foreach (Transform child in targetsRoot)
        {
            if (!child || child.GetComponentInChildren<Renderer>(true) == null) continue;

            var t = child.GetComponent<ShootingTarget>();
            if (!t)
            {
                t = child.gameObject.AddComponent<ShootingTarget>();
                t.displayName = child.name;
            }
            t.EnsureCollider();
        }
    }
   
    public void Open()
    {
        if (!Available) return;

        Current = State.Ready;
        Shots = Hits = Score = 0;
        CountTargets();        
        ApplyCursor();
    }  
   
    public void RestartRound()
    {
        if (Current != State.Results) return;
        Current = State.Ready;
        CountTargets();
        ApplyCursor();
    }
  
    public void Close()
    {
        DropWeapon();
        Current = State.Closed;
        ApplyCursor();
    }

    public void CloseAndReset()
    {
        DropWeapon();
        Current = State.Closed;
        Available = false;
        ApplyCursor();
    }

    void StartRound(RangeWeapon w)
    {
        if (!w || !playerCamera) return;

        HeldWeapon = w;
        w.Take(playerCamera, playerRoot);
        w.OnShot += HandleShot;
        w.FireEnabled = false;

        if (clearMarksOnStart) ImpactMarks.Clear();
        ResetTargets();

        Shots = Hits = Score = 0;
        TimeLeft = roundDuration;
        _countLeft = Mathf.Max(0f, countdown);
        Current = _countLeft > 0f ? State.Countdown : State.Running;
        if (Current == State.Running) HeldWeapon.FireEnabled = true;

        ApplyCursor();
    }

    void EndRound(bool aborted)
    {
        if (HeldWeapon) HeldWeapon.FireEnabled = false;
        DropWeapon();

        Current = State.Results;

        if (!aborted && Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }     

        ApplyCursor();
    }

    void DropWeapon()
    {
        if (!HeldWeapon) return;
        HeldWeapon.OnShot -= HandleShot;
        HeldWeapon.PutBack();
        HeldWeapon = null;
    }

    void ResetTargets()
    {
        foreach (var t in AllTargets()) t.ResetHits();
    }

    void CountTargets() => _targetCount = AllTargets().Length;

    ShootingTarget[] AllTargets() =>
        FindObjectsByType<ShootingTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    void HandleShot() => Shots++;    

    void HandleTargetHit(ShootingTarget target, RaycastHit hit)
    {
        if (Current != State.Running) return;
        Hits++;
        Score += Mathf.Max(1, target.points);
        _hitFlash = 0.12f;
    }

    public string Grade
    {
        get
        {
            if (Score >= hitsForS) return "S";
            if (Score >= hitsForA) return "A";
            if (Score >= hitsForB) return "B";
            if (Score >= hitsForC) return "C";
            return "D";
        }
    }

    public string TimeLeftString
    {
        get
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
            return $"{t / 60:00}:{t % 60:00}";
        }
    }

    void Update()
    {
        if (Paused) return;

        if (_hitFlash > 0f) _hitFlash -= Time.unscaledDeltaTime;

        switch (Current)
        {
            case State.Closed:
                if (!Available && shift && shift.Finished && (openAfterFailedShift || !shift.Failed))
                    Available = true;
                break;

            case State.Ready:
                Focused = FindWeaponInView();
                if (Focused && InteractPressed()) StartRound(Focused);
                else if (LeavePressed()) Close();
                break;

            case State.Countdown:
                _countLeft -= Time.deltaTime;
                if (_countLeft <= 0f)
                {
                    Current = State.Running;
                    if (HeldWeapon) { HeldWeapon.Refill(); HeldWeapon.FireEnabled = true; }
                }
                else if (LeavePressed()) EndRound(true);
                break;

            case State.Running:
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) { TimeLeft = 0f; EndRound(false); }
                else if (LeavePressed()) EndRound(true);
                break;

            case State.Results:
                if (LeavePressed()) Close();
                break;
        }
    }    
    void LateUpdate()
    {
        if (Current == State.Closed || Paused) return;
        ApplyCursor();
    }

    void ApplyCursor()
    {
        if (Current == State.Closed)
        {            
            if (inputLock) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        bool free = Current == State.Results;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    RangeWeapon FindWeaponInView()
    {
        if (!playerCamera) return null;

        RangeWeapon best = null;
        float bestAngle = pickupAngle;

        foreach (var w in weapons)
        {
            if (!w || w.Held || !w.gameObject.activeInHierarchy) continue;

            Vector3 to = w.transform.position - playerCamera.transform.position;
            if (to.magnitude > pickupRange) continue;

            float angle = Vector3.Angle(playerCamera.transform.forward, to);
            if (angle > bestAngle) continue;

            bestAngle = angle;
            best = w;
        }

        return best;
    }

    bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }    
    
    bool LeavePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }
}
