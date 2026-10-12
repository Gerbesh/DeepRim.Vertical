using System;
using System.Collections.Generic;
using System.Reflection;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalCombat;

public sealed class CompProperties_VerticalFlight : CompProperties
{ public CompProperties_VerticalFlight() { compClass = typeof(CompVerticalFlight); } }
public sealed class CompVerticalFlight : ThingComp
{
    public bool Active;
    public MapParent Source, Destination;
    public int SourceLevel, DestinationLevel;
    public float Duration, PreviousFraction;
    public int LastTick = -1;
    public override void PostExposeData()
    {
        Scribe_Values.Look(ref Active, "verticalFlight"); Scribe_References.Look(ref Source, "verticalSource"); Scribe_References.Look(ref Destination, "verticalDestination");
        Scribe_Values.Look(ref SourceLevel, "verticalSourceLevel"); Scribe_Values.Look(ref DestinationLevel, "verticalDestinationLevel");
        Scribe_Values.Look(ref Duration, "verticalFlightDuration"); Scribe_Values.Look(ref LastTick, "verticalLastTick", -1);
    }
    public void Begin(Map source, Map destination)
    {
        if (!VerticalSight.SameSite(source, destination, out var site)) return;
        Active = true; Source = source.Parent; Destination = destination.Parent;
        SourceLevel = site.floors.Find(f => f.Map == source).levelIndex;
        DestinationLevel = site.floors.Find(f => f.Map == destination).levelIndex;
    }
}
public static class VerticalProjectileUtility
{
    [ThreadStatic] private static bool checking;
    public static readonly AccessTools.FieldRef<Projectile, int> Remaining = AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
    public static readonly AccessTools.FieldRef<Projectile, Vector3> Origin = AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
    public static readonly AccessTools.FieldRef<Projectile, Vector3> Destination = AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
    private static readonly MethodInfo Intercept = AccessTools.Method(typeof(Projectile), "CheckForFreeInterceptBetween");
    public static void InstallDefs()
    {
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.projectile == null || !typeof(Projectile).IsAssignableFrom(def.thingClass)) continue;
            def.comps ??= new List<CompProperties>();
            if (!def.comps.Exists(c => c is CompProperties_VerticalFlight)) def.comps.Add(new CompProperties_VerticalFlight());
        }
    }
    public static bool Checking => checking;
    private static bool NativeIntercept(Projectile projectile, Vector3 start, Vector3 end)
    {
        checking = true;
        try { return (bool)Intercept.Invoke(projectile, new object[] { start, end }); }
        finally { checking = false; }
    }
    private static bool Stop(Projectile projectile, Vector3 point)
    {
        if (point.ToIntVec3().InBounds(projectile.Map)) projectile.Position = point.ToIntVec3();
        AccessTools.Method(projectile.GetType(), "Impact", new[] { typeof(Thing), typeof(bool) }).Invoke(projectile, new object[] { null, false });
        return true;
    }
    public static bool Traverse(Projectile projectile, CompVerticalFlight flight)
    {
        if (flight.Source?.Map == null || flight.Destination?.Map == null || !VerticalSight.SameSite(flight.Source.Map, flight.Destination.Map, out var site))
        { projectile.Destroy(); return true; }
        var fraction = Mathf.Clamp01(1f - Remaining(projectile) / flight.Duration);
        var segments = VerticalSight.Segments(site, flight.SourceLevel, flight.DestinationLevel, Origin(projectile), Destination(projectile), flight.PreviousFraction, fraction);
        if (segments == null) { projectile.Destroy(); return true; }
        foreach (var segment in segments)
        {
            if (projectile.Map != segment.Map) { projectile.Destroy(); return true; }
            if (NativeIntercept(projectile, segment.Start, segment.End)) return true;
            if (segment.NextMap == null) continue;
            if (!VerticalSight.Open(site, segment.LowerBoundary, segment.End.ToIntVec3())) return Stop(projectile, segment.End);
            var source = projectile.Map; var oldPosition = projectile.Position;
            try { projectile.DeSpawn(); GenSpawn.Spawn(projectile, segment.End.ToIntVec3(), segment.NextMap); }
            catch
            {
                if (!projectile.Spawned) GenSpawn.Spawn(projectile, oldPosition, source);
                projectile.Destroy(); throw;
            }
        }
        return false;
    }
}
[HarmonyPatch(typeof(Projectile), "get_StartingTicksToImpact")]
public static class VerticalProjectileDurationPatch
{
    public static void Postfix(Projectile __instance, ref float __result)
    {
        var flight = __instance.GetComp<CompVerticalFlight>();
        if (flight?.Active != true) return;
        if (flight.Duration <= 0)
        {
            var delta = VerticalProjectileUtility.Destination(__instance) - VerticalProjectileUtility.Origin(__instance);
            flight.Duration = Mathf.Max(.001f, Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z
                + Mathf.Pow((flight.DestinationLevel - flight.SourceLevel) * VerticalSight.FloorHeight, 2)) / __instance.def.projectile.SpeedTilesPerTick);
        }
        __result = flight.Duration;
    }
}
[HarmonyPatch(typeof(Projectile), "TickInterval")]
public static class VerticalProjectileTickPatch
{
    public static bool Prefix(Projectile __instance)
    {
        var flight = __instance.GetComp<CompVerticalFlight>();
        if (flight?.Active != true) return true;
        var tick = Find.TickManager.TicksGame;
        if (flight.LastTick == tick) return false; // The receiving map may tick later in the same game tick.
        flight.LastTick = tick;
        flight.PreviousFraction = Mathf.Clamp01(1f - VerticalProjectileUtility.Remaining(__instance) / flight.Duration);
        return true;
    }
}
[HarmonyPatch(typeof(Projectile), "get_UpdateRateTicks")]
public static class VerticalProjectileUpdateRatePatch
{
    public static void Postfix(Projectile __instance, ref int __result)
    { if (__instance.GetComp<CompVerticalFlight>()?.Active == true) __result = 1; }
}
[HarmonyPatch(typeof(Projectile), "CheckForFreeInterceptBetween")]
public static class VerticalProjectileInterceptPatch
{
    public static bool Prefix(Projectile __instance, ref bool __result)
    {
        var flight = __instance.GetComp<CompVerticalFlight>();
        if (flight?.Active != true || VerticalProjectileUtility.Checking) return true;
        __result = VerticalProjectileUtility.Traverse(__instance, flight); return false;
    }
}
[HarmonyPatch(typeof(Projectile), "get_ExactRotation")]
public static class VerticalProjectileRotationPatch
{
    public static bool Prefix(Projectile __instance, ref Quaternion __result)
    {
        if (__instance.GetComp<CompVerticalFlight>()?.Active != true
            || (VerticalProjectileUtility.Destination(__instance) - VerticalProjectileUtility.Origin(__instance)).MagnitudeHorizontalSquared() > .0001f) return true;
        __result = Quaternion.identity; return false;
    }
}
