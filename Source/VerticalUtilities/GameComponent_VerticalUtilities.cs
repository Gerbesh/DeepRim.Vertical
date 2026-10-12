using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalUtilities;

public sealed class GameComponent_VerticalUtilities : GameComponent
{
    private readonly HashSet<Building_VerticalUtility> buildings = new();
    private Dictionary<NodeKey, Building_VerticalUtility> nodes = new();
    private bool dirty = true;
    private int powerGraphTick = -1;
    private readonly Dictionary<PowerNet, PowerGroup> powerGroups = new();
    private readonly UtilityMath.TickLedger<CompPowerTrader> tickedTraders = new();
    private readonly UtilityMath.TickLedger<CompPowerBattery> tickedBatteries = new();
    private int powerRiserCount;
    private bool plumbingDirty = true;
    public GameComponent_VerticalUtilities(Game game) { }
    public static GameComponent_VerticalUtilities Get() => Current.Game?.GetComponent<GameComponent_VerticalUtilities>();
    public IEnumerable<Building_VerticalUtility> Buildings => buildings;
    public bool HasPowerRisers => powerRiserCount > 0;

    public void Register(Building_VerticalUtility building)
    {
        if (buildings.Add(building) && building.Kind == UtilityKind.Power) powerRiserCount++;
        if (building.Map.IsPlayerHome && VerticalSiteWorldComponent.TryGet(out var world)) world.GetOrCreateSiteForMap(building.Map);
        Invalidate(building.Kind == UtilityKind.Plumbing);
    }
    public void Unregister(Building_VerticalUtility building)
    {
        if (buildings.Remove(building) && building.Kind == UtilityKind.Power) powerRiserCount--;
        Invalidate(building.Kind == UtilityKind.Plumbing);
    }
    public void Invalidate(bool plumbing = true) { dirty = true; plumbingDirty |= plumbing; powerGraphTick = -1; }
    public void InvalidatePower() { powerGraphTick = -1; }
    public void RemoveMap(Map map)
    {
        buildings.RemoveWhere(b => b.Map == map);
        powerRiserCount = buildings.Count(b => b.Kind == UtilityKind.Power);
        powerGroups.Clear();
        Invalidate();
    }
    public override void FinalizeInit()
    {
        buildings.Clear();
        foreach (var map in Find.Maps)
            foreach (var building in map.listerThings.AllThings.OfType<Building_VerticalUtility>()) buildings.Add(building);
        powerRiserCount = buildings.Count(b => b.Kind == UtilityKind.Power);
        Invalidate();
    }
    public override void GameComponentTick()
    {
        FlushPlumbing();
        if (Find.TickManager.TicksGame % 120 == 0) ExchangeHeat();
    }
    public void FlushPlumbing()
    {
        if (plumbingDirty)
        {
            plumbingDirty = false;
            DubsHygieneAdapter.RefreshSites();
        }
    }
    private void EnsureNodes()
    {
        if (!dirty) return;
        nodes.Clear();
        buildings.RemoveWhere(b => !b.Spawned || b.Destroyed);
        powerRiserCount = buildings.Count(b => b.Kind == UtilityKind.Power);
        foreach (var b in buildings)
        {
            if (VerticalSiteWorldComponent.TryGet(out var world) && world.TryGetFloor(b.Map, out var site, out var floor))
                nodes[new NodeKey(site.siteId, floor.levelIndex, b.Position.x, b.Position.z, Channel(b.Kind))] = b;
        }
        dirty = false;
    }
    private static int Channel(UtilityKind kind) => kind == UtilityKind.Power ? 1 : kind == UtilityKind.Plumbing ? 2 : 0;
    private bool KeyFor(Building_VerticalUtility b, out NodeKey key)
    {
        key = default;
        if (!b.Spawned || !VerticalSiteWorldComponent.TryGet(out var world) || !world.TryGetFloor(b.Map, out var site, out var floor)) return false;
        key = new NodeKey(site.siteId, floor.levelIndex, b.Position.x, b.Position.z, Channel(b.Kind));
        return true;
    }
    public IEnumerable<Tuple<Building_VerticalUtility, Building_VerticalUtility>> Edges(UtilityKind kind)
    {
        EnsureNodes();
        foreach (var pair in nodes)
        {
            if (pair.Value.Kind != kind || !pair.Value.Enabled) continue;
            if (nodes.TryGetValue(pair.Key.AtLevel(pair.Key.Level + 1), out var next) && next.Kind == kind && next.Enabled)
                yield return Tuple.Create(pair.Value, next);
        }
    }
    public List<int> IntakeLevels(Building_VerticalUtility outlet)
    {
        EnsureNodes();
        var result = new List<int>();
        if (!KeyFor(outlet, out var key)) return result;
        for (var level = key.Level - 1; nodes.TryGetValue(key.AtLevel(level), out var node); level--)
        {
            if (!node.Enabled) break;
            if (node.Kind == UtilityKind.ThermalIntake) result.Add(level);
        }
        return result;
    }
    private Building_VerticalUtility FindSource(Building_VerticalUtility outlet)
    {
        if (!KeyFor(outlet, out var key)) return null;
        var level = UtilityMath.FindIntakeLevel(key.Level, outlet.SelectedSourceLevel, candidate =>
            nodes.TryGetValue(key.AtLevel(candidate), out var node)
                ? new UtilityMath.ColumnNode(node.Kind, node.Enabled) : (UtilityMath.ColumnNode?)null);
        return level.HasValue ? nodes[key.AtLevel(level.Value)] : null;
    }
    private void ExchangeHeat()
    {
        EnsureNodes();
        var rooms = new Dictionary<Room, float>();
        var deltas = new Dictionary<Room, float>();
        var links = new List<HeatLink>();
        var counts = new Dictionary<Building_VerticalUtility, int>();
        foreach (var outlet in buildings.Where(b => b.Kind == UtilityKind.ThermalOutlet).OrderBy(b => b.thingIDNumber))
        {
            outlet.SourceLevel = null; outlet.LastHeat = 0f;
            outlet.StatusKey = outlet.Powered ? "DeepRimVertical.Utility.NoConnection" : "DeepRimVertical.Utility.NoPower";
            if (!outlet.Powered) continue;
            var source = FindSource(outlet);
            if (source == null) continue;
            KeyFor(source, out var sourceKey);
            outlet.SourceLevel = sourceKey.Level;
            outlet.SourceTemperature = source.GetRoom()?.Temperature ?? source.Map.mapTemperature.OutdoorTemp;
            if (!source.Powered) { outlet.StatusKey = "DeepRimVertical.Utility.SourceOffline"; continue; }
            var from = source.GetRoom(); var to = outlet.GetRoom();
            if (from == null || to == null || from == to || from.UsesOutdoorTemperature || to.UsesOutdoorTemperature)
            { outlet.StatusKey = "DeepRimVertical.Utility.EnclosedRoom"; continue; }
            rooms[from] = from.Temperature; rooms[to] = to.Temperature;
            links.Add(new HeatLink(source, outlet, from, to));
            counts[source] = counts.TryGetValue(source, out var count) ? count + 1 : 1;
        }
        // Equal and opposite cell-degree energy transfers; never combine room identities.
        // Divide by both source fan-out and room degree to prevent collective overshoot.
        var degree = new Dictionary<Room, int>();
        foreach (var link in links)
        {
            degree[link.From] = degree.TryGetValue(link.From, out var a) ? a + 1 : 1;
            degree[link.To] = degree.TryGetValue(link.To, out var b) ? b + 1 : 1;
        }
        foreach (var link in links)
        {
            var heat = UtilityMath.HeatTransfer(rooms[link.From], rooms[link.To], link.From.CellCount,
                link.To.CellCount, 240f / counts[link.Source]);
            heat /= Math.Max(degree[link.From], degree[link.To]);
            deltas[link.From] = (deltas.TryGetValue(link.From, out var a) ? a : 0f) - heat / link.From.CellCount;
            deltas[link.To] = (deltas.TryGetValue(link.To, out var b) ? b : 0f) + heat / link.To.CellCount;
            link.Outlet.SourceTemperature = rooms[link.From];
            link.Outlet.LastHeat = heat;
            link.Outlet.StatusKey = "DeepRimVertical.Utility.Operating";
        }
        foreach (var delta in deltas) delta.Key.Temperature = rooms[delta.Key] + delta.Value;
    }

