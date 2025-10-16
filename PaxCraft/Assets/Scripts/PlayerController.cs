using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Minecraft Physics Values")]
    public float walkSpeed = 4.317f;           // Minecraft: 4.317 blocks/sec
    public float sprintSpeed = 5.612f;         // Minecraft: 5.612 blocks/sec
    public float jumpVelocity = 8.5f;          // Minecraft: jump velocity
    public float gravity = 32f;                // Minecraft: 32 blocks/s²
    public float terminalVelocity = -78.4f;    // Minecraft: terminal velocity
    
    [Header("Mouse Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;
    
    [Header("Collision Settings")]
    public float playerRadius = 0.3f;          // Player capsule radius
    public float playerHeight = 1.8f;          // Player height
    public LayerMask collisionMask;            // What to collide with (leave empty = all)
    
    // Components
    private CharacterController controller;
    private Camera playerCamera;
    
    // Movement state
    private Vector3 moveVelocity;
    private Vector3 verticalVelocity = new Vector3(0, -1f, 0); // Start with downward velocity
    private bool isGrounded;
    private float verticalRotation = 0f;
    private bool isReady = false; // Wait for World to spawn us
    
    void Awake()
    {
        // Get or add CharacterController
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
        
        // Configure controller with minimal settings (we handle collision manually)
        controller.center = new Vector3(0, playerHeight / 2f, 0);
        controller.height = playerHeight;
        controller.radius = playerRadius;
        controller.skinWidth = 0.005f;      // Very thin skin
        controller.minMoveDistance = 0f;    // No minimum
        controller.stepOffset = 0f;         // No auto-step (manual only)
        
        playerCamera = GetComponentInChildren<Camera>();
        
        Debug.Log($"[PlayerController] Initialized with Minecraft physics");
        Debug.Log($"  Walk: {walkSpeed} m/s, Sprint: {sprintSpeed} m/s");
        Debug.Log($"  Jump: {jumpVelocity} m/s, Gravity: {gravity} m/s²");
    }
    
    void Start()
    {
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Wait for World to spawn us before starting movement
        StartCoroutine(WaitForSpawn());
    }
    
    IEnumerator WaitForSpawn()
    {
        // Wait for World.cs to position us (it waits 0.5s)
        yield return new WaitForSeconds(0.6f);
        
        // NOW we're ready to start moving and falling
        isReady = true;
        verticalVelocity = new Vector3(0, -10f, 0); // Start falling immediately
        
        Debug.Log("[PlayerController] Spawn complete - ready to move and fall!");
    }
    
    void Update()
    {
        if (controller == null || !isReady)
            return;
        
        // Check if grounded
        CheckGrounded();
        
        // Handle input
        HandleMovement();
        HandleMouseLook();
        HandleCursorToggle();
    }
    
    void CheckGrounded()
    {
        // Check if standing on ground using raycast
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        float rayDistance = (playerHeight / 2f) + 0.2f;
        
        // Raycast downward
        bool rayHitGround = Physics.Raycast(rayOrigin, Vector3.down, rayDistance);
        
        // Use CharacterController's built-in check as backup
        isGrounded = controller.isGrounded || rayHitGround;
        
        // Debug visualization
        Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, isGrounded ? Color.green : Color.red, 0.1f);
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        // Calculate movement direction (relative to camera direction)
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        
        Vector3 inputDirection = (forward * vertical + right * horizontal);
        
        // Normalize to prevent faster diagonal movement
        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();
        
        // Determine speed (sprint with Shift)
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift) && inputDirection.magnitude > 0.1f;
        float targetSpeed = wantsToSprint ? sprintSpeed : walkSpeed;
        
        // Set velocity instantly (Minecraft has instant acceleration)
        if (inputDirection.magnitude > 0.01f)
        {
            moveVelocity = inputDirection * targetSpeed;
        }
        else
        {
            // Apply friction when no input
            moveVelocity *= 0.5f; // Minecraft stops quickly
            if (moveVelocity.magnitude < 0.1f)
                moveVelocity = Vector3.zero;
        }
        
        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = jumpVelocity;
        }
        
        // Apply gravity (ALWAYS, even when grounded for sticky ground)
        verticalVelocity.y -= gravity * Time.deltaTime;
        
        // Cap at terminal velocity
        verticalVelocity.y = Mathf.Max(verticalVelocity.y, terminalVelocity);
        
        // If grounded and falling, stop vertical movement
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f; // Small negative to "stick" to ground
        }
        
        // Combine horizontal and vertical movement
        Vector3 finalMove = (moveVelocity + verticalVelocity) * Time.deltaTime;
        
        // CRITICAL: Check collision before moving to prevent phasing
        finalMove = CheckAndPreventPhasing(finalMove);
        
        // Move
        controller.Move(finalMove);
    }
    
    // ANTI-PHASING SYSTEM - Uses SphereCast for robust collision detection
    Vector3 CheckAndPreventPhasing(Vector3 desiredMove)
    {
        // Separate horizontal and vertical movement
        Vector3 horizontalMove = new Vector3(desiredMove.x, 0, desiredMove.z);
        float verticalMove = desiredMove.y;
        
        if (horizontalMove.magnitude < 0.001f)
            return desiredMove; // No horizontal movement, nothing to check
        
        // Check for obstacles using MULTIPLE SphereCasts at different heights
        // This is more reliable than raycasts for catching 1-block walls
        float[] checkHeights = new float[] { 
            0.3f,   // Low (feet)
            0.6f,   // Medium-low  
            0.9f,   // Medium (CRITICAL for 1-block walls)
            1.2f,   // Medium-high
            1.5f    // High (head)
        };
        
        Vector3 moveDirection = horizontalMove.normalized;
        float moveDistance = horizontalMove.magnitude;
        float minSafeDistance = moveDistance;
        bool hitObstacle = false;
        
        foreach (float height in checkHeights)
        {
            Vector3 origin = transform.position + Vector3.up * height;
            RaycastHit hit;
            
            // SphereCast is much better than Raycast for collision detection
            // It checks a sphere moving through space (more reliable)
            float sphereRadius = playerRadius * 0.8f;
            float castDistance = moveDistance + playerRadius;
            
            if (Physics.SphereCast(origin, sphereRadius, moveDirection, out hit, castDistance))
            {
                hitObstacle = true;
                
                // Calculate safe distance (stop before hitting wall)
                float safeDistance = Mathf.Max(0, hit.distance - playerRadius * 1.2f);
                minSafeDistance = Mathf.Min(minSafeDistance, safeDistance);
                
                // Debug visualization
                Debug.DrawRay(origin, moveDirection * hit.distance, Color.red, 0.1f);
            }
            else
            {
                // No hit - draw green
                Debug.DrawRay(origin, moveDirection * castDistance, Color.green, 0.1f);
            }
        }
        
        // If we hit something, limit movement
        if (hitObstacle)
        {
            horizontalMove = moveDirection * minSafeDistance;
        }
        
        // Recombine horizontal and vertical
        return new Vector3(horizontalMove.x, verticalMove, horizontalMove.z);
    }
    
    void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;
        
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate player body horizontally
        transform.Rotate(Vector3.up * mouseX);
        
        // Rotate camera vertically
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
    
    void HandleCursorToggle()
    {
        // Unlock cursor with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        // Re-lock cursor on click
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    // Public getters for UI/debug
    public float GetCurrentSpeed()
    {
        if (controller == null) return 0f;
        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        return horizontalVel.magnitude;
    }
    
    public bool IsSprinting()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool isMoving = (horizontal != 0 || vertical != 0);
        return Input.GetKey(KeyCode.LeftShift) && isMoving && isGrounded;
    }
    
    public bool IsGrounded()
    {
        return isGrounded;
    }
}
