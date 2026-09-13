using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public class PlayerInteractor : MonoBehaviour
{
   
    public Camera playerCamera;
    public CheckpointController controller;

   
    public float range = 6f;
   
    public float maxAngle = 40f;
   
    public float aimHeight = 1.2f;

    
    public bool allowDoors = true;
    public float doorRange = 3f;
    public LayerMask doorMask = ~0;

   
    public bool requireLineOfSight = false;
    public LayerMask obstacleMask = ~0;

    
    public KeyCode interactKey = KeyCode.E;
    public KeyCode closeKey = KeyCode.Escape;
    
    public string keyLabel = "E";

    public bool PanelOpen { get; private set; }
    
    public int LastCloseFrame { get; private set; } = -1;
    public Visitor Focused { get; private set; }
    public Door FocusedDoor { get; private set; }
    public string Prompt { get; private set; }

    Camera Cam
    {
        get
        {
            if (playerCamera) return playerCamera;
            if (Camera.main) return Camera.main;
            return GetComponentInChildren<Camera>();
        }
    }

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<CheckpointController>();
    }

    

    void Update()
    {
        if (PanelOpen)
        {
            if (!StageInteractive()) { Close(); return; }
            if (ClosePressed()) Close();
            return;
        }

        Focused = Detect();
        Prompt = BuildPrompt();

        
        FocusedDoor = (Focused == null && allowDoors) ? DetectDoor() : null;
        if (FocusedDoor) Prompt = FocusedDoor.PromptText(keyLabel);

        if (!InteractPressed()) return;

        if (Focused != null) Open();
        else if (FocusedDoor) FocusedDoor.Interact(Cam ? Cam.transform.position : transform.position);
    }

    Door DetectDoor()
    {
        var cam = Cam;
        if (!cam) return null;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out var hit, doorRange, doorMask, QueryTriggerInteraction.Ignore))
            return null;

      
        var door = hit.collider.GetComponentInParent<Door>();
        return door && door.playerCanUse ? door : null;
    }

    bool StageInteractive()
    {
        if (!controller) return false;

        var s = controller.CurrentStage;
        return s == CheckpointController.Stage.Documents ||
               s == CheckpointController.Stage.Inspection;
    }

    Visitor Detect()
    {
        if (!controller) return null;

        var v = controller.Current;
        if (v == null || !StageInteractive()) return null;

        var cam = Cam;
        if (!cam) return null;

        Vector3 target = v.transform.position + Vector3.up * aimHeight;
        Vector3 to = target - cam.transform.position;
        float dist = to.magnitude;

        if (dist > range) return null;
        if (Vector3.Angle(cam.transform.forward, to) > maxAngle) return null;

        if (requireLineOfSight &&
            Physics.Raycast(cam.transform.position, to.normalized, out var hit, dist,
                            obstacleMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<Visitor>() != v)
            return null;

        return v;
    }

    string BuildPrompt()
    {
        if (Focused == null || !controller) return null;

        switch (controller.CurrentStage)
        {
            case CheckpointController.Stage.Documents:  return $"[{keyLabel}]  Проверить документы";
            case CheckpointController.Stage.Inspection: return $"[{keyLabel}]  Осмотреть вещи";
            default: return null;
        }
    }

    public void Open()  => PanelOpen = true;
    public void Close()
    {
        PanelOpen = false;
        LastCloseFrame = Time.frameCount;
    }

    bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    bool ClosePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(closeKey);
#endif
    }

    void OnDrawGizmosSelected()
    {
        var cam = Cam;
        if (!cam) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawRay(cam.transform.position, cam.transform.forward * range);
    }
}
