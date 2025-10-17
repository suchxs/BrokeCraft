using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Burst-compatible block texture data.
/// Maps block types and faces to texture atlas indices.
/// All data uses value types and can be used in Burst jobs.
/// </summary>
public static class BlockTextureData
{
    // Texture indices from CompleteBlockAtlas_Custom.png
    // Built from ALL 1083 Minecraft block textures (33x33 grid)
    // See CompleteBlockAtlas_Reference.png for visual ID reference
    public const int TEX_STONE = 907;
    public const int TEX_DIRT = 346;
    public const int TEX_GRASS_TOP = 446;
    public const int TEX_GRASS_SIDE = 443;
    public const int TEX_BEDROCK = 48;
    
    /// <summary>
    /// Get texture atlas index for a block type and face.
    /// This function is Burst-compatible (no managed types, pure value types).
    /// </summary>
    public static int GetTextureIndex(BlockType blockType, int faceIndex)
    {
        switch (blockType)
        {
            case BlockType.Air:
                return 0; // Air doesn't render, but need valid index
            
            case BlockType.Stone:
                return TEX_STONE; // All faces use same texture
            
            case BlockType.Dirt:
                return TEX_DIRT; // All faces use same texture
            
            case BlockType.Grass:
                // Grass has different textures per face
                if (faceIndex == (int)VoxelData.Face.Top)
                    return TEX_GRASS_TOP;
                else if (faceIndex == (int)VoxelData.Face.Bottom)
                    return TEX_DIRT; // Bottom is dirt
                else
                    return TEX_GRASS_SIDE; // Sides are grass side texture
            
            case BlockType.Bedrock:
                return TEX_BEDROCK; // All faces use same texture
            
            default:
                return TEX_STONE; // Fallback
        }
    }
    
    /// <summary>
    /// Convert texture index to UV coordinates in atlas.
    /// 33x33 grid atlas with Y-flip for Unity UV coordinates.
    /// Burst-compatible using float2.
    /// </summary>
    public static float2 GetAtlasUVOffset(int textureIndex)
    {
        // Calculate grid position (row-major order)
        int x = textureIndex % VoxelData.AtlasWidth;
        int y = textureIndex / VoxelData.AtlasWidth;
        
        // CRITICAL: Flip Y coordinate!
        // Atlas rows: 0 at TOP, 32 at BOTTOM
        // Unity UVs: 0 at BOTTOM, 1 at TOP
        int flippedY = (VoxelData.AtlasHeight - 1) - y;
        
        return new float2(
            x * VoxelData.NormalizedBlockTextureSizeX,
            flippedY * VoxelData.NormalizedBlockTextureSizeY
        );
    }
    
    /// <summary>
    /// Get full UV coordinate for a vertex with padding to prevent texture bleeding.
    /// baseUV should be (0,0), (0,1), (1,0), or (1,1) from VoxelData.VoxelUVs
    /// Burst-compatible.
    /// </summary>
    public static float2 GetAtlasUV(int textureIndex, float2 baseUV)
    {
        float2 offset = GetAtlasUVOffset(textureIndex);
        
        // Apply UV padding to prevent texture bleeding (seams between blocks)
        // Pull UVs slightly inward from edges (0.001 padding on each side)
        float2 paddedUV = new float2(
            baseUV.x * (1f - VoxelData.UVPadding * 2f) + VoxelData.UVPadding,
            baseUV.y * (1f - VoxelData.UVPadding * 2f) + VoxelData.UVPadding
        );
        
        // Scale to texture size and add offset
        return new float2(
            offset.x + paddedUV.x * VoxelData.NormalizedBlockTextureSizeX,
            offset.y + paddedUV.y * VoxelData.NormalizedBlockTextureSizeY
        );
    }
}

