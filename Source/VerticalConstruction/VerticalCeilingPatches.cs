using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalConstruction;

public static class VerticalCeilingProtection
{
    public static bool Covered(Map map, IntVec3 cell) => map != null
        && VerticalSiteWorldComponent.TryGet(out var world) && world.TryGetFloor(map, out var site, out var floor)
        && site.FloorAt(floor.levelIndex + 1)?.Map is Map upper && cell.InBounds(upper)
        && VerticalCeilingComponent.HasUpperCeilingMaterial(upper, cell);
}

[HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]
public static class VerticalRoofChangedPatch
{
    public static void Postfix(Map ___map, IntVec3 c, RoofDef def)
    {
        // Restore a lost ceiling only where an actual upper structure still covers the cell.
        // Do not continually assign a roof or claim an independently constructed/natural one.
        if (def == null && VerticalCeilingProtection.Covered(___map, c))
            ___map.GetComponent<VerticalCeilingComponent>()?.Invalidate(c);
    }
}

[HarmonyPatch(typeof(WorkGiver_RemoveRoof), nameof(WorkGiver_RemoveRoof.HasJobOnCell))]
public static class VerticalRoofRemovalJobPatch
{
    public static void Postfix(Pawn pawn, IntVec3 c, ref bool __result)
    {
        if (__result && pawn.Map.GetComponent<VerticalCeilingComponent>()?.Owns(c) == true
            && VerticalCeilingProtection.Covered(pawn.Map, c)) __result = false;
    }
}
