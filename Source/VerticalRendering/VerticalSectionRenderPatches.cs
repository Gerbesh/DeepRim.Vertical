using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public static class VerticalSectionRenderPatches
{
    private static readonly Dictionary<int, VerticalSectionRenderState> RenderStatesByMapId = new();
    private static int renderingMapId = -999999;
    private static int lastLevelDiff;

    public static bool RenderMapMeshRecursively(Map map)
    {
        if (!ShouldRender(map))
        {
            return true;
        }

        var viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1).ClipInsideMap(map);
        RenderMap(map, viewRect);
        return false;
    }

    public static void RenderMap(Map map, CellRect viewRect)
    {
        if (map == Find.CurrentMap && VerticalRenderContextService.GetLevel(map) > 0)
        {
            DrawSelectiveLowerUnderlay(map, viewRect);
            DrawUpperNeutralBase(map, viewRect);
        }

        var sections = Traverse.Create(map.mapDrawer).Field("sections").GetValue<Section[,]>();
        foreach (var (x, y) in EnumerateVisibleSections(viewRect, sections))
        {
            var section = sections[x, y];
            if (section != null && viewRect.Overlaps(section.Bounds))
            {
                section.DrawSection();
                section.DrawDynamicSections(viewRect);
            }
        }
        VerticalOverlay.VerticalGhostRenderer.DrawFor(map);
    }

    public static void BuildSectionWorkers(Map mapRendering)
    {
        renderingMapId = Find.CurrentMap.uniqueID;
        DirtyRenderingMapCells(mapRendering);
    }

    public static void DirtyRenderingMapCells(Map mapRendering)
    {
        UpperOverlayVisibilityMask.MarkDirty(mapRendering);
        var terrainGrid = UpperLevelTerrainGrid.GetFor(mapRendering);
        var grounds = terrainGrid?.GetGroundsAtLevel(VerticalRenderContextService.GetLevel(mapRendering));
        if (grounds == null)
        {
            return;
        }

        foreach (var cell in grounds)
        {
            mapRendering.mapDrawer.MapMeshDirty(cell, 1UL);
        }
    }

    public static void DirtyRenderingMapCells(Map mapRendering, IntVec3 loc)
    {
        _ = loc;
        UpperLevelTerrainGrid.MarkDirty(mapRendering);
        UpperOverlayVisibilityMask.MarkDirty(mapRendering);
        if (mapRendering != null && RenderStatesByMapId.TryGetValue(mapRendering.uniqueID, out var renderState))
        {
            renderState.MarkDirty(loc);
        }
    }

    public static void BlurLowerLevels(Map map, int level, CellRect viewRect)
    {
        _ = map;
        _ = level;
        _ = viewRect;
    }

    public static void BlurLowerLevel(int levelDiff, Section section)
    {
        if (levelDiff != lastLevelDiff)
        {
            VerticalSectionLayerUtility.SetTintMaterialColor(levelDiff);
            lastLevelDiff = levelDiff;
        }

        var center = section.GetCenter();
        center.y += Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
        Graphics.DrawMesh(VerticalSectionLayerUtility.SectionMesh, center, Quaternion.identity, VerticalSectionLayerUtility.TintMaterial, 0);
    }

    public static void Reset(Map map)
    {
        _ = map;
        if (map != null)
        {
            RenderStatesByMapId.Remove(map.uniqueID);
        }

        renderingMapId = -999999;
    }

    private static bool ShouldRender(Map map)
    {
        if (map == null || Current.ProgramState != ProgramState.Playing || !VerticalRuntime.Settings.enableUpperFloorOverlay || Find.CurrentMap == null)
        {
            return false;
        }

        if (!VerticalRenderContextService.TryGetContext(Find.CurrentMap, out var activeContext) || activeContext.CurrentFloor.levelIndex <= 0)
        {
            return false;
        }

        var mapLevel = VerticalRenderContextService.GetLevel(map);
        if (map == Find.CurrentMap && mapLevel == 0)
        {
            Reset(map);
            return false;
        }

        if (mapLevel < 0)
        {
            return false;
        }

        return VerticalRenderContextService.SharesSite(map, Find.CurrentMap)
               && mapLevel <= activeContext.CurrentFloor.levelIndex;
    }

    private static void DrawUpperNeutralBase(Map map, CellRect viewRect)
    {
        if (VerticalRenderContextService.GetLevel(map) <= 0)
        {
            return;
        }

        var mask = UpperOverlayVisibilityMask.GetFor(map);
        var overlayEnabled = VerticalRuntime.Settings.enableUpperFloorOverlay;
        foreach (var cell in viewRect.Cells)
        {
            var cellState = mask.GetCellState(cell, overlayEnabled);
            if (!cellState.ShowNeutralBase)
            {
                continue;
            }

            var position = cell.ToVector3ShiftedWithAltitude(AltitudeLayer.Terrain);
            position.y -= 0.01f;
            Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity, VerticalSectionLayerUtility.UpperVoidNeutralMaterial, 0);
        }
    }

    private static void DrawSelectiveLowerUnderlay(Map map, CellRect viewRect)
    {
        if (VerticalRenderContextService.GetLevel(map) <= 0 || !VerticalRuntime.Settings.enableUpperFloorOverlay)
        {
            return;
        }

        if (!VerticalRenderContextService.TryGetContext(map, out var context) || context.LowerFloors.Count == 0)
        {
            return;
        }

        var terrainGrid = UpperLevelTerrainGrid.GetFor(map);
        var mask = UpperOverlayVisibilityMask.GetFor(map);
        if (terrainGrid == null || mask == null)
        {
            return;
        }

        var renderState = GetOrCreateRenderState(map);
        renderState.RebuildWorkers(map, context, terrainGrid, ToVisibleSectionRect(viewRect));
        var overlayEnabled = VerticalRuntime.Settings.enableUpperFloorOverlay;
        foreach (var worker in renderState.GetActiveWorkers())
        {
            worker.Layer.Prepare(worker.SourceMap, worker.LevelIndex, terrainGrid, worker.DistanceFromActive, worker.Alpha, mask, overlayEnabled);
            if (worker.Dirty)
            {
                worker.Layer.Regenerate();
                worker.Dirty = false;
            }

            worker.Layer.DrawLowerLevel(viewRect);
        }
    }

    private static IEnumerable<(int x, int y)> EnumerateVisibleSections(CellRect viewRect, Section[,] sections)
    {
        if (sections == null)
        {
            yield break;
        }

        var maxX = sections.GetLength(0) - 1;
        var maxY = sections.GetLength(1) - 1;
        var minSectionX = Mathf.Clamp(viewRect.minX / Section.Size, 0, maxX);
        var maxSectionX = Mathf.Clamp(viewRect.maxX / Section.Size, 0, maxX);
        var minSectionY = Mathf.Clamp(viewRect.minZ / Section.Size, 0, maxY);
        var maxSectionY = Mathf.Clamp(viewRect.maxZ / Section.Size, 0, maxY);

        for (var x = minSectionX; x <= maxSectionX; x++)
        {
            for (var y = minSectionY; y <= maxSectionY; y++)
            {
                yield return (x, y);
            }
        }
    }

    private static VerticalSectionRenderState GetOrCreateRenderState(Map map)
    {
        if (!RenderStatesByMapId.TryGetValue(map.uniqueID, out var state))
        {
            state = new VerticalSectionRenderState(map);
            RenderStatesByMapId[map.uniqueID] = state;
            VerticalOverlayDebugService.Log($"Created selective render state for map {map.uniqueID} at level {VerticalRenderContextService.GetLevel(map)}.");
        }

        return state;
    }

    private static CellRect ToVisibleSectionRect(CellRect viewRect)
    {
        var minX = viewRect.minX / Section.Size;
        var minZ = viewRect.minZ / Section.Size;
        var maxX = viewRect.maxX / Section.Size;
        var maxZ = viewRect.maxZ / Section.Size;
        return CellRect.FromLimits(minX, minZ, maxX, maxZ);
    }
}

[HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.DrawMapMesh))]
public static class MapDrawer_DrawMapMesh_VerticalSectionRender_Patch
{
    public static bool Prefix(MapDrawer __instance)
    {
        var map = Traverse.Create(__instance).Field("map").GetValue<Map>();
        return VerticalSectionRenderPatches.RenderMapMeshRecursively(map);
    }
}

[HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.MapMeshDirty), typeof(IntVec3), typeof(ulong))]
public static class MapDrawer_MapMeshDirty_VerticalSectionRender_Patch
{
    public static void Postfix(MapDrawer __instance, IntVec3 loc)
    {
        var map = Traverse.Create(__instance).Field("map").GetValue<Map>();
        VerticalSectionRenderPatches.DirtyRenderingMapCells(map, loc);
    }
}
