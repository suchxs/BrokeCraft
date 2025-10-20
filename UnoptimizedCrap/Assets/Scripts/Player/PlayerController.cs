using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal CharacterController-driven FPS movement tuned for smooth collisions.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private Transform cameraTransform;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference sneakAction;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = VoxelData.PlayerWalkSpeed;
    [SerializeField] private float sprintSpeed = VoxelData.PlayerSprintSpeed;
    [SerializeField] private float sneakSpeed = VoxelData.PlayerSneakSpeed;
    [SerializeField] private float jumpHeight = VoxelData.PlayerJumpHeight;
    [SerializeField] private float gravity = VoxelData.PlayerGravity;

    [Header("Character Controller")]
    [SerializeField] private float controllerHeight = VoxelData.PlayerHeight;
    [SerializeField] private float controllerRadius = VoxelData.PlayerWidth * 0.5f;
    [SerializeField] private float stepOffset = VoxelData.PlayerStepOffset;
    [SerializeField] private float slopeLimit = VoxelData.PlayerSlopeLimit;
    // Note: skinWidth and minMoveDistance are hardcoded in ConfigureController() for optimal collision

    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 0.15f;  // Tuned for 800 DPI
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float eyeHeight = VoxelData.PlayerEyeHeight;
    [SerializeField] private bool enableMouseSmoothing = true;
    [SerializeField] [Tooltip("Higher values increase smoothing lag. Set to ~0.03 for subtle dampening.")]
    private float mouseSmoothingTime = 0.035f;

    [Header("Streaming")]
    [SerializeField] private bool enableDynamicLoading = true;
    [SerializeField] private float chunkUpdateInterval = 0.5f;

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private Vector2 smoothedLookDelta;
    private Vector2 lookDeltaVelocity;
    private float verticalVelocity;
    private float chunkTimer;
    private int3 lastChunkPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        ConfigureController();

        if (cameraTransform == null)
        {
            var camera = GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                cameraTransform = camera.transform;
            }
        }

        yaw = transform.eulerAngles.y;
        pitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableAction(lookAction);
        EnableAction(jumpAction);
        EnableAction(sprintAction);
        EnableAction(sneakAction);
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(lookAction);
        DisableAction(jumpAction);
        DisableAction(sprintAction);
        DisableAction(sneakAction);
    }

    public void Initialize(World worldRef)
    {
        world = worldRef;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        // Read mouse input and apply player rotation IMMEDIATELY
        // Player body rotation must happen BEFORE movement calculation
        ReadMouseInput();
        
        // Apply player yaw rotation NOW so movement uses correct forward direction
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        
        UpdateMovement(deltaTime);
        UpdateChunkStreaming(transform.position, deltaTime);
    }

    private void LateUpdate()
    {
        // Only APPLY camera transform in LateUpdate (after physics settled)
        // Input was already read in Update, so no delay
        ApplyCameraTransform();
    }

    private void ReadMouseInput()
    {
        // Read mouse delta properly from Input System
        Vector2 lookDelta = Vector2.zero;
        
        if (lookAction != null && lookAction.action != null)
        {
            // ReadValue gives us the delta for this frame (not accumulated)
            lookDelta = lookAction.action.ReadValue<Vector2>();
        }

        if (enableMouseSmoothing)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                lookDelta = Vector2.SmoothDamp(smoothedLookDelta, lookDelta, ref lookDeltaVelocity, mouseSmoothingTime, Mathf.Infinity, dt);
                smoothedLookDelta = lookDelta;
            }
        }
        else
        {
            smoothedLookDelta = Vector2.zero;
            lookDeltaVelocity = Vector2.zero;
        }

        // Scale down raw pixel delta and make frame-rate independent
        // Pointer delta is in pixels, so we need much lower sensitivity
        yaw += lookDelta.x * mouseSensitivity;
        pitch -= lookDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
    }

    private void ApplyCameraTransform()
    {
        if (cameraTransform == null)
        {
            return;
        }

        // Set camera position and rotation directly in world space (not local)
        // This prevents sub-pixel jitter from CharacterController micro-adjustments
        Vector3 cameraWorldPos = transform.position + Vector3.up * eyeHeight;
        cameraTransform.position = cameraWorldPos;
        
        // Apply both player yaw and camera pitch
        cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateMovement(float deltaTime)
    {
        Vector2 moveInput = ReadVector2(moveAction);
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput = moveInput.normalized;
        }

        float targetSpeed = walkSpeed;
        if (IsPressed(sprintAction))
        {
            targetSpeed = sprintSpeed;
        }
        else if (IsPressed(sneakAction))
        {
            targetSpeed = sneakSpeed;
        }

        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        moveDirection = transform.TransformDirection(moveDirection);

        Vector3 velocity = moveDirection * targetSpeed;

        bool grounded = controller.isGrounded;
        
        // Use a small negative value when grounded to keep player "stuck" to ground
        // This prevents micro-bouncing and improves stability
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f;
        }

        if (grounded && WasTriggered(jumpAction))
        {
            verticalVelocity = math.sqrt(math.max(0.01f, 2f * gravity * math.max(0.01f, jumpHeight)));
        }

        verticalVelocity -= gravity * deltaTime;
        velocity.y = verticalVelocity;

        CollisionFlags flags = controller.Move(velocity * deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        if ((flags & CollisionFlags.Below) != 0)
        {
            verticalVelocity = -0.5f;
        }
    }

    private void UpdateChunkStreaming(Vector3 position, float deltaTime)
    {
        if (!enableDynamicLoading || world == null)
        {
            return;
        }

        chunkTimer -= deltaTime;
        if (chunkTimer > 0f)
        {
            return;
        }

        chunkTimer = math.max(0.05f, chunkUpdateInterval);
        float3 worldPos = new float3(position.x, position.y, position.z);
        int3 chunkPosition = World.WorldPosToChunkPos(worldPos);
        
        // Only update chunks if player moved to a different chunk
        // This prevents spam when hitting walls or idling
        if (math.all(chunkPosition == lastChunkPosition))
        {
            return;
        }
        
        lastChunkPosition = chunkPosition;
        world.LoadChunksAroundPosition(chunkPosition);
    }

    private void ConfigureController()
    {
        if (controller == null)
        {
            return;
        }

        controller.height = math.max(0.5f, controllerHeight);
        controller.radius = math.max(0.1f, controllerRadius);
        controller.stepOffset = math.max(0f, stepOffset);
        controller.slopeLimit = math.clamp(slopeLimit, 1f, 89f);
        
        // Reduced skinWidth to minimize collision push-out jitter
        controller.skinWidth = 0.001f;
        
        // Zero minMoveDistance prevents micro-movement accumulation
        controller.minMoveDistance = 0f;
        
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        controller.detectCollisions = true;
        controller.enableOverlapRecovery = true;
    }

    private static void EnableAction(InputActionReference reference)
    {
        reference?.action?.Enable();
    }

    private static void DisableAction(InputActionReference reference)
    {
        reference?.action?.Disable();
    }

    private static Vector2 ReadVector2(InputActionReference reference)
    {
        return reference?.action != null ? reference.action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool IsPressed(InputActionReference reference)
    {
        return reference?.action != null && reference.action.IsPressed();
    }

    private static bool WasTriggered(InputActionReference reference)
    {
        return reference?.action != null && reference.action.triggered;
    }
}
