# Changelog

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
