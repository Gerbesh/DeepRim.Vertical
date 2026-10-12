# 07 Temperature And Utilities

User requirement, October 11, 2026: cold/hot depth zones remain thermally distinct. Ordinary stairs do not cause large automatic mixing; controlled transfer uses a dedicated **thermoventilation intake → insulated vertical duct → outlet**, with the intake below the outlet in the same column. The first implementation includes cable risers and a Dubs Bad Hygiene plumbing adapter. See [the design](vertical_utility_risers_design.md) and [gameplay checks](vertical_utilities_test_plan.md).

The disabled depth-profile lookup now returns false. The cell-temperature override has been removed: room readers see the same stored temperature modified by native heating, geology and controlled transfer. Refrigerator behavior and full utility gameplay still require acceptance. Gas/open-shaft leakage remains absent.

## Implemented foundation

- `DepthTemperatureUtility.EvaluateGeologicalTarget(int levelIndex)`
- `MapTemperature.OutdoorTemp` postfix now rewrites underground outdoor temperature to the geological target
- `MapTemperature.SeasonalTemp` postfix now rewrites underground seasonal temperature to the geological target
- Cell-temperature reads use native room state without a geothermal override.
- `RoomTempTracker.DeepEqualizationTempChangePerInterval()` prefix now drives thick-roof underground rooms toward the geological target instead of vanilla `15C`
- Underground generation uses this profile as a gate for hot-depth content.
  - At the configured hot target (`+60C` by default), underground map generation attempts lava river carving when a lava terrain def is available.

## Current default profile

- floor `0`: `0C` contribution from geological pressure utility
- down to `-4`: interpolate toward `-20C`
- from `-5` to `-15`: interpolate toward `+60C`
- deeper than `-15`: clamp at `+60C`

## Confirmed vanilla hooks

- `MapTemperature.get_OutdoorTemp()`
- `MapTemperature.get_SeasonalTemp()`
- `RoomTempTracker.EqualizeTemperature()`
- `RoomTempTracker.DeepEqualizationTempChangePerInterval()`
- `PowerNetManager.UpdatePowerNetsAndConnections_First()`
- `PowerNetManager.PowerNetsTick()`
- `PowerNet.PowerNetTick()`, `CanPowerNow()` and `get_HasActivePowerSource()` for linked network solving
- `TickManager.DoSingleTick()` prefix flushes pending plumbing topology before native consumers tick

## Current caveat

- Lava rivers are content-soft-dependent.
  - With Odyssey-style lava terrain defs loaded, generation can place lava channels on the deepest hot floors.
  - Without a lava terrain def, the floor still generates as sealed hot rock without lava.
- Underground ambient outdoor/seasonal temperature and thick-roof deep equalization use the enabled geological profile.
- Thermoventilation transfers equal and opposite room energy every 120 ticks, limited to 240 cell-degrees per intake step and reduced for shared room/source connections. It requires powered intake/outlet and enclosed rooms; intermediate ducts remain insulated.
- Linked power runs the native solver on a temporary union of unique real components, preserving map ownership. Vanilla local inspection and short-circuit resource accounting remain local.
- Dubs uses native tanks, pumping, contamination and sewage through explicit matching risers. Separate heating/air networks and other pipe mods require adapters.
- Full save/load, solar-flare, water quality and 3× simulation checks remain gameplay acceptance requirements.
