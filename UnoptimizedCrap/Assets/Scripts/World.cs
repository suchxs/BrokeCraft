using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Manages the entire voxel world - spawning, updating, and removing chunks.
/// This is the main coordinator for the chunk system.
/// </summary>
public class World : MonoBehaviour
{
    private const int DefaultHorizontalViewDistance = 32; // diameter in chunks (balanced)
    private const int DefaultVerticalViewDistance = 20;  // taller view for vertical visibility
    private const int DefaultMaxCreationsPerFrame = 24;
    private const int WarmupMultiplier = 2;
    private const int WarmupMaxHorizontalHalf = 12;
    private const int WarmupMaxVerticalHalf = 8;
    private const int WarmupCreationBoost = 4;

    [Header("World Settings")]
    [Tooltip("Material to use for all chunks (assign your texture atlas material here)")]
    public Material chunkMaterial;
    
    [Tooltip("Chunk prefab (will be created automatically if null)")]
    public GameObject chunkPrefab;
    
    [Header("Cubic Chunks Configuration")]
    [Tooltip("Horizontal view distance in chunks (X/Z axes)")]
    public int horizontalViewDistance = DefaultHorizontalViewDistance;
    
    [Tooltip("Vertical view distance in chunks (Y axis) - up and down")]
    public int verticalViewDistance = DefaultVerticalViewDistance;
    
    [Tooltip("Generate chunks on start")]
    public bool generateOnStart = true;
    
    [Tooltip("Maximum number of new chunks to instantiate per frame when streaming.")]
    [Range(1, 256)]
    public int maxChunkCreationsPerFrame = DefaultMaxCreationsPerFrame;

    [Header("Distant Terrain")]
    [SerializeField] private bool enableDistantTerrain = false;
    [Tooltip("Optional renderer that draws kilometre-scale horizon geometry. Will be created automatically if omitted.")]
    [SerializeField] private DistantTerrainRenderer distantTerrainRenderer;
    [Header("Loading UI")]
    [SerializeField] private LoadingOverlay loadingOverlay;
    
    [Header("Terrain Generation")]
    [Tooltip("Procedural terrain parameters (Perlin FBM with Burst jobs)")]
    public TerrainGenerationSettings terrainSettings = TerrainGenerationSettings.CreateDefault();
    
    [Header("World Origin")]
    [Tooltip("Center chunk position for initial generation")]
    public int3 worldOriginChunk = new int3(0, 4, 0); // Start at Y=4 so we're near terrain surface (Y=64 world coord)
    
    // Dictionary to store all active chunks
    private Dictionary<int3, Chunk> chunks = new Dictionary<int3, Chunk>();
    private readonly Queue<int3> chunkCreationQueue = new Queue<int3>();
    private readonly HashSet<int3> pendingChunkCreations = new HashSet<int3>();
    
    // Parent transform for organization
    private Transform chunksParent;
    private ChunkPool chunkPool;
    private bool isPrewarming;
    private float prewarmProgress;
    private bool prewarmComplete;
    private const int UnloadBuffer = 2;
    private int3 currentCenterChunk;

    public event Action<int3, NativeArray<ChunkColumnSummary>> ChunkColumnSummaryReady;
    public event Action<int3> ChunkColumnSummaryInvalidated;
    public bool IsDistantTerrainEnabled => enableDistantTerrain;
    
    private void Start()
    {
        // Create parent object for chunks
        GameObject parent = new GameObject("Chunks");
        parent.transform.parent = transform;
        chunksParent = parent.transform;

        chunkPool = new ChunkPool(chunkPrefab, chunkMaterial, chunksParent);
        currentCenterChunk = worldOriginChunk;
        AppSettings.Load();

        // Validate material
        if (chunkMaterial == null)
        {
            Debug.LogError("No chunk material assigned! Please assign a material with your texture atlas.");
        }
        else
        {
            Shader vcShader = Shader.Find("Custom/ChunkVertexColor");
            if (vcShader != null)
            {
                chunkMaterial.shader = vcShader;
            }
            else
            {
                Debug.LogWarning("Custom/ChunkVertexColor shader not found. Grass tinting requires vertex color support.");
            }
        }

        AppSettings.ApplyToWorld(this);
        EnforceMinimumViewSettings();

        // Generate initial world (with optional overlay)
        if (generateOnStart)
        {
            StartCoroutine(PrewarmAndGenerateWorld());
        }

        if (enableDistantTerrain)
        {
            InitializeDistantTerrain();
        }
    }
    
    private void Update()
    {
        ProcessChunkCreationQueue(false);
    }

