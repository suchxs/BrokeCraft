using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Simple chunk pooling to avoid destroy/instantiate hitches when streaming.
/// </summary>
public class ChunkPool
{
    private readonly Stack<Chunk> pool = new Stack<Chunk>();
    private readonly GameObject chunkPrefab;
    private readonly Material chunkMaterial;
    private readonly Transform parent;

    public ChunkPool(GameObject prefab, Material material, Transform parentTransform)
    {
        chunkPrefab = prefab;
        chunkMaterial = material;
        parent = parentTransform;
    }

    public Chunk Get(int3 position, World world, int lodStep)
    {
        Chunk chunk;
        if (pool.Count > 0)
        {
            chunk = pool.Pop();
            chunk.gameObject.SetActive(true);
        }
        else
        {
            GameObject go = chunkPrefab != null
                ? Object.Instantiate(chunkPrefab, parent)
                : new GameObject("PooledChunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(Chunk));

            chunk = go.GetComponent<Chunk>();
        }

        chunk.Initialize(position, chunkMaterial, world, lodStep);
        return chunk;
    }

    public void Return(Chunk chunk)
    {
        if (chunk == null)
        {
            return;
        }

        chunk.gameObject.SetActive(false);
        pool.Push(chunk);
    }
}
