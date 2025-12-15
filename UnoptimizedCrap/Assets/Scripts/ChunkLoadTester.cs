using Unity.Mathematics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Test script for cubic chunk dynamic loading.
/// Uses NEW Input System if available, falls back to old Input otherwise.
/// Burst-compatible movement logic.
/// </summary>
public class ChunkLoadTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float verticalSpeed = 5f;
    
    [Header("Chunk Loading")]
    [SerializeField] private bool enableDynamicLoading = true;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private bool logChunkMoves = false;
    
    private int3 lastChunkPos;
    private float lastUpdateTime;
    
#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard;
#endif
    
    private void Start()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<World>();
        }
        
        if (world == null)
        {
            Debug.LogError("ChunkLoadTester: No World found in scene!");
            enabled = false;
            return;
        }
        
#if ENABLE_INPUT_SYSTEM
        keyboard = Keyboard.current;
        if (keyboard == null)
        {
            Debug.LogWarning("ChunkLoadTester: No keyboard found!");
        }
#endif
        
        // Initialize position
        lastChunkPos = CubicChunkHelper.WorldFloatPosToChunkPos(transform.position);
        lastUpdateTime = Time.time;
        
        Debug.Log($"[ChunkLoadTester] Started at chunk {lastChunkPos}");
    }
    
    private void Update()
    {
        HandleMovement();
        
        if (enableDynamicLoading)
        {
            HandleChunkLoading();
        }
        
        // Debug info
#if ENABLE_INPUT_SYSTEM
        if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
        {
            PrintDebugInfo();
        }
#else
        if (Input.GetKeyDown(KeyCode.I))
        {
            PrintDebugInfo();
        }
#endif
    }
    
    private void HandleMovement()
    {
        float horizontal = 0f;
        float forward = 0f;
        float vertical = 0f;
        
#if ENABLE_INPUT_SYSTEM
        // NEW Input System
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) forward = 1f;
            if (keyboard.sKey.isPressed) forward = -1f;
            if (keyboard.aKey.isPressed) horizontal = -1f;
            if (keyboard.dKey.isPressed) horizontal = 1f;
            if (keyboard.qKey.isPressed) vertical = -1f;
            if (keyboard.eKey.isPressed) vertical = 1f;
        }
#else
        // OLD Input System fallback
        if (Input.GetKey(KeyCode.W)) forward = 1f;
        if (Input.GetKey(KeyCode.S)) forward = -1f;
        if (Input.GetKey(KeyCode.A)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D)) horizontal = 1f;
        if (Input.GetKey(KeyCode.Q)) vertical = -1f;
        if (Input.GetKey(KeyCode.E)) vertical = 1f;
#endif
        
        // Move transform
        Vector3 moveDir = new Vector3(horizontal, 0, forward).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
        transform.position += Vector3.up * vertical * verticalSpeed * Time.deltaTime;
    }
    
    private void HandleChunkLoading()
    {
        // Only check periodically to avoid performance issues
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        // Get current chunk position
        int3 currentChunkPos = CubicChunkHelper.WorldFloatPosToChunkPos(transform.position);
        
        // If we moved to a new chunk, trigger loading
        if (!currentChunkPos.Equals(lastChunkPos))
        {
            if (logChunkMoves)
            {
                Debug.Log($"[ChunkLoadTester] Moved to chunk {currentChunkPos} (world Y: {currentChunkPos.y * VoxelData.ChunkHeight})");
            }
            world.LoadChunksAroundPosition(currentChunkPos);
            lastChunkPos = currentChunkPos;
        }
    }
    
    private void PrintDebugInfo()
    {
        int3 currentChunk = CubicChunkHelper.WorldFloatPosToChunkPos(transform.position);
        float3 worldPos = transform.position;
        
        Debug.Log("=== Chunk Load Tester Info ===");
        Debug.Log($"World Position: ({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})");
        Debug.Log($"Chunk Position: {currentChunk}");
        Debug.Log($"Loaded Chunks: {world.GetLoadedChunkCount()}");
        Debug.Log($"Dynamic Loading: {(enableDynamicLoading ? "ON" : "OFF")}");
        Debug.Log("\nControls:");
        Debug.Log("  WASD - Move horizontally");
        Debug.Log("  Q/E - Move vertically (up/down)");
        Debug.Log("  I - Show debug info");
        Debug.Log("  F3 - Toggle debug overlay");
    }
}
