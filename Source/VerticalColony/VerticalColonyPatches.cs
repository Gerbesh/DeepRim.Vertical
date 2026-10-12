using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

[HarmonyPatch(typeof(Map), "get_PlayerWealthForStoryteller")]
public static class VerticalColonyWealthPatch
{
    public static bool Prefix(Map __instance, ref float __result)
    {
        if (!VerticalColonyUtility.TrySite(__instance, out var site) || VerticalColonyUtility.Anchor(site)?.IsPlayerHome != true) return true;
        __result = VerticalColonyUtility.ThreatWealth(site); return false;
    }
}
[HarmonyPatch(typeof(Map), "get_PlayerPawnsForStoryteller")]
public static class VerticalColonyPopulationPatch
{
    public static void Postfix(Map __instance, ref IEnumerable<Pawn> __result)
    {
        if (VerticalColonyUtility.TrySite(__instance, out var site) && VerticalColonyUtility.Anchor(site)?.IsPlayerHome == true)
            __result = VerticalColonyUtility.PlayerPawns(site);
    }
}
[HarmonyPatch(typeof(Map), "get_StoryState")]
public static class VerticalColonyStoryStatePatch
{
    public static void Postfix(Map __instance, ref StoryState __result)
    {
        if (VerticalColonyUtility.TrySite(__instance, out var site) && VerticalColonyUtility.Anchor(site)?.IsPlayerHome == true)
            __result = VerticalColonyUtility.Anchor(site).storyState;
    }
}
[HarmonyPatch(typeof(Storyteller), "get_AllIncidentTargets")]
public static class VerticalColonyIncidentTargetsPatch
{
    public static void Postfix(ref List<IIncidentTarget> __result)
    {
        // Copy: the engine reuses its scratch list in nested storyteller calls.
        __result = __result.Where(t => t is not Map map || !VerticalColonyUtility.TrySite(map, out var site)
            || VerticalColonyUtility.Anchor(site)?.IsPlayerHome != true || map == VerticalColonyUtility.Anchor(site)).ToList();
    }
}
[HarmonyPatch(typeof(World), "get_PlayerWealthForStoryteller")]
public static class VerticalWorldWealthPatch
{
    public static bool Prefix(ref float __result)
    {
        if (!VerticalSiteWorldComponent.TryGet(out var world) || !world.Sites.Any(s => VerticalColonyUtility.Maps(s).Count() > 1)) return true;
        __result = Find.Maps.Where(map => !VerticalColonyUtility.TrySite(map, out var site)
            || VerticalColonyUtility.Anchor(site)?.IsPlayerHome != true || map == VerticalColonyUtility.Anchor(site))
            .Sum(map => map.PlayerWealthForStoryteller) + Find.WorldObjects.Caravans.Sum(c => c.PlayerWealthForStoryteller);
        return false;
    }
}
[HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
public static class VerticalColonyResourceReadoutPatch
{
    public static void Prefix(out int __state) { __state = ColonyQueryScope.Resources; ColonyQueryScope.Resources++; }
    public static void Finalizer(int __state) => ColonyQueryScope.Resources = __state;
}
[HarmonyPatch(typeof(ResourceCounter), "get_AllCountedAmounts")]
public static class VerticalColonyResourceDictionaryPatch
{
    private static readonly AccessTools.FieldRef<ResourceCounter, Map> MapField = AccessTools.FieldRefAccess<ResourceCounter, Map>("map");
    public static void Postfix(ResourceCounter __instance, ref Dictionary<ThingDef, int> __result)
    {
        if (ColonyQueryScope.Resources > 0 && VerticalColonyUtility.TrySite(MapField(__instance), out _))
            __result = Current.Game.GetComponent<GameComponent_VerticalColony>().Resources(MapField(__instance));
    }
}
[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.GetCount))]
public static class VerticalColonyResourceCountPatch
{
    private static readonly AccessTools.FieldRef<ResourceCounter, Map> MapField = AccessTools.FieldRefAccess<ResourceCounter, Map>("map");
    public static void Postfix(ResourceCounter __instance, ThingDef rDef, ref int __result)
    {
        if (ColonyQueryScope.Resources <= 0 || !VerticalColonyUtility.TrySite(MapField(__instance), out _)) return;
        var counts = Current.Game.GetComponent<GameComponent_VerticalColony>().Resources(MapField(__instance));
        __result = counts.TryGetValue(rDef, out var count) ? count : 0;
    }
}
