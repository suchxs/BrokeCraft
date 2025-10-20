using Unity.Mathematics;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Minecraft-style first-person player controller with accurate physics.
/// Uses Unity's Input System (new or old) and CharacterController for movement.
/// Movement values match Minecraft Java Edition.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private Transform cameraTransform;
    
    [Header("Movement - Minecraft Values")]
    [Tooltip("Walking speed (4.317 m/s in Minecraft)")]
    [SerializeField] private float walkSpeed = 4.317f;
    
    [Tooltip("Sprinting speed (5.612 m/s in Minecraft)")]
    [SerializeField] private float sprintSpeed = 5.612f;
    
    [Tooltip("Sneaking speed (1.295 m/s in Minecraft)")]
    [SerializeField] private float sneakSpeed = 1.295f;
    
    [Header("Jump & Gravity - Minecraft Values")]
    [Tooltip("Jump height in blocks (1.25 blocks in Minecraft)")]
    [SerializeField] private float jumpHeight = 1.25f;
    
    [Tooltip("Gravity acceleration (32 m/s² in Minecraft)")]
    [SerializeField] private float gravity = 32f;
    
    [Header("Camera")]
    [Tooltip("Mouse sensitivity for camera look")]
    [SerializeField] private float mouseSensitivity = 2f;
    
    [Tooltip("Maximum vertical look angle (prevents flipping)")]
    [SerializeField] private float maxLookAngle = 90f;
    
    [Header("Player Dimensions - Minecraft Values")]
    [Tooltip("Player height (1.8 blocks in Minecraft)")]
    [SerializeField] private float playerHeight = 1.8f;
    
    [Tooltip("Player eye height from ground (1.62 blocks in Minecraft)")]
    [SerializeField] private float eyeHeight = 1.62f;
    
    [Tooltip("Player width (0.6 blocks in Minecraft)")]
    [SerializeField] private float playerWidth = 0.6f;
    
    [Header("Dynamic Chunk Loading")]
    [SerializeField] private bool enableDynamicLoading = true;
    [SerializeField] private float chunkUpdateInterval = 0.5f;
    
    // Components
    private CharacterController characterController;
    
#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard;
    private Mouse mouse;
#endif
    
    // Movement state
    private Vector3 velocity;
    private bool isSprinting;
    private bool isSneaking;
    private bool isGrounded;
    
    // Camera rotation
    private float cameraPitch;
    private float playerYaw;
    
    // Chunk loading tracking
    private int3 lastChunkPos;
    private float lastChunkUpdateTime;
    
    // Input values
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    
    private void Awake()
    {
        // Setup CharacterController with Minecraft-accurate dimensions and collision settings
        characterController = GetComponent<CharacterController>();
        characterController.height = VoxelData.PlayerHeight;
        characterController.radius = VoxelData.PlayerWidth / 2f;
        characterController.center = new Vector3(0, VoxelData.PlayerHeight / 2f, 0);
        
        // Critical settings to prevent phasing through blocks (Minecraft-precise collision)
        characterController.skinWidth = VoxelData.PlayerSkinWidth;
        characterController.minMoveDistance = VoxelData.PlayerMinMoveDistance;
        characterController.stepOffset = VoxelData.PlayerStepOffset;
        characterController.slopeLimit = VoxelData.PlayerSlopeLimit;
        
        // Setup camera if not assigned
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
            if (cameraTransform != null)
            {
                // Position camera at eye height
                cameraTransform.SetParent(transform);
                cameraTransform.localPosition = new Vector3(0, eyeHeight, 0);
                cameraTransform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError("PlayerController: No camera found! Assign a camera or tag one as MainCamera.");
            }
        }
        
        // Lock and hide cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        keyboard = Keyboard.current;
        mouse = Mouse.current;
        
        if (keyboard == null)
        {
            Debug.LogWarning("PlayerController: No keyboard found!");
        }
        if (mouse == null)
        {
            Debug.LogWarning("PlayerController: No mouse found!");
        }
#endif
    }
    
    private void Start()
    {
        // Find world if not assigned
        if (world == null)
        {
            world = FindObjectOfType<World>();
            if (world == null)
            {
                Debug.LogWarning("PlayerController: No World found - dynamic chunk loading disabled.");
                enableDynamicLoading = false;
            }
        }
        
        // Initialize chunk tracking
        lastChunkPos = CubicChunkHelper.WorldFloatPosToChunkPos(transform.position);
        lastChunkUpdateTime = Time.time;
        
        // Initialize camera rotation from current transform
        playerYaw = transform.eulerAngles.y;
        cameraPitch = cameraTransform != null ? cameraTransform.localEulerAngles.x : 0f;
        if (cameraPitch > 180f) cameraPitch -= 360f; // Normalize to -180 to 180
        
        Debug.Log($"[PlayerController] Spawned at world position {transform.position}");
        Debug.Log($"[PlayerController] Chunk position: {lastChunkPos}");
    }
    
    private void Update()
    {
        HandleInput();
        HandleCameraRotation();
        HandleMovement();
        HandleChunkLoading();
        
        // Debug toggle cursor lock with Escape
#if ENABLE_INPUT_SYSTEM
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ToggleCursorLock();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorLock();
        }
