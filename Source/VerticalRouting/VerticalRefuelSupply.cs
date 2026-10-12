using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalColony;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalRefuelSupply
{
    public static bool Valid(Thing target) => target?.Spawned == true && !target.IsBurning()
        && target.TryGetComp<CompRefuelable>() is CompRefuelable fuel && fuel.allowAutoRefuel && fuel.ShouldAutoRefuelNow;
    public static VerticalTaskRecord Find(Pawn pawn, VerticalSiteRecord site, WorkGiverDef giver)
    {
        foreach (var map in VerticalColonyUtility.Maps(site))
        foreach (var target in VerticalWorkSearch.Scan(map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable), 32, pawn))
        {
            if (target.Faction != pawn.Faction || !Valid(target) || !VerticalRouteUtility.Allowed(pawn, target)
                || GameComponent_VerticalTasks.Get().Claimed(target, pawn)
                || map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant != pawn && r.Target.Thing == target)) continue;
            var fuel = target.TryGetComp<CompRefuelable>();
            var needed = fuel.GetFuelCountToFullyRefuel();
            // Vanilla can handle available local fuel without a remote courier.
            if (map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Any(t => fuel.Props.fuelFilter.Allows(t)
                && VerticalWorkSearch.SourceUsable(pawn, t) && VerticalRouteUtility.Reach(pawn, map, target.Position, t, PathEndMode.ClosestTouch)))
            {
                if (map != pawn.Map && VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, map, target, PathEndMode.Touch) != null)
                    return new VerticalTaskRecord { pawn = pawn, kind = VerticalTaskKind.Work, stage = VerticalTaskStage.ToDestination,
                        destination = target, destinationMap = map.Parent, destinationCell = target.Position, workGiver = giver };
                continue;
            }
            foreach (var sourceMap in VerticalColonyUtility.Maps(site).Where(m => m != map && VerticalWorkSearch.WithinHaulDistance(site, m, map)))
            foreach (var source in VerticalWorkSearch.Scan(sourceMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver), 192, pawn))
            {
                if (!fuel.Props.fuelFilter.Allows(source) || !VerticalWorkSearch.SourceUsable(pawn, source)) continue;
                var count = Math.Min(needed, Math.Min(source.stackCount, pawn.carryTracker.MaxStackSpaceEver(source.def)));
                if (count <= 0 || fuel.Props.atomicFueling && count < needed
                    || VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, sourceMap, source, PathEndMode.ClosestTouch, true) == null
                    || VerticalRouteUtility.Find(pawn, sourceMap, source.Position, map, target, PathEndMode.Touch, true) == null) continue;
                return new VerticalTaskRecord { pawn = pawn, kind = VerticalTaskKind.RefuelSupply, stage = VerticalTaskStage.ToSource,
                    source = source, sourceMap = sourceMap.Parent, destination = target, destinationMap = map.Parent,
                    destinationCell = target.Position, count = count, workGiver = giver };
            }
        }
        return null;
    }
}
public sealed class JobDriver_VerticalRefuelCarried : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnForbidden(TargetIndex.A);
        this.FailOn(() => pawn.carryTracker.CarriedThing == null || !VerticalRefuelSupply.Valid(job.targetA.Thing));
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        yield return Toils_General.Wait(240, TargetIndex.A).WithProgressBarToilDelay(TargetIndex.A);
        yield return Toils_General.Do(() =>
        {
            var fuel = job.targetA.Thing.TryGetComp<CompRefuelable>(); var cargo = pawn.carryTracker.CarriedThing;
            if (!fuel.Props.fuelFilter.Allows(cargo) || fuel.Props.atomicFueling && cargo.stackCount < fuel.GetFuelCountToFullyRefuel())
            { EndJobWith(JobCondition.Incompletable); return; }
            fuel.Refuel(new List<Thing> { cargo });
        });
    }
}
