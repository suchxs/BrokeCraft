using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// MINECRAFT-STYLE MULTITHREADING SYSTEM
// This class handles all background thread work for chunk generation
// Only mesh upload to GPU happens on main thread - everything else is threaded!

public class ChunkThreading
{
    // Thread-safe queue for chunk generation requests
    private static Queue<ChunkDataRequest> dataRequestQueue = new Queue<ChunkDataRequest>();
    private static Queue<MeshDataRequest> meshRequestQueue = new Queue<MeshDataRequest>();
    
    // Completed data ready for main thread
    private static Queue<ChunkDataResult> completedDataResults = new Queue<ChunkDataResult>();
    private static Queue<MeshDataResult> completedMeshResults = new Queue<MeshDataResult>();
    
    // Thread synchronization
    private static object dataLock = new object();
    private static object meshLock = new object();
    private static object dataResultLock = new object();
    private static object meshResultLock = new object();
    
    // Worker threads
    private static Thread[] dataThreads;
    private static Thread[] meshThreads;
    private static bool isRunning = false;
    
    // Thread count (Minecraft uses CPU core count - 2)
    private static int dataThreadCount = Mathf.Max(1, SystemInfo.processorCount - 2);
    private static int meshThreadCount = Mathf.Max(1, SystemInfo.processorCount - 2);
    
    // Static reference to block types for texture lookups (thread-safe read-only access)
    private static BlockType[] blockTypes;
    
    // Initialize threading system
    public static void Initialize(BlockType[] worldBlockTypes = null)
    {
        if (isRunning) return;
        
        isRunning = true;
        
        // Store block types for texture lookups (thread-safe read-only)
        blockTypes = worldBlockTypes;
        
        // Create data generation threads
        dataThreads = new Thread[dataThreadCount];
        for (int i = 0; i < dataThreadCount; i++)
        {
            dataThreads[i] = new Thread(DataThreadWorker);
            dataThreads[i].IsBackground = true;
            dataThreads[i].Priority = System.Threading.ThreadPriority.BelowNormal;
            dataThreads[i].Start();
        }
        
        // Create mesh generation threads
        meshThreads = new Thread[meshThreadCount];
        for (int i = 0; i < meshThreadCount; i++)
        {
            meshThreads[i] = new Thread(MeshThreadWorker);
            meshThreads[i].IsBackground = true;
            meshThreads[i].Priority = System.Threading.ThreadPriority.BelowNormal;
            meshThreads[i].Start();
        }
        
        Debug.Log($"[ChunkThreading] Initialized with {dataThreadCount} data threads and {meshThreadCount} mesh threads");
    }
    
    // Shutdown threading system
    public static void Shutdown()
    {
        isRunning = false;
        
        // Wait for threads to finish (with timeout)
        if (dataThreads != null)
        {
            foreach (var thread in dataThreads)
            {
                if (thread != null && thread.IsAlive)
                    thread.Join(100);
            }
        }
        
        if (meshThreads != null)
        {
            foreach (var thread in meshThreads)
            {
                if (thread != null && thread.IsAlive)
                    thread.Join(100);
            }
        }
    }
    
    // Request chunk data generation (voxel map) on background thread
    public static void RequestChunkData(ChunkCoord coord, World world, Action<ChunkDataResult> callback)
    {
        lock (dataLock)
        {
            dataRequestQueue.Enqueue(new ChunkDataRequest(coord, world, callback));
        }
    }
    
    // Request mesh generation on background thread
    public static void RequestMeshData(ChunkDataResult chunkData, Action<MeshDataResult> callback)
    {
        lock (meshLock)
        {
            meshRequestQueue.Enqueue(new MeshDataRequest(chunkData, callback));
        }
    }
    
    // Main thread: Process completed results
    public static void ProcessCompletedData()
    {
        // Process chunk data results
        lock (dataResultLock)
        {
            while (completedDataResults.Count > 0)
            {
                ChunkDataResult result = completedDataResults.Dequeue();
                result.callback?.Invoke(result);
            }
        }
        
        // Process mesh data results
        lock (meshResultLock)
        {
            while (completedMeshResults.Count > 0)
            {
                MeshDataResult result = completedMeshResults.Dequeue();
                result.callback?.Invoke(result);
            }
        }
    }
    
