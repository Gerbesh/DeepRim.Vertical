using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalColony;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalProductionSupply
{
    public static bool BillValid(VerticalTaskRecord task) => task.bill != null && task.destination is IBillGiver giver
        && giver.BillStack.Bills.Contains(task.bill) && task.bill.ShouldDoNow() && task.bill.PawnAllowedToStartAnew(task.pawn);
    private static bool Ingredient(Pawn pawn, Bill bill, IngredientCount ingredient, Thing thing) => ingredient.filter.Allows(thing)
        && bill.IsFixedOrAllowedIngredient(thing) && VerticalWorkSearch.SourceUsable(pawn, thing);
    private static bool Local(Pawn pawn, Thing bench, Bill bill, Thing thing) => thing.Position.DistanceToSquared(bench.Position) <= bill.ingredientSearchRadius * bill.ingredientSearchRadius
        && VerticalRouteUtility.Reach(pawn, bench.Map, bench.InteractionCell, thing, PathEndMode.ClosestTouch);
    private static float Required(Bill bill, IngredientCount ingredient) => bill.recipe.Worker.GetIngredientCount(ingredient, bill);
    private static float Value(Bill bill, ThingDef def) => bill.recipe.IngredientValueGetter.ValuePerUnitOf(def);
    public static VerticalTaskRecord Find(Pawn pawn, VerticalSiteRecord site, WorkGiver_DoBill scanner)
    {
        foreach (var map in VerticalColonyUtility.Maps(site))
        foreach (var bench in VerticalWorkSearch.Scan(map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest), 32, pawn))
        {
            if (bench is not IBillGiver giver || bench is Pawn || bench.Faction != pawn.Faction || bench.IsBurning()
                || !scanner.ThingIsUsableBillGiver(bench) || !giver.UsableForBillsAfterFueling()
                || !VerticalRouteUtility.Allowed(pawn, bench) || GameComponent_VerticalTasks.Get().Claimed(bench, pawn)) continue;
            if (bench.TryGetComp<CompRefuelable>() is CompRefuelable fuel && !fuel.HasFuel) continue;
            if (bench.TryGetComp<CompPowerTrader>() is CompPowerTrader power && !power.PowerOn) continue;
            foreach (var bill in giver.BillStack.Bills.OfType<Bill_Production>())
            {
                if (!bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn)
                    || bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) != null
                    || bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != scanner.def.workType
                    || bill is Bill_ProductionWithUft unfinished && unfinished.BoundUft != null) continue;
                var complete = true;
                foreach (var ingredient in bill.recipe.ingredients)
                {
                    var local = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .Where(t => Ingredient(pawn, bill, ingredient, t) && Local(pawn, bench, bill, t)).ToList();
                    var localValue = local.Sum(t => t.stackCount * Value(bill, t.def));
                    var required = Required(bill, ingredient);
                    if (localValue >= required) continue;
                    complete = false;
                    if (!VerticalRuntime.Settings.allowCrossLevelHauling) continue;
                    var incomingValue = GameComponent_VerticalTasks.Get().Tasks.Where(t => t.kind == VerticalTaskKind.BillSupply && t.bill == bill && t.destination == bench)
                        .Select(t => new { Task = t, Item = t.stage == VerticalTaskStage.ToSource ? t.source : t.pawn.carryTracker.CarriedThing })
                        .Where(t => t.Item != null && ingredient.filter.Allows(t.Item) && bill.IsFixedOrAllowedIngredient(t.Item))
                        .Sum(t => t.Task.count * Value(bill, t.Item.def));
                    foreach (var sourceMap in VerticalColonyUtility.Maps(site).Where(m => m != map && VerticalWorkSearch.WithinHaulDistance(site, m, map)))
                    foreach (var source in VerticalWorkSearch.Scan(sourceMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver), 192, pawn))
                    {
                        if (!Ingredient(pawn, bill, ingredient, source) || !bill.recipe.allowMixingIngredients && local.Count > 0 && local.All(t => t.def != source.def)) continue;
                        var value = Value(bill, source.def);
                        if (value <= 0) continue;
                        var needed = (int)Math.Ceiling((required - localValue - incomingValue) / value);
                        var count = Math.Min(needed, Math.Min(source.stackCount, pawn.carryTracker.MaxStackSpaceEver(source.def)));
                        if (count <= 0 || !bench.InteractionCell.Standable(map) || !VerticalRouteUtility.Allowed(pawn, map, bench.InteractionCell)) continue;
                        if (VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, sourceMap, source, PathEndMode.ClosestTouch, true) == null
                            || VerticalRouteUtility.Find(pawn, sourceMap, source.Position, map, bench.InteractionCell, PathEndMode.OnCell, true) == null) continue;
                        return new VerticalTaskRecord
                        {
                            pawn = pawn, kind = VerticalTaskKind.BillSupply, stage = VerticalTaskStage.ToSource,
                            source = source, sourceMap = sourceMap.Parent, destination = bench, destinationMap = map.Parent,
                            destinationCell = bench.InteractionCell, count = count, workGiver = scanner.def, bill = bill
                        };
                    }
                }
                if (complete && map != pawn.Map && VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, map, bench, PathEndMode.InteractionCell) != null)
                    return new VerticalTaskRecord
                    {
                        pawn = pawn, kind = VerticalTaskKind.Work, stage = VerticalTaskStage.ToDestination,
                        destination = bench, destinationMap = map.Parent, destinationCell = bench.Position, workGiver = scanner.def
                    };
            }
        }
        return null;
    }
}

public sealed class JobDriver_VerticalDeliverIngredient : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.B);
        this.FailOn(() => pawn.carryTracker.CarriedThing == null);
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        yield return Toils_General.Do(() =>
        {
            if (!pawn.carryTracker.TryDropCarriedThing(job.targetA.Cell, ThingPlaceMode.Near, out _))
                EndJobWith(JobCondition.Incompletable);
        });
    }
}
