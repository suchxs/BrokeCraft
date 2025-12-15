using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Compact per-column terrain metadata extracted from a chunk once its voxel data is ready.
/// Stores both local (chunk space) and world-space heights so distant systems can reuse the data.
/// </summary>
public struct ChunkColumnSummary
{
    public int surfaceLocalY;
    public int surfaceWorldY;
    public int minLocalSolidY;
    public int minWorldSolidY;
    public int maxLocalSolidY;
    public int maxWorldSolidY;
    public int solidHeight;
    public BlockType surfaceBlock;
    public byte hasSurface;
    public byte surfaceBiome;
}

/// <summary>
/// Burst job that scans a cubic chunk and produces one <see cref="ChunkColumnSummary"/> per X/Z column.
/// </summary>
[BurstCompile(CompileSynchronously = true)]
public struct ChunkColumnSummaryJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<BlockType> Blocks;
    [ReadOnly] public NativeArray<BiomeId> ColumnBiomes;
    public NativeArray<ChunkColumnSummary> Summaries;
    public int3 ChunkPosition;

    public void Execute(int index)
    {
        int chunkWidth = VoxelData.ChunkWidth;
        int chunkHeight = VoxelData.ChunkHeight;

        int localX = index % chunkWidth;
        int localZ = index / chunkWidth;

        int highestSolid = -1;
        int lowestSolid = chunkHeight;
        BlockType surfaceBlock = BlockType.Air;

        for (int localY = chunkHeight - 1; localY >= 0; localY--)
        {
            int blockIndex = VoxelData.GetBlockIndex(localX, localY, localZ);
            BlockType block = Blocks[blockIndex];
            if (block == BlockType.Air)
            {
                continue;
            }

            if (highestSolid < 0)
            {
                highestSolid = localY;
                surfaceBlock = block;
            }

            lowestSolid = localY;
        }

        bool hasSurface = highestSolid >= 0;
        int baseWorldY = ChunkPosition.y * chunkHeight;
        int worldSurfaceY = hasSurface ? baseWorldY + highestSolid : int.MinValue;
        int worldMinSolidY = hasSurface ? baseWorldY + lowestSolid : int.MinValue;

        Summaries[index] = new ChunkColumnSummary
        {
            surfaceLocalY = highestSolid,
            surfaceWorldY = worldSurfaceY,
            minLocalSolidY = hasSurface ? lowestSolid : -1,
            minWorldSolidY = worldMinSolidY,
            maxLocalSolidY = highestSolid,
            maxWorldSolidY = worldSurfaceY,
            solidHeight = hasSurface ? math.max(1, highestSolid - lowestSolid + 1) : 0,
            surfaceBlock = surfaceBlock,
            hasSurface = hasSurface ? (byte)1 : (byte)0,
            surfaceBiome = ColumnBiomes.IsCreated && index < ColumnBiomes.Length
                ? (byte)ColumnBiomes[index]
                : (byte)BiomeId.Plains
        };
    }
}