    // WORKER THREAD: Generate chunk voxel data
    private static void DataThreadWorker()
    {
        while (isRunning)
        {
            ChunkDataRequest request = null;
            
            lock (dataLock)
            {
                if (dataRequestQueue.Count > 0)
                {
                    request = dataRequestQueue.Dequeue();
                }
            }
            
            if (request != null)
            {
                // EXPENSIVE WORK HAPPENS HERE (on background thread!)
                ChunkDataResult result = GenerateChunkData(request);
                
                lock (dataResultLock)
                {
                    completedDataResults.Enqueue(result);
                }
            }
            else
            {
                Thread.Sleep(1); // Avoid busy-waiting
            }
        }
    }
    
    // WORKER THREAD: Generate mesh data
    private static void MeshThreadWorker()
    {
        while (isRunning)
        {
            MeshDataRequest request = null;
            
            lock (meshLock)
            {
                if (meshRequestQueue.Count > 0)
                {
                    request = meshRequestQueue.Dequeue();
                }
            }
            
            if (request != null)
            {
                // EXPENSIVE WORK HAPPENS HERE (on background thread!)
                MeshDataResult result = GenerateMeshData(request);
                
                lock (meshResultLock)
                {
                    completedMeshResults.Enqueue(result);
                }
            }
            else
            {
                Thread.Sleep(1); // Avoid busy-waiting
            }
        }
    }
    
    // Generate voxel map data (BACKGROUND THREAD)
    private static ChunkDataResult GenerateChunkData(ChunkDataRequest request)
    {
        ChunkDataResult result = new ChunkDataResult();
        result.coord = request.coord;
        result.callback = request.callback;
        result.world = request.world;  // Store World reference for mesh generation
        result.voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
        
        // SEBASTIAN LAGUE APPROACH: Generate heightmap first
        int[,] heightMap = new int[VoxelData.ChunkWidth, VoxelData.ChunkWidth];
        BiomeAttributes[,] biomeMap = new BiomeAttributes[VoxelData.ChunkWidth, VoxelData.ChunkWidth];
        
        // Cache biomes and heights
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                int worldX = request.coord.x * VoxelData.ChunkWidth + x;
                int worldZ = request.coord.z * VoxelData.ChunkWidth + z;
                
                // Thread-safe world access (only read operations!)
                biomeMap[x, z] = request.world.GetBiome(worldX, worldZ);
                heightMap[x, z] = request.world.GetTerrainHeightAt(worldX, worldZ);
            }
        }
        
        // Fill voxel map based on height (MATCHES Chunk.cs EXACTLY)
        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                int terrainHeight = heightMap[x, z];
                BiomeAttributes biome = biomeMap[x, z];
                
                // Fill column from bottom to terrain height
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    if (y == 0)
                    {
                        result.voxelMap[x, y, z] = 1; // Bedrock
                    }
                    else if (y <= terrainHeight - request.world.surfaceLayer)
                    {
                        result.voxelMap[x, y, z] = 2; // Stone
                    }
                    else if (y < terrainHeight)
                    {
                        result.voxelMap[x, y, z] = (byte)(biome != null ? biome.subSurfaceBlock : 4); // Dirt/subsurface
                    }
                    else if (y == terrainHeight)
                    {
                        result.voxelMap[x, y, z] = (byte)(biome != null ? biome.surfaceBlock : 3); // Grass/surface
                    }
                    else
                    {
                        result.voxelMap[x, y, z] = 0; // Air
                    }
                }
            }
        }
        
        result.biomeMap = biomeMap;
        return result;
    }
    
    // Generate mesh vertex/triangle data (BACKGROUND THREAD)
    private static MeshDataResult GenerateMeshData(MeshDataRequest request)
    {
        MeshDataResult result = new MeshDataResult();
        result.coord = request.chunkData.coord;
        result.callback = request.callback;
        result.chunkData = request.chunkData;  // Store reference to chunk data
        result.biomeMap = request.chunkData.biomeMap;  // Store biome map
        result.sectionMeshData = new SectionMeshData[VoxelData.SectionsPerChunk];
        
        // Generate mesh for each section
        for (int sectionIndex = 0; sectionIndex < VoxelData.SectionsPerChunk; sectionIndex++)
        {
            result.sectionMeshData[sectionIndex] = GenerateSectionMesh(
                request.chunkData.voxelMap,
                request.chunkData.biomeMap,
                sectionIndex,
                request.chunkData.coord,
                request.chunkData.world
            );
        }
        
        return result;
    }
    
    // Generate mesh data for a single section (BACKGROUND THREAD)
    private static SectionMeshData GenerateSectionMesh(byte[,,] voxelMap, BiomeAttributes[,] biomeMap, int sectionIndex, ChunkCoord chunkCoord, World world)
    {
        SectionMeshData meshData = new SectionMeshData();
        meshData.vertices = new List<Vector3>();
        meshData.triangles = new List<int>();
        meshData.uvs = new List<Vector2>();
        meshData.colors = new List<Color>();
        
        int yOffset = sectionIndex * VoxelData.SectionHeight;
        int vertexIndex = 0;
        
        // Check if section is completely empty (optimization)
        bool isEmpty = true;
        for (int y = 0; y < VoxelData.SectionHeight && isEmpty; y++)
        {
            int worldY = y + yOffset;
            if (worldY >= VoxelData.ChunkHeight) continue;
            
            for (int x = 0; x < VoxelData.ChunkWidth && isEmpty; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth && isEmpty; z++)
                {
                    if (voxelMap[x, worldY, z] != 0)
                    {
                        isEmpty = false;
                    }
                }
            }
        }
        
        // Skip empty sections
        if (isEmpty)
        {
            return meshData;
        }
        
        // Generate mesh for non-empty sections
        for (int y = 0; y < VoxelData.SectionHeight; y++)
        {
            int worldY = y + yOffset;
            if (worldY >= VoxelData.ChunkHeight) continue;
            
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    byte blockID = voxelMap[x, worldY, z];
                    if (blockID == 0) continue; // Air
                    
                    BiomeAttributes biome = biomeMap[x, z];
                    
                    // Check each face
                    for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                    {
                        // Check neighbor voxel
                        int checkX = x + (int)VoxelData.faceChecks[faceIndex].x;
                        int checkY = worldY + (int)VoxelData.faceChecks[faceIndex].y;
                        int checkZ = z + (int)VoxelData.faceChecks[faceIndex].z;
                        
                        // Determine if face should render (MINECRAFT-STYLE FACE CULLING)
                        bool shouldRenderFace = CheckShouldRenderFace(
                            voxelMap, 
                            x, worldY, z, 
                            checkX, checkY, checkZ,
                            chunkCoord,
                            world
                        );
                        
                        if (shouldRenderFace)
                        {
                            // Determine vertex color (biome tinting for grass block top face)
                            bool isGrassBlock = blockID == 3;  // Grass block ID
                            bool isTopFace = faceIndex == 2;  // Face index 2 = top face
                            bool shouldTint = isGrassBlock && isTopFace && (biome != null && biome.useGrassColoring);
                            Color vertexColor = shouldTint ? biome.grassColor : Color.white;
                            
                            // Add vertices (local Y relative to section)
                            Vector3 blockPos = new Vector3(x, y, z);
                            for (int i = 0; i < 4; i++)
                            {
                                Vector3 vert = blockPos + VoxelData.voxelVerts[VoxelData.voxelTris[faceIndex, i]];
                                meshData.vertices.Add(vert);
                                meshData.colors.Add(vertexColor);
                            }
                            
                            // Add UVs from texture atlas
                            AddTextureUVs(meshData.uvs, blockID, faceIndex);
                            
                            // Add triangles
                            meshData.triangles.Add(vertexIndex);
                            meshData.triangles.Add(vertexIndex + 1);
                            meshData.triangles.Add(vertexIndex + 2);
                            meshData.triangles.Add(vertexIndex + 2);
                            meshData.triangles.Add(vertexIndex + 1);
                            meshData.triangles.Add(vertexIndex + 3);
                            
                            vertexIndex += 4;
                        }
                    }
                }
            }
        }
        
        return meshData;
    }
    
    // Check if a face should render (MINECRAFT-STYLE FACE CULLING)
    // Handles both internal and edge faces properly
    private static bool CheckShouldRenderFace(
        byte[,,] voxelMap,
        int x, int y, int z,
        int checkX, int checkY, int checkZ,
        ChunkCoord chunkCoord,
        World world)
    {
        // Check if neighbor position is within chunk bounds
        if (checkX >= 0 && checkX < VoxelData.ChunkWidth &&
            checkY >= 0 && checkY < VoxelData.ChunkHeight &&
            checkZ >= 0 && checkZ < VoxelData.ChunkWidth)
        {
            // Internal face - check neighbor voxel in same chunk
            byte neighborBlock = voxelMap[checkX, checkY, checkZ];
            
            // Render face only if neighbor is air (transparent)
            if (blockTypes != null && neighborBlock < blockTypes.Length)
            {
                return !blockTypes[neighborBlock].isSolid;
            }
            return neighborBlock == 0; // Air
        }
        
        // EDGE FACE - neighbor is in different chunk or out of bounds
        // Use terrain height to determine if face should render (CRITICAL OPTIMIZATION!)
        
        // Calculate global world position of the neighboring block
        int globalX = (chunkCoord.x * VoxelData.ChunkWidth) + x;
        int globalZ = (chunkCoord.z * VoxelData.ChunkWidth) + z;
        
        // Adjust for the face direction
        int neighborGlobalX = globalX + (checkX - x);
        int neighborGlobalZ = globalZ + (checkZ - z);
        int neighborGlobalY = checkY;
        
        // Handle Y out of bounds
        if (neighborGlobalY < 0)
        {
            // Below world - assume solid (bedrock)
            return false; // Don't render face
        }
        if (neighborGlobalY >= VoxelData.ChunkHeight)
        {
            // Above world - assume air
            return true; // Render face
        }
        
        // Get actual terrain height at neighboring position (thread-safe!)
        int terrainHeight = world.GetTerrainHeightAt(neighborGlobalX, neighborGlobalZ);
        
        // If neighbor position is underground, assume solid (hide face)
        if (neighborGlobalY <= terrainHeight)
        {
            return false; // Don't render - neighbor is solid ground
        }
        
        // Above terrain - assume air (render face)
        return true;
    }
    
    // Add texture UVs for a face (BACKGROUND THREAD - texture atlas lookup)
    private static void AddTextureUVs(List<Vector2> uvs, byte blockID, int faceIndex)
    {
        // Safety check
        if (blockTypes == null || blockID >= blockTypes.Length)
        {
            // Fallback to simple UVs
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 1));
            return;
        }
        
        // Get texture ID from block type
        int textureID = blockTypes[blockID].GetTextureID(faceIndex);
        
        // Calculate column and row in atlas
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

