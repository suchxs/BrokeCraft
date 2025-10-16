using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkSection : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public MeshCollider meshCollider;

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
    
    // OPTIMIZATION: Delay collider generation (Minecraft-style!)
    private bool colliderNeedsUpdate = false;
    private Mesh cachedMesh = null;
    private int colliderBakeFrame = -1; // Frame when collider should bake

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
    // OPTIMIZED: Early exit on first non-air block found
    public bool IsEmpty()
    {
        // Check in optimal order (most likely to find blocks faster)
        for (int y = 0; y < VoxelData.SectionHeight; y++)
        {
            int chunkY = sectionYOffset + y;
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    // Early exit - return false immediately on first block found
                    if (parentChunk.voxelMap[x, chunkY, z] != 0)
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
    
    // THREADED MESH SYSTEM: Upload pre-generated mesh data to GPU (MAIN THREAD ONLY!)
    public void UploadMeshData(SectionMeshData meshData)
    {
        // Check if mesh data is empty
        if (meshData.vertices == null || meshData.vertices.Count == 0)
        {
            // Empty section - disable renderer and collider
            if (meshRenderer != null)
                meshRenderer.enabled = false;
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // Clear mesh reference!
                meshCollider.enabled = false;
            }
            return;
        }
        
        // Enable renderer
        if (meshRenderer != null)
            meshRenderer.enabled = true;
        
        // OPTIMIZATION: Get mesh from pool (reuse instead of allocate!)
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            mesh = ChunkPool.GetMesh();
            mesh.name = $"Section_Y{sectionYOffset}_Threaded";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16; // 16-bit indices for smaller meshes
            meshFilter.mesh = mesh;
        }
        else
        {
            mesh.Clear();
        }
        
        // Upload mesh data to GPU (Unity API - MUST be on main thread!)
        mesh.SetVertices(meshData.vertices);
        mesh.SetTriangles(meshData.triangles, 0);
        mesh.SetUVs(0, meshData.uvs);
        mesh.SetColors(meshData.colors);
        
        // OPTIMIZATION: Skip normals for flat shading (Minecraft style)
        // mesh.RecalculateNormals(); // Disabled for performance!
        
        // Schedule delayed collider baking
        cachedMesh = mesh;
        colliderBakeFrame = Time.frameCount + (gameObject.GetInstanceID() % 60) + 10;
        colliderNeedsUpdate = true;
    }

    void AddVoxelDataToSection(int x, int y, int z)
    {
        // Get block type from parent chunk
        byte blockID = parentChunk.GetVoxel(x, y, z);

        // Skip air blocks entirely
        if (blockID == 0) return;

        // OPTIMIZATION: Get biome color (simplified - no blending for performance!)
        int globalX = x + (parentChunk.coord.x * VoxelData.ChunkWidth);
        int globalZ = z + (parentChunk.coord.z * VoxelData.ChunkWidth);
        BiomeAttributes biome = world.GetBiome(globalX, globalZ);
        Color blockColor = biome != null ? biome.grassColor : Color.green; // Direct color (faster!)

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
        // OPTIMIZATION: If no vertices, disable renderer and collider entirely
        if (vertices.Count == 0)
        {
            if (meshFilter.mesh != null)
                meshFilter.mesh.Clear();
            
            // Disable components to save performance
            if (meshRenderer != null)
                meshRenderer.enabled = false;
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // Clear mesh reference!
                meshCollider.enabled = false;
            }
            
            return;
        }

        // Enable renderer if we have mesh data
        if (meshRenderer != null)
            meshRenderer.enabled = true;

        // OPTIMIZATION: Get mesh from pool (reuse instead of allocate!)
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            mesh = ChunkPool.GetMesh();
            mesh.name = $"Section_Y{sectionYOffset}";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16; // 16-bit indices
            meshFilter.mesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        // OPTIMIZATION: Use SetVertices/SetTriangles to avoid allocations
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);  // Set vertex colors for biome tinting

        // MINECRAFT OPTIMIZATION: Skip normals calculation for faster rendering
        // (Flat shading is fine for blocky voxels!)
        // mesh.RecalculateNormals();  // Disabled for HUGE performance boost!
        
        // MINECRAFT OPTIMIZATION: DELAY collider generation!
        cachedMesh = mesh;
        
        // Schedule collider baking for a future frame (staggered based on instance ID)
        colliderBakeFrame = Time.frameCount + (gameObject.GetInstanceID() % 60) + 10;
        colliderNeedsUpdate = true;
    }
    
    // OPTIMIZED: Bake colliders in a staggered manner (simple, no coroutines!)
    void Update()
    {
        if (colliderNeedsUpdate && Time.frameCount >= colliderBakeFrame && cachedMesh != null)
        {
            BakeCollider();
            colliderNeedsUpdate = false;
        }
    }
    
    // Bake collider mesh
    void BakeCollider()
    {
        if (meshCollider != null && cachedMesh != null)
        {
            meshCollider.sharedMesh = null;  // Clear old mesh first
            meshCollider.convex = false;     // MUST be false for terrain
            meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
                                         | MeshColliderCookingOptions.EnableMeshCleaning 
                                         | MeshColliderCookingOptions.WeldColocatedVertices;
            meshCollider.sharedMesh = cachedMesh;  // Assign new mesh
            meshCollider.enabled = true;
        }
    }
    
    // Return mesh to pool when section is destroyed/reused
    public void ReturnMeshToPool()
    {
        if (meshFilter != null && meshFilter.mesh != null)
        {
            ChunkPool.ReturnMesh(meshFilter.mesh);
            meshFilter.mesh = null;
        }
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

