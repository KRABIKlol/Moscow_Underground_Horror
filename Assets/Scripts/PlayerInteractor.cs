using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Взгляд игрока на посетителя: показывает подсказку и по клавише открывает панель проверки.
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty: uses Camera.main, then any camera in children.")]
    public Camera playerCamera;
    public CheckpointController controller;

    [Header("Detection")]
    [Tooltip("Max distance from the camera to the visitor, meters.")]
    public float range = 6f;
    [Tooltip("Max angle between where you look and the visitor, degrees.")]
    public float maxAngle = 40f;
    [Tooltip("Height above the visitor's feet used as the aim target.")]
    public float aimHeight = 1.2f;

    [Header("Line of sight (optional)")]
    [Tooltip("Require nothing solid between the camera and the visitor. Needs colliders in the scene.")]
    public bool requireLineOfSight = false;
    public LayerMask obstacleMask = ~0;

    [Header("Keys")]
    [Tooltip("Legacy Input Manager only. With the new Input System the keys are E and Escape.")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode closeKey = KeyCode.Escape;
    [Tooltip("Letter shown in the on-screen prompt.")]
    public string keyLabel = "E";

    public bool PanelOpen { get; private set; }
    public Visitor Focused { get; private set; }
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

    void Start()
    {
        if (!Cam)
            Debug.LogError("[Interactor] Камера не найдена. Заполни поле Player Camera " +
                           "или поставь камере тег MainCamera.", this);
    }

    void Update()
    {
        if (!controller) return;

        if (PanelOpen)
        {
            if (!StageInteractive()) { Close(); return; }
            if (ClosePressed()) Close();
            return;
        }

        Focused = Detect();
        Prompt = BuildPrompt();

        if (!string.IsNullOrEmpty(Prompt) && InteractPressed()) Open();
    }

    bool StageInteractive()
    {
        var s = controller.CurrentStage;
        return s == CheckpointController.Stage.Documents ||
               s == CheckpointController.Stage.Inspection;
    }

    Visitor Detect()
    {
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
        if (Focused == null) return null;

        switch (controller.CurrentStage)
        {
            case CheckpointController.Stage.Documents:  return $"[{keyLabel}]  Проверить документы";
            case CheckpointController.Stage.Inspection: return $"[{keyLabel}]  Осмотреть вещи";
            default: return null;
        }
    }

    public void Open()  => PanelOpen = true;
    public void Close() => PanelOpen = false;

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
