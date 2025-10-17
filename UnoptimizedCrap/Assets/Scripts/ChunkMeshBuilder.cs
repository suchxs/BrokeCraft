using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Burst-compiled job that generates chunk mesh data with face culling.
/// This runs on worker threads and is HEAVILY optimized by the Burst compiler.
/// </summary>
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct ChunkMeshBuilder : IJob
{
    // Input: Block data for this chunk
    [ReadOnly] public NativeArray<BlockType> Blocks;
    
    // Input: Chunk world position
    public int3 ChunkPosition;
    
    // Input: Neighboring chunk data (6 directions: Back, Front, Top, Bottom, Left, Right)
    // Each array contains edge blocks from neighboring chunk (or empty if no neighbor)
    [ReadOnly] public NativeArray<BlockType> NeighborBack;
    [ReadOnly] public NativeArray<BlockType> NeighborFront;
    [ReadOnly] public NativeArray<BlockType> NeighborTop;
    [ReadOnly] public NativeArray<BlockType> NeighborBottom;
    [ReadOnly] public NativeArray<BlockType> NeighborLeft;
    [ReadOnly] public NativeArray<BlockType> NeighborRight;
    
    // Output: Mesh data (will be resized as needed)
    public NativeList<float3> Vertices;
    public NativeList<int> Triangles;
    public NativeList<float2> UVs;
    public NativeList<float3> Normals;
    
    // Static lookup data as bytes for memory efficiency (copied from VoxelData)
    [ReadOnly] public NativeArray<byte3> VoxelVerticesBytes;
    [ReadOnly] public NativeArray<sbyte3> FaceChecksBytes;
    [ReadOnly] public NativeArray<byte> VoxelTrianglesBytes;
    [ReadOnly] public NativeArray<byte2> VoxelUVsBytes;
    [ReadOnly] public NativeArray<float3> FaceNormals; // Normals stay as float3 (need precision)
    
    public void Execute()
    {
        // Iterate through all blocks in the chunk
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int y = 0; y < VoxelData.ChunkHeight; y++)
            {
                for (int z = 0; z < VoxelData.ChunkDepth; z++)
                {
                    int index = VoxelData.GetBlockIndex(x, y, z);
                    BlockType blockType = Blocks[index];
                    
                    // Skip air blocks
                    if (blockType == BlockType.Air)
                        continue;
                    
                    // Check each face of the block
                    for (int faceIndex = 0; faceIndex < VoxelData.FaceCount; faceIndex++)
                    {
                        // Check if neighboring block is solid (for face culling)
                        if (!IsFaceVisible(x, y, z, faceIndex))
                            continue;
                        
                        // Add face to mesh
                        AddFace(x, y, z, faceIndex, blockType);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Check if a face should be rendered by checking actual neighboring chunk data.
    /// Burst-compatible cross-chunk face culling.
    /// </summary>
    private bool IsFaceVisible(int x, int y, int z, int faceIndex)
    {
        // Convert byte face check to int3
        sbyte3 faceCheckByte = FaceChecksBytes[faceIndex];
        int3 faceCheck = new int3(faceCheckByte.x, faceCheckByte.y, faceCheckByte.z);
        int3 neighborPos = new int3(x, y, z) + faceCheck;
        
        // Check if neighbor is within this chunk
        if (VoxelData.IsBlockInChunk(neighborPos.x, neighborPos.y, neighborPos.z))
        {
            // Neighbor is in this chunk - check directly
            int neighborIndex = VoxelData.GetBlockIndex(neighborPos.x, neighborPos.y, neighborPos.z);
            return Blocks[neighborIndex] == BlockType.Air;
        }
        
        // Neighbor is in adjacent chunk - check neighbor data
        return IsNeighborChunkBlockAir(x, y, z, faceIndex);
    }
    
    /// <summary>
    /// Check if block in neighboring chunk is air.
    /// Returns true if neighbor chunk doesn't exist (render face at world edge).
    /// </summary>
    private bool IsNeighborChunkBlockAir(int x, int y, int z, int faceIndex)
    {
        // Determine which neighbor array to check and local position in that chunk
        switch (faceIndex)
        {
            case 0: // Back (-Z)
                if (NeighborBack.Length == 0) return true; // No neighbor chunk
                return GetNeighborBlock(NeighborBack, x, y, VoxelData.ChunkDepth - 1) == BlockType.Air;
                
            case 1: // Front (+Z)
                if (NeighborFront.Length == 0) return true;
                return GetNeighborBlock(NeighborFront, x, y, 0) == BlockType.Air;
                
            case 2: // Top (+Y)
                if (NeighborTop.Length == 0) return true;
                return GetNeighborBlock(NeighborTop, x, 0, z) == BlockType.Air;
                
            case 3: // Bottom (-Y)
                if (NeighborBottom.Length == 0) return true;
                return GetNeighborBlock(NeighborBottom, x, VoxelData.ChunkHeight - 1, z) == BlockType.Air;
                
            case 4: // Left (-X)
                if (NeighborLeft.Length == 0) return true;
                return GetNeighborBlock(NeighborLeft, VoxelData.ChunkWidth - 1, y, z) == BlockType.Air;
                
            case 5: // Right (+X)
                if (NeighborRight.Length == 0) return true;
                return GetNeighborBlock(NeighborRight, 0, y, z) == BlockType.Air;
        }
        
        return true; // Default: render face if unknown
    }
    
    /// <summary>
    /// Get block from neighbor chunk data array.
    /// Burst-compatible inline method.
    /// </summary>
    private BlockType GetNeighborBlock(NativeArray<BlockType> neighborData, int x, int y, int z)
    {
        if (neighborData.Length == 0) return BlockType.Air;
        
        int index = VoxelData.GetBlockIndex(x, y, z);
        if (index >= 0 && index < neighborData.Length)
            return neighborData[index];
            
        return BlockType.Air;
    }
    
    /// <summary>
    /// Add a single face to the mesh
    /// </summary>
    private void AddFace(int x, int y, int z, int faceIndex, BlockType blockType)
    {
        int vertexIndex = Vertices.Length;
        float3 blockPos = new float3(x, y, z);
        
        // Get the 4 vertex indices for this face
        int triOffset = faceIndex * 4;
        
        // Add 4 vertices for this face
        for (int i = 0; i < 4; i++)
        {
            // Get vertex index from byte array
            byte vertIndexByte = VoxelTrianglesBytes[triOffset + i];
            
            // Convert byte3 vertex to float3
            byte3 vertByte = VoxelVerticesBytes[vertIndexByte];
            float3 vert = new float3(vertByte.x, vertByte.y, vertByte.z);
            
            // Add to block position
            float3 vertPos = blockPos + vert;
            Vertices.Add(vertPos);
            
            // Add UV coordinates (adjusted for texture atlas)
            float2 uv = GetUVForBlock(blockType, faceIndex, i);
            UVs.Add(uv);
            
            // Add normal for this face
            Normals.Add(FaceNormals[faceIndex]);
        }
        
        // Add 2 triangles (6 indices) for this quad face
        // Triangle 1: 0, 1, 2
        Triangles.Add(vertexIndex + 0);
        Triangles.Add(vertexIndex + 1);
        Triangles.Add(vertexIndex + 2);
        
        // Triangle 2: 2, 1, 3
        Triangles.Add(vertexIndex + 2);
        Triangles.Add(vertexIndex + 1);
        Triangles.Add(vertexIndex + 3);
    }
    
    /// <summary>
    /// Get UV coordinates for a specific block face from the texture atlas
    /// </summary>
    private float2 GetUVForBlock(BlockType blockType, int faceIndex, int vertexIndex)
    {
        // Get base UV from byte array and convert to float2
        byte2 uvByte = VoxelUVsBytes[vertexIndex];
        float2 baseUV = new float2(uvByte.x, uvByte.y);
        
        // Get texture index from BlockTextureData
        int textureIndex = BlockTextureData.GetTextureIndex(blockType, faceIndex);
        
        // Convert to atlas UV coordinates
        return BlockTextureData.GetAtlasUV(textureIndex, baseUV);
    }
}

/// <summary>
/// Helper class to manage mesh building jobs
/// </summary>
public static class ChunkMeshHelper
{
    /// <summary>
    /// Create native arrays for voxel data using byte-optimized versions (call once and reuse)
    /// </summary>
    public static void InitializeVoxelData(
        out NativeArray<byte3> verticesBytes,
        out NativeArray<sbyte3> faceChecksBytes,
        out NativeArray<byte> trianglesBytes,
        out NativeArray<byte2> uvsBytes,
        out NativeArray<float3> normals,
        Allocator allocator)
    {
        verticesBytes = new NativeArray<byte3>(VoxelData.VoxelVerticesBytes, allocator);
        faceChecksBytes = new NativeArray<sbyte3>(VoxelData.FaceChecksBytes, allocator);
        trianglesBytes = new NativeArray<byte>(VoxelData.VoxelTrianglesBytes, allocator);
        uvsBytes = new NativeArray<byte2>(VoxelData.VoxelUVsBytes, allocator);
        normals = new NativeArray<float3>(VoxelData.FaceNormals, allocator);
    }
}

