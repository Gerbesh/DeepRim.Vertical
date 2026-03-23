using RimWorld;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using Verse;

namespace DeepRim.Vertical.VerticalState;

public static class UpperFloorStateService
{
    private static readonly Dictionary<int, UpperFloorStateCache> CacheByMapId = new();

    public static void MarkDirty(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var cache))
        {
            cache = new UpperFloorStateCache();
            CacheByMapId[map.uniqueID] = cache;
        }

        cache.IsDirty = true;
    }

    public static UpperFloorCellState GetState(Map map, IntVec3 cell)
    {
        if (map == null || !cell.InBounds(map))
        {
            return UpperFloorCellState.Void;
        }

        return GetOrBuildCache(map).States[map.cellIndices.CellToIndex(cell)];
    }

    public static bool IsWalkable(Map map, IntVec3 cell)
    {
        return GetState(map, cell) != UpperFloorCellState.Void;
    }

    public static IEnumerable<IntVec3> GetCells(Map map, UpperFloorCellState state)
    {
        var cache = GetOrBuildCache(map);
        for (var i = 0; i < cache.States.Length; i++)
        {
            if (cache.States[i] == state)
            {
                yield return map.cellIndices.IndexToCell(i);
            }
        }
    }

    public static IEnumerable<IntVec3> GetNonVoidCells(Map map)
    {
        var cache = GetOrBuildCache(map);
        for (var i = 0; i < cache.States.Length; i++)
        {
            if (cache.States[i] != UpperFloorCellState.Void)
            {
                yield return map.cellIndices.IndexToCell(i);
            }
        }
    }

    public static IEnumerable<IntVec3> GetPortalCells(Map map)
    {
        return GetCells(map, UpperFloorCellState.PortalCore);
    }

    private static UpperFloorStateCache GetOrBuildCache(Map map)
    {
        if (map == null)
        {
            return UpperFloorStateCache.Empty;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var cache))
        {
            cache = new UpperFloorStateCache();
            CacheByMapId[map.uniqueID] = cache;
        }

        if (!cache.IsDirty && cache.States.Length == map.cellIndices.NumGridCells)
        {
            return cache;
        }

        Rebuild(map, cache);
        cache.IsDirty = false;
        return cache;
    }

    private static void Rebuild(Map map, UpperFloorStateCache cache)
    {
        cache.States = map == null ? [] : new UpperFloorCellState[map.cellIndices.NumGridCells];
        if (map == null)
        {
            return;
        }

        if (!VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out var currentFloor) || currentFloor.levelIndex <= 0)
        {
            for (var i = 0; i < cache.States.Length; i++)
            {
                cache.States[i] = UpperFloorCellState.Structural;
            }

            return;
        }

        var belowFloor = site.FloorAt(currentFloor.levelIndex - 1);
        var portalCells = site.portals
            .Where(portal => portal.sourceLevel == currentFloor.levelIndex)
            .Select(portal => portal.Cell)
            .ToHashSet();

        foreach (var cell in map.AllCells)
        {
            var index = map.cellIndices.CellToIndex(cell);
            if (portalCells.Contains(cell))
            {
                cache.States[index] = UpperFloorCellState.PortalCore;
                continue;
            }

            if (HasUpperStructure(map, cell))
            {
                cache.States[index] = UpperFloorCellState.Structural;
                continue;
            }

            var terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain == VerticalDefOf.DeepRimVertical_UpperVoid)
            {
                cache.States[index] = UpperFloorCellState.Void;
                continue;
            }

            if (terrain == VerticalDefOf.DeepRimVertical_UpperDeck)
            {
                cache.States[index] = UpperFloorCellState.Deck;
                continue;
            }

            if (terrain != null && terrain.IsFloor)
            {
                cache.States[index] = UpperFloorCellState.Deck;
                continue;
            }

            cache.States[index] = UpperFloorCellState.Void;
        }
    }

    private static bool HasUpperStructure(Map map, IntVec3 cell)
    {
        foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
        {
            if (thing is Pawn)
            {
                continue;
            }

            if (thing is Frame || thing.def.category == ThingCategory.Building)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class UpperFloorStateCache
    {
        public static readonly UpperFloorStateCache Empty = new();

        public bool IsDirty = true;
        public UpperFloorCellState[] States = [];
    }
}
