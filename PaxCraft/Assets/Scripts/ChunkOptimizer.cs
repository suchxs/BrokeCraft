using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handles chunk visibility optimization (frustum culling, distance culling)
public class ChunkOptimizer : MonoBehaviour
{
    [Header("Optimization Settings")]
    public bool enableOptimization = false;    // MASTER SWITCH - disabled for small static worlds
    public float updateFrequency = 0.5f;       // How often to update (seconds)
    public bool enableFrustumCulling = false;  // Disabled by default (Minecraft doesn't use this)
    public float colliderDistance = 200f;      // Distance to keep colliders enabled (blocks)
    
    private Camera playerCamera;
    private Transform playerTransform;
    private World world;
    private Plane[] frustumPlanes;
    
    // Performance stats
    private int visibleChunks = 0;
    private int totalChunks = 0;
    
    void Start()
    {
        // Find references
        playerCamera = Camera.main;
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
            playerTransform = player.transform;
        
        world = FindObjectOfType<World>();
        
        // Start optimization loop
        StartCoroutine(OptimizeChunks());
    }
    
    IEnumerator OptimizeChunks()
    {
        // Wait for world to initialize
        yield return new WaitForSeconds(2f);
        
        Debug.Log("[ChunkOptimizer] Starting chunk optimization (renderer/collider management)...");
        
        while (true)
        {
            yield return new WaitForSeconds(updateFrequency);
            
            if (playerCamera != null && world != null && playerTransform != null)
            {
                UpdateChunkVisibility();
            }
        }
    }
    
    void UpdateChunkVisibility()
    {
        // If optimization disabled, make sure everything is enabled
        if (!enableOptimization)
        {
            foreach (var chunk in world.GetAllChunks())
            {
                ChunkSection[] sections = chunk.GetComponentsInChildren<ChunkSection>();
                foreach (var section in sections)
                {
                    if (section.meshRenderer != null)
                        section.meshRenderer.enabled = true;
                    if (section.meshCollider != null)
                        section.meshCollider.enabled = true;
                }
            }
            return;
        }
        
        // Calculate frustum planes for culling
        if (enableFrustumCulling)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        }
        
        visibleChunks = 0;
        totalChunks = 0;
        int collidersEnabled = 0;
        
        Vector3 playerPos = playerTransform != null ? playerTransform.position : playerCamera.transform.position;
        
        // Check all chunks
        foreach (var chunk in world.GetAllChunks())
        {
            totalChunks++;
            bool shouldRender = ShouldChunkBeVisible(chunk, playerPos);
            
            // Enable/disable chunk sections
            SetChunkVisibility(chunk, shouldRender);
            
            if (shouldRender)
                visibleChunks++;
            
            // Count active colliders (for debug)
            Vector3 chunkCenter = chunk.transform.position + new Vector3(VoxelData.ChunkWidth / 2f, 0, VoxelData.ChunkWidth / 2f);
            if (Vector3.Distance(chunkCenter, playerPos) < 200f)
                collidersEnabled++;
        }
        
        // Debug log occasionally
        if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
        {
            Debug.Log($"[ChunkOptimizer] Visible: {visibleChunks}/{totalChunks}, Colliders: {collidersEnabled}");
        }
    }
    
    bool ShouldChunkBeVisible(Chunk chunk, Vector3 playerPos)
    {
        // Get chunk bounds
        Bounds chunkBounds = GetChunkBounds(chunk);
        
        // Frustum culling (optional, disabled by default for Minecraft behavior)
        // NOTE: World.cs now handles distance-based loading/unloading
        if (enableFrustumCulling)
        {
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds))
                return false;
        }
        
        // Always render loaded chunks (World.cs handles which chunks exist)
        return true;
    }
    
    Bounds GetChunkBounds(Chunk chunk)
    {
        Vector3 center = chunk.transform.position + new Vector3(
            VoxelData.ChunkWidth / 2f,
            VoxelData.ChunkHeight / 2f,
            VoxelData.ChunkWidth / 2f
        );
        
        Vector3 size = new Vector3(
            VoxelData.ChunkWidth,
            VoxelData.ChunkHeight,
            VoxelData.ChunkWidth
        );
        
        return new Bounds(center, size);
    }
    
    void SetChunkVisibility(Chunk chunk, bool visible)
    {
        // Enable/disable all section renderers
        ChunkSection[] sections = chunk.GetComponentsInChildren<ChunkSection>();
        
        Vector3 playerPos = playerTransform != null ? playerTransform.position : playerCamera.transform.position;
        
        // Calculate distance from player to chunk CENTER (more accurate)
        Vector3 chunkCenter = chunk.transform.position + new Vector3(
            VoxelData.ChunkWidth / 2f,
            0,
            VoxelData.ChunkWidth / 2f
        );
        float distToChunk = Vector3.Distance(chunkCenter, playerPos);
        
        foreach (var section in sections)
        {
            // Manage renderer visibility (frustum culling if enabled)
            if (section.meshRenderer != null)
                section.meshRenderer.enabled = visible;
            
            // Manage collider based on distance (keep nearby for player physics)
            if (section.meshCollider != null)
            {
                bool needsCollider = distToChunk < colliderDistance;
                
                // Only manage collider if mesh exists
                if (section.meshFilter != null && section.meshFilter.mesh != null)
                {
                    if (section.meshFilter.mesh.vertexCount > 0)
                    {
                        section.meshCollider.enabled = needsCollider;
                    }
                }
            }
        }
    }
    
    // Public getter for debug UI
    public string GetStats()
    {
        return $"Chunks: {visibleChunks}/{totalChunks} visible";
    }
    
    // Debug visualization (collider distance circle)
    void OnDrawGizmos()
    {
        if (playerTransform == null)
            return;
        
        // Draw collider distance circle (red = colliders disabled beyond this)
        Gizmos.color = Color.red;
        
        // Draw horizontal circle at player height
        Vector3 playerPos = playerTransform.position;
        int segments = 32;
        float angleStep = 360f / segments;
        
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            
            Vector3 point1 = playerPos + new Vector3(
                Mathf.Cos(angle1) * colliderDistance,
                0,
                Mathf.Sin(angle1) * colliderDistance
            );
            
            Vector3 point2 = playerPos + new Vector3(
                Mathf.Cos(angle2) * colliderDistance,
                0,
                Mathf.Sin(angle2) * colliderDistance
            );
            
            Gizmos.DrawLine(point1, point2);
        }
    }
}

