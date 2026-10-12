# Changelog

## Unreleased - 2026-10-12

- Count real materials across site floors for native build designators, including their independent material-presence check; refresh aggregate UI counts after native stock updates without changing local job inventories.
- Allow a higher-priority remote stockpile to compete with native local hauling from the same work giver. Preserve equal-priority storage and the explicit prefer-local setting.
- Preserve the complete native map drawing pipeline, batch the upper neutral base by native section, avoid structural-cache resets for ordinary mesh dirtiness, refresh affected upper workers, and release external layer meshes/caches on viewport removal, map removal and load.
- Make floor navigation follow actual text/button height; hide support debug details by default and add translated storage/material help. Respect lower-map fog and every roof type in overlay visibility.

- Connect upper decks and full building blocks to owned constructed ceilings on the floor immediately below; keep original/native roofs untouched, persist ownership across saves, and reconcile on terrain and building changes.
- Add downward-only 1/2/3+ floor firing benefits: effective maximum range x1.30/x1.60/x2.00 and cover interception penalty reduced by 30%/60%/100%. Prioritize targets below among valid remote candidates. Same-floor and upward shots keep baseline values.

- Added a common colony context for threat wealth/population, fixed-wealth colony age, world wealth deduplication, coordinated history snapshots, resource-panel queries and one storyteller scheduling target per site. Native map-local resource and wealth ownership is preserved.
- Route terrain-dependent incidents to eligible floors and include remote colonists/animals in ordinary disease candidate pools; explicitly targeted quest incidents retain their map.
- Added food/bed travel, physical production ingredient/fuel supply and shared native recipe counters, including builtin counter overrides and separate explicit include-storage groups. Added remote repair, home-area cleaning/firefighting, tending and research.
- Extend read-only route preflight to query the correct starting region and normal danger policy; urgent work gets its own scan budget and can interrupt ordinary travel safely. Urgent hunger can cancel ordinary transit while preserving cargo.
- Added 3D floor-boundary line-of-fire checks, native projectile transport/impact/save data, cross-floor target selection and a drafted-pawn attack command. Solid terrain/foundations, roofs and boundary walls block the ray; stairs alone do not open a firing shaft. Direct projectiles only; special combat/raid/transport integrations remain separate work.
- Added directed, weighted stair routes with native local reachability and walking between different stair cells on intermediate floors. Queries preserve the actual pawn map and scope per-map allowed areas.
- Replaced static pending transfers with saved task records, carry-safe traversal jobs, per-leg route validation and original-map bindings for queued cell orders.
- Added automatic mining, frame completion, physical construction supply and higher-priority stockpile hauling for ordinary colonists. Native jobs own local work after arrival; task claims bridge remote planning and local reservations.
- Cancel invalid destinations safely, resolve blueprint-to-frame replacement, and preserve carried objects through hops and failure. Scan attempts are throttled to 180 ticks with bounded candidate batches.
- Rotate candidate windows to prevent starvation beyond the first scan budget, compare remote storage priorities globally, and add the native forbid component to all stairs and paired exits.
- Added core route checks and an opt-in three-floor Unity/Mono probe with real pawn walking, two en-route save/load cycles and resource conservation checks. See the routing test guide for unsupported systems.
- Passed native three-floor mining, physical hauling, construction, global storage priority, queue/draft preservation, blocked/forbidden/missing route rejection and safe carried-cargo cancellation. Native profiling also passed all 23 hooks, hidden-floor rows, CSV/TXT and cleanup; colony-scale acceleration remains unmeasured.

## Unreleased - 2026-10-11

- Added five buildable utility sections under Structure: thermal intake/duct/outlet, cable riser and optional Dubs Bad Hygiene plumbing riser. Adjacent floors connect only at matching X/Z with open valves.
- Added conservative room-to-room heat transfer with a shared intake budget, explicit source selection and insulated intermediate rooms. Fixed disabled geothermal settings still applying 0 °C and removed the cell-temperature override.
- Solve linked electrical networks once using unique native consumers and batteries while preserving their original map ownership. Track components across topology changes to avoid duplicate energy accounting within a tick.
- Bind Dubs Bad Hygiene optionally by reflection (local 3.1.2800, Workshop 836308268); rebuild explicit floor plumbing links using native tanks, pumping, sewage and water quality. Other pipe-network types are not supported by this riser.
- Added utility source/heat/topology/native-component-ownership and dependency-contract checks. Full gameplay acceptance, balance and compatibility with other network mods remain to be checked.
- Native startup exposed an ambiguous colonist-bar layout patch; resolve its exact public overload so bootstrap and subsequent optional adapters can load.
- Passed an isolated Unity/Mono game probe: native power solving across two floors, parallel links, conservative room heat transfer, actual Dubs water withdrawal/quality and disconnection, plus real game save/load with valve/source/network restoration. Long-running colony play and accelerated performance remain acceptance tasks.

