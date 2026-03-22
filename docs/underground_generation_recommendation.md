# Underground Generation Recommendation

## Decision Record

- Decision: redesign underground floor generation around a custom lightweight generator path that uses `MapGenerator.GenerateMap(...)` only for lifecycle integration and emits the final underground state directly through custom gen steps.
- Status: implemented and locally verified after review.
- Why:
  - current post-process path still pays for content it deletes
  - `pocketMapProperties` biome/temp are currently ignored
  - surface tile mutators still leak into underground generation
  - fog behavior is incorrect for a sealed map
  - a custom single-step or few-step generator path is already proven valid by vanilla DLC generators like `MetalHell` and `Labyrinth`

## Target Architecture

### Core direction

- Keep the current same-tile multi-map vertical world model.
- Keep `MapParent`-based floor ownership.
- Keep `MapGenerator.GenerateMap(...)` as the integration point.
- Replace the current underground generation service with custom gen steps that generate the final underground map state in one pass.

### Recommended generator shape

- `DeepRimVertical_Underground`
  - `isUnderground=true`
  - no vanilla surface gen steps
  - no mutator steps from the surface tile path
  - explicit custom gen steps only

Suggested logical phases:

1. `DeepRimVertical_UndergroundLayout`
   - compute entry pocket
   - compute cavern masks for `-1/-2`
   - compute solid-rock mask for deeper floors
   - compute ore mask
   - compute hazard/thermal masks for deep hot floors

2. `DeepRimVertical_UndergroundMaterialize`
   - write terrain where needed
   - spawn rock/resource things only once
   - apply roof intent once
   - avoid any destroy/rebuild cycle

3. `DeepRimVertical_UndergroundFinalize`
   - establish fog roots around the entry
   - apply final cleanup invariants
   - stamp floor metadata needed by runtime systems

## What To Keep

- mask-driven layout computation
- depth-based content scaling
- custom geological temperature model
- dedicated underground floor generator def
- same-tile vertical map parent architecture

## What To Remove

- `WipeMapFast(...)` as a normal generation step
- dependence on `Underground_RocksFromGrid` for final state
- dependence on `MutatorPostElevationFertility`
- dependence on `MutatorPostTerrain`
- dependence on `MutatorFinal`
- reliance on `Fog` without explicit unfog roots
- random rock selection from unrestricted global natural rock pool

## What To Rewrite Fully

- `UndergroundGenerationService`
  - from post-generation rewrite service
  - into true underground layout/materialization builder

- underground generator def
  - from a partially vanilla path
  - into a strictly curated custom path

- fog initialization for underground floors
  - from generic `GenStep_Fog`
  - into explicit underground entry unfogging

## Handling Biome and Runtime Context

The review found that the current underground biome def is not active at runtime for these maps.

Recommended direction:

- do not depend on surface tile biome semantics for underground floors
- make underground runtime context explicit

Two feasible ways exist:

1. Primary recommendation:
   - keep floors as normal `MapParent` maps
   - patch or provide underground-specific runtime context where biome/weather-dependent logic must diverge
   - keep the generator itself independent from `Map.Biome.extraGenSteps` and tile mutator chains

2. Secondary fallback experiment:
   - evaluate using pocket-map semantics for underground floors in an isolated branch
   - only proceed if persistent-colony compatibility is proven acceptable

Why option 1 is preferred:

- it preserves the current vertical architecture
- it avoids importing pocket-map lifecycle assumptions into a permanent colony floor model

## Performance Strategy

### Main rule

- Never generate content only to destroy it.

### Concrete recommendations

- replace the current post-process pipeline with direct materialization
- avoid `listerThings.AllThings.ToList()` cleanup passes during normal generation
- avoid full-map spawn/destroy churn
- precompute masks in contiguous arrays once per generation
- keep one whole-map finalization pass only
- use deterministic depth-band rules rather than expensive search-heavy adaptive logic
- keep lava/hot-depth content optional and sparse

### Expected performance improvement areas

- lower allocations during generation
- fewer region/room/path dirty updates from mass destruction
- fewer long frame stalls from spawn/destroy churn
- less DLC incidental content to clean up

## Sealed Map Rules

- map edges must always end in solid rock
- underground cavities remain roofed unless the design explicitly creates a vertical shaft
- no open-sky interpretation for underground voids
- `-1/-2` may contain controlled cavern pockets
- `-3` and deeper should bias hard toward solid rock with ore veins
- hot-depth content must be explicitly gated by depth band and local support

## Migration Plan

1. Freeze the current generator contract in docs and tests.
2. Introduce new custom underground gen step classes without deleting the current path yet.
3. Build a new lightweight underground `MapGeneratorDef` that references only custom underground steps.
4. Move layout computation from `UndergroundGenerationService.Generate(...)` into the new gen-step pipeline.
5. Replace post-generation wipe/respawn with direct one-pass materialization.
6. Add explicit underground fog-root handling.
7. Lock geology selection to a deterministic site rule instead of broad random global rock selection.
8. Re-verify temperature behavior against the new generator/runtime context.
9. Remove deprecated wipe-based code once the new path is stable.

## Risks and Limitations

- Solid underground maps still require many rock thing spawns because of RimWorld's representation model.
- Runtime biome/weather semantics for non-pocket same-tile underground maps remain a compatibility-sensitive area.
- Some storyteller and arrival behaviors may still need explicit underground gating after generator redesign.
- Hot-depth lava content can easily become a performance trap if coverage is too high.
- Same-tile multi-map architecture remains outside vanilla's main happy path, so regression testing must stay broad.

## Final Recommendation

- Rewrite underground generation now.
- Do not spend more iteration budget on tuning the current wipe-and-respawn architecture.
- Use a dedicated lightweight generator path with custom underground builder steps.
- Treat vanilla underground and pocket generators as references, not as reusable bases.
