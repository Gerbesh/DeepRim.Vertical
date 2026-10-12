# 05 Pathing And Jobs

## Current state (2026-10-12)

- `VerticalRouteUtility` searches directed stair endpoints with native local reachability. `GameComponent_VerticalTasks` persists travel, mining/frame work, physical hauling/construction supply and intercepted queued orders.
- Each leg validates its live stairs, ends only the owned transit job and transfers with `curJob == null`, so native despawn does not clear the queue or carried objects. Draft status is restored. Local native work/reservations restart on arrival.
- Shared site identity, paired stair buildings and portal metadata exist. `Pawn_PathFollower` patches currently guard upper-floor void; they do not implement cross-map routes.
- Work priorities, allowed areas, hauling enable/distance and same-level preference participate in the first scheduler. Remote bill ingredients, needs and other work-giver adapters remain unimplemented.

See [current gameplay checks and implementation limits](vertical_routing_test_plan.md). The [complete colony routing and logistics design](cross_level_colony_design.md) and [acceptance scenarios](cross_level_colony_test_plan.md) include additional requirements beyond this implementation.

## Planned route model

- Same-level pathing remains vanilla.
- Cross-level movement will be modeled as:
  - local reachability to a valid portal cell on floor A
  - portal traversal edge
  - local reachability from linked portal cell on floor B

## Engine integration points and limits

- `Pawn_PathFollower.StartPath` / `TryEnterNextPathCell`: local paths stay on their real map.
- `Reachability.CanReach`: evaluate each local segment with explicit map/start and movement policy; a positive cross-map answer alone cannot execute a vanilla job.
- `Pawn.DeSpawn`, `Pawn_JobTracker.StopAll` / `CleanupCurrentJob`: transfer boundaries need durable intent and explicit cargo/queue handling.
- `JobGiver_Work.TryIssueJobPackage`: preserve work giver priority when comparing local and remote candidates; use map-aware adapters.
- `ReservationManager.CanReserve`, `CanReserveStack`, `Reserve`: vanilla requires claimant on the manager's map. Site-level plan claims need a bridge to local jobs, not a global bypass.
- `WorkGiver_ConstructDeliverResources.ResourceDeliverJobFor`: local resource availability, enroute and recipient conversion need construction supply integration.
- `WorkGiver_DoBill.TryFindBestBillIngredients` and its helpers: local region/reservation assumptions require physical supply to the bench map before vanilla bill execution.
- `Verse.AI.HaulAIUtility.HaulToStorageJob`, `RimWorld.StoreUtility`, `Verse.AI.Toils_Haul`: local storage targets and carrying stages need a map-aware haul intent.
