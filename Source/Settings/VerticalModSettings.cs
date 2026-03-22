using Verse;

namespace DeepRim.Vertical.Settings;

public sealed class VerticalModSettings : ModSettings
{
    public bool showNavigator = true;
    public bool enableDevButtons = true;
    public bool adoptSurfaceMapOnDemand = true;
    public bool enableDepthThermalProfile = true;
    public int maxAboveGroundFloors = 4;
    public int maxUndergroundFloors = 15;
    public int coldDepthLevel = -4;
    public float coldDepthTemp = -20f;
    public int hotDepthLevel = -15;
    public float hotDepthTemp = 60f;
    public float shaftHeatExchangeMultiplier = 1f;
    public float closedPortalHeatLeakMultiplier = 0.2f;
    public bool allowCrossLevelBills = true;
    public int crossLevelBillIngredientSearchRangeByFloor = 60;
    public bool allowCrossLevelHauling = true;
    public int crossLevelHaulMaxFloorDistance = 6;
    public bool preferSameLevelWork;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref showNavigator, "showNavigator", true);
        Scribe_Values.Look(ref enableDevButtons, "enableDevButtons", true);
        Scribe_Values.Look(ref adoptSurfaceMapOnDemand, "adoptSurfaceMapOnDemand", true);
        Scribe_Values.Look(ref enableDepthThermalProfile, "enableDepthThermalProfile", true);
        Scribe_Values.Look(ref maxAboveGroundFloors, "maxAboveGroundFloors", 4);
        Scribe_Values.Look(ref maxUndergroundFloors, "maxUndergroundFloors", 15);
        Scribe_Values.Look(ref coldDepthLevel, "coldDepthLevel", -4);
        Scribe_Values.Look(ref coldDepthTemp, "coldDepthTemp", -20f);
        Scribe_Values.Look(ref hotDepthLevel, "hotDepthLevel", -15);
        Scribe_Values.Look(ref hotDepthTemp, "hotDepthTemp", 60f);
        Scribe_Values.Look(ref shaftHeatExchangeMultiplier, "shaftHeatExchangeMultiplier", 1f);
        Scribe_Values.Look(ref closedPortalHeatLeakMultiplier, "closedPortalHeatLeakMultiplier", 0.2f);
        Scribe_Values.Look(ref allowCrossLevelBills, "allowCrossLevelBills", true);
        Scribe_Values.Look(ref crossLevelBillIngredientSearchRangeByFloor, "crossLevelBillIngredientSearchRangeByFloor", 60);
        Scribe_Values.Look(ref allowCrossLevelHauling, "allowCrossLevelHauling", true);
        Scribe_Values.Look(ref crossLevelHaulMaxFloorDistance, "crossLevelHaulMaxFloorDistance", 6);
        Scribe_Values.Look(ref preferSameLevelWork, "preferSameLevelWork", false);
    }

    public void ResetToDefaults()
    {
        showNavigator = true;
        enableDevButtons = true;
        adoptSurfaceMapOnDemand = true;
        enableDepthThermalProfile = true;
        maxAboveGroundFloors = 4;
        maxUndergroundFloors = 15;
        coldDepthLevel = -4;
        coldDepthTemp = -20f;
        hotDepthLevel = -15;
        hotDepthTemp = 60f;
        shaftHeatExchangeMultiplier = 1f;
        closedPortalHeatLeakMultiplier = 0.2f;
        allowCrossLevelBills = true;
        crossLevelBillIngredientSearchRangeByFloor = 60;
        allowCrossLevelHauling = true;
        crossLevelHaulMaxFloorDistance = 6;
        preferSameLevelWork = false;
    }
}
