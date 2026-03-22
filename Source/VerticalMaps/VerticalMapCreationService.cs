using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class VerticalMapCreationService
{
    public static bool TryCreateFloor(Map sourceMap, IntVec3 anchorCell, int levelDelta, out Map targetMap)
    {
        targetMap = null;
        if (sourceMap == null || levelDelta == 0 || !VerticalSiteWorldComponent.TryGet(out var component))
        {
            return false;
        }

        var site = component.GetOrCreateSiteForMap(sourceMap);
        if (site == null || !component.TryGetFloor(sourceMap, out _, out var currentFloor))
        {
            Messages.Message("DeepRimVertical.Messages.CouldNotCreateSite".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var targetLevel = currentFloor.levelIndex + levelDelta;
        if (!IsWithinConfiguredBounds(targetLevel))
        {
            Messages.Message("DeepRimVertical.Messages.FloorOutOfRange".Translate(targetLevel), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        anchorCell = targetLevel < 0 ? ClampUndergroundAnchor(sourceMap, anchorCell) : anchorCell;

        var existing = site.FloorAt(targetLevel);
        if (existing?.Map != null)
        {
            targetMap = existing.Map;
            CameraJumper.TryJump(anchorCell, targetMap, CameraJumper.MovementMode.Cut);
            return true;
        }

        var mapParent = (VerticalSiteMapParent)WorldObjectMaker.MakeWorldObject(VerticalDefOf.DeepRimVertical_FloorSite);
        component.RegisterGeneratedFloor(site, mapParent, targetLevel, sourceMap);
        Find.WorldObjects.Add(mapParent);

        if (targetLevel < 0)
        {
            targetMap = MapGenerator.GenerateMap(
                sourceMap.Size,
                mapParent,
                mapParent.MapGeneratorDef,
                mapParent.ExtraGenStepDefs,
                map => UndergroundGenerationService.PrepareForGeneration(map, sourceMap, anchorCell, targetLevel),
                false,
                false);
        }
        else
        {
            targetMap = MapGenerator.GenerateMap(sourceMap.Size, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, false, false);
            PrepareGeneratedFloor(targetMap, anchorCell, targetLevel);
        }

        component.RegisterPortal(sourceMap, currentFloor.levelIndex, targetLevel, anchorCell);
        var mapComponent = targetMap.GetComponent<VerticalMapComponent>();
        if (mapComponent != null)
        {
            mapComponent.siteId = site.siteId;
            mapComponent.levelIndex = targetLevel;
        }

        Messages.Message("DeepRimVertical.Messages.CreatedFloor".Translate(VerticalFloorLabel.Format(targetLevel)), MessageTypeDefOf.PositiveEvent, false);
        CameraJumper.TryJump(anchorCell, targetMap, CameraJumper.MovementMode.Cut);
        return true;
    }

    private static bool IsWithinConfiguredBounds(int targetLevel)
    {
        if (targetLevel > 0)
        {
            return targetLevel <= VerticalRuntime.Settings.maxAboveGroundFloors;
        }

        return Mathf.Abs(targetLevel) <= VerticalRuntime.Settings.maxUndergroundFloors;
    }

    private static IntVec3 ClampUndergroundAnchor(Map map, IntVec3 anchorCell)
    {
        if (map == null)
        {
            return anchorCell;
        }

        return new IntVec3(
            Mathf.Clamp(anchorCell.x, 6, map.Size.x - 7),
            0,
            Mathf.Clamp(anchorCell.z, 6, map.Size.z - 7));
    }

    private static void PrepareGeneratedFloor(Map map, IntVec3 anchorCell, int levelIndex)
    {
        if (levelIndex < 0)
        {
            return;
        }

        var pocket = CellRect.CenteredOn(anchorCell, 2).ClipInsideMap(map);
        foreach (var cell in pocket.Cells)
        {
            map.roofGrid.SetRoof(cell, null);
            var things = map.thingGrid.ThingsListAtFast(cell).ToList();
            foreach (var thing in things)
            {
                if (thing is Pawn)
                {
                    continue;
                }

                if (thing.def.passability == Traversability.Impassable || thing.def.mineable || thing is Building)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
