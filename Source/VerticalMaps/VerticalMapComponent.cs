using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.VerticalRouting;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalSupports;
using DeepRim.Vertical.VerticalTemperature;
using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalProfiling;
using DeepRim.Vertical.VerticalWorld;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public sealed class VerticalMapComponent : MapComponent
{
    private static bool navigatorVisible = true;

    public string siteId;
    public int levelIndex;
    private static Vector2 scrollPosition;

    public VerticalMapComponent(Map map)
        : base(map)
    {
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        RefreshFromRegistry();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref siteId, "siteId");
        Scribe_Values.Look(ref levelIndex, "levelIndex", 0);
    }

    public override void MapGenerated()
    {
        base.MapGenerated();
        RefreshFromRegistry();
    }

    public override void MapRemoved()
    {
        base.MapRemoved();
        LongEventHandler.ExecuteWhenFinished(() => VerticalSectionRenderPatches.RemoveMap(map));
        VerticalUtilities.GameComponent_VerticalUtilities.Get()?.RemoveMap(map);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();
        UpperFloorClimateService.SyncFromGround(map);
    }

    public override void MapComponentOnGUI()
    {
        base.MapComponentOnGUI();
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap != map)
        {
            return;
        }

        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        component.GetOrCreateSiteForMap(map);
        RefreshFromRegistry();

        HandleHotkeys(component);
        DrawToggleButton();
        var navigatorWindow = Find.WindowStack.WindowOfType<VerticalNavigatorWindow>();
        if (!VerticalRuntime.Settings.showNavigator || !navigatorVisible)
        {
            navigatorWindow?.Close(false);
        }
        else if (navigatorWindow == null)
        {
            Find.WindowStack.Add(new VerticalNavigatorWindow());
        }
    }

    public void RefreshFromRegistry()
    {
        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        if (!component.TryGetFloor(map, out var site, out var floor))
        {
            return;
        }

        siteId = site.siteId;
        levelIndex = floor.levelIndex;
    }

    private void HandleHotkeys(VerticalSiteWorldComponent component)
    {
        if (VerticalDefOf.DeepRimVertical_ToggleNavigator.JustPressed)
        {
            navigatorVisible = !navigatorVisible;
        }

        if (VerticalDefOf.DeepRimVertical_SwitchFloorUp.JustPressed)
        {
            TryJumpRelative(component, 1);
        }
        else if (VerticalDefOf.DeepRimVertical_SwitchFloorDown.JustPressed)
        {
            TryJumpRelative(component, -1);
        }
    }

    private void DrawToggleButton()
    {
        if (Widgets.ButtonText(new Rect(UI.screenWidth - 340f, UI.screenHeight - 118f, 110f, 32f), "DeepRimVertical.Profiler.Button".Translate()))
        {
            VerticalProfilerWindow.Open();
        }
        if (!VerticalRuntime.Settings.showNavigator) return;
        var buttonRect = new Rect(UI.screenWidth - 224f, UI.screenHeight - 118f, 52f, 32f);
        if (Widgets.ButtonText(buttonRect, navigatorVisible ? "LVL-" : "LVL+"))
        {
            navigatorVisible = !navigatorVisible;
        }
    }

    internal static void NavigatorClosed()
    {
        navigatorVisible = false;
    }

    internal void DrawNavigatorContents(VerticalSiteWorldComponent component, Rect inRect)
    {
        if (!component.TryGetFloor(map, out _, out var currentFloor))
        {
            return;
        }

        var floors = component.GetOrderedFloors(map).ToList();
        var listingRect = new Rect(0f, 0f, inRect.width, inRect.height);
        var listing = new Listing_Standard();
        listing.Begin(listingRect);
        listing.Label("DeepRimVertical.Navigator.CurrentFloor".Translate(VerticalFloorLabel.Format(currentFloor.levelIndex)));
        if (listing.ButtonText("DeepRimVertical.Navigator.LogisticsButton".Translate()))
            Find.WindowStack.Add(new Dialog_MessageBox("DeepRimVertical.Navigator.LogisticsHelp".Translate()));

        var focusCell = UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center;
        if (VerticalRuntime.Settings.enableDevButtons)
        {
            if (listing.ButtonText("DeepRimVertical.Navigator.CreateAbove".Translate()))
            {
                VerticalMapCreationService.TryCreateFloor(map, 1, out _);
            }

            if (listing.ButtonText("DeepRimVertical.Navigator.CreateBelow".Translate()))
            {
                VerticalMapCreationService.TryCreateFloor(map, -1, out _);
            }
        }

        if (currentFloor.levelIndex > 0)
        {
            listing.GapLine();
            if (listing.ButtonText("DeepRimVertical.Navigator.ToggleOverlay".Translate(VerticalOverlayLabels.TranslateEnabledState(VerticalRuntime.Settings.enableUpperFloorOverlay))))
            {
                VerticalRuntime.Settings.enableUpperFloorOverlay = !VerticalRuntime.Settings.enableUpperFloorOverlay;
                VerticalOverlayDebugService.Log($"Overlay toggled to {(VerticalRuntime.Settings.enableUpperFloorOverlay ? "On" : "Off")} on map {map.uniqueID}.");
                VerticalMapInvalidationService.MarkSiteDirty(map);
            }

            if (listing.ButtonText("DeepRimVertical.Navigator.CycleOverlayMode".Translate(VerticalOverlayLabels.TranslateMode((VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode))))
            {
                VerticalRuntime.Settings.upperFloorOverlayMode = (int)VerticalOverlayLabels.NextMode((VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode);
                VerticalOverlayDebugService.Log($"Overlay mode switched to {(VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode} on map {map.uniqueID}.");
                VerticalMapInvalidationService.MarkSiteDirty(map);
            }

            if (VerticalRuntime.Settings.enableUpperOverlayDebugOverlay)
            {
                listing.Label("DeepRimVertical.Navigator.SupportInfo".Translate(
                    focusCell.ToString(), VerticalSupportService.DescribeCellSupport(map, focusCell) + " / " + DescribeCellState(focusCell),
                    VerticalRuntime.Settings.upperFloorMaxOverhang));
                DrawUpperOverlayDebugInfo(listing, focusCell);
            }
        }
        listing.GapLine();
        TooltipHandler.TipRegion(new Rect(0f, listing.CurHeight, listingRect.width, Text.LineHeight), "DeepRimVertical.Navigator.LogisticsHelp".Translate());
        listing.Label("DeepRimVertical.Navigator.FloorList".Translate());
        var listTopOffset = listing.CurHeight;
        var desiredListHeight = Mathf.Min(224f, floors.Count * 28f + 5f);
        var availableHeight = Mathf.Max(24f, listingRect.height - listTopOffset - 4f);
        // Listing.Begin already enters listingRect's GUI group; these are local coordinates.
        var viewRect = new Rect(0f, listTopOffset, listingRect.width, Mathf.Min(availableHeight, desiredListHeight));
        var innerRect = new Rect(0f, 0f, viewRect.width - 16f, floors.Count * 28f + 5f);
        Widgets.BeginScrollView(viewRect, ref scrollPosition, innerRect, true);
        var y = 0f;
        foreach (var floor in floors.OrderByDescending(f => f.levelIndex))
        {
            var row = new Rect(0f, y, innerRect.width, 24f);
            var label = floor.levelIndex == currentFloor.levelIndex
                ? "DeepRimVertical.Navigator.FloorButtonCurrent".Translate(VerticalFloorLabel.Format(floor.levelIndex))
                : "DeepRimVertical.Navigator.FloorButton".Translate(VerticalFloorLabel.Format(floor.levelIndex));

            if (Widgets.ButtonText(row, label))
            {
                VerticalCameraSyncService.JumpPreservingView(map, floor.Map, focusCell);
            }

            y += 28f;
        }

        Widgets.EndScrollView();
        listing.Gap(viewRect.height + 4f);

        var portals = VerticalRuntime.Settings.enableUpperOverlayDebugOverlay ? component.PortalsAt(map, focusCell).ToList() : new System.Collections.Generic.List<DeepRim.Vertical.Persistence.VerticalPortalRecord>();
        if (portals.Count > 0)
        {
            listing.GapLine();
            listing.Label("DeepRimVertical.Navigator.PortalsAtCell".Translate(focusCell.ToString()));
            foreach (var portal in portals)
            {
                listing.Label("DeepRimVertical.Navigator.PortalLink".Translate(VerticalFloorLabel.Format(portal.sourceLevel), VerticalFloorLabel.Format(portal.targetLevel)));
            }
        }

        listing.End();
    }

    private void TryJumpRelative(VerticalSiteWorldComponent component, int delta)
    {
        if (!component.TryGetAdjacentFloor(map, delta, out _, out var targetFloor) || targetFloor?.Map == null)
        {
            Messages.Message(
                delta > 0
                    ? "DeepRimVertical.Messages.NoFloorAbove".Translate()
                    : "DeepRimVertical.Messages.NoFloorBelow".Translate(),
                MessageTypeDefOf.RejectInput,
                false);
            return;
        }

        var focusCell = UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center;
        VerticalCameraSyncService.JumpPreservingView(map, targetFloor.Map, focusCell);
    }

    private string DescribeCellState(IntVec3 cell)
    {
        return cell.InBounds(map)
            ? UpperFloorStateService.GetState(map, cell).ToString()
            : "OutOfBounds";
    }

    private void DrawUpperOverlayDebugInfo(Listing_Standard listing, IntVec3 focusCell)
    {
        if (VerticalRuntime.Settings.enableUpperOverlayDebugOverlay != true
            || !focusCell.InBounds(map)
            || levelIndex <= 0)
        {
            return;
        }

        var mask = UpperOverlayVisibilityMask.GetFor(map);
        if (mask == null)
        {
            return;
        }

        var overlayEnabled = VerticalRuntime.Settings.enableUpperFloorOverlay;
        var cellState = mask.GetCellState(focusCell, overlayEnabled);
        var lowerMap = VerticalRenderContextService.LowerMap(map);
        var sample = LowerLevelCellSampler.Sample(lowerMap, focusCell);
        VerticalOverlayDebugService.DrawCursorCell(map, focusCell, cellState);
        VerticalOverlayDebugService.LogVerbose(
            $"Cell {focusCell} map={map.uniqueID} level={levelIndex} terrain={map.terrainGrid.TerrainAt(focusCell)?.defName ?? "null"} " +
            $"upperTerrain={cellState.HasUpperTerrain} upperThing={cellState.HasUpperThing} showLower={cellState.ShowLower} neutral={cellState.ShowNeutralBase} " +
            $"source={VerticalOverlayDebugService.DescribeLowerSource(cellState, sample)} sourceLevel={(sample.SourceMap == null ? "none" : sample.SourceLevel.ToString())}");

        listing.Gap(4f);
        listing.Label("DeepRimVertical.Navigator.DebugCell".Translate(
            focusCell.ToString(),
            map.terrainGrid.TerrainAt(focusCell)?.label ?? "null",
            sample.SourceMap == null ? "none" : VerticalFloorLabel.Format(sample.SourceLevel),
            cellState.HasUpperTerrain.ToString(),
            cellState.HasUpperThing.ToString(),
            cellState.ShowLower.ToString(),
            cellState.ShowNeutralBase.ToString(),
            cellState.BlockLowerCompletely.ToString(),
            VerticalOverlayDebugService.DescribeLowerSource(cellState, sample)));
    }

}
