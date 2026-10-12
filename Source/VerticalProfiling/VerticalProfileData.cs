using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DeepRim.Vertical.VerticalProfiling;

public enum VerticalProfileMetric
{
    GameTick, FrameCpu, FrameInterval,
    MapPreTick, MapPostTick, ThingTick, MapUpdate,
    MeshUpdate, MeshDraw, PathService, JobSearch, Temperature, Power,
    ClimateSync, Transfers, FullInvalidation, CellInvalidation,
    StateRebuild, SupportRebuild, TerrainRebuild, MaskRebuild,
    UnderlayRebuild, WorkerScan, GhostDraw, ObjectTick
}

// Fixed-size histograms keep captures bounded. Percentiles are upper bounds, not exact samples.
public sealed class VerticalProfileStats
{
    private static readonly double[] Bounds = { .01, .025, .05, .1, .25, .5, 1, 2, 4, 8, 16, 33, 66, 125, 250, 500, 1000, 2000, 5000 };
    private readonly long[] buckets = new long[Bounds.Length + 1];
    public long Calls { get; private set; }
    public double TotalMs { get; private set; }
    public double MaxMs { get; private set; }
    public long Errors { get; private set; }
    public double MeanMs => Calls == 0 ? 0 : TotalMs / Calls;

    public void Add(double milliseconds, bool failed = false)
    {
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0)
            return;
        Calls++;
        TotalMs += milliseconds;
        MaxMs = Math.Max(MaxMs, milliseconds);
        if (failed) Errors++;
        var index = 0;
        while (index < Bounds.Length && milliseconds > Bounds[index]) index++;
        buckets[index]++;
    }

    public double PercentileUpperBound(double fraction)
    {
        if (Calls == 0) return 0;
        fraction = Math.Max(0, Math.Min(1, fraction));
        var rank = Math.Max(1, (long)Math.Ceiling(Calls * fraction));
        long cumulative = 0;
        for (var i = 0; i < buckets.Length; i++)
        {
            cumulative += buckets[i];
            if (cumulative >= rank) return i < Bounds.Length ? Math.Min(Bounds[i], MaxMs) : MaxMs;
        }
        return MaxMs;
    }

    public VerticalProfileStats Copy()
    {
        var copy = new VerticalProfileStats { Calls = Calls, TotalMs = TotalMs, MaxMs = MaxMs, Errors = Errors };
        Array.Copy(buckets, copy.buckets, buckets.Length);
        return copy;
    }
}

public readonly struct VerticalProfileKey : IEquatable<VerticalProfileKey>
{
    public VerticalProfileKey(int mapId, VerticalProfileMetric metric, int objectKey = 0)
    {
        MapId = mapId;
        Metric = metric;
        ObjectKey = objectKey;
    }
    public int MapId { get; }
    public VerticalProfileMetric Metric { get; }
    public int ObjectKey { get; }
    public bool Equals(VerticalProfileKey other) => MapId == other.MapId && Metric == other.Metric && ObjectKey == other.ObjectKey;
    public override bool Equals(object obj) => obj is VerticalProfileKey other && Equals(other);
    public override int GetHashCode() => unchecked((MapId * 397 ^ (int)Metric) * 397 ^ ObjectKey);
}

public sealed class VerticalProfileMapInfo
{
    public int MapId;
    public int? Level;
    public string SiteId;
    public string Label;
    public int PawnCount;
    public int SizeX;
    public int SizeZ;
    public int ThingCount;
}

public sealed class VerticalProfileRow
{
    public VerticalProfileKey Key;
    public string Label;
    public VerticalProfileStats Stats;
}

public sealed class VerticalProfileSnapshot
{
    public DateTime StartedUtc;
    public double Seconds;
    public bool Running;
    public string StopReason;
    public long HeapDelta;
    public int[] Collections = new int[3];
    public long DroppedSamples;
    public readonly Dictionary<string, string> Metadata = new();
    public readonly Dictionary<int, VerticalProfileMapInfo> Maps = new();
    public readonly List<VerticalProfileRow> Rows = new();
    public long Ticks => Find(-1, VerticalProfileMetric.GameTick)?.Calls ?? 0;
    public long Frames => Find(-1, VerticalProfileMetric.FrameCpu)?.Calls ?? 0;
    public double Tps => Seconds > 0 ? Ticks / Seconds : 0;
    public VerticalProfileStats Find(int mapId, VerticalProfileMetric metric) =>
        Rows.FirstOrDefault(row => row.Key.MapId == mapId && row.Key.Metric == metric && row.Key.ObjectKey == 0)?.Stats;

