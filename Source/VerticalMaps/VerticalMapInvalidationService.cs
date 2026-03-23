using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalSupports;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class VerticalMapInvalidationService
{
    public static void MarkSiteDirty(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        foreach (var floor in component.GetOrderedFloors(map))
        {
            if (floor?.Map == null)
            {
                continue;
            }

            UpperFloorStateService.MarkDirty(floor.Map);
            UpperLevelTerrainGrid.MarkDirty(floor.Map);
            UpperOverlayVisibilityMask.MarkDirty(floor.Map);
            VerticalOverlayDebugService.Log($"Invalidated vertical render state for map {floor.Map.uniqueID} at level {VerticalRenderContextService.GetLevel(floor.Map)}.");
            VerticalSupportService.MarkDirty(floor.Map);
            VerticalGhostRenderer.MarkDirty(floor.Map);
            VerticalSectionRenderPatches.Reset(floor.Map);
            if (CanInvalidateDrawMesh(floor.Map))
            {
                floor.Map.mapDrawer.WholeMapChanged(ulong.MaxValue);
            }
        }
    }

    private static bool CanInvalidateDrawMesh(Map map)
    {
        if (map?.mapDrawer == null)
        {
            return false;
        }

        var sections = Traverse.Create(map.mapDrawer).Field("sections").GetValue<Section[,]>();
        return sections != null;
    }
}
