using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Represents a single chunk in the world.
/// Manages block data and mesh generation using Burst-compiled jobs.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    // Block data for this chunk
    private NativeArray<BlockType> blocks;
    private bool isBlocksAllocated = false;
    
    // Mesh components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    
    // Job data
    private JobHandle meshJobHandle;
    private bool isMeshJobRunning = false;
    
    // Chunk position in world space (chunk coordinates, not block coordinates)
    public int3 ChunkPosition { get; private set; }
    
    // Reference to world for querying neighboring chunks
    private World world;
    
    // Voxel lookup data as bytes (shared across all chunks, allocated once)
    // Using byte types for 4x memory reduction
    private static NativeArray<byte3> voxelVerticesBytes;
    private static NativeArray<sbyte3> faceChecksBytes;
    private static NativeArray<byte> voxelTrianglesBytes;
    private static NativeArray<byte2> voxelUVsBytes;
    private static NativeArray<float3> faceNormals;
    private static bool isVoxelDataInitialized = false;
    
    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Create mesh
        mesh = new Mesh();
        mesh.name = "Chunk Mesh";
        meshFilter.mesh = mesh;
        
        // Initialize shared voxel data if not already done
        InitializeVoxelDataIfNeeded();
    }
    
    /// <summary>
    /// Initialize this chunk with block data and position
    /// </summary>
    public void Initialize(int3 chunkPosition, Material material, World worldRef)
    {
        ChunkPosition = chunkPosition;
        world = worldRef;
        
        transform.position = new Vector3(
            chunkPosition.x * VoxelData.ChunkWidth,
            chunkPosition.y * VoxelData.ChunkHeight,
            chunkPosition.z * VoxelData.ChunkDepth
        );
        
        meshRenderer.material = material;
        
        // Allocate block data
        if (!isBlocksAllocated)
        {
            blocks = new NativeArray<BlockType>(VoxelData.ChunkSize, Allocator.Persistent);
            isBlocksAllocated = true;
        }
        
        // Generate initial terrain data
        GenerateTerrainData();
        
        // Start mesh generation
        StartMeshGeneration();
    }
    
    /// <summary>
    /// Generate terrain data for this cubic chunk.
    /// Uses world Y position to determine block types (supports unlimited height).
    /// Terrain surface is at world Y = 64 (sea level).
    /// </summary>
    private void GenerateTerrainData()
    {
        // World Y coordinates for this chunk
        int chunkWorldYStart = ChunkPosition.y * VoxelData.ChunkHeight;
        
        // Terrain generation constants (world Y coordinates)
        const int BEDROCK_LEVEL = 0;
        const int TERRAIN_SURFACE = 64;
        const int DIRT_DEPTH = 4;
        
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkDepth; z++)
            {
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    int index = VoxelData.GetBlockIndex(x, y, z);
                    int worldY = chunkWorldYStart + y;
                    
                    // Generate based on world Y coordinate
                    if (worldY == BEDROCK_LEVEL)
                    {
                        // Bedrock only at Y=0
                        blocks[index] = BlockType.Bedrock;
                    }
                    else if (worldY < BEDROCK_LEVEL)
                    {
                        // Below bedrock = air (for caves/void)
                        blocks[index] = BlockType.Air;
                    }
                    else if (worldY > TERRAIN_SURFACE)
                    {
                        // Above terrain = air
                        blocks[index] = BlockType.Air;
                    }
                    else if (worldY == TERRAIN_SURFACE)
                    {
                        // Surface layer = grass
                        blocks[index] = BlockType.Grass;
                    }
                    else if (worldY > TERRAIN_SURFACE - DIRT_DEPTH)
                    {
                        // 4 blocks below surface = dirt
                        blocks[index] = BlockType.Dirt;
                    }
                    else
                    {
                        // Everything else below = stone
                        blocks[index] = BlockType.Stone;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Request mesh regeneration (e.g., when neighbors change)
    /// </summary>
    public void RequestMeshRegeneration()
    {
        needsMeshRegeneration = true;
    }
    
    private bool needsMeshRegeneration = false;
    
    void Update()
    {
        // Regenerate mesh if requested and not already running
        if (needsMeshRegeneration && !isMeshJobRunning)
        {
            needsMeshRegeneration = false;
            StartMeshGeneration();
        }
    }
    
    /// <summary>
    /// Start the Burst-compiled mesh generation job with cross-chunk face culling
    /// </summary>
    public void StartMeshGeneration()
    {
        // Don't start if already running
        if (isMeshJobRunning)
            return;
        
        // Create native lists for mesh data (output from job)
        var vertices = new NativeList<float3>(Allocator.TempJob);
        var triangles = new NativeList<int>(Allocator.TempJob);
        var uvs = new NativeList<float2>(Allocator.TempJob);
        var normals = new NativeList<float3>(Allocator.TempJob);
        
        // Get neighboring chunk data for cross-chunk face culling
        var neighborBack = GetNeighborChunkData(new int3(0, 0, -1));
        var neighborFront = GetNeighborChunkData(new int3(0, 0, 1));
        var neighborTop = GetNeighborChunkData(new int3(0, 1, 0));
        var neighborBottom = GetNeighborChunkData(new int3(0, -1, 0));
        var neighborLeft = GetNeighborChunkData(new int3(-1, 0, 0));
        var neighborRight = GetNeighborChunkData(new int3(1, 0, 0));
        
        // Create and schedule the mesh generation job with neighbor data
        var job = new ChunkMeshBuilder
        {
            Blocks = blocks,
            ChunkPosition = ChunkPosition,
            Vertices = vertices,
            Triangles = triangles,
            UVs = uvs,
            Normals = normals,
            VoxelVerticesBytes = voxelVerticesBytes,
            FaceChecksBytes = faceChecksBytes,
            VoxelTrianglesBytes = voxelTrianglesBytes,
            VoxelUVsBytes = voxelUVsBytes,
            FaceNormals = faceNormals,
            NeighborBack = neighborBack,
            NeighborFront = neighborFront,
            NeighborTop = neighborTop,
            NeighborBottom = neighborBottom,
            NeighborLeft = neighborLeft,
            NeighborRight = neighborRight
        };
        
        meshJobHandle = job.Schedule();
        isMeshJobRunning = true;
        
        // Store references for completion
        StartCoroutine(CompleteMeshGeneration(vertices, triangles, uvs, normals, 
            neighborBack, neighborFront, neighborTop, neighborBottom, neighborLeft, neighborRight));
    }
    
    /// <summary>
    /// Get block data from neighboring chunk for cross-chunk face culling.
    /// Returns empty array if neighbor doesn't exist.
    /// </summary>
    private NativeArray<BlockType> GetNeighborChunkData(int3 offset)
    {
        if (world == null)
            return new NativeArray<BlockType>(0, Allocator.TempJob);
        
        int3 neighborPos = ChunkPosition + offset;
        Chunk neighborChunk = world.GetChunkAt(neighborPos);
        
        if (neighborChunk == null)
            return new NativeArray<BlockType>(0, Allocator.TempJob);
        
        // Copy neighbor's block data to new NativeArray for job
        var neighborData = new NativeArray<BlockType>(VoxelData.ChunkSize, Allocator.TempJob);
        NativeArray<BlockType>.Copy(neighborChunk.blocks, neighborData);
        
        return neighborData;
    }
    
    /// <summary>
    /// Wait for job completion and apply mesh data, then dispose neighbor arrays
    /// </summary>
    private System.Collections.IEnumerator CompleteMeshGeneration(
        NativeList<float3> vertices,
        NativeList<int> triangles,
        NativeList<float2> uvs,
        NativeList<float3> normals,
        NativeArray<BlockType> neighborBack,
        NativeArray<BlockType> neighborFront,
        NativeArray<BlockType> neighborTop,
        NativeArray<BlockType> neighborBottom,
        NativeArray<BlockType> neighborLeft,
        NativeArray<BlockType> neighborRight)
    {
        // Wait for job to complete
        yield return new WaitUntil(() => meshJobHandle.IsCompleted);
        
        meshJobHandle.Complete();
        isMeshJobRunning = false;
        
        // Apply mesh data
        ApplyMeshData(vertices, triangles, uvs, normals);
        
        // Dispose native collections
        vertices.Dispose();
        triangles.Dispose();
        uvs.Dispose();
        normals.Dispose();
        
        // Dispose neighbor data arrays
        if (neighborBack.IsCreated) neighborBack.Dispose();
        if (neighborFront.IsCreated) neighborFront.Dispose();
        if (neighborTop.IsCreated) neighborTop.Dispose();
        if (neighborBottom.IsCreated) neighborBottom.Dispose();
        if (neighborLeft.IsCreated) neighborLeft.Dispose();
        if (neighborRight.IsCreated) neighborRight.Dispose();
    }
    
    /// <summary>
    /// Apply the generated mesh data to the Unity mesh
    /// </summary>
    private void ApplyMeshData(
        NativeList<float3> vertices,
        NativeList<int> triangles,
        NativeList<float2> uvs,
        NativeList<float3> normals)
    {
        mesh.Clear();
        
        // Convert NativeList to Unity mesh data
        var vertexArray = new Vector3[vertices.Length];
        var uvArray = new Vector2[uvs.Length];
        var normalArray = new Vector3[normals.Length];
        var triangleArray = new int[triangles.Length];
        
        for (int i = 0; i < vertices.Length; i++)
        {
            vertexArray[i] = vertices[i];
            uvArray[i] = uvs[i];
            normalArray[i] = normals[i];
        }
        
        for (int i = 0; i < triangles.Length; i++)
        {
            triangleArray[i] = triangles[i];
        }
        
        mesh.vertices = vertexArray;
        mesh.uv = uvArray;
        mesh.normals = normalArray;
        mesh.SetTriangles(triangleArray, 0);
        
        // Recalculate bounds for culling
        mesh.RecalculateBounds();
    }
    
    /// <summary>
    /// Get block at position within chunk
    /// </summary>
    public BlockType GetBlock(int x, int y, int z)
    {
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return BlockType.Air;
        
        return blocks[VoxelData.GetBlockIndex(x, y, z)];
    }
    
    /// <summary>
    /// Get direct access to blocks array (for neighbor queries)
    /// </summary>
    public NativeArray<BlockType> GetBlocksArray()
    {
        return blocks;
    }
    
    /// <summary>
    /// Set block at position within chunk and regenerate mesh
    /// </summary>
    public void SetBlock(int x, int y, int z, BlockType blockType)
    {
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return;
        
        blocks[VoxelData.GetBlockIndex(x, y, z)] = blockType;
        
        // Regenerate mesh
        StartMeshGeneration();
    }
    
    /// <summary>
    /// Initialize shared voxel lookup data with byte optimization (called once)
    /// </summary>
    private static void InitializeVoxelDataIfNeeded()
    {
        if (isVoxelDataInitialized)
            return;
        
        ChunkMeshHelper.InitializeVoxelData(
            out voxelVerticesBytes,
            out faceChecksBytes,
            out voxelTrianglesBytes,
            out voxelUVsBytes,
            out faceNormals,
            Allocator.Persistent
        );
        
        isVoxelDataInitialized = true;
    }
    
    /// <summary>
    /// Cleanup when chunk is destroyed
    /// </summary>
    private void OnDestroy()
    {
        // Complete any running jobs
        if (isMeshJobRunning)
        {
            meshJobHandle.Complete();
            isMeshJobRunning = false;
        }
        
        // Dispose native arrays
        if (isBlocksAllocated && blocks.IsCreated)
        {
            blocks.Dispose();
            isBlocksAllocated = false;
        }
    }
    
    /// <summary>
    /// Static cleanup for shared voxel data (call on application quit)
    /// </summary>
    public static void DisposeStaticData()
    {
        if (!isVoxelDataInitialized)
            return;
        
        if (voxelVerticesBytes.IsCreated) voxelVerticesBytes.Dispose();
        if (faceChecksBytes.IsCreated) faceChecksBytes.Dispose();
        if (voxelTrianglesBytes.IsCreated) voxelTrianglesBytes.Dispose();
        if (voxelUVsBytes.IsCreated) voxelUVsBytes.Dispose();
        if (faceNormals.IsCreated) faceNormals.Dispose();
        
        isVoxelDataInitialized = false;
    }
}

