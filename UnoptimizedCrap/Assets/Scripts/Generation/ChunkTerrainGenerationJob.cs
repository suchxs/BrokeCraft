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
    public int3 ChunkPosition;
    [ReadOnly] public TerrainNoiseSettings NoiseSettings;

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

        TerrainHeightSample sample = TerrainNoise.SampleHeight(new float2(worldX, worldZ), NoiseSettings);
        int terrainHeight = (int)math.floor(sample.Height);

        int chunkWorldYStart = ChunkPosition.y * chunkHeight;
        int blockIndex = x + chunkWidth * (chunkHeight * z);
        int worldY = chunkWorldYStart;

        for (int y = 0; y < chunkHeight; y++)
        {
            BlockType block = ResolveBlockType(worldY, terrainHeight, sample.Normalized, sample.Redistributed);
            Blocks[blockIndex] = block;
            worldY++;
            blockIndex += chunkWidth;
        }
    }

    private BlockType ResolveBlockType(int worldY, int terrainHeight, float normalized, float redistributed)
    {
        if (worldY <= BedrockDepth)
            return BlockType.Bedrock;

        if (worldY > terrainHeight)
        {
            return BlockType.Air;
        }

        int depthFromSurface = terrainHeight - worldY;
        if (depthFromSurface == 0)
        {
            bool isAlpine = normalized >= AlpineNormalizedThreshold;
            bool isSteep = redistributed <= SteepRedistributionThreshold;
            return (isAlpine || isSteep) ? BlockType.Stone : BlockType.Grass;
        }

        if (depthFromSurface <= SoilDepth)
        {
            return BlockType.Dirt;
        }

        return BlockType.Stone;
    }
}
