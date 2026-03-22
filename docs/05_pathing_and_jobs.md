# 05 Pathing And Jobs

## Current state

- Not implemented yet.
- Foundation work completed for shared floor/site identity and portal metadata, which is the prerequisite for all inter-floor routing.

## Planned route model

- Same-level pathing remains vanilla.
- Cross-level movement will be modeled as:
  - local reachability to a valid portal cell on floor A
  - portal traversal edge
  - local reachability from linked portal cell on floor B

## Confirmed integration points

- `Pawn_PathFollower.StartPath(...)`
- `Pawn_PathFollower.TryEnterNextPathCell()`
- `Reachability.CanReach(...)`
- `ReservationManager.CanReserve(...)`
- `ReservationManager.Reserve(...)`
- `WorkGiver_DoBill.TryFindBestBillIngredients(...)`
- `HaulAIUtility.HaulToStorageJob(...)`
