# Underground Generation Review

## Scope

- Task: review and redesign underground floor generation for DeepRim Vertical on RimWorld `1.6.4633 rev1260`.
- Review completed. The recommended architecture from this review has since been implemented and verified in local gameplay testing.
- Source of truth:
  - local mod source in `G:\rimworld\Source`
  - local mod defs in `G:\rimworld\1.6\Defs`
  - local RimWorld install in `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
  - local managed assembly `Assembly-CSharp.dll`
  - local installed DLC: `Royalty`, `Ideology`, `Biotech`, `Anomaly`, `Odyssey`

## Confirmed Runtime Facts

### Map generation runtime

- `Verse.MapGenerator.GenerateMap(...)` seeds and constructs the `Map`, starts initial weather, runs ordered gen steps, then finalizes the map.
- If `MapGeneratorDef.isUnderground=true`, `GenerateMap(...)` pre-fills every cell with `RoofRockThick` or `mapGenerator.roofDef` before gen steps.
- `pocketMapProperties.biome` and `pocketMapProperties.temperature` are only applied when `GenerateMap(..., isPocketMap: true, ...)` is used.
- For non-pocket maps, `Map.TileInfo` comes from the world tile, and `Map.Biome` is `TileInfo.PrimaryBiome`.
- `MapTemperature.OutdoorTemp` and `SeasonalTemp` use `pocketMapProperties.temperature` only for pocket maps; normal maps use world tile temperature.
- `MapGenerator` also injects:
  - `map.TileInfo.Mutators` extra/post gen behavior
  - `map.Biome.extraGenSteps`
  - `map.Biome.preventGenSteps`

### Fog behavior

- `GenStep_Fog` unfogs from `MapGenerator.PlayerStartSpot` if present.
- If no start spot exists, it tries to unfog from a standable, unroofed, edge-reachable cell.
- A fully sealed underground map with all edges rock and all walkable cells roofed can therefore end up fully fogged unless the generator explicitly unfogs entry areas or sets roots to unfog.

### Vanilla underground-relevant gen steps

- `GenStep_RocksFromGrid` spawns natural rock things based on elevation and cave noise, applies natural roofs, then scatters mineable lumps.
- `GenStep_Terrain` writes terrain for every cell and can destroy edifices that sit on invalid terrain.
- `GenStep_MutatorPostElevationFertility`, `GenStep_MutatorPostTerrain`, `GenStep_MutatorCriticalStructures`, `GenStep_MutatorNonCriticalStructures`, `GenStep_MutatorFinal` all execute tile mutator workers from `map.TileInfo.Mutators`.
- `GenStep_Plants` iterates all cells in random order and asks `wildPlantSpawner` to populate them.
- `GenStep_Animals` loops until ecosystem is full or hits the hard `10000` iteration guard.
- `GenStep_PlaceCaveExit` clears a pocket and spawns an actual `CaveExit`.

## Current Mod Implementation Review

### Current pipeline

- Floor creation lives in `Source\VerticalMaps\VerticalMapCreationService.cs`.
- Underground preparation lives in `Source\VerticalMaps\UndergroundGenerationService.cs`.
- Underground floors use `MapGenerator.GenerateMap(sourceMap.Size, mapParent, mapParent.MapGeneratorDef, ..., isPocketMap: false, ...)`.
- The active underground `MapGeneratorDef` is `DeepRimVertical_Underground`.
- After `GenerateMap(...)`, the mod calls `UndergroundGenerationService.Generate(...)`.

### Current underground generator def

- `DeepRimVertical_Underground`
  - `isUnderground=true`
  - `pocketMapProperties.biome=DeepRimVertical_UndergroundBiome`
  - `pocketMapProperties.temperature=0`
  - gen steps:
    - `ElevationFertility`
    - `MutatorPostElevationFertility`
    - `Underground_RocksFromGrid`
    - `Terrain`
    - `MutatorPostTerrain`
    - `MutatorFinal`
    - `Fog`

### What the mod does after generation

- Destroys all non-pawn things on the generated map.
- Picks a random natural rock def from all loaded defs.
- Builds in-memory masks for:
  - open pocket
  - shallow caverns on `-1` and `-2`
  - optional lava rivers
  - resource deposits
- Iterates the whole map and:
  - keeps thick roof everywhere
  - sets lava/volcanic terrain for marked cells
  - respawns rock or resource thing on every non-open cell

## Confirmed Architectural Problems

### 1. The generator is still post-factum, not truly underground-first

- The mod still asks vanilla to generate an underground-ish map first.
- It then destroys most of the generated result.
- It then rebuilds the desired final state on top.
- This is a redesign improvement relative to a surface generator, but it is not the requested production-ready architecture.

### 2. `pocketMapProperties` biome and temperature are currently ignored

- Underground floors are generated with `isPocketMap: false`.
- Therefore:
  - `DeepRimVertical_UndergroundBiome` is not the map biome at runtime
  - `pocketMapProperties.temperature=0` is not used by vanilla temperature logic
- The underground map continues to inherit the surface tile biome and tile mutators unless separately patched around.
- This is a root cause candidate for:
  - inappropriate weather context
  - biome-derived behavior mismatches
  - surface tile mutator leakage

### 3. Surface tile mutators still run during the underground generator path

- The current underground generator includes:
  - `MutatorPostElevationFertility`
  - `MutatorPostTerrain`
  - `MutatorFinal`
- These steps run against `map.TileInfo.Mutators`, which for these maps is still the surface world tile.
- Result: underground floors can still inherit surface/coast/river/cave/mountain or DLC mutator behavior that does not belong to a sealed vertical floor.

### 4. Fog generation is logically wrong for a sealed floor

- The current underground generator has `Fog`.
- It does not set `MapGenerator.PlayerStartSpot`.
- It does not add roots to unfog.
- It then seals the map post-factum.
- Result: sealed underground maps have no reliable unfog root and are vulnerable to fully fogged or illogically fogged states.

### 5. The performance model is still dominated by spawn/destroy churn

- `Underground_RocksFromGrid` already spawns rock and resource lumps.
- `WipeMapFast` then destroys virtually all of it.
- `ApplyFinalState` respawns rock/resource things over most cells.
- On a large map, the current pipeline pays for:
  - vanilla map setup
  - vanilla underground rock spawning
  - optional mutator work
  - destruction of most spawned things
  - full-map respawn of sealed rock state
- This is the clearest local hotspot for stalls and apparent hangs during `-1` creation.

### 6. Rock choice is nondeterministic and overly broad

- `ChoosePrimaryRockDef()` samples from all loaded natural rock defs.
- That is broader than the source tile geology and broader than a curated underground palette.
- It can produce cross-DLC or thematically inconsistent rock selection for the same site.

### 7. Temperature is currently patched after the fact

- The mod patches:
  - `MapTemperature.OutdoorTemp`
  - `MapTemperature.SeasonalTemp`
  - `GenTemperature.GetTemperatureForCell`
  - `RoomTempTracker.DeepEqualizationTempChangePerInterval`
- This compensates for the fact that the map is not a true underground-biome map in vanilla terms.
- It improves symptoms, but it confirms that the core generator/runtime context is still not aligned with underground semantics.

## Generator Inventory

| Generator | Type | Underground flag | Pocket biome/temp | Main path | Applicability to vertical underground floors |
| --- | --- | --- | --- | --- | --- |
| `Base_Player` + `MapCommonBase` | Surface colony | No | No | Surface terrain, roads, ruins, geysers, scenario, plants, animals, DLC debris | Unsafe. Pulls broad surface and DLC content. |
| `Encounter` + `MapCommonBase` | Lightweight surface encounter | No | No | Same common base, fewer explicit top-level steps | Unsafe as a general underground base. Still surface tile biome/mutator context. |
| `DeepRimVertical_Underground` | Current mod path | Yes | Defined but unused at runtime | `ElevationFertility`, `Underground_RocksFromGrid`, terrain, mutator post steps, fog, then full post-process | Better than surface path, but still architecturally wrong and expensive. |
| `Undercave` | DLC underground pocket | Yes | `Undercave`, `15C`, `UndergroundCave` mutator | Underground rocks + ruins + mutators + flesh features + cave exit + plants | Good reference, not reusable as-is. Contains unwanted special content. |
| `InsectLair` | DLC underground pocket | Yes | `Underground`, `15C`, `UndergroundCave` mutator | Underground rocks + ruins + cave exit + insect structures + plants | Good reference, not reusable as-is. |
| `AncientStockpile` | DLC underground pocket | Yes | `Underground`, `15C` | Minimal underground rocks + terrain + chunks + fog | Strongest vanilla reference for a lightweight underground path, but still not sealed-by-design. |
| `MetalHell` | DLC special underground pocket | Yes | `MetalHell`, `31C` | Single bespoke gen step | Useful proof that a one-step custom underground generator is valid. Content itself is not reusable. |
| `Labyrinth` | DLC special pocket | No underground flag | `Labyrinth`, `25C` | Single bespoke gen step | Useful proof of custom single-step path. Not semantically relevant. |
| `SpacePocket` / `Space` family | Pocket/special | No | Yes | Bespoke space gen | Useful for pocket biome semantics, not for geology. |
| `Mechhive` | Space special | No | Inherited from space path | Bespoke orbital content | Not applicable to underground floors. |

## Safe / Unsafe Vanilla Pieces

### Safe or conditionally safe references

- `MapGenerator.GenerateMap(...)` itself:
  - good lifecycle hook for map construction, components, save integration, weather bootstrapping, finalization
  - safe if the gen step list is kept intentionally small
- `isUnderground=true`:
  - useful because it pre-roofs the map consistently
- `AncientStockpile` as a reference shape:
  - demonstrates a much lighter underground path than surface generation
- Single bespoke gen step generators such as `MetalHell` and `Labyrinth`:
  - confirm that a custom, low-step generator path is a supported pattern

### Unsafe or not directly reusable

- `MapCommonBase`
- `Base_Player`
- `Encounter`
- `Undercave`
- `InsectLair`
- any gen step chain that includes:
  - roads
  - surface ruins
  - geysers
  - scenario parts
  - plants
  - animals
  - cave exits
  - mutator critical/noncritical structure generation

### High-risk gen steps for sealed vertical floors

- `MutatorPostElevationFertility`
- `MutatorPostTerrain`
- `MutatorCriticalStructures`
- `MutatorNonCriticalStructures`
- `MutatorFinal`
- `Plants`
- `Animals`
- `ScenParts`
- `PlaceCaveExit`
- `Underground_ScatterRuinsSimple`
- `RockChunks`

These are high-risk because they either:

- depend on surface tile mutators or biome context
- inject unwanted content
- assume open/encounter cave design rather than sealed colony floors
- create more spawn/destroy cost with little value

## Performance Analysis

### Cost of current approach

The current path pays three different generation costs:

1. `MapGenerator.GenerateMap(...)` map lifecycle and gen step execution.
2. `WipeMapFast(...)` destruction of nearly all generated content.
3. `ApplyFinalState(...)` full-map respawn of natural rock/resource things.

The most expensive local patterns are:

- whole-map `GenSpawn.Spawn(...)` for sealed rock cells
- broad destroy loops over `listerThings.AllThings`
- generating content that is intentionally thrown away
- repeated region/room/path dirtying side effects from large thing churn

### Cost of vanilla `MapGenerator.GenerateMap(...)`

- Baseline map lifecycle cost is acceptable and gives stable integration with RimWorld internals.
- The problem is not `GenerateMap(...)` by itself.
- The problem is feeding it a step list that generates content the mod does not actually want.

### Cost of current post-process approach

- High.
- It is the worst of both worlds:
  - still pays for vanilla generation
  - still pays for custom sealing
- This is the leading suspect for the reported creation stalls.

### Cost of a mask-driven approach

- The current mod already proves that mask-driven layout computation is cheap enough.
- In-memory masks for open cells, deep hazards, ore, and roof intent are the right direction.
- The failure is not the mask phase.
- The failure is using masks only after expensive vanilla content has already been created.

## Findings Summary

### Confirmed

- The current underground path is not surface generation anymore.
- But it is also not a true underground-first architecture yet.
- Underground biome/temperature from `pocketMapProperties` are currently not applied.
- Surface tile mutators still influence the map.
- Fog logic is not coherent for a fully sealed floor.
- The current hot path is heavy destroy/respawn churn.

### Main redesign implication

- The next architecture should keep RimWorld map lifecycle integration.
- It should stop using a generator path that produces disposable content.
- It should build the final underground state directly in one custom, lightweight generator path.
