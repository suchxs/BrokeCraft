using UnityEngine;

// MINECRAFT-STYLE LOD (Level of Detail) SYSTEM
// Reduces mesh complexity for distant chunks to improve performance
public class ChunkLOD : MonoBehaviour
{
    public enum LODLevel
    {
        High = 0,    // Full detail (near player)
        Medium = 1,  // 50% detail
        Low = 2,     // 25% detail (far from player)
    }
    
    // LOD distance thresholds (in chunks)
    private const int HIGH_LOD_DISTANCE = 4;   // Within 4 chunks = full detail
    private const int MEDIUM_LOD_DISTANCE = 8; // 4-8 chunks = medium detail
    // Beyond 8 chunks = low detail
    
    private World world;
    private Transform playerTransform;
    
    // Cache for LOD levels per chunk
    private System.Collections.Generic.Dictionary<ChunkCoord, LODLevel> chunkLODCache = 
        new System.Collections.Generic.Dictionary<ChunkCoord, LODLevel>();
    
    public void Initialize(World worldRef, Transform player)
    {
        world = worldRef;
        playerTransform = player;
        
        // Update LOD every 0.5 seconds (not every frame)
        InvokeRepeating("UpdateChunkLODs", 1f, 0.5f);
        
        Debug.Log("[ChunkLOD] ✓ LOD system initialized");
    }
    
    void UpdateChunkLODs()
    {
        if (playerTransform == null || world == null) return;
        
        // Get player chunk position
        ChunkCoord playerChunk = GetChunkCoordFromPosition(playerTransform.position);
        
        // Update LOD for all active chunks
        foreach (Chunk chunk in world.GetAllChunks())
        {
            if (chunk == null) continue;
            
            // Calculate distance to player
            float distance = Vector2.Distance(
                new Vector2(chunk.coord.x, chunk.coord.z),
                new Vector2(playerChunk.x, playerChunk.z)
            );
            
            // Determine LOD level based on distance
            LODLevel desiredLOD = GetLODLevelForDistance(distance);
            
            // Check if LOD needs updating
            if (!chunkLODCache.ContainsKey(chunk.coord) || chunkLODCache[chunk.coord] != desiredLOD)
            {
                chunkLODCache[chunk.coord] = desiredLOD;
                // Note: LOD changes would require mesh regeneration
                // For now, we just track LOD levels for future use
            }
        }
    }
    
    LODLevel GetLODLevelForDistance(float distanceInChunks)
    {
        if (distanceInChunks <= HIGH_LOD_DISTANCE)
            return LODLevel.High;
        else if (distanceInChunks <= MEDIUM_LOD_DISTANCE)
            return LODLevel.Medium;
        else
            return LODLevel.Low;
    }
    
    // Get LOD level for a chunk coordinate (used during mesh generation)
    public static LODLevel GetLODForChunk(ChunkCoord coord, ChunkCoord playerChunk)
    {
        float distance = Vector2.Distance(
            new Vector2(coord.x, coord.z),
            new Vector2(playerChunk.x, playerChunk.z)
        );
        
        if (distance <= HIGH_LOD_DISTANCE)
            return LODLevel.High;
        else if (distance <= MEDIUM_LOD_DISTANCE)
            return LODLevel.Medium;
        else
            return LODLevel.Low;
    }
    
    ChunkCoord GetChunkCoordFromPosition(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x, z);
    }
    
    void OnDestroy()
    {
        CancelInvoke("UpdateChunkLODs");
    }
}

