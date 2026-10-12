using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalConstruction;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public static class RouteMath
{
    // Non-negative directed graph; null costs represent blocked local legs.
    public static List<int> ShortestPath(int count, int start, int goal, Func<int, int, float?> cost)
    {
        var distances = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
        var previous = Enumerable.Repeat(-1, count).ToArray();
        var done = new bool[count];
        distances[start] = 0f;
        for (var step = 0; step < count; step++)
        {
            var next = -1;
            for (var i = 0; i < count; i++)
                if (!done[i] && (next < 0 || distances[i] < distances[next])) next = i;
            if (next < 0 || float.IsPositiveInfinity(distances[next])) return null;
            if (next == goal)
            {
                var route = new List<int>();
                for (var at = goal; at != -1; at = previous[at]) route.Add(at);
                route.Reverse(); return route;
            }
            done[next] = true;
            for (var i = 0; i < count; i++)
            {
                if (done[i]) continue;
                var edge = cost(next, i);
                if (!edge.HasValue || edge.Value < 0f || float.IsNaN(edge.Value) || float.IsInfinity(edge.Value)) continue;
                var candidate = distances[next] + edge.Value;
                if (candidate < distances[i]) { distances[i] = candidate; previous[i] = next; }
            }
        }
        return null;
    }
}

public sealed class VerticalRouteHop
{
    public Map Source, Destination;
    public IntVec3 Cell;
    public int TargetLevel;
    public float Cost;
}

public static class VerticalRouteUtility
{
    private sealed class Node
    {
        public Map Map;
        public LocalTargetInfo Target;
        public PathEndMode Mode;
        public VerticalRouteHop Hop;
    }
    public static bool Allowed(Pawn pawn, Map map, IntVec3 cell)
    {
        if (map == null || !cell.InBounds(map)) return false;
        var area = RemoteReachContext.AreaAt(pawn, map);
        return area == null || area.TrueCount == 0 || area[cell];
    }
    public static bool Allowed(Pawn pawn, Thing thing) => thing?.Spawned == true && !thing.Destroyed
        && Allowed(pawn, thing.Map, thing.Position) && !thing.IsForbidden(pawn.Faction);

    public static bool Reach(Pawn pawn, Map map, IntVec3 from, LocalTargetInfo target, PathEndMode mode)
    {
        if (!pawn.Spawned || !from.InBounds(map) || !target.Cell.InBounds(map) || !Allowed(pawn, map, target.Cell)) return false;
        using (new RemoteReachContext.Scope(pawn, map, from))
            return map.reachability.CanReach(from, target, mode, TraverseParms.For(pawn, pawn.NormalMaxDanger()));
    }
    public static bool ValidHop(Pawn pawn, VerticalSiteRecord site, VerticalPortalRecord portal, bool carrying)
    {
        var source = site.FloorAt(portal.sourceLevel)?.Map;
        var destination = site.FloorAt(portal.targetLevel)?.Map;
        if (source == null || destination == null || !portal.allowsPawnMove || portal.doorLikeBlockingState
            || carrying && !portal.allowsItemTransfer || !Allowed(pawn, source, portal.Cell) || !Allowed(pawn, destination, portal.Cell)) return false;
        var allowedDanger = pawn.NormalMaxDanger();
        var currentDanger = pawn.GetRegion(RegionType.Set_All)?.DangerFor(pawn) ?? Danger.None;
        if ((int)allowedDanger < (int)currentDanger) allowedDanger = currentDanger;
        if ((int)allowedDanger < (int)Danger.Deadly)
        {
            if ((int)(portal.Cell.GetRegion(source, RegionType.Set_All)?.DangerFor(pawn) ?? Danger.None) > (int)allowedDanger
                || (int)(portal.Cell.GetRegion(destination, RegionType.Set_All)?.DangerFor(pawn) ?? Danger.None) > (int)allowedDanger) return false;
        }
        if (!portal.Cell.InBounds(source) || !portal.Cell.InBounds(destination)
            || !portal.Cell.Standable(source) || !portal.Cell.Standable(destination)
            || !UpperFloorStateService.IsWalkable(source, portal.Cell) || !UpperFloorStateService.IsWalkable(destination, portal.Cell)) return false;
        var stair = portal.Cell.GetThingList(source).OfType<Building_VerticalStair>()
            .FirstOrDefault(s => s.LinkedStair?.Spawned == true && s.LinkedStair.Map == destination && s.LinkedStair.Position == portal.Cell);
        return stair != null && !stair.IsForbidden(pawn.Faction) && !stair.LinkedStair.IsForbidden(pawn.Faction);
    }
    public static List<VerticalRouteHop> Find(Pawn pawn, Map fromMap, IntVec3 from, Map targetMap,
        LocalTargetInfo target, PathEndMode mode, bool carrying = false)
    {
        if (!VerticalSiteWorldComponent.TryGet(out var world) || !world.TryGetFloor(fromMap, out var site, out _)
            || !world.TryGetFloor(targetMap, out var other, out _) || site != other) return null;
        var nodes = new List<Node>
        {
            new() { Map = fromMap, Target = from, Mode = PathEndMode.OnCell },
            new() { Map = targetMap, Target = target, Mode = mode }
        };
        foreach (var portal in site.portals.OrderBy(p => p.sourceLevel).ThenBy(p => p.cellX).ThenBy(p => p.cellZ))
        {
            if (!ValidHop(pawn, site, portal, carrying)) continue;
            var hop = new VerticalRouteHop
            {
                Source = site.FloorAt(portal.sourceLevel).Map, Destination = site.FloorAt(portal.targetLevel).Map,
                Cell = portal.Cell, TargetLevel = portal.targetLevel, Cost = Math.Max(1f, portal.traversalCost)
            };
            nodes.Add(new Node { Map = hop.Source, Target = hop.Cell, Mode = PathEndMode.OnCell, Hop = hop });
            nodes.Add(new Node { Map = hop.Destination, Target = hop.Cell, Mode = PathEndMode.OnCell });
        }
        var path = RouteMath.ShortestPath(nodes.Count, 0, 1, (a, b) =>
        {
            var left = nodes[a]; var right = nodes[b];
            if (left.Hop != null && b == a + 1) return left.Hop.Cost;
            if (left.Map != right.Map || !Reach(pawn, left.Map, left.Target.Cell, right.Target, right.Mode)) return null;
            return (float?)Math.Max(1f, left.Target.Cell.DistanceTo(right.Target.Cell) * 13f);
        });
        if (path == null) return null;
        var result = new List<VerticalRouteHop>();
        for (var i = 0; i < path.Count - 1; i++)
            if (nodes[path[i]].Hop != null && path[i + 1] == path[i] + 1) result.Add(nodes[path[i]].Hop);
        return result;
    }
}

