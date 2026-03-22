using HarmonyLib;
using RimWorld;
using DeepRim.Vertical.VerticalWorld;
using System.Collections.Generic;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

[HarmonyPatch(typeof(Map), "get_CanEverExit")]
public static class Map_CanEverExit_Patch
{
    public static void Postfix(Map __instance, ref bool __result)
    {
        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        if (component.TryGetFloor(__instance, out _, out var floor) && floor.levelIndex < 0)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.IncidentTargetTags))]
public static class Map_IncidentTargetTags_Patch
{
    public static void Postfix(Map __instance, ref IEnumerable<IncidentTargetTagDef> __result)
    {
        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        if (component.TryGetFloor(__instance, out _, out var floor) && floor.levelIndex < 0)
        {
            __result = new[] { IncidentTargetTagDefOf.Map_Misc };
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker), "CanFireNowSub")]
public static class IncidentWorker_CanFireNowSub_Patch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (__instance is not IncidentWorker_RaidEnemy)
        {
            return true;
        }

        if (parms?.target is Map map
            && VerticalSiteWorldComponent.TryGet(out var component)
            && component.TryGetFloor(map, out _, out var floor)
            && floor.levelIndex < 0)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
