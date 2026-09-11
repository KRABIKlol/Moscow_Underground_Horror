using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Камера (обзор)")]
    [Tooltip("Камера от первого лица. Если не назначить вручную, будет найдена автоматически среди дочерних объектов.")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Скорость передвижения")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [Tooltip("Как быстро текущая скорость нарастает/тормозит при смене режима движения")]
    [SerializeField] private float acceleration = 12f;

    [Header("Прыжок и гравитация")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Присед")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [Tooltip("Если включено — присед переключается нажатием клавиши, а не удержанием")]
    [SerializeField] private bool crouchIsToggle = false;
    [Tooltip("Клавиша приседа для старого Input Manager (в новом Input System используется левый Ctrl)")]
    [SerializeField] private KeyCode crouchKeyLegacy = KeyCode.LeftControl;

    [Header("Аниматор (необязательно)")]
    [Tooltip("Animator модели персонажа (например, охранника). Если не назначить — анимация просто не будет обновляться.")]
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
        HandleEscape();
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
    }

    private void HandleEscape()
    {
        // Удобно для тестирования в редакторе: Esc возвращает курсор мыши.
#if ENABLE_INPUT_SYSTEM
        bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (escPressed)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState == CursorLockMode.None)
        {
#if ENABLE_INPUT_SYSTEM
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            bool clicked = Input.GetMouseButtonDown(0);
#endif
            if (clicked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
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

        // Целевая скорость учитывает величину ввода — если WASD не нажаты, она равна 0,
        // а не "разгоняется" впустую только от удержания Shift. Это важно и для движения,
        // и для параметра Speed, который пойдёт в аниматор.
        float maxSpeed = isCrouching ? crouchSpeed : (sprintHeld ? sprintSpeed : walkSpeed);
        float targetSpeed = maxSpeed * inputDir.magnitude;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        bool grounded = controller.isGrounded;
        bool didJump = false;
        if (grounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f; // прижимает к земле, чтобы isGrounded не "мигал"

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

        // Speed — обычная скорость персонажа (м/с). Пороги в Blend Tree можно
        // задавать теми же числами, что стоят в полях Walk/Sprint/Crouch Speed выше.
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