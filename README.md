# UnoptimizedCraft
> ⚠️ **Work In progress** - I will post an update when everything is in order <br>
> ⚠️ **Chunk Generation and Logic is still unstable and crap** - Whole Chunk Generation and Voxel Logic is still being rewritten <br>

A high-performance Minecraft-style voxel engine written in C# + Unity, leveraging Cubic Chunks, LOD, Frustum & Occlusion Culling, and Burst Compiler optimizations to achieve massive render distances and unlimited world height — without vanilla C# overhead. The idea is to combine [Distant Horizons](https://www.curseforge.com/minecraft/mc-mods/distant-horizons) Level Details (Removing Minecraft render distance) and [Cubic Chunks](https://www.curseforge.com/minecraft/mc-mods/opencubicchunks) (Remove build limit of minecraft)


## How does this shit work?
### 1. Voxels & Rendering
- Voxels are stored in a contiguous array, Each block generates only the visible faces (face culling) and Textures are drawn from a single atlas to reduce draw calls.
- Each chunk is built into a single combined mesh instead of instantiating cubes individually. The mesh can be split per side to allow side-based culling (disabling unseen faces).

### 2. Mesh & Vertex Optimization
- Vertex positions are whole numbers ((0,0,0), (1,0,0)), allowing them to be stored as bytes, which drastically reduces vertex memory footprint.
- Mesh data is generated directly in memory for maximum Burst Compatibility

### 3. Chunk System (Cubic Chunks)
- Replaces Minecraft's flat chunk system and uses cubic chunks to support infinite vertical expansion
- Chunk load **dynamically** both horizontally and vertically
- Each vertical column loads or unloads based on the player’s height.

## Optimization 
### Burst Compiler
- All Code must be Burst-Compatible instead of Vanilla c#
### Occlusion Culling and LOD
- LOD meshes are combined to reduce draw calls
- Hidden chunks (behind terrain) are not rendered at all
### Greedy Meshing
- Adjacent faces are merged into larger quads which greatly reduce triangle count
### Multithreading (finally lol)
- All world generation and mesh creation occur off the main thread (using Unity Job System and C# Task System)
- Terrain and Chunk Data are parallelized to use multiple cores effectively

## Terrain Generation
### Perlin Noise and Fractal Brownian Motion
- Inspired by [Sebastian Lague's Procedural Landmass Generation Guide](https://github.com/SebLague/Procedural-Landmass-Generation)
- Terrain height = exponential function of noise (for natural mountain ranges)
### Performance
- Integrated FastNoiseSIMD, for fast multi-core noise evaluation
- Parallelized accross worker threads for near-instant chunk generation


  
Special Thanks to:
- https://www.youtube.com/watch?v=QF2Nj1zME40
- https://www.alanzucconi.com/2022/06/05/minecraft-world-generation/
- https://minecraft.fandom.com/wiki/Blocks.png-atlas
- https://www.youtube.com/watch?v=wbpMiKiSKm8
- https://rtouti.github.io/graphics/perlin-noise-algorithm
- https://minecraft.fandom.com/wiki/Noise_generator
- https://booth.pm/en/items/3226395
