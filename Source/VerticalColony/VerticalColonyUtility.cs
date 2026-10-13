using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

public static class VerticalColonyUtility
{
    public static bool TrySite(Map map, out VerticalSiteRecord site)
    {
        site = null;
        return map != null && VerticalSiteWorldComponent.TryGet(out var world) && world.TryGetFloor(map, out site, out _);
    }
    public static IEnumerable<Map> Maps(VerticalSiteRecord site) => site.OrderedFloors.Select(f => f.Map).Where(m => m != null).Distinct();
    public static Map Anchor(VerticalSiteRecord site) => site.FloorAt(0)?.Map ?? Maps(site).FirstOrDefault(m => m.IsPlayerHome);
    public static IEnumerable<Map> Maps(Map map) => TrySite(map, out var site) ? Maps(site) : new[] { map };
    public static IEnumerable<Pawn> PlayerPawns(VerticalSiteRecord site) => Maps(site)
        .SelectMany(m => m.mapPawns.PawnsInFaction(Faction.OfPlayer)).Distinct();
    public static float ThreatWealth(VerticalSiteRecord site)
    {
        var anchor = Anchor(site);
        if (Find.Storyteller.difficulty.fixedWealthMode)
            return StorytellerUtility.FixedWealthModeMapWealthFromTimeCurve.Evaluate(anchor.AgeInDays * Find.Storyteller.difficulty.fixedWealthTimeFactor);
        return Current.Game.GetComponent<GameComponent_VerticalColony>().Wealth(site);
    }
    public static Dictionary<ThingDef, int> Resources(Map map)
    {
        var result = new Dictionary<ThingDef, int>();
        foreach (var floor in Maps(map))
        foreach (var pair in RawResources(floor.resourceCounter))
            result[pair.Key] = (result.TryGetValue(pair.Key, out var count) ? count : 0) + pair.Value;
        return result;
    }
    private static readonly HarmonyLib.AccessTools.FieldRef<ResourceCounter, Dictionary<ThingDef, int>> Counts =
        HarmonyLib.AccessTools.FieldRefAccess<ResourceCounter, Dictionary<ThingDef, int>>("countedAmounts");
    public static Dictionary<ThingDef, int> RawResources(ResourceCounter counter) => Counts(counter);
}

public sealed class GameComponent_VerticalColony : GameComponent
{
    private sealed class WealthSnapshot { public Map[] Maps; public int Tick; public float Value; }
    private readonly Dictionary<string, WealthSnapshot> wealth = new();
    public bool SynchronizingWealth { get; private set; }
    private readonly Dictionary<Map, Dictionary<ThingDef, int>> resources = new();
    private int resourceTick = -1;
    public GameComponent_VerticalColony(Game game) { }
    public float Wealth(VerticalSiteRecord site)
    {
        var maps = VerticalColonyUtility.Maps(site).ToArray();
        var tick = Find.TickManager.TicksGame;
        if (!wealth.TryGetValue(site.siteId, out var snapshot) || tick - snapshot.Tick > 5000 || !maps.SequenceEqual(snapshot.Maps))
        {
            // Recount all real owners in the same snapshot: independently aged map caches can
            // otherwise count a transferred pawn/cargo on both its old and new map.
            SynchronizingWealth = true;
            try { foreach (var map in maps) map.wealthWatcher.ForceRecount(true); }
            finally { SynchronizingWealth = false; }
            snapshot = new WealthSnapshot { Maps = maps, Tick = tick,
                Value = maps.Sum(m => m.wealthWatcher.WealthItems + .5f * m.wealthWatcher.WealthBuildings + m.wealthWatcher.WealthPawns) };
            wealth[site.siteId] = snapshot;
        }
        return snapshot.Value;
    }
    public void Invalidate() { wealth.Clear(); resources.Clear(); resourceTick = -1; }
    public void InvalidateResources() { resources.Clear(); resourceTick = -1; }
    public void InvalidateWealth(Map map)
    { if (!SynchronizingWealth && VerticalColonyUtility.TrySite(map, out var site)) wealth.Remove(site.siteId); }
    public Dictionary<ThingDef, int> Resources(Map map)
    {
        var tick = Find.TickManager.TicksGame / 150;
        if (tick != resourceTick) { resourceTick = tick; resources.Clear(); }
        var key = VerticalColonyUtility.TrySite(map, out var site) ? VerticalColonyUtility.Anchor(site) ?? map : map;
        if (!resources.TryGetValue(key, out var counts)) resources[key] = counts = VerticalColonyUtility.Resources(map);
        return counts;
    }
}

internal static class ColonyQueryScope
{
    [ThreadStatic] public static int Resources;
    [ThreadStatic] public static int Disease;
    [ThreadStatic] public static int ExplicitQuest;
}
