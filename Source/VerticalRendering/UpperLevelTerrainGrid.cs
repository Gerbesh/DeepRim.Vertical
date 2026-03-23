using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public sealed class UpperLevelTerrainGrid
{
    private static readonly Dictionary<int, UpperLevelTerrainGrid> CacheByMapId = new();

    private readonly Dictionary<int, HashSet<IntVec2>> visibleSectionsByLevel = new();
    private readonly Dictionary<int, HashSet<IntVec3>> deckCellsByLevel = new();
    private readonly Dictionary<int, HashSet<IntVec3>> groundsByLevel = new();
    private readonly Dictionary<int, HashSet<IntVec3>> terrainCellsByLevel = new();
    private readonly Dictionary<int, HashSet<IntVec3>> thingCellsByLevel = new();
    private bool dirty = true;

    private UpperLevelTerrainGrid(Map map)
    {
        Map = map;
    }

    public Map Map { get; }

    public static UpperLevelTerrainGrid GetFor(Map map)
    {
        if (map == null)
        {
            return null;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var grid))
        {
            grid = new UpperLevelTerrainGrid(map);
            CacheByMapId[map.uniqueID] = grid;
        }

        grid.SetupUpperLevelTerrainCache();
        return grid;
    }

    public static void MarkDirty(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var grid))
        {
            grid = new UpperLevelTerrainGrid(map);
            CacheByMapId[map.uniqueID] = grid;
        }

        grid.dirty = true;
    }

    public IEnumerable<IntVec2> GetVisibleSectionsAtLevel(int level)
    {
        SetupUpperLevelTerrainCache();
        return visibleSectionsByLevel.TryGetValue(level, out var sections) ? sections : [];
    }

    public HashSet<IntVec3> GetDeckCellsAtLevel(int level)
    {
        SetupUpperLevelTerrainCache();
        return deckCellsByLevel.TryGetValue(level, out var cells) ? cells : [];
    }

    public HashSet<IntVec3> GetGroundsAtLevel(int level)
    {
        SetupUpperLevelTerrainCache();
        return groundsByLevel.TryGetValue(level, out var cells) ? cells : [];
    }

    public bool ShouldRenderLowerLevel(int level, IntVec3 cell)
    {
        SetupUpperLevelTerrainCache();
        return groundsByLevel.TryGetValue(level, out var cells) && cells.Contains(cell);
    }

    public bool HasUpperTerrainAtLevel(int level, IntVec3 cell)
    {
        SetupUpperLevelTerrainCache();
        return terrainCellsByLevel.TryGetValue(level, out var cells) && cells.Contains(cell);
    }

    public bool HasUpperThingAtLevel(int level, IntVec3 cell)
    {
        SetupUpperLevelTerrainCache();
        return thingCellsByLevel.TryGetValue(level, out var cells) && cells.Contains(cell);
    }

    public void Clear()
    {
        groundsByLevel.Clear();
        deckCellsByLevel.Clear();
        visibleSectionsByLevel.Clear();
        terrainCellsByLevel.Clear();
        thingCellsByLevel.Clear();
    }

    public void SetupUpperLevelTerrainCache()
    {
        if (!dirty)
        {
            return;
        }

        Clear();

        if (Map == null)
        {
            dirty = false;
            return;
        }

        if (!VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(Map, out var site, out var currentFloor))
        {
            dirty = false;
            return;
        }

        foreach (var floor in site.OrderedFloors)
        {
            if (floor?.Map == null || floor.levelIndex <= 0)
            {
                continue;
            }

            var groundCells = new HashSet<IntVec3>();
            var terrainCells = new HashSet<IntVec3>();
            var thingCells = new HashSet<IntVec3>();
            var deckCells = new HashSet<IntVec3>();
            var visibleSections = new HashSet<IntVec2>();
            var map = floor.Map;
            var voidTerrain = VerticalDefOf.DeepRimVertical_UpperVoid;
            var numGridCells = map.cellIndices.NumGridCells;
            for (var i = 0; i < numGridCells; i++)
            {
                var terrain = map.terrainGrid.TerrainAtIgnoreTemp(i);
                var cell = map.cellIndices.IndexToCell(i);
                if (terrain != null && terrain != voidTerrain)
                {
                    terrainCells.Add(cell);
                    groundCells.Add(cell);
                    AddToSection(visibleSections, cell);
                }

                if (terrain != null && terrain.isFoundation && terrain.IsSubstructure)
                {
                    deckCells.Add(cell);
                }
            }

            foreach (var portalCell in site.portals.Where(portal => portal.sourceLevel == floor.levelIndex).Select(portal => portal.Cell))
            {
                if (!portalCell.InBounds(map))
                {
                    continue;
                }

                groundCells.Add(portalCell);
                AddToSection(visibleSections, portalCell);
            }

            foreach (var thing in map.listerThings.AllThings)
            {
                if (!ShouldOwnSection(thing))
                {
                    continue;
                }

                foreach (var occupiedCell in thing.OccupiedRect().Cells)
                {
                    if (!occupiedCell.InBounds(map))
                    {
                        continue;
                    }

                    thingCells.Add(occupiedCell);
                    groundCells.Add(occupiedCell);
                    AddToSection(visibleSections, occupiedCell);
                }
            }

            groundsByLevel[floor.levelIndex] = groundCells;
            terrainCellsByLevel[floor.levelIndex] = terrainCells;
            thingCellsByLevel[floor.levelIndex] = thingCells;
            deckCellsByLevel[floor.levelIndex] = deckCells;
            visibleSectionsByLevel[floor.levelIndex] = visibleSections;
        }

        dirty = false;
    }

    private static void AddToSection(HashSet<IntVec2> visibleSections, IntVec3 cell)
    {
        cell.ToSection(out var x, out var y);
        visibleSections.Add(new IntVec2(x, y));
    }

    private static bool ShouldOwnSection(Thing thing)
    {
        if (thing == null || thing.Destroyed)
        {
            return false;
        }

        if (thing is Pawn or Plant or Mote)
        {
            return false;
        }

        return thing.def.category == ThingCategory.Building
               || thing is Blueprint
               || thing is Frame;
    }
}
