using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Burst-compatible voxel data constants.
/// All data is stored as simple types (int, float, etc.) that can be used in Burst jobs.
/// Arrays are stored as inline constants or will be copied to NativeArrays at runtime.
/// </summary>
public static class VoxelData
{
    // Cubic chunk dimensions - MUST be same in all axes for true cubic chunks
    // Small for testing (16x16x16 typical for production)
    public const int ChunkWidth = 16;
    public const int ChunkHeight = 16;
    public const int ChunkDepth = 16;
    
    // Total blocks per chunk
    public const int ChunkSize = ChunkWidth * ChunkHeight * ChunkDepth;
    
    // Terrain generation constants
    public const int MinWorldHeight = -64;     // Minecraft-style build limit (bottom)
    public const int MaxWorldHeight = 320;     // Minecraft-style build limit (top)
    public const int SeaLevel = 64;            // Default water level
    
    // Default terrain layer depths
    public const int DefaultSoilDepth = 4;
    public const int DefaultBedrockDepth = 1;
    public const int MinSoilDepth = 1;
    public const int MaxSoilDepth = 8;
    public const int MinBedrockDepth = 1;
    public const int MaxBedrockDepth = 4;
    
    // Player physics constants (Minecraft Java Edition values)
    public const float PlayerWalkSpeed = 4.317f;    // m/s
    public const float PlayerSprintSpeed = 5.612f;  // m/s
    public const float PlayerSneakSpeed = 1.295f;   // m/s
    public const float PlayerJumpHeight = 1.25f;    // blocks
    public const float PlayerGravity = 32f;         // m/s² (Minecraft gravity)
    public const float PlayerHeight = 1.8f;         // blocks
    public const float PlayerEyeHeight = 1.62f;     // blocks from ground
    public const float PlayerWidth = 0.6f;          // blocks
    public const float PlayerBlockReach = 4.5f;     // survival reach distance

    // CharacterController settings for precise Minecraft-like collision
    public const float PlayerSkinWidth = 0.04f;     // Small but safe skin to avoid tunnelling
    public const float PlayerMinMoveDistance = 0.001f;  // Allow micro-movements
    public const float PlayerStepOffset = 0.5f;     // Can step up half block
    public const float PlayerSlopeLimit = 45f;      // Standard slope limit
    
    // Texture atlas configuration
    // Complete atlas: 1083 textures in 33x33 grid (528x528px, 16px per texture)
    // Built from ALL Minecraft block textures for future-proofing
    public const int AtlasWidth = 33;   // Textures horizontally
    public const int AtlasHeight = 33;  // Textures vertically
    public const float NormalizedBlockTextureSizeX = 1f / AtlasWidth;
    public const float NormalizedBlockTextureSizeY = 1f / AtlasHeight;
    
    // UV padding to prevent texture bleeding (seams between blocks)
    // This value pulls UVs inward from edges to avoid sampling neighbor textures
    // Higher value = less bleeding but slightly smaller texture area used
    public const float UVPadding = 0.005f;  // Increased from 0.001f
    
    // Face directions for culling checks
    public const int FaceCount = 6;
    public enum Face
    {
        Back = 0,   // -Z
        Front = 1,  // +Z
        Top = 2,    // +Y
        Bottom = 3, // -Y
        Left = 4,   // -X
        Right = 5   // +X
    }
    
    // Mesh generation constants
    public const int VerticesPerFace = 4;      // Quad face = 4 vertices
    public const int TrianglesPerFace = 2;     // Quad = 2 triangles
    public const int IndicesPerTriangle = 3;   // Triangle = 3 vertex indices
    public const int IndicesPerFace = 6;       // 2 triangles * 3 indices = 6
    
    // Voxel vertices as bytes (8 corners of a cube)
    // Using byte3 for massive memory savings - voxel coords are always 0 or 1
    // 3 bytes per vertex vs 12 bytes for float3 = 4x memory reduction
    public static readonly byte3[] VoxelVerticesBytes = new byte3[8]
    {
        new byte3(0, 0, 0), // 0
        new byte3(1, 0, 0), // 1
        new byte3(1, 1, 0), // 2
        new byte3(0, 1, 0), // 3
        new byte3(0, 0, 1), // 4
        new byte3(1, 0, 1), // 5
        new byte3(1, 1, 1), // 6
        new byte3(0, 1, 1)  // 7
    };
    
    /// <summary>
    /// Convert byte3 vertex to float3 for mesh data.
    /// Burst will inline this for zero overhead.
    /// </summary>
    public static float3 ByteVertexToFloat(byte3 vertex)
    {
        return new float3(vertex.x, vertex.y, vertex.z);
    }
    
    // Face check directions as bytes (neighboring block offsets for culling)
    // Using byte for memory efficiency - values are only -1, 0, or 1
    // Stored as sbyte (signed byte) since we need negative values
    public static readonly sbyte3[] FaceChecksBytes = new sbyte3[6]
    {
        new sbyte3(0, 0, -1), // Back
        new sbyte3(0, 0, 1),  // Front
        new sbyte3(0, 1, 0),  // Top
        new sbyte3(0, -1, 0), // Bottom
        new sbyte3(-1, 0, 0), // Left
        new sbyte3(1, 0, 0)   // Right
    };
    
    /// <summary>
    /// Convert sbyte3 face check to int3.
    /// Burst will inline this for zero overhead.
    /// </summary>
    public static int3 ByteFaceCheckToInt(sbyte3 faceCheck)
    {
        return new int3(faceCheck.x, faceCheck.y, faceCheck.z);
    }
    
    // Vertex indices for each face as bytes (indices 0-7, fits in byte)
    // 4 vertices per face, 6 faces = 24 bytes instead of 96 bytes with int
    public static readonly byte[] VoxelTrianglesBytes = new byte[24]
    {
        // Back Face (indices 0-3)
        0, 3, 1, 2,
        // Front Face (indices 4-7)
        5, 6, 4, 7,
        // Top Face (indices 8-11)
        3, 7, 2, 6,
        // Bottom Face (indices 12-15)
        1, 5, 0, 4,
        // Left Face (indices 16-19)
        4, 7, 0, 3,
        // Right Face (indices 20-23)
        1, 2, 5, 6
    };
    
    // UV coordinates as bytes (0 or 1 only, then converted to float)
    // 2 bytes per UV vs 8 bytes for float2 = 4x memory reduction
    public static readonly byte2[] VoxelUVsBytes = new byte2[4]
    {
        new byte2(0, 0), // Bottom-left
        new byte2(0, 1), // Top-left
        new byte2(1, 0), // Bottom-right
        new byte2(1, 1)  // Top-right
    };
    
    /// <summary>
    /// Convert byte2 UV to float2.
    /// Burst will inline this for zero overhead.
    /// </summary>
    public static float2 ByteUVToFloat(byte2 uv)
    {
        return new float2(uv.x, uv.y);
    }
    
    // Face normals for lighting calculations
    public static readonly float3[] FaceNormals = new float3[6]
    {
        new float3(0, 0, -1), // Back
        new float3(0, 0, 1),  // Front
        new float3(0, 1, 0),  // Top
        new float3(0, -1, 0), // Bottom
        new float3(-1, 0, 0), // Left
        new float3(1, 0, 0)   // Right
    };
    
    /// <summary>
    /// Convert 3D chunk coordinates to 1D array index
    /// </summary>
    public static int GetBlockIndex(int x, int y, int z)
    {
        return x + ChunkWidth * (y + ChunkHeight * z);
    }
    
    /// <summary>
    /// Check if block coordinates are within chunk bounds
    /// </summary>
    public static bool IsBlockInChunk(int x, int y, int z)
    {
        return x >= 0 && x < ChunkWidth &&
               y >= 0 && y < ChunkHeight &&
               z >= 0 && z < ChunkDepth;
    }
}
