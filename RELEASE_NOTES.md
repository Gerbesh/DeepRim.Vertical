# Release Notes

This workspace snapshot now includes a working underground floor generation redesign for DeepRim Vertical on RimWorld `1.6.4633`.

## Included

- verified RimWorld 1.6.4633 source-of-truth documentation
- compilable mod workspace and install script
- save-safe vertical site registry and floor records
- floor creation and switching UI
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

## Still not included

- cross-level pathfinding and reservations
- cross-level hauling and bill ingredient search patches
- utility transfer graphs
- vertical combat and LOS resolver
- non-debug construction UX for stairs/shafts
