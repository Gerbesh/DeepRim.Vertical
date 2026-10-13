using System;
using System.Collections.Generic;
using System.Reflection;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalRouting;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalSupports;
using DeepRim.Vertical.VerticalTemperature;
using DeepRim.Vertical.VerticalColony;
using DeepRim.Vertical.VerticalConstruction;
using DeepRim.Vertical.VerticalUtilities;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalProfiling;

// No HarmonyPatch attributes: these costly hooks exist only during an explicit capture.
public static class VerticalProfilerPatches
{
    public const string HarmonyId = "gerbe.deeprim.vertical.profiler";
    private static readonly Harmony ProfilerHarmony = new(HarmonyId);
    private static readonly Dictionary<MethodBase, VerticalProfileMetric> Metrics = new();
    [ThreadStatic] private static int thingDepth;

    public static int InstalledCount => Metrics.Count;

    public static void Install()
    {
        Uninstall();
        try
        {
            foreach (var hook in GetHooks())
            {
                Metrics.Add(hook.Method, hook.Metric);
                ProfilerHarmony.Patch(hook.Method,
                    prefix: new HarmonyMethod(hook.Prefix) { priority = Priority.First + 1 },
                    finalizer: new HarmonyMethod(hook.Finalizer) { priority = Priority.Last });
            }
        }
        catch
        {
            Uninstall();
            throw;
        }
    }

