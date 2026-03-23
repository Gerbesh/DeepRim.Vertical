# Upper Floor Creation Model

## Source of truth

- Local RimWorld install: `1.6.4633 rev1260`
- Confirmed runtime signature:
  - `Verse.MapGenerator.GenerateMap(IntVec3, MapParent, MapGeneratorDef, IEnumerable<GenStepWithParams>, Action<Map>, bool, bool)`
- Previous positive-floor path reused the source surface generator and only cleaned the result afterwards.

## Decision

Positive floors now use a dedicated `DeepRimVertical_UpperFloor` generator path.

- They stay separate `Map` instances.
- They do not inherit the source surface `MapGeneratorDef`.
- They do not execute vanilla surface, biome, ruin, plant, animal, geyser or DLC gen steps.
- They initialize as upper construction layers:
  - no procedural content
  - no generated things
  - no generated roof of their own
  - default slab-floor semantics above already roofed cells below
  - an anchor-driven construction foothold around the stair / shaft entry point

## Runtime path

1. `VerticalMapCreationService.TryCreateFloor(...)`
2. `VerticalDefOf.DeepRimVertical_UpperFloor`
3. `UpperFloorGenerationService.PrepareForGeneration(...)`
4. `GenStep_DeepRimVerticalUpperFloorInit.Generate(...)`
5. `UpperFloorGenerationService.InitializeEmptyConstructionLayer(...)`

## Why this is the correct model

- Upper floors stop feeling like fresh outdoor maps.
- Lower-floor ghost data is no longer competing with soil-like surface content.
- Support validation can reason over a stable architectural layer instead of a half-cleaned surface map.

## Gameplay rule: floor equals roof from below

For gameplay, the default upper-floor walk/build surface is not "natural ground". It is the slab implied by the floor below.

- If a cell on the lower floor is roofed, the cell above is treated as having a default floor slab.
- This creates immediate usable upper-floor deck over finished rooms below.
- The player can later replace the tile with another floor type.

This means the first useful `+1` floor usually appears above already completed covered rooms, not above open courtyards.

## Gameplay rule: how construction starts on a new floor

The new floor must always have a practical starting foothold.

- The aligned stair / shaft / portal core is the primary start point.
- Roofed rooms below provide the broad default floor slab.
- Support-like structures below provide the structural seed for outward building.

Recommended player loop:

1. Build a stair / shaft core from a finished room below.
2. Create the upper floor.
3. Start from the core landing and the slab above the roofed room.
4. Extend walls / supports / columns to grow the support graph.
5. Only then cantilever outward into new space.
