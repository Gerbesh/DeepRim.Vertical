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
        Log.Message("[DeepRim Vertical] Bootstrap initialized.");
    }
}
