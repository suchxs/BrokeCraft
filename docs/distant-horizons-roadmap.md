# Distant Horizon Integration Notes

## Snapshot — Baseline Understanding (2025-10-20)
- **Reference Mod (Minecraft Distant Horizons)**  
  - Clipmap stack of ever-larger LOD rings; each ring doubles sample spacing and reuses cached tiles.  
  - Far meshes are sourced from accumulated column summaries (height, biome tint, lighting) published by near chunks.  
  - All heavy lifting happens off the main thread; render thread swaps ready meshes.  
  - Persistent tile cache lets revisited areas stream instantly.

- **What I currently implement Today**  
  - `World` streams cubic chunks but exposes no summary data for far renderers.  
  - `DistantTerrainRenderer` resamples procedural noise directly every refresh, so it ignores real chunk edits and performs duplicate work.  
  - Only a single far surface exists (no clipmap tiers), so large radii require dense sampling and expensive rebuilds.

## Active Work — Iteration 1
- **Chunk Column Summaries** *(✅ 2025-10-20)*  
  - `ChunkColumnSummaryJob` now runs off the terrain output to collapse each X/Z stack into Burst-friendly metadata.  
  - `Chunk` raises the summaries via `World.NotifyChunkColumnSummaryReady`, making the data stream available without copying full voxel arrays.
- **Distant Renderer Ingestion Hook** *(✅ 2025-10-20)*  
  - `World` forwards summary events to `DistantTerrainRenderer`, which caches vertical slices and rebuilds a height override map for the horizon job.  
  - The distant height pass consumes these overrides while staying on worker threads, so near edits bleed into the far mesh without touching the main thread.
- **Far-Mesh Surface Tinting** *(✅ 2025-10-20)*  
  - Column summaries now feed per-column block tints into Burst jobs; `DistantTerrainMeshJob` writes vertex colors so the horizon matches near-field palettes.  
  - A `NativeHashMap<int2,float4>` keeps cached colors alongside heights, keeping the render thread free of conversions.

## Iteration 1 Progress — 2025-10-20
- Burst-backed column summaries mirror chunk updates, including manual block edits, and forward invalidation when chunks unload.  
- The distant renderer keeps per-column caches (managed + native height/tint maps) to blend real terrain profiles and colors into the kilometre-scale horizon.  
- Next follow-up: reuse cached summaries to seed clipmap layers and persist overrides between sessions.

## Next Candidates (Not Started)
- Compress and stream far-mesh tint maps alongside heights for clipmap reuse.  
- Introduce multi-layer clipmap controller feeding the renderer.  
- Persist reusable LOD tiles to disk.  
- Seam-morphing between near mesh and first clipmap ring.
