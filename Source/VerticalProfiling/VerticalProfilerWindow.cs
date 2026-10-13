using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalProfiling;

public sealed class VerticalProfilerWindow : Window
{
    private Vector2 scroll;
    private VerticalProfileSnapshot snapshot;
    private long refreshedAt;
    public override Vector2 InitialSize => new(Mathf.Min(1080, UI.screenWidth - 40), Mathf.Min(760, UI.screenHeight - 60));

    public VerticalProfilerWindow()
    {
        optionalTitle = "DeepRimVertical.Profiler.Title".Translate();
        doCloseX = true;
        draggable = true;
        forcePause = false;
        preventCameraMotion = false;
        closeOnAccept = false;
        absorbInputAroundWindow = false;
    }

    public static void Open()
    {
        if (!Find.WindowStack.IsOpen<VerticalProfilerWindow>()) Find.WindowStack.Add(new VerticalProfilerWindow());
    }

    public override void DoWindowContents(Rect inRect)
    {
        var oldFont = Text.Font;
        try
        {
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(0, 0, 205, 30), "DeepRimVertical.Profiler.Start".Translate(), active: !VerticalProfiler.Running))
            {
                if (!VerticalProfiler.Start(out var reason)) Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                refreshedAt = 0;
            }
            if (Widgets.ButtonText(new Rect(215, 0, 130, 30), "DeepRimVertical.Profiler.Stop".Translate(), active: VerticalProfiler.Running))
            {
                VerticalProfiler.Stop();
                refreshedAt = 0;
            }
            if (Widgets.ButtonText(new Rect(355, 0, 175, 30), "DeepRimVertical.Profiler.Export".Translate(), active: !VerticalProfiler.Running && VerticalProfiler.Snapshot() != null))
            {
                var path = VerticalProfiler.ExportLast();
                if (path != null) Messages.Message("DeepRimVertical.Profiler.Saved".Translate(path), MessageTypeDefOf.TaskCompletion, false);
            }
            var now = Stopwatch.GetTimestamp();
            if (refreshedAt == 0 || (double)(now - refreshedAt) / Stopwatch.Frequency >= 1 || snapshot?.Running != VerticalProfiler.Running)
            {
                snapshot = VerticalProfiler.Snapshot();
                refreshedAt = now;
            }
            if (snapshot == null)
            {
                Widgets.Label(new Rect(0, 45, inRect.width, 150), "DeepRimVertical.Profiler.Intro".Translate());
                return;
            }