#endif
    }
    
    /// <summary>
    /// Handle input from keyboard/mouse (supports both Input Systems)
    /// </summary>
    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        // NEW Input System
        if (keyboard != null)
        {
            // Movement input
            moveInput = Vector2.zero;
            if (keyboard.wKey.isPressed) moveInput.y = 1f;
            if (keyboard.sKey.isPressed) moveInput.y = -1f;
            if (keyboard.aKey.isPressed) moveInput.x = -1f;
            if (keyboard.dKey.isPressed) moveInput.x = 1f;
            
            // Jump input
            if (keyboard.spaceKey.wasPressedThisFrame)
                jumpPressed = true;
            
            // Sprint input
            isSprinting = keyboard.leftShiftKey.isPressed;
            
            // Sneak toggle
            if (keyboard.leftCtrlKey.wasPressedThisFrame)
                isSneaking = !isSneaking;
        }
        
        if (mouse != null)
        {
            // Mouse look input
            lookInput = mouse.delta.ReadValue();
        }
#else
        // OLD Input System fallback
        moveInput = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) moveInput.y = 1f;
        if (Input.GetKey(KeyCode.S)) moveInput.y = -1f;
        if (Input.GetKey(KeyCode.A)) moveInput.x = -1f;
        if (Input.GetKey(KeyCode.D)) moveInput.x = 1f;
        
        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;
        
        isSprinting = Input.GetKey(KeyCode.LeftShift);
        
        if (Input.GetKeyDown(KeyCode.LeftControl))
            isSneaking = !isSneaking;
        
        // Mouse look
        lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }
    
    /// <summary>
    /// Handle first-person camera rotation with mouse look
    /// </summary>
    private void HandleCameraRotation()
    {
        if (cameraTransform == null) return;
        
        // Apply mouse sensitivity (scale down new Input System delta which is in pixels)
#if ENABLE_INPUT_SYSTEM
        float mouseX = lookInput.x * mouseSensitivity * 0.02f; // Scale for new Input System
        float mouseY = lookInput.y * mouseSensitivity * 0.02f;
#else
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
#endif
        
        // Rotate player body horizontally (yaw)
        playerYaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, playerYaw, 0f);
        
        // Rotate camera vertically (pitch) with clamping
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }
    
    /// <summary>
    /// Handle player movement with Minecraft-accurate physics
    /// </summary>
    private void HandleMovement()
    {
        // Check if grounded (with small buffer for slopes)
        isGrounded = characterController.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            // Reset falling velocity when grounded
            velocity.y = -2f; // Small downward force to keep grounded
        }
        
        // Determine movement speed based on state
        float currentSpeed = walkSpeed;
        if (isSneaking)
        {
            currentSpeed = sneakSpeed;
        }
        else if (isSprinting && moveInput.y > 0) // Can only sprint forward
        {
            currentSpeed = sprintSpeed;
        }
        
        // Calculate movement direction relative to camera
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        // Get input direction
        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        
        // Apply horizontal movement
        Vector3 horizontalMove = moveDirection * currentSpeed;
        
        // Handle jumping (Minecraft-style)
        if (jumpPressed && isGrounded)
        {
            // Calculate jump velocity to reach desired height
            // Using physics formula: v = sqrt(2 * g * h)
            velocity.y = Mathf.Sqrt(2f * gravity * jumpHeight);
        }
        jumpPressed = false; // Reset jump
        
        // Apply gravity
        velocity.y -= gravity * Time.deltaTime;
        
        // Combine horizontal and vertical movement
        Vector3 finalMove = horizontalMove + new Vector3(0, velocity.y, 0);
        
        // Move character
        characterController.Move(finalMove * Time.deltaTime);
    }
    
    /// <summary>
    /// Update chunk loading based on player position
    /// </summary>
    private void HandleChunkLoading()
    {
        if (!enableDynamicLoading || world == null)
            return;
        
        // Only check periodically for performance
        if (Time.time - lastChunkUpdateTime < chunkUpdateInterval)
            return;
        
        lastChunkUpdateTime = Time.time;
        
        // Get current chunk position
        int3 currentChunkPos = CubicChunkHelper.WorldFloatPosToChunkPos(transform.position);
        
        // If we moved to a new chunk, trigger loading
        if (!currentChunkPos.Equals(lastChunkPos))
        {
            world.LoadChunksAroundPosition(currentChunkPos);
            lastChunkPos = currentChunkPos;
            
            Debug.Log($"[PlayerController] Entered chunk {currentChunkPos} (world Y: {transform.position.y:F1})");
        }
    }
    
    /// <summary>
    /// Toggle cursor lock state (for debugging/menu access)
    /// </summary>
    private void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
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
    
    /// <summary>
    /// Get player's current movement state for debugging
    /// </summary>
    public string GetMovementState()
    {
        if (isSneaking) return "Sneaking";
        if (isSprinting) return "Sprinting";
        return "Walking";
    }
    
    private void OnGUI()
    {
        // Simple debug HUD
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("Player Debug Info");
        GUILayout.Label($"Position: {transform.position.ToString("F1")}");
        GUILayout.Label($"Chunk: {lastChunkPos}");
        GUILayout.Label($"State: {GetMovementState()}");
        GUILayout.Label($"Grounded: {isGrounded}");
        GUILayout.Label($"Velocity Y: {velocity.y:F2}");
        
        if (world != null)
        {
            GUILayout.Label($"Loaded Chunks: {world.GetLoadedChunkCount()}");
        }
        
        GUILayout.Label("\nControls:");
        GUILayout.Label("WASD - Move");
        GUILayout.Label("Space - Jump");
        GUILayout.Label("Shift - Sprint");
        GUILayout.Label("Ctrl - Sneak");
        GUILayout.Label("ESC - Toggle Cursor");
        GUILayout.EndArea();
    }
}
