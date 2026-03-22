using HarmonyLib;
using System.Reflection;
using Verse;

namespace DeepRim.Vertical.VerticalTemperature;

[HarmonyPatch(typeof(MapTemperature), "get_OutdoorTemp")]
public static class MapTemperature_OutdoorTemp_Patch
{
    public static void Postfix(MapTemperature __instance, ref float __result)
    {
        var map = MapTemperatureAccess.Map(__instance);
        if (DepthTemperatureUtility.TryGetGeologicalTarget(map, out _, out var target))
        {
            __result = target;
        }
    }
}

[HarmonyPatch(typeof(MapTemperature), "get_SeasonalTemp")]
public static class MapTemperature_SeasonalTemp_Patch
{
    public static void Postfix(MapTemperature __instance, ref float __result)
    {
        var map = MapTemperatureAccess.Map(__instance);
        if (DepthTemperatureUtility.TryGetGeologicalTarget(map, out _, out var target))
        {
            __result = target;
        }
    }
}

[HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.GetTemperatureForCell))]
public static class GenTemperature_GetTemperatureForCell_Patch
{
    public static void Postfix(IntVec3 c, Map map, ref float __result)
    {
        __result = DepthTemperatureUtility.ApplyGeologicalPressure(map, c, __result);
    }
}

[HarmonyPatch(typeof(RoomTempTracker), "DeepEqualizationTempChangePerInterval")]
public static class RoomTempTracker_DeepEqualizationTempChangePerInterval_Patch
{
    public static bool Prefix(RoomTempTracker __instance, ref float __result)
    {
        var room = RoomTempTrackerAccess.Room(__instance);
        var map = room?.Map;
        if (!DepthTemperatureUtility.TryGetGeologicalTarget(map, out _, out var target))
        {
            return true;
        }

        var thickRoofCoverage = RoomTempTrackerAccess.ThickRoofCoverage(__instance);
        if (thickRoofCoverage < 0.001f)
        {
            __result = 0f;
            return false;
        }

        __result = (target - __instance.Temperature) * thickRoofCoverage * 0.002f * 120f;
        return false;
    }
}

internal static class MapTemperatureAccess
{
    private static readonly FieldInfo MapField = AccessTools.Field(typeof(MapTemperature), "map");

    public static Map Map(MapTemperature instance)
    {
        return (Map)MapField.GetValue(instance);
    }
}

internal static class RoomTempTrackerAccess
{
    private static readonly FieldInfo RoomField = AccessTools.Field(typeof(RoomTempTracker), "room");
    private static readonly FieldInfo ThickRoofCoverageField = AccessTools.Field(typeof(RoomTempTracker), "thickRoofCoverage");

    public static Room Room(RoomTempTracker instance)
    {
        return (Room)RoomField.GetValue(instance);
    }

    public static float ThickRoofCoverage(RoomTempTracker instance)
    {
        return (float)ThickRoofCoverageField.GetValue(instance);
    }
}
