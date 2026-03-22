# 02 Patch Points

This file records confirmed current patch candidates from local `Assembly-CSharp.dll` reflection on RimWorld `1.6.4633`.

## Implemented now

- `Verse.Map.get_CanEverExit()`
  - postfix
  - forces `false` on underground floors to block map-edge exit flow
- `Verse.Map.IncidentTargetTags()`
  - postfix
  - rewrites underground map tags to `Map_Misc` only, so storyteller does not treat underground floors as normal player-home incident targets
- `RimWorld.IncidentWorker.CanFireNowSub(RimWorld.IncidentParms)`
  - prefix
  - only when `__instance is IncidentWorker_RaidEnemy`
  - hard-rejects enemy raids whose target is an underground floor map

## Confirmed patch candidates

### Map creation / lifecycle

- `Verse.MapGenerator.GenerateMap(Verse.IntVec3, RimWorld.Planet.MapParent, Verse.MapGeneratorDef, IEnumerable<Verse.GenStepWithParams>, Action<Verse.Map>, Boolean, Boolean)`
- `RimWorld.Planet.MapParent.PostMapGenerate()`

### Pathing / movement

- `Verse.AI.Pawn_PathFollower.StartPath(Verse.LocalTargetInfo, Verse.AI.PathEndMode)`
- `Verse.AI.Pawn_PathFollower.TryEnterNextPathCell()`
- `Verse.Reachability.CanReach(...)`
- `Verse.Map.get_CanEverExit()`

### Targeting / combat

- `Verse.AI.AttackTargetFinder.BestAttackTarget(...)`
- `Verse.AI.AttackTargetFinder.BestShootTargetFromCurrentPosition(...)`
- `Verse.AI.AttackTargetFinder.CanShootAtFromCurrentPosition(...)`
- `Verse.Verb_Shoot.TryCastShot()`
- `Verse.Projectile.Launch(...)`
- `Verse.Projectile.Tick()`
- `Verse.Projectile.ImpactSomething()`

### Jobs / hauling / bills

- `RimWorld.WorkGiver_DoBill.TryFindBestBillIngredients(...)`
- `RimWorld.WorkGiver_DoBill.TryStartNewDoBillJob(...)`
- `Verse.AI.HaulAIUtility.HaulToStorageJob(...)`
- `Verse.AI.ReservationManager.CanReserve(...)`
- `Verse.AI.ReservationManager.Reserve(...)`

### Utilities / temperature

- `RimWorld.PowerNetManager.UpdatePowerNetsAndConnections_First()`
- `RimWorld.PowerNetManager.PowerNetsTick()`
- `Verse.GenTemperature.GetTemperatureForCell(...)`
- `Verse.RoomTempTracker.EqualizeTemperature()`

### UI / interaction

- `RimWorld.FloatMenuMakerMap.GetOptions(List<Verse.Pawn>, UnityEngine.Vector3, RimWorld.FloatMenuContext ByRef)`

## Validation note

- The method names above were confirmed against the current local `Assembly-CSharp.dll`.
- Underground restriction patches are compiled and active in the current workspace build.
- Important detail:
  - `CanFireNowSub(...)` is declared on base `IncidentWorker`, not on `IncidentWorker_RaidEnemy`.
  - The patch therefore targets the base method and filters by runtime worker type.

## Why Prepatcher is not justified at this stage

- All currently confirmed extension targets have callable runtime methods and do not require static field rewrites or pre-JIT method body surgery.
- The current architecture is intentionally service-first with thin Harmony hooks.
