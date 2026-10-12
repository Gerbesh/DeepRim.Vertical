# 06 Combat And LOS

Updated October 12, 2026 against installed RimWorld **1.6.4871 rev591**. Ordinary `Verb_LaunchProjectile` weapons now target hostile things across floors in their site. Each map segment and boundary checks walls, solid decks/foundations and roofs; stairs alone do not create a firing shaft. Native projectiles physically migrate between maps, retain native impact handling, use three-dimensional flight duration and survive save/load. Drafted armed colonists expose a cross-floor attack command; automatic targeting also scans viable remote hostiles. See [current gameplay checks](unified_colony_test_plan.md).

Overhead artillery, abilities, beams and Combat Extended require explicit adapters. The design below records broader goals beyond the current direct-projectile implementation.

## Current implementation and verified engine boundaries

- `VerticalPawnVisibilityService` draws lower pawns through a column of upper overlay masks. It does not implement combat visibility, enemy target acquisition or selectable cross-floor targets.
- Portal records serialize `allowsSight`, `allowsProjectilePass` and `doorLikeBlockingState`. No combat executor consumes those flags.
- Stair connection removes a rock Thing at the destination, places a landing and registers reciprocal movement links. It does not remove the underground roof or create an authoritative opening through the shared floor boundary.
- `Verse.AI.AttackTargetFinder` gathers potential targets from the searcher's own map. Its local reachability and cover queries also use map-specific data.
- `Verse.Verb.TryFindShootLineFromTo` rejects a Thing on another map. `Verse.Verb_LaunchProjectile.TryCastShot` checks the map again and spawns the projectile on the caster's map.
- `Verse.Projectile.CanHit` rejects targets on another map. Travel/collision work is in `TickInterval(int delta)` as well as the smaller `Tick()` path; patching `Tick()` alone is insufficient in this engine version.

These methods are integration constraints, not a validated set of runtime patches. Full Unity/Mono verification remains necessary. A permissive `GenSight` postfix cannot implement the complete pipeline.

## Physical geometry

Source and target must belong to the same site, with explicit Map/Thing or Map/cell identities. Local cell coordinates remain X/Z. Physical floor height is a separate model parameter; neither `IntVec3.y` nor Unity draw altitude is a floor coordinate.

Introduce a shared boundary grid for every pair of adjacent floors. It represents solid deck/ceiling, open aperture, hatch state, shaft and material. Sight, projectile passage, movement and heat/gas are distinct permissions. An enclosed staircase may permit movement while blocking sight and bullets. A decorative reveal mask must not determine ballistics.

Resolve the overhead roof of the lower map and the supporting deck of the upper map into one consistent boundary. Thick rock is solid unless an explicit excavation has opened it. Old portal records must not acquire open-shaft geometry merely because their saved sight/projectile flags default to true.

For a direct shot, use a conceptual 3D ray from the muzzle position to the selected aim point. For each crossed boundary at height `b`, compute `t = (b - sourceHeight) / (targetHeight - sourceHeight)` and interpolate X/Z at that `t`. Only crossings inside the trajectory count; exact cell-edge/corner intersections need a deterministic conservative rule. A sloped ray can cross different cells on successive boundaries. Checking a single shared X/Z column, or just the cells above the target, is incorrect.

Between crossings, test the local map segment against walls, cover and eligible interceptors. Apply the selected 3D range policy, including height, and handle equal X/Z positions without a zero-length flight. The current direct-fire adapter applies the downward tiered range/cover bonus documented below. Accuracy, muzzle height and projectile type-specific ballistics still require explicit broader rules.

## Complete firing pipeline

1. Gather valid candidates across relevant floors without exposing fog-hidden targets. Respect faction, threat eligibility and visibility in both directions.
2. Support explicit player selection of the real cross-map target, highlighting the target floor and reasons for rejection. Render ghosts are insufficient target identities.
3. Validate weapon capability, range, geometry, warmup and each burst shot. Preserve ammo/charges, cooldown and normal same-floor behavior.
4. Calculate the actual aim/miss trajectory. Trace that trajectory, not only the intended target line: a miss can hit the edge of an opening or an intermediate pawn.
5. Advance a dedicated cross-floor projectile state through map segments. Use the correct map for collision, shields/interceptors, cover and effects. Recheck changing boundaries during flight.
6. Apply one physical impact with vanilla damage semantics where supported. Preserve launcher/equipment, hit flags, intended/used target and friendly-fire rules. Never directly damage the remote target as a substitute for flight.
7. Save flight identity, current segment and remaining travel state. Restore safely if a map/target disappeared; do not replay an already applied impact.

The exact adapter implementation must follow actual projectile types: blindly despawning and relaunching every vanilla projectile at a boundary can reset lifetime, miss state, sound and launcher attribution. A dedicated map-aware resolver plus explicit type adapters is the proposed approach, not a promise that every Projectile subclass can be transferred unchanged.

## Capability stages

- First: standard direct projectile weapons, downward and upward, player targeting and supported automatic acquisition, obstruction, misses and save/load.
- Next: turret acquisition and supported combat movement between floors. Local Lord/duty, chase, flee and rescue behavior needs route-aware integration separately from firing.
- Separate adapters: arcing explosives, overhead/mortar shots, beams, flamethrowers, explosions through apertures, psychic/ability effects and custom mod verbs. Do not infer compatibility from inheritance alone.

Until an adapter exists, cross-floor use of that weapon/ability must report a clear unsupported action. Same-floor behavior remains handled by the original engine path. Explosion propagation requires its own volume/occlusion policy; it does not follow automatically from permission for a single bullet.

## Height advantage (October 12 implementation)

Normal direct-projectile shots aimed **down** gain an effective range multiplier of 1.30 for one floor, 1.60 for two floors, and 2.00 for three or more floors. The cover-blocking probability is multiplied by 0.70, 0.40 and 0.00 respectively. The bonus is zero on the same level or when shooting upward. Native weapon hit chance still depends on actual 3D distance; this change does not raise base accuracy or bypass walls, decks, foundations, roofs, projectile collision, fog, or unsupported verb types. Automatic remote selection considers greater downward height advantage before distance among eligible targets, but vanilla same-map targeting is left intact.

The rules are implemented in VerticalHeightAdvantage, VerticalSight, VerticalLaunchShotPatch and VerticalAttackTargetPatch. Extrapolating extra levels past three does not stack more benefits. Some modded weapon verbs and targeting abstractions need dedicated adapters.
