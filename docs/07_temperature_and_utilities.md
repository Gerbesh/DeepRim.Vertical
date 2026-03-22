# 07 Temperature And Utilities

## Implemented foundation

- `DepthTemperatureUtility.EvaluateGeologicalTarget(int levelIndex)`
- `MapTemperature.OutdoorTemp` postfix now rewrites underground outdoor temperature to the geological target
- `MapTemperature.SeasonalTemp` postfix now rewrites underground seasonal temperature to the geological target
- `GenTemperature.GetTemperatureForCell(...)` postfix applies underground geological pressure at runtime
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
- `GenTemperature.GetTemperatureForCell(...)`
- `RoomTempTracker.EqualizeTemperature()`
- `RoomTempTracker.DeepEqualizationTempChangePerInterval()`
- `PowerNetManager.UpdatePowerNetsAndConnections_First()`
- `PowerNetManager.PowerNetsTick()`

## Current caveat

- Lava rivers are content-soft-dependent.
  - With Odyssey-style lava terrain defs loaded, generation can place lava channels on the deepest hot floors.
  - Without a lava terrain def, the floor still generates as sealed hot rock without lava.
- Current thermal implementation biases cell temperature toward the geological target.
  - It now overrides underground ambient outdoor/seasonal temperature and underground thick-roof deep equalization.
  - Proper portal-based heat exchange between floors still needs a later dedicated pass.
