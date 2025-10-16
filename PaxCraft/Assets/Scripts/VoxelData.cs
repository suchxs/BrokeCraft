using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelData
{

    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 256;  // Full world height
    public static readonly int SectionHeight = 16;  // Each section is 16 blocks tall
    
    // Calculate number of sections per chunk
    public static readonly int SectionsPerChunk = ChunkHeight / SectionHeight;  // 256/16 = 16 sections

    // Texture atlas dimensions (Minecraft standard: 1024x512 with 16x16 textures)
    public static readonly int TextureAtlasWidth = 64;   // 64 blocks wide (1024 ÷ 16)
    public static readonly int TextureAtlasHeight = 32;  // 32 blocks tall (512 ÷ 16)
    
    // Pre-calculated normalized texture size
    public static readonly float NormalizedBlockTextureWidth = 1f / 64f;   // 0.015625
    public static readonly float NormalizedBlockTextureHeight = 1f / 32f;  // 0.03125

    public static readonly Vector3[] voxelVerts = new Vector3[8] {

        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 1.0f),

    };

    public static readonly Vector3[] faceChecks = new Vector3[6] {

        new Vector3(0.0f, 0.0f, -1.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, -1.0f, 0.0f),
        new Vector3(-1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f)

    };

    public static readonly int[,] voxelTris = new int[6, 4] {

        // Back, Front, Top, Bottom, Left, Right

		// 0 1 2 2 1 3
		{0, 3, 1, 2}, // Back Face
		{5, 6, 4, 7}, // Front Face
		{3, 7, 2, 6}, // Top Face
		{1, 5, 0, 4}, // Bottom Face
		{4, 7, 0, 3}, // Left Face
		{1, 2, 5, 6} // Right Face

	};

    public static readonly Vector2[] voxelUvs = new Vector2[4] {

        new Vector2 (0.0f, 0.0f),
        new Vector2 (0.0f, 1.0f),
        new Vector2 (1.0f, 0.0f),
        new Vector2 (1.0f, 1.0f)

    };

}

// Chunk coordinate structure for tracking chunk positions
[System.Serializable]
public struct ChunkCoord
{
    public int x;
    public int z;

    public ChunkCoord(int _x, int _z)
    {
        x = _x;
        z = _z;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is ChunkCoord))
            return false;

        ChunkCoord coord = (ChunkCoord)obj;
        return x == coord.x && z == coord.z;
    }

    public override int GetHashCode()
    {
        return (x * 397) ^ z;
    }
}