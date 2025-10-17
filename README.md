# UnoptimizedCraft
> ⚠️ **Chunk Generation and Logic is still unstable and crap** - Whole Chunk Generation and Voxel Logic is still being rewritten <br>

A high-performance Minecraft-style voxel engine written in C# + Unity, leveraging Cubic Chunks, LOD, Frustum & Occlusion Culling, and Burst Compiler optimizations to achieve massive render distances and unlimited world height — without vanilla C# overhead. The idea is to combine Distant Horizons Level Details (Removing minecraft render distance) and Cubic Chunks (Remove build limit of minecraft)


## How does this shit work?
### 1. Voxels & Rendering
- Voxels are stored in a contiguous array:



Special Thanks to:
- https://www.youtube.com/watch?v=QF2Nj1zME40
- https://www.alanzucconi.com/2022/06/05/minecraft-world-generation/
- https://minecraft.fandom.com/wiki/Blocks.png-atlas
- https://www.youtube.com/watch?v=wbpMiKiSKm8
- https://rtouti.github.io/graphics/perlin-noise-algorithm
- https://minecraft.fandom.com/wiki/Noise_generator
- https://booth.pm/en/items/3226395
