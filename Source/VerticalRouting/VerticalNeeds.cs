using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using DeepRim.Vertical.VerticalColony;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalNeeds
{
    public static bool Eligible(Pawn pawn, bool allowCargo = false) => pawn?.Spawned == true && pawn.IsColonist && pawn.RaceProps.Humanlike
        && !pawn.Dead && !pawn.Downed && !pawn.Drafted && !pawn.InMentalState && pawn.GetLord() == null
        && (allowCargo || pawn.carryTracker.CarriedThing == null);
    public static bool FoodAllowed(Pawn pawn, Thing food)
    {
        if (!VerticalRouteUtility.Allowed(pawn, food) || food is Plant || food is Corpse || food.def.IsDrug
            || !food.def.IsNutritionGivingIngestible || !food.IngestibleNow || !pawn.WillEat(food, pawn)) return false;
        using (new RemoteReachContext.Scope(pawn, food.Map))
            return !food.IsForbidden(pawn) && food.IsSociallyProper(pawn, false, true)
                && !food.Map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant != pawn && r.Target.Thing == food)
                && !GameComponent_VerticalTasks.Get().Claimed(food, pawn);
    }
    public static bool BedAllowed(Pawn pawn, Building_Bed bed)
    {
        if (!VerticalRouteUtility.Allowed(pawn, bed) || bed.Faction != pawn.Faction) return false;
        using (new RemoteReachContext.Scope(pawn, bed.Map))
            return RestUtility.CanUseBedNow(bed, pawn, true)
                && !GameComponent_VerticalTasks.Get().Claimed(bed, pawn)
                && bed.Map.reservationManager.ReservationsReadOnly.Count(r => r.Claimant != pawn && r.Target.Thing == bed) < bed.SleepingSlotsCount;
    }
    private static Job Accept(Pawn pawn, Thing target, VerticalTaskKind kind)
    {
        if (VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, target.Map, target, PathEndMode.Touch) == null) return null;
        return GameComponent_VerticalTasks.Get().Accept(new VerticalTaskRecord
        {
            pawn = pawn, kind = kind, stage = VerticalTaskStage.ToDestination,
            destination = target, destinationMap = target.Map.Parent, destinationCell = target.Position
        });
    }
    public static Job Food(Pawn pawn)
    {
        var service = GameComponent_VerticalTasks.Get();
        if (service == null || !Eligible(pawn, true) || FoodUtility.ShouldBeFedBySomeone(pawn) || !VerticalColonyUtility.TrySite(pawn.Map, out var site)) return null;
        var current = service.Tasks.FirstOrDefault(t => t.pawn == pawn);
        if (current != null && (current.forced || current.workGiver?.emergency == true || current.kind == VerticalTaskKind.Food
            || pawn.needs.food.CurLevelPercentage >= .15f)) return null;
        if (!service.CanSearchFood(pawn)) return null;
        foreach (var food in VerticalWorkSearch.Scan(VerticalColonyUtility.Maps(site).Where(m => m != pawn.Map)
            .SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource))
            .Where(t => FoodAllowed(pawn, t)).OrderByDescending(t => t.def.ingestible.preferability), 64, pawn))
        {
            if (VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, food.Map, food, PathEndMode.Touch) == null) continue;
            if (current != null) service.CancelPawn(pawn);
            if (pawn.carryTracker.CarriedThing != null && !pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _)) return null;
            var job = Accept(pawn, food, VerticalTaskKind.Food); if (job != null) return job;
        }
        return null;
    }
    public static Job Rest(Pawn pawn)
    {
        if (!Eligible(pawn) || !VerticalColonyUtility.TrySite(pawn.Map, out var site) || GameComponent_VerticalTasks.Get().HasTask(pawn)) return null;
        foreach (var bed in VerticalColonyUtility.Maps(site).Where(m => m != pawn.Map)
            .SelectMany(m => m.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            .Where(b => BedAllowed(pawn, b)).OrderByDescending(b => b == pawn.ownership.OwnedBed).ThenByDescending(b => b.Medical).Take(32))
        {
            var job = Accept(pawn, bed, VerticalTaskKind.Rest); if (job != null) return job;
        }
        return null;
    }
    public static Job LocalJob(VerticalTaskRecord task)
    {
        var pawn = task.pawn;
        if (task.kind == VerticalTaskKind.Rest)
            return task.destination is Building_Bed bed && RestUtility.IsValidBedFor(bed, pawn, pawn, true)
                ? JobMaker.MakeJob(JobDefOf.LayDown, bed) : null;
        if (!FoodAllowed(pawn, task.destination)) return null;
        var food = task.destination;
        var job = JobMaker.MakeJob(JobDefOf.Ingest, food);
        job.count = FoodUtility.WillIngestStackCountOf(pawn, food.def, FoodUtility.GetNutrition(pawn, food, food.def));
        return job;
    }
}

[HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
public static class VerticalFoodJobPatch
{
    public static void Postfix(Pawn pawn, ref Job __result)
    { if (__result == null) __result = VerticalNeeds.Food(pawn); }
}
[HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
public static class VerticalRestJobPatch
{
    public static void Postfix(Pawn pawn, ref Job __result)
    {
        if (__result?.def == JobDefOf.LayDown && __result.targetA.HasThing) return;
        // A null result may signify disturbance or a need threshold; never override that refusal.
        if (__result == null) return;
        var job = VerticalNeeds.Rest(pawn);
        if (job != null) { JobMaker.ReturnToPool(__result); __result = job; }
    }
}
[HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
public static class VerticalBedPreflightPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));
        var replacement = AccessTools.Method(typeof(RemoteReachContext), nameof(RemoteReachContext.GuardMapHeld));
        var replaced = false;
        foreach (var instruction in instructions)
        {
            if (!replaced && instruction.Calls(getter))
            { instruction.opcode = OpCodes.Call; instruction.operand = replacement; replaced = true; }
            yield return instruction;
        }
        if (!replaced) throw new System.InvalidOperationException("Native bed preflight map guard changed");
    }
}