    public PowerGroup GetPowerGroup(PowerNet net)
    {
        if (!HasPowerRisers || net == null || net.Map == null || net.Map.gameConditionManager.ElectricityDisabled(net.Map)) return null;
        var tick = Find.TickManager.TicksGame;
        if (powerGraphTick != tick)
        {
            powerGroups.Clear();
            var edges = new List<Tuple<PowerNet, PowerNet>>();
            var nets = new HashSet<PowerNet>();
            foreach (var edge in Edges(UtilityKind.Power))
            {
                var a = edge.Item1.TryGetComp<CompPower>()?.PowerNet;
                var b = edge.Item2.TryGetComp<CompPower>()?.PowerNet;
                if (a == null || b == null || a.Map.gameConditionManager.ElectricityDisabled(a.Map)
                    || b.Map.gameConditionManager.ElectricityDisabled(b.Map)) continue;
                nets.Add(a); nets.Add(b); edges.Add(Tuple.Create(a, b));
            }
            foreach (var group in UtilityMath.Groups(nets, edges).Where(g => g.Count > 1))
            {
                var state = new PowerGroup(group);
                foreach (var member in group) powerGroups[member] = state;
            }
            powerGraphTick = tick;
        }
        powerGroups.TryGetValue(net, out var found);
        return found;
    }
    public PowerNet PrepareTick(PowerGroup group)
    {
        var tick = Find.TickManager.TicksGame;
        if (group.LastTick == tick) return null;
        group.LastTick = tick;
        var view = group.BuildView();
        view.powerComps.RemoveAll(c => !tickedTraders.Claim(tick, c));
        view.batteryComps.RemoveAll(c => !tickedBatteries.Claim(tick, c));
        return view.powerComps.Count == 0 && view.batteryComps.Count == 0 ? null : view;
    }
    public bool TrackLocalTick(PowerNet net, out PowerNet view)
    {
        view = null;
        var tick = Find.TickManager.TicksGame;
        if (!net.powerComps.Any(c => tickedTraders.Contains(tick, c))
            && !net.batteryComps.Any(c => tickedBatteries.Contains(tick, c)))
        {
            foreach (var c in net.powerComps) tickedTraders.Claim(tick, c);
            foreach (var c in net.batteryComps) tickedBatteries.Claim(tick, c);
            return true;
        }
        // A network rebuilt/split later in the same tick must not charge its old batteries twice.
        view = new PowerGroup(new List<PowerNet> { net }).BuildView();
        view.powerComps.RemoveAll(c => !tickedTraders.Claim(tick, c));
        view.batteryComps.RemoveAll(c => !tickedBatteries.Claim(tick, c));
        return false;
    }
    public sealed class PowerGroup
    {
        public readonly List<PowerNet> Nets;
        public int LastTick = -1;
        private PowerNet view;
        public PowerGroup(List<PowerNet> nets) { Nets = nets; }
        public PowerNet View() => view ??= BuildView();
        public PowerNet BuildView() => new(Array.Empty<CompPower>())
        {
            powerNetManager = Nets[0].powerNetManager,
            powerComps = Nets.SelectMany(n => n.powerComps).Distinct().ToList(),
            batteryComps = Nets.SelectMany(n => n.batteryComps).Distinct().ToList(),
            transmitters = Nets.SelectMany(n => n.transmitters).Distinct().ToList(),
            hasPowerSource = Nets.Any(n => n.hasPowerSource)
        };
    }
    private readonly struct HeatLink
    {
        public readonly Building_VerticalUtility Source, Outlet;
        public readonly Room From, To;
        public HeatLink(Building_VerticalUtility source, Building_VerticalUtility outlet, Room from, Room to)
        { Source = source; Outlet = outlet; From = from; To = to; }
    }
    private readonly struct NodeKey : IEquatable<NodeKey>
    {
        private readonly string site;
        private readonly int x, z, channel;
        public readonly int Level;
        public NodeKey(string site, int level, int x, int z, int channel)
        { this.site = site; Level = level; this.x = x; this.z = z; this.channel = channel; }
        public NodeKey AtLevel(int level) => new(site, level, x, z, channel);
        public bool Equals(NodeKey other) => site == other.site && Level == other.Level && x == other.x && z == other.z && channel == other.channel;
        public override bool Equals(object obj) => obj is NodeKey key && Equals(key);
        public override int GetHashCode() => unchecked((site?.GetHashCode() ?? 0) * 397 ^ Level * 31 ^ x * 17 ^ z * 7 ^ channel);
    }
}
