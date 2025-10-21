# Ideal Roadmap

## Phase 1 — Core Data Flow (✔ In Progress)
**Goal**: Establish all CPU-side data structures and make terrain summaries available to the renderer.
### 1. Column Summaries
- ✅ Implemented `ChunkColumnSummaryJob` collapsing vertical stacks into:
  - Height samples (max Y of solid block)
  - Biome/grass tint (float4)
  - Skylight & blocklight averages
  - “Surface normal proxy” (Δheight between neighbor columns)
- ✅ Forward summaries via `World.NotifyColumnSummaryReady`.
- 🔜 Maintain a “chunk summary ring buffer” per player region for rapid invalidation.
- 🔜 Add slope/normal approximations per column (for lighting)
- 🔜Maintain a “column summary ring buffer” around player for quick invalidation.

### 2. Renderer Ingestion
- ✅ `DistantTerrainRenderer` caches summaries in Native containers (Burst-safe).
- ✅ Height/tint sampling decoupled from terrain noise; edits trigger differential updates.
- 🔜 Implement *dirty-region rebuilds* — only rebuild mesh patches where summaries changed.

---

## Phase 2 — Clipmap System
**Goal**: Build a scalable LOD ring hierarchy like DH’s “clipmap stack.”
### 3. Multi-Ring Clipmap Controller
- Design a stack of rings (LOD0..LOD3+), each doubling its grid spacing:
  - LOD0: 16×16 (same as world chunks)
  - LOD1: 32×32 (merges 2×2 summaries)
  - LOD2: 64×64
  - etc.
- Each ring has:
  - Its own job queue for rebuilds
  - A “tile cache” storing generated meshes
  - A world-to-ring coordinate transform to reuse tiles as player moves
  - Each ring defines a "morph zone width" (distance from player before switching LOD).
  - Near the morph boundary, both LOD meshes render together with a blend factor.
  - The renderer gradually increases/decreases the opacity of the new mesh over 0.5–1s.
- Implement *clipmap scrolling* — when player crosses half a ring’s width, shift and reuse cached tiles instead of regenerating everything.
- Implement ring transition logic (when to load/unload tiles).
- For LOD1+: aggregate column data by sampling only the topmost solid block per cell group (2×2 or 4×4).
  - Average heights, normals, and colors across those cells to form simplified vertices.
  - Skip non-visible surfaces entirely (hidden below height threshold).
- Implement color/tint averaging during merge.
- Add debug view to visualize ring boundaries and tile states.

---

## Phase 3 — Rendering & Streaming
**Goal**: Offload terrain mesh generation & GPU uploads like DH’s “CPU Load” model.
### 4. Async Background Streaming (CPU-side)
- Create a Burst job queue system (LODMeshBuilderJob).
- Limit concurrent workers by user setting (like DH “CPU Load”).
- Prioritize near-ring tiles first.
- Cache generated vertex/index buffers in temporary native arrays.
- Handle cancellations for tiles invalidated before completion.
- Use a priority queue based on tile distance to camera.
  - Highest priority: near-ring tiles within player FOV.
  - Lowest priority: tiles fully behind camera or farthest ring.
  - Prevents frame spikes when turning quickly or flying fast.
  
### 5. Async GPU Swap (Render-side)
- Separate “generation” from “swap”:
  - Worker threads run Burst jobs to bake vertex/index buffers.
  - Main thread only enqueues GPU upload + replaces references atomically.
- Implement a `ReadyMeshQueue` so rendering never blocks generation.
- Use **Mesh LOD blending** (crossfade or vertex morphing) to hide ring transitions.
- Never block rendering — rendering always uses last known valid mesh.

### 6. Frustum & Distance Culling
- Each ring maintains a per-frame visible tile list.
- GPU-side culling (ComputeShader or Burst job) trims unseen tiles before submission.
- Optional: add *ring mask LOD fading* for visual consistency (like DH smooth transitions).

---

## Phase 4 — Persistence & Streaming
**Goal**: Avoid regenerating terrain repeatedly; persist tile data on disk.
### 7. LOD Tile Cache (Memory + Disk)
- On-disk cache (SQLite or binary blob):
  - Key: `(ring, tile_x, tile_z, world_seed, version)`
  - Value: compressed vertex buffer + tint map.
- In-memory cache for nearby tiles; unload least-recently-used.
- Async background streaming (similar to DH “CPU load” slider).
- Preload tiles from disk during startup (non-blocking).
- Optional: compress height/tint maps using LZ4 or delta coding.
- Store generator version and seed hash in metadata header.
  - When mismatched, invalidate cached tile and rebuild.
  - Avoids loading incompatible geometry when world seed changes.
---

## Phase 5 — Seamless Integration
**Goal:** Fix visual seams and blend near/far transitions like DH’s ring morphing.
### 8. Seam Morphing
- Generate vertex morph data between near chunks and first clipmap ring.
- Blend heights and tints in a radius near player.
- Optional: GPU-based morph (vertex shader lerp).
- Add fade transitions when swapping LODs.
- Implement horizon color blending for biome transitions.
- Match horizon color to world fog color dynamically.
  - Add distance-based color fade in shader (fog blending).
  - Optional: skybox ambient reflection influence on far terrain.

### 9. Dynamic Update Propagation
- When a chunk changes:
  - Update column summary immediately.
  - Mark dependent LOD tiles dirty up the clipmap hierarchy.
  - Background rebuild those tiles progressively (lowest priority).
- Use averaged skylight/blocklight from summaries to color distant meshes.
- Add optional fake “sunlight shadow” gradient (DH does per-column shade).
- Bake vertex ambient occlusion (simple height delta AO).
- Add tone-mapping to make distant terrain match near fog color.
---

## Phase 6 — Polishing & Optimization
**Goal**: Match DH’s runtime smoothness, culling, and scalability.
- Implement GPU frustum culling per ring.
- Add LOD fade masks (smooth fade out at horizon).
- Reuse compute buffers to avoid GC pressure.
- Profile job scheduling and GPU upload stalls.
- Add debug metrics HUD:
  - Active tiles per ring
  - Cache hit ratio
  - Worker queue depth
  - Average generation time
  - LOD ring generation time (avg ms)
  - GPU upload queue length
  - Tile cache hit/miss percentage
  - Morph blend ratio (active LODs overlap)
- Optional: async compute shader for LOD merging.