    public static IReadOnlyList<VerticalProfileHook> GetHooks()
    {
        var hooks = new List<VerticalProfileHook>();
        void Add(Type type, string methodName, VerticalProfileMetric metric, string prefix, string finalizer = nameof(Finalizer))
        {
            var method = AccessTools.Method(type, methodName) ?? throw new MissingMethodException(type.FullName, methodName);
            // Harmony/Mono requires the actual declaration for inherited entry points.
            if (method.ReflectedType != method.DeclaringType)
                method = AccessTools.DeclaredMethod(method.DeclaringType, method.Name,
                    Array.ConvertAll(method.GetParameters(), parameter => parameter.ParameterType));
            hooks.Add(new VerticalProfileHook(method, metric,
                AccessTools.Method(typeof(VerticalProfilerPatches), prefix), AccessTools.Method(typeof(VerticalProfilerPatches), finalizer)));
        }
        Add(typeof(TickManager), nameof(TickManager.DoSingleTick), VerticalProfileMetric.GameTick, nameof(GlobalPrefix));
        Add(typeof(Root_Play), nameof(Root_Play.Update), VerticalProfileMetric.FrameCpu, nameof(FramePrefix), nameof(FrameFinalizer));
        Add(typeof(Map), nameof(Map.MapPreTick), VerticalProfileMetric.MapPreTick, nameof(MapPrefix));
        Add(typeof(Map), nameof(Map.MapPostTick), VerticalProfileMetric.MapPostTick, nameof(MapPrefix));
        Add(typeof(Map), nameof(Map.MapUpdate), VerticalProfileMetric.MapUpdate, nameof(MapPrefix));
        Add(typeof(Thing), nameof(Thing.DoTick), VerticalProfileMetric.ThingTick, nameof(ThingPrefix), nameof(ThingFinalizer));
        Add(typeof(MapDrawer), nameof(MapDrawer.MapMeshDrawerUpdate_First), VerticalProfileMetric.MeshUpdate, nameof(PrivateMapPrefix));
        Add(typeof(MapDrawer), nameof(MapDrawer.DrawMapMesh), VerticalProfileMetric.MeshDraw, nameof(PrivateMapPrefix));
        Add(typeof(PathFinder), nameof(PathFinder.PathFinderTick), VerticalProfileMetric.PathService, nameof(PrivateMapPrefix));
        Add(typeof(MapTemperature), nameof(MapTemperature.MapTemperatureTick), VerticalProfileMetric.Temperature, nameof(PrivateMapPrefix));
        Add(typeof(PowerNetManager), nameof(PowerNetManager.PowerNetsTick), VerticalProfileMetric.Power, nameof(PrivateMapPrefix));
        Add(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage), VerticalProfileMetric.JobSearch, nameof(WorkPrefix));
        Add(typeof(UpperFloorClimateService), nameof(UpperFloorClimateService.SyncFromGround), VerticalProfileMetric.ClimateSync, nameof(FirstMapPrefix));
        Add(typeof(GameComponent_VerticalTasks), "Advance", VerticalProfileMetric.Transfers, nameof(VerticalTaskPrefix));
        Add(typeof(VerticalMapInvalidationService), nameof(VerticalMapInvalidationService.MarkSiteDirty), VerticalProfileMetric.FullInvalidation, nameof(FirstMapPrefix));
        Add(typeof(VerticalMapInvalidationService), nameof(VerticalMapInvalidationService.MarkSiteCellsDirty), VerticalProfileMetric.CellInvalidation, nameof(FirstMapPrefix));
        Add(typeof(UpperFloorStateService), "Rebuild", VerticalProfileMetric.StateRebuild, nameof(FirstMapPrefix));
        Add(typeof(VerticalSupportService), "RebuildCache", VerticalProfileMetric.SupportRebuild, nameof(FirstMapPrefix));
        Add(typeof(UpperLevelTerrainGrid), nameof(UpperLevelTerrainGrid.SetupUpperLevelTerrainCache), VerticalProfileMetric.TerrainRebuild, nameof(TerrainPrefix));
        Add(typeof(UpperOverlayVisibilityMask), "RebuildIfDirty", VerticalProfileMetric.MaskRebuild, nameof(MaskPrefix));
        Add(typeof(SectionLayer_LowerLevel), nameof(SectionLayer_LowerLevel.Regenerate), VerticalProfileMetric.UnderlayRebuild, nameof(PrivateMapPrefix));
        Add(typeof(VerticalSectionRenderState), nameof(VerticalSectionRenderState.RebuildWorkers), VerticalProfileMetric.WorkerScan, nameof(FirstMapPrefix));
        Add(typeof(VerticalGhostRenderer), nameof(VerticalGhostRenderer.DrawFor), VerticalProfileMetric.GhostDraw, nameof(FirstMapPrefix));
        Add(typeof(Root), "OnGUI", VerticalProfileMetric.GuiCpu, nameof(GlobalPrefix));
        Add(typeof(VerticalProfilerWindow), nameof(VerticalProfilerWindow.DoWindowContents), VerticalProfileMetric.ProfilerUi, nameof(GlobalPrefix));
        Add(typeof(SectionLayer_UpperVoidBase), nameof(SectionLayer_UpperVoidBase.Regenerate), VerticalProfileMetric.NeutralBaseRebuild, nameof(PrivateMapPrefix));
        Add(typeof(VerticalWorkSearch), nameof(VerticalWorkSearch.Find), VerticalProfileMetric.WorkPlanning, nameof(WorkPrefix));
        Add(typeof(VerticalRouteUtility), nameof(VerticalRouteUtility.Find), VerticalProfileMetric.RoutePlanning, nameof(WorkPrefix));
        Add(typeof(GameComponent_VerticalTasks), nameof(GameComponent_VerticalTasks.GameComponentTick), VerticalProfileMetric.TaskMaintenance, nameof(GlobalPrefix));
        Add(typeof(GameComponent_VerticalUtilities), nameof(GameComponent_VerticalUtilities.GameComponentTick), VerticalProfileMetric.UtilityMaintenance, nameof(GlobalPrefix));
        Add(typeof(GameComponent_VerticalColony), nameof(GameComponent_VerticalColony.Wealth), VerticalProfileMetric.ColonyAccounting, nameof(GlobalPrefix));
        Add(typeof(VerticalCeilingComponent), nameof(VerticalCeilingComponent.MapComponentTick), VerticalProfileMetric.CeilingSync, nameof(PrivateMapPrefix));
        Add(typeof(DubsHygieneAdapter), nameof(DubsHygieneAdapter.RefreshSites), VerticalProfileMetric.PlumbingRefresh, nameof(GlobalPrefix));
        Add(typeof(GameComponent_VerticalUtilities), nameof(GameComponent_VerticalUtilities.GetPowerGroup), VerticalProfileMetric.PowerGraph, nameof(GlobalPrefix));
        return hooks;
    }

    public static void Uninstall()
    {
        // Remove only this owner; the mod's functional patches and other profilers remain intact.
        List<Exception> failures = null;
        foreach (var original in new List<MethodBase>(Metrics.Keys))
        {
            try { ProfilerHarmony.Unpatch(original, HarmonyPatchType.All, HarmonyId); Metrics.Remove(original); }
            catch (Exception error) { (failures ??= new()).Add(error); }
        }
        if (failures != null) throw new AggregateException("Profiler hooks could not all be removed; failed hooks remain tracked for retry", failures);
    }

    public static void VerticalTaskPrefix(VerticalTaskRecord __0, MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(__0.pawn?.Map?.uniqueID ?? -1, Metrics[__originalMethod]) : default;

    public static void GlobalPrefix(MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(-1, Metrics[__originalMethod]) : default;

    public static void FramePrefix(MethodBase __originalMethod, out VerticalProfileScope __state)
    {
        VerticalProfiler.FrameStarted();
        GlobalPrefix(__originalMethod, out __state);
    }

    public static void MapPrefix(Map __instance, MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(__instance, Metrics[__originalMethod]) : default;

    public static void PrivateMapPrefix(Map ___map, MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(___map, Metrics[__originalMethod]) : default;

    public static void FirstMapPrefix(Map __0, MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(__0, Metrics[__originalMethod]) : default;

    public static void WorkPrefix(Pawn pawn, MethodBase __originalMethod, out VerticalProfileScope __state) =>
        __state = VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(pawn?.MapHeld, Metrics[__originalMethod]) : default;

    public static void TerrainPrefix(UpperLevelTerrainGrid __instance, bool ___dirty, out VerticalProfileScope __state) =>
        __state = ___dirty && VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(__instance.Map, VerticalProfileMetric.TerrainRebuild) : default;

    public static void MaskPrefix(UpperOverlayVisibilityMask __instance, bool ___dirty, out VerticalProfileScope __state) =>
        __state = ___dirty && VerticalProfiler.CapturingOnCurrentThread ? VerticalProfiler.Begin(__instance.Map, VerticalProfileMetric.MaskRebuild) : default;

    public static void ThingPrefix(Thing __instance, out VerticalThingProfileScope __state)
    {
        __state = default;
        if (!VerticalProfiler.CapturingOnCurrentThread) return;
        __state.Entered = true;
        // DoTick recursively ticks held things. Count only the outer owner in floor totals.
        if (thingDepth++ == 0) __state.Scope = VerticalProfiler.Begin(__instance.MapHeld, VerticalProfileMetric.ThingTick);
    }

    public static void ThingFinalizer(Thing __instance, VerticalThingProfileScope __state, Exception __exception)
    {
        if (!__state.Entered) return;
        thingDepth = Math.Max(0, thingDepth - 1);
        VerticalProfiler.EndThing(__state.Scope, __instance, __exception != null);
    }

    public static void Finalizer(VerticalProfileScope __state, Exception __exception) => VerticalProfiler.End(__state, __exception != null);

    public static void FrameFinalizer(VerticalProfileScope __state, Exception __exception)
    {
        VerticalProfiler.End(__state, __exception != null);
        VerticalProfiler.Poll();
    }
}

public struct VerticalThingProfileScope
{
    public bool Entered;
    public VerticalProfileScope Scope;
}

public sealed class VerticalProfileHook
{
    public VerticalProfileHook(MethodInfo method, VerticalProfileMetric metric, MethodInfo prefix, MethodInfo finalizer)
    {
        Method = method; Metric = metric; Prefix = prefix; Finalizer = finalizer;
    }
    public MethodInfo Method { get; }
    public VerticalProfileMetric Metric { get; }
    public MethodInfo Prefix { get; }
    public MethodInfo Finalizer { get; }
}

[HarmonyPatch(typeof(Game), nameof(Game.ClearCaches))]
public static class Game_ClearCaches_VerticalProfilerPatch
{
    public static void Prefix() => VerticalProfiler.Stop("game-reset");
}
