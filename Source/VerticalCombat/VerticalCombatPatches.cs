using System;
using System.Linq;
using DeepRim.Vertical.VerticalColony;
using DeepRim.Vertical.VerticalRouting;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DeepRim.Vertical.VerticalCombat;

[HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
public static class VerticalShootLinePatch
{
    public static bool Prefix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, bool ignoreRange, ref bool __result)
    {
        if (!targ.HasThing || __instance.Caster?.Map == targ.Thing.Map) return true;
        resultingLine = new ShootLine(root, targ.Cell);
        __result = VerticalSight.CanShoot(__instance, root, targ.Thing, ignoreRange); return false;
    }
}
[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class VerticalLaunchShotPatch
{
    private static readonly AccessTools.FieldRef<Verb, int> LastShot = AccessTools.FieldRefAccess<Verb, int>("lastShotTick");
    private static readonly AccessTools.FieldRef<Verb, bool> NonTargetPawns = AccessTools.FieldRefAccess<Verb, bool>("canHitNonTargetPawnsNow");
    public static bool Prefix(Verb_LaunchProjectile __instance, ref bool __result)
    {
        var target = __instance.CurrentTarget.Thing; var caster = __instance.Caster;
        if (target == null || caster.Map == target.Map) return true;
        __result = false;
        if (!VerticalSight.CanShoot(__instance, caster.Position, target)) return false;
        var definition = __instance.Projectile;
        var equipment = __instance.EquipmentSource;
        var launcher = caster.TryGetComp<CompMannable>()?.ManningPawn ?? caster;
        var distance = VerticalSight.Distance(caster, target);
        var accuracy = __instance.verbProps.GetHitChanceFactor(equipment, distance);
        if (launcher is Pawn pawn) accuracy *= Mathf.Pow(Mathf.Clamp01(pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn)), distance);
        if (target is Pawn victim && victim.GetPosture() != PawnPosture.Standing) accuracy *= .5f;
        var usedTarget = new LocalTargetInfo(target);
        var hitFlags = ProjectileHitFlags.IntendedTarget;
        if (NonTargetPawns(__instance)) hitFlags |= ProjectileHitFlags.NonTargetPawns;
        if (__instance.verbProps.ForcedMissRadius > .5f || __instance.verbProps.canGoWild && !Rand.Chance(Mathf.Clamp01(accuracy)))
        {
            var radius = Mathf.Max(1f, __instance.verbProps.ForcedMissRadius);
            var cell = target.Position + GenRadial.RadialPattern[Rand.Range(1, GenRadial.NumCellsInRadius(radius))];
            if (!cell.InBounds(target.Map)) cell = target.Position;
            usedTarget = cell; hitFlags = ProjectileHitFlags.NonTargetWorld;
            if (NonTargetPawns(__instance)) hitFlags |= ProjectileHitFlags.NonTargetPawns;
        }
        else if (target.def.CanBenefitFromCover && VerticalSight.SameSite(caster.Map, target.Map, out var site))
        {
            var from = site.floors.Find(f => f.Map == caster.Map).levelIndex;
            var to = site.floors.Find(f => f.Map == target.Map).levelIndex;
            var segments = VerticalSight.Segments(site, from, to, caster.Position.ToVector3Shifted(), target.Position.ToVector3Shifted());
            var entry = segments[segments.Count - 1].Start.ToIntVec3();
            var covers = CoverUtility.CalculateCoverGiverSet(target, entry, target.Map);
            var heightLevels = VerticalHeightAdvantage.Levels(site, caster.Map, target.Map);
            var coverBlockChance = CoverUtility.CalculateOverallBlockChance(target, entry, target.Map)
                * VerticalHeightAdvantage.CoverBlockMultiplier(heightLevels);
            if (covers.Count > 0 && Rand.Chance(coverBlockChance))
            {
                usedTarget = covers.RandomElementByWeight(c => c.BlockChance).Thing;
                hitFlags = ProjectileHitFlags.NonTargetWorld;
                if (NonTargetPawns(__instance)) hitFlags |= ProjectileHitFlags.NonTargetPawns;
            }
        }
        var projectile = (Projectile)ThingMaker.MakeThing(definition);
        GenSpawn.Spawn(projectile, caster.Position, caster.Map);
        var flight = projectile.GetComp<CompVerticalFlight>();
        if (flight == null) { projectile.Destroy(); return false; }
        flight.Begin(caster.Map, target.Map);
        equipment?.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
        equipment?.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
        if (equipment?.GetComp<CompUniqueWeapon>() is CompUniqueWeapon unique)
            foreach (var trait in unique.TraitsListForReading)
            { if (trait.damageDefOverride != null) projectile.damageDefOverride = trait.damageDefOverride; if (trait.extraDamages != null) projectile.extraDamages.AddRange(trait.extraDamages); }
        projectile.Launch(launcher, caster.DrawPos, usedTarget, target, hitFlags, __instance.preventFriendlyFire, caster == launcher ? equipment : caster);
        LastShot(__instance) = Find.TickManager.TicksGame;
        __result = true; return false;
    }
}
[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
public static class VerticalAttackTargetPatch
{
    public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist,
        IntVec3 locus, float maxTravelRadiusFromLocus, ref IAttackTarget __result)
    {
        if (__result != null || searcher.CurrentEffectiveVerb is not Verb_LaunchProjectile verb || searcher.Thing?.Spawned != true
            || !VerticalColonyUtility.TrySite(searcher.Thing.Map, out var site)) return;
        foreach (var target in VerticalColonyUtility.Maps(site).Where(m => m != searcher.Thing.Map)
            .SelectMany(m => m.attackTargetsCache.GetPotentialTargetsFor(searcher).ToArray())
            .Where(t => t.Thing?.Spawned == true)
            .OrderByDescending(t => VerticalHeightAdvantage.Levels(site, searcher.Thing.Map, t.Thing.Map))
            .ThenBy(t => VerticalSight.Distance(searcher.Thing, t.Thing)).Take(64))
        {
            var thing = target.Thing; var distance = VerticalSight.Distance(searcher.Thing, thing);
            var rangeBonus = VerticalHeightAdvantage.RangeMultiplier(VerticalHeightAdvantage.Levels(site, searcher.Thing.Map, thing.Map));
            if (distance < minDist || distance > maxDist * rangeBonus || !searcher.Thing.HostileTo(thing) || validator != null && !validator(thing)
                || ((flags & (TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable)) != 0 && target.ThreatDisabled(searcher))
                || (flags & TargetScanFlags.NeedAutoTargetable) != 0 && !AttackTargetFinder.IsAutoTargetable(target)
                || (flags & TargetScanFlags.IgnoreNonCombatants) != 0 && thing is Pawn nonCombatant && !nonCombatant.IsCombatant()
                || (flags & TargetScanFlags.NeedActiveThreat) != 0 && !GenHostility.IsActiveThreatTo(target, searcher.Thing.Faction)
                || (flags & TargetScanFlags.NeedNonBurning) != 0 && thing.IsBurning()
                || (flags & TargetScanFlags.NeedNotUnderThickRoof) != 0 && thing.Position.GetRoof(thing.Map)?.isThickRoof == true) continue;
            if (searcher is Pawn pawn && pawn.GetLord() is Lord lord && !lord.LordJob.ValidateAttackTarget(pawn, thing)) continue;
            if (maxTravelRadiusFromLocus < 9999 && thing.Position.DistanceTo(locus) > maxTravelRadiusFromLocus + verb.EffectiveRange * rangeBonus) continue;
            if (!verb.CanHitTarget(thing)) continue;
            if ((flags & TargetScanFlags.NeedReachable) != 0 && searcher is Pawn traveler
                && VerticalRouteUtility.Find(traveler, traveler.Map, traveler.Position, thing.Map, thing, PathEndMode.Touch) == null) continue;
            __result = target; return;
        }
    }
}
