using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalTransferService
{
    public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(List<Pawn> pawns, Map map, IntVec3 cell)
    {
        if (map == null || pawns == null || pawns.Count == 0 || !VerticalSiteWorldComponent.TryGet(out var component)
            || !component.TryGetFloor(map, out _, out _)) yield break;
        foreach (var portal in component.PortalsAt(map, cell)
            .Where(p => p.allowsPawnMove && !p.doorLikeBlockingState).OrderBy(p => p.targetLevel))
        {
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "DeepRimVertical.FloatMenu.UsePortal".Translate(VerticalFloorLabel.Format(portal.targetLevel)), () =>
                {
                    foreach (var pawn in pawns.Where(p => p.Map == map)) TryQueueTransfer(pawn, cell, portal.targetLevel, out _);
                }), pawns[0], cell);
        }
    }
    public static bool TryQueueTransfer(Pawn pawn, IntVec3 portalCell, int targetLevel, out string reason)
    {
        reason = "DeepRimVertical.Messages.NoPortalRoute".Translate();
        if (pawn?.Spawned != true || !VerticalSiteWorldComponent.TryGet(out var world)
            || !world.TryGetFloor(pawn.Map, out var site, out _)) return false;
        var destination = site.FloorAt(targetLevel)?.Map;
        if (destination == null || !world.PortalsAt(pawn.Map, portalCell).Any(p => p.targetLevel == targetLevel
            && VerticalRouteUtility.ValidHop(pawn, site, p, pawn.carryTracker.CarriedThing != null))) return false;
        if (GameComponent_VerticalTasks.Get()?.QueueTravel(pawn, portalCell, destination, targetLevel) != true) return false;
        reason = null; return true;
    }
}