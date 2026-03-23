using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

[HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.GetMaterial), typeof(TerrainDef), typeof(bool), typeof(ColorDef))]
public static class TerrainGrid_GetMaterial_UpperVoidPatch
{
    public static void Postfix(TerrainGrid __instance, TerrainDef def, ref Material __result)
    {
        if (def != VerticalWorld.VerticalDefOf.DeepRimVertical_UpperVoid)
        {
            return;
        }

        var map = Traverse.Create(__instance).Field("map").GetValue<Map>();
        if (map == null || VerticalRenderContextService.GetLevel(map) <= 0)
        {
            return;
        }

        if (VerticalRuntime.Settings.enableUpperFloorOverlay
            && Find.CurrentMap != null
            && VerticalRenderContextService.SharesSite(map, Find.CurrentMap))
        {
            __result = VerticalSectionLayerUtility.UpperVoidTransparentMaterial;
            return;
        }

        __result = VerticalSectionLayerUtility.UpperVoidNeutralMaterial;
    }
}
