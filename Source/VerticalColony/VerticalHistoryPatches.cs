using System.Collections.Generic;
using System.Reflection;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

[HarmonyPatch]
public static class VerticalWealthHistorySnapshotPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(HistoryAutoRecorderWorker_WealthItems), typeof(HistoryAutoRecorderWorker_WealthBuildings),
            typeof(HistoryAutoRecorderWorker_WealthPawns), typeof(HistoryAutoRecorderWorker_WealthTotal) })
            yield return AccessTools.Method(type, "PullRecord");
    }
    public static void Prefix()
    {
        if (Current.Game == null || !VerticalSiteWorldComponent.TryGet(out var world)) return;
        var colony = Current.Game.GetComponent<GameComponent_VerticalColony>();
        foreach (var site in world.Sites)
            if (VerticalColonyUtility.Anchor(site)?.IsPlayerHome == true) colony.Wealth(site);
    }
}
[HarmonyPatch(typeof(WealthWatcher), nameof(WealthWatcher.ForceRecount))]
public static class VerticalWealthRecountInvalidationPatch
{
    private static readonly AccessTools.FieldRef<WealthWatcher, Map> MapField = AccessTools.FieldRefAccess<WealthWatcher, Map>("map");
    public static void Postfix(WealthWatcher __instance) => Current.Game?.GetComponent<GameComponent_VerticalColony>()?.InvalidateWealth(MapField(__instance));
}
