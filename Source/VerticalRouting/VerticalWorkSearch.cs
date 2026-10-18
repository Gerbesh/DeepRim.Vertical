using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalWorkSearch
{
    public static bool Eligible(Pawn pawn, bool allowCarrying = false) => pawn?.Spawned == true && pawn.IsColonist && pawn.RaceProps.Humanlike
        && !pawn.Drafted && !pawn.Downed && !pawn.InMentalState && !pawn.Dead && pawn.GetLord() == null
        && !pawn.health.hediffSet.InLabor() && pawn.workSettings?.EverWork == true && (allowCarrying || pawn.carryTracker.CarriedThing == null);
    public static bool CanUse(Pawn pawn, WorkGiver giver) => giver != null && !pawn.WorkTagIsDisabled(giver.def.workTags)
        && (giver.def.workType == null || !pawn.WorkTypeIsDisabled(giver.def.workType) && pawn.workSettings.GetPriority(giver.def.workType) > 0)
        && giver.MissingRequiredCapacity(pawn) == null;

    public static Job Find(Pawn pawn, Job localJob, bool emergency = false)
    {
        if (!Eligible(pawn, emergency) || !VerticalSiteWorldComponent.TryGet(out var world) || !world.TryGetFloor(pawn.Map, out var site, out _)
            || site.floors.Count < 2 || site.portals.Count == 0) return null;
        var service = GameComponent_VerticalTasks.Get();
        if (service == null || !service.CanSearch(pawn, emergency)) return null;
        var givers = emergency ? pawn.workSettings.WorkGiversInOrderEmergency : pawn.workSettings.WorkGiversInOrderNormal;
        var localIndex = localJob?.workGiverDef == null ? (localJob != null ? -1 : int.MaxValue)
            : givers.FindIndex(g => g.def == localJob.workGiverDef);
        if (localJob != null && (localIndex < 0 || VerticalRuntime.Settings.preferSameLevelWork)) return null;
        for (var i = 0; i < givers.Count && i <= localIndex; i++)
        {
            var giver = givers[i];
            var competingHaul = i == localIndex;
            if (competingHaul && (giver.GetType() != typeof(WorkGiver_HaulGeneral)
                || localJob?.def != JobDefOf.HaulToCell || !localJob.targetB.Cell.InBounds(pawn.Map))) continue;
            if (!CanUse(pawn, giver)) continue;
            VerticalTaskRecord task = null;
            if (giver.GetType() == typeof(WorkGiver_Miner)) task = FindMine(pawn, site, giver.def);
            else if (giver.GetType() == typeof(WorkGiver_ConstructFinishFrames)) task = FindFrame(pawn, site, giver.def);
            else if (giver is WorkGiver_ConstructDeliverResources && VerticalRuntime.Settings.allowCrossLevelHauling)
                task = FindSupply(pawn, site, giver.def);
            else if (giver.GetType() == typeof(WorkGiver_HaulGeneral) && VerticalRuntime.Settings.allowCrossLevelHauling)
                task = FindHaul(pawn, site, giver.def);
            else if (giver is WorkGiver_DoBill billScanner) task = VerticalProductionSupply.Find(pawn, site, billScanner);
            else if (giver is WorkGiver_Refuel) task = VerticalRefuelSupply.Find(pawn, site, giver.def);
            else if (giver is WorkGiver_Scanner simpleScanner) task = VerticalSimpleWork.Find(pawn, site, simpleScanner);
            if (task == null) continue;
            if (competingHaul)
            {
                var localPriority = localJob.targetB.Cell.GetSlotGroup(pawn.Map)?.Settings.Priority ?? StoragePriority.Unstored;
                var remotePriority = task.destinationCell.GetSlotGroup(task.destinationMap.Map)?.Settings.Priority ?? StoragePriority.Unstored;
                if (remotePriority <= localPriority) continue;
            }
            if (emergency && service.HasTask(pawn)) service.CancelPawn(pawn);
            var job = service.Accept(task);
            if (job != null) { job.workGiverDef = giver.def; return job; }
        }
        return null;
    }
    private static IEnumerable<Map> Maps(VerticalSiteRecord site) => site.OrderedFloors.Select(f => f.Map).Where(m => m != null);
    public static IEnumerable<T> Scan<T>(IEnumerable<T> candidates, int budget, Pawn pawn)
    {
        var list = candidates as IList<T> ?? candidates.ToList();
        if (list.Count == 0) yield break;
        // Advance the bounded window so unreachable early candidates cannot starve later work.
        var start = (int)(((long)(Verse.Find.TickManager.TicksGame / 180) + pawn.thingIDNumber) * budget % list.Count);
        for (var i = 0; i < Math.Min(budget, list.Count); i++) yield return list[(start + i) % list.Count];
    }
    private static VerticalTaskRecord Work(Pawn pawn, Thing target, WorkGiverDef giver) => new()
    {
        pawn = pawn, kind = VerticalTaskKind.Work, stage = VerticalTaskStage.ToDestination,
        destination = target, destinationMap = target.Map.Parent, destinationCell = target.Position, workGiver = giver
    };
    private static bool FreeTarget(Pawn pawn, Thing thing) => VerticalRouteUtility.Allowed(pawn, thing)
        && !GameComponent_VerticalTasks.Get().Claimed(thing, pawn)
        && !thing.Map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant != pawn && r.Target.Thing == thing);
    private static bool RouteTo(Pawn pawn, Thing target, bool cargo = false) => VerticalRouteUtility.Find(pawn,
        pawn.Map, pawn.Position, target.Map, target, PathEndMode.Touch, cargo) != null;
    private static VerticalTaskRecord FindMine(Pawn pawn, VerticalSiteRecord site, WorkGiverDef giver)
    {
        foreach (var map in Maps(site).Where(m => m != pawn.Map))
        foreach (var designation in Scan(map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine)
            .Concat(map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.MineVein)), 64, pawn))
        {
            var mineable = designation.target.Cell.GetFirstMineable(map);
            if (mineable != null && FreeTarget(pawn, mineable) && RouteTo(pawn, mineable)) return Work(pawn, mineable, giver);
        }
        return null;
    }
    private static VerticalTaskRecord FindFrame(Pawn pawn, VerticalSiteRecord site, WorkGiverDef giver)
    {
        foreach (var frame in Scan(Maps(site).Where(m => m != pawn.Map).SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)).OfType<Frame>(), 64, pawn))
        {
            if (!frame.IsCompleted() || frame.Faction != pawn.Faction || !FreeTarget(pawn, frame)) continue;
            // In 1.7, some pawns have no Construction skill record (disabled skill / modded race).
            // A null SkillRecord or partially initialized frame must not break their whole think tree.
            if (frame.BuildDef == null || pawn.skills?.GetSkill(SkillDefOf.Construction) is not { } construction
                || construction.Level < frame.BuildDef.constructionSkillPrerequisite) continue;
            if (RouteTo(pawn, frame)) return Work(pawn, frame, giver);
        }
        return null;
    }
    public static bool ValidStoreCell(Pawn pawn, Map map, IntVec3 cell, Thing item)
    {
        return cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map) && VerticalRouteUtility.Allowed(pawn, map, cell)
            && !cell.ContainsStaticFire(map) && cell.IsValidStorageFor(map, item)
            && (cell.GetSlotGroup(map).parent is not Thing building || building.Faction == pawn.Faction)
            && cell.GetSlotGroup(map).parent.HaulDestinationEnabled
            && !map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant != pawn && !r.Target.HasThing && r.Target.Cell == cell)
            && !GameComponent_VerticalTasks.Get().ClaimedCell(map, cell, pawn);
    }
    private static int Capacity(Map map, IntVec3 cell, Thing item)
    {
        if (cell.GetItemCount(map) < cell.GetMaxItemsAllowedInCell(map)) return item.def.stackLimit;
        return cell.GetThingList(map).Where(t => t.CanStackWith(item)).Sum(t => Math.Max(0, t.def.stackLimit - t.stackCount));
    }
    public static bool SourceUsable(Pawn pawn, Thing source)
    {
        if (!FreeTarget(pawn, source) || !source.def.EverHaulable || source.Fogged() || source.IsBurning()
            || source is UnfinishedThing || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
        // Use native social and per-target restrictions after scoping only the area read.
        using (new RemoteReachContext.Scope(pawn, source.Map))
            return !source.IsForbidden(pawn) && (!source.def.IsNutritionGivingIngestible
                || source.IsSociallyProper(pawn, false, true));
    }
    public static bool WithinHaulDistance(VerticalSiteRecord site, Map a, Map b) =>
        Math.Abs(site.floors.First(f => f.Map == a).levelIndex - site.floors.First(f => f.Map == b).levelIndex)
            <= VerticalRuntime.Settings.crossLevelHaulMaxFloorDistance;
    private static VerticalTaskRecord FindHaul(Pawn pawn, VerticalSiteRecord site, WorkGiverDef giver)
    {
        foreach (var sourceMap in Maps(site))
        foreach (var item in Scan(sourceMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver), 192, pawn))
        {
            if (!SourceUsable(pawn, item) || !item.def.alwaysHaulable && sourceMap.designationManager.DesignationOn(item, DesignationDefOf.Haul) == null && !item.IsInValidStorage()) continue;
            var priority = StoreUtility.CurrentStoragePriorityOf(item);
            foreach (var destination in Maps(site).Where(m => m != sourceMap && WithinHaulDistance(site, sourceMap, m))
                .SelectMany(m => m.haulDestinationManager.AllGroupsListInPriorityOrder.Select(g => new { Map = m, Group = g }))
                .OrderByDescending(d => d.Group.Settings.Priority))
            {
                var targetMap = destination.Map;
                var group = destination.Group;
                if (group.Settings.Priority <= priority || !group.parent.Accepts(item)) continue;
                // Prefer a higher local destination if one exists; do not bypass vanilla priorities.
                if (StoreUtility.TryFindBestBetterStoreCellFor(item, null, sourceMap, priority, pawn.Faction, out var localCell)
                    && localCell.GetSlotGroup(sourceMap).Settings.Priority >= group.Settings.Priority) continue;
                foreach (var cell in Scan(group.CellsList, 64, pawn))
                {
                    if (!ValidStoreCell(pawn, targetMap, cell, item)) continue;
                    var count = Math.Min(item.stackCount, Math.Min(pawn.carryTracker.MaxStackSpaceEver(item.def), Capacity(targetMap, cell, item)));
                    if (count <= 0) continue;
                    if (VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, sourceMap, item, PathEndMode.ClosestTouch, true) == null
                        || VerticalRouteUtility.Find(pawn, sourceMap, item.Position, targetMap, cell, PathEndMode.OnCell, true) == null) continue;
                    return new VerticalTaskRecord
                    {
                        pawn = pawn, kind = VerticalTaskKind.Haul, stage = VerticalTaskStage.ToSource, source = item,
                        sourceMap = sourceMap.Parent, destinationMap = targetMap.Parent, destinationCell = cell, count = count, workGiver = giver
                    };
                }
            }
        }
        return null;
    }
    private static VerticalTaskRecord FindSupply(Pawn pawn, VerticalSiteRecord site, WorkGiverDef giver)
    {
        var scanner = (WorkGiver_Scanner)giver.Worker;
        foreach (var targetMap in Maps(site))
        foreach (var destination in Scan(targetMap.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest), 64, pawn))
        {
            if (destination is not IConstructible constructible || destination is Blueprint_Install
                || destination.Faction != pawn.Faction || !VerticalRouteUtility.Allowed(pawn, destination)) continue;
            if (GenConstruct.FirstBlockingThing(destination, pawn) != null) continue;
            foreach (var material in constructible.TotalMaterialCost())
            {
                var remaining = destination is IHaulEnroute enroute ? enroute.GetSpaceRemainingWithEnroute(material.thingDef, pawn) : constructible.ThingCountNeeded(material.thingDef);
                remaining -= GameComponent_VerticalTasks.Get().Incoming(destination, material.thingDef);
                if (remaining <= 0) continue;
                foreach (var sourceMap in Maps(site).Where(m => m != targetMap && WithinHaulDistance(site, m, targetMap)))
                foreach (var source in Scan(sourceMap.listerThings.ThingsOfDef(material.thingDef), 64, pawn))
                {
                    if (!SourceUsable(pawn, source)) continue;
                    var count = Math.Min(remaining, Math.Min(source.stackCount, pawn.carryTracker.MaxStackSpaceEver(source.def)));
                    if (count <= 0 || VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, sourceMap, source, PathEndMode.ClosestTouch, true) == null
                        || VerticalRouteUtility.Find(pawn, sourceMap, source.Position, targetMap, destination, PathEndMode.Touch, true) == null) continue;
                    return new VerticalTaskRecord
                    {
                        pawn = pawn, kind = VerticalTaskKind.Supply, stage = VerticalTaskStage.ToSource,
                        source = source, sourceMap = sourceMap.Parent, destination = destination, destinationMap = targetMap.Parent,
                        destinationCell = destination.Position, workGiver = giver, count = count,
                        destinationBuildDef = destination.def.entityDefToBuild, destinationStuff = destination.Stuff
                    };
                }
            }
        }
        return null;
    }
}