- Added an on-demand per-floor CPU profiler beside the navigator: 30-second captures include surface and hidden maps, TPS/tick/frame timing, work/path/thermal/power/render/cache detail and expensive pawns/object defs.
- Export profiler CSV/TXT automatically to the game's save-data folder; bound samples, avoid double-counting nested held ticks, and remove only profiler-owned hooks on stop/game reset. Local statistics, engine contracts and CLR probes pass; full capture in Unity/Mono remains unverified.

- Made stairs buildable from 40 units of any supported native metal, wood or stone material, using the vanilla Stuff selector and stat factors.
- Added material-specific stair sprites/menu icons for Metal, Planks, Bricks and Smooth appearances, tinted with the native material color.
- Preserve the material on paired exits; migrate earlier fixed-steel stairs, frames and blueprints to steel without changing newly selected materials.

- Combined portraits across a vertical site's floors into one colony group; empty floors no longer create location slots.
- Preserved each pawn's actual map for camera jumps and selection, and rebuilt drag callbacks for the combined group.
- Kept the colony group active on any of its floors; other colonies and caravans remain separate.
- Hid internal floor world objects from world drawing, selection and caravan gizmos.

- Separated floor creation from entrances: navigator buttons no longer use the cursor, carve an underground entry pocket or register a free portal.
- New underground maps remain fogged until an entrance is built.
- Added buildable stairs up/down under Architect -> Structure, with Russian labels and dedicated textures.
- Completed construction generates/reuses the adjacent level, excavates only its landing cell and registers a two-way route.
- Stair pairs persist across saves; removing either end removes both routes and the paired endpoint.
- Revalidate pending transfers when a route is removed or the pawn's order changes; reject blocked landings instead of falling back to the map center.
- Excavation currently uses Construction work as part of building stairs; dedicated Mining work remains future work.

## 0.2.0-dev - 2026-03-23

- Added dedicated upper-floor terrain defs:
  - `DeepRimVertical_UpperVoid`
  - `DeepRimVertical_UpperDeck`
- Switched positive-floor generation away from concrete flood fill to `UpperVoid` baseline generation.
- Added upper-floor camera synchronization so floor switching preserves position and zoom within the same site.
- Added upper-floor climate synchronization so positive floors mirror weather and outdoor temperature from level `0`.
- Reworked upper-floor rendering architecture:
  - removed legacy lower-floor snapshot cache path
  - introduced `VerticalRendering` pipeline patches
  - added `UpperLevelTerrainGrid`
  - added `SectionLayer_LowerLevel`
  - added runtime `UpperVoid` material override support
- Added selective upper-floor lower-level composition:
  - lower floors are now revealed through cell-level `UpperVoid` visibility instead of unconditional full-map underlay draw
  - positive floors can stack visibility through intermediate levels
  - lower-floor flora, items, buildings, and live pawns now use dedicated reveal logic
- Added upper-floor debug tooling:
  - settings toggles for debug logging and in-game overlay diagnostics
  - navigator debug readout for current cell visibility state and lower source selection
  - targeted creation/jump/site-binding logs for floor registration and camera transitions
- Reduced several full-site redraw paths to local cell invalidation during positive-floor placement and terrain updates.
- Added site-wide render invalidation guards so map generation no longer crashes on early `WholeMapChanged()` calls.
- Added new engineering docs for upper-floor rendering, support overlay behavior, and test planning.
- Known issue:
  - upper-floor rendering is now usable for the main path but still needs polish for parity and performance
  - positive-floor placement and deep stacked overlays still need optimization work
  - underground floors remain the more stable path overall

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
