using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalTransferService
{
    private static readonly Dictionary<int, PendingTransfer> PendingByPawnId = new();

    public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(List<Pawn> pawns, Map map, IntVec3 cell)
    {
        if (map == null || pawns == null || pawns.Count == 0 || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out _, out var floor))
        {
            yield break;
        }

        foreach (var portal in component.PortalsAt(map, cell).OrderBy(p => p.targetLevel))
        {
            var label = "DeepRimVertical.FloatMenu.UsePortal".Translate(VerticalFloorLabel.Format(portal.targetLevel));
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                foreach (var pawn in pawns.Where(p => p.Map == map))
                {
                    TryQueueTransfer(pawn, cell, portal.targetLevel, out _);
                }
            }), pawns[0], cell);
        }
    }

    public static bool TryQueueTransfer(Pawn pawn, IntVec3 portalCell, int targetLevel, out string reason)
    {
        reason = null;
        if (pawn == null || pawn.Map == null || !portalCell.InBounds(pawn.Map))
        {
            reason = "DeepRimVertical.Messages.NoPortalRoute".Translate();
            return false;
        }

        if (!VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(pawn.Map, out var site, out var currentFloor))
        {
            reason = "DeepRimVertical.Messages.NoPortalRoute".Translate();
            return false;
        }

        var targetFloor = site.FloorAt(targetLevel);
        if (targetFloor?.Map == null)
        {
            reason = "DeepRimVertical.Messages.NoPortalRoute".Translate();
            return false;
        }

        if (!pawn.CanReach(portalCell, PathEndMode.OnCell, Danger.Deadly))
        {
            reason = "DeepRimVertical.Messages.CannotReachPortal".Translate();
            return false;
        }

        PendingByPawnId[pawn.thingIDNumber] = new PendingTransfer(pawn, pawn.Map, portalCell, targetFloor.Map, targetLevel);
        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, portalCell), JobTag.Misc);
        return true;
    }

    public static void ProcessPendingTransfers(Map map)
    {
        if (map == null)
        {
            return;
        }

        foreach (var pair in PendingByPawnId.Values.Where(value => value.SourceMap == map).ToList())
        {
            var pawn = pair.Pawn;
            if (pawn == null || pawn.Destroyed || pawn.Map != map)
            {
                PendingByPawnId.Remove(pair.PawnId);
                continue;
            }

            if (pawn.Position != pair.SourceCell || pawn.pather.MovingNow)
            {
                continue;
            }

            var targetCell = FindTargetCell(pair.TargetMap, pair.SourceCell);
            pawn.DeSpawn();
            GenSpawn.Spawn(pawn, targetCell, pair.TargetMap, WipeMode.Vanish);
            pawn.pather.StopDead();
            PendingByPawnId.Remove(pair.PawnId);
            VerticalMapInvalidationService.MarkSiteDirty(pair.TargetMap);
        }
    }

    private static IntVec3 FindTargetCell(Map targetMap, IntVec3 preferredCell)
    {
        if (preferredCell.InBounds(targetMap) && UpperFloorStateService.IsWalkable(targetMap, preferredCell))
        {
            return preferredCell;
        }

        return CellFinder.TryFindRandomReachableNearbyCell(
            preferredCell,
            targetMap,
            6f,
            TraverseParms.For(TraverseMode.PassAllDestroyableThingsNotWater),
            cell => UpperFloorStateService.IsWalkable(targetMap, cell),
            null,
            out var result,
            16)
            ? result
            : targetMap.Center;
    }

    private sealed class PendingTransfer
    {
        public PendingTransfer(Pawn pawn, Map sourceMap, IntVec3 sourceCell, Map targetMap, int targetLevel)
        {
            Pawn = pawn;
            PawnId = pawn.thingIDNumber;
            SourceMap = sourceMap;
            SourceCell = sourceCell;
            TargetMap = targetMap;
            TargetLevel = targetLevel;
        }

        public Pawn Pawn { get; }

        public int PawnId { get; }

        public Map SourceMap { get; }

        public IntVec3 SourceCell { get; }

        public Map TargetMap { get; }

        public int TargetLevel { get; }
    }
}
