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
    private float sampledSpawnHeight;
    private static readonly int3 ChunkDimensions = new int3(VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkDepth);

    private IEnumerator Start()
    {
        AppSettings.Load();
        StartMusicLoop();

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

        float timeoutRemaining = readinessTimeoutSeconds;
        bool useTimeout = readinessTimeoutSeconds > 0f;

        // Wait for world prewarm to finish
        while (!world.IsPrewarmComplete)
        {
            if (useTimeout && HasTimedOut(ref timeoutRemaining, "world prewarm"))
            {
                yield break;
            }
            yield return null;
        }

        sampledSpawnHeight = SampleHeight(spawnWorldX, spawnWorldZ);
        int3 spawnChunk = new int3(
            (int)math.floor(spawnWorldX / (float)ChunkDimensions.x),
            (int)math.floor(sampledSpawnHeight / (float)ChunkDimensions.y),
            (int)math.floor(spawnWorldZ / (float)ChunkDimensions.z)
        );
        world.LoadChunksAroundPosition(spawnChunk);

        // Wait for spawn area readiness with no early exit
        while (!IsSpawnAreaReady() || (world != null && !world.IsPrewarmComplete))
        {
            if (useTimeout && HasTimedOut(ref timeoutRemaining, "spawn area readiness"))
            {
                yield break;
            }
            yield return null;
        }

        SpawnPlayer();
    }

    private bool IsSpawnAreaReady()
    {
        int3 baseBlock = new int3(spawnWorldX, (int)sampledSpawnHeight, spawnWorldZ);
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
        float spawnY = sampledSpawnHeight + VoxelData.PlayerHeight + 0.5f;
        Vector3 spawnPosition = new Vector3(spawnWorldX, spawnY, spawnWorldZ);
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

    private float SampleHeight(int worldX, int worldZ)
    {
        TerrainGenerationSettings settings = world.GetTerrainGenerationSettings();
        TerrainNoiseSettings noise = settings.ToNoiseSettings();
        BiomeNoiseSettings biome = settings.ToBiomeNoiseSettings();

        BiomeWeights weights = SampleBiomeWeights(worldX, worldZ, biome);
        TerrainNoiseSettings adjusted = AdjustNoiseForBiome(noise, biome, weights);
        TerrainHeightSample sample = TerrainNoise.SampleHeight(new float2(worldX, worldZ), adjusted);
        return sample.Height;
    }

    private static BiomeWeights SampleBiomeWeights(int worldX, int worldZ, BiomeNoiseSettings biomeSettings)
    {
        float2 coords = new float2(worldX, worldZ);
        float2 scaled = (coords + biomeSettings.biomeOffset) / math.max(1f, biomeSettings.biomeScale);
        float biomeNoise = noise.snoise(scaled) * 0.5f + 0.5f;

        float desertW = BiomeWeight(biomeNoise, 0.15f, 0.35f);
        float plainsW = BiomeWeight(biomeNoise, 0.5f, 0.3f);
        float mountainW = BiomeWeight(biomeNoise, 0.85f, 0.35f);

        float weightSum = math.max(0.0001f, desertW + plainsW + mountainW);
        float3 normalized = new float3(desertW, plainsW, mountainW) / weightSum;

        BiomeId dominant = BiomeId.Plains;
        float maxW = normalized.y;
        if (normalized.x > maxW)
        {
            maxW = normalized.x;
            dominant = BiomeId.Desert;
        }
        if (normalized.z > maxW)
        {
            dominant = BiomeId.Mountains;
        }

        return new BiomeWeights
        {
            Desert = normalized.x,
            Plains = normalized.y,
            Mountains = normalized.z,
            Dominant = dominant
        };
    }

    private static float BiomeWeight(float value, float center, float radius)
    {
        float dist = math.abs(value - center);
        return math.saturate(1f - dist / math.max(radius, 0.0001f));
    }

    private static TerrainNoiseSettings AdjustNoiseForBiome(TerrainNoiseSettings baseSettings, BiomeNoiseSettings biome, BiomeWeights weights)
    {
        float3 heightMultipliers = new float3(biome.desertHeightMultiplier, biome.plainsHeightMultiplier, biome.mountainHeightMultiplier);
        float3 baseOffsets = new float3(biome.desertBaseOffset, biome.plainsBaseOffset, biome.mountainBaseOffset);
        float3 ridge = new float3(biome.desertRidgeStrength, biome.plainsRidgeStrength, biome.mountainRidgeStrength);
        float3 redistribution = new float3(biome.desertRedistribution, biome.plainsRedistribution, biome.mountainRedistribution);
        float3 expBlend = new float3(biome.desertExponentialBlend, biome.plainsExponentialBlend, biome.mountainExponentialBlend);
        float3 expScale = new float3(biome.desertExponentialScale, biome.plainsExponentialScale, biome.mountainExponentialScale);

        float3 weightsVec = new float3(weights.Desert, weights.Plains, weights.Mountains);

        TerrainNoiseSettings adjusted = baseSettings;
        adjusted.heightMultiplier = baseSettings.heightMultiplier * math.csum(weightsVec * heightMultipliers);
        adjusted.baseHeight = baseSettings.baseHeight + math.csum(weightsVec * baseOffsets);
        adjusted.ridgeStrength = math.clamp(math.csum(weightsVec * ridge), 0f, 1f);
        adjusted.redistributionPower = math.max(0.001f, math.csum(weightsVec * redistribution));
        adjusted.exponentialBlend = math.clamp(math.csum(weightsVec * expBlend), 0f, 1f);
        adjusted.exponentialScale = math.max(0.001f, math.csum(weightsVec * expScale));

        return adjusted;
    }

    private static bool HasTimedOut(ref float timeoutRemaining, string stage)
    {
        timeoutRemaining -= Time.deltaTime;
        if (timeoutRemaining > 0f)
        {
            return false;
        }

        Debug.LogError($"GameManager timed out waiting for {stage}.");
        return true;
    }

    private void StartMusicLoop()
    {
        AudioClip clip = Resources.Load<AudioClip>("Music/Music");
        float volume = AppSettings.AudioVolume;
        PersistentMusicPlayer.EnsureExists(clip, volume);
    }
}
