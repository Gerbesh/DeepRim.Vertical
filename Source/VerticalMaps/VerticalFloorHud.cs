using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.VerticalProfiling;
using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalSupports;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

// Three vanilla Widgets buttons; no persistent window or giant list on screen.
// The center button uses RimWorld's native FloatMenu for infrequent actions.
public static class VerticalFloorHud
{
    private const float ButtonHeight = 32f;
    private const float ArrowWidth = 32f;
    private const float LabelWidth = 96f;
    private const float Gap = 2f;

    public static void Draw(Map map, VerticalSiteWorldComponent site)
    {
        if (!site.TryGetFloor(map, out _, out var current))
            return;

        var above = site.TryGetAdjacentFloor(map, 1, out _, out var upper) ? upper?.Map : null;
        var below = site.TryGetAdjacentFloor(map, -1, out _, out var lower) ? lower?.Map : null;
        var focusCell = UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center;

        // Same lower-right HUD region as the original LVL button, but with
        // no extra profiler button and no occlusion of the game map.
        var totalWidth = ArrowWidth * 2f + LabelWidth + Gap * 2f;
        var x = Mathf.Max(8f, UI.screenWidth - totalWidth - 65f);
        var y = Mathf.Max(8f, UI.screenHeight - ButtonHeight - 86f);
        var upRect = new Rect(x, y, ArrowWidth, ButtonHeight);
        var levelRect = new Rect(x + ArrowWidth + Gap, y, LabelWidth, ButtonHeight);
        var downRect = new Rect(levelRect.xMax + Gap, y, ArrowWidth, ButtonHeight);

        if (Widgets.ButtonText(upRect, "▲", active: above != null))
            VerticalCameraSyncService.JumpPreservingView(map, above, focusCell);
        TooltipHandler.TipRegion(upRect, "DeepRimVertical.Hud.Up".Translate());

        if (Widgets.ButtonText(levelRect, "DeepRimVertical.Hud.Level".Translate(VerticalFloorLabel.Format(current.levelIndex))))
            OpenFloorMenu(map, site, current.levelIndex, above != null, below != null, focusCell);
        TooltipHandler.TipRegion(levelRect, "DeepRimVertical.Hud.Select".Translate());

        if (Widgets.ButtonText(downRect, "▼", active: below != null))
            VerticalCameraSyncService.JumpPreservingView(map, below, focusCell);
        TooltipHandler.TipRegion(downRect, "DeepRimVertical.Hud.Down".Translate());

        if (VerticalRuntime.Settings.enableUpperOverlayDebugOverlay && current.levelIndex > 0)
            DrawDebugTooltip(map, levelRect, focusCell);
    }

    private static void OpenFloorMenu(Map map, VerticalSiteWorldComponent site, int currentLevel,
        bool hasAbove, bool hasBelow, IntVec3 focus)
    {
        var options = new List<FloatMenuOption>();
        foreach (var floor in site.GetOrderedFloors(map).Where(f => f.Map != null).OrderByDescending(f => f.levelIndex))
        {
            var target = floor.Map;
            var label = (floor.levelIndex == currentLevel
                ? "DeepRimVertical.Navigator.FloorButtonCurrent"
                : "DeepRimVertical.Navigator.FloorButton").Translate(VerticalFloorLabel.Format(floor.levelIndex)).ToString();
            options.Add(new FloatMenuOption(label,
                floor.levelIndex == currentLevel ? null : () => VerticalCameraSyncService.JumpPreservingView(map, target, focus)));
        }

        if (VerticalRuntime.Settings.enableDevButtons)
        {
            if (!hasAbove && currentLevel < VerticalRuntime.Settings.maxAboveGroundFloors)
                options.Add(new FloatMenuOption("DeepRimVertical.Navigator.CreateAbove".Translate(),
                    () => VerticalMapCreationService.TryCreateFloor(map, 1, out _)));
            if (!hasBelow && currentLevel > -VerticalRuntime.Settings.maxUndergroundFloors)
                options.Add(new FloatMenuOption("DeepRimVertical.Navigator.CreateBelow".Translate(),
                    () => VerticalMapCreationService.TryCreateFloor(map, -1, out _)));
        }

        if (currentLevel > 0)
        {
            options.Add(new FloatMenuOption("DeepRimVertical.Navigator.ToggleOverlay".Translate(
                VerticalOverlayLabels.TranslateEnabledState(VerticalRuntime.Settings.enableUpperFloorOverlay)), () =>
            {
                VerticalRuntime.Settings.enableUpperFloorOverlay = !VerticalRuntime.Settings.enableUpperFloorOverlay;
                VerticalMapInvalidationService.MarkSiteDirty(map);
            }));
            options.Add(new FloatMenuOption("DeepRimVertical.Navigator.CycleOverlayMode".Translate(
                VerticalOverlayLabels.TranslateMode((VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode)), () =>
            {
                VerticalRuntime.Settings.upperFloorOverlayMode =
                    (int)VerticalOverlayLabels.NextMode((VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode);
                VerticalMapInvalidationService.MarkSiteDirty(map);
            }));
        }

        options.Add(new FloatMenuOption("DeepRimVertical.Profiler.Button".Translate(), VerticalProfilerWindow.Open));
        options.Add(new FloatMenuOption("DeepRimVertical.Navigator.LogisticsButton".Translate(),
            () => Find.WindowStack.Add(new Dialog_MessageBox("DeepRimVertical.Navigator.LogisticsHelp".Translate()))));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void DrawDebugTooltip(Map map, Rect rect, IntVec3 cell)
    {
        if (!cell.InBounds(map))
            return;
        var mask = UpperOverlayVisibilityMask.GetFor(map);
        if (mask == null)
            return;
        var state = mask.GetCellState(cell, VerticalRuntime.Settings.enableUpperFloorOverlay);
        var sample = LowerLevelCellSampler.Sample(VerticalRenderContextService.LowerMap(map), cell);
        VerticalOverlayDebugService.DrawCursorCell(map, cell, state);
        TooltipHandler.TipRegion(rect, "DeepRimVertical.Navigator.DebugCell".Translate(
            cell.ToString(),
            map.terrainGrid.TerrainAt(cell)?.label ?? "null",
            sample.SourceMap == null ? "none" : VerticalFloorLabel.Format(sample.SourceLevel),
            state.HasUpperTerrain.ToString(),
            state.HasUpperThing.ToString(),
            state.ShowLower.ToString(),
            state.ShowNeutralBase.ToString(),
            state.BlockLowerCompletely.ToString(),
            VerticalOverlayDebugService.DescribeLowerSource(state, sample)));
    }
}
