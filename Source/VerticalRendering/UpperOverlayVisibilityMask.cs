using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public readonly struct UpperOverlayCellState
{
    public UpperOverlayCellState(bool hasUpperTerrain, bool hasUpperThing, bool showLower, bool showNeutralBase)
    {
        HasUpperTerrain = hasUpperTerrain;
        HasUpperThing = hasUpperThing;
        ShowLower = showLower;
        ShowNeutralBase = showNeutralBase;
    }

    public bool HasUpperTerrain { get; }

    public bool HasUpperThing { get; }

    public bool ShowLower { get; }

    public bool ShowNeutralBase { get; }

    public bool BlockLowerCompletely => !ShowLower;
}

public sealed class UpperOverlayVisibilityMask
{
    private static readonly Dictionary<int, UpperOverlayVisibilityMask> CacheByMapId = new();

    private BitArray upperTerrainOccupancy;
    private BitArray upperThingOccupancy;
    private HashSet<IntVec2> revealSections = [];
    private bool dirty = true;

    private UpperOverlayVisibilityMask(Map map)
    {
        Map = map;
    }

    public Map Map { get; }

    public static UpperOverlayVisibilityMask GetFor(Map map)
    {
        if (map == null)
        {
            return null;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var mask))
        {
            mask = new UpperOverlayVisibilityMask(map);
            CacheByMapId[map.uniqueID] = mask;
        }

        mask.RebuildIfDirty();
        return mask;
    }

    public static void MarkDirty(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (!CacheByMapId.TryGetValue(map.uniqueID, out var mask))
        {
            mask = new UpperOverlayVisibilityMask(map);
            CacheByMapId[map.uniqueID] = mask;
        }

        mask.dirty = true;
        VerticalOverlayDebugService.LogVerbose($"Marked overlay mask dirty for map {map.uniqueID}.");
    }

    public bool HasUpperTerrainAt(IntVec3 cell)
    {
        return cell.InBounds(Map) && upperTerrainOccupancy[Map.cellIndices.CellToIndex(cell)];
    }

    public bool HasUpperThingAt(IntVec3 cell)
    {
        return cell.InBounds(Map) && upperThingOccupancy[Map.cellIndices.CellToIndex(cell)];
    }

    public UpperOverlayCellState GetCellState(IntVec3 cell, bool overlayEnabled)
    {
        if (Map == null || !cell.InBounds(Map))
        {
            return default;
        }

        RebuildIfDirty();

        var terrain = Map.terrainGrid.TerrainAt(cell);
        var isUpperVoid = terrain == VerticalWorld.VerticalDefOf.DeepRimVertical_UpperVoid;
        var hasUpperTerrain = HasUpperTerrainAt(cell);
        var hasUpperThing = HasUpperThingAt(cell);
        var showLower = overlayEnabled && isUpperVoid && !hasUpperTerrain && !hasUpperThing;
        var showNeutralBase = isUpperVoid && (!overlayEnabled || hasUpperThing);
        return new UpperOverlayCellState(hasUpperTerrain, hasUpperThing, showLower, showNeutralBase);
    }

    public bool SectionHasRevealCells(IntVec2 sectionCoord)
    {
        RebuildIfDirty();
        return revealSections.Contains(sectionCoord);
    }

    private void RebuildIfDirty()
    {
        if (!dirty)
        {
            return;
        }

        if (Map == null)
        {
            dirty = false;
            return;
        }

        var numCells = Map.cellIndices.NumGridCells;
        upperTerrainOccupancy = new BitArray(numCells);
        upperThingOccupancy = new BitArray(numCells);
        revealSections = [];
        var upperVoid = VerticalWorld.VerticalDefOf.DeepRimVertical_UpperVoid;

        for (var index = 0; index < numCells; index++)
        {
            var terrain = Map.terrainGrid.TerrainAtIgnoreTemp(index);
            upperTerrainOccupancy[index] = terrain != null && terrain != upperVoid;
        }

        foreach (var thing in Map.listerThings.AllThings)
        {
            if (!CountsAsUpperThing(thing))
            {
                continue;
            }

            foreach (var cell in thing.OccupiedRect().Cells)
            {
                if (cell.InBounds(Map))
                {
                    upperThingOccupancy[Map.cellIndices.CellToIndex(cell)] = true;
                }
            }
        }

        for (var index = 0; index < numCells; index++)
        {
            if (upperTerrainOccupancy[index] || upperThingOccupancy[index])
            {
                continue;
            }

            var terrain = Map.terrainGrid.TerrainAtIgnoreTemp(index);
            if (terrain != upperVoid)
            {
                continue;
            }

            AddRevealSection(Map.cellIndices.IndexToCell(index));
        }

        dirty = false;
        VerticalOverlayDebugService.LogVerbose($"Rebuilt overlay mask for map {Map.uniqueID} at level {VerticalRenderContextService.GetLevel(Map)}.");
    }

    private void AddRevealSection(IntVec3 cell)
    {
        cell.ToSection(out var x, out var y);
        revealSections.Add(new IntVec2(x, y));
    }

    private static bool CountsAsUpperThing(Thing thing)
    {
        if (thing == null || thing.Destroyed || !thing.Spawned)
        {
            return false;
        }

        if (thing is Pawn or Plant or Mote)
        {
            return false;
        }

        if (thing is Blueprint or Frame)
        {
            return true;
        }

        if (thing.def.category == ThingCategory.Building)
        {
            return true;
        }

        return false;
    }
}
