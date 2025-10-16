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
    
    [Header("Terrain Generation - Minecraft Style")]
    public int baseHeight = 64;          // Sea level / base terrain height
    public int terrainHeightMultiplier = 40;  // Maximum terrain height variation
    public int surfaceLayer = 4;         // Depth of dirt layer below grass
    
    [Header("Multi-Octave Noise Settings")]
    public float scale = 0.005f;         // Base frequency (LOWER = BIGGER features)
    public int octaves = 4;              // Number of noise layers (more = more detail)
    public float persistence = 0.45f;    // How much each octave contributes (0-1)
    public float lacunarity = 2f;        // Frequency multiplier per octave

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