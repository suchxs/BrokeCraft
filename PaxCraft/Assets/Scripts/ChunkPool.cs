using System.Collections.Generic;
using UnityEngine;

// MINECRAFT-STYLE CHUNK POOLING
// Reuses GameObject/Mesh instances to eliminate GC pressure and allocation lag spikes
public class ChunkPool : MonoBehaviour
{
    // Pooled chunk GameObjects (deactivated, ready for reuse)
    private static Stack<GameObject> chunkObjectPool = new Stack<GameObject>();
    private static Stack<Mesh> meshPool = new Stack<Mesh>();
    
    // Pool settings - MEMORY FIX: Reduced to prevent memory bloat
    private const int INITIAL_POOL_SIZE = 20;  // Reduced from 50
    private const int MAX_POOL_SIZE = 50;      // Reduced from 200 - prevents pool accumulation
    
    // Reference to World for setup
    private World world;
    
    public void Initialize(World worldRef)
    {
        world = worldRef;
        
        // Pre-warm pool with initial chunks
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            // Create mesh for pool
            Mesh mesh = new Mesh();
            mesh.name = $"PooledMesh_{i}";
            meshPool.Push(mesh);
        }
        
        Debug.Log($"[ChunkPool] ✓ Initialized with {INITIAL_POOL_SIZE} pre-allocated meshes");
    }
    
    // Get a chunk GameObject from pool (or create new if empty)
    public static GameObject GetChunkObject(ChunkCoord coord, Vector3 position)
    {
        GameObject chunkObject;
        
        if (chunkObjectPool.Count > 0)
        {
            // Reuse from pool
            chunkObject = chunkObjectPool.Pop();
            chunkObject.name = $"Chunk_{coord.x}_{coord.z}";
            chunkObject.transform.position = position;
            
            // CRITICAL FIX: Clean up old sections with stale collider data before reactivating!
            CleanupOldSections(chunkObject);
            
            chunkObject.SetActive(true);
        }
        else
        {
            // Create new (pool exhausted)
            chunkObject = new GameObject($"Chunk_{coord.x}_{coord.z}");
            chunkObject.transform.position = position;
        }
        
        return chunkObject;
    }
    
    // CRITICAL FIX: Clean up old section components to prevent collider errors
    private static void CleanupOldSections(GameObject chunkObject)
    {
        // Get all old section children
        ChunkSection[] oldSections = chunkObject.GetComponentsInChildren<ChunkSection>(true);
        
        foreach (ChunkSection section in oldSections)
        {
            // Return mesh to pool and clear collider
            section.ReturnMeshToPool();
            
            // Destroy the old section GameObject
            if (section.gameObject != chunkObject)
            {
                GameObject.Destroy(section.gameObject);
            }
        }
    }
    
    // Return chunk GameObject to pool
    public static void ReturnChunkObject(GameObject chunkObject)
    {
        if (chunkObjectPool.Count < MAX_POOL_SIZE)
        {
            // Clean up components before pooling
            Chunk chunk = chunkObject.GetComponent<Chunk>();
            if (chunk != null)
            {
                // Clean references
                chunk.world = null;
            }
            
            chunkObject.SetActive(false);
            chunkObjectPool.Push(chunkObject);
        }
        else
        {
            // Pool full - destroy
            Destroy(chunkObject);
        }
    }
    
    // Get a mesh from pool (or create new if empty)
    public static Mesh GetMesh()
    {
        if (meshPool.Count > 0)
        {
            Mesh mesh = meshPool.Pop();
            mesh.Clear(); // Clear old data
            return mesh;
        }
        else
        {
            // Create new mesh
            return new Mesh();
        }
    }
    
    // Return mesh to pool
    public static void ReturnMesh(Mesh mesh)
    {
        if (mesh != null && meshPool.Count < MAX_POOL_SIZE)
        {
            mesh.Clear();
            meshPool.Push(mesh);
        }
    }
    
    // Get pool statistics
    public static string GetPoolStats()
    {
        return $"Chunk Pool: {chunkObjectPool.Count} | Mesh Pool: {meshPool.Count}";
    }
}