    public bool IsPrewarming => isPrewarming && !prewarmComplete;
    public bool IsPrewarmComplete => prewarmComplete;
    public float PrewarmProgress => prewarmProgress;

    private void EnforceMinimumViewSettings()
    {
        horizontalViewDistance = math.max(horizontalViewDistance, DefaultHorizontalViewDistance);
        verticalViewDistance = math.max(verticalViewDistance, DefaultVerticalViewDistance);
        maxChunkCreationsPerFrame = math.max(maxChunkCreationsPerFrame, DefaultMaxCreationsPerFrame);

        // Cap to prevent runaway memory/perf if tweaked in inspector
        horizontalViewDistance = math.clamp(horizontalViewDistance, 2, 48);
        verticalViewDistance = math.clamp(verticalViewDistance, 2, 24);
        maxChunkCreationsPerFrame = math.clamp(maxChunkCreationsPerFrame, 1, 64);
    }

    private int GetHorizontalRadius()
    {
        return math.max(1, horizontalViewDistance / 2);
    }

    private int GetVerticalRadius()
    {
        return math.max(1, verticalViewDistance / 2);
    }

    private int DetermineLodStep(int3 centerChunkPos, int3 targetChunkPos)
    {
        // Disable LOD steps for now to avoid visual artifacts on terrain tops.
        return 1;
    }

    private System.Collections.IEnumerator PrewarmAndGenerateWorld()
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.Show();
            loadingOverlay.SetProgress(0f);
        }

        int warmupHorizontal = math.min(GetHorizontalRadius() * WarmupMultiplier, WarmupMaxHorizontalHalf);
        int warmupVertical = math.min(GetVerticalRadius() * WarmupMultiplier, WarmupMaxVerticalHalf);
        int total = (warmupHorizontal * 2 + 1) * (warmupHorizontal * 2 + 1) * (warmupVertical * 2 + 1);
        prewarmProgress = 0f;
        isPrewarming = true;
        prewarmComplete = false;

        // Synchronously create origin chunk first
        CreateChunk(worldOriginChunk);
        prewarmProgress = 1f / math.max(1, total);
        if (loadingOverlay != null)
        {
            loadingOverlay.SetProgress(prewarmProgress);
        }
        yield return null;

        for (int x = -warmupHorizontal; x <= warmupHorizontal; x++)
        {
            for (int z = -warmupHorizontal; z <= warmupHorizontal; z++)
            {
                for (int y = -warmupVertical; y <= warmupVertical; y++)
                {
                    int3 chunkPos = worldOriginChunk + new int3(x, y, z);
                    if (chunkPos.Equals(worldOriginChunk))
                    {
                        continue;
                    }

                    EnqueueChunkCreation(chunkPos);
                }
            }
        }

        // Let the queue drain while updating progress
        while (chunkCreationQueue.Count > 0 || pendingChunkCreations.Count > 0)
        {
            ProcessChunkCreationQueue(true);

            int loaded = chunks.Count;
            float progress = math.saturate(loaded / math.max(1f, total));
            prewarmProgress = progress;
            if (loadingOverlay != null)
            {
                loadingOverlay.SetProgress(prewarmProgress);
            }
            yield return null;
        }

        if (loadingOverlay != null)
        {
            loadingOverlay.SetProgress(1f);
            loadingOverlay.Hide();
        }

        isPrewarming = false;
        prewarmComplete = true;
    }
    
    /// <summary>
    /// Generate initial cubic chunks around the world origin.
    /// Loads chunks in all 3 dimensions (true cubic chunks system).
    /// </summary>
    public void GenerateWorld()
    {
        int horizontalHalf = GetHorizontalRadius();
        int verticalHalf = GetVerticalRadius();
        
        int chunksGenerated = 0;
        
        // Generate cubic volume of chunks centered on worldOriginChunk
        for (int x = -horizontalHalf; x < horizontalHalf; x++)
        {
            for (int z = -horizontalHalf; z < horizontalHalf; z++)
            {
                for (int y = -verticalHalf; y < verticalHalf; y++)
                {
                    int3 chunkPos = worldOriginChunk + new int3(x, y, z);
                    CreateChunk(chunkPos);
                    chunksGenerated++;
                }
            }
        }
        
        Debug.Log($"[Cubic Chunks] Generated {chunksGenerated} chunks in 3D volume");
        Debug.Log($"[Cubic Chunks] Horizontal: {horizontalViewDistance}x{horizontalViewDistance}, Vertical: {verticalViewDistance}");
        Debug.Log($"[Cubic Chunks] World origin at chunk {worldOriginChunk} (world Y: {worldOriginChunk.y * VoxelData.ChunkHeight})");
    }
    
    /// <summary>
    /// Load chunks around a specific position (for player movement).
    /// Call this when player moves to dynamically load/unload chunks.
    /// </summary>
    public void LoadChunksAroundPosition(int3 centerChunkPos)
    {
        currentCenterChunk = centerChunkPos;
        int horizontalHalf = GetHorizontalRadius();
        int verticalHalf = GetVerticalRadius();
        
        // Determine which chunks should exist
        for (int x = -horizontalHalf; x < horizontalHalf; x++)
        {
            for (int z = -horizontalHalf; z < horizontalHalf; z++)
            {
                for (int y = -verticalHalf; y < verticalHalf; y++)
                {
                    int3 chunkPos = centerChunkPos + new int3(x, y, z);
                    
                    // Create chunk if it doesn't exist
                    if (!chunks.ContainsKey(chunkPos))
                    {
                        EnqueueChunkCreation(chunkPos);
                    }
                }
            }
        }
        
        // Unload chunks that are too far away
        UnloadDistantChunks(centerChunkPos);
    }
    
    /// <summary>
    /// Unload chunks that are beyond view distance from center position.
    /// </summary>
    private void UnloadDistantChunks(int3 centerChunkPos)
    {
        if (isPrewarming)
        {
            return; // avoid gaps during prewarm
        }

        int horizontalHalf = GetHorizontalRadius();
        int verticalHalf = GetVerticalRadius();
        
        // Find chunks to remove (can't modify dictionary during iteration)
        var chunksToRemove = new System.Collections.Generic.List<int3>();
        
        foreach (var kvp in chunks)
        {
            int3 chunkPos = kvp.Key;
            int3 distance = math.abs(chunkPos - centerChunkPos);
            
            // Changed >= to > to prevent edge chunks from constantly unloading/reloading
            // Add +1 buffer to avoid flickering at boundaries
            if (distance.x > horizontalHalf + UnloadBuffer || 
                distance.z > horizontalHalf + UnloadBuffer || 
                distance.y > verticalHalf + UnloadBuffer)
            {
                chunksToRemove.Add(chunkPos);
            }
        }
        
        // Remove distant chunks
        foreach (int3 pos in chunksToRemove)
        {
            RemoveChunk(pos);
        }
        
        // Removed debug spam - use F3 overlay to see chunk count
    }
    
    /// <summary>
    /// Convert world position (float3) to chunk position (int3).
    /// Uses CubicChunkHelper for consistency.
    /// </summary>
    public static int3 WorldPosToChunkPos(float3 worldPos)
    {
        return CubicChunkHelper.WorldFloatPosToChunkPos(worldPos);
    }
    
    /// <summary>
    /// Get total number of loaded chunks.
    /// </summary>
    public int GetLoadedChunkCount()
    {
        return chunks.Count;
    }

    /// <summary>
    /// Get current terrain generation settings.
    /// </summary>
    public TerrainGenerationSettings GetTerrainGenerationSettings()
    {
        return terrainSettings;
    }
    
    /// <summary>
    /// Get chunk at specific position (returns null if doesn't exist).
    /// </summary>
    public Chunk GetChunkAt(int3 chunkPos)
    {
        return GetChunk(chunkPos);
    }
    
    /// <summary>
    /// Create a single chunk at the specified position
    /// </summary>
    public Chunk CreateChunk(int3 chunkPosition)
    {
        // Don't create if already exists
        if (chunks.ContainsKey(chunkPosition))
            return chunks[chunkPosition];

        int lodStep = DetermineLodStep(chunkPosition, chunkPosition);
        Chunk chunk = chunkPool.Get(chunkPosition, this, lodStep);
        
        // Store in dictionary
        chunks[chunkPosition] = chunk;
        
        // Notify neighbors to regenerate meshes (for proper cross-chunk face culling)
        NotifyNeighborsToRegenerate(chunkPosition);
        
        return chunk;
    }
    
    private void InitializeDistantTerrain()
    {
        if (distantTerrainRenderer == null)
        {
            GameObject rendererObject = new GameObject("DistantTerrain");
            rendererObject.transform.SetParent(transform, false);
            distantTerrainRenderer = rendererObject.AddComponent<DistantTerrainRenderer>();
        }

        distantTerrainRenderer.Initialize(this, chunkMaterial);
        distantTerrainRenderer.ConfigureNearField(ComputeNearFieldRadiusMeters());
    }

    private float ComputeNearFieldRadiusMeters()
    {
        float horizontalHalf = math.max(1, horizontalViewDistance) * 0.5f;
        float chunkSpan = VoxelData.ChunkWidth * (horizontalHalf + 2f);
        return math.max(VoxelData.ChunkWidth * 2f, chunkSpan);
    }

    /// <summary>
    /// Register a viewer transform (typically the player) so the distant renderer can track it.
    /// </summary>
    public void RegisterViewer(Transform viewerTransform)
    {
        if (!enableDistantTerrain)
        {
            return;
        }

        if (viewerTransform == null)
        {
            return;
        }

        if (distantTerrainRenderer == null)
        {
            InitializeDistantTerrain();
        }

        distantTerrainRenderer?.SetViewer(viewerTransform, true);
    }

    private void EnqueueChunkCreation(int3 chunkPosition)
    {
        if (chunks.ContainsKey(chunkPosition))
        {
            return;
        }

        if (pendingChunkCreations.Contains(chunkPosition))
        {
            return;
        }

        pendingChunkCreations.Add(chunkPosition);
        chunkCreationQueue.Enqueue(chunkPosition);
    }
    
    private void ProcessChunkCreationQueue(bool warmup)
    {
        if (chunkCreationQueue.Count == 0)
        {
            return;
        }

        int processed = 0;
        int maxPerFrame = warmup
            ? math.max(1, chunkCreationQueue.Count) // aggressively drain during warmup for faster loads
            : math.max(1, maxChunkCreationsPerFrame);

        while (processed < maxPerFrame && chunkCreationQueue.Count > 0)
        {
            int3 chunkPosition = chunkCreationQueue.Dequeue();
            pendingChunkCreations.Remove(chunkPosition);

            if (chunks.ContainsKey(chunkPosition))
            {
                continue;
            }

            int lodStep = DetermineLodStep(currentCenterChunk, chunkPosition);
            Chunk chunk = chunkPool.Get(chunkPosition, this, lodStep);
            chunks[chunkPosition] = chunk;
            NotifyNeighborsToRegenerate(chunkPosition);
            processed++;
        }
    }
    
    /// <summary>
    /// Notify neighboring chunks to regenerate their meshes when a new chunk is added
    /// </summary>
    internal void NotifyNeighborsToRegenerate(int3 chunkPosition)
    {
        // Check all 6 neighboring positions
        int3[] neighborOffsets = new int3[]
        {
            new int3(0, 0, -1),  // Back
            new int3(0, 0, 1),   // Front
            new int3(0, 1, 0),   // Top
            new int3(0, -1, 0),  // Bottom
            new int3(-1, 0, 0),  // Left
            new int3(1, 0, 0)    // Right
        };
        
        foreach (int3 offset in neighborOffsets)
        {
            int3 neighborPos = chunkPosition + offset;
            Chunk neighbor = GetChunkAt(neighborPos);
            
            if (neighbor != null)
            {
                neighbor.RequestMeshRegeneration();
            }
        }
    }

    internal void NotifyChunkColumnSummaryReady(int3 chunkPosition, NativeArray<ChunkColumnSummary> summaries)
    {
        if (!enableDistantTerrain)
        {
            return;
        }

        ChunkColumnSummaryReady?.Invoke(chunkPosition, summaries);
        if (distantTerrainRenderer != null)
        {
            distantTerrainRenderer.OnChunkSummaryUpdated(chunkPosition, summaries);
        }
    }

    internal void NotifyChunkColumnSummaryInvalidated(int3 chunkPosition)
    {
        if (!enableDistantTerrain)
        {
            return;
        }

        ChunkColumnSummaryInvalidated?.Invoke(chunkPosition);
        if (distantTerrainRenderer != null)
        {
            distantTerrainRenderer.OnChunkSummaryInvalidated(chunkPosition);
        }
    }
    
    /// <summary>
    /// Remove a chunk at the specified position
    /// </summary>
    public void RemoveChunk(int3 chunkPosition)
    {
        if (!chunks.ContainsKey(chunkPosition))
            return;
        
        Chunk chunk = chunks[chunkPosition];
        chunks.Remove(chunkPosition);
        pendingChunkCreations.Remove(chunkPosition);
        NotifyChunkColumnSummaryInvalidated(chunkPosition);
        
        // Notify neighbors to regenerate meshes (edges may need to render faces now)
        NotifyNeighborsToRegenerate(chunkPosition);
        
        chunkPool.Return(chunk);
    }
    
    /// <summary>
    /// Get chunk at position (returns null if doesn't exist)
    /// </summary>
    public Chunk GetChunk(int3 chunkPosition)
    {
        chunks.TryGetValue(chunkPosition, out Chunk chunk);
        return chunk;
    }
    
    /// <summary>
    /// Get block at world block coordinates
    /// </summary>
    public BlockType GetBlockAtPosition(int3 worldBlockPos)
    {
        // Convert world block position to chunk position
        int3 chunkPos = new int3(
            Mathf.FloorToInt(worldBlockPos.x / (float)VoxelData.ChunkWidth),
            Mathf.FloorToInt(worldBlockPos.y / (float)VoxelData.ChunkHeight),
            Mathf.FloorToInt(worldBlockPos.z / (float)VoxelData.ChunkDepth)
        );
        
        Chunk chunk = GetChunk(chunkPos);
        if (chunk == null)
            return BlockType.Air;
        
        // Convert to local block position within chunk
        int3 localPos = new int3(
            worldBlockPos.x - chunkPos.x * VoxelData.ChunkWidth,
            worldBlockPos.y - chunkPos.y * VoxelData.ChunkHeight,
            worldBlockPos.z - chunkPos.z * VoxelData.ChunkDepth
        );
        
        return chunk.GetBlock(localPos.x, localPos.y, localPos.z);
    }
    
    /// <summary>
    /// Set block at world block coordinates
    /// </summary>
    public void SetBlockAtPosition(int3 worldBlockPos, BlockType blockType)
    {
        // Convert world block position to chunk position
        int3 chunkPos = new int3(
            Mathf.FloorToInt(worldBlockPos.x / (float)VoxelData.ChunkWidth),
            Mathf.FloorToInt(worldBlockPos.y / (float)VoxelData.ChunkHeight),
            Mathf.FloorToInt(worldBlockPos.z / (float)VoxelData.ChunkDepth)
        );
        
        Chunk chunk = GetChunk(chunkPos);
        if (chunk == null)
            return;
        
        // Convert to local block position within chunk
        int3 localPos = new int3(
            worldBlockPos.x - chunkPos.x * VoxelData.ChunkWidth,
            worldBlockPos.y - chunkPos.y * VoxelData.ChunkHeight,
            worldBlockPos.z - chunkPos.z * VoxelData.ChunkDepth
        );
        
        chunk.SetBlock(localPos.x, localPos.y, localPos.z, blockType);
        
        // TODO: Update neighboring chunks if block is on boundary
    }
    
    /// <summary>
    /// Get a safe spawn position above terrain at given XZ coordinates.
    /// Scans upward from sea level to find first air block above solid ground.
    /// </summary>
    public Vector3 GetSpawnPosition(int worldX = 0, int worldZ = 0)
    {
        // Start scanning from sea level
        int scanY = VoxelData.SeaLevel;
        int maxScanHeight = VoxelData.MaxWorldHeight - 10; // Don't scan too high
        
        bool foundGround = false;
        int groundY = scanY;
        
        // Scan upward to find ground
        for (int y = scanY; y < maxScanHeight; y++)
        {
            BlockType currentBlock = GetBlockAtPosition(new int3(worldX, y, worldZ));
            BlockType blockAbove = GetBlockAtPosition(new int3(worldX, y + 1, worldZ));
            
            // Found solid ground with air above
            if (currentBlock != BlockType.Air && blockAbove == BlockType.Air)
            {
                foundGround = true;
                groundY = y;
                break;
            }
        }
        
        if (!foundGround)
        {
            // No ground found, use sea level + offset
            Debug.LogWarning($"No ground found at ({worldX}, {worldZ}), spawning at sea level + 10");
            groundY = VoxelData.SeaLevel + 10;
        }
        
        // Spawn player slightly above ground (add player height + small buffer)
        float spawnY = groundY + VoxelData.PlayerHeight + 0.5f;
        
        return new Vector3(worldX, spawnY, worldZ);
    }
    
    /// <summary>
    /// Clear all chunks
    /// </summary>
    public void ClearWorld()
    {
        foreach (var chunk in chunks.Values)
        {
            Destroy(chunk.gameObject);
        }
        chunks.Clear();
    }
    
    private void OnApplicationQuit()
    {
        // Complete all pending jobs before disposing static data
        CompleteAllChunkJobs();
        
        // Cleanup static voxel data
        Chunk.DisposeStaticData();
        chunkPool = null;
    }
    
    private void OnDestroy()
    {
        // Complete all pending jobs before disposing static data
        CompleteAllChunkJobs();
        
        // Cleanup static voxel data
        Chunk.DisposeStaticData();
        chunkPool = null;
    }
    
    /// <summary>
    /// Complete all pending jobs in all chunks to ensure safe cleanup
    /// </summary>
    private void CompleteAllChunkJobs()
    {
        foreach (var chunk in chunks.Values)
        {
            if (chunk != null)
            {
                chunk.CompleteAllJobs();
            }
        }
    }
}
