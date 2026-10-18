using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalTemperature;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

// Save data remains unchanged. Floor navigation is a small native HUD control.
public sealed class VerticalMapComponent : MapComponent
{
    private static bool navigatorVisible = true;

    public string siteId;
    public int levelIndex;

    public VerticalMapComponent(Map map) : base(map) { }

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
            return;
        if (!VerticalSiteWorldComponent.TryGet(out var component))
            return;

        component.GetOrCreateSiteForMap(map);
        RefreshFromRegistry();

        // The floor hotkeys still work when the HUD is hidden.
        HandleHotkeys(component);
        if (VerticalRuntime.Settings.showNavigator && navigatorVisible)
            VerticalFloorHud.Draw(map, component);
    }

    public void RefreshFromRegistry()
    {
        if (!VerticalSiteWorldComponent.TryGet(out var component))
            return;
        if (!component.TryGetFloor(map, out var site, out var floor))
            return;
        siteId = site.siteId;
        levelIndex = floor.levelIndex;
    }

    private void HandleHotkeys(VerticalSiteWorldComponent component)
    {
        if (VerticalDefOf.DeepRimVertical_ToggleNavigator.JustPressed)
            navigatorVisible = !navigatorVisible;

        if (VerticalDefOf.DeepRimVertical_SwitchFloorUp.JustPressed)
            TryJumpRelative(component, 1);
        else if (VerticalDefOf.DeepRimVertical_SwitchFloorDown.JustPressed)
            TryJumpRelative(component, -1);
    }

    private void TryJumpRelative(VerticalSiteWorldComponent component, int delta)
    {
        if (!component.TryGetAdjacentFloor(map, delta, out _, out var floor) || floor?.Map == null)
        {
            Messages.Message(
                delta > 0
                    ? "DeepRimVertical.Messages.NoFloorAbove".Translate()
                    : "DeepRimVertical.Messages.NoFloorBelow".Translate(),
                MessageTypeDefOf.RejectInput,
                false);
            return;
        }

        var focus = UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center;
        VerticalCameraSyncService.JumpPreservingView(map, floor.Map, focus);
    }
}
