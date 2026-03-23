using System.Collections.Generic;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalState;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
public static class FloatMenuMakerMap_GetOptions_VerticalTransferPatch
{
    public static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }

        var cell = IntVec3.FromVector3(clickPos);
        foreach (var option in VerticalTransferService.GetFloatMenuOptions(selectedPawns, map, cell))
        {
            __result.Add(option);
        }
    }
}

[HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
public static class Pawn_PathFollower_StartPath_VoidGuardPatch
{
    public static bool Prefix(Pawn_PathFollower __instance, LocalTargetInfo dest)
    {
        var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn?.Map == null || !dest.IsValid || !dest.Cell.InBounds(pawn.Map))
        {
            return true;
        }

        if (!UpperFloorStateService.IsWalkable(pawn.Map, dest.Cell))
        {
            __instance.StopDead();
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
public static class Pawn_PathFollower_TryEnterNextPathCell_VoidGuardPatch
{
    public static bool Prefix(Pawn_PathFollower __instance)
    {
        var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        var nextCell = Traverse.Create(__instance).Field("nextCell").GetValue<IntVec3>();
        if (pawn?.Map == null || !nextCell.InBounds(pawn.Map))
        {
            return true;
        }

        if (!UpperFloorStateService.IsWalkable(pawn.Map, nextCell))
        {
            __instance.StopDead();
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
public static class TerrainGrid_SetTerrain_UpperFloorStatePatch
{
    public static void Postfix(TerrainGrid __instance, IntVec3 c)
    {
        var map = Traverse.Create(__instance).Field("map").GetValue<Map>();
        if (map == null)
        {
            return;
        }

        UpperFloorStateService.MarkDirty(map);
        VerticalMapInvalidationService.MarkSiteCellDirty(map, c, 1UL);
    }
}
