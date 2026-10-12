using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalConstruction;

/// <summary>
/// Tracks only the lower-map roofs created by upper floor construction.
/// Native/natural/player roofs are never claimed or removed by the synchronizer.
/// </summary>
public sealed class VerticalCeilingComponent : MapComponent
{
    private List<IntVec3> savedOwnedCells = new();
    private HashSet<IntVec3> ownedCells = new();
    private readonly HashSet<IntVec3> pendingCells = new();
    private bool fullRefresh = true;

    public VerticalCeilingComponent(Map map) : base(map) { }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
            savedOwnedCells = ownedCells.ToList();
        Scribe_Collections.Look(ref savedOwnedCells, "deepRimOwnedCeilingCells", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            savedOwnedCells ??= new List<IntVec3>();
            ownedCells = new HashSet<IntVec3>(savedOwnedCells);
            fullRefresh = true;
        }
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        fullRefresh = true;
    }

    public override void MapGenerated()
    {
        base.MapGenerated();
        fullRefresh = true;
    }

    public void InvalidateAll() => fullRefresh = true;
    public void Invalidate(IntVec3 cell)
    {
        if (cell.InBounds(map)) pendingCells.Add(cell);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();
        if (!fullRefresh && pendingCells.Count == 0) return;
        if (!VerticalSiteWorldComponent.TryGet(out var component)
            || !component.TryGetFloor(map, out var site, out var floor)) return;

        var upperFloor = site.FloorAt(floor.levelIndex + 1);
        // Do not erase owned ceilings while a registered adjacent map is temporarily unloaded.
        if (upperFloor != null && upperFloor.Map == null) return;
        var upper = upperFloor?.Map;
        if (fullRefresh)
        {
            fullRefresh = false;
            pendingCells.Clear();
            bool changed = false;
            if (upper == null)
            {
                foreach (var cell in ownedCells.ToArray())
                    changed |= SynchronizeCell(null, cell);
            }
            else
            {
                foreach (var cell in map.AllCells)
                    changed |= SynchronizeCell(upper, cell);
            }
            if (changed) VerticalMapInvalidationService.MarkSiteDirty(map);
        }
        else
        {
            var cells = pendingCells.ToArray();
            pendingCells.Clear();
            foreach (var cell in cells)
                if (SynchronizeCell(upper, cell))
                    VerticalMapInvalidationService.MarkSiteCellDirty(map, cell, 1UL);
        }
    }

    public bool Owns(IntVec3 cell) => ownedCells.Contains(cell);

    private bool SynchronizeCell(Map upper, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return false;
        var shouldCover = upper != null && cell.InBounds(upper) && HasUpperCeilingMaterial(upper, cell);
        var roof = map.roofGrid.RoofAt(cell);
        if (shouldCover)
        {
            // Existing natural, underground or player-constructed roof belongs to RimWorld.
            if (roof != null && !ownedCells.Contains(cell)) return false;
            if (roof != null && roof != RoofDefOf.RoofConstructed)
            {
                ownedCells.Remove(cell);
                return false;
            }
            if (roof == RoofDefOf.RoofConstructed) return false;
            ownedCells.Add(cell);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            return true;
        }

        if (!ownedCells.Remove(cell)) return false;
        // If another system replaced our roof, relinquish ownership instead of deleting it.
        if (roof != RoofDefOf.RoofConstructed) return false;
        map.roofGrid.SetRoof(cell, null);
        return true;
    }

    public static bool HasUpperCeilingMaterial(Map upper, IntVec3 cell)
    {
        var terrain = upper.terrainGrid.TerrainAt(cell);
        if (terrain != null && terrain != VerticalDefOf.DeepRimVertical_UpperVoid && terrain.IsFloor)
            return true;

        // Full building blocks, not furniture, pawns, blueprints or transient frames.
        return upper.thingGrid.ThingsListAtFast(cell)
            .Any(t => t is Building && t.def.Fillage == FillCategory.Full);
    }
}

public static class VerticalCeilingService
{
    public static void InvalidateUpperCell(Map upperMap, IntVec3 cell)
    {
        if (upperMap == null || !VerticalSiteWorldComponent.TryGet(out var component)
            || !component.TryGetFloor(upperMap, out var site, out var floor)) return;
        var lower = site.FloorAt(floor.levelIndex - 1)?.Map;
        lower?.GetComponent<VerticalCeilingComponent>()?.Invalidate(cell);
    }

    public static void InvalidateBelow(Map upperMap)
    {
        if (upperMap == null || !VerticalSiteWorldComponent.TryGet(out var component)
            || !component.TryGetFloor(upperMap, out var site, out var floor)) return;
        site.FloorAt(floor.levelIndex - 1)?.Map?.GetComponent<VerticalCeilingComponent>()?.InvalidateAll();
    }
}
