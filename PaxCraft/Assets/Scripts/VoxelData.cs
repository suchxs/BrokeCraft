using UnityEngine;
using System.Collections.Generic;
using UnityEditor.PackageManager;

public static class VoxelData
{


    public static readonly int ChunkWidth = 5;
    public static readonly int ChunkHeight = 5;

    public static readonly Vector3[] voxelVerts = new Vector3[8]
    {
        // 0 - 7 Vertices
        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 1.0f),
    };

    public static readonly Vector3[] faceChecks = new Vector3[6] // Voxel Adjacent of voxelTris to check if it's visible to player
    {
        new Vector3(0.0f, 0.0f, -1.0f), // Back
        new Vector3(0.0f, 0.0f, 1.0f),  // Front
        new Vector3(0.0f, 1.0f, 0.0f),  // Top
        new Vector3(0.0f, -1.0f, 0.0f), // Bottom
        new Vector3(-1.0f, 0.0f, 0.0f), // Left
        new Vector3(1.0f, 0.0f, 0.0f),  // Right
    };

    // data from Mojang themselves lol

    // 6 vertices (inices) per face

    public static readonly int[][] voxelTris = new int[][]
    {
        // 0 1 2 2 1 3
        new int[] {0, 3, 1, 2}, // Back Face
        new int[] {5, 6, 4, 7}, // Front Face
        new int[] {3, 7, 2, 6}, // Top Face
        new int[] {1, 5, 0, 4}, // Bottom Face
        new int[] {4, 7, 0, 3}, // Left Face
        new int[] {1, 2, 5, 6}  // Right Face
    };

    public static readonly Vector2[] voxelUvs = new Vector2[4]
    {
        new Vector2 (0.0f, 0.0f),
        new Vector2 (0.0f, 1.0f),
        new Vector2 (1.0f, 0.0f),
        new Vector2 (1.0f, 1.0f)
    };
}
