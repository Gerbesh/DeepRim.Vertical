using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalWorld;

[DefOf]
public static class VerticalDefOf
{
    public static WorldObjectDef DeepRimVertical_FloorSite;
    public static MapGeneratorDef DeepRimVertical_Underground;
    public static MapGeneratorDef DeepRimVertical_UpperFloor;
    public static TerrainDef DeepRimVertical_UpperVoid;
    public static TerrainDef DeepRimVertical_UpperDeck;
    public static KeyBindingDef DeepRimVertical_ToggleNavigator;
    public static KeyBindingDef DeepRimVertical_SwitchFloorUp;
    public static KeyBindingDef DeepRimVertical_SwitchFloorDown;

    static VerticalDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(VerticalDefOf));
    }
}
