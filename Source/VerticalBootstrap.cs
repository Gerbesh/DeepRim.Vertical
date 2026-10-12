using HarmonyLib;
using Verse;

namespace DeepRim.Vertical;

[StaticConstructorOnStartup]
public static class VerticalBootstrap
{
    public const string HarmonyId = "gerbe.deeprim.vertical";

    static VerticalBootstrap()
    {
        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
        VerticalCombat.VerticalProjectileUtility.InstallDefs();
        VerticalUtilities.DubsHygieneAdapter.Install(harmony);
        Log.Message("[DeepRim Vertical] Bootstrap initialized.");
    }
}
