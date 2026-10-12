using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using Verse;

namespace DeepRim.Vertical.VerticalUtilities;

// Optional runtime binding: no BadHygiene types in public signatures or assembly references.
public static class DubsHygieneAdapter
{
    public const string PackageId = "dubwise.dubsbadhygiene";
    public const string WorkshopId = "836308268";
    private static Type pipeType, netType;
    private static FieldInfo netRef, things, mapComp, netMode, slave, ip, masterCounter, managers;
    private static MethodInfo allTileNets, initNet, refresh, regen;
    private static FieldInfo[] capacities;
    private static readonly Dictionary<object, List<object>> groups = new();
    private static readonly Dictionary<int, List<object>> tileMembers = new();
    private static bool rebuilding;
    public static bool Available { get; private set; }

    public static void Install(Harmony harmony)
    {
        pipeType = AccessTools.TypeByName("DubsBadHygiene.CompPipe");
        netType = AccessTools.TypeByName("DubsBadHygiene.PlumbingNet");
        var manager = AccessTools.TypeByName("DubsBadHygiene.HygienePipeMapComp");
        if (pipeType == null || netType == null || manager == null) return;
        try
        {
            netRef = RequireField(pipeType, "pipeNetRef");
            things = RequireField(netType, "PipedThings");
            mapComp = RequireField(netType, "MapComp");
            netMode = RequireField(netType, "NetType");
            slave = RequireField(netType, "slave"); ip = RequireField(netType, "IP");
            masterCounter = RequireField(manager, "MasterInternetID");
            managers = RequireField(manager, "DBHComps");
            allTileNets = AccessTools.Method(netType, "AllTileNets", new[] { typeof(int) });
            initNet = AccessTools.Method(netType, "InitNet");
            refresh = AccessTools.Method(manager, "RefreshInternetsOnTile", new[] { typeof(int) });
            regen = AccessTools.Method(manager, "RegenPipeGrids");
            capacities = new[] { "HeatingCap", "CoolingCap", "BoilerCapacitySum", "HeatStoreCapacitySum",
                "OutdoorUnitCapacitySum", "IndoorUnitCapacitySum", "WaterCap", "GroundWaterCapacitySum",
                "PumpingCapacitySum", "WaterPumpedPerTick" }.Select(name => RequireField(netType, name)).ToArray();
            if (allTileNets == null || initNet == null || refresh == null || regen == null) throw new MissingMethodException("DBH network contract");
            harmony.Patch(refresh, prefix: new HarmonyMethod(typeof(DubsHygieneAdapter), nameof(RefreshPrefix)));
            harmony.Patch(AccessTools.Method(netType, "Tick"), postfix: new HarmonyMethod(typeof(DubsHygieneAdapter), nameof(TickPostfix)));
            harmony.Patch(AccessTools.Method(manager, "MapRemoved"), postfix: new HarmonyMethod(typeof(DubsHygieneAdapter), nameof(MapRemovedPostfix)));
            Available = true;
            Log.Message("[DeepRim Vertical] Dubs Bad Hygiene adapter bound: " + netType.Assembly.GetName().Version);
        }
        catch (Exception error)
        {
            Log.Error("[DeepRim Vertical] DBH risers unavailable: " + error);
        }
    }
    private static FieldInfo RequireField(Type type, string name) => AccessTools.Field(type, name)
        ?? throw new MissingFieldException(type.FullName, name);
    private static Map MapOf(object net) => ((MapComponent)mapComp.GetValue(net))?.map;
    private static object PipeNet(Building_VerticalUtility building) => building.AllComps
        .Where(c => pipeType.IsInstanceOfType(c)).Select(c => netRef.GetValue(c)).FirstOrDefault();
    public static void Clear() { groups.Clear(); tileMembers.Clear(); rebuilding = false; }
    public static bool IsConnected(Building_VerticalUtility building)
    {
        if (!Available) return false;
        var net = PipeNet(building);
        return net != null && groups.TryGetValue(net, out var group) && group.Count > 1;
    }
    public static void MapRemovedPostfix(object __instance)
    {
        var tile = (int)((MapComponent)__instance).map.Tile;
        RemoveTile(tile);
        if (Available) refresh.Invoke(null, new object[] { tile });
    }
    private static void RemoveTile(int tile)
    {
        if (!tileMembers.TryGetValue(tile, out var previous)) return;
        foreach (var net in previous) groups.Remove(net);
        tileMembers.Remove(tile);
    }
    public static void RefreshSites()
    {
        if (!Available || !VerticalSiteWorldComponent.TryGet(out var world)) return;
        // RegisterPipe only marks dirty; force local grids before resolving the vertical links.
        // This also works while the relevant floors are not visible in the camera.
        foreach (var manager in ((IDictionary)managers.GetValue(null)).Values.Cast<object>().ToList())
        {
            if (world.TryGetFloor(((MapComponent)manager).map, out _, out _)) regen.Invoke(manager, null);
        }
        foreach (var tile in world.Sites.Select(s => (int)s.tile).Distinct()) refresh.Invoke(null, new object[] { tile });
    }
    public static bool RefreshPrefix(int tile)
    {
        if (!Available || rebuilding || !VerticalSiteWorldComponent.TryGet(out var world)
            || !world.Sites.Any(s => (int)s.tile == tile && s.floors.Any(f => f.levelIndex != 0))) return true;
        rebuilding = true;
        try
        {
            var nets = ((IEnumerable)allTileNets.Invoke(null, new object[] { tile })).Cast<object>().ToList();
            RemoveTile(tile);
            tileMembers[tile] = nets;
            foreach (var net in nets)
            {
                groups.Remove(net);
                // Native multi-map grouping mutates PipedThings. Restore genuine local membership
                // before rebuilding, so removing a riser cannot leave remote tanks/fixtures attached.
                var set = (HashSet<ThingWithComps>)things.GetValue(net);
                var map = MapOf(net);
                set.RemoveWhere(t => t == null || !t.Spawned || t.Map != map
                    || !t.AllComps.Any(c => pipeType.IsInstanceOfType(c) && ReferenceEquals(netRef.GetValue(c), net)));
                initNet.Invoke(net, null);
                slave.SetValue(net, false);
                foreach (var field in capacities) field.SetValue(net, 0f);
            }
            var edges = new List<Tuple<object, object>>();
            var service = GameComponent_VerticalUtilities.Get();
            if (service != null)
            {
                foreach (var edge in service.Edges(UtilityKind.Plumbing))
                {
                    if ((int)edge.Item1.Map.Tile != tile) continue;
                    var a = PipeNet(edge.Item1); var b = PipeNet(edge.Item2);
                    if (a != null && b != null && nets.Contains(a) && nets.Contains(b)
                        && Equals(netMode.GetValue(a), netMode.GetValue(b))) edges.Add(Tuple.Create(a, b));
                }
            }
            // Preserve DBH's ordinary same-tile links between non-vertical maps, e.g. pocket maps.
            // Additional DeepRim floors only participate through the explicit riser edges above.
            var cellsField = RequireField(netType, "cells");
            for (var i = 0; i < nets.Count; i++)
            for (var j = i + 1; j < nets.Count; j++)
            {
                var a = nets[i]; var b = nets[j];
                if (MapOf(a) == MapOf(b) || MapOf(a)?.Parent is VerticalSiteMapParent
                    || MapOf(b)?.Parent is VerticalSiteMapParent || !Equals(netMode.GetValue(a), netMode.GetValue(b))) continue;
                if (((HashSet<IntVec3>)cellsField.GetValue(a)).Overlaps((HashSet<IntVec3>)cellsField.GetValue(b)))
                    edges.Add(Tuple.Create(a, b));
            }
            foreach (var group in UtilityMath.Groups(nets, edges))
            {
                group.Sort((a, b) => (MapOf(a)?.uniqueID ?? 0).CompareTo(MapOf(b)?.uniqueID ?? 0));
                var id = (int)masterCounter.GetValue(null) + 1;
                masterCounter.SetValue(null, id);
                var union = new HashSet<ThingWithComps>();
                foreach (var net in group) union.UnionWith((HashSet<ThingWithComps>)things.GetValue(net));
                foreach (var net in group)
                {
                    var set = (HashSet<ThingWithComps>)things.GetValue(net);
                    set.Clear(); set.UnionWith(union);
                    ip.SetValue(net, id); slave.SetValue(net, !ReferenceEquals(net, group[0]));
                    initNet.Invoke(net, null);
                    groups[net] = group;
                }
            }
            return false;
        }
        finally { rebuilding = false; }
    }
    public static void TickPostfix(object __instance)
    {
        if (!Available || (bool)slave.GetValue(__instance) || !groups.TryGetValue(__instance, out var group)) return;
        // Native tanks tick once on their own maps; each sees the same per-tank pumping share.
        // Resource storage itself remains exclusively on native CompWaterStorage instances.
        foreach (var member in group)
        {
            if (ReferenceEquals(member, __instance)) continue;
            foreach (var field in capacities) field.SetValue(member, field.GetValue(__instance));
        }
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.ClearCaches))]
public static class UtilityResetPatch
{
    public static void Prefix() => DubsHygieneAdapter.Clear();
}
