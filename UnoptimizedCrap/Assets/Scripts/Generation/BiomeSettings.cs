using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Inspector-friendly biome settings that convert to Burst-safe data for generation jobs.
/// </summary>
[System.Serializable]
public struct BiomeSettings
{
    [Header("Sampling")]
    [Tooltip("Larger values spread biomes farther apart.")]
    public float biomeScale;
    [Tooltip("Offsets the biome noise to shift layout.")]
    public Vector2 biomeOffset;

    [Header("Height Multipliers")]
    public float desertHeightMultiplier;
    public float plainsHeightMultiplier;
    public float mountainHeightMultiplier;

    [Header("Base Height Offsets")]
    public float desertBaseOffset;
    public float plainsBaseOffset;
    public float mountainBaseOffset;

    [Header("Shaping")]
    public float desertRidgeStrength;
    public float plainsRidgeStrength;
    public float mountainRidgeStrength;
    public float desertRedistribution;
    public float plainsRedistribution;
    public float mountainRedistribution;
    [Range(0f, 1f)] public float desertExponentialBlend;
    [Range(0f, 1f)] public float plainsExponentialBlend;
    [Range(0f, 1f)] public float mountainExponentialBlend;
    public float desertExponentialScale;
    public float plainsExponentialScale;
    public float mountainExponentialScale;

    [Header("Surface Layers")]
    [Tooltip("How many blocks of sand to place above stone in deserts.")]
    public int desertSandDepth;
    [Tooltip("Extra sand depth for dunes near the surface.")]
    public int desertSurfaceBonusDepth;
    [Tooltip("Normalize height threshold to start exposing stone on mountains.")]
    public float mountainAlpineThreshold;

    public BiomeNoiseSettings ToNoiseSettings()
    {
        return new BiomeNoiseSettings
        {
            biomeScale = math.max(1f, biomeScale),
            biomeOffset = new float2(biomeOffset.x, biomeOffset.y),
            desertHeightMultiplier = desertHeightMultiplier,
            plainsHeightMultiplier = plainsHeightMultiplier,
            mountainHeightMultiplier = mountainHeightMultiplier,
            desertBaseOffset = desertBaseOffset,
            plainsBaseOffset = plainsBaseOffset,
            mountainBaseOffset = mountainBaseOffset,
            desertRidgeStrength = desertRidgeStrength,
            plainsRidgeStrength = plainsRidgeStrength,
            mountainRidgeStrength = mountainRidgeStrength,
            desertRedistribution = desertRedistribution,
            plainsRedistribution = plainsRedistribution,
            mountainRedistribution = mountainRedistribution,
            desertExponentialBlend = desertExponentialBlend,
            plainsExponentialBlend = plainsExponentialBlend,
            mountainExponentialBlend = mountainExponentialBlend,
            desertExponentialScale = desertExponentialScale,
            plainsExponentialScale = plainsExponentialScale,
            mountainExponentialScale = mountainExponentialScale,
            desertSandDepth = math.max(1, desertSandDepth),
            desertSurfaceBonusDepth = math.max(0, desertSurfaceBonusDepth),
            mountainAlpineThreshold = math.clamp(mountainAlpineThreshold, 0f, 1f)
        };
    }

    public static BiomeSettings CreateDefault()
    {
        return new BiomeSettings
        {
            biomeScale = 320f,
            biomeOffset = Vector2.zero,
            desertHeightMultiplier = 0.55f,
            plainsHeightMultiplier = 1f,
            mountainHeightMultiplier = 1.8f,
            desertBaseOffset = -6f,
            plainsBaseOffset = 0f,
            mountainBaseOffset = 12f,
            desertRidgeStrength = 0.08f,
            plainsRidgeStrength = 0.15f,
            mountainRidgeStrength = 0.65f,
            desertRedistribution = 1f,
            plainsRedistribution = 1.25f,
            mountainRedistribution = 1.9f,
            desertExponentialBlend = 0.12f,
            plainsExponentialBlend = 0.35f,
            mountainExponentialBlend = 0.58f,
            desertExponentialScale = 1.2f,
            plainsExponentialScale = 1.75f,
            mountainExponentialScale = 2.4f,
            desertSandDepth = 5,
            desertSurfaceBonusDepth = 2,
            mountainAlpineThreshold = 0.62f
        };
    }
}

/// <summary>
/// Burst-safe biome parameters consumed by generation jobs.
/// </summary>
public struct BiomeNoiseSettings
{
    public float biomeScale;
    public float2 biomeOffset;

    public float desertHeightMultiplier;
    public float plainsHeightMultiplier;
    public float mountainHeightMultiplier;

    public float desertBaseOffset;
    public float plainsBaseOffset;
    public float mountainBaseOffset;

    public float desertRidgeStrength;
    public float plainsRidgeStrength;
    public float mountainRidgeStrength;

    public float desertRedistribution;
    public float plainsRedistribution;
    public float mountainRedistribution;

    public float desertExponentialBlend;
    public float plainsExponentialBlend;
    public float mountainExponentialBlend;

    public float desertExponentialScale;
    public float plainsExponentialScale;
    public float mountainExponentialScale;

    public int desertSandDepth;
    public int desertSurfaceBonusDepth;
    public float mountainAlpineThreshold;
}
