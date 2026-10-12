using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public sealed class JobDriver_VerticalTravel : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        if (GameComponent_VerticalTasks.Get()?.OnTravelStart(pawn, job) != true)
        {
            yield return Toils_General.Do(() => EndJobWith(JobCondition.Incompletable));
            yield break;
        }
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        yield return Toils_General.Wait(job.count > 0 ? job.count : 45);
        var ready = ToilMaker.MakeToil("VerticalArrival");
        ready.initAction = () => GameComponent_VerticalTasks.Get()?.MarkReady(pawn, job.loadID);
        ready.defaultCompleteMode = ToilCompleteMode.Never;
        yield return ready;
    }
}

public sealed class JobDriver_VerticalPickup : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, job.count, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnForbidden(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
        yield return Toils_Haul.StartCarryThing(TargetIndex.A);
        var ready = ToilMaker.MakeToil("VerticalCargoReady");
        ready.initAction = () => GameComponent_VerticalTasks.Get()?.MarkReady(pawn, job.loadID);
        ready.defaultCompleteMode = ToilCompleteMode.Never;
        yield return ready;
    }
}

public sealed class JobDriver_VerticalDeliver : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => pawn.carryTracker.CarriedThing == null || !job.targetA.Cell.IsValidStorageFor(pawn.Map, pawn.carryTracker.CarriedThing));
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        var place = ToilMaker.MakeToil("VerticalStoreCargo");
        place.initAction = () =>
        {
            // Never use the native storage fallback that can destroy unplaceable cargo.
            if (!pawn.carryTracker.TryDropCarriedThing(job.targetA.Cell, ThingPlaceMode.Direct, out _))
                EndJobWith(JobCondition.Incompletable);
        };
        yield return place;
    }
}
