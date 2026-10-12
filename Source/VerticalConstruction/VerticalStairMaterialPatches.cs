using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalConstruction;

public static class VerticalStairMaterialUtility
{
    public static void RestoreLegacySteel(Thing thing, ThingDef steel)
    {
        var buildDef = thing.def?.entityDefToBuild as ThingDef ?? thing.def;
        if (buildDef?.GetModExtension<VerticalStairExtension>() == null || !buildDef.MadeFromStuff)
        {
            return;
        }

        if (thing is Blueprint_Build blueprint)
        {
            if (blueprint.stuffToUse == null)
            {
                blueprint.stuffToUse = steel;
            }
        }
        else if (thing.def.MadeFromStuff && thing.Stuff == null)
        {
            thing.SetStuffDirect(steel);
        }
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.ExposeData))]
public static class Thing_ExposeData_VerticalStairMaterialPatch
{
    public static void Prefix(Thing __instance)
    {
        // Migrate before vanilla reports missing Stuff on older fixed-steel stairs and frames.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            VerticalStairMaterialUtility.RestoreLegacySteel(__instance, ThingDefOf.Steel);
        }
    }
}
