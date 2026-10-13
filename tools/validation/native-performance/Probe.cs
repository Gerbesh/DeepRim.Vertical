// Test-only normal-window DEV benchmark. Never part of the production mod.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using DeepRim.Vertical;
using DeepRim.Vertical.VerticalConstruction;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalProfiling;
using DeepRim.Vertical.VerticalRendering;
using RimWorld;
using UnityEngine;
using Verse;

public sealed class VerticalPerformanceNativeProbe : GameComponent
{
    private sealed class Scenario
    {
        public string Label;
        public int Level;
        public TimeSpeed Speed;
        public bool Overlay;
        public Scenario(string label, int level, TimeSpeed speed, bool overlay = true)
        { Label = label; Level = level; Speed = speed; Overlay = overlay; }
    }
    private readonly Dictionary<int, Map> maps = new Dictionary<int, Map>();
    private readonly List<Scenario> scenarios = new List<Scenario>();
    private int index = -1, state, guiFrames, captureGuiStart, engineErrors;
    private double warmUntil;
    private bool failed;
    private string root;
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    public VerticalPerformanceNativeProbe(Game game) { }
    private bool Enabled => GenFilePaths.SaveDataFolderPath.EndsWith("native-performance", StringComparison.OrdinalIgnoreCase);
    private bool VisualOnly => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "visual-only.txt"));
    public override void GameComponentOnGUI() { if (Enabled && Event.current.type == EventType.Repaint) guiFrames++; }
    public override void GameComponentUpdate()
    {
        if (!Enabled || failed || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (state == 0)
            {
                root = GenFilePaths.SaveDataFolderPath;
                Directory.CreateDirectory(Path.Combine(root, "Measurements"));
                if (Application.isBatchMode || Screen.width < 800 || Screen.height < 600)
                    throw new Exception("Benchmark requires a rendered normal window, screen=" + Screen.width + "x" + Screen.height);
                Prefs.DevMode = true;
                Application.logMessageReceived += (message, stack, kind) =>
                {
                    if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) engineErrors++;
                };
                foreach (var window in Find.WindowStack.Windows.ToList())
                    if (window.GetType().Name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0) window.Close(false);
                maps[0] = Find.CurrentMap;
                foreach (var pawn in maps[0].mapPawns.FreeColonistsSpawned) pawn.drafter.Drafted = true;
                CameraJumper.TryJump(maps[0].Center, maps[0], CameraJumper.MovementMode.Cut);
                File.WriteAllText(Path.Combine(root, "summary.csv"), "scenario,seconds,frames,fps,tps,target_tps,frame_mean_ms,frame_p95_upper_ms,gui_mean_ms,tick_mean_ms,gui_repaints,exceptions,observed_target_tps\n");
                scenarios.Add(new Scenario("before-floors-pause", 0, TimeSpeed.Paused));
                scenarios.Add(new Scenario("before-floors-3x", 0, TimeSpeed.Superfast));
                Log.Message("[PERFORMANCE] Normal DEV window " + Screen.width + "x" + Screen.height + "; map " + maps[0].Size + "; natural TickManager only");
                state = 1;
                Next();
                return;
            }
            Find.TickManager.CurTimeSpeed = scenarios[index].Speed;
            if (state == 1)
            {
                if (Now < warmUntil || guiFrames < 30) return;
                if (!VerticalProfiler.Start(out var reason, VisualOnly ? 5 : 20)) throw new Exception(reason);
                if (VisualOnly) ScreenCapture.CaptureScreenshot(Path.Combine(root, "Measurements", scenarios[index].Label + "-warm.png"));
                captureGuiStart = guiFrames;
                Log.Message("[PERFORMANCE] CAPTURE " + scenarios[index].Label + " targetTPS=" + 60 * Find.TickManager.TickRateMultiplier);
                state = 2;
                return;
            }
            if (state != 2 || VerticalProfiler.Running) return;
            Record();
            if (index == 1)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                foreach (var level in new[] { -1, 1, 2, 3 })
                {
                    var source = level == -1 || level == 1 ? maps[0] : maps[level - 1];
                    if (!VerticalMapCreationService.TryCreateFloor(source, level < 0 ? -1 : 1, out var map, false))
                        throw new Exception("Floor creation failed: " + level);
                    maps[level] = map;
                }
                // Real paired wooden stairs leave most of the upper view open.
                foreach (var level in new[] { -1, 0, 1, 2 })
                {
                    var map = maps[level];
                    var cell = map.Center + new IntVec3(level * 3, 0, 0);
                    foreach (var thing in cell.GetThingList(map).ToList())
                        if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant) thing.Destroy();
                    var stair = (Building_VerticalStair)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("DeepRimVertical_StairsUp"), ThingDefOf.WoodLog);
                    stair.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(stair, cell, map, Rot4.North, WipeMode.Vanish, true);
                    if (!VerticalStairService.Connect(stair)) throw new Exception("Stair connection failed at " + level);
                }
                foreach (var map in maps.Values)
                {
                    foreach (var cell in CellRect.CenteredOn(map.Center, 15)) map.fogGrid.Unfog(cell);
                    map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                }
                if (VisualOnly)
                    maps[0].fogGrid.Refog(CellRect.CenteredOn(maps[0].Center + new IntVec3(10, 0, 10), 4));
                var cases = new[]
                {
                    new Scenario("surface-pause", 0, TimeSpeed.Paused),
                    new Scenario("surface-1x", 0, TimeSpeed.Normal),
                    new Scenario("surface-3x", 0, TimeSpeed.Superfast),
                    new Scenario("upper1-pause-on", 1, TimeSpeed.Paused),
                    new Scenario("upper1-1x-on", 1, TimeSpeed.Normal),
                    new Scenario("upper1-3x-on", 1, TimeSpeed.Superfast),
                    new Scenario("upper1-3x-off", 1, TimeSpeed.Superfast, false),
                    new Scenario("underground-3x", -1, TimeSpeed.Superfast),
                    new Scenario("upper3-3x-on", 3, TimeSpeed.Superfast),
                    new Scenario("upper3-3x-off", 3, TimeSpeed.Superfast, false)
                };
                if (VisualOnly) cases = new[] { cases[0], cases[3], new Scenario("upper3-pause-on", 3, TimeSpeed.Paused) };
                for (var repeat = 1; repeat <= (VisualOnly ? 1 : 2); repeat++)
                    foreach (var scenario in repeat == 1 ? cases : cases.Reverse())
                        scenarios.Add(new Scenario("r" + repeat + "-" + scenario.Label, scenario.Level, scenario.Speed, scenario.Overlay));
                GameDataSaveLoader.SaveGame("DEV-performance-five-floors");
                Log.Message("[PERFORMANCE] Five real maps, linked stair column, baseline saved");
            }
            Next();
        }
        catch (Exception error)
        {
            failed = true;
            VerticalProfiler.Stop("probe-failed");
            Log.Error("[PERFORMANCE] FAIL " + error);
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "FAIL " + error);
            Application.Quit();
        }
    }
    private void Next()
    {
        index++;
        if (index >= scenarios.Count)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            File.WriteAllText(Path.Combine(root, "result.txt"), "PASS rendered DEV " + (VisualOnly ? "visual smoke" : "benchmark") + "; " + index + " captures; natural ticks; no manual ticking");
            Log.Message("[PERFORMANCE] PASS " + index + " captures");
            Application.Quit();
            return;
        }
        var scenario = scenarios[index];
        VerticalRuntime.Settings.enableUpperFloorOverlay = scenario.Overlay;
        VerticalRuntime.Settings.enableUpperOverlayDebugVerbose = false;
        VerticalRuntime.Settings.enableUpperOverlayDebugLogging = false;
        VerticalCameraSyncService.JumpPreservingView(Find.CurrentMap, maps[scenario.Level], maps[0].Center);
        Find.TickManager.CurTimeSpeed = scenario.Speed;
        if (VisualOnly && scenario.Level == 3) VerticalProfilerWindow.Open();
        warmUntil = Now + (VisualOnly ? 3 : 6);
        state = 1;
        File.WriteAllText(Path.Combine(root, "status.txt"), scenario.Label + " warmup; " + Screen.width + "x" + Screen.height);
        ScreenCapture.CaptureScreenshot(Path.Combine(root, "Measurements", scenario.Label + ".png"));
    }
    private void Record()
    {
        var snapshot = VerticalProfiler.Snapshot();
        var frames = snapshot.Find(-1, VerticalProfileMetric.FrameInterval);
        var gui = snapshot.Find(-1, VerticalProfileMetric.GuiCpu);
        var ticks = snapshot.Find(-1, VerticalProfileMetric.GameTick);
        var repaints = guiFrames - captureGuiStart;
        if (engineErrors != 0 || snapshot.Rows.Any(row => row.Stats.Errors != 0))
            throw new Exception("Engine or timed-scope errors during benchmark: " + engineErrors);
        if (snapshot.Frames < 100 || repaints < 100 || gui == null) throw new Exception("No rendered frames in capture");
        if (scenarios[index].Speed != TimeSpeed.Paused && snapshot.Ticks == 0) throw new Exception("Simulation unexpectedly paused");
        snapshot.Metadata.TryGetValue("observed_target_tps", out var targetRates);
        var values = new object[] { scenarios[index].Label, snapshot.Seconds, snapshot.Frames,
            frames?.MeanMs > 0 ? 1000 / frames.MeanMs : 0, snapshot.Tps, 60 * Find.TickManager.TickRateMultiplier,
            frames?.MeanMs ?? 0, frames?.PercentileUpperBound(.95) ?? 0, gui.MeanMs, ticks?.MeanMs ?? 0, repaints, snapshot.Rows.Sum(r => r.Stats.Errors), targetRates };
        File.AppendAllText(Path.Combine(root, "summary.csv"), string.Join(",", values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))) + "\n");
        File.Copy(VerticalProfiler.LastCsvPath, Path.Combine(root, "Measurements", scenarios[index].Label + ".csv"), true);
        File.Copy(VerticalProfiler.LastTextPath, Path.Combine(root, "Measurements", scenarios[index].Label + ".txt"), true);
        Log.Message("[PERFORMANCE] RESULT " + string.Join(" ", values));
    }
}
