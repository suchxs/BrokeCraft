using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Burst job that fills chunk block data using FBM Perlin terrain generation.
/// </summary>
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct ChunkTerrainGenerationJob : IJobFor
{
    [NativeDisableParallelForRestriction] public NativeArray<BlockType> Blocks;
    [WriteOnly] public NativeArray<BiomeId> ColumnBiomes;
    public int3 ChunkPosition;
    [ReadOnly] public TerrainNoiseSettings NoiseSettings;
    [ReadOnly] public BiomeNoiseSettings BiomeSettings;

    public int SoilDepth;
    public int BedrockDepth;
    public float AlpineNormalizedThreshold;
    public float SteepRedistributionThreshold;

    public void Execute(int columnIndex)
    {
        int chunkWidth = VoxelData.ChunkWidth;
        int chunkHeight = VoxelData.ChunkHeight;
        int chunkDepth = VoxelData.ChunkDepth;
        
        int z = columnIndex / chunkWidth;
        int x = columnIndex - z * chunkWidth;

        int worldX = ChunkPosition.x * chunkWidth + x;
        int worldZ = ChunkPosition.z * chunkDepth + z;

        BiomeWeights biomeWeights = SampleBiomeWeights(worldX, worldZ);
        TerrainNoiseSettings columnSettings = AdjustNoiseForBiome(NoiseSettings, biomeWeights);

        TerrainHeightSample sample = TerrainNoise.SampleHeight(new float2(worldX, worldZ), columnSettings);
        int terrainHeight = (int)math.floor(sample.Height);
        
        // Clamp terrain height to valid world bounds to prevent edge case bugs
        terrainHeight = math.clamp(terrainHeight, VoxelData.MinWorldHeight, VoxelData.MaxWorldHeight);

        int chunkWorldYStart = ChunkPosition.y * chunkHeight;
        int blockIndex = x + chunkWidth * (chunkHeight * z);
        int worldY = chunkWorldYStart;

        if (ColumnBiomes.IsCreated)
        {
            ColumnBiomes[columnIndex] = biomeWeights.Dominant;
        }

        for (int y = 0; y < chunkHeight; y++)
        {
            BlockType block = ResolveBlockType(worldY, terrainHeight, sample.Normalized, sample.Redistributed, biomeWeights);
            Blocks[blockIndex] = block;
            worldY++;
            blockIndex += chunkWidth;
        }
    }

    private BlockType ResolveBlockType(int worldY, int terrainHeight, float normalized, float redistributed, BiomeWeights biomeWeights)
    {
        // Bedrock layer at bottom of world
        if (worldY <= BedrockDepth)
            return BlockType.Bedrock;

        // Air above terrain surface
        if (worldY > terrainHeight)
            return BlockType.Air;

        // Surface layer - grass or stone based on biome conditions
        int depthFromSurface = terrainHeight - worldY;
        BiomeId dominant = biomeWeights.Dominant;

        if (dominant == BiomeId.Desert)
        {
            int sandDepth = BiomeSettings.desertSandDepth;
            if (depthFromSurface == 0)
            {
                sandDepth += BiomeSettings.desertSurfaceBonusDepth;
            }

            if (depthFromSurface <= sandDepth)
            {
                return BlockType.Sand;
            }

            return BlockType.Stone;
        }

        if (depthFromSurface == 0)
        {
            float alpineThreshold = dominant == BiomeId.Mountains
                ? BiomeSettings.mountainAlpineThreshold
                : AlpineNormalizedThreshold;

            bool isAlpine = normalized >= alpineThreshold;
            bool isSteep = redistributed <= SteepRedistributionThreshold;
            return (isAlpine || isSteep) ? BlockType.Stone : BlockType.Grass;
        }

        // Soil layer beneath grass
        if (depthFromSurface <= SoilDepth)
            return BlockType.Dirt;

        // Deep underground - stone
        return BlockType.Stone;
    }

    private BiomeWeights SampleBiomeWeights(int worldX, int worldZ)
    {
        float2 coords = new float2(worldX, worldZ);
        float2 scaled = (coords + BiomeSettings.biomeOffset) / math.max(1f, BiomeSettings.biomeScale);
        float biomeNoise = noise.snoise(scaled) * 0.5f + 0.5f;

        // Soft triangular weights so transitions stay smooth.
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

    private TerrainNoiseSettings AdjustNoiseForBiome(TerrainNoiseSettings baseSettings, BiomeWeights weights)
    {
        float3 heightMultipliers = new float3(BiomeSettings.desertHeightMultiplier, BiomeSettings.plainsHeightMultiplier, BiomeSettings.mountainHeightMultiplier);
        float3 baseOffsets = new float3(BiomeSettings.desertBaseOffset, BiomeSettings.plainsBaseOffset, BiomeSettings.mountainBaseOffset);
        float3 ridge = new float3(BiomeSettings.desertRidgeStrength, BiomeSettings.plainsRidgeStrength, BiomeSettings.mountainRidgeStrength);
        float3 redistribution = new float3(BiomeSettings.desertRedistribution, BiomeSettings.plainsRedistribution, BiomeSettings.mountainRedistribution);
        float3 expBlend = new float3(BiomeSettings.desertExponentialBlend, BiomeSettings.plainsExponentialBlend, BiomeSettings.mountainExponentialBlend);
        float3 expScale = new float3(BiomeSettings.desertExponentialScale, BiomeSettings.plainsExponentialScale, BiomeSettings.mountainExponentialScale);

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
}

public struct BiomeWeights
{
    public float Desert;
    public float Plains;
    public float Mountains;
    public BiomeId Dominant;
}

public enum BiomeId : byte
{
    Desert = 0,
    Plains = 1,
    Mountains = 2
}
