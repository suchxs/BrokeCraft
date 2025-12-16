using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct TreePlacementJob : IJob
{
    [NativeDisableParallelForRestriction] public NativeArray<BlockType> Blocks;
    public int3 ChunkPosition;
    public TerrainNoiseSettings NoiseSettings;
    public BiomeNoiseSettings BiomeSettings;
    public int TreeSpacing;
    public uint Seed;

    private const int ChunkWidth = VoxelData.ChunkWidth;
    private const int ChunkHeight = VoxelData.ChunkHeight;
    private const int ChunkDepth = VoxelData.ChunkDepth;

    public void Execute()
    {
        for (int x = 2; x < ChunkWidth - 2; x += 1)
        {
            for (int z = 2; z < ChunkDepth - 2; z += 1)
            {
                int worldX = ChunkPosition.x * ChunkWidth + x;
                int worldZ = ChunkPosition.z * ChunkDepth + z;

                // Light randomness to thin out trees
                uint hash = math.hash(new uint3((uint)worldX, (uint)worldZ, Seed));
                if ((hash & 0xFF) > 200)
                {
                    continue;
                }

                if ((math.abs(worldX) % TreeSpacing) != 0 || (math.abs(worldZ) % TreeSpacing) != 0)
                {
                    continue;
                }

                // Require plains biome at center
                BiomeWeights weights = SampleBiomeWeights(worldX, worldZ);
                if (weights.Desert > 0.6f)
                {
                    continue; // avoid very dry biomes
                }

                // Check for a 2x2 flat grass patch using the generated blocks
                int surfaceY;
                if (!TryGetFlatGrassPatch(x, z, out surfaceY))
                {
                    continue;
                }

                // Must be inside this chunk vertically
                if (surfaceY <= 0 || surfaceY >= ChunkHeight - 4)
                {
                    continue;
                }

                PlaceTree(x, surfaceY + 1, z, hash);
            }
        }
    }

    private void PlaceTree(int localX, int baseY, int localZ, uint hash)
    {
        int trunkHeight = 4 + (int)(hash % 3); // 4-6
        int topY = baseY + trunkHeight;
        if (topY + 2 >= ChunkHeight)
        {
            return; // tree would exceed chunk height
        }

        // Trunk
        for (int i = 0; i < trunkHeight; i++)
        {
            int y = baseY + i;
            int idx = VoxelData.GetBlockIndex(localX, y, localZ);
            Blocks[idx] = BlockType.OakLog;
        }

        // Leaves
        int canopyBase = baseY + trunkHeight - 2;
        for (int y = 0; y < 3; y++)
        {
            int layerY = canopyBase + y;
            int radius = (y == 2) ? 1 : 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (math.abs(dx) + math.abs(dz) > radius + 1)
                        continue;

                    int px = localX + dx;
                    int pz = localZ + dz;
                    if (px < 0 || px >= ChunkWidth || pz < 0 || pz >= ChunkDepth)
                        continue;

                    int idx = VoxelData.GetBlockIndex(px, layerY, pz);
                    if (Blocks[idx] == BlockType.Air)
                    {
                        Blocks[idx] = BlockType.OakLeaves;
                    }
                }
            }
        }
    }

    private bool TryGetFlatGrassPatch(int localX, int localZ, out int surfaceY)
    {
        surfaceY = -1;
        if (localX < 0 || localZ < 0 || localX >= ChunkWidth - 1 || localZ >= ChunkDepth - 1)
        {
            return false;
        }

        int patchY = -1;
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dz = 0; dz <= 1; dz++)
            {
                int lx = localX + dx;
                int lz = localZ + dz;
                int y;
                if (!TryGetSurfaceHeight(lx, lz, out y))
                {
                    return false;
                }

                int index = VoxelData.GetBlockIndex(lx, y, lz);
                if (Blocks[index] != BlockType.Grass)
                {
                    return false;
                }

                if (patchY == -1)
                {
                    patchY = y;
                }
                else if (y != patchY)
                {
                    return false;
                }
            }
        }
        surfaceY = patchY;
        return patchY >= 0;
    }

    private bool TryGetSurfaceHeight(int localX, int localZ, out int surfaceY)
    {
        surfaceY = -1;
        for (int y = ChunkHeight - 1; y >= 0; y--)
        {
            int idx = VoxelData.GetBlockIndex(localX, y, localZ);
            BlockType type = Blocks[idx];
            if (type != BlockType.Air && type != BlockType.OakLeaves)
            {
                surfaceY = y;
                return true;
            }
        }
        return false;
    }

    private BiomeWeights SampleBiomeWeights(int worldX, int worldZ)
    {
        float2 coords = new float2(worldX, worldZ);
        float2 scaled = (coords + BiomeSettings.biomeOffset) / math.max(1f, BiomeSettings.biomeScale);
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
