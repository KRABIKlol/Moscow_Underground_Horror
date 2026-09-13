using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
   
    [SerializeField] private float acceleration = 12f;

    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    
    [SerializeField] private bool crouchIsToggle = false;
    
    [SerializeField] private KeyCode crouchKeyLegacy = KeyCode.LeftControl;

  
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string groundedParam = "IsGrounded";
    [SerializeField] private string crouchParam = "IsCrouching";
    [SerializeField] private string jumpTrigger = "Jump";

    private CharacterController controller;
    private Vector3 velocity;
    private float currentSpeed;
    private float cameraStandY;
    private float cameraCrouchY;
    private float pitch;
    private bool isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight / 2f, 0f);

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera != null)
        {
            cameraStandY = playerCamera.transform.localPosition.y;
            cameraCrouchY = cameraStandY - (standingHeight - crouchHeight);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {       
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
    }
    

    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX, mouseY;
#if ENABLE_INPUT_SYSTEM
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        mouseX = delta.x * mouseSensitivity * 0.02f;
        mouseY = delta.y * mouseSensitivity * 0.02f;
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif
        transform.Rotate(Vector3.up * mouseX);

        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCrouch()
    {
        bool held = GetCrouchHeld();
        bool down = GetCrouchDown();

        if (crouchIsToggle)
        {
            if (down)
            {
                if (!isCrouching) isCrouching = true;
                else if (CanStandUp()) isCrouching = false;
            }
        }
        else
        {
            if (held) isCrouching = true;
            else if (isCrouching && CanStandUp()) isCrouching = false;
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);

        if (playerCamera != null)
        {
            float targetCamY = isCrouching ? cameraCrouchY : cameraStandY;
            Vector3 camPos = playerCamera.transform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            playerCamera.transform.localPosition = camPos;
        }
    }

    private void HandleMovement()
    {
        float inputX = 0f, inputZ = 0f;
        bool sprintHeld = false, jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.dKey.isPressed) inputX += 1f;
            if (kb.aKey.isPressed) inputX -= 1f;
            if (kb.wKey.isPressed) inputZ += 1f;
            if (kb.sKey.isPressed) inputZ -= 1f;
            sprintHeld = kb.leftShiftKey.isPressed;
            jumpPressed = kb.spaceKey.wasPressedThisFrame;
        }
#else
        inputX = Input.GetAxisRaw("Horizontal");
        inputZ = Input.GetAxisRaw("Vertical");
        sprintHeld = Input.GetKey(KeyCode.LeftShift);
        jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif

        Vector3 inputDir = Vector3.ClampMagnitude(new Vector3(inputX, 0f, inputZ), 1f);
        Vector3 worldDir = transform.TransformDirection(inputDir);

        float maxSpeed = isCrouching ? crouchSpeed : (sprintHeld ? sprintSpeed : walkSpeed);
        float targetSpeed = maxSpeed * inputDir.magnitude;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        bool grounded = controller.isGrounded;
        bool didJump = false;
        if (grounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f; 

            if (jumpPressed && !isCrouching)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                didJump = true;
            }
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 motion = worldDir * currentSpeed + Vector3.up * velocity.y;
        controller.Move(motion * Time.deltaTime);

        UpdateAnimator(grounded, didJump);
    }

    private void UpdateAnimator(bool grounded, bool didJump)
    {
        if (animator == null) return;

        animator.SetFloat(speedParam, currentSpeed, 0.15f, Time.deltaTime);
        animator.SetBool(groundedParam, grounded);
        animator.SetBool(crouchParam, isCrouching);

        if (didJump)
            animator.SetTrigger(jumpTrigger);
    }

    private bool GetCrouchHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
#else
        return Input.GetKey(crouchKeyLegacy);
#endif
    }

    private bool GetCrouchDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(crouchKeyLegacy);
#endif
    }

    private bool CanStandUp()
    {
        float checkDistance = standingHeight - controller.height;
        if (checkDistance <= 0.01f) return true;

        Vector3 origin = transform.position + Vector3.up * controller.height;
        return !Physics.Raycast(origin, Vector3.up, checkDistance, ~0, QueryTriggerInteraction.Ignore);
    }
}