# Unified colony verification — 2026-10-12

Production assembly SHA-256: `B80F9B69FD4035FD48237535F95B7C4279F3F04090C019B1FBDD782FD9CC671F`.

Release build: net472, installed RimWorld 1.6.4871 rev591, zero warnings/errors. Native isolated profile enabled Harmony, Core, all five installed DLC, Dubs Bad Hygiene and DeepRim. Probe source: `tools/validation/native-colony/Probe.cs`; reproduction: its README. Private engine bodies, generated saves and smoke logs are excluded from Git and the mod package.

## Native result

`obj/native-smoke-colony/probe-storage-priority.log` ends in `[COLONY PROBE] PASS`. It exercises four actual maps and three save/load cycles (mining transit, carried resources and a projectile in flight).

- Shared map/world/history wealth without multiplying a site by its floor count; fixed-wealth age, native population and story-state ownership, one ordinary incident target, disease candidates and scoped resource readout.
- Remote stored wood/steel in native construction material/cost queries and the menu's material-presence helper; scope restored to local job counters. This is query/patch acceptance, not a visual mouse-click test of the complete GUI.
- A higher-priority remote stockpile beats a competing lower-priority local haul job; global storage priorities, competing reservations, physical delivery and conservation.
- Mining, multi-hop walking between different stair cells, construction material delivery/native wall completion, remote food/native ingestion, owned bed/native sleep, ingredient delivery/native recipe/until-X, physical fuel/native consumption, repair/cleaning/tending/research.
- Upper deck/full block creates a lower owned ceiling; removal removes only the owned ceiling; independently built and natural roofs survive; ceiling ownership survives loading.
- Range/cover tier table 0/1/2/3+, actual downward range boundary, blocked decks/roofs/walls, automatic/manual hostile targeting, native downward/upward projectile impact and saved flight.
- Original-map queued cell orders, drafted travel with independent destination geometry, blocked/forbidden/missing stairs, cancellation conserving carried cargo.
- Four-floor profiler rows, 23 native hooks, 200 captured ticks, CSV/TXT exports and hook cleanup.

## Additional checks

All five local suites pass: routing, stairs, utilities, colonist bar and profiler. Documentation validator and `git diff --check` pass. Renderer changes retain native passes, batch the neutral base by native section, scope invalidation, respect fog/roofs and dispose external worker meshes/caches on load/removal. These changes compile and load with the native probe; interactive visual parity and FPS are not established by a batch run.

## Remaining acceptance

Use [the gameplay plan](unified_colony_test_plan.md) for the user's colony, including construction menus, stockpile filters/priorities, roofs and 1×/3× profiles. No actual accelerated-colony lag reduction is claimed. Fire/plant-cut adapters, unusual patient states and every incident execution need further gameplay acceptance. Caravan/trade staging, rescue/capture, animal/mech work, full hostile group routing, cultivation cell scanners, falling/gas/fire propagation and exotic/Combat Extended combat remain explicit integration work. [Current known issues](11_known_issues.md).
