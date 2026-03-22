using UnityEngine;
using Verse;

namespace DeepRim.Vertical.Settings;

public sealed class DeepRimVerticalMod : Mod
{
    private readonly VerticalModSettings settings;

    public DeepRimVerticalMod(ModContentPack content)
        : base(content)
    {
        settings = GetSettings<VerticalModSettings>();
        VerticalRuntime.Settings = settings;
    }

    public override string SettingsCategory()
    {
        return "DeepRimVertical.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.Label("DeepRimVertical.Settings.Foundation".Translate());
        listing.CheckboxLabeled("DeepRimVertical.Settings.ShowNavigator".Translate(), ref settings.showNavigator);
        listing.CheckboxLabeled("DeepRimVertical.Settings.EnableDevButtons".Translate(), ref settings.enableDevButtons);
        listing.CheckboxLabeled("DeepRimVertical.Settings.AdoptSurfaceMap".Translate(), ref settings.adoptSurfaceMapOnDemand);
        listing.GapLine();
        listing.Label("DeepRimVertical.Settings.FloorBounds".Translate(settings.maxAboveGroundFloors, settings.maxUndergroundFloors));
        settings.maxAboveGroundFloors = Mathf.RoundToInt(listing.Slider(settings.maxAboveGroundFloors, 1f, 8f));
        settings.maxUndergroundFloors = Mathf.RoundToInt(listing.Slider(settings.maxUndergroundFloors, 4f, 25f));
        listing.GapLine();
        listing.Label("DeepRimVertical.Settings.Temperature".Translate());
        listing.CheckboxLabeled("DeepRimVertical.Settings.EnableDepthThermalProfile".Translate(), ref settings.enableDepthThermalProfile);
        settings.coldDepthLevel = Mathf.RoundToInt(listing.SliderLabeled("DeepRimVertical.Settings.ColdDepthLevel".Translate(settings.coldDepthLevel), settings.coldDepthLevel, -10f, -1f));
        settings.coldDepthTemp = listing.SliderLabeled("DeepRimVertical.Settings.ColdDepthTemp".Translate(settings.coldDepthTemp.ToStringTemperature()), settings.coldDepthTemp, -80f, 10f);
        settings.hotDepthLevel = Mathf.RoundToInt(listing.SliderLabeled("DeepRimVertical.Settings.HotDepthLevel".Translate(settings.hotDepthLevel), settings.hotDepthLevel, -25f, -4f));
        settings.hotDepthTemp = listing.SliderLabeled("DeepRimVertical.Settings.HotDepthTemp".Translate(settings.hotDepthTemp.ToStringTemperature()), settings.hotDepthTemp, -20f, 120f);
        settings.shaftHeatExchangeMultiplier = listing.SliderLabeled("DeepRimVertical.Settings.ShaftHeatExchangeMultiplier".Translate(settings.shaftHeatExchangeMultiplier.ToStringPercent()), settings.shaftHeatExchangeMultiplier, 0f, 5f);
        settings.closedPortalHeatLeakMultiplier = listing.SliderLabeled("DeepRimVertical.Settings.ClosedPortalHeatLeakMultiplier".Translate(settings.closedPortalHeatLeakMultiplier.ToStringPercent()), settings.closedPortalHeatLeakMultiplier, 0f, 2f);
        listing.GapLine();
        listing.Label("DeepRimVertical.Settings.Jobs".Translate());
        listing.CheckboxLabeled("DeepRimVertical.Settings.AllowCrossLevelBills".Translate(), ref settings.allowCrossLevelBills);
        listing.CheckboxLabeled("DeepRimVertical.Settings.AllowCrossLevelHauling".Translate(), ref settings.allowCrossLevelHauling);
        listing.CheckboxLabeled("DeepRimVertical.Settings.PreferSameLevelWork".Translate(), ref settings.preferSameLevelWork);
        settings.crossLevelBillIngredientSearchRangeByFloor = Mathf.RoundToInt(listing.SliderLabeled("DeepRimVertical.Settings.BillRangeByFloor".Translate(settings.crossLevelBillIngredientSearchRangeByFloor), settings.crossLevelBillIngredientSearchRangeByFloor, 10f, 150f));
        settings.crossLevelHaulMaxFloorDistance = Mathf.RoundToInt(listing.SliderLabeled("DeepRimVertical.Settings.HaulMaxFloorDistance".Translate(settings.crossLevelHaulMaxFloorDistance), settings.crossLevelHaulMaxFloorDistance, 1f, 25f));
        listing.GapLine();
        if (listing.ButtonText("DeepRimVertical.Settings.ResetDefaults".Translate()))
        {
            settings.ResetToDefaults();
        }

        listing.End();
        settings.Write();
    }
}
