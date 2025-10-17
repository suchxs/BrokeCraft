#!/usr/bin/env python3
"""
Build texture atlas from Minecraft block textures.
Creates a compact atlas with only the blocks we need for the voxel engine.
"""

import os
from PIL import Image
import json

# Blocks we need (in order they'll appear in atlas)
REQUIRED_BLOCKS = [
    'stone.png',
    'dirt.png',
    'grass_block_top.png',
    'grass_block_side.png',
    'bedrock.png',
]

def build_atlas():
    # Paths
    textures_dir = os.path.join(os.path.dirname(__file__), 'Minecraft_Textures', 'block')
    output_atlas = os.path.join(os.path.dirname(__file__), 'VoxelBlockAtlas.png')
    output_mapping = os.path.join(os.path.dirname(__file__), 'VoxelBlockAtlas_Mapping.json')
    
    if not os.path.exists(textures_dir):
        print(f"ERROR: Directory not found: {textures_dir}")
        return
    
    # Load all textures
    textures = []
    texture_names = []
    
    for block_name in REQUIRED_BLOCKS:
        texture_path = os.path.join(textures_dir, block_name)
        
        if not os.path.exists(texture_path):
            print(f"WARNING: Texture not found: {block_name}")
            # Create placeholder (magenta for missing texture)
            img = Image.new('RGBA', (16, 16), (255, 0, 255, 255))
            textures.append(img)
        else:
            img = Image.open(texture_path).convert('RGBA')
            textures.append(img)
            print(f"Loaded: {block_name} ({img.size[0]}x{img.size[1]})")
        
        texture_names.append(block_name.replace('.png', ''))
    
    # Get texture size (assume all same size)
    if textures:
        tex_width = textures[0].size[0]
        tex_height = textures[0].size[1]
    else:
        print("ERROR: No textures loaded!")
        return
    
    # Calculate atlas dimensions (single row for simplicity)
    atlas_width = tex_width * len(textures)
    atlas_height = tex_height
    
    print(f"\nBuilding atlas: {atlas_width}x{atlas_height}")
    print(f"Texture size: {tex_width}x{tex_height}")
    print(f"Number of textures: {len(textures)}")
    
    # Create atlas image
    atlas = Image.new('RGBA', (atlas_width, atlas_height), (0, 0, 0, 0))
    
    # Paste textures into atlas (left to right)
    mapping = {}
    for i, (tex, name) in enumerate(zip(textures, texture_names)):
        x_offset = i * tex_width
        atlas.paste(tex, (x_offset, 0))
        mapping[name] = i
        print(f"  [{i}] {name} at x={x_offset}")
    
    # Save atlas
    atlas.save(output_atlas)
    print(f"\n[OK] Atlas saved: {output_atlas}")
    
    # Save mapping JSON
    with open(output_mapping, 'w') as f:
        json.dump(mapping, f, indent=2)
    print(f"[OK] Mapping saved: {output_mapping}")
    
    # Print summary
    print("\n" + "="*60)
    print("ATLAS BUILT SUCCESSFULLY!")
    print("="*60)
    print(f"Atlas dimensions: {atlas_width}x{atlas_height}")
    print(f"Grid size: {len(textures)}x1")
    print(f"Textures per block: {tex_width}x{tex_height}px")
    print("\nTexture indices:")
    for name, idx in mapping.items():
        print(f"  {idx}: {name}")
    print("\nNext steps:")
    print("1. Copy VoxelBlockAtlas.png to Unity: Assets/Textures/")
    print("2. Set Filter Mode to 'Point (no filter)'")
    print("3. Update VoxelData.cs: TextureAtlasSizeInBlocks = " + str(len(textures)))
    print("4. Update BlockTextureData.cs with these indices:")
    for name, idx in mapping.items():
        const_name = "TEX_" + name.upper().replace('_BLOCK', '')
        print(f"   public const int {const_name} = {idx};")

if __name__ == "__main__":
    build_atlas()