// Read-only route preflight retains the actual pawn Map and all map registries.
// Only the native reachability guard and area getter see this explicit query context.
internal static class RemoteReachContext
{
    [ThreadStatic] public static Pawn Pawn;
    [ThreadStatic] public static Map Map;
    [ThreadStatic] public static IntVec3 Start;
    private static readonly FieldInfo Areas = AccessTools.Field(typeof(Pawn_PlayerSettings), "allowedAreas");
    public static Area AreaAt(Pawn pawn, Map map) => pawn.playerSettings?.RespectsAllowedArea == true
        && ((Dictionary<Map, Area>)Areas.GetValue(pawn.playerSettings)).TryGetValue(map, out var area) ? area : null;
    public static Map GuardMap(Thing thing) => thing == Pawn && Map != null ? Map : thing.Map;
    public static Map GuardMapHeld(Thing thing) => thing == Pawn && Map != null ? Map : thing.MapHeld;
    public static Region GuardRegion(Thing thing, RegionType types) => thing == Pawn && Map != null && Start.IsValid
        ? Start.GetRegion(Map, types) : thing.GetRegion(types);
    public sealed class Scope : IDisposable
    {
        private readonly Pawn previousPawn = Pawn;
        private readonly Map previousMap = Map;
        private readonly IntVec3 previousStart = Start;
        public Scope(Pawn pawn, Map map, IntVec3? start = null) { Pawn = pawn; Map = map; Start = start ?? IntVec3.Invalid; }
        public void Dispose() { Pawn = previousPawn; Map = previousMap; Start = previousStart; }
    }
}

[HarmonyPatch(typeof(Region), nameof(Region.Allows))]
public static class VerticalRegionDangerPreflightPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.Method(typeof(RegionAndRoomQuery), nameof(RegionAndRoomQuery.GetRegion), new[] { typeof(Thing), typeof(RegionType) });
        var replacement = AccessTools.Method(typeof(RemoteReachContext), nameof(RemoteReachContext.GuardRegion));
        var replaced = false;
        foreach (var instruction in instructions)
        {
            if (!replaced && instruction.Calls(getter)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; replaced = true; }
            yield return instruction;
        }
        if (!replaced) throw new InvalidOperationException("Native region danger query changed");
    }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) })]
public static class VerticalReachabilityPreflightPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));
        var replacement = AccessTools.Method(typeof(RemoteReachContext), nameof(RemoteReachContext.GuardMap));
        var replaced = false;
        foreach (var instruction in instructions)
        {
            if (!replaced && instruction.Calls(getter))
            { instruction.opcode = OpCodes.Call; instruction.operand = replacement; replaced = true; }
            yield return instruction;
        }
        if (!replaced) throw new InvalidOperationException("Native reachability map guard changed");
    }
}

[HarmonyPatch(typeof(Pawn_PlayerSettings), "get_EffectiveAreaRestrictionInPawnCurrentMap")]
public static class VerticalPreflightAreaPatch
{
    public static bool Prefix(Pawn_PlayerSettings __instance, ref Area __result)
    {
        if (RemoteReachContext.Map == null || RemoteReachContext.Pawn?.playerSettings != __instance) return true;
        __result = RemoteReachContext.AreaAt(RemoteReachContext.Pawn, RemoteReachContext.Map); return false;
    }
}
