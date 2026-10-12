using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalProfiling;

public readonly struct VerticalProfileScope
{
    internal VerticalProfileScope(int session, VerticalProfileKey key, long start)
    {
        Session = session;
        Key = key;
        Start = start;
    }
    internal int Session { get; }
    internal VerticalProfileKey Key { get; }
    internal long Start { get; }
}

public static class VerticalProfiler
{
    private const int MaxRows = 4096;
    private const int MaxObjectRows = 1024;
    private static readonly Dictionary<VerticalProfileKey, VerticalProfileRow> Rows = new();
    private static readonly Dictionary<int, VerticalProfileMapInfo> Maps = new();
    private static readonly Dictionary<string, string> Metadata = new();
    private static readonly HashSet<TimeSpeed> Speeds = new();
    private static readonly int[] InitialCollections = new int[3];
    private static int sessionId;
    private static int mainThreadId;
    private static int objectRows;
    private static long startedAt;
    private static long lastFrame;
    private static long initialHeap;
    private static long dropped;
    private static double durationSeconds;
    private static DateTime startedUtc;
    private static Game game;
    private static VerticalProfileSnapshot completed;
    public static bool Running { get; private set; }
    public static string LastCsvPath { get; private set; }
    public static string LastTextPath { get; private set; }
    public static bool CapturingOnCurrentThread => Running && Thread.CurrentThread.ManagedThreadId == mainThreadId;

