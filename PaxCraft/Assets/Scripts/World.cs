using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public Material material;
    public BlockType[] blocktypes;

    [Header("World Generation")]
    public int worldSizeInChunks = 16;  // Create a 16x16 chunk world (256x256 blocks)
    public int seed = 1234;              // World seed for consistent generation
    public int surfaceLayer = 4;         // Depth of dirt layer below grass
    
    [Header("Multi-Octave Noise Settings")]
    public int octaves = 4;              // Number of noise layers (more = more detail)
    public float persistence = 0.45f;    // How much each octave contributes (0-1)
    public float lacunarity = 2f;        // Frequency multiplier per octave
    
    [Header("Biome Settings")]
    public float biomeScale = 0.001f;    // LOWER = LARGER biomes, smoother transitions
    public BiomeAttributes[] biomes;     // Array of biome configurations

    // Dictionary to store all chunks by their coordinates
    Dictionary<ChunkCoord, Chunk> chunks = new Dictionary<ChunkCoord, Chunk>();

    void Start()
    {
        GenerateWorld();
    }

    void GenerateWorld()
    {
        Debug.Log($"Generating world: {worldSizeInChunks}x{worldSizeInChunks} chunks");

        // Step 1: Create all chunks and populate voxel data
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                CreateChunk(x, z);
            }
        }

        Debug.Log($"Created {chunks.Count} chunks, generating meshes...");

        // Step 2: Generate meshes after all chunks exist (so neighbors can be checked)
        foreach (Chunk chunk in chunks.Values)
        {
            chunk.GenerateMesh();
        }

        Debug.Log($"World generation complete!");
    }

    void CreateChunk(int x, int z)
    {
        ChunkCoord coord = new ChunkCoord(x, z);

        // Create chunk GameObject
        GameObject chunkObject = new GameObject($"Chunk_{x}_{z}");
        chunkObject.transform.parent = transform;

        // Position chunk in world space
        // Each chunk is VoxelData.ChunkWidth (16) blocks wide
        Vector3 chunkPosition = new Vector3(
            x * VoxelData.ChunkWidth,
            0,
            z * VoxelData.ChunkWidth
        );
        chunkObject.transform.position = chunkPosition;

        // Add Chunk component and initialize
        Chunk chunk = chunkObject.AddComponent<Chunk>();
        chunk.coord = coord;
        chunk.Initialize(this);

        // Store in dictionary
        chunks[coord] = chunk;
    }

    // Get chunk at specific coordinate (for neighbor access later)
    public Chunk GetChunk(ChunkCoord coord)
    {
        if (chunks.ContainsKey(coord))
            return chunks[coord];
        return null;
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