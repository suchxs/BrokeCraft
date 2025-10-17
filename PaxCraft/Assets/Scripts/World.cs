using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class World : MonoBehaviour
{
    public Material material;
    public BlockType[] blocktypes;

    [Header("World Generation - INFINITE like Minecraft")]
    public int seed = 1234;              // World seed for consistent generation
    public int surfaceLayer = 4;         // Depth of dirt layer below grass
    
    [Header("View Distance (Minecraft-Style)")]
    [Tooltip("Circular radius in chunks. 6 = ~113 chunks, 8 = ~201 chunks, 10 = ~314 chunks")]
    [Range(4, 16)]
    public int viewDistanceInChunks = 6; // Start lower for smooth performance!
    
    [Header("FBM (Fractal Brownian Motion) Settings - Sebastian Lague Style")]
    [Tooltip("Global noise scale - affects the 'zoom' of terrain features. LOWER = larger features")]
    public float noiseScale = 0.003f;    // ONE consistent scale for the entire world!
    
    [Tooltip("Number of noise layers to combine. More = more detail but slower")]
    [Range(1, 8)]
    public int octaves = 4;
    
    [Tooltip("How much each octave contributes (amplitude multiplier). Lower = smoother terrain")]
    [Range(0.1f, 0.9f)]
    public float persistence = 0.5f;
    
    [Tooltip("Frequency multiplier per octave. 2.0 is standard for FBM")]
    [Range(1.5f, 3.0f)]
    public float lacunarity = 2f;
    
    [Header("Terrain Shaping")]
    [Tooltip("Base terrain height (Y level) - where flat areas will be")]
    public int baseTerrainHeight = 64;
    
    [Tooltip("Curve to shape terrain height (0-1 input → 0-1 output). Makes valleys/peaks more dramatic")]
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Tooltip("Maximum height variation from base (in blocks) - total possible height range")]
    public int maxHeightVariation = 80;
    
    // THREAD-SAFE: Pre-baked height curve lookup table (AnimationCurve is NOT thread-safe!)
    private static float[] heightCurveLookup;
    private const int CURVE_RESOLUTION = 1024; // Higher = more accurate
    
    // OPTIMIZATION: FastNoiseLite for 10x faster terrain generation!
    private static FastNoiseLite terrainNoise;
    private static FastNoiseLite temperatureNoise;
    private static FastNoiseLite humidityNoise;
    
    [Header("Biome Blending")]
    [Tooltip("Radius in blocks to sample for biome blending. Larger = smoother transitions (3-4 recommended)")]
    [Range(1, 16)]
    public int biomeBlendRadius = 3;  // OPTIMIZATION FIX: Reduced from 8 to 3 (85% fewer calculations!)
    
    [Tooltip("Use smooth interpolation for biome transitions (prevents walls)")]
    public bool useSmoothBlending = true;
    
    [Header("Biome Settings - Minecraft Style (Temperature + Humidity)")]
    [Tooltip("Temperature noise scale - LOWER = LARGER climate zones (0.0001-0.0005)")]
    public float temperatureScale = 0.0002f;   // Very large temperature zones
    
    [Tooltip("Humidity noise scale - LOWER = LARGER moisture zones (0.0001-0.0005)")]
    public float humidityScale = 0.00025f;     // Large humidity zones
    
    [Tooltip("Temperature offset for noise variation")]
    public float temperatureOffset = 10000f;
    
    [Tooltip("Humidity offset for noise variation")]
    public float humidityOffset = 20000f;
    
    public BiomeAttributes[] biomes;     // Array of biome configurations (use BiomeType for assignment)

    // INFINITE WORLD SYSTEM WITH CACHING (Minecraft-style)
    Dictionary<ChunkCoord, Chunk> chunks = new Dictionary<ChunkCoord, Chunk>(); // All chunks (active + cached)
    List<ChunkCoord> activeChunks = new List<ChunkCoord>(); // Currently visible chunks
    Queue<ChunkCoord> chunkGenerationQueue = new Queue<ChunkCoord>();
    
    // OPTIMIZATION FIX: Rate-limited mesh update queue (prevents cascading regeneration lag!)
    private Queue<Chunk> meshUpdateQueue = new Queue<Chunk>();
    private HashSet<Chunk> meshUpdateQueueSet = new HashSet<Chunk>(); // Prevent duplicate queuing
    private const int MAX_MESH_UPDATES_PER_FRAME = 1; // AGGRESSIVE FIX: Only 1 per frame to eliminate spikes!
    
    // CHUNK CACHING (Minecraft optimization)
    Dictionary<ChunkCoord, Chunk> cachedChunks = new Dictionary<ChunkCoord, Chunk>(); // Inactive but saved chunks
    int maxCachedChunks = 20; // MEMORY FIX: Reduced from 100 to 20 (saves ~5MB + mesh data!)
    
    // Player tracking for chunk loading
    public Transform player;
    ChunkCoord playerLastChunkCoord;
    
    // Performance tracking
    private bool isGeneratingChunks = false;
    
    // MULTITHREADING SYSTEM (Minecraft-style)
    private Dictionary<ChunkCoord, Chunk> pendingChunks = new Dictionary<ChunkCoord, Chunk>();
    
    void Awake()
    {
        // Ensure World is at origin before anything else
        transform.position = Vector3.zero;
        
        // Initialize height curve if not set (creates dramatic terrain like Sebastian Lague)
        InitializeHeightCurve();
        
        // CRITICAL: Bake height curve into thread-safe lookup table BEFORE starting threads!
        BakeHeightCurveLookup();
        
        // OPTIMIZATION: Initialize FastNoiseLite for 10x faster noise generation!
        InitializeFastNoise();
        
        // MINECRAFT OPTIMIZATION #1: Initialize multithreading system with block types
        ChunkThreading.Initialize(blocktypes);
        
        // MINECRAFT OPTIMIZATION #2: Unlock frame rate for maximum performance!
        Application.targetFrameRate = -1;  // No FPS limit
        QualitySettings.vSyncCount = 0;    // Disable VSync for max FPS
        
        // OPTIMIZATION: Add ChunkOptimizer component if not present
        if (GetComponent<ChunkOptimizer>() == null)
        {
            gameObject.AddComponent<ChunkOptimizer>();
            Debug.Log("[World] ChunkOptimizer added for performance optimization");
        }
        
        // OPTIMIZATION: Initialize chunk pooling system
        ChunkPool poolManager = GetComponent<ChunkPool>();
        if (poolManager == null)
        {
            poolManager = gameObject.AddComponent<ChunkPool>();
        }
        poolManager.Initialize(this);
        Debug.Log("[World] Chunk pooling system initialized");
        
        // MINECRAFT OPTIMIZATION: Initialize frame budget upload manager
        ChunkUploadManager uploadManager = GetComponent<ChunkUploadManager>();
        if (uploadManager == null)
        {
            uploadManager = gameObject.AddComponent<ChunkUploadManager>();
        }
        Debug.Log("[World] ✓ Frame budget upload manager initialized");
        
        // OPTIMIZATION: Initialize LOD system (DISABLED - causing overhead)
        // ChunkLOD lodManager = GetComponent<ChunkLOD>();
        // if (lodManager == null)
        // {
        //     lodManager = gameObject.AddComponent<ChunkLOD>();
        // }
        // LOD will be initialized after player is found
        
        // Add debug overlays if not present
        if (FindObjectOfType<DebugConsole>() == null)
        {
            gameObject.AddComponent<DebugConsole>();
            Debug.Log("[World] DebugConsole added (F1)");
        }
        
        if (FindObjectOfType<ChunkDebugOverlay>() == null)
        {
            gameObject.AddComponent<ChunkDebugOverlay>();
            Debug.Log("[World] ChunkDebugOverlay added (F3)");
        }
        
        Debug.Log($"[World] ✓ Multithreading initialized: {SystemInfo.processorCount} cores detected");
    }

    void Start()
    {
        // Find player if not assigned
        if (player == null)
        {
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }
        
        // Start infinite chunk generation with proper spawn
        if (player != null)
        {
            StartCoroutine(InitializeWorldAndSpawnPlayer());
            
            // Initialize LOD system with player reference (DISABLED - causing overhead)
            // ChunkLOD lodManager = GetComponent<ChunkLOD>();
            // if (lodManager != null)
            // {
            //     lodManager.Initialize(this, player);
            // }
        }
        else
        {
            Debug.LogError("[World] No player found! Assign player in Inspector or add PlayerController.");
        }
    }
    
    // Initialize world by generating spawn chunks FIRST, then spawning player
    IEnumerator InitializeWorldAndSpawnPlayer()
    {
        Debug.Log("[World] Generating spawn area...");
        
        // Get spawn position (spawn player HIGH in the air to prevent falling through loading chunks)
        Vector3 spawnPos = GetSpawnPosition();
        ChunkCoord spawnChunk = GetChunkCoordFromPosition(spawnPos);
        
        // IMMEDIATELY place player at spawn (don't disable controller!)
        // They'll slowly fall while chunks generate beneath them
        player.position = spawnPos;
        
        // Initialize player chunk coord
        playerLastChunkCoord = spawnChunk;
        
        Debug.Log($"[World] Player placed at {spawnPos} - generating ground...");
        
        // MINECRAFT OPTIMIZATION: Generate MINIMAL spawn area (just 1 chunk!)
        // Player falls while nearby chunks load in background
        int generated = 0;
        
        List<Chunk> spawnChunks = new List<Chunk>();
        
        Debug.Log($"[World] Generating spawn platform (1 chunk)...");
        
        // PHASE 1: Generate ONLY the spawn chunk (instant ground!)
        Chunk spawnChunkObj = CreateChunkOptimized(spawnChunk.x, spawnChunk.z, false);
        activeChunks.Add(spawnChunk);
        spawnChunks.Add(spawnChunkObj);
        generated++;
        
        yield return null; // Give one frame for chunk to render
        
        // PHASE 2: Update neighbor edges (only once, at the end)
        Debug.Log($"[World] Fixing chunk boundaries...");
        foreach (Chunk chunk in spawnChunks)
        {
            chunk.RegenerateMesh(); // Update edges now that all neighbors exist
        }
        
        Debug.Log($"[World] ✓ Spawn platform loaded! ({generated} chunk) - Safe landing!");
        
        // MINECRAFT STYLE: Load remaining spawn chunks GRADUALLY in background
        // Queue nearby chunks but let upload manager spread them over frames
        Debug.Log("[World] Loading nearby chunks in background...");
        
        // Wait a tiny bit for player to fall
        yield return new WaitForSeconds(0.1f);
        
        // Queue nearby chunks for background loading (upload manager will pace them)
        CheckViewDistance();
        
        // Start the chunk generation system (will only generate when player moves)
        StartCoroutine(GenerateChunksAroundPlayer());
    }
    
    void Update()
    {
        // MINECRAFT THREADING: Process completed chunk data from background threads
        ChunkThreading.ProcessCompletedData();
        
        // OPTIMIZATION FIX: Process queued mesh updates (rate-limited to prevent lag spikes!)
        ProcessMeshUpdateQueue();
        
        // MEMORY FIX: Trigger garbage collection every 5 seconds to prevent memory buildup
        if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
        {
            System.GC.Collect(0, System.GCCollectionMode.Optimized); // Quick generation 0 collection
        }
        
        // Check if player moved to a new chunk
        if (player != null)
        {
            ChunkCoord currentChunkCoord = GetChunkCoordFromPosition(player.position);
            
            // If player moved to a new chunk, update chunks
            if (!currentChunkCoord.Equals(playerLastChunkCoord))
            {
                playerLastChunkCoord = currentChunkCoord;
                CheckViewDistance();
            }
        }
    }
    
    // OPTIMIZATION FIX: Queue chunk for mesh update (prevents immediate cascade)
    public void QueueChunkMeshUpdate(Chunk chunk)
    {
        if (chunk != null && !meshUpdateQueueSet.Contains(chunk))
        {
            meshUpdateQueue.Enqueue(chunk);
            meshUpdateQueueSet.Add(chunk);
        }
    }
    
    // OPTIMIZATION FIX: Process mesh updates with frame budget (AGGRESSIVE - only 1 per frame!)
    void ProcessMeshUpdateQueue()
    {
        int updatesThisFrame = 0;
        
        while (meshUpdateQueue.Count > 0 && updatesThisFrame < MAX_MESH_UPDATES_PER_FRAME)
        {
            Chunk chunk = meshUpdateQueue.Dequeue();
            meshUpdateQueueSet.Remove(chunk);
            
            // Verify chunk still exists and is active
            if (chunk != null && chunk.gameObject.activeInHierarchy)
            {
                chunk.RegenerateMesh();
                updatesThisFrame++;
            }
        }
    }
    
    void OnDestroy()
    {
        // Shutdown threading system cleanly
        ChunkThreading.Shutdown();
        Debug.Log("[World] Multithreading shutdown complete");
    }
    
    // Initialize a nice terrain curve if not already set
    void InitializeHeightCurve()
    {
        // If curve has default values (2 keys, linear), replace with better curve
        if (heightCurve == null || heightCurve.keys.Length <= 2)
        {
            heightCurve = new AnimationCurve();
            
            // MINECRAFT-STYLE CURVE: Gentle slopes, dramatic peaks
            // This curve creates realistic terrain with smooth valleys and towering mountains
            heightCurve.AddKey(0.0f, 0.0f);    // Deep valleys/ocean floor
            heightCurve.AddKey(0.2f, 0.05f);   // Very gentle rise
            heightCurve.AddKey(0.4f, 0.2f);    // Plains/gentle hills
            heightCurve.AddKey(0.6f, 0.45f);   // Hills start rising
            heightCurve.AddKey(0.75f, 0.7f);   // Mountains rise faster
            heightCurve.AddKey(0.9f, 0.9f);    // Steep mountain slopes
            heightCurve.AddKey(1.0f, 1.0f);    // Towering peaks
            
            Debug.Log("[World] Initialized Minecraft-style terrain height curve");
        }
    }
    
    // THREAD-SAFE: Bake AnimationCurve into lookup table (AnimationCurve.Evaluate is NOT thread-safe!)
    void BakeHeightCurveLookup()
    {
        heightCurveLookup = new float[CURVE_RESOLUTION];
        
        for (int i = 0; i < CURVE_RESOLUTION; i++)
        {
            float t = (float)i / (CURVE_RESOLUTION - 1);
            heightCurveLookup[i] = heightCurve.Evaluate(t);
        }
        
        Debug.Log($"[World] ✓ Baked height curve lookup table ({CURVE_RESOLUTION} samples) for thread-safe terrain generation");
    }
    
    // OPTIMIZATION: Initialize FastNoiseLite instances (10x faster than Perlin!)
    void InitializeFastNoise()
    {
        // Terrain noise - used for height generation
        terrainNoise = new FastNoiseLite(seed);
        terrainNoise.SetFrequency(noiseScale);
        
        // Temperature noise - used for biome climate
        temperatureNoise = new FastNoiseLite(seed + 1000);
        temperatureNoise.SetFrequency(temperatureScale);
        
        // Humidity noise - used for biome moisture
        humidityNoise = new FastNoiseLite(seed + 2000);
        humidityNoise.SetFrequency(humidityScale);
        
        Debug.Log("[World] ✓ FastNoiseLite initialized (10x faster terrain generation!)");
    }
    
    // THREAD-SAFE: Evaluate height curve using pre-baked lookup table
    public static float EvaluateHeightCurve(float t)
    {
        // Clamp input to 0-1 range
        t = Mathf.Clamp01(t);
        
        // Convert to lookup table index
        float indexFloat = t * (CURVE_RESOLUTION - 1);
        int index0 = Mathf.FloorToInt(indexFloat);
        int index1 = Mathf.Min(index0 + 1, CURVE_RESOLUTION - 1);
        
        // Linear interpolation between two nearest samples
        float frac = indexFloat - index0;
        return Mathf.Lerp(heightCurveLookup[index0], heightCurveLookup[index1], frac);
    }
    
    // Smooth interpolation function (matches Chunk.cs)
    float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    // OPTIMIZATION: FBM noise using FastNoiseLite (10x faster than Perlin!)
    // This is the ONLY noise function - it returns the same value for the same (x,z) everywhere
    public float GenerateNoise(int x, int z)
    {
        float amplitude = 1f;
        float frequency = 1f;  // FastNoiseLite handles frequency internally
        float noiseHeight = 0f;
        float maxPossibleHeight = 0f;
        
        // Calculate max possible height for normalization
        float tempAmplitude = 1f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += tempAmplitude;
            tempAmplitude *= persistence;
        }
        
        // Layer multiple octaves using FastNoiseLite (FBM)
        for (int i = 0; i < octaves; i++)
        {
            // OPTIMIZATION: Use FastNoiseLite instead of Unity's slow Perlin noise!
            // FastNoiseLite returns values in [0, 1] range already
            float noiseValue = terrainNoise.GetNoise(x * frequency, z * frequency);
            
            // Convert to [-1, 1] range for FBM
            noiseValue = noiseValue * 2f - 1f;
            
            // Add weighted noise
            noiseHeight += noiseValue * amplitude;
            
            // Adjust for next octave
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        
        // Normalize to 0-1 range using max possible height
        float normalized = (noiseHeight + maxPossibleHeight) / (2f * maxPossibleHeight);
        return Mathf.Clamp01(normalized);
    }

    // MINECRAFT-STYLE INFINITE WORLD GENERATION
    
    // Get spawn position (world origin by default)
    Vector3 GetSpawnPosition()
    {
        int spawnX = 0;
        int spawnZ = 0;
        int terrainHeight = GetTerrainHeightAt(spawnX, spawnZ);
        
        // Spawn player HIGH above terrain so chunks generate while falling
        return new Vector3(spawnX, terrainHeight + 30, spawnZ);
    }
    
    // Convert world position to chunk coordinate
    ChunkCoord GetChunkCoordFromPosition(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x, z);
    }
    
    // Check which chunks should be loaded/unloaded based on player position
    // MINECRAFT STYLE: CIRCULAR radius, not square! (30% fewer chunks!)
    void CheckViewDistance()
    {
        ChunkCoord playerChunk = GetChunkCoordFromPosition(player.position);
        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);
        activeChunks.Clear();
        
        // Build list of chunks sorted by DISTANCE (closest first - true Minecraft style!)
        List<ChunkCoordWithDistance> chunksWithDistance = new List<ChunkCoordWithDistance>();
        
        // Only load chunks in CIRCULAR radius (not square!)
        int viewDistSquared = viewDistanceInChunks * viewDistanceInChunks;
        
        for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
        {
            for (int z = -viewDistanceInChunks; z <= viewDistanceInChunks; z++)
            {
                // CRITICAL: Only load if within CIRCULAR radius (like Minecraft!)
                float distSquared = x * x + z * z;
                if (distSquared <= viewDistSquared)
                {
                    ChunkCoord coord = new ChunkCoord(playerChunk.x + x, playerChunk.z + z);
                    chunksWithDistance.Add(new ChunkCoordWithDistance(coord, distSquared));
                    activeChunks.Add(coord);
                }
            }
        }
        
        // Sort by distance (closest first!)
        chunksWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        // Queue chunks that don't exist (caching disabled to prevent memory leaks)
        foreach (var item in chunksWithDistance)
        {
            ChunkCoord coord = item.coord;
            
            // MEMORY FIX: Caching disabled - always regenerate to prevent memory buildup
            // Check if chunk doesn't exist
            if (!chunks.ContainsKey(coord))
            {
                // Need to generate new chunk
                if (!chunkGenerationQueue.Contains(coord))
                {
                    chunkGenerationQueue.Enqueue(coord);
                }
            }
        }
        
        // CRITICAL FIX: Unload ALL chunks that are too far away (not just previously active)
        // This fixes the bug where threaded chunks weren't being unloaded!
        List<ChunkCoord> chunksToUnload = new List<ChunkCoord>();
        
        foreach (var kvp in chunks)
        {
            ChunkCoord coord = kvp.Key;
            if (!activeChunks.Contains(coord))
            {
                chunksToUnload.Add(coord);
            }
        }
        
        // Unload chunks outside active range
        foreach (ChunkCoord coord in chunksToUnload)
        {
            UnloadChunk(coord);
        }
    }
    
    // Helper struct for distance-based sorting
    struct ChunkCoordWithDistance
    {
        public ChunkCoord coord;
        public float distance;
        
        public ChunkCoordWithDistance(ChunkCoord c, float d)
        {
            coord = c;
            distance = d;
        }
    }
    
    // Generate chunks around player (coroutine runs continuously)
    // MINECRAFT THREADING: Sends chunk requests to background threads!
    IEnumerator GenerateChunksAroundPlayer()
    {
        // AGGRESSIVE FIX: Moderate generation rate to prevent queue buildup
        // Let upload manager catch up instead of overwhelming it
        float chunksPerSecond = 60f; // Reduced from 120 to prevent queue overflow
        float timeBetweenChunks = 1f / chunksPerSecond;
        float lastChunkTime = 0f;
        
        while (true)
        {
            // Only queue if enough time has passed (rate limiting!)
            if (Time.time - lastChunkTime >= timeBetweenChunks)
            {
                // Queue chunks for background generation
                if (chunkGenerationQueue.Count > 0)
                {
                    // MINECRAFT STYLE: Queue 1 chunk request per frame
                    ChunkCoord coord = chunkGenerationQueue.Dequeue();
                    
                    // Double-check it's still in view distance
                    if (activeChunks.Contains(coord))
                    {
                        // THREADED: Request chunk data generation on background thread!
                        ChunkThreading.RequestChunkData(coord, this, OnChunkDataGenerated);
                        lastChunkTime = Time.time;
                        
                        // Only log occasionally (every 50 chunks)
                        if (chunkGenerationQueue.Count % 50 == 0 && chunkGenerationQueue.Count > 0)
                        {
                            Debug.Log($"[World] 🧵 Threading: {chunkGenerationQueue.Count} chunks queued");
                        }
                    }
                }
            }
            
            yield return null; // Wait for next frame
        }
    }
    
    // CALLBACK: Called when background thread finishes generating chunk data
    void OnChunkDataGenerated(ChunkDataResult result)
    {
        // This runs on MAIN THREAD (thread-safe callback from ChunkThreading.ProcessCompletedData())
        // Voxel data is ready - now request MESH generation on background thread!
        
        // Double-check chunk is still needed
        if (!activeChunks.Contains(result.coord) || chunks.ContainsKey(result.coord))
        {
            return; // Player moved away or chunk already exists
        }
        
        // MINECRAFT OPTIMIZATION: Request mesh generation on background thread!
        // This is where the REAL performance gain happens - mesh generation is expensive!
        ChunkThreading.RequestMeshData(result, OnChunkMeshGenerated);
    }
    
    // CALLBACK: Called when background thread finishes generating mesh data
    void OnChunkMeshGenerated(MeshDataResult result)
    {
        // This runs on MAIN THREAD
        // Mesh data is ready - queue for GPU upload with frame budget
        
        // Double-check chunk is still needed
        if (!activeChunks.Contains(result.coord) || chunks.ContainsKey(result.coord))
        {
            return; // Player moved away or chunk already exists
        }
        
        // MINECRAFT OPTIMIZATION: Queue for upload instead of immediate creation
        // This spreads GPU work over multiple frames (smooth FPS!)
        bool isPriority = ChunkUploadManager.IsNearPlayer(result.coord, player.position);
        ChunkUploadManager.Instance.QueueMeshUpload(result, isPriority);
    }
    
    // Create a chunk from pre-generated mesh data (MAIN THREAD - GPU upload only!)
    // PUBLIC: Called by ChunkUploadManager with frame budget control
    public void CreateChunkFromMeshDataImmediate(MeshDataResult meshResult)
    {
        ChunkDataResult chunkData = meshResult.chunkData;
        
        // OPTIMIZATION FIX: Use pooled GameObject instead of allocating new one!
        Vector3 position = new Vector3(
            chunkData.coord.x * VoxelData.ChunkWidth,
            0,
            chunkData.coord.z * VoxelData.ChunkWidth
        );
        GameObject chunkObject = ChunkPool.GetChunkObject(chunkData.coord, position);
        chunkObject.transform.SetParent(transform);
        
        // Add Chunk component (or reuse if exists)
        Chunk chunk = chunkObject.GetComponent<Chunk>();
        if (chunk == null)
        {
            chunk = chunkObject.AddComponent<Chunk>();
        }
        chunk.coord = chunkData.coord;
        chunk.world = this;
        
        // Use pre-generated voxel data from background thread
        chunk.voxelMap = chunkData.voxelMap;
        
        // Add to dictionary
        chunks[chunkData.coord] = chunk;
        
        // Create sections and upload pre-generated mesh data to GPU
        chunk.CreateSectionsFromMeshData(meshResult.sectionMeshData, meshResult.biomeMap);
        
        // Mark as meshed for neighbor updates
        chunk.MarkAsMeshed();
        
        // CRITICAL FIX: Queue neighbor updates after chunk is created
        // Check all 4 horizontal neighbors and queue them for edge updates
        ChunkCoord[] neighborCoords = new ChunkCoord[]
        {
            new ChunkCoord(chunkData.coord.x + 1, chunkData.coord.z),
            new ChunkCoord(chunkData.coord.x - 1, chunkData.coord.z),
            new ChunkCoord(chunkData.coord.x, chunkData.coord.z + 1),
            new ChunkCoord(chunkData.coord.x, chunkData.coord.z - 1)
        };
        
        foreach (ChunkCoord neighborCoord in neighborCoords)
        {
            Chunk neighbor = GetChunk(neighborCoord);
            if (neighbor != null && neighbor.gameObject.activeInHierarchy)
            {
                QueueChunkMeshUpdate(neighbor);
            }
        }
    }
    
    // Unload a chunk (CACHE instead of destroy - Minecraft style!)
    void UnloadChunk(ChunkCoord coord)
    {
        if (chunks.ContainsKey(coord))
        {
            Chunk chunk = chunks[coord];
            chunks.Remove(coord);
            
            // MEMORY FIX: Remove from mesh update queue if present
            if (meshUpdateQueueSet.Contains(chunk))
            {
                meshUpdateQueueSet.Remove(chunk);
            }
            
            // MEMORY FIX: Don't cache - DESTROY immediately to free memory!
            // Caching was causing memory buildup - better to regenerate than leak memory
            chunk.ReturnMeshesToPool();
            ChunkPool.ReturnChunkObject(chunk.gameObject);
            
            /* OLD CACHING CODE (caused memory leaks):
            // MINECRAFT OPTIMIZATION: Cache instead of destroy!
            if (cachedChunks.Count < maxCachedChunks)
            {
                // Add to cache (just disable, don't destroy)
                chunk.gameObject.SetActive(false);
                cachedChunks[coord] = chunk;
            }
            else
            {
                // Cache full - destroy oldest chunk
                if (cachedChunks.Count > 0)
                {
                    // Remove oldest cached chunk
                    var oldestCoord = cachedChunks.Keys.First();
                    Chunk oldestChunk = cachedChunks[oldestCoord];
                    
                    // Return meshes to pool before destroying chunk
                    oldestChunk.ReturnMeshesToPool();
                    
                    // OPTIMIZATION FIX: Return GameObject to pool instead of destroying!
                    ChunkPool.ReturnChunkObject(oldestChunk.gameObject);
                    cachedChunks.Remove(oldestCoord);
                }
                
                // Cache this chunk
                chunk.gameObject.SetActive(false);
                cachedChunks[coord] = chunk;
            }
            */
        }
    }
    
    // Reactivate chunk from cache (INSTANT!)
    void ActivateChunkFromCache(ChunkCoord coord)
    {
        if (cachedChunks.ContainsKey(coord))
        {
            Chunk chunk = cachedChunks[coord];
            cachedChunks.Remove(coord);
            
            // Reactivate chunk (instant - no mesh regeneration!)
            chunk.gameObject.SetActive(true);
            chunks[coord] = chunk;
            
            // CRITICAL FIX: Queue neighbor updates (not this chunk - it already has mesh!)
            // Check all 4 horizontal neighbors and queue them for edge updates
            ChunkCoord[] neighborCoords = new ChunkCoord[]
            {
                new ChunkCoord(coord.x + 1, coord.z),
                new ChunkCoord(coord.x - 1, coord.z),
                new ChunkCoord(coord.x, coord.z + 1),
                new ChunkCoord(coord.x, coord.z - 1)
            };
            
            foreach (ChunkCoord neighborCoord in neighborCoords)
            {
                Chunk neighbor = GetChunk(neighborCoord);
                if (neighbor != null && neighbor.gameObject.activeInHierarchy)
                {
                    QueueChunkMeshUpdate(neighbor);
                }
            }
        }
    }
    
    // Calculate actual terrain height at a specific position (matches chunk generation exactly)
    // PUBLIC: Used by Chunk.cs for neighbor face culling optimization
    // THREAD-SAFE: Uses pre-baked curve lookup instead of AnimationCurve.Evaluate()
    public int GetTerrainHeightAt(int x, int z)
    {
        // SEBASTIAN LAGUE'S APPROACH: Same logic as Chunk.cs
        
        // Step 1: Generate consistent noise
        float baseNoise = GenerateNoise(x, z);
        
        // Step 2: Get blended biome height multiplier
        float blendedHeightMultiplier = GetBlendedHeightMultiplier(x, z);
        
        // Step 3: Apply height curve (THREAD-SAFE lookup table!)
        float curvedHeight = EvaluateHeightCurve(baseNoise);
        
        // Step 4: Apply biome multiplier
        float finalHeight = curvedHeight * blendedHeightMultiplier;
        
        // Step 5: Convert to blocks
        int height = Mathf.FloorToInt(baseTerrainHeight + (finalHeight * maxHeightVariation));
        height = Mathf.Clamp(height, 1, VoxelData.ChunkHeight - 1);
        
        return height;
    }
    
    // Get blended height multiplier from nearby biomes (matches Chunk.cs exactly)
    float GetBlendedHeightMultiplier(int x, int z)
    {
        float totalMultiplier = 0f;
        float totalWeight = 0f;
        
        // Sample biomes in a radius
        for (int offsetX = -biomeBlendRadius; offsetX <= biomeBlendRadius; offsetX++)
        {
            for (int offsetZ = -biomeBlendRadius; offsetZ <= biomeBlendRadius; offsetZ++)
            {
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                
                if (distance > biomeBlendRadius)
                    continue;
                
                // Smooth falloff (inverse square with smoothstep)
                float normalizedDist = distance / biomeBlendRadius;
                float weight = 1f - SmoothStep(0f, 1f, normalizedDist);
                weight = weight * weight; // Square for extra smoothness
                
                // Get biome height weight at this position
                BiomeAttributes biome = GetBiome(x + offsetX, z + offsetZ);
                if (biome != null)
                {
                    totalMultiplier += biome.heightWeight * weight;
                    totalWeight += weight;
                }
            }
        }
        
        // Normalize
        if (totalWeight > 0.001f)
        {
            return totalMultiplier / totalWeight;
        }
        
        return 0.5f; // Default mid-level height
    }

    void CreateChunk(int x, int z)
    {
        CreateChunkOptimized(x, z, true);
    }
    
    Chunk CreateChunkOptimized(int x, int z, bool notifyNeighbors)
    {
        ChunkCoord coord = new ChunkCoord(x, z);

        // OPTIMIZATION FIX: Get chunk from pool instead of creating new GameObject
        Vector3 chunkPosition = new Vector3(
            x * VoxelData.ChunkWidth,
            0,
            z * VoxelData.ChunkWidth
        );
        GameObject chunkObject = ChunkPool.GetChunkObject(coord, chunkPosition);
        
        // Then parent it (keeping world position)
        chunkObject.transform.SetParent(transform, true);

        // Add Chunk component (or reuse if exists)
        Chunk chunk = chunkObject.GetComponent<Chunk>();
        if (chunk == null)
        {
            chunk = chunkObject.AddComponent<Chunk>();
        }
        chunk.coord = coord;
        chunk.Initialize(this);

        // Store in dictionary
        chunks[coord] = chunk;
        
        // Generate mesh (optionally skip neighbor notification for speed)
        chunk.GenerateMesh(notifyNeighbors);
        
        return chunk;
    }

    // Get chunk at specific coordinate (for neighbor access later)
    public Chunk GetChunk(ChunkCoord coord)
    {
        if (chunks.ContainsKey(coord))
            return chunks[coord];
        return null;
    }
    
    // OPTIMIZATION: Expose all chunks for optimizer
    public IEnumerable<Chunk> GetAllChunks()
    {
        return chunks.Values;
    }
    
    // Get world generation stats for debug UI
    public string GetWorldStats()
    {
        int loadedChunks = chunks.Count;
        int queuedChunks = chunkGenerationQueue.Count;
        int meshUpdateQueue = this.meshUpdateQueue.Count;
        
        // Calculate approximate circular chunks (π * r²)
        int maxVisible = Mathf.FloorToInt(Mathf.PI * viewDistanceInChunks * viewDistanceInChunks);
        
        string poolStats = ChunkPool.GetPoolStats();
        string uploadStats = ChunkUploadManager.Instance != null ? ChunkUploadManager.Instance.GetStats() : "";
        
        // MEMORY MONITORING: Show memory usage
        long memoryUsedMB = System.GC.GetTotalMemory(false) / 1048576; // Convert to MB
        
        return $"World: INFINITE | Loaded: {loadedChunks}/{maxVisible} | Queue: {queuedChunks} | MeshQ: {meshUpdateQueue} | Memory: {memoryUsedMB}MB | {poolStats} | {uploadStats}";
    }

    // OPTIMIZATION: Biome generation using FastNoiseLite (10x faster than Perlin!)
    public BiomeAttributes GetBiome(int x, int z)
    {
        if (biomes == null || biomes.Length == 0)
            return null;

        // OPTIMIZATION: Use FastNoiseLite for biome noise (much faster!)
        // FastNoiseLite returns values in [0, 1] range already
        float temperature = temperatureNoise.GetNoise(x + temperatureOffset, z + temperatureOffset);
        float humidity = humidityNoise.GetNoise(x + humidityOffset, z + humidityOffset);
        
        // Select biome based on temperature + humidity grid
        // This creates natural, large biome regions like Minecraft
        return SelectBiomeFromClimate(temperature, humidity);
    }
    
    // Select biome based on temperature and humidity values (0-1 range)
    BiomeAttributes SelectBiomeFromClimate(float temperature, float humidity)
    {
        // MINECRAFT-STYLE BIOME GRID:
        // 
        //              DRY (0)        MEDIUM (0.5)      WET (1)
        // COLD (0)     Snowy Tundra   Snowy Taiga       Ice Spikes
        // MEDIUM (0.5) Plains         Forest            Swamp
        // HOT (1)      Desert         Savanna           Jungle
        
        // Categorize temperature (3 zones)
        int tempZone = 0;  // 0=cold, 1=medium, 2=hot
        if (temperature > 0.66f)
            tempZone = 2;  // Hot
        else if (temperature > 0.33f)
            tempZone = 1;  // Medium
        
        // Categorize humidity (3 zones)
        int humidZone = 0;  // 0=dry, 1=medium, 2=wet
        if (humidity > 0.66f)
            humidZone = 2;  // Wet
        else if (humidity > 0.33f)
            humidZone = 1;  // Medium
        
        // Map to biome index (0-8 for 3x3 grid)
        int biomeIndex = tempZone * 3 + humidZone;
        
        // Clamp to available biomes
        biomeIndex = Mathf.Clamp(biomeIndex, 0, biomes.Length - 1);
        
        return biomes[biomeIndex];
    }

    // Get blended biome properties at position (for smooth transitions)
    public BiomeAttributes GetBlendedBiome(int x, int z)
    {
        if (biomes == null || biomes.Length == 0)
            return null;

        // Sample biome at 4 neighboring positions for blending
        BiomeAttributes center = GetBiome(x, z);
        BiomeAttributes north = GetBiome(x, z + 4);
        BiomeAttributes south = GetBiome(x, z - 4);
        BiomeAttributes east = GetBiome(x + 4, z);
        BiomeAttributes west = GetBiome(x - 4, z);

        // If all neighbors are the same biome, return it directly
        if (center == north && center == south && center == east && center == west)
            return center;

        // Blend biome properties (simplified - just returns center for now)
        // This creates smoother transitions as neighboring biomes influence terrain
        return center;
    }

    // Get blended grass color at position (Minecraft-style blending)
    public Color GetBlendedGrassColor(int x, int z)
    {
        if (biomes == null || biomes.Length == 0)
            return Color.white;

        // Sample multiple positions and blend colors
        int sampleRadius = 2;
        Color totalColor = Color.black;
        int sampleCount = 0;

        for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
        {
            for (int dz = -sampleRadius; dz <= sampleRadius; dz++)
            {
                BiomeAttributes biome = GetBiome(x + dx, z + dz);
                if (biome != null)
                {
                    totalColor += biome.grassColor;
                    sampleCount++;
                }
            }
        }

        if (sampleCount == 0)
            return Color.white;

        return totalColor / sampleCount;
    }

}

[System.Serializable]
public class BlockType
{

    public string blockName;
    public bool isSolid;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    // Back, Front, Top, Bottom, Left, Right

    public int GetTextureID(int faceIndex)
    {

        switch (faceIndex)
        {

            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID; invalid face index");
                return 0;


        }

    }

}