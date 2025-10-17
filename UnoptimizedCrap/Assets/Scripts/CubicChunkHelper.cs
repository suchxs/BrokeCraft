using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Helper utilities for cubic chunks system.
/// Burst-compatible static methods for chunk calculations.
/// </summary>
public static class CubicChunkHelper
{
    /// <summary>
    /// Convert world block position to chunk position.
    /// Burst-compatible using int3.
    /// </summary>
    public static int3 WorldBlockPosToChunkPos(int3 worldBlockPos)
    {
        return new int3(
            (int)math.floor(worldBlockPos.x / (float)VoxelData.ChunkWidth),
            (int)math.floor(worldBlockPos.y / (float)VoxelData.ChunkHeight),
            (int)math.floor(worldBlockPos.z / (float)VoxelData.ChunkDepth)
        );
    }
    
    /// <summary>
    /// Convert world float position to chunk position.
    /// Burst-compatible using float3 and int3.
    /// </summary>
    public static int3 WorldFloatPosToChunkPos(float3 worldPos)
    {
        return new int3(
            (int)math.floor(worldPos.x / VoxelData.ChunkWidth),
            (int)math.floor(worldPos.y / VoxelData.ChunkHeight),
            (int)math.floor(worldPos.z / VoxelData.ChunkDepth)
        );
    }
    
    /// <summary>
    /// Convert chunk position to world position (bottom corner).
    /// Burst-compatible.
    /// </summary>
    public static float3 ChunkPosToWorldPos(int3 chunkPos)
    {
        return new float3(
            chunkPos.x * VoxelData.ChunkWidth,
            chunkPos.y * VoxelData.ChunkHeight,
            chunkPos.z * VoxelData.ChunkDepth
        );
    }
    
    /// <summary>
    /// Get local block position within a chunk from world block position.
    /// Burst-compatible.
    /// </summary>
    public static int3 WorldBlockPosToLocalPos(int3 worldBlockPos)
    {
        int3 chunkPos = WorldBlockPosToChunkPos(worldBlockPos);
        return new int3(
            worldBlockPos.x - chunkPos.x * VoxelData.ChunkWidth,
            worldBlockPos.y - chunkPos.y * VoxelData.ChunkHeight,
            worldBlockPos.z - chunkPos.z * VoxelData.ChunkDepth
        );
    }
    
    /// <summary>
    /// Calculate number of chunks in a cubic volume.
    /// </summary>
    public static int CalculateChunkCount(int horizontalDistance, int verticalDistance)
    {
        return horizontalDistance * horizontalDistance * verticalDistance;
    }
    
    /// <summary>
    /// Check if a chunk position is within view distance of a center position.
    /// Burst-compatible.
    /// </summary>
    public static bool IsChunkInRange(int3 chunkPos, int3 centerPos, int horizontalDistance, int verticalDistance)
    {
        int3 distance = math.abs(chunkPos - centerPos);
        int horizontalHalf = horizontalDistance / 2;
        int verticalHalf = verticalDistance / 2;
        
        return distance.x < horizontalHalf && 
               distance.z < horizontalHalf && 
               distance.y < verticalHalf;
    }
    
    /// <summary>
    /// Get world Y coordinate from chunk Y and local Y.
    /// </summary>
    public static int GetWorldY(int chunkY, int localY)
    {
        return chunkY * VoxelData.ChunkHeight + localY;
    }
    
    /// <summary>
    /// Get chunk Y coordinate from world Y.
    /// </summary>
    public static int GetChunkY(int worldY)
    {
        return (int)math.floor(worldY / (float)VoxelData.ChunkHeight);
    }
}

