using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkSection : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    int vertexIndex = 0;
    List<Vector3> vertices;
    List<int> triangles;
    List<Vector2> uvs;
    List<Color> colors;  // For biome-based grass coloring

    // Reference to parent chunk for accessing full voxel map
    Chunk parentChunk;
    World world;

    // This section's Y offset in the chunk (0, 16, 32, 48, etc.)
    public int sectionYOffset;

    public void Initialize(Chunk _parentChunk, World _world, int _yOffset)
    {
        parentChunk = _parentChunk;
        world = _world;
        sectionYOffset = _yOffset;

        // Pre-allocate lists for 16x16x16 section (worst case: all blocks visible)
        int maxVerts = VoxelData.ChunkWidth * VoxelData.SectionHeight * VoxelData.ChunkWidth * 24;
        vertices = new List<Vector3>(maxVerts);
        triangles = new List<int>(maxVerts * 36 / 24);
        uvs = new List<Vector2>(maxVerts);
        colors = new List<Color>(maxVerts);
    }

    // Check if this section is completely empty (all air blocks)
    public bool IsEmpty()
    {
        for (int y = 0; y < VoxelData.SectionHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    // Check actual Y position in chunk
                    byte blockID = parentChunk.GetVoxel(x, sectionYOffset + y, z);
                    if (blockID != 0) // Not air
                        return false;
                }
            }
        }
        return true;
    }

    public void GenerateMesh()
    {
        // Clear lists
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();
        vertexIndex = 0;

        // Only generate mesh for blocks in this section
        for (int y = 0; y < VoxelData.SectionHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    // Calculate actual Y position in chunk
                    int chunkY = sectionYOffset + y;
                    AddVoxelDataToSection(x, chunkY, z);
                }
            }
        }

        CreateMesh();
    }

    void AddVoxelDataToSection(int x, int y, int z)
    {
        // Get block type from parent chunk
        byte blockID = parentChunk.GetVoxel(x, y, z);

        // Skip air blocks entirely
        if (blockID == 0) return;

        // Get biome and blended grass color for this block's global position
        int globalX = x + (parentChunk.coord.x * VoxelData.ChunkWidth);
        int globalZ = z + (parentChunk.coord.z * VoxelData.ChunkWidth);
        BiomeAttributes biome = world.GetBiome(globalX, globalZ);
        Color blockColor = world.GetBlendedGrassColor(globalX, globalZ);  // Use blended color!

        // Local position within this section (for mesh vertices)
        // y needs to be relative to section, not chunk
        int localY = y - sectionYOffset;
        Vector3 blockPos = new Vector3(x, localY, z);

        for (int p = 0; p < 6; p++)
        {
            // Check if face should be rendered (check in chunk coordinates)
            if (!CheckVoxel(x, y, z, p))
            {
                // Add the 4 vertices for this face
                vertices.Add(blockPos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]]);
                vertices.Add(blockPos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]]);
                vertices.Add(blockPos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]]);
                vertices.Add(blockPos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);

                // Add vertex colors (biome color for grass block TOP FACE only)
                bool isGrassBlock = blockID == 3;  // Grass block ID
                bool isTopFace = p == 2;  // Face index 2 = top face
                bool shouldTint = isGrassBlock && isTopFace && (biome != null && biome.useGrassColoring);
                Color vertexColor = shouldTint ? blockColor : Color.white;
                
                colors.Add(vertexColor);
                colors.Add(vertexColor);
                colors.Add(vertexColor);
                colors.Add(vertexColor);

                AddTexture(world.blocktypes[blockID].GetTextureID(p));

                // Add triangles
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
                vertexIndex += 4;
            }
        }
    }

    // Check adjacent voxel in chunk coordinates (supports cross-chunk checks)
    bool CheckVoxel(int x, int y, int z, int faceIndex)
    {
        // Add face offset direction
        x += (int)VoxelData.faceChecks[faceIndex].x;
        y += (int)VoxelData.faceChecks[faceIndex].y;
        z += (int)VoxelData.faceChecks[faceIndex].z;

        // Get voxel from parent chunk (which handles cross-chunk lookups)
        // If out of bounds, Chunk.GetVoxel() will check neighboring chunks
        byte blockID = parentChunk.GetVoxel(x, y, z);
        return world.blocktypes[blockID].isSolid;
    }

    void CreateMesh()
    {
        // Don't create mesh if empty
        if (vertices.Count == 0)
        {
            if (meshFilter.mesh != null)
                meshFilter.mesh.Clear();
            return;
        }

        // Reuse existing mesh if available
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"Section_Y{sectionYOffset}";
            meshFilter.mesh = mesh;
        }

        mesh.Clear();

        // Use SetVertices/SetTriangles to avoid allocations
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);  // Set vertex colors for biome tinting

        mesh.RecalculateNormals();
    }

    void AddTexture(int textureID)
    {
        // Calculate column and row in atlas (supports non-square atlases)
        int col = textureID % VoxelData.TextureAtlasWidth;
        int row = textureID / VoxelData.TextureAtlasWidth;

        // Calculate normalized UV coordinates
        float x = col * VoxelData.NormalizedBlockTextureWidth;
        float y = row * VoxelData.NormalizedBlockTextureHeight;

        // Flip Y (Unity UV origin is bottom-left, texture atlas is top-left)
        y = 1f - y - VoxelData.NormalizedBlockTextureHeight;

        // Add 4 UV coordinates for this face (quad)
        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + VoxelData.NormalizedBlockTextureHeight));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureWidth, y));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureWidth, y + VoxelData.NormalizedBlockTextureHeight));
    }
}

