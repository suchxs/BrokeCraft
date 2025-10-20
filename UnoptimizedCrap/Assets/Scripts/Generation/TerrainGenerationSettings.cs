using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Unity-serializable terrain settings that can be passed to Burst jobs.
/// </summary>
[System.Serializable]
public struct TerrainGenerationSettings
{
    [Header("Noise")]
    public float noiseScale;
    [Range(1, 12)] public int octaves;
    public float lacunarity;
    public float persistence;
    public float redistributionPower;
    public float baseHeight;
    public float heightMultiplier;
    [Range(0f, 1f)] public float ridgeStrength;
    public float domainWarpStrength;
    public float domainWarpFrequency;
    public float exponentialBase;
    public float exponentialScale;
    [Range(0f, 1f)] public float exponentialBlend;
    public float minHeight;
    public float maxHeight;
    public Vector2 offset;
    public uint seed;

    [Header("Block Layers")]
    [Range(1, 8)] public int soilDepth;
    [Range(1, 4)] public int bedrockDepth;
    [Range(0f, 1f)] public float alpineNormalizedThreshold;
    [Range(0f, 1f)] public float steepRedistributionThreshold;

    public TerrainNoiseSettings ToNoiseSettings()
    {
        TerrainNoiseSettings noiseSettings = TerrainNoiseSettings.Default;

        noiseSettings.scale = noiseScale;
        noiseSettings.octaves = octaves;
        noiseSettings.lacunarity = lacunarity;
        noiseSettings.persistence = persistence;
        noiseSettings.redistributionPower = redistributionPower;
        noiseSettings.baseHeight = baseHeight;
        noiseSettings.heightMultiplier = heightMultiplier;
        noiseSettings.ridgeStrength = ridgeStrength;
        noiseSettings.domainWarpStrength = domainWarpStrength;
        noiseSettings.domainWarpFrequency = domainWarpFrequency;
        noiseSettings.exponentialBase = exponentialBase;
        noiseSettings.exponentialScale = exponentialScale;
        noiseSettings.exponentialBlend = exponentialBlend;
        noiseSettings.minHeight = minHeight;
        noiseSettings.maxHeight = maxHeight;
        noiseSettings.offset = new float2(offset.x, offset.y);
        noiseSettings.seed = seed;

        return noiseSettings;
    }

    public static TerrainGenerationSettings CreateDefault()
    {
        return new TerrainGenerationSettings
        {
            noiseScale = TerrainNoiseSettings.Default.scale,
            octaves = TerrainNoiseSettings.Default.octaves,
            lacunarity = TerrainNoiseSettings.Default.lacunarity,
            persistence = TerrainNoiseSettings.Default.persistence,
            redistributionPower = TerrainNoiseSettings.Default.redistributionPower,
            baseHeight = TerrainNoiseSettings.Default.baseHeight,
            heightMultiplier = TerrainNoiseSettings.Default.heightMultiplier,
            ridgeStrength = TerrainNoiseSettings.Default.ridgeStrength,
            domainWarpStrength = TerrainNoiseSettings.Default.domainWarpStrength,
            domainWarpFrequency = TerrainNoiseSettings.Default.domainWarpFrequency,
            exponentialBase = TerrainNoiseSettings.Default.exponentialBase,
            exponentialScale = TerrainNoiseSettings.Default.exponentialScale,
            exponentialBlend = TerrainNoiseSettings.Default.exponentialBlend,
            minHeight = TerrainNoiseSettings.Default.minHeight,
            maxHeight = TerrainNoiseSettings.Default.maxHeight,
            offset = Vector2.zero,
            seed = TerrainNoiseSettings.Default.seed,
            soilDepth = 4,
            bedrockDepth = 1,
            alpineNormalizedThreshold = 0.72f,
            steepRedistributionThreshold = 0.35f
        };
    }
}