    // These three buckets do not overlap. Detail timers and frame timers must not be added here.
    public double SimulationMs(int mapId) =>
        (Find(mapId, VerticalProfileMetric.MapPreTick)?.TotalMs ?? 0) +
        (Find(mapId, VerticalProfileMetric.MapPostTick)?.TotalMs ?? 0) +
        (Find(mapId, VerticalProfileMetric.ThingTick)?.TotalMs ?? 0);

    public static string CsvCell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static void Line(StringBuilder builder, params string[] values) => builder.AppendLine(string.Join(",", values.Select(CsvCell)));

    public string ToCsv()
    {
        var result = new StringBuilder();
        Line(result, "kind", "map_id", "level", "site_id", "label", "metric", "calls", "total_ms", "mean_ms", "p95_upper_ms", "max_ms", "exceptions", "ms_per_game_tick");
        var meta = new Dictionary<string, string>(Metadata)
        {
            ["started_utc"] = StartedUtc.ToString("O", CultureInfo.InvariantCulture),
            ["elapsed_seconds"] = Number(Seconds), ["actual_tps"] = Number(Tps),
            ["ticks"] = Ticks.ToString(CultureInfo.InvariantCulture),
            ["frames"] = Frames.ToString(CultureInfo.InvariantCulture),
            ["stop_reason"] = StopReason, ["running"] = Running.ToString(),
            ["managed_heap_delta_bytes"] = HeapDelta.ToString(CultureInfo.InvariantCulture),
            ["gc_collections_0_1_2"] = string.Join("/", Collections),
            ["dropped_samples"] = DroppedSamples.ToString(CultureInfo.InvariantCulture),
            ["timing_semantics"] = "Inclusive CPU scopes; detail rows overlap parents; ThingTick excludes nested holders from floor totals; p95 is a histogram upper bound; heap delta is NOT allocated bytes; native/GPU memory and background path jobs are not measured."
        };
        foreach (var pair in meta.OrderBy(pair => pair.Key))
            Line(result, "metadata", "", "", "", pair.Key, pair.Value, "", "", "", "", "", "", "");
        foreach (var map in Maps.Values.OrderBy(map => map.MapId))
        {
            Line(result, "map", map.MapId.ToString(CultureInfo.InvariantCulture), map.Level?.ToString(CultureInfo.InvariantCulture), map.SiteId, map.Label,
                "pawn_count_at_first_observation", map.PawnCount.ToString(CultureInfo.InvariantCulture), "", "", "", "", "", "");
            Line(result, "map", map.MapId.ToString(CultureInfo.InvariantCulture), map.Level?.ToString(CultureInfo.InvariantCulture), map.SiteId, map.Label,
                "size_and_thing_count_at_first_observation", $"{map.SizeX}x{map.SizeZ}; things={map.ThingCount}", "", "", "", "", "", "");
        }
        foreach (var row in Rows.OrderBy(row => row.Key.MapId).ThenByDescending(row => row.Stats.TotalMs))
        {
            Maps.TryGetValue(row.Key.MapId, out var map);
            Line(result, "timing", row.Key.MapId.ToString(CultureInfo.InvariantCulture), map?.Level?.ToString(CultureInfo.InvariantCulture), map?.SiteId,
                row.Label, row.Key.Metric.ToString(), row.Stats.Calls.ToString(CultureInfo.InvariantCulture), Number(row.Stats.TotalMs),
                Number(row.Stats.MeanMs), Number(row.Stats.PercentileUpperBound(.95)), Number(row.Stats.MaxMs),
                row.Stats.Errors.ToString(CultureInfo.InvariantCulture), Ticks > 0 ? Number(row.Stats.TotalMs / Ticks) : "");
        }
        return result.ToString();
    }
}
