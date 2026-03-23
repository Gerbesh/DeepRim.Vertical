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
- `VerticalRendering`
- `VerticalSupports`
- `VerticalState`

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
- Generation now uses a dedicated lightweight underground generator path.
  - The underground `MapGeneratorDef` runs only mod-owned gen steps.
  - Surface and DLC special gen steps are no longer used as the base path for underground floors.
- Generation is mask-driven rather than destructive multi-pass.
  - The generator computes open cells, resource cells and lava cells in memory first.
  - It then materializes the final underground state directly.
  - This avoids repeated spawn/destroy cycles on the same cells.
- Underground empty space is still cavern space, not open sky.
  - Current underground generator keeps `RoofRockThick` over carved pockets and caverns.
  - Roofless vertical openings are reserved for explicit portal/shaft objects later.
- Underground floors use a dedicated mod `MapGeneratorDef` instead of inheriting the surface map generator.
  - This prevents surface-specific gen steps and special content from leaking into underground floors.
  - It also marks underground floors as `isUnderground=true` for vanilla systems that key off generator metadata.
- Resource density now scales upward by depth.
- Lava rivers are explicitly depth-gated and start appearing on the configured hot-depth band such as `-15` and deeper.

## Rejected alternatives

### Single map with fake z-layers

- Rejected because it would require invasive rewrites across reachability, rooming, grids, draw layers, line of sight and selection.

### World map sub-tiles or pocket maps for every floor

- Rejected because coordinate projection and cross-level queries become much harder.

## Current risks

- RimWorld world object APIs are not designed around many player-home `MapParent`s on one tile.
- Same-tile multi-map pathing is not provided by vanilla; cross-level movement will require a dedicated route planner layer.
- Cross-level combat cannot be solved by a single `GenSight` patch; it needs a targeted extension pipeline.
- Underground biome and temperature semantics for non-pocket same-tile floors still rely on targeted runtime handling rather than true pocket-map biome ownership.
- Upper-floor rendering is currently a dedicated compatibility/rendering layer rather than a finalized parity implementation.
- Positive floors now intentionally mirror weather and outdoor temperature from floor `0`.

## Upper-floor rendering policy

- Positive floors use `DeepRimVertical_UpperVoid` as the baseline terrain instead of concrete flood fill.
- Level `0` remains the climate source for all positive floors.
- Camera transitions between floors of the same site preserve the current viewport position and zoom.
- Ghost overlay rendering is still an active engineering area.
  - Goal: reveal lower floors only through open `UpperVoid` cells.
  - Current implementation uses selective lower-level composition driven by per-cell visibility masks.
  - Lower static content and lower live pawns are rendered through separate paths:
    - static content through selective underlay section rendering
    - pawns through a dynamic visibility-gated ghost pass
  - Current visual result is usable for iterative development, but not yet considered release-quality parity with the reference mod.
