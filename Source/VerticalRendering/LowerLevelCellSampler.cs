using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public readonly struct LowerLevelCellSample
{
    public LowerLevelCellSample(Map sourceMap, TerrainDef terrain, bool usedRecursiveVoidChain, bool roofBlocked)
    {
        SourceMap = sourceMap;
        Terrain = terrain;
        UsedRecursiveVoidChain = usedRecursiveVoidChain;
        RoofBlocked = roofBlocked;
    }

    public Map SourceMap { get; }

    public TerrainDef Terrain { get; }

    public bool UsedRecursiveVoidChain { get; }

    public bool RoofBlocked { get; }

    public int SourceLevel => SourceMap == null ? int.MinValue : VerticalRenderContextService.GetLevel(SourceMap);

    public bool HasRenderableTerrain => SourceMap != null && (Terrain != null || RoofBlocked);
}

public static class LowerLevelCellSampler
{
    public static LowerLevelCellSample Sample(Map startMap, IntVec3 cell)
    {
        if (startMap == null || !cell.InBounds(startMap))
        {
            return default;
        }

        var map = startMap;
        var terrain = map.terrainGrid.TerrainAt(cell);
        var upperVoid = VerticalWorld.VerticalDefOf.DeepRimVertical_UpperVoid;
        var usedRecursiveVoidChain = false;

        while (map != null)
        {
            if (map.roofGrid.RoofAt(cell) == RoofDefOf.RoofConstructed)
            {
                return new LowerLevelCellSample(map, terrain, usedRecursiveVoidChain, roofBlocked: true);
            }

            if (HasVisibleContentAt(map, cell, terrain, upperVoid))
            {
                return new LowerLevelCellSample(map, terrain, usedRecursiveVoidChain, roofBlocked: false);
            }

            if (terrain != upperVoid || VerticalRenderContextService.GetLevel(map) <= 0)
            {
                break;
            }

            usedRecursiveVoidChain = true;
            map = VerticalRenderContextService.LowerMap(map);
            if (map == null || !cell.InBounds(map))
            {
                return default;
            }

            terrain = map.terrainGrid.TerrainAt(cell);
        }

        return map == null ? default : new LowerLevelCellSample(map, terrain, usedRecursiveVoidChain, roofBlocked: false);
    }

    private static bool HasVisibleContentAt(Map map, IntVec3 cell, TerrainDef terrain, TerrainDef upperVoid)
    {
        if (map == null || !cell.InBounds(map))
        {
            return false;
        }

        if (terrain != null && terrain != upperVoid)
        {
            return true;
        }

        var things = map.thingGrid.ThingsListAtFast(cell);
        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            if (!ShouldStopAtThing(thing))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ShouldStopAtThing(Thing thing)
    {
        if (thing == null || thing.Destroyed || !thing.Spawned)
        {
            return false;
        }

        if (thing is Pawn or Mote)
        {
            return false;
        }

        if (thing is Blueprint or Frame)
        {
            return true;
        }

        return thing.def.category is ThingCategory.Building or ThingCategory.Plant or ThingCategory.Item
               && thing.def.drawerType is not DrawerType.None;
    }
}
