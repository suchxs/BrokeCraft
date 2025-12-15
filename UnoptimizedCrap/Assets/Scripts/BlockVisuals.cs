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
        new float4(0.38f, 0.58f, 0.29f, 1f), // Grass (base, overridden by biome tint)
        new float4(0.15f, 0.13f, 0.18f, 1f), // Bedrock
        new float4(0.86f, 0.82f, 0.65f, 1f)  // Sand
    };

    private static readonly float4 plainsGrass = new float4(0.568f, 0.741f, 0.349f, 1f); // #91BD59
    private static readonly float4 mountainGrass = new float4(0.475f, 0.752f, 0.353f, 1f); // #79C05A

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

    public static float4 GetSurfaceTint(BlockType blockType, BiomeId biome)
    {
        if (blockType == BlockType.Grass)
        {
            switch (biome)
            {
                case BiomeId.Mountains:
                    return mountainGrass;
                case BiomeId.Plains:
                default:
                    return plainsGrass;
            }
        }

        return GetSurfaceTint(blockType);
    }
}