// Request structures
public class ChunkDataRequest
{
    public ChunkCoord coord;
    public World world;
    public Action<ChunkDataResult> callback;
    
    public ChunkDataRequest(ChunkCoord c, World w, Action<ChunkDataResult> cb)
    {
        coord = c;
        world = w;
        callback = cb;
    }
}

public class MeshDataRequest
{
    public ChunkDataResult chunkData;
    public Action<MeshDataResult> callback;
    
    public MeshDataRequest(ChunkDataResult data, Action<MeshDataResult> cb)
    {
        chunkData = data;
        callback = cb;
    }
}

// Result structures
public class ChunkDataResult
{
    public ChunkCoord coord;
    public byte[,,] voxelMap;
    public BiomeAttributes[,] biomeMap;
    public World world;  // Reference to World for terrain height checks
    public Action<ChunkDataResult> callback;
}

public class MeshDataResult
{
    public ChunkCoord coord;
    public SectionMeshData[] sectionMeshData;
    public BiomeAttributes[,] biomeMap;
    public ChunkDataResult chunkData;  // Reference to original chunk data (for voxelMap access)
    public Action<MeshDataResult> callback;
}

public class SectionMeshData
{
    public List<Vector3> vertices;
    public List<int> triangles;
    public List<Vector2> uvs;
    public List<Color> colors;
}

