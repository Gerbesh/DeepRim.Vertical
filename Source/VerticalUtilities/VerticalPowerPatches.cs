using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalUtilities;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class VerticalUtilityBeforeTickPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix() => GameComponent_VerticalUtilities.Get()?.FlushPlumbing();
}

// The original solver runs once on a temporary union of real components.
// Component ownership/transNet and actual battery storage stay on their maps.
[HarmonyPatch(typeof(PowerNet), nameof(PowerNet.PowerNetTick))]
public static class VerticalPowerTickPatch
{
    [ThreadStatic] internal static bool Solving;
    public static bool Prefix(PowerNet __instance)
    {
        if (Solving) return true;
        var service = GameComponent_VerticalUtilities.Get();
        if (service == null || !service.HasPowerRisers) return true;
        var group = service.GetPowerGroup(__instance);
        PowerNet view;
        if (group == null)
        {
            if (service.TrackLocalTick(__instance, out view)) return true;
        }
        else view = service.PrepareTick(group);
        if (view == null) return false;
        if (view.powerComps.Count == 0 && view.batteryComps.Count == 0) return false;
        Solving = true;
        try { view.PowerNetTick(); }
        finally { Solving = false; }
        return false;
    }
}

[HarmonyPatch(typeof(PowerNet), nameof(PowerNet.CanPowerNow))]
public static class VerticalPowerAvailabilityPatch
{
    public static bool Prefix(PowerNet __instance, CompPowerTrader powerTrader, ref bool __result)
    {
        if (VerticalPowerTickPatch.Solving) return true;
        var group = GameComponent_VerticalUtilities.Get()?.GetPowerGroup(__instance);
        if (group == null) return true;
        // View is not in the topology, so this recursive call takes the native path.
        __result = group.View().CanPowerNow(powerTrader);
        return false;
    }
}

[HarmonyPatch(typeof(PowerNet), "get_HasActivePowerSource")]
public static class VerticalPowerSourcePatch
{
    public static bool Prefix(PowerNet __instance, ref bool __result)
    {
        if (VerticalPowerTickPatch.Solving) return true;
        var group = GameComponent_VerticalUtilities.Get()?.GetPowerGroup(__instance);
        if (group == null) return true;
        __result = group.View().HasActivePowerSource;
        return false;
    }
}

[HarmonyPatch(typeof(PowerNetManager), nameof(PowerNetManager.UpdatePowerNetsAndConnections_First))]
public static class VerticalPowerTopologyPatch
{
    private static readonly FieldInfo Delayed = AccessTools.Field(typeof(PowerNetManager), "delayedActions");
    public static void Prefix(PowerNetManager __instance, out bool __state) =>
        __state = ((ICollection)Delayed.GetValue(__instance)).Count > 0;
    public static void Postfix(bool __state)
    {
        if (__state) GameComponent_VerticalUtilities.Get()?.InvalidatePower();
    }
}
