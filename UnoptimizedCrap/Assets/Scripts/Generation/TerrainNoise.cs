using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

/// <summary>
/// Burst-friendly noise settings for terrain generation.
/// Mirrors concepts from Sebastian Lague's procedural landmass generation.
/// </summary>
public struct TerrainNoiseSettings
{
    public float scale;
    public int octaves;
    public float lacunarity;
    public float persistence;
    public float redistributionPower;
    public float baseHeight;
    public float heightMultiplier;
    public float ridgeStrength;
    public float domainWarpStrength;
    public float domainWarpFrequency;
    public float exponentialBase;
    public float exponentialScale;
    public float exponentialBlend;
    public float minHeight;
    public float maxHeight;
    public float2 offset;
    public uint seed;
    public bool useFastNoiseSimd;

    public static TerrainNoiseSettings Default => new TerrainNoiseSettings
    {
        scale = 180f,
        octaves = 6,
        lacunarity = 2f,
        persistence = 0.5f,
        redistributionPower = 1.35f,
        baseHeight = 32f,
        heightMultiplier = 96f,
        ridgeStrength = 0.3f,
        domainWarpStrength = 45f,
        domainWarpFrequency = 0.4f,
        exponentialBase = 2f,
        exponentialScale = 1.75f,
        exponentialBlend = 0.35f,
        minHeight = 0f,
        maxHeight = 512f,
        offset = float2.zero,
        seed = 1442695041u, // large odd constant for hashing
        useFastNoiseSimd = true
    };
}

public readonly struct TerrainHeightSample
{
    public readonly float Height;
    public readonly float Normalized;
    public readonly float Redistributed;

    public TerrainHeightSample(float height, float normalized, float redistributed)
    {
        Height = height;
        Normalized = normalized;
        Redistributed = redistributed;
    }
}

/// <summary>
/// Burst-friendly procedural noise helper that mixes FBM Perlin with ridge and exponential shaping.
/// </summary>
[BurstCompile]
public static class TerrainNoise
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TerrainHeightSample SampleHeight(float2 worldXZ, in TerrainNoiseSettings settings)
    {
        float effectiveScale = math.max(settings.scale, 0.0001f);
        float2 baseCoords = (worldXZ + settings.offset) / effectiveScale;

        float2 warpedCoords = baseCoords;
        if (settings.domainWarpStrength > 0.0001f)
        {
            float2 warp = DomainWarp(baseCoords, settings);
            warpedCoords += warp * (settings.domainWarpStrength / effectiveScale);
        }

        float amplitude = 1f;
        float frequency = 1f;
        float heightAccum = 0f;
        float amplitudeAccum = 0f;

        for (int octave = 0; octave < math.max(settings.octaves, 1); octave++)
        {
            float2 octaveOffset = GetOctaveOffset(settings.seed, octave);
            float2 sampleCoords = warpedCoords * frequency + octaveOffset;

            float noiseValue = SampleOctave(sampleCoords, settings);

            // Ridge-like shaping inspired by Seb Lague
            float ridge = 1f - math.abs(noiseValue * 2f - 1f);
            ridge = math.pow(ridge, 2.2f);
            float blended = math.lerp(noiseValue, ridge, math.clamp(settings.ridgeStrength, 0f, 1f));

            heightAccum += blended * amplitude;
            amplitudeAccum += amplitude;

            amplitude *= settings.persistence;
            frequency *= settings.lacunarity;
        }

        float normalized = amplitudeAccum > 0f ? heightAccum / amplitudeAccum : 0f;
        normalized = math.clamp(normalized, 0f, 1f);

        float redistributed = math.pow(normalized, math.max(settings.redistributionPower, 0.001f));
        float baseHeight = settings.baseHeight + redistributed * settings.heightMultiplier;

        float exponentialComponent = math.pow(
            math.max(settings.exponentialBase, math.FLT_MIN_NORMAL),
            redistributed * settings.exponentialScale);

        // Use blend factor to mix standard terrain with exponential peaks
        float expBlend = math.clamp(settings.exponentialBlend, 0f, 1f);
        float exponentialHeight = exponentialComponent * settings.heightMultiplier;
        float blendedHeight = math.lerp(baseHeight, exponentialHeight, expBlend);
        float clampedHeight = math.clamp(blendedHeight, settings.minHeight, settings.maxHeight);

        return new TerrainHeightSample(clampedHeight, normalized, redistributed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 DomainWarp(float2 coords, in TerrainNoiseSettings settings)
    {
        float frequency = math.max(settings.domainWarpFrequency, 0.0001f);
        float2 warpSeed = HashFloat2(settings.seed * 2654435761u);

        float2 xOffset = coords * frequency + warpSeed;
        float2 yOffset = coords * (frequency * 1.37f) + warpSeed.yx + 19.19f;

        float warpX = noise.snoise(xOffset);
        float warpY = noise.snoise(yOffset);

        return new float2(warpX, warpY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 GetOctaveOffset(uint seed, int octaveIndex)
    {
        uint hashed = math.hash(new uint3((uint)octaveIndex, seed, 0x9E3779B9u));
        float2 rand = HashFloat2(hashed);
        return rand * 2048f; // Large spread to avoid pattern overlap
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SampleOctave(float2 sampleCoords, in TerrainNoiseSettings settings)
    {
        if (settings.useFastNoiseSimd)
        {
            // Fast hash-based value noise (SIMD-friendly, no managed calls)
            return FastNoiseSimd.Sample(sampleCoords);
        }

        // Convert simplex noise [-1,1] to [0,1]
        float noiseValue = noise.snoise(sampleCoords);
        return noiseValue * 0.5f + 0.5f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float2 HashFloat2(uint value)
    {
        uint hashed = value * 747796405u + 2891336453u;
        uint hashY = hashed ^ 0x9E3779B9u;

        return new float2(
            (hashed & 0x00FFFFFFu) / 16777215f - 0.5f,
            (hashY & 0x00FFFFFFu) / 16777215f - 0.5f);
    }
}

/// <summary>
/// Lightweight, Burst-friendly hash noise intended as a FastNoiseSIMD-style replacement when native bindings are unavailable.
/// Uses 2D value noise with bilinear interpolation for speed.
/// </summary>
[BurstCompile]
public static class FastNoiseSimd
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sample(float2 p)
    {
        int ix = (int)math.floor(p.x);
        int iz = (int)math.floor(p.y);

        float fx = p.x - ix;
        float fz = p.y - iz;

        float v00 = Hash01(ix, iz);
        float v10 = Hash01(ix + 1, iz);
        float v01 = Hash01(ix, iz + 1);
        float v11 = Hash01(ix + 1, iz + 1);

        float vx0 = math.lerp(v00, v10, Smooth(fx));
        float vx1 = math.lerp(v01, v11, Smooth(fx));
        return math.lerp(vx0, vx1, Smooth(fz));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Hash01(int x, int z)
    {
        uint h = (uint)(x * 374761393 + z * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0x00FFFFFFu) / 16777215f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
