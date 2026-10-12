# 04 Save Schema

## Serialized in `VerticalSiteWorldComponent`

- `List<VerticalSiteRecord> sites`

## Serialized in `VerticalSiteRecord`

- `siteId`
- `label`
- `tile`
- `floors`
- `portals`

## Serialized in `VerticalFloorRecord`

- `levelIndex`
- `isSurfaceAnchor`
- `mapGeneratorDefName`
- `mapSizeX`
- `mapSizeZ`
- `mapParent` reference

## Serialized in `VerticalPortalRecord`

- source/target levels
- shared XY cell
- interaction flags
- traversal cost
- blocking state

## Cross-floor task persistence (first implementation, 2026-10-12)

`GameComponent_VerticalTasks` serializes `verticalTasks` and `verticalQueuedMaps`. A task owns pawn/target/map references, kind/stage, material count, work giver, expected native job ID, ready state, creation tick, next hop and player-forced status. Construction records retain buildable/stuff definitions to resolve a blueprint replaced by its matching frame. An intercepted queued order is serialized deeply only while the task owns it; ordinary queued jobs remain owned by the native pawn job queue.

Queued cell orders retain their original map through pawn/job-ID/map bindings. Physical carried items stay serialized by the native pawn carry tracker; task records never serialize a second copy. Paths and local reservations remain native and are reacquired as each leg starts. See [current gameplay checks and limitations](unified_colony_test_plan.md).

Food, rest, recipe supply and fuel supply append task enum values without renumbering existing values. Recipe supply retains its native bill reference; deleting or suspending the bill cancels its intent. Each native projectile's optional `CompVerticalFlight` serializes active status, source/destination map-parent references, levels, duration and last tick. Native projectile data retains its targets, remaining flight time and damage. On load, physical map ownership remains native; the last-tick guard prevents a bullet migrating into a later-ticked map from advancing twice.

Colony wealth/resource snapshots and query scopes are runtime caches and are rebuilt after loading. No aggregate cache owns or duplicates physical pawns or items.