    public static bool Start(out string reason, double seconds = 30)
    {
        reason = null;
        if (Running) return true;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            reason = "DeepRimVertical.Profiler.NeedGame".Translate();
            return false;
        }
        try
        {
            VerticalProfilerPatches.Install();
            Rows.Clear(); Maps.Clear(); Metadata.Clear(); Speeds.Clear();
            objectRows = 0; dropped = 0; lastFrame = 0;
            completed = null; LastCsvPath = null; LastTextPath = null;
            sessionId++;
            if (sessionId == 0) sessionId++;
            game = Current.Game;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            durationSeconds = Math.Max(1, Math.Min(300, seconds));
            startedUtc = DateTime.UtcNow;
            foreach (var map in Find.Maps) ObserveMap(map);
            Metadata["game_version"] = VersionControl.CurrentVersionStringWithRev;
            Metadata["mods"] = string.Join(";", LoadedModManager.RunningMods.Select(mod => mod.PackageIdPlayerFacing));
            Metadata["active_map_at_start"] = (Find.CurrentMap?.uniqueID ?? -2).ToString(CultureInfo.InvariantCulture);
            Metadata["target_tps_at_start"] = (60 * Find.TickManager.TickRateMultiplier).ToString(CultureInfo.InvariantCulture);
            Metadata["speed_at_start"] = Find.TickManager.CurTimeSpeed.ToString();
            Metadata["overlay_enabled"] = VerticalRuntime.Settings.enableUpperFloorOverlay.ToString();
            Metadata["overlay_mode"] = VerticalRuntime.Settings.upperFloorOverlayMode.ToString(CultureInfo.InvariantCulture);
            Metadata["overlay_verbose"] = VerticalRuntime.Settings.enableUpperOverlayDebugVerbose.ToString();
            Metadata["fast_ecology"] = DebugSettings.fastEcology.ToString();
            Metadata["capture_seconds_requested"] = durationSeconds.ToString(CultureInfo.InvariantCulture);
            Metadata["instrumentation"] = "On-demand full root Thing.DoTick timing, per-map scopes and overlapping detail. Compare like-for-like captures; capture itself adds overhead. Main thread only.";
            initialHeap = GC.GetTotalMemory(false);
            for (var i = 0; i < 3; i++) InitialCollections[i] = GC.CollectionCount(i);
            startedAt = Stopwatch.GetTimestamp();
            Running = true;
            return true;
        }
        catch (Exception error)
        {
            Running = false; game = null;
            VerticalProfilerPatches.Uninstall();
            reason = "DeepRimVertical.Profiler.StartFailed".Translate(error.Message);
            Log.Error($"[DeepRim Vertical][Profiler] Capture could not start: {error}");
            return false;
        }
    }

    public static void Stop(string reason = "manual")
    {
        if (!Running) return;
        // Freeze before unpatching/exporting: their cost is not part of the captured interval.
        var endedAt = Stopwatch.GetTimestamp();
        Running = false;
        try { completed = SnapshotAt(endedAt, false, reason); }
        catch (Exception error)
        {
            completed = null;
            Log.Error($"[DeepRim Vertical][Profiler] Could not freeze report: {error}");
        }
        finally
        {
            game = null;
            try { VerticalProfilerPatches.Uninstall(); }
            catch (Exception error) { Log.Error($"[DeepRim Vertical][Profiler] Hook cleanup failed; recording is disabled: {error}"); }
        }
        ExportLast();
    }

    public static void Poll()
    {
        if (!Running) return;
        if (Current.Game != game || Current.ProgramState != ProgramState.Playing)
        {
            Stop("game-changed");
            return;
        }
        if (SecondsSince(startedAt, Stopwatch.GetTimestamp()) >= durationSeconds) Stop("duration-complete");
    }

    public static VerticalProfileSnapshot Snapshot() => Running ? SnapshotAt(Stopwatch.GetTimestamp(), true, null) : completed;

    private static VerticalProfileSnapshot SnapshotAt(long end, bool running, string reason)
    {
        var snapshot = new VerticalProfileSnapshot
        {
            StartedUtc = startedUtc, Seconds = SecondsSince(startedAt, end), Running = running,
            StopReason = reason, HeapDelta = GC.GetTotalMemory(false) - initialHeap, DroppedSamples = dropped
        };
        for (var i = 0; i < 3; i++) snapshot.Collections[i] = GC.CollectionCount(i) - InitialCollections[i];
        foreach (var entry in Metadata) snapshot.Metadata.Add(entry.Key, entry.Value);
        snapshot.Metadata["observed_speeds"] = string.Join(";", Speeds.OrderBy(speed => speed));
        foreach (var map in Maps) snapshot.Maps.Add(map.Key, map.Value);
        foreach (var row in Rows.Values)
            snapshot.Rows.Add(new VerticalProfileRow { Key = row.Key, Label = row.Label, Stats = row.Stats.Copy() });
        return snapshot;
    }

    private static void ObserveMap(Map map)
    {
        if (map == null || Maps.ContainsKey(map.uniqueID)) return;
        var info = new VerticalProfileMapInfo
        {
            MapId = map.uniqueID, Label = map.Parent?.LabelCap.ToString() ?? $"Map {map.uniqueID}",
            PawnCount = map.mapPawns?.AllPawnsSpawned.Count ?? 0,
            SizeX = map.Size.x, SizeZ = map.Size.z, ThingCount = map.listerThings?.AllThings.Count ?? 0,
            Level = map.Parent is VerticalSiteMapParent parent ? parent.LevelIndex : map.IsPlayerHome ? 0 : (int?)null
        };
        if (VerticalSiteWorldComponent.TryGet(out var component) && component.TryGetFloor(map, out var site, out var floor))
        {
            info.SiteId = site.siteId;
            info.Level = floor.levelIndex;
        }
        Maps.Add(map.uniqueID, info);
    }

    public static VerticalProfileScope Begin(Map map, VerticalProfileMetric metric)
    {
        if (!CapturingOnCurrentThread) return default;
        ObserveMap(map);
        return Begin(map?.uniqueID ?? -2, metric);
    }

    public static VerticalProfileScope Begin(int mapId, VerticalProfileMetric metric) => CapturingOnCurrentThread
        ? new VerticalProfileScope(sessionId, new VerticalProfileKey(mapId, metric), Stopwatch.GetTimestamp()) : default;

    public static void End(VerticalProfileScope scope, bool failed = false)
    {
        if (!Valid(scope)) return;
        Add(scope.Key, SecondsSince(scope.Start, Stopwatch.GetTimestamp()) * 1000, failed);
    }

    public static void EndThing(VerticalProfileScope scope, Thing thing, bool failed)
    {
        if (!Valid(scope)) return;
        var milliseconds = SecondsSince(scope.Start, Stopwatch.GetTimestamp()) * 1000;
        Add(scope.Key, milliseconds, failed);
        // Pawn instances identify a repeatedly stalled worker; other objects aggregate by def.
        var objectKey = thing is Pawn ? -thing.thingIDNumber : (thing?.def?.shortHash ?? 0) + 1;
        var key = new VerticalProfileKey(scope.Key.MapId, VerticalProfileMetric.ObjectTick, objectKey);
        if (!Rows.ContainsKey(key))
        {
            if (objectRows >= MaxObjectRows || Rows.Count >= MaxRows) { dropped++; return; }
            objectRows++;
            var label = thing is Pawn pawn
                ? $"{pawn.LabelShortCap} [{thing.ThingID}]"
                : thing?.def?.defName ?? "Unknown thing";
            Rows.Add(key, new VerticalProfileRow { Key = key, Label = label, Stats = new VerticalProfileStats() });
        }
        Add(key, milliseconds, failed);
    }

    public static void FrameStarted()
    {
        if (!CapturingOnCurrentThread) return;
        var now = Stopwatch.GetTimestamp();
        if (lastFrame != 0) Add(new VerticalProfileKey(-1, VerticalProfileMetric.FrameInterval), SecondsSince(lastFrame, now) * 1000, false);
        lastFrame = now;
        Speeds.Add(Find.TickManager.CurTimeSpeed);
    }

    private static bool Valid(VerticalProfileScope scope) => CapturingOnCurrentThread && scope.Session != 0 && scope.Session == sessionId;
    private static double SecondsSince(long start, long end) => (double)(end - start) / Stopwatch.Frequency;

    private static void Add(VerticalProfileKey key, double ms, bool failed)
    {
        if (!Rows.TryGetValue(key, out var row))
        {
            if (Rows.Count >= MaxRows) { dropped++; return; }
            row = new VerticalProfileRow { Key = key, Label = key.Metric.ToString(), Stats = new VerticalProfileStats() };
            Rows.Add(key, row);
        }
        row.Stats.Add(ms, failed);
    }

    public static string ExportLast()
    {
        if (Running || completed == null) return null;
        try
        {
            var directory = Path.Combine(GenFilePaths.SaveDataFolderPath, "DeepRimVertical", "Profiles");
            Directory.CreateDirectory(directory);
            var stem = "floors-" + completed.StartedUtc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-UTC";
            LastCsvPath = Path.Combine(directory, stem + ".csv");
            LastTextPath = Path.Combine(directory, stem + ".txt");
            File.WriteAllText(LastCsvPath, completed.ToCsv(), new UTF8Encoding(true));
            File.WriteAllText(LastTextPath, TextReport(completed), new UTF8Encoding(true));
            Log.Message($"[DeepRim Vertical][Profiler] Report: {LastCsvPath}");
            return LastCsvPath;
        }
        catch (Exception error)
        {
            LastCsvPath = null; LastTextPath = null;
            Log.Error($"[DeepRim Vertical][Profiler] Export failed; captured data remains in the profiler window: {error}");
            return null;
        }
    }

    public static string TextReport(VerticalProfileSnapshot snapshot)
    {
        var result = new StringBuilder();
        result.AppendLine("DeepRim Vertical: per-floor CPU capture");
        result.AppendLine($"UTC: {snapshot.StartedUtc:O}; elapsed: {snapshot.Seconds:F2}s; ticks: {snapshot.Ticks}; actual TPS: {snapshot.Tps:F1}; stop: {snapshot.StopReason}");
        foreach (var pair in snapshot.Metadata.OrderBy(pair => pair.Key)) result.AppendLine($"{pair.Key}: {pair.Value}");
        result.AppendLine($"Managed heap delta: {snapshot.HeapDelta} bytes (not allocation count); GC collections 0/1/2: {string.Join("/", snapshot.Collections)}; dropped detail samples: {snapshot.DroppedSamples}");
        result.AppendLine("Inclusive CPU scopes. Detail scopes overlap parents: do not sum all rows. GPU/native memory and background path execution are not measured.");
        result.AppendLine("Floor simulation = MapPreTick + outer Thing.DoTick + MapPostTick. Nested held things are included once with their owner. MapUpdate/frame costs are separate.");
        foreach (var map in snapshot.Maps.Values.OrderBy(map => map.MapId))
            result.AppendLine($"Map #{map.MapId}; floor {map.Level?.ToString() ?? "n/a"}; {map.Label}; size: {map.SizeX}x{map.SizeZ}; things: {map.ThingCount}; pawns at first observation: {map.PawnCount}; simulation ms/game tick: {(snapshot.Ticks > 0 ? snapshot.SimulationMs(map.MapId) / snapshot.Ticks : 0):F4}");
        result.AppendLine("map | metric/object | calls | total ms | mean ms | p95 upper ms | max ms | escaping exceptions");
        foreach (var row in snapshot.Rows.OrderBy(row => row.Key.MapId).ThenByDescending(row => row.Stats.TotalMs))
            result.AppendLine($"{row.Key.MapId} | {row.Label} | {row.Stats.Calls} | {row.Stats.TotalMs:F4} | {row.Stats.MeanMs:F4} | {row.Stats.PercentileUpperBound(.95):F4} | {row.Stats.MaxMs:F4} | {row.Stats.Errors}");
        return result.ToString();
    }
}
