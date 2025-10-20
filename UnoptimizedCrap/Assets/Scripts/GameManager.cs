using System.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Coordinates world readiness and spawns the player once terrain data is prepared.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnWorldX = 0;
    [SerializeField] private int spawnWorldZ = 0;
    [SerializeField] private bool centerOnBlock = true;
    [SerializeField] private float readinessTimeoutSeconds = 12f;

    private PlayerController playerInstance;
    private static readonly int3 ChunkDimensions = new int3(VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkDepth);

    private IEnumerator Start()
    {
        if (world == null)
        {
            Debug.LogError("GameManager requires a World reference.");
            yield break;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("GameManager requires a player prefab reference.");
            yield break;
        }

        // Allow World.Start() to execute before we poll its data.
        yield return null;

        float timer = readinessTimeoutSeconds;
        while (!IsSpawnAreaReady())
        {
            if (timer <= 0f)
            {
                Debug.LogWarning("Spawn area did not become ready before timeout. Spawning player anyway.");
                break;
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        SpawnPlayer();
    }

    private bool IsSpawnAreaReady()
    {
        int3 baseBlock = new int3(spawnWorldX, VoxelData.SeaLevel, spawnWorldZ);
        int3 baseChunk = new int3(
            (int)math.floor(baseBlock.x / (float)ChunkDimensions.x),
            (int)math.floor(baseBlock.y / (float)ChunkDimensions.y),
            (int)math.floor(baseBlock.z / (float)ChunkDimensions.z)
        );

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    int3 chunkPos = baseChunk + new int3(x, y, z);
                    Chunk chunk = world.GetChunkAt(chunkPos);
                    bool requiresMeshReady = chunkPos.Equals(baseChunk);
                    if (chunk == null || !chunk.IsTerrainDataReady || (requiresMeshReady && !chunk.IsMeshReady))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void SpawnPlayer()
    {
        Vector3 spawnPosition = world.GetSpawnPosition(spawnWorldX, spawnWorldZ);
        if (centerOnBlock)
        {
            spawnPosition.x += 0.5f;
            spawnPosition.z += 0.5f;
        }

        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        playerInstance = playerObject.GetComponent<PlayerController>();
        if (playerInstance != null)
        {
            playerInstance.Initialize(world);
            world.RegisterViewer(playerObject.transform);
        }
        else
        {
            Debug.LogWarning("Player prefab is missing the PlayerController component.");
        }
    }
}
