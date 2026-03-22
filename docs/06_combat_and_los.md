# 06 Combat And LOS

## Current state

- Not implemented yet.
- Portal records already contain the interaction flags the combat layer will consume:
  - `allowsSight`
  - `allowsProjectilePass`
  - `doorLikeBlockingState`

## Planned algorithm

1. Source and target must belong to the same `VerticalSiteRecord`.
2. Shared XY projection is evaluated.
3. Every intermediate floor boundary must have a portal/opening at that XY with sight and projectile permission.
4. Local same-floor checks remain vanilla around source and target.
5. Cross-level shots use a dedicated resolver instead of rewriting all same-level projectile logic.

## Confirmed integration points

- `AttackTargetFinder.BestAttackTarget(...)`
- `AttackTargetFinder.BestShootTargetFromCurrentPosition(...)`
- `AttackTargetFinder.CanShootAtFromCurrentPosition(...)`
- `Verb_Shoot.TryCastShot()`
- `Projectile.Launch(...)`
- `Projectile.Tick()`
- `Projectile.ImpactSomething()`
