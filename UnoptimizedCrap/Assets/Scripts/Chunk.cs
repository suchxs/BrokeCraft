using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Represents a single chunk in the world.
/// Manages block data and mesh generation using Burst-compiled jobs.
/// Includes optimized mesh collision for player physics.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    // Block data for this chunk
    private NativeArray<BlockType> blocks;
    private bool isBlocksAllocated = false;
    
    // Mesh components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
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
        meshCollider = GetComponent<MeshCollider>();
        
        // Create mesh (shared between renderer and collider for efficiency)
        mesh = new Mesh();
        mesh.name = "Chunk Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support large meshes
        meshFilter.mesh = mesh;
        
        // Configure mesh collider for optimal performance
        if (meshCollider != null)
        {
            meshCollider.convex = false;  // Precise collision (not convex hull)
            meshCollider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning | 
                                          MeshColliderCookingOptions.WeldColocatedVertices;
        }
        
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
        
        // Ensure chunk is on Default layer for proper collision
        gameObject.layer = 0; // Default layer
        
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
    /// Generate terrain data for this cubic chunk using Burst-compiled parallel job.
    /// Schedules async generation - terrain data becomes ready when job completes in Update().
    /// Uses world Y position to determine block types (supports unlimited height).
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
            SoilDepth = math.clamp(settings.soilDepth, VoxelData.MinSoilDepth, VoxelData.MaxSoilDepth),
            BedrockDepth = math.clamp(settings.bedrockDepth, VoxelData.MinBedrockDepth, VoxelData.MaxBedrockDepth),
            AlpineNormalizedThreshold = math.clamp(settings.alpineNormalizedThreshold, 0f, 1f),
            SteepRedistributionThreshold = math.clamp(settings.steepRedistributionThreshold, 0f, 1f)
        };

        int columnCount = VoxelData.ChunkWidth * VoxelData.ChunkDepth;
        int batchSize = math.max(1, columnCount / math.max(1, JobsUtility.JobWorkerCount));
        
        // Schedule job asynchronously - will complete in Update() without blocking
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
    
    /// <summary>
    /// Async job tracking - checks for completed jobs without stalling.
    /// Terrain generation and mesh generation both run on worker threads via Burst.
    /// </summary>
    void Update()
    {
        // Check if terrain generation job finished (non-blocking check via IsCompleted)
        if (isTerrainJobRunning && terrainJobHandle.IsCompleted)
        {
            // Job finished - apply results without blocking (Complete() on finished job = no stall)
            ApplyTerrainDataFromJob();
        }
        
        // Start mesh generation once terrain is ready and mesh is requested
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
        
        // Store references for completion (need to dispose neighbor arrays too)
        StartCoroutine(CompleteMeshGeneration(vertices, triangles, uvs, normals,
            neighborBack, neighborFront, neighborTop, neighborBottom, neighborLeft, neighborRight));
    }
    
    /// <summary>
    /// Get block data from neighboring chunk for cross-chunk face culling.
    /// Returns empty (but valid) array if neighbor doesn't exist.
    /// </summary>
    private NativeArray<BlockType> GetNeighborChunkData(int3 offset, out JobHandle dependency)
    {
        dependency = default;
        
        if (world == null)
        {
            // Return valid but empty array (job scheduler requires IsCreated == true)
            return new NativeArray<BlockType>(0, Allocator.TempJob);
        }
        
        int3 neighborPos = ChunkPosition + offset;
        Chunk neighborChunk = world.GetChunkAt(neighborPos);
        
        if (neighborChunk == null)
        {
            // Return valid but empty array (job scheduler requires IsCreated == true)
            return new NativeArray<BlockType>(0, Allocator.TempJob);
        }
        
        dependency = neighborChunk.GetTerrainGenerationHandle();

        return neighborChunk.GetBlocksArrayUnsafe();
    }
    
    /// <summary>
    /// Wait for job completion and apply mesh data, then dispose all temporary native arrays
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
        
        // Dispose mesh data native collections
        vertices.Dispose();
        triangles.Dispose();
        uvs.Dispose();
        normals.Dispose();
        
        // Dispose neighbor arrays (only if they were allocated as TempJob in GetNeighborChunkData)
        // These are either empty TempJob arrays or references to existing chunk data
        // Only dispose the empty TempJob ones (Length == 0 means we allocated them)
        if (neighborBack.IsCreated && neighborBack.Length == 0) neighborBack.Dispose();
        if (neighborFront.IsCreated && neighborFront.Length == 0) neighborFront.Dispose();
        if (neighborTop.IsCreated && neighborTop.Length == 0) neighborTop.Dispose();
        if (neighborBottom.IsCreated && neighborBottom.Length == 0) neighborBottom.Dispose();
        if (neighborLeft.IsCreated && neighborLeft.Length == 0) neighborLeft.Dispose();
        if (neighborRight.IsCreated && neighborRight.Length == 0) neighborRight.Dispose();
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
        
        // Recalculate bounds for culling and collision
        mesh.RecalculateBounds();
        
        // Update mesh collider ONLY if mesh has vertices (avoid warnings for empty chunks)
        if (meshCollider != null)
        {
            if (vertices.Length > 0 && triangles.Length > 0)
            {
                // Valid mesh - update collider
                meshCollider.sharedMesh = null; // Clear first to force rebuild
                meshCollider.sharedMesh = mesh; // Assign new mesh (Unity cooks collision mesh automatically)
            }
            else
            {
                // Empty chunk (all air) - clear collider to avoid warnings and save memory
                meshCollider.sharedMesh = null;
            }
        }
    }
    
    /// <summary>
    /// Get block at position within chunk.
    /// WARNING: May stall if terrain generation is still running - prefer checking IsTerrainDataReady first.
    /// </summary>
    public BlockType GetBlock(int x, int y, int z)
    {
        // Only force completion if absolutely necessary (this stalls the main thread!)
        if (!terrainDataReady && isTerrainJobRunning)
        {
            Debug.LogWarning($"Chunk {ChunkPosition}: Force-completing terrain job for GetBlock() - this causes a stall!");
            ApplyTerrainDataFromJob();
        }
        
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return BlockType.Air;
        
        return blocks[VoxelData.GetBlockIndex(x, y, z)];
    }
    
    /// <summary>
    /// Get direct access to blocks array (for neighbor queries).
    /// WARNING: May stall if terrain generation is still running - prefer checking IsTerrainDataReady first.
    /// </summary>
    public NativeArray<BlockType> GetBlocksArray()
    {
        // Only force completion if absolutely necessary (this stalls the main thread!)
        if (!terrainDataReady && isTerrainJobRunning)
        {
            Debug.LogWarning($"Chunk {ChunkPosition}: Force-completing terrain job for GetBlocksArray() - this causes a stall!");
            ApplyTerrainDataFromJob();
        }
        
        return blocks;
    }

    private NativeArray<BlockType> GetBlocksArrayUnsafe()
    {
        return blocks;
    }

    /// <summary>
    /// Get the terrain generation job handle for dependency chaining (e.g., mesh generation).
    /// </summary>
    public JobHandle GetTerrainGenerationHandle()
    {
        return terrainJobHandle;
    }

    /// <summary>
    /// Check if terrain data is ready without forcing completion.
    /// Use this to avoid stalling the main thread.
    /// </summary>
    public bool IsTerrainDataReady => terrainDataReady;
    
    /// <summary>
    /// Check if terrain generation job is currently running on worker threads.
    /// </summary>
    public bool IsTerrainJobRunning => isTerrainJobRunning;

    internal void ReceiveTerrainGenerationJob(JobHandle jobHandle)
    {
        terrainJobHandle = jobHandle;
        isTerrainJobRunning = true;
        terrainDataReady = false;
        needsMeshRegeneration = true;
    }

    /// <summary>
    /// Complete terrain generation job and mark data as ready.
    /// This STALLS the main thread until the job finishes - only call when necessary!
    /// Normally called automatically in Update() when job IsCompleted (no stall).
    /// </summary>
    private void ApplyTerrainDataFromJob()
    {
        if (isTerrainJobRunning)
        {
            terrainJobHandle.Complete(); // STALL WARNING: Blocks main thread until job finishes!
            isTerrainJobRunning = false;
            terrainDataReady = true;
            terrainJobHandle = default;
        }
    }
    
    /// <summary>
    /// Set block at position within chunk and regenerate mesh.
    /// WARNING: May stall if terrain generation is still running.
    /// </summary>
    public void SetBlock(int x, int y, int z, BlockType blockType)
    {
        if (!VoxelData.IsBlockInChunk(x, y, z))
            return;

        // Only force completion if absolutely necessary (this stalls the main thread!)
        if (!terrainDataReady && isTerrainJobRunning)
        {
            Debug.LogWarning($"Chunk {ChunkPosition}: Force-completing terrain job for SetBlock() - this causes a stall!");
            ApplyTerrainDataFromJob();
        }
        
        blocks[VoxelData.GetBlockIndex(x, y, z)] = blockType;
        
        // Regenerate mesh
        RequestMeshRegeneration();
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
