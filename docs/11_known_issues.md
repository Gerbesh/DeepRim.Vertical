# 11 Known Issues

Current implementation and acceptance: [unified colony guide](unified_colony_test_plan.md). The [original colony systems audit](colony_systems_audit.md) remains a historical inventory of broader goals.

## P0 / blocking for release

- Multi-hop stair movement and first mining/construction/stockpile adapters are implemented; large-colony performance and broader routing/reservation compatibility remain to be accepted.
- Remote food/rest, physical recipe/fuel supply, repair/cleaning/tending/research/firefighting and designated plant cutting have adapters. Rescue/capture, animal/mech-specific work, cultivation cell scanners, surgery, exotic recipes and third-party work drivers need dedicated integration.
- Direct native projectiles cross open floor boundaries with LOS and persistent flight. Overhead artillery, beams, abilities and Combat Extended remain unsupported.
- Utility transfer has a first implementation for thermoventilation, electricity and Dubs Bad Hygiene plumbing. Native gameplay, save/load and accelerated simulation still require acceptance; other utility adapters are missing.
- Colony/world/history wealth, storyteller population/state, incident-target deduplication, diseases and resource readout share one site context. Caravan/trade staging and full hostile Lord-group stair navigation remain missing; every DLC event and quest lifecycle has not been accepted.
- Transfers use despawn/spawn with saved task intent, cargo preservation and original-map queued orders. Broader queue/driver compatibility and generation/transfer lifecycle recovery still need acceptance.

## P1 / important

- Stairs up/down are buildable under Architect -> Structure. Excavation currently happens as part of Construction work; dedicated Mining jobs and shaft construction UX are not implemented.
- `+` floors have a selective lower-floor overlay prototype. The current renderer drops vanilla global draw passes, has incomplete invalidation coverage and does not apply the planned depth tint correctly. See [the current rendering audit](wiki/Rendering-audit.md).
- Floors share one colonist-bar group and their internal world objects are hidden. Physical maps retain native pawn/item ownership; caravan/trader inventories still require staging on the surface.
- No verified in-workspace image-generation pipeline is configured; any art assets are currently expected to be produced manually via the web workflow if needed.
- Ordinary colony events target the surface anchor once; infestation, deep-drill infestation, short circuit and crop blight can select a viable real floor. Explicit quest targets are preserved. Broader incident execution needs gameplay acceptance.
- Underground runtime biome/weather ownership is still synthetic for same-tile non-pocket maps and should continue to be audited against edge-case vanilla systems.
- The disabled-profile 0 °C defect and cell-temperature override were fixed with the utility implementation; refrigerator and room behavior still need gameplay acceptance.
- Pawn despawn invalidates site-wide caches, and a manual transfer additionally invalidates the whole site. Actual lag contribution remains unmeasured; use the installed [per-floor profiler](per_floor_profiling.md).
- Projectile spawn/despawn no longer invalidates static site overlays. This removes avoidable work but is not evidence that the user's acceleration lag is resolved.
- Openings currently carry direct projectiles and explicit utility connections; gravity/falling, smoke/gas/fire spread and unrestricted temperature mixing are not implemented.
