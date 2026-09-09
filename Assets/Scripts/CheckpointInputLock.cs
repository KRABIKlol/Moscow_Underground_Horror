using System.Collections.Generic;
using UnityEngine;

/// Пока на посту идёт проверка - замораживает игрока и отпускает курсор.
public class CheckpointInputLock : MonoBehaviour
{
    [Header("References")]
    public CheckpointController controller;
    public ShiftManager shift;
    [Tooltip("Leave empty to auto-find. When present, the player is frozen only while the check panel is open.")]
    public PlayerInteractor interactor;
    [Tooltip("Leave empty to auto-find. While training on the range is on, the player stays free.")]
    public ShootingRange range;

    [Tooltip("Drag the Player object here. Add the camera rig too if it is not a child of the player.")]
    public List<GameObject> objectsToFreeze = new List<GameObject>();

    public Rigidbody playerBody;

    [Tooltip("Component type names that must stay enabled, e.g. PlayerHealth. Leave empty normally.")]
    public List<string> keepEnabled = new List<string>();

    [Header("Options")]
    [Tooltip("Keep the player frozen while the visitor walks through the scanner too.")]
    public bool lockDuringScan = true;

    [Tooltip("Re-lock and hide the cursor when the menu closes (normal FPS behaviour).")]
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
        if (objectsToFreeze.Count == 0)
            Debug.LogWarning("[InputLock] Objects To Freeze is empty - nothing will be frozen.", this);

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
        // Экран итогов смены. Но если игрок ушёл в тир - он должен ходить и стрелять.
        if (shift && shift.Finished) return !(range && range.PlayerFree);

        // Есть интерактор - замораживаем ровно на время открытой панели.
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
                if (mb is PlayerInteractor) continue;   // иначе не закрыть панель по Esc
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
