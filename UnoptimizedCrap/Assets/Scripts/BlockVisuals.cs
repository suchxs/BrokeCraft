using Unity.Mathematics;

/// <summary>
/// Lightweight block appearance hints for systems that cannot access full materials (e.g., distant LODs).
/// Burst-friendly values (float4) so they can be copied into native containers.
/// </summary>
public static class BlockVisuals
{
    // Predefined tints for known blocks. Alpha channel stays at 1 for consistency.
    private static readonly float4[] blockTints =
    {
        new float4(0.6f, 0.75f, 0.95f, 1f), // Air (unused but keeps array aligned)
        new float4(0.45f, 0.45f, 0.48f, 1f), // Stone
        new float4(0.42f, 0.32f, 0.24f, 1f), // Dirt
        new float4(0.38f, 0.58f, 0.29f, 1f), // Grass
        new float4(0.15f, 0.13f, 0.18f, 1f)  // Bedrock
    };

    /// <summary>
    /// Returns a simple RGBA tint for the given block type.
    /// </summary>
    public static float4 GetSurfaceTint(BlockType blockType)
    {
        int index = (int)blockType;
        if (index >= 0 && index < blockTints.Length)
        {
            return blockTints[index];
        }

        // Default neutral gray if new blocks appear without defined tint.
        return new float4(0.5f, 0.5f, 0.5f, 1f);
    }
}
