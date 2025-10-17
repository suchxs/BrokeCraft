#!/usr/bin/env python3
"""
Build COMPLETE texture atlas from ALL Minecraft block textures.
Creates reference image with texture IDs for easy lookup.
Future-proof - all textures available!
"""

import os
from PIL import Image, ImageDraw, ImageFont
import json
import math

def build_full_atlas():
    # Paths
    textures_dir = os.path.join(os.path.dirname(__file__), 'Minecraft_Textures', 'block')
    output_atlas = os.path.join(os.path.dirname(__file__), 'CompleteBlockAtlas_Custom.png')
    output_reference = os.path.join(os.path.dirname(__file__), 'CompleteBlockAtlas_Reference.png')
    output_mapping = os.path.join(os.path.dirname(__file__), 'CompleteBlockAtlas_Mapping.json')
    
    if not os.path.exists(textures_dir):
        print(f"ERROR: Directory not found: {textures_dir}")
        return
    
    print("Scanning for ALL Minecraft block textures...")
    
    # Get ALL .png files from directory (sorted for consistency)
    all_files = sorted([f for f in os.listdir(textures_dir) if f.endswith('.png')])
    
    print(f"Found {len(all_files)} texture files")
    
    # Load all textures
    textures = []
    texture_names = []
    
    for i, block_name in enumerate(all_files):
        texture_path = os.path.join(textures_dir, block_name)
        
        try:
            img = Image.open(texture_path).convert('RGBA')
            textures.append(img)
            texture_names.append(block_name.replace('.png', ''))
            if (i + 1) % 100 == 0:
                print(f"  Loaded {i + 1}/{len(all_files)} textures...")
        except Exception as e:
            print(f"WARNING: Could not load {block_name}: {e}")
    
    print(f"Successfully loaded {len(textures)} textures")
    
    if not textures:
        print("ERROR: No textures loaded!")
        return
    
    # Get texture size (assume all same size)
    tex_width = textures[0].size[0]
    tex_height = textures[0].size[1]
    
    # Calculate optimal grid size (as square as possible)
    num_textures = len(textures)
    grid_width = math.ceil(math.sqrt(num_textures))
    grid_height = math.ceil(num_textures / grid_width)
    
    atlas_width = tex_width * grid_width
    atlas_height = tex_height * grid_height
    
    print(f"\nBuilding atlas:")
    print(f"  Grid: {grid_width}x{grid_height}")
    print(f"  Atlas: {atlas_width}x{atlas_height}px")
    print(f"  Texture size: {tex_width}x{tex_height}px")
    print(f"  Total textures: {num_textures}")
    
    # Create atlas image
    atlas = Image.new('RGBA', (atlas_width, atlas_height), (0, 0, 0, 0))
    
    # Create reference image (with IDs)
    reference = Image.new('RGBA', (atlas_width, atlas_height), (0, 0, 0, 0))
    
    # Paste textures into atlas (row by row)
    mapping = {}
    
    for i, (tex, name) in enumerate(zip(textures, texture_names)):
        x = i % grid_width
        y = i // grid_width
        x_offset = x * tex_width
        y_offset = y * tex_height
        
        # Paste into atlas
        atlas.paste(tex, (x_offset, y_offset))
        
        # Paste into reference and add ID text
        reference.paste(tex, (x_offset, y_offset))
        
        # Store mapping
        mapping[name] = i
        
        if (i + 1) % 100 == 0:
            print(f"  Processed {i + 1}/{num_textures} textures...")
    
    # Add ID numbers to reference image
    print("\nAdding ID labels to reference image...")
    draw = ImageDraw.Draw(reference)
    
    # Try to load a font, fall back to default if not available
    try:
        font = ImageFont.truetype("arial.ttf", 10)
    except:
        font = ImageFont.load_default()
    
    for i, name in enumerate(texture_names):
        x = i % grid_width
        y = i // grid_width
        x_offset = x * tex_width
        y_offset = y * tex_height
        
        # Draw ID number with background for readability
        text = str(i)
        
        # Black background
        draw.rectangle(
            [(x_offset, y_offset), (x_offset + tex_width//2, y_offset + 10)],
            fill=(0, 0, 0, 180)
        )
        
        # White text
        draw.text((x_offset + 2, y_offset), text, fill=(255, 255, 255, 255), font=font)
    
    # Save atlas
    atlas.save(output_atlas)
    print(f"\n[OK] Atlas saved: {output_atlas}")
    
    # Save reference
    reference.save(output_reference)
    print(f"[OK] Reference image saved: {output_reference}")
    
    # Save mapping JSON
    with open(output_mapping, 'w') as f:
        json.dump(mapping, f, indent=2)
    print(f"[OK] Mapping saved: {output_mapping}")
    
    # Print summary
    print("\n" + "="*70)
    print("COMPLETE ATLAS BUILT SUCCESSFULLY!")
    print("="*70)
    print(f"Atlas dimensions: {atlas_width}x{atlas_height}px")
    print(f"Grid size: {grid_width}x{grid_height}")
    print(f"Total textures: {num_textures}")
    print(f"Texture size: {tex_width}x{tex_height}px")
    
    # Find and print indices for blocks we need
    print("\n" + "="*70)
    print("TEXTURE INDICES FOR CURRENT BLOCKS:")
    print("="*70)
    
    needed_blocks = {
        'stone': 'TEX_STONE',
        'dirt': 'TEX_DIRT',
        'grass_block_top': 'TEX_GRASS_TOP',
        'grass_block_side': 'TEX_GRASS_SIDE',
        'bedrock': 'TEX_BEDROCK'
    }
    
    for block_name, const_name in needed_blocks.items():
        if block_name in mapping:
            print(f"{const_name:20s} = {mapping[block_name]:4d}  // {block_name}")
        else:
            print(f"{const_name:20s} = NOT FOUND // {block_name}")
    
    print("\n" + "="*70)
    print("NEXT STEPS:")
    print("="*70)
    print("1. Open CompleteBlockAtlas_Reference.png to see texture IDs")
    print("2. Copy CompleteBlockAtlas_Custom.png to Unity Assets/Textures/")
    print("3. Set texture Filter Mode to 'Point (no filter)'")
    print(f"4. Update VoxelData.cs:")
    print(f"   public const int AtlasWidth = {grid_width};")
    print(f"   public const int AtlasHeight = {grid_height};")
    print("5. Update BlockTextureData.cs with the indices shown above")
    
    # Print first 20 for quick reference
    print("\n" + "="*70)
    print("FIRST 20 TEXTURES (for quick reference):")
    print("="*70)
    for i, name in enumerate(texture_names[:20]):
        print(f"  [{i:3d}] {name}")

if __name__ == "__main__":
    build_full_atlas()

