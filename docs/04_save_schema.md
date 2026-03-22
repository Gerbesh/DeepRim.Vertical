# 04 Save Schema

## Serialized in `VerticalSiteWorldComponent`

- `List<VerticalSiteRecord> sites`

## Serialized in `VerticalSiteRecord`

- `siteId`
- `label`
- `tile`
- `floors`
- `portals`

## Serialized in `VerticalFloorRecord`

- `levelIndex`
- `isSurfaceAnchor`
- `mapGeneratorDefName`
- `mapSizeX`
- `mapSizeZ`
- `mapParent` reference

## Serialized in `VerticalPortalRecord`

- source/target levels
- shared XY cell
- interaction flags
- traversal cost
- blocking state
