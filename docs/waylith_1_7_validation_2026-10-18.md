# RimWorld 1.7 Waylith acceptance, 18 October 2026

## Tested configuration

- RimWorld **1.7.4981 rev136**, Waylith (.NET 10), Windows.
- Source: commit `2b5eb67` and installed `DeepRimVertical` mod.
- Isolated game profiles (`-savedatafolder`), Core + Harmony 1.7 + DeepRim 1.7 + a test-only native probe. User's saves/config were not changed.
- No DLC or Dubs Bad Hygiene loaded in these probes.

## Native colony probe: PASS

Profile: `obj/waylith17-live/native-smoke-colony`.
Existing `tools/validation/native-colony/Probe.cs` compiled separately for .NET 10, and run using `RimWorld.exe -batchmode -quicktest`.

- Four-floor colony, connected stairs and multi-floor routes.
- Physical mining, hauling, prioritized stockpiles, construction, food and rest, bills and until-X, fueling, cleaning, tending, research, plant-cutting/harvesting.
- Persistent roof ownership and repeated overlay mesh regeneration; projectile flight up/down, height advantage, blocking and target selection.
- Three real save/load cycles, including travelling worker, carried steel and projectile mid-flight.
- Queue/drafted movement, blocked stairs, route destruction without item loss.
- Profiler: four floors, 34 hooks, CSV/TXT exports.
- The game's `result.txt` contains `PASS: native four-floor colony`.
- Console (1,902 lines): no `MissingMethodException`, test `FAIL` or logged severe errors. There were **103 Waylith persistent allocation warnings at shutdown** (source not attributed).

## Normal-window rendered test: PASS

Profile: `obj/waylith17-visual/native-performance`.
Existing `tools/validation/native-performance/Probe.cs` compiled separately for .NET 10, run in a real rendered window with `visual-only.txt`.

- Game-generated snapshots: floor 0 with navigator, floor +1 with enabled ghost overlay, floor +3 with active profiler window.
- Navigator screenshot shows levels `-1, 0, +1, +2, +3`, no evident misplaced/blank windows.
- Five five-second 2560x1440 captures: ~143.83 to 144.02 FPS in this small test map, likely capped; initial 3x test: ~361.7 TPS against 360 target.
- `result.txt`: `PASS rendered DEV visual smoke; 5 captures; natural ticks; no manual ticking`.
- Console (965 lines): no `MissingMethodException` or logged severe errors; **85 persistent allocation warnings at shutdown**, unattributed.

## Limitations

- These are acceptance smoke tests, not long-running stability tests or comparisons with 1.6.
- Button and keybinding clicks were not individually automated; window display, map switch and overlays were verified in live captures.
- No DLC, third-party mod interactions, existing user saves, or Dubs Bad Hygiene integration tested.
- Native shutdown allocation warnings need a baseline run without DeepRim before attribution.
- Keep save backups until broader compatibility has been confirmed.

## Retained local evidence

`obj/waylith17-live/native-smoke-colony/{console.out,result.txt,Saves/}`
`obj/waylith17-visual/native-performance/{console.out,summary.csv,result.txt,Measurements/}`

All test artifacts are outside the production mod.
