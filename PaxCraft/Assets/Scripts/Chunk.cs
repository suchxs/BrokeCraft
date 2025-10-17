using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    // The full voxel map for entire chunk (16x16x256) = 65KB per chunk!
    // Public for neighbor chunk access
    // MEMORY NOTE: This is cleared after mesh generation to save memory
    public byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    // Array of chunk sections (16 sections of 16 height each)
    ChunkSection[] sections = new ChunkSection[VoxelData.SectionsPerChunk];

    // Chunk's position in the world grid
    public ChunkCoord coord;

    public World world; // Public for threading system access
    
    // Track if this chunk has been meshed (for neighbor updates)
    private bool isMeshed = false;

    // Initialize chunk with world reference (LEGACY: for synchronous generation)
    public void Initialize(World _world)
    {
        world = _world;
        PopulateVoxelMap(); // Only called for sync generation (spawn chunks)
    }
    
    // Initialize from pre-generated voxel data (THREADED)
    // voxelMap is already populated by background thread - skip PopulateVoxelMap()
    public void InitializeFromThreadData(World _world)
    {
        world = _world;
        // voxelMap already set by CreateChunkFromThreadData - skip generation!
    }
    
    // Mark chunk as meshed (called after mesh upload)
    public void MarkAsMeshed()
    {
        isMeshed = true;
    }
    
    // Return all section meshes to pool (for chunk pooling)
    public void ReturnMeshesToPool()
    {
        if (sections != null)
        {
            foreach (ChunkSection section in sections)
            {
                if (section != null)
                {
                    section.ReturnMeshToPool();
                }
            }
        }
    }

    // Generate mesh (called after all chunks are initialized)
    public void GenerateMesh(bool notifyNeighbors = true)
    {
        CreateSections();
        GenerateAllSections();
        isMeshed = true;
        
        // OPTIMIZATION FIX: Neighbor notifications now queued and rate-limited
        // This prevents cascading mesh regenerations that cause lag spikes!
        if (notifyNeighbors)
        {
            NotifyNeighborsToUpdate();
        }
    }
    
    // Regenerate mesh (for when neighbors load and we need to update edges)
    public void RegenerateMesh()
    {
        if (!isMeshed) return; // Don't regenerate if never meshed
        
        // Only regenerate if we have sections
        if (sections != null && sections.Length > 0)
        {
            GenerateAllSections();
        }
    }
    
    // Tell neighboring chunks to update their meshes (fixes edge faces)
    // OPTIMIZATION: Now queues updates instead of immediate regeneration
    void NotifyNeighborsToUpdate()
    {
        // Check all 4 horizontal neighbors
        ChunkCoord[] neighborCoords = new ChunkCoord[]
        {
            new ChunkCoord(coord.x + 1, coord.z),
            new ChunkCoord(coord.x - 1, coord.z),
            new ChunkCoord(coord.x, coord.z + 1),
            new ChunkCoord(coord.x, coord.z - 1)
        };
        
        foreach (ChunkCoord neighborCoord in neighborCoords)
        {
            Chunk neighbor = world.GetChunk(neighborCoord);
            if (neighbor != null && neighbor.isMeshed)
            {
                // CRITICAL FIX: Queue update instead of immediate regeneration!
                // This prevents cascading updates that cause lag spikes
                world.QueueChunkMeshUpdate(neighbor);
            }
        }
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

    // Get voxel from neighboring chunk (MINECRAFT-STYLE FACE CULLING)
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

        // MINECRAFT LOGIC: If neighbor exists AND is active, get its actual voxel
        if (neighborChunk != null && neighborChunk.gameObject.activeInHierarchy)
        {
            return neighborChunk.voxelMap[x, y, z];
        }

        // CRITICAL: Neighbor doesn't exist (not generated yet, cached, or world edge)
        // FIXED: Check ACTUAL terrain height at this position, not base height!
        
        // Calculate global position to check terrain height
        int globalX = (neighborChunkX * VoxelData.ChunkWidth) + x;
        int globalZ = (neighborChunkZ * VoxelData.ChunkWidth) + z;
        
        // Get actual terrain height at this exact position
        int actualTerrainHeight = world.GetTerrainHeightAt(globalX, globalZ);
        
        // If we're underground at this position, assume solid (hide face)
        if (y <= actualTerrainHeight)
        {
            return 2; // Stone (solid) - face won't render (MASSIVE optimization!)
        }
        
        // Above terrain: Assume air (render face)
        return 0;
    }

    void PopulateVoxelMap()
    {
        // SEBASTIAN LAGUE APPROACH: Generate heightmap first, then convert to voxels
        // This ensures consistent noise across the entire terrain
        
        // Cache biomes per column
        BiomeAttributes[,] biomeCache = new BiomeAttributes[VoxelData.ChunkWidth, VoxelData.ChunkWidth];
        int[,] heightCache = new int[VoxelData.ChunkWidth, VoxelData.ChunkWidth];
        
        // Pre-calculate heights for entire chunk
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                int globalX = x + (coord.x * VoxelData.ChunkWidth);
                int globalZ = z + (coord.z * VoxelData.ChunkWidth);
                
                // Get biome (for block types only)
                biomeCache[x, z] = world.GetBiome(globalX, globalZ);
                
                // Generate height using Sebastian Lague's approach
                heightCache[x, z] = GetTerrainHeight(globalX, globalZ);
            }
        }
        
        // Generate terrain using cached values
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                BiomeAttributes biome = biomeCache[x, z];
                int terrainHeight = heightCache[x, z];

                // Fill column from bottom to terrain height
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    if (y == 0)
                    {
                        voxelMap[x, y, z] = 1; // Bedrock
                    }
                    else if (y <= terrainHeight - world.surfaceLayer)
                    {
                        voxelMap[x, y, z] = 2; // Stone
                    }
                    else if (y < terrainHeight)
                    {
                        voxelMap[x, y, z] = (byte)(biome != null ? biome.subSurfaceBlock : 4);
                    }
                    else if (y == terrainHeight)
                    {
                        voxelMap[x, y, z] = (byte)(biome != null ? biome.surfaceBlock : 3);
                    }
                    else
                    {
                        voxelMap[x, y, z] = 0; // Air
                    }
                }
            }
        }
    }

    // SEBASTIAN LAGUE'S APPROACH: Consistent noise, biomes only affect interpretation
    int GetTerrainHeight(int x, int z)
    {
        // Step 1: Generate ONE consistent noise value for this position
        // This noise is the SAME across the entire world - no frequency changes!
        float baseNoise = world.GenerateNoise(x, z);
        
        // Step 2: Get blended biome HEIGHT MULTIPLIERS (not noise parameters!)
        float blendedHeightMultiplier = GetBlendedHeightMultiplier(x, z);
        
        // Step 3: Apply height curve to noise (THREAD-SAFE version!)
        float curvedHeight = World.EvaluateHeightCurve(baseNoise);
        
        // Step 4: Apply biome height multiplier to the curved noise
        // This is where biomes affect the terrain - they scale the HEIGHT, not the noise!
        float finalHeight = curvedHeight * blendedHeightMultiplier;
        
        // Step 5: Convert to block height
        int height = Mathf.FloorToInt(world.baseTerrainHeight + (finalHeight * world.maxHeightVariation));
        height = Mathf.Clamp(height, 1, VoxelData.ChunkHeight - 1);
        
        return height;
    }
    
    // Get blended height multiplier from nearby biomes
    // This is the ONLY thing biomes affect - they don't change the noise itself!
    float GetBlendedHeightMultiplier(int x, int z)
    {
        float totalMultiplier = 0f;
        float totalWeight = 0f;
        
        int blendRadius = world.biomeBlendRadius;
        
        // Sample biomes in a radius
        for (int offsetX = -blendRadius; offsetX <= blendRadius; offsetX++)
        {
            for (int offsetZ = -blendRadius; offsetZ <= blendRadius; offsetZ++)
            {
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                
                if (distance > blendRadius)
                    continue;
                
                // Smooth falloff (inverse square with smoothstep)
                float normalizedDist = distance / blendRadius;
                float weight = 1f - SmoothStep(0f, 1f, normalizedDist);
                weight = weight * weight; // Square for extra smoothness
                
                // Get biome height weight at this position
                BiomeAttributes biome = world.GetBiome(x + offsetX, z + offsetZ);
                if (biome != null)
                {
                    // Biomes only provide HEIGHT MULTIPLIERS (0-1 range)
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
    
    // Smooth interpolation function (like GLSL smoothstep)
    float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
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
            section.meshCollider = sectionObject.AddComponent<MeshCollider>();
            
            // Set material from world
            section.meshRenderer.material = world.material;

            // Initialize the section
            section.Initialize(this, world, yOffset);

            sections[i] = section;
        }
    }
    
    // THREADED MESH SYSTEM: Create sections from pre-generated mesh data
    public void CreateSectionsFromMeshData(SectionMeshData[] meshDataArray, BiomeAttributes[,] biomeMap)
    {
        // Create section GameObjects
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
            section.meshCollider = sectionObject.AddComponent<MeshCollider>();
            
            // Set material from world
            section.meshRenderer.material = world.material;

            // Initialize the section
            section.Initialize(this, world, yOffset);
            
            // Upload pre-generated mesh data to GPU (MAIN THREAD only - Unity API!)
            section.UploadMeshData(meshDataArray[i]);

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
        
        // Reduced logging - only log if debugging needed
        // Debug.Log($"Chunk ({coord.x},{coord.z}): Generated {generatedSections}/{sections.Length} sections");
    }

}