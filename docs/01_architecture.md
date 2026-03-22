# 01 Architecture

## Chosen world model

- One vertical colony site spans multiple `Map` instances.
- All floors share:
  - one world tile
  - one XY grid
  - one `VerticalSiteRecord`
- Floor `0` is the adopted surface map.
- Non-surface floors are represented by custom `MapParent` world objects on the same tile.

## Why this model

- It preserves vanilla map internals instead of inventing a fake 3D cell engine.
- It gives stable save/load boundaries through existing `MapParent` and `Map` serialization.
- It keeps same-level behavior vanilla by default.
- It allows cross-level systems to be layered on top:
  - pathing graph over portals
  - LOS/projectile resolver over shared XY projection
  - utility links over floor/site registry

## Current modules

- `VerticalWorld`
- `VerticalMaps`
- `VerticalUI`
- `Persistence`
- `Settings`
- `VerticalTemperature`

## Underground floor generation policy

- Floor `-1` and `-2` are generated as rock-sealed underground maps with:
  - a carved entry pocket around the anchor cell
  - a small number of caverns away from the entry
  - scattered mineable resource deposits
- Floor `-3` and deeper are generated as almost entirely solid rock:
  - thick rock roof across the whole map
  - rock-filled edges
  - resource deposits embedded into the rock mass
  - no open map-edge lanes
- Hot deep floors soft-enable lava river generation when a lava terrain exists in loaded defs.
  - Current soft-compat target is Odyssey-style lava terrain such as `LavaDeep`.
- Generation is now mask-driven rather than destructive multi-pass.
  - The generator computes open cells, resource cells and lava cells in memory first.
  - It then applies the final map state in one pass.
  - This avoids repeated spawn/destroy cycles on the same cells.
- Underground empty space is still cavern space, not open sky.
  - Current underground generator keeps `RoofRockThick` over carved pockets and caverns.
  - Roofless vertical openings are reserved for explicit portal/shaft objects later.
- Underground floors now use a dedicated mod `MapGeneratorDef` instead of inheriting the surface map generator.
  - This prevents surface-specific gen steps and special content from leaking into underground floors.
  - It also marks underground floors as `isUnderground=true` for vanilla systems that key off generator metadata.

## Rejected alternatives

### Single map with fake z-layers

- Rejected because it would require invasive rewrites across reachability, rooming, grids, draw layers, line of sight and selection.

### World map sub-tiles or pocket maps for every floor

- Rejected because coordinate projection and cross-level queries become much harder.

## Current risks

- RimWorld world object APIs are not designed around many player-home `MapParent`s on one tile.
- Same-tile multi-map pathing is not provided by vanilla; cross-level movement will require a dedicated route planner layer.
- Cross-level combat cannot be solved by a single `GenSight` patch; it needs a targeted extension pipeline.
- Underground generation still post-processes its generated map into the final sealed-rock shape, even though it now starts from a dedicated underground generator def.
