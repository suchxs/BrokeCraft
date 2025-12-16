/// <summary>
/// Block types in the voxel world.
/// Using byte for minimal memory footprint (supports up to 256 block types).
/// Burst-compatible - no managed types.
/// </summary>
public enum BlockType : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
    Bedrock = 4,
    Sand = 5,
    OakLog = 6,
    OakLeaves = 7,
    // Add more block types as needed (Wood, Sand, Cobblestone, etc.)
}
