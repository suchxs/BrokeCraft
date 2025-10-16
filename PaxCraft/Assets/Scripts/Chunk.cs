using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    // The full voxel map for entire chunk (16x16x256)
    // Public for neighbor chunk access
    public byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    // Array of chunk sections (16 sections of 16 height each)
    ChunkSection[] sections = new ChunkSection[VoxelData.SectionsPerChunk];

    // Chunk's position in the world grid
    public ChunkCoord coord;

    World world;

    // Initialize chunk with world reference
    public void Initialize(World _world)
    {
        world = _world;
        PopulateVoxelMap();
    }

    // Generate mesh (called after all chunks are initialized)
    public void GenerateMesh()
    {
        CreateSections();
        GenerateAllSections();
    }

    // Public method for sections to access voxel data (supports cross-chunk lookups)
    public byte GetVoxel(int x, int y, int z)
    {
        // If Y is out of bounds, return air (no chunks above/below)
        if (y < 0 || y >= VoxelData.ChunkHeight)
            return 0;

        // If X or Z is out of bounds, check neighboring chunk
        if (x < 0 || x >= VoxelData.ChunkWidth || z < 0 || z >= VoxelData.ChunkWidth)
        {
            return GetVoxelFromNeighbor(x, y, z);
        }

        return voxelMap[x, y, z];
    }

    // Get voxel from neighboring chunk
    byte GetVoxelFromNeighbor(int x, int y, int z)
    {
        // Calculate which neighboring chunk to check
        int neighborChunkX = coord.x;
        int neighborChunkZ = coord.z;

        // Adjust coordinates and find neighbor
        if (x < 0)
        {
            neighborChunkX--;
            x += VoxelData.ChunkWidth;
        }
        else if (x >= VoxelData.ChunkWidth)
        {
            neighborChunkX++;
            x -= VoxelData.ChunkWidth;
        }

        if (z < 0)
        {
            neighborChunkZ--;
            z += VoxelData.ChunkWidth;
        }
        else if (z >= VoxelData.ChunkWidth)
        {
            neighborChunkZ++;
            z -= VoxelData.ChunkWidth;
        }

        // Get neighboring chunk from world
        ChunkCoord neighborCoord = new ChunkCoord(neighborChunkX, neighborChunkZ);
        Chunk neighborChunk = world.GetChunk(neighborCoord);

        // If neighbor exists, get its voxel
        if (neighborChunk != null)
            return neighborChunk.voxelMap[x, y, z];

        // No neighbor chunk = air (edge of world)
        return 0;
    }

    void PopulateVoxelMap()
    {
        // Generate terrain using Perlin noise
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                // Calculate global block position
                int globalX = x + (coord.x * VoxelData.ChunkWidth);
                int globalZ = z + (coord.z * VoxelData.ChunkWidth);

                // Get terrain height using Perlin noise
                int terrainHeight = GetTerrainHeight(globalX, globalZ);

                // Fill column from bottom to terrain height
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    // Block type selection based on height
                    if (y == 0)
                    {
                        // Bedrock at bottom
                        voxelMap[x, y, z] = 1;
                    }
                    else if (y <= terrainHeight - world.surfaceLayer)
                    {
                        // Stone (below dirt layer)
                        voxelMap[x, y, z] = 2;
                    }
                    else if (y < terrainHeight)
                    {
                        // Dirt (surface layer)
                        voxelMap[x, y, z] = 4;
                    }
                    else if (y == terrainHeight)
                    {
                        // Grass block on top
                        voxelMap[x, y, z] = 3;
                    }
                    else
                    {
                        // Air above terrain
                        voxelMap[x, y, z] = 0;
                    }
                }
            }
        }
    }

    // Calculate terrain height using multi-octave Perlin noise (like Minecraft)
    int GetTerrainHeight(int x, int z)
    {
        float amplitude = 1f;
        float frequency = world.scale;
        float noiseHeight = 0f;
        float maxValue = 0f;  // Used for normalizing

        // Layer multiple octaves of Perlin noise
        for (int i = 0; i < world.octaves; i++)
        {
            // Calculate coordinates with seed offset
            float xCoord = (x + world.seed) * frequency;
            float zCoord = (z + world.seed) * frequency;

            // Get Perlin noise value (-1 to 1 range, centered)
            float perlinValue = Mathf.PerlinNoise(xCoord, zCoord) * 2 - 1;

            // Add to total height
            noiseHeight += perlinValue * amplitude;
            maxValue += amplitude;

            // Adjust for next octave
            amplitude *= world.persistence;  // Decrease influence
            frequency *= world.lacunarity;   // Increase detail
        }

        // Normalize to 0-1 range
        noiseHeight = (noiseHeight + maxValue) / (maxValue * 2);

        // Convert to terrain height
        int height = Mathf.FloorToInt(world.baseHeight + (noiseHeight * world.terrainHeightMultiplier));

        // Clamp to valid range
        height = Mathf.Clamp(height, 1, VoxelData.ChunkHeight - 1);

        return height;
    }

    void CreateSections()
    {
        // Create a section for each vertical slice of the chunk
        for (int i = 0; i < VoxelData.SectionsPerChunk; i++)
        {
            int yOffset = i * VoxelData.SectionHeight;

            // Create section GameObject as child of this chunk
            GameObject sectionObject = new GameObject($"Section_{i}");
            sectionObject.transform.parent = transform;
            sectionObject.transform.localPosition = new Vector3(0, yOffset, 0);

            // Add required components
            ChunkSection section = sectionObject.AddComponent<ChunkSection>();
            section.meshRenderer = sectionObject.AddComponent<MeshRenderer>();
            section.meshFilter = sectionObject.AddComponent<MeshFilter>();
            
            // Set material from world
            section.meshRenderer.material = world.material;

            // Initialize the section
            section.Initialize(this, world, yOffset);

            sections[i] = section;
        }
    }

    void GenerateAllSections()
    {
        // Generate mesh for each section (skip empty ones)
        int generatedSections = 0;
        for (int i = 0; i < sections.Length; i++)
        {
            // Skip completely empty sections for better performance
            if (!sections[i].IsEmpty())
            {
                sections[i].GenerateMesh();
                generatedSections++;
            }
        }
        
        Debug.Log($"Chunk ({coord.x},{coord.z}): Generated {generatedSections}/{sections.Length} sections");
    }

}