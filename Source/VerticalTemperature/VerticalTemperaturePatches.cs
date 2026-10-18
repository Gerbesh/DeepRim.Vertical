using HarmonyLib;
using Verse;

namespace DeepRim.Vertical.VerticalTemperature;

[HarmonyPatch(typeof(MapTemperature), "get_OutdoorTemp")]
public static class MapTemperature_OutdoorTemp_Patch
{
    public static void Postfix(MapTemperature __instance, ref float __result)
    {
        var map = MapTemperatureAccess.Map(__instance);
        if (UpperFloorClimateService.TryGetMirroredOutdoorTemp(map, out var mirroredTemp))
        {
            __result = mirroredTemp;
            return;
        }

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
        if (UpperFloorClimateService.TryGetMirroredSeasonalTemp(map, out var mirroredTemp))
        {
            __result = mirroredTemp;
            return;
        }

        if (DepthTemperatureUtility.TryGetGeologicalTarget(map, out _, out var target))
        {
            __result = target;
        }
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
    // Cache private field accessors for .NET 10 rather than reflecting every temperature read.
    private static readonly AccessTools.FieldRef<MapTemperature, Map> MapField =
        AccessTools.FieldRefAccess<MapTemperature, Map>("map");

    public static Map Map(MapTemperature instance) => instance == null ? null : MapField(instance);
}

internal static class RoomTempTrackerAccess
{
    private static readonly AccessTools.FieldRef<RoomTempTracker, Room> RoomField =
        AccessTools.FieldRefAccess<RoomTempTracker, Room>("room");
    private static readonly AccessTools.FieldRef<RoomTempTracker, float> ThickRoofCoverageField =
        AccessTools.FieldRefAccess<RoomTempTracker, float>("thickRoofCoverage");

    public static Room Room(RoomTempTracker instance) => instance == null ? null : RoomField(instance);
    public static float ThickRoofCoverage(RoomTempTracker instance) => instance == null ? 0f : ThickRoofCoverageField(instance);
}
