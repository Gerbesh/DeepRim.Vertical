# Release Notes

This development snapshot targets the locally installed RimWorld 1.6 assemblies. Earlier 1.6.4633 notes below describe historical verification; the October 2026 audit targets 1.6.4871.

## Included

- fixes for remote construction material selection and competing stockpile priorities, plus native render-pass preservation, section-based neutral background and render-cache cleanup; [verified assembly and scope](docs/unified_colony_verification.md)

- owned constructed ceilings under upper-floor decks and full wall blocks, physically reflected in lower map roofs and restored across saves without claiming native roofs
- direct-fire height advantage: shooting down from 1/2/3+ floors gets +30/+60/+100% effective range and cover mitigation; same-floor/upward and solid-boundary rules are unchanged

- shared site wealth/population/storyteller state, deduplicated world wealth and incident scheduling, synchronized wealth history, colony resource readout and native product quotas
- autonomous remote food/rest, physical recipe ingredients and fuel, additional repair/cleaning/tending/research/firefighting/plant-cutting adapters
- direct native cross-floor projectile flight, physical LOS boundaries, automatic and manual hostile targeting, mid-flight save/load; [scope and gameplay checks](docs/unified_colony_test_plan.md)

- first cross-floor work adapters: mining, completed frames, construction supplies and physical stockpile hauling; durable multi-hop stair travel and queued cell orders bound to their original map. See [scope and gameplay checks](docs/vertical_routing_test_plan.md).
- thermoventilation intake/duct/outlet, electricity risers and optional Dubs Bad Hygiene water/sewage risers; [setup and acceptance checks](docs/vertical_utilities_test_plan.md)
- a single real room temperature for underground heat transfer, with the disabled geothermal profile correctly bypassed

- on-demand per-floor profiler (button beside LVL), 30-second CSV/TXT captures for accelerated-game lag diagnosis; native three-floor hooks/export/cleanup checked, actual lag benchmark still pending

- independent floor creation without cursor-based excavation or automatic portals
- buildable stairs up/down under Architect -> Structure; completed Construction work opens a one-cell landing and a paired passage
- material-selectable stairs (40 units) with native Metal/Planks/Bricks/Smooth appearance families and material colors

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

- rescue/capture, caravan/trade staging, animal/mech work, cultivation cell scanners and third-party work-giver integrations
- broader routing/reservation compatibility and colony-scale performance acceptance
- adapters for utility mods other than Dubs Bad Hygiene, and cross-floor heating/air-conditioning pipe networks
- overhead artillery, special abilities/beams and Combat Extended combat adapters
- hostile Lord-group vertical movement, gravity/falling and cross-floor gas/fire propagation
- dedicated Mining work for stair excavation and finalized shaft construction UX
- finalized upper-floor visual parity and ghost overlay polish
- upper-floor performance optimization for dense positive-floor placement/update scenarios
