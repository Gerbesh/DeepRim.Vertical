# Release Notes

This workspace snapshot now includes a working underground floor generation redesign plus an in-progress upper-floor rendering stack for DeepRim Vertical on RimWorld `1.6.4633`.

## Included

- verified RimWorld 1.6.4633 source-of-truth documentation
- compilable mod workspace and install script
- save-safe vertical site registry and floor records
- floor creation and switching UI
- upper-floor creation with `UpperVoid` baseline terrain
- upper-floor camera sync between site floors
- upper-floor weather and outdoor temperature mirroring from level `0`
- selective upper-floor lower-level reveal rendering
- stacked positive-floor visibility through intermediate levels
- lower-floor live pawn ghost rendering through visible channels
- upper-floor debug logging and navigator diagnostics
- depth thermal profile runtime handling
- dedicated lightweight underground generator path
- sealed underground map generation with explicit fog initialization
- depth-scaled resource generation
- hot-depth lava river generation

## Confirmed in local game testing

- underground floors create quickly
- underground visibility behaves correctly
- underground temperatures behave correctly with the thermal profile enabled
- deep floors now provide stronger resource yield
- lava rivers appear on hot-depth floors such as `-15`
- upper-floor switching preserves camera position more reliably
- upper-floor weather no longer diverges from level `0`
- upper floors can now reveal lower terrain/buildings/items more selectively instead of relying on the older unconditional underlay path
- lower live pawns can now be rendered through open upper-floor void cells

## Still not included

- cross-level pathfinding and reservations
- cross-level hauling and bill ingredient search patches
- utility transfer graphs
- vertical combat and LOS resolver
- non-debug construction UX for stairs/shafts
- finalized upper-floor visual parity and ghost overlay polish
- upper-floor performance optimization for dense positive-floor placement/update scenarios
