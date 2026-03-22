# 03 Data Model

## Entities

### `VerticalSiteRecord`

- ownership: `VerticalSiteWorldComponent`
- identity: `siteId`
- scope: one world tile, multiple floors, shared portal registry

### `VerticalFloorRecord`

- identity: `levelIndex` inside one site
- persistent references:
  - `MapParent mapParent`
  - generator def name
  - map size

### `VerticalPortalRecord`

- current stage purpose:
  - save-safe coordinate-level portal registration
  - link `sourceLevel -> targetLevel` at one shared XY cell

## Runtime ownership

- `VerticalSiteWorldComponent`
  - serialized source of truth
  - caches by `siteId`
  - caches by `MapParent`
- `VerticalMapComponent`
  - per-map convenience metadata
  - not authoritative for floor ownership
