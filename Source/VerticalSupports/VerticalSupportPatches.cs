using DeepRim.Vertical.VerticalMaps;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace DeepRim.Vertical.VerticalSupports;

[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.CanDesignateCell))]
public static class Designator_Build_CanDesignateCell_VerticalSupportPatch
{
    public static void Postfix(Designator_Build __instance, IntVec3 c, ref AcceptanceReport __result)
    {
        if (!__result.Accepted || Find.CurrentMap == null)
        {
            return;
        }

        var placingRot = ResolvePlacingRotation(__instance);
        if (!VerticalSupportService.AllowsPlacement(Find.CurrentMap, __instance.PlacingDef, c, placingRot, out var reason))
        {
            __result = new AcceptanceReport(reason);
        }
    }

    private static Rot4 ResolvePlacingRotation(Designator_Build designator)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = designator.GetType();

        var prop = type.GetProperty("PlacingRot", flags);
        if (prop?.PropertyType == typeof(Rot4))
        {
            return (Rot4)prop.GetValue(designator);
        }

        var field = type.GetField("placingRot", flags) ?? type.GetField("PlacingRot", flags);
        if (field?.FieldType == typeof(Rot4))
        {
            return (Rot4)field.GetValue(designator);
        }

        return Rot4.North;
    }
}

[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanPlaceBlueprintAt))]
public static class GenConstruct_CanPlaceBlueprintAt_VerticalSupportPatch
{
    public static void Postfix(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map, ref AcceptanceReport __result)
    {
        if (!__result.Accepted)
        {
            return;
        }

        if (!VerticalSupportService.AllowsPlacement(map, entDef, center, rot, out var reason))
        {
            __result = new AcceptanceReport(reason);
        }
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
public static class Thing_SpawnSetup_VerticalInvalidationPatch
{
    public static void Postfix(Thing __instance, Map map)
    {
        if (__instance is Pawn or Plant or Mote)
        {
            return;
        }

        VerticalMapInvalidationService.MarkSiteCellsDirty(map, __instance.OccupiedRect(), 1UL);
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
public static class Thing_DeSpawn_VerticalInvalidationPatch
{
    public readonly struct DeSpawnState
    {
        public DeSpawnState(Map map, CellRect rect)
        {
            Map = map;
            Rect = rect;
        }

        public Map Map { get; }

        public CellRect Rect { get; }
    }

    public static void Prefix(Thing __instance, out DeSpawnState __state)
    {
        __state = new DeSpawnState(__instance.Map, __instance.OccupiedRect());
    }

    public static void Postfix(DeSpawnState __state)
    {
        if (__state.Map != null)
        {
            VerticalMapInvalidationService.MarkSiteCellsDirty(__state.Map, __state.Rect, 1UL);
        }
    }
}
