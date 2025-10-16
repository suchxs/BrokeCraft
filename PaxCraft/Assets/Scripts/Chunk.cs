using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    // The full voxel map for entire chunk (16x16x256)
    byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    // Array of chunk sections (16 sections of 16 height each)
    ChunkSection[] sections = new ChunkSection[VoxelData.SectionsPerChunk];

    World world;

    public GameObject sectionPrefab;  // Prefab with MeshRenderer and MeshFilter

    // Initialize chunk with world reference
    public void Initialize(World _world)
    {
        world = _world;

        PopulateVoxelMap();
        CreateSections();
        GenerateAllSections();
    }

    // Public method for sections to access voxel data
    public byte GetVoxel(int x, int y, int z)
    {
        // Bounds check
        if (x < 0 || x >= VoxelData.ChunkWidth ||
            y < 0 || y >= VoxelData.ChunkHeight ||
            z < 0 || z >= VoxelData.ChunkWidth)
            return 0; // Return air if out of bounds

        return voxelMap[x, y, z];
    }

    void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    // Create test terrain that uses multiple sections
                    // Block IDs: 0=Air, 1=Bedrock, 2=Stone, 3=Grass
                    
                    if (y == 0)
                        voxelMap[x, y, z] = 1; // Bedrock layer at bottom
                    else if (y < 64)
                        voxelMap[x, y, z] = 2; // Stone up to y=63 (4 sections!)
                    else if (y == 64)
                        voxelMap[x, y, z] = 3; // Grass at y=64 (like Minecraft sea level)
                    else
                        voxelMap[x, y, z] = 0; // Air above y=64
                    
                    // The chunk still goes to y=255, but it's all air above y=64
                }
            }
        }
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
        for (int i = 0; i < sections.Length; i++)
        {
            // Skip completely empty sections for better performance
            if (!sections[i].IsEmpty())
            {
                sections[i].GenerateMesh();
                Debug.Log($"Generated mesh for Section_{i} (y={i * VoxelData.SectionHeight}-{(i + 1) * VoxelData.SectionHeight - 1})");
            }
            else
            {
                Debug.Log($"Skipped Section_{i} (empty)");
            }
        }
    }

}