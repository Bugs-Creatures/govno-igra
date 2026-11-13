using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Min(0f)] private float walkSpeed = 4f;
    [SerializeField, Min(0f)] private float sprintSpeed = 7f;
    [SerializeField, Min(0f)] private float crouchSpeed = 2f;
    [SerializeField, Min(0.1f)] private float crouchHeight = 1.15f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;

    private const float Gravity = 20f;
    private const float GroundingSpeed = 2f;
    private const float PitchLimit = 85f;

    private CharacterController controller;
    private InputActionMap playerActions;
    private InputAction move;
    private InputAction look;
    private InputAction sprint;
    private InputAction jump;
    private InputAction crouch;
    private Transform view;
    private Vector3 standingViewPosition;
    private Vector3 standingCenter;
    private float standingHeight;
    private float pitch;
    private float verticalSpeed;
    private bool crouched;

    public bool HasGameplayInput { get; private set; }
    public bool IsGrounded => controller.isGrounded;
    public Vector3 Velocity => controller.velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (viewCamera == null || inputActions == null)
        {
            Debug.LogError("Assign a view camera and input actions to the first-person controller.", this);
            enabled = false;
            return;
        }

        view = viewCamera.transform;
        standingViewPosition = view.localPosition;
        standingCenter = controller.center;
        standingHeight = controller.height;
        crouchHeight = Mathf.Clamp(crouchHeight, 2f * controller.radius, standingHeight);
        pitch = Mathf.DeltaAngle(0f, view.localEulerAngles.x);

        playerActions = inputActions.FindActionMap("Player", throwIfNotFound: true).Clone();
        playerActions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        move = playerActions.FindAction("Move", throwIfNotFound: true);
        look = playerActions.FindAction("Look", throwIfNotFound: true);
        sprint = playerActions.FindAction("Sprint", throwIfNotFound: true);
        jump = playerActions.FindAction("Jump", throwIfNotFound: true);
        crouch = playerActions.FindAction("Crouch", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        playerActions.Enable();
        SetCursorCaptured(true);
    }

    private void OnDisable()
    {
        HasGameplayInput = false;
        if (playerActions == null)
            return;

        playerActions.Disable();
        SetCursorCaptured(false);
    }

    private void OnDestroy()
    {
        playerActions?.Dispose();
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SetCursorCaptured(false);

        bool captured = Cursor.lockState == CursorLockMode.Locked;
        HasGameplayInput = captured && Application.isFocused;
        if (!captured && Mouse.current?.leftButton.wasPressedThisFrame == true)
            SetCursorCaptured(true);

        Vector2 movement = Vector2.zero;
        bool jumping = false;
        bool sprinting = false;
        if (HasGameplayInput)
        {
            Vector2 mouseDelta = look.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, mouseDelta.x, 0f);
            pitch = Mathf.Clamp(pitch - mouseDelta.y, -PitchLimit, PitchLimit);
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            jumping = jump.WasPressedThisFrame();
            sprinting = sprint.IsPressed();
        }

        bool grounded = controller.isGrounded;
        UpdateStance(HasGameplayInput && crouch.IsPressed());

        if (grounded)
        {
            if (jumping)
                verticalSpeed = Mathf.Sqrt(2f * Gravity * jumpHeight);
            else if (verticalSpeed < 0f)
                verticalSpeed = -GroundingSpeed;
        }

        float dt = Time.deltaTime;
        verticalSpeed -= Gravity * dt;
        float speed = walkSpeed;
        if (crouched)
            speed = crouchSpeed;
        else if (sprinting)
            speed = sprintSpeed;

        Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * speed;
        velocity.y = verticalSpeed;
        CollisionFlags collisions = controller.Move(velocity * dt);
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;
    }

    private void UpdateStance(bool wantsToCrouch)
    {
        if (crouched == wantsToCrouch)
            return;

        float heightDifference = standingHeight - crouchHeight;
        if (!wantsToCrouch && heightDifference > 0f)
        {
            Vector3 head = controller.center + Vector3.up * (controller.height * 0.5f - controller.radius);
            if (Physics.SphereCast(transform.TransformPoint(head), controller.radius, transform.up, out _,
                    heightDifference, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
        }

        crouched = wantsToCrouch;
        float drop = crouched ? heightDifference : 0f;
        controller.height = standingHeight - drop;
        controller.center = standingCenter - Vector3.up * (drop * 0.5f);
        view.localPosition = standingViewPosition - Vector3.up * drop;
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}
