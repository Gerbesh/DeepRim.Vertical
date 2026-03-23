using System.Linq;
using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.VerticalState;
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
        VerticalRendering.VerticalOverlayDebugService.Log(
            $"TryCreateFloor sourceMap={sourceMap.uniqueID} sourceLevel={currentFloor.levelIndex} delta={levelDelta} targetLevel={targetLevel} anchor={anchorCell}.");
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
            VerticalRendering.VerticalOverlayDebugService.Log(
                $"TryCreateFloor reused existing targetMap={targetMap.uniqueID} targetLevel={targetLevel} site={site.siteId}.");
            VerticalCameraSyncService.JumpPreservingView(sourceMap, targetMap, anchorCell);
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
        else if (targetLevel > 0)
        {
            targetMap = MapGenerator.GenerateMap(
                sourceMap.Size,
                mapParent,
                VerticalDefOf.DeepRimVertical_UpperFloor,
                mapParent.ExtraGenStepDefs,
                map => UpperFloorGenerationService.PrepareForGeneration(map, sourceMap, anchorCell, targetLevel),
                false,
                false);
        }
        else
        {
            targetMap = MapGenerator.GenerateMap(sourceMap.Size, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, false, false);
        }

        component.RegisterPortal(sourceMap, currentFloor.levelIndex, targetLevel, anchorCell);
        if (targetLevel > 0)
        {
            UpperFloorStateService.MarkDirty(targetMap);
        }

        var mapComponent = targetMap.GetComponent<VerticalMapComponent>();
        if (mapComponent != null)
        {
            mapComponent.siteId = site.siteId;
            mapComponent.levelIndex = targetLevel;
        }

        VerticalRendering.VerticalOverlayDebugService.Log(
            $"TryCreateFloor generated targetMap={targetMap.uniqueID} targetLevel={targetLevel} parent={targetMap.Parent?.GetUniqueLoadID() ?? "null"} site={site.siteId}.");
        Messages.Message("DeepRimVertical.Messages.CreatedFloor".Translate(VerticalFloorLabel.Format(targetLevel)), MessageTypeDefOf.PositiveEvent, false);
        VerticalMapInvalidationService.MarkSiteDirty(sourceMap);
        VerticalCameraSyncService.JumpPreservingView(sourceMap, targetMap, anchorCell);
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
}
