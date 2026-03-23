using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalOverlay;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalState;
using DeepRim.Vertical.VerticalWorld;
using Verse;

namespace DeepRim.Vertical.VerticalSupports;

public static class VerticalSupportService
{
    private static readonly Dictionary<int, SupportCache> CacheByMapId = new();

    public static void MarkDirty(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var cache))
        {
            cache = new SupportCache();
            CacheByMapId[map.uniqueID] = cache;
        }

        cache.IsDirty = true;
    }

    public static IEnumerable<IntVec3> GetBuildableCells(Map map)
    {
        return GetOrBuildCache(map).BuildableCells;
    }

    public static IEnumerable<IntVec3> GetOverlaySupportCells(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out _, out var floor))
        {
            return [];
        }

        if (floor.levelIndex <= 0)
        {
            return map.listerThings.AllThings
                .Where(VerticalRenderFilterPolicy.ShouldRenderSupportThing)
                .SelectMany(thing => thing.OccupiedRect().Cells)
                .Distinct()
                .ToList();
        }

        return GetOrBuildCache(map).ConnectedStructureCells.Count > 0
            ? GetOrBuildCache(map).ConnectedStructureCells
            : UpperFloorStateService.GetNonVoidCells(map).ToList();
    }

    public static bool AllowsPlacement(Map map, BuildableDef buildableDef, IntVec3 cell, Rot4 rot, out string reason)
    {
        reason = null;
        if (!IsPositiveFloor(map))
        {
            return true;
        }

        var cache = GetOrBuildCache(map);
        foreach (var occupiedCell in OccupiedRectFor(buildableDef, cell, rot).Cells)
        {
            if (!occupiedCell.InBounds(map) || !cache.BuildableMask[map.cellIndices.CellToIndex(occupiedCell)])
            {
                reason = "DeepRimVertical.Messages.SupportBlocked".Translate();
                return false;
            }
        }

        return true;
    }

    public static string DescribeCellSupport(Map map, IntVec3 cell)
    {
        if (!IsPositiveFloor(map))
        {
            return "DeepRimVertical.Support.Status.NotRequired".Translate();
        }

        if (!cell.InBounds(map))
        {
            return "DeepRimVertical.Support.Status.Unsupported".Translate();
        }

        return GetOrBuildCache(map).BuildableMask[map.cellIndices.CellToIndex(cell)]
            ? "DeepRimVertical.Support.Status.Supported".Translate()
            : "DeepRimVertical.Support.Status.Unsupported".Translate();
    }

    private static SupportCache GetOrBuildCache(Map map)
    {
        if (map == null)
        {
            return SupportCache.Empty;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var cache))
        {
            cache = new SupportCache();
            CacheByMapId[map.uniqueID] = cache;
        }

        if (!cache.IsDirty)
        {
            return cache;
        }

        RebuildCache(map, cache);
        cache.IsDirty = false;
        return cache;
    }

    private static void RebuildCache(Map map, SupportCache cache)
    {
        cache.Clear(map);

        if (!IsPositiveFloor(map) || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out var floor))
        {
            FillAllSupported(map, cache);
            return;
        }

        var belowFloor = site.FloorAt(floor.levelIndex - 1);
        if (belowFloor?.Map == null)
        {
            return;
        }

        foreach (var cell in GetBaseSeedCells(site, belowFloor))
        {
            AddSeed(cache, map, cell);
        }

        ExpandSupport(cache, map, cache.SeedCells);

        var structuralThings = map.listerThings.AllThings.Where(VerticalRenderFilterPolicy.ShouldRenderSupportThing).ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var thing in structuralThings)
            {
                var occupied = thing.OccupiedRect().Cells.ToList();
                if (!occupied.Any(cell => CellIsSupported(map, cache, cell)))
                {
                    continue;
                }

                var newCells = new List<IntVec3>();
                foreach (var occupiedCell in occupied)
                {
                    if (cache.ConnectedStructureCellSet.Add(occupiedCell))
                    {
                        cache.ConnectedStructureCells.Add(occupiedCell);
                        newCells.Add(occupiedCell);
                    }
                }

                if (newCells.Count == 0)
                {
                    continue;
                }

                ExpandSupport(cache, map, newCells);
                changed = true;
            }
        }
    }

    private static IEnumerable<IntVec3> GetBaseSeedCells(VerticalSiteRecord site, VerticalFloorRecord belowFloor)
    {
        if (belowFloor.Map == null)
        {
            return [];
        }

        var supportThings = belowFloor.Map.listerThings.AllThings
            .Where(VerticalRenderFilterPolicy.ShouldRenderSupportThing)
            .SelectMany(thing => thing.OccupiedRect().Cells);

        var roofBackedCells = belowFloor.Map.AllCells.Where(cell => belowFloor.Map.roofGrid.Roofed(cell));
        var portalCells = site.portals.Where(portal => portal.sourceLevel == belowFloor.levelIndex).Select(portal => portal.Cell);

        if (belowFloor.levelIndex <= 0)
        {
            return supportThings
                .Concat(roofBackedCells)
                .Concat(portalCells)
                .Distinct()
                .ToList();
        }

        return UpperFloorStateService.GetNonVoidCells(belowFloor.Map)
            .Concat(supportThings)
            .Concat(portalCells)
            .Distinct()
            .ToList();
    }

    private static void AddSeed(SupportCache cache, Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map) || !cache.SeedCellSet.Add(cell))
        {
            return;
        }

        cache.SeedCells.Add(cell);
    }

    private static void ExpandSupport(SupportCache cache, Map map, IEnumerable<IntVec3> originCells)
    {
        var radius = VerticalRuntime.Settings.upperFloorMaxOverhang;
        foreach (var origin in originCells)
        {
            foreach (var cell in CellRect.CenteredOn(origin, radius).ClipInsideMap(map).Cells)
            {
                if (cell.DistanceTo(origin) > radius + 0.1f)
                {
                    continue;
                }

                var index = map.cellIndices.CellToIndex(cell);
                if (cache.BuildableMask[index])
                {
                    continue;
                }

                cache.BuildableMask[index] = true;
                cache.BuildableCells.Add(cell);
            }
        }
    }

    private static bool CellIsSupported(Map map, SupportCache cache, IntVec3 cell)
    {
        return cell.InBounds(map) && cache.BuildableMask[map.cellIndices.CellToIndex(cell)];
    }

    private static bool IsPositiveFloor(Map map)
    {
        return map != null
               && VerticalSiteWorldComponent.TryGet(out var component)
               && component.TryGetFloor(map, out _, out var floor)
               && floor.levelIndex > 0;
    }

    private static void FillAllSupported(Map map, SupportCache cache)
    {
        if (map == null)
        {
            return;
        }

        foreach (var cell in map.AllCells)
        {
            cache.BuildableMask[map.cellIndices.CellToIndex(cell)] = true;
            cache.BuildableCells.Add(cell);
        }
    }

    private static CellRect OccupiedRectFor(BuildableDef buildableDef, IntVec3 cell, Rot4 rot)
    {
        return buildableDef is ThingDef thingDef
            ? GenAdj.OccupiedRect(cell, rot, thingDef.Size)
            : new CellRect(cell.x, cell.z, 1, 1);
    }

    private sealed class SupportCache
    {
        public static readonly SupportCache Empty = new();

        public bool IsDirty = true;
        public bool[] BuildableMask = [];
        public List<IntVec3> BuildableCells { get; } = new();
        public List<IntVec3> SeedCells { get; } = new();
        public HashSet<IntVec3> SeedCellSet { get; } = new();
        public List<IntVec3> ConnectedStructureCells { get; } = new();
        public HashSet<IntVec3> ConnectedStructureCellSet { get; } = new();

        public void Clear(Map map)
        {
            BuildableMask = map == null ? [] : new bool[map.cellIndices.NumGridCells];
            BuildableCells.Clear();
            SeedCells.Clear();
            SeedCellSet.Clear();
            ConnectedStructureCells.Clear();
            ConnectedStructureCellSet.Clear();
        }
    }
}
