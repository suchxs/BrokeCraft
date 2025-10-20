using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
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

    // Terrain generation job state
    private JobHandle terrainJobHandle;
    private bool isTerrainJobRunning = false;
    private bool terrainDataReady = false;
    
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
        ScheduleTerrainGeneration();
    }
    
    /// <summary>
    /// Generate terrain data for this cubic chunk.
    /// Uses world Y position to determine block types (supports unlimited height).
    /// Terrain surface is at world Y = 64 (sea level).
    /// </summary>
    private void ScheduleTerrainGeneration()
    {
        TerrainGenerationSettings settings = world != null 
            ? world.GetTerrainGenerationSettings()
            : TerrainGenerationSettings.CreateDefault();

        var job = new ChunkTerrainGenerationJob
        {
            Blocks = blocks,
            ChunkPosition = ChunkPosition,
            NoiseSettings = settings.ToNoiseSettings(),
            SoilDepth = math.max(1, settings.soilDepth),
            BedrockDepth = math.max(1, settings.bedrockDepth),
            AlpineNormalizedThreshold = math.clamp(settings.alpineNormalizedThreshold, 0f, 1f),
            SteepRedistributionThreshold = math.clamp(settings.steepRedistributionThreshold, 0f, 1f)
        };

        int columnCount = VoxelData.ChunkWidth * VoxelData.ChunkDepth;
        int batchSize = math.max(1, columnCount / math.max(1, JobsUtility.JobWorkerCount));
        ReceiveTerrainGenerationJob(job.ScheduleParallel(columnCount, batchSize, default));
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
        if (isTerrainJobRunning && terrainJobHandle.IsCompleted)
        {
            ApplyTerrainDataFromJob();
        }
        
        // Regenerate mesh if requested and not already running
        if (terrainDataReady && needsMeshRegeneration && !isMeshJobRunning)
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

        // Include terrain job as dependency if still running
        JobHandle meshDependencies = terrainDataReady ? default : terrainJobHandle;
        
        // Get neighboring chunk data for cross-chunk face culling
        JobHandle neighborHandle;
        var neighborBack = GetNeighborChunkData(new int3(0, 0, -1), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        var neighborFront = GetNeighborChunkData(new int3(0, 0, 1), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        var neighborTop = GetNeighborChunkData(new int3(0, 1, 0), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        var neighborBottom = GetNeighborChunkData(new int3(0, -1, 0), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        var neighborLeft = GetNeighborChunkData(new int3(-1, 0, 0), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        var neighborRight = GetNeighborChunkData(new int3(1, 0, 0), out neighborHandle);
        meshDependencies = JobHandle.CombineDependencies(meshDependencies, neighborHandle);
        
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
        
        meshJobHandle = job.Schedule(meshDependencies);
        isMeshJobRunning = true;
        
        // Store references for completion
        StartCoroutine(CompleteMeshGeneration(vertices, triangles, uvs, normals));
    }
    
    /// <summary>
    /// Get block data from neighboring chunk for cross-chunk face culling.
    /// Returns empty array if neighbor doesn't exist.
    /// </summary>
    private NativeArray<BlockType> GetNeighborChunkData(int3 offset, out JobHandle dependency)
    {
        dependency = default;
        
        if (world == null)
            return default;
        
        int3 neighborPos = ChunkPosition + offset;
        Chunk neighborChunk = world.GetChunkAt(neighborPos);
        
        if (neighborChunk == null)
            return default;
        
        dependency = neighborChunk.GetTerrainGenerationHandle();

        return neighborChunk.GetBlocksArrayUnsafe();
    }
    
    /// <summary>
    /// Wait for job completion and apply mesh data, then dispose neighbor arrays
    /// </summary>
    private System.Collections.IEnumerator CompleteMeshGeneration(
        NativeList<float3> vertices,
        NativeList<int> triangles,
        NativeList<float2> uvs,
        NativeList<float3> normals)
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
        if (!terrainDataReady)
        {
        ApplyTerrainDataFromJob();
        }
        
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return BlockType.Air;
        
        return blocks[VoxelData.GetBlockIndex(x, y, z)];
    }
    
    /// <summary>
    /// Get direct access to blocks array (for neighbor queries)
    /// </summary>
    public NativeArray<BlockType> GetBlocksArray()
    {
        if (!terrainDataReady)
        {
            ApplyTerrainDataFromJob();
        }
        
        return blocks;
    }

    private NativeArray<BlockType> GetBlocksArrayUnsafe()
    {
        return blocks;
    }

    public JobHandle GetTerrainGenerationHandle()
    {
        return terrainJobHandle;
    }

    public bool IsTerrainDataReady => terrainDataReady;

    internal void ReceiveTerrainGenerationJob(JobHandle jobHandle)
    {
        terrainJobHandle = jobHandle;
        isTerrainJobRunning = true;
        terrainDataReady = false;
        needsMeshRegeneration = true;
    }

    private void ApplyTerrainDataFromJob()
    {
        if (isTerrainJobRunning)
        {
            terrainJobHandle.Complete();
            isTerrainJobRunning = false;
            terrainDataReady = true;
            terrainJobHandle = default;
        }
    }
    
    /// <summary>
    /// Set block at position within chunk and regenerate mesh
    /// </summary>
    public void SetBlock(int x, int y, int z, BlockType blockType)
    {
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return;

        if (!terrainDataReady)
        {
            ApplyTerrainDataFromJob();
        }
        
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

        ApplyTerrainDataFromJob();
        
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
