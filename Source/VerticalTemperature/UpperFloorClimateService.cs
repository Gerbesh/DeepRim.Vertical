using DeepRim.Vertical.VerticalRendering;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace DeepRim.Vertical.VerticalTemperature;

public static class UpperFloorClimateService
{
    private static readonly FieldInfo CurWeatherField = AccessTools.Field(typeof(WeatherManager), "curWeather");
    private static readonly FieldInfo LastWeatherField = AccessTools.Field(typeof(WeatherManager), "lastWeather");
    private static readonly FieldInfo PrevSkyTargetLerpField = AccessTools.Field(typeof(WeatherManager), "prevSkyTargetLerp");
    private static readonly FieldInfo CurrSkyTargetLerpField = AccessTools.Field(typeof(WeatherManager), "currSkyTargetLerp");
    private static readonly FieldInfo CurWeatherAgeField = AccessTools.Field(typeof(WeatherManager), "curWeatherAge");
    private static readonly FieldInfo TickedLastWeatherField = AccessTools.Field(typeof(WeatherManager), "tickedLastWeather");
    private static readonly FieldInfo CurWeatherDurationField = AccessTools.Field(typeof(WeatherDecider), "curWeatherDuration");
    private static readonly FieldInfo TicksWhenRainAllowedAgainField = AccessTools.Field(typeof(WeatherDecider), "ticksWhenRainAllowedAgain");

    public static bool ShouldMirrorGroundClimate(Map map)
    {
        return map != null && VerticalRenderContextService.GetLevel(map) > 0;
    }

    public static Map GroundMap(Map map)
    {
        return ShouldMirrorGroundClimate(map) ? VerticalRenderContextService.GroundMap(map) : null;
    }

    public static void SyncFromGround(Map map)
    {
        var groundMap = GroundMap(map);
        if (groundMap == null || groundMap == map)
        {
            return;
        }

        SyncWeatherManager(groundMap.weatherManager, map.weatherManager);
        SyncWeatherDecider(groundMap.weatherDecider, map.weatherDecider);
    }

    public static bool TryGetMirroredOutdoorTemp(Map map, out float outdoorTemp)
    {
        outdoorTemp = 0f;
        var groundMap = GroundMap(map);
        if (groundMap == null)
        {
            return false;
        }

        outdoorTemp = groundMap.mapTemperature.OutdoorTemp;
        return true;
    }

    public static bool TryGetMirroredSeasonalTemp(Map map, out float seasonalTemp)
    {
        seasonalTemp = 0f;
        var groundMap = GroundMap(map);
        if (groundMap == null)
        {
            return false;
        }

        seasonalTemp = groundMap.mapTemperature.SeasonalTemp;
        return true;
    }

    private static void SyncWeatherManager(WeatherManager source, WeatherManager target)
    {
        if (source == null || target == null)
        {
            return;
        }

        CurWeatherField?.SetValue(target, CurWeatherField.GetValue(source));
        LastWeatherField?.SetValue(target, LastWeatherField.GetValue(source));
        PrevSkyTargetLerpField?.SetValue(target, PrevSkyTargetLerpField.GetValue(source));
        CurrSkyTargetLerpField?.SetValue(target, CurrSkyTargetLerpField.GetValue(source));
        CurWeatherAgeField?.SetValue(target, CurWeatherAgeField.GetValue(source));
        TickedLastWeatherField?.SetValue(target, TickedLastWeatherField.GetValue(source));
    }

    private static void SyncWeatherDecider(WeatherDecider source, WeatherDecider target)
    {
        if (source == null || target == null)
        {
            return;
        }

        CurWeatherDurationField?.SetValue(target, CurWeatherDurationField.GetValue(source));
        TicksWhenRainAllowedAgainField?.SetValue(target, TicksWhenRainAllowedAgainField.GetValue(source));
    }
}
