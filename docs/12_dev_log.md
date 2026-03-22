# 12 Dev Log

## 2026-03-22 - Stage 0 research

- Confirmed local game version `1.6.4633 rev1260`.
- Confirmed all DLC are installed locally, including `Odyssey`.
- Verified that local `Source/` is partial and cannot be the only authority.
- Switched source-of-truth for signatures to reflection on local `Assembly-CSharp.dll`.
- Confirmed key APIs for map generation, pathing, combat, utilities and UI.

## 2026-03-22 - Foundation start

- Decision: adopt the existing surface map as floor `0`, add non-surface floors as custom same-tile `MapParent`s.
- Implemented:
  - project skeleton
  - bilingual strings
  - settings
  - world/map components
  - floor registry records
  - custom floor map parent
  - floor generation service
  - navigator UI and hotkeys
- Added SDK-style `net472` project plus `DeepRim.Vertical.sln`.
- Verified clean local build:
  - `dotnet build .\DeepRim.Vertical.csproj -c Release`
  - output: `1.6\Assemblies\DeepRim.Vertical.dll`

## 2026-03-22 - Asset workflow note

- Confirmed that a Codex `imagegen` skill exists in the environment.
- Live local use was not configured in this workspace because `OPENAI_API_KEY` and Python image dependencies are absent.
- Agreed workflow for now: generate assets manually through the web version if art is needed.

## 2026-03-22 - Navigator and underground floor pass

- Reworked the temporary floor UI into a toggleable draggable navigator window with a dedicated toggle binding path.
- Removed the duplicate-map bug caused by an extra `Current.Game.AddMap(...)` after `MapGenerator.GenerateMap(...)`.
- Connected underground floor creation to a dedicated `UndergroundGenerationService`.
- Implemented current underground generation rules:
  - full-map natural rock fill
  - thick rock roof
  - safe carved pocket around the anchor cell
  - small caverns only on `-1` and `-2`
  - resource deposits embedded into rock
  - lava river carving on hot deep floors when compatible lava terrain defs are present
- Added underground restrictions:
  - `Map.CanEverExit = false`
  - underground incident tags collapse to `Map_Misc`
  - enemy raids reject underground map targets

## 2026-03-22 - Thermal and upper-floor design follow-up

- Tightened the underground entry pocket to the aligned stair cell footprint instead of a large starter chamber.
- Connected `DepthTemperatureUtility` to runtime temperature reads through `GenTemperature.GetTemperatureForCell(...)`.
- Accepted current above-ground UX target:
  - `+` floors stay separate maps
  - active `+` floor is interactive
  - all lower non-negative floors down to `0` are shown as layered ghost support overlays
  - negative floors are not included in the upward ghost stack

## 2026-03-22 - Bootstrap fix for underground raid patch

- Found a Harmony bootstrap crash in the main menu caused by targeting `IncidentWorker_RaidEnemy.CanFireNowSub(...)` directly.
- Revalidated the local assembly and confirmed `CanFireNowSub(...)` is declared on base `IncidentWorker`.
- Moved the patch target to `IncidentWorker.CanFireNowSub(...)` and filtered by `__instance is IncidentWorker_RaidEnemy`.

## 2026-03-22 - Underground temperature pipeline fix

- Decompilation showed the main problem was not only cell temperature.
- `MapTemperature.OutdoorTemp` and `SeasonalTemp` were still returning the surface tile climate for underground floors.
- `RoomTempTracker.DeepEqualizationTempChangePerInterval()` was also pulling thick-roof rooms toward vanilla `15C`.
- Added runtime patches so underground floors use the geological target as their ambient map temperature and deep-room equalization target.

## 2026-03-22 - Underground generation performance rewrite

- Replaced the heaviest underground generation path with a mask-driven pipeline.
- Old cost center:
  - fill almost every cell with rock
  - carve by destroying many of those same spawned rocks
  - respawn resources/lava afterwards
- New pipeline:
  - wipe generated map once through `AllThings`
  - compute `open`, `lava`, `volcanic`, and `resource` masks in memory
  - apply final underground state in a single cell pass
- This removes most redundant `Destroy` / `Spawn` churn and should reduce floor creation hitching.

## 2026-03-22 - Underground roof invariant fix

- Found a generation bug where carved underground caverns were left roofless, which visually created impossible open-sky pockets.
- Fixed the invariant: current underground floors always keep thick rock roof over carved empty space.
- Future roof openings must come only from explicit vertical shaft / portal content, not from generic cave carving.

## 2026-03-22 - Dedicated underground generator defs

- Found that underground floors were still inheriting the surface `MapGeneratorDef` from the source map.
- This allowed surface and DLC-specific generation steps to leak into underground floors, including unwanted special content.
- Added:
  - `DeepRimVertical_Underground` `MapGeneratorDef`
  - `DeepRimVertical_UndergroundBiome` `BiomeDef`
- Switched underground floor creation to this dedicated generator path.
- Added a direct fallback in `DepthTemperatureUtility` to read `VerticalSiteMapParent.LevelIndex` so depth temperature does not depend only on registry timing.

## 2026-03-22 - Underground generator redesign implemented

- Replaced the remaining underground post-process rewrite path with a true lightweight underground gen-step pipeline.
- New underground generator path now runs only mod-owned steps:
  - layout
  - materialization
  - fog initialization
- Removed dependence on:
  - `Underground_RocksFromGrid`
  - mutator post steps
  - generic `Fog`
  - wipe-and-respawn generation flow
- Underground generation now:
  - prepares context before content generation
  - computes masks in memory
  - materializes the sealed map directly
  - explicitly establishes underground fog visibility around the entry
- Rock selection was tightened to deterministic local geology rather than broad random global natural rock sampling.

## 2026-03-22 - Underground tuning pass from in-game verification

- In-game verification confirmed:
  - underground floors create quickly
  - underground visibility is correct
  - depth thermal profile behaves as expected
- Follow-up tuning changes:
  - increased resource density with depth
  - increased deposit size with depth
  - changed lava activation to explicit depth gating
  - ensured lava rivers appear on the configured hot-depth band such as `-15` and deeper
- User verification after tuning confirmed the underground floors now behave as intended.
