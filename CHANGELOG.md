# Changelog

## 0.2.0-dev - 2026-03-23

- Added dedicated upper-floor terrain defs:
  - `DeepRimVertical_UpperVoid`
  - `DeepRimVertical_UpperDeck`
- Switched positive-floor generation away from concrete flood fill to `UpperVoid` baseline generation.
- Added upper-floor camera synchronization so floor switching preserves position and zoom within the same site.
- Added upper-floor climate synchronization so positive floors mirror weather and outdoor temperature from level `0`.
- Reworked upper-floor rendering architecture:
  - removed legacy lower-floor snapshot cache path
  - introduced `VerticalRendering` pipeline patches
  - added `UpperLevelTerrainGrid`
  - added `SectionLayer_LowerLevel`
  - added runtime `UpperVoid` material override support
- Added selective upper-floor lower-level composition:
  - lower floors are now revealed through cell-level `UpperVoid` visibility instead of unconditional full-map underlay draw
  - positive floors can stack visibility through intermediate levels
  - lower-floor flora, items, buildings, and live pawns now use dedicated reveal logic
- Added upper-floor debug tooling:
  - settings toggles for debug logging and in-game overlay diagnostics
  - navigator debug readout for current cell visibility state and lower source selection
  - targeted creation/jump/site-binding logs for floor registration and camera transitions
- Reduced several full-site redraw paths to local cell invalidation during positive-floor placement and terrain updates.
- Added site-wide render invalidation guards so map generation no longer crashes on early `WholeMapChanged()` calls.
- Added new engineering docs for upper-floor rendering, support overlay behavior, and test planning.
- Known issue:
  - upper-floor rendering is now usable for the main path but still needs polish for parity and performance
  - positive-floor placement and deep stacked overlays still need optimization work
  - underground floors remain the more stable path overall

## 0.1.0-dev - 2026-03-22

- Confirmed local RimWorld target version `1.6.4633 rev1260`.
- Added production mod skeleton with `About`, `Defs`, `Languages`, `Source`, `docs` and build project.
- Implemented vertical registry foundation:
  - `VerticalSiteWorldComponent`
  - `VerticalMapComponent`
  - `VerticalSiteMapParent`
  - persistent floor and portal records
- Added floor creation service using confirmed `MapGenerator.GenerateMap(...)` and `Current.Game.AddMap(...)`.
- Added in-game floor navigator UI with PageUp/PageDown hotkeys.
- Added bilingual settings and initial depth thermal profile utility.
- Reworked underground floor generation into a dedicated lightweight generator path with custom gen steps.
- Removed the underground wipe-and-respawn generation cycle.
- Fixed sealed underground fog initialization and entry visibility.
- Locked underground geology to deterministic local rock selection.
- Increased deep-floor resource density and enabled explicit lava-river generation on hot depths.
- Wrote research and architecture docs for stage 0 and partial stage 1.
- Added `DeepRim.Vertical.sln` and verified clean Release build output.
