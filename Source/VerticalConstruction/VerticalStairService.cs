using System.Linq;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalConstruction;

public sealed class VerticalStairExtension : DefModExtension
{
    public int levelDelta;
    public bool isExit;
}

public static class VerticalStairService
{
    public static AcceptanceReport CanBuild(Map map, IntVec3 cell, int delta, Thing ignore = null)
    {
        if (map == null || !cell.InBounds(map) || cell.DistanceToEdge(map) < 2)
        {
            return new AcceptanceReport("DeepRimVertical.Stairs.TooCloseToEdge".Translate());
        }

        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return new AcceptanceReport("DeepRimVertical.Messages.CouldNotCreateSite".Translate());
        }

        // Placement previews must not adopt maps or generate levels.
        var currentLevel = component.TryGetFloor(map, out var site, out var floor) ? floor.levelIndex : 0;
        var targetLevel = currentLevel + delta;
        if (!VerticalMapCreationService.IsWithinConfiguredBounds(targetLevel))
        {
            return new AcceptanceReport("DeepRimVertical.Messages.FloorOutOfRange".Translate(targetLevel));
        }

        if (cell.GetThingList(map).Any(thing => thing != ignore
            && (thing is Building_VerticalStair || thing.def.entityDefToBuild is ThingDef planned
                && planned.GetModExtension<VerticalStairExtension>() != null)))
        {
            return new AcceptanceReport("DeepRimVertical.Stairs.AlreadyOccupied".Translate());
        }

        var targetMap = site?.FloorAt(targetLevel)?.Map;
        return targetMap == null ? AcceptanceReport.WasAccepted : CanOpenLanding(targetMap, cell);
    }

    public static bool Connect(Building_VerticalStair stair)
    {
        if (!stair.Spawned || stair.LinkedStair != null)
        {
            return false;
        }

        var sourceMap = stair.Map;
        var cell = stair.Position;
        var delta = stair.def.GetModExtension<VerticalStairExtension>().levelDelta;
        var report = CanBuild(sourceMap, cell, delta, stair);
        if (!report.Accepted)
        {
            Messages.Message(report.Reason, stair, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (!VerticalMapCreationService.TryCreateFloor(sourceMap, delta, out var targetMap, jumpToFloor: false))
        {
            return false;
        }

        // Recheck after generation and before changing any target cells.
        report = CanOpenLanding(targetMap, cell);
        if (!report.Accepted)
        {
            Messages.Message(report.Reason, stair, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        foreach (var rock in cell.GetThingList(targetMap).Where(thing => thing.def.building?.isNaturalRock == true).ToList())
        {
            rock.Destroy(DestroyMode.Vanish);
        }

        if (targetMap.terrainGrid.TerrainAt(cell) == VerticalDefOf.DeepRimVertical_UpperVoid)
        {
            targetMap.terrainGrid.SetTerrain(cell, VerticalDefOf.DeepRimVertical_UpperDeck);
        }

        var exitDef = DefDatabase<ThingDef>.GetNamed(delta > 0
            ? "DeepRimVertical_StairExitDown" : "DeepRimVertical_StairExitUp");
        var exit = (Building_VerticalStair)ThingMaker.MakeThing(exitDef, stair.Stuff ?? ThingDefOf.Steel);
        exit.SetFaction(stair.Faction);
        stair.LinkedStair = exit;
        exit.LinkedStair = stair;
        GenSpawn.Spawn(exit, cell, targetMap, stair.Rotation, WipeMode.Vanish);

        if (VerticalSiteWorldComponent.TryGet(out var component)
            && component.TryGetFloor(sourceMap, out _, out var floor))
        {
            component.RegisterPortal(sourceMap, floor.levelIndex, floor.levelIndex + delta, cell);
        }

        targetMap.fogGrid.Unfog(cell);
        targetMap.fogGrid.FloodUnfogAdjacent(cell, sendLetters: false);
        return true;
    }

    private static AcceptanceReport CanOpenLanding(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map))
        {
            return new AcceptanceReport("DeepRimVertical.Stairs.LandingBlocked".Translate());
        }

        var terrain = map.terrainGrid.TerrainAt(cell);
        if (terrain != VerticalDefOf.DeepRimVertical_UpperVoid && terrain.passability == Traversability.Impassable)
        {
            return new AcceptanceReport("DeepRimVertical.Stairs.LandingBlocked".Translate());
        }

        foreach (var thing in cell.GetThingList(map))
        {
            if (thing.def.building?.isNaturalRock == true)
            {
                continue;
            }

            if (thing is Building || thing is Blueprint || thing is Frame)
            {
                return new AcceptanceReport("DeepRimVertical.Stairs.LandingBlocked".Translate());
            }
        }

        return AcceptanceReport.WasAccepted;
    }
}

public sealed class PlaceWorker_VerticalStair : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
        Thing thingToIgnore = null, Thing thing = null)
    {
        return VerticalStairService.CanBuild(map, loc,
            ((ThingDef)checkingDef).GetModExtension<VerticalStairExtension>().levelDelta, thingToIgnore);
    }
}
