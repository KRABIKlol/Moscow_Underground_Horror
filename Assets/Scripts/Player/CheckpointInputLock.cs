using System.Collections.Generic;
using UnityEngine;

public class CheckpointInputLock : MonoBehaviour
{
   
    public CheckpointController controller;
    public ShiftManager shift;
   
    public PlayerInteractor interactor;
   
    public ShootingRange range;

    
    public List<GameObject> objectsToFreeze = new List<GameObject>();

    public Rigidbody playerBody;

    public List<string> keepEnabled = new List<string>();

   
    public bool lockDuringScan = true;

    public bool lockCursorWhenFree = true;

    public bool IsLocked { get; private set; }

    readonly List<MonoBehaviour> _disabled = new List<MonoBehaviour>();

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<CheckpointController>();
        if (!shift) shift = FindFirstObjectByType<ShiftManager>();
        if (!interactor) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (!range) range = FindFirstObjectByType<ShootingRange>();
    }

    void Start()
    {
       

        SetCursor(false);
    }

    void Update()
    {
        bool need = NeedLock();
        if (need == IsLocked) return;

        IsLocked = need;
        if (need) Freeze();
        else Unfreeze();
        SetCursor(need);
    }

    bool NeedLock()
    {
       
        if (shift && shift.Finished) return !(range && range.PlayerFree);

        if (interactor) return interactor.PanelOpen;

        if (!controller) return false;

        var s = controller.CurrentStage;
        if (lockDuringScan) return s != CheckpointController.Stage.Empty;

        return s == CheckpointController.Stage.Documents ||
               s == CheckpointController.Stage.Inspection;
    }

    void OnDisable()
    {
        if (!IsLocked) return;
        IsLocked = false;
        Unfreeze();
        SetCursor(false);
    }

    void Freeze()
    {
        _disabled.Clear();

        foreach (var go in objectsToFreeze)
        {
            if (!go) continue;

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb || mb == this || !mb.enabled) continue;
                if (mb is PlayerInteractor) continue;  
                if (keepEnabled.Contains(mb.GetType().Name)) continue;

                mb.enabled = false;
                _disabled.Add(mb);
            }
        }

        if (playerBody)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }
    }

    void Unfreeze()
    {
        foreach (var mb in _disabled)
            if (mb) mb.enabled = true;

        _disabled.Clear();
    }

    void SetCursor(bool menuOpen)
    {
        if (menuOpen || !lockCursorWhenFree)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
