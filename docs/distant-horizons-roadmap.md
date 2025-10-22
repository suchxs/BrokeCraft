# Distant Horizon Integration Notes

## Snapshot — Baseline Understanding (2025-10-20)
- **Reference Mod (Minecraft Distant Horizons)**  
  - Clipmap stack of ever-larger LOD rings; each ring doubles sample spacing and reuses cached tiles.  
  - Far meshes are sourced from accumulated column summaries (height, biome tint, lighting) published by near chunks.  
  - All heavy lifting happens off the main thread; render thread swaps ready meshes.  
  - Persistent tile cache lets revisited areas stream instantly.

## Iteration 1 — 2025-10-20
- `ChunkColumnSummaryJob` collapses each X/Z stack into Burst-friendly metadata and `Chunk` raises summaries via `World.NotifyChunkColumnSummaryReady`, avoiding full voxel copies.
- `World` forwards summary events to `DistantTerrainRenderer`, which caches vertical slices and rebuilds the height override map while staying off the main thread.
- Column summaries feed per-column block tints into Burst jobs so distant meshes inherit near-field palettes, with cached overrides living in native containers for reuse.

## Iteration 2 — 2025-10-22
- Packed the horizon tint map into 32-bit color values before handing data to Burst jobs, shrinking the native cache footprint and lowering copy bandwidth.
- The distant mesh job now unpacks these packed colors on worker threads, keeping vertex color writes Burst-compatible without extra allocations.

## Next Candidates (Not Started)
- Maintain a chunk summary ring buffer around the player for faster invalidation when streaming.  
- Add slope/normal approximations per column to improve distant lighting without height re-sampling.  
- Implement dirty-region rebuilds so only affected mesh patches regenerate when summaries change.  
- Stream the packed tint map alongside height data into clipmap-ready tiles for reuse.  
- Introduce multi-layer clipmap controller feeding the renderer.  
- Persist reusable LOD tiles to disk.  
- Seam-morphing between near mesh and first clipmap ring.
