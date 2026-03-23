# Upper Floor Support And Overlay Architecture

## Confirmed patch points

- `RimWorld.Designator_Build.CanDesignateCell(IntVec3)`
- `RimWorld.GenConstruct.CanPlaceBlueprintAt(...)`
- `Verse.MapDrawer.DrawMapMesh()`
- `Verse.Thing.SpawnSetup(Map, bool)`
- `Verse.Thing.DeSpawn(DestroyMode)`

## Services

- `UpperFloorGenerationService`
- `GhostStackQueryService`
- `VerticalGhostRenderer`
- `VerticalRenderFilterPolicy`
- `VerticalSupportService`
- `VerticalMapInvalidationService`

## Support model

`v1` uses a practical graph:

1. Immediate lower floor provides seed cells.
2. Seed cells come from four gameplay sources:
   - support-like structures
   - aligned portal / stair / shaft cells
   - roofed cells on the floor below
   - connected supported structural cells on positive floors below
3. Support expands horizontally by the configured overhang radius.
4. Already supported structural elements on the active upper floor can extend the graph further.

Default overhang: `5`

## Gameplay reading

- `roof below` gives a default slab and an intuitive first pawn-working area
- `stair / shaft core` gives the guaranteed bootstrapping point even if the lower room is small
- `supports / walls / columns` are what let the player move beyond the initial slab

This solves the bootstrap problem:

- pawns do not arrive on a purely abstract empty map
- the player has a predictable first place to stand and build
- the next expansion step is visually explained by the lower-floor ghost overlay

## Cache strategy

Cached:

- buildable mask per map
- seed cells per map
- connected structural cells per map
- ghost cell lists per active upper floor

Invalidated by:

- floor creation
- portal registration
- thing spawn
- thing despawn
- overlay mode toggles