            var tick = snapshot.Find(-1, VerticalProfileMetric.GameTick);
            var interval = snapshot.Find(-1, VerticalProfileMetric.FrameInterval);
            Widgets.Label(new Rect(0, 40, inRect.width, 25), "DeepRimVertical.Profiler.Summary".Translate(
                snapshot.Running ? "DeepRimVertical.Profiler.Recording".Translate().ToString() : "DeepRimVertical.Profiler.Finished".Translate().ToString(),
                snapshot.Seconds.ToString("F1"), snapshot.Tps.ToString("F1"), interval?.MeanMs > 0 ? (1000 / interval.MeanMs).ToString("F1") : "—"));
            if (snapshot.Metadata.TryGetValue("observed_target_tps", out var targetRates) && !string.IsNullOrEmpty(targetRates))
                TooltipHandler.TipRegion(new Rect(0, 40, inRect.width, 25), "DeepRimVertical.Profiler.TargetRates".Translate(targetRates));
            Widgets.Label(new Rect(0, 65, inRect.width, 25), "DeepRimVertical.Profiler.TickSummary".Translate(
                (tick?.MeanMs ?? 0).ToString("F3"), (tick?.PercentileUpperBound(.95) ?? 0).ToString("F3"), (tick?.MaxMs ?? 0).ToString("F3"),
                string.Join("/", snapshot.Collections), (snapshot.HeapDelta / 1048576d).ToString("F2")));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0, 92, inRect.width, 43), "DeepRimVertical.Profiler.Caveat".Translate());
            if (!string.IsNullOrEmpty(VerticalProfiler.LastCsvPath)) Widgets.Label(new Rect(0, 136, inRect.width, 35), VerticalProfiler.LastCsvPath);

            var detailRows = snapshot.Rows.Where(row => row.Key.Metric != VerticalProfileMetric.ObjectTick && row.Key.Metric != VerticalProfileMetric.FrameInterval)
                .OrderByDescending(row => row.Stats.TotalMs).ToList();
            var objects = snapshot.Rows.Where(row => row.Key.Metric == VerticalProfileMetric.ObjectTick)
                .OrderByDescending(row => row.Stats.TotalMs).Take(25).ToList();
            var outer = new Rect(0, 175, inRect.width, Mathf.Max(60, inRect.height - 178));
            var height = 200 + snapshot.Maps.Count * 24 + (detailRows.Count + objects.Count) * 23;
            var inner = new Rect(0, 0, outer.width - 18, height);
            Widgets.BeginScrollView(outer, ref scroll, inner);
            float y = 0;
            Widgets.Label(new Rect(0, y, inner.width, 25), "DeepRimVertical.Profiler.Floors".Translate()); y += 27;
            DrawColumns(inner.width, y, new[] { "DeepRimVertical.Profiler.Floor".Translate().ToString(), "DeepRimVertical.Profiler.SimPerTick".Translate().ToString(), "DeepRimVertical.Profiler.UpdatePerFrame".Translate().ToString(), "DeepRimVertical.Profiler.Invalidations".Translate().ToString(), "DeepRimVertical.Profiler.Pawns".Translate().ToString() }); y += 24;
            foreach (var map in snapshot.Maps.Values.OrderByDescending(map => map.Level).ThenBy(map => map.MapId))
            {
                DrawColumns(inner.width, y, new[] { MapLabel(map.MapId), snapshot.Ticks > 0 ? (snapshot.SimulationMs(map.MapId) / snapshot.Ticks).ToString("F3") : "—",
                    snapshot.Frames > 0 ? ((snapshot.Find(map.MapId, VerticalProfileMetric.MapUpdate)?.TotalMs ?? 0) / snapshot.Frames).ToString("F3") : "—",
                    (snapshot.Find(map.MapId, VerticalProfileMetric.FullInvalidation)?.Calls ?? 0).ToString(), map.PawnCount.ToString() }); y += 24;
            }
            y += 15;
            Widgets.Label(new Rect(0, y, inner.width, 25), "DeepRimVertical.Profiler.Details".Translate()); y += 25;
            DrawDetailHeader(inner.width, ref y);
            foreach (var row in detailRows) DrawDetail(row, inner.width, ref y);
            y += 15;
            Widgets.Label(new Rect(0, y, inner.width, 25), "DeepRimVertical.Profiler.Objects".Translate()); y += 25;
            DrawDetailHeader(inner.width, ref y);
            foreach (var row in objects) DrawDetail(row, inner.width, ref y);
            Widgets.EndScrollView();
        }
        finally { Text.Font = oldFont; }
    }

    private string MapLabel(int mapId)
    {
        if (mapId == -1) return "DeepRimVertical.Profiler.Global".Translate();
        if (!snapshot.Maps.TryGetValue(mapId, out var map)) return "DeepRimVertical.Profiler.Unassigned".Translate();
        return (map.Level.HasValue ? (map.Level > 0 ? "+" : "") + map.Level.Value : "—") + $" / #{mapId}";
    }

    private static void DrawColumns(float width, float y, IReadOnlyList<string> values)
    {
        var columnWidth = width / values.Count;
        for (var i = 0; i < values.Count; i++) Widgets.Label(new Rect(i * columnWidth, y, columnWidth - 7, 23), values[i]);
    }

    private static void DrawDetailHeader(float width, ref float y)
    {
        DrawDetailColumns(width, y, "DeepRimVertical.Profiler.Floor".Translate(), "DeepRimVertical.Profiler.Metric".Translate(), "N", "Σ ms", "avg ms", "p95≤ ms", "max ms"); y += 23;
    }

    private void DrawDetail(VerticalProfileRow row, float width, ref float y)
    {
        var label = row.Key.Metric == VerticalProfileMetric.ObjectTick ? row.Label : ("DeepRimVertical.Profiler.Metric." + row.Key.Metric).Translate().ToString();
        DrawDetailColumns(width, y, MapLabel(row.Key.MapId), label, row.Stats.Calls.ToString(), row.Stats.TotalMs.ToString("F2"), row.Stats.MeanMs.ToString("F3"), row.Stats.PercentileUpperBound(.95).ToString("F3"), row.Stats.MaxMs.ToString("F3"));
        y += 23;
    }

    private static void DrawDetailColumns(float width, float y, params string[] values)
    {
        var proportions = new[] { .10f, .34f, .10f, .12f, .11f, .11f, .12f };
        float x = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var cellWidth = width * proportions[i];
            Widgets.Label(new Rect(x, y, cellWidth - 5, 23), values[i]);
            x += cellWidth;
        }
    }
}
