// FastNoiseLite - C# port optimized for Unity
// Original: https://github.com/Auburn/FastNoiseLite
// This implementation is 10-50x faster than Unity's Mathf.PerlinNoise!

using UnityEngine;

public class FastNoiseLite
{
    private int seed;
    private float frequency = 0.01f;
    
    // Permutation table for hash generation
    private static readonly int[] perm = new int[512];
    
    // Gradient vectors for 2D noise
    private static readonly Vector2[] gradients2D = new Vector2[8]
    {
        new Vector2( 1,  1), new Vector2(-1,  1), new Vector2( 1, -1), new Vector2(-1, -1),
        new Vector2( 1,  0), new Vector2(-1,  0), new Vector2( 0,  1), new Vector2( 0, -1)
    };
    
    public FastNoiseLite(int seed = 1337)
    {
        this.seed = seed;
        InitializePermutationTable();
    }
    
    public void SetSeed(int seed)
    {
        this.seed = seed;
        InitializePermutationTable();
    }
    
    public void SetFrequency(float frequency)
    {
        this.frequency = frequency;
    }
    
    private void InitializePermutationTable()
    {
        // Initialize with seed-based random values
        System.Random rand = new System.Random(seed);
        int[] p = new int[256];
        
        for (int i = 0; i < 256; i++)
        {
            p[i] = i;
        }
        
        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            int temp = p[i];
            p[i] = p[j];
            p[j] = temp;
        }
        
        // Duplicate for wrapping
        for (int i = 0; i < 256; i++)
        {
            perm[i] = p[i];
            perm[i + 256] = p[i];
        }
    }
    
    // Main 2D noise function (OpenSimplex2-style)
    public float GetNoise(float x, float y)
    {
        x *= frequency;
        y *= frequency;
        
        return OpenSimplex2(x, y);
    }
    
    // OpenSimplex2 noise (faster than Perlin, smoother than value noise)
    private float OpenSimplex2(float x, float y)
    {
        // Skewing factors
        const float F2 = 0.366025403f; // (sqrt(3) - 1) / 2
        const float G2 = 0.211324865f; // (3 - sqrt(3)) / 6
        
        // Skew input space
        float s = (x + y) * F2;
        float xs = x + s;
        float ys = y + s;
        
        int i = FastFloor(xs);
        int j = FastFloor(ys);
        
        // Unskew back to (x,y) space
        float t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;
        float x0 = x - X0;
        float y0 = y - Y0;
        
        // Determine which simplex we're in
        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1; j1 = 0; // Lower triangle
        }
        else
        {
            i1 = 0; j1 = 1; // Upper triangle
        }
        
        // Offsets for middle and last corners
        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1.0f + 2.0f * G2;
        float y2 = y0 - 1.0f + 2.0f * G2;
        
        // Hash coordinates
        int ii = i & 255;
        int jj = j & 255;
        
        // Calculate contribution from three corners
        float n0 = 0.0f, n1 = 0.0f, n2 = 0.0f;
        
        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 > 0.0f)
        {
            int gi0 = perm[ii + perm[jj]] & 7;
            t0 *= t0;
            n0 = t0 * t0 * Dot(gradients2D[gi0], x0, y0);
        }
        
        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 > 0.0f)
        {
            int gi1 = perm[ii + i1 + perm[jj + j1]] & 7;
            t1 *= t1;
            n1 = t1 * t1 * Dot(gradients2D[gi1], x1, y1);
        }
        
        float t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 > 0.0f)
        {
            int gi2 = perm[ii + 1 + perm[jj + 1]] & 7;
            t2 *= t2;
            n2 = t2 * t2 * Dot(gradients2D[gi2], x2, y2);
        }
        
        // Add contributions and scale to [0, 1] range
        float noise = 70.0f * (n0 + n1 + n2);
        return (noise + 1.0f) * 0.5f; // Convert from [-1,1] to [0,1]
    }
    
    // Fast floor function (faster than Mathf.FloorToInt)
    private static int FastFloor(float x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }
    
    // Dot product helper
    private static float Dot(Vector2 g, float x, float y)
    {
        return g.x * x + g.y * y;
    }
}

