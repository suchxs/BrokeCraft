using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public Material material;
    public BlockType[] blocktypes;

    [Header("World Generation")]
    public int worldSizeInChunks = 16;   // Fixed world size (16x16 = 256 chunks)
    public int seed = 1234;              // World seed for consistent generation
    public int surfaceLayer = 4;         // Depth of dirt layer below grass
    
    [Header("Generation Settings")]
    public bool generateAsync = true;    // Spread generation over frames (recommended)
    public int chunksPerFrame = 5;       // How many chunks to generate per frame if async
    
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
    
    [Header("Biome Blending")]
    [Tooltip("Radius in blocks to sample for biome blending. Larger = smoother transitions (6-8 recommended)")]
    [Range(1, 16)]
    public int biomeBlendRadius = 8;  // Increased from 4 for smoother transitions
    
    [Tooltip("Use smooth interpolation for biome transitions (prevents walls)")]
    public bool useSmoothBlending = true;
    
    [Header("Biome Settings")]
    [Tooltip("Biome noise scale - LOWER = LARGER biomes with smoother transitions (0.0005-0.002)")]
    public float biomeScale = 0.0003f;   // Large biomes for seamless transitions
    
    public BiomeAttributes[] biomes;     // Array of biome configurations

    // Dictionary to store all chunks by their coordinates
    Dictionary<ChunkCoord, Chunk> chunks = new Dictionary<ChunkCoord, Chunk>();
    
    // Generation queue for async mode
    private Queue<ChunkCoord> chunkQueue = new Queue<ChunkCoord>();
    private bool isGenerating = false;

    void Start()
    {
        // Ensure World is at origin for proper chunk positioning
        transform.position = Vector3.zero;
        
        // Generate static world
        if (generateAsync)
            StartCoroutine(GenerateWorldAsync());
        else
            GenerateWorldImmediate();
    }
    
    void Awake()
    {
        // Also set in Awake to ensure it's at origin before anything else
        transform.position = Vector3.zero;
        
        // Initialize height curve if not set (creates dramatic terrain like Sebastian Lague)
        InitializeHeightCurve();
        
        // OPTIMIZATION: Add ChunkOptimizer component if not present
        if (GetComponent<ChunkOptimizer>() == null)
        {
            gameObject.AddComponent<ChunkOptimizer>();
            Debug.Log("[World] ChunkOptimizer added for performance optimization");
        }
        
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
    
    // Smooth interpolation function (matches Chunk.cs)
    float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
    
    // SEBASTIAN LAGUE'S APPROACH: Generate consistent FBM noise across entire world
    // This is the ONLY noise function - it returns the same value for the same (x,z) everywhere
    public float GenerateNoise(int x, int z)
    {
        float amplitude = 1f;
        float frequency = noiseScale;  // ONE consistent scale for the entire world!
        float noiseHeight = 0f;
        float maxPossibleHeight = 0f;
        
        // Calculate max possible height for normalization (Sebastian Lague's approach)
        float tempAmplitude = 1f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += tempAmplitude;
            tempAmplitude *= persistence;
        }
        
        // Layer multiple octaves of Perlin noise (FBM)
        for (int i = 0; i < octaves; i++)
        {
            // Calculate coordinates with seed offset
            float xCoord = (x + seed) * frequency;
            float zCoord = (z + seed) * frequency;
            
            // Get Perlin noise value (-1 to 1 range)
            float perlinValue = Mathf.PerlinNoise(xCoord, zCoord) * 2 - 1;
            
            // Add weighted noise
            noiseHeight += perlinValue * amplitude;
            
            // Adjust for next octave
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        
        // Normalize to 0-1 range using max possible height
        float normalized = (noiseHeight + maxPossibleHeight) / (2f * maxPossibleHeight);
        return Mathf.Clamp01(normalized);
    }

    // Generate entire world immediately (blocking)
    void GenerateWorldImmediate()
    {
        Debug.Log($"[World] Generating {worldSizeInChunks}x{worldSizeInChunks} world immediately...");
        
        // Step 1: Create all chunks
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                CreateChunk(x, z);
            }
        }
        
        Debug.Log($"[World] Created {chunks.Count} chunks, done!");
        
        // Spawn player after generation
        StartCoroutine(SpawnPlayer());
    }
    
    // Generate world spread over multiple frames (recommended)
    IEnumerator GenerateWorldAsync()
    {
        Debug.Log($"[World] Generating {worldSizeInChunks}x{worldSizeInChunks} world (async, {chunksPerFrame} per frame)...");
        isGenerating = true;
        
        // Queue all chunks
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                chunkQueue.Enqueue(new ChunkCoord(x, z));
            }
        }
        
        int totalChunks = chunkQueue.Count;
        int generated = 0;
        
        // Generate chunks over multiple frames
        while (chunkQueue.Count > 0)
        {
            int chunksThisFrame = 0;
            
            while (chunkQueue.Count > 0 && chunksThisFrame < chunksPerFrame)
            {
                ChunkCoord coord = chunkQueue.Dequeue();
                CreateChunk(coord.x, coord.z);
                chunksThisFrame++;
                generated++;
            }
            
            // Progress log every 50 chunks
            if (generated % 50 == 0)
            {
                Debug.Log($"[World] Progress: {generated}/{totalChunks} chunks ({(float)generated/totalChunks*100:F0}%)");
            }
            
            yield return null; // Wait for next frame
        }
        
        isGenerating = false;
        Debug.Log($"[World] Generation complete! {chunks.Count} chunks created.");
        
        // Spawn player after generation
        StartCoroutine(SpawnPlayer());
    }
    
    // Spawn player at world center
    IEnumerator SpawnPlayer()
    {
        yield return new WaitForSeconds(0.5f);
        
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            // Calculate world center
            float worldSize = worldSizeInChunks * VoxelData.ChunkWidth;
            int centerX = Mathf.FloorToInt(worldSize / 2f);
            int centerZ = Mathf.FloorToInt(worldSize / 2f);
            
            // Get terrain height at spawn
            int terrainHeight = GetTerrainHeightAt(centerX, centerZ);
            
            // Spawn player 5 blocks above terrain
            Vector3 spawnPosition = new Vector3(centerX, terrainHeight + 5, centerZ);
            player.transform.position = spawnPosition;
            
            Debug.Log($"[World] Player spawned at: {spawnPosition} (center of {worldSizeInChunks}x{worldSizeInChunks} world)");
        }
    }
    
    // Calculate actual terrain height at a specific position (matches chunk generation exactly)
    int GetTerrainHeightAt(int x, int z)
    {
        // SEBASTIAN LAGUE'S APPROACH: Same logic as Chunk.cs
        
        // Step 1: Generate consistent noise
        float baseNoise = GenerateNoise(x, z);
        
        // Step 2: Get blended biome height multiplier
        float blendedHeightMultiplier = GetBlendedHeightMultiplier(x, z);
        
        // Step 3: Apply height curve
        float curvedHeight = heightCurve.Evaluate(baseNoise);
        
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
        ChunkCoord coord = new ChunkCoord(x, z);

        // Create chunk GameObject
        GameObject chunkObject = new GameObject($"Chunk_{x}_{z}");

        // Position chunk in world space FIRST
        // Each chunk is VoxelData.ChunkWidth (16) blocks wide
        Vector3 chunkPosition = new Vector3(
            x * VoxelData.ChunkWidth,
            0,
            z * VoxelData.ChunkWidth
        );
        chunkObject.transform.position = chunkPosition;
        
        // Then parent it (keeping world position)
        chunkObject.transform.SetParent(transform, true);

        // Add Chunk component and initialize
        Chunk chunk = chunkObject.AddComponent<Chunk>();
        chunk.coord = coord;
        chunk.Initialize(this);

        // Store in dictionary
        chunks[coord] = chunk;
        
        // Generate mesh immediately
        chunk.GenerateMesh();
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
        int maxChunks = worldSizeInChunks * worldSizeInChunks;
        int queuedChunks = chunkQueue.Count;
        
        return $"World: {worldSizeInChunks}x{worldSizeInChunks} | Chunks: {loadedChunks}/{maxChunks} | Queue: {queuedChunks}";
    }

    // Get biome at world position (with smooth blending)
    public BiomeAttributes GetBiome(int x, int z)
    {
        if (biomes == null || biomes.Length == 0)
            return null;

        // Use 2D Perlin noise for biome selection
        float xCoord = (x + seed) * biomeScale;
        float zCoord = (z + seed) * biomeScale;
        
        float biomeValue = Mathf.PerlinNoise(xCoord, zCoord);
        
        // Map noise value to biome index
        int biomeIndex = Mathf.FloorToInt(biomeValue * biomes.Length);
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