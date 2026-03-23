using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public readonly struct LowerLevelCellSample
{
    public LowerLevelCellSample(Map sourceMap, TerrainDef terrain, bool usedRecursiveVoidChain)
    {
        SourceMap = sourceMap;
        Terrain = terrain;
        UsedRecursiveVoidChain = usedRecursiveVoidChain;
    }

    public Map SourceMap { get; }

    public TerrainDef Terrain { get; }

    public bool UsedRecursiveVoidChain { get; }

    public int SourceLevel => SourceMap == null ? int.MinValue : VerticalRenderContextService.GetLevel(SourceMap);

    public bool HasRenderableTerrain => SourceMap != null && Terrain != null;
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

        while (terrain == upperVoid && VerticalRenderContextService.GetLevel(map) > 0)
        {
            usedRecursiveVoidChain = true;
            map = VerticalRenderContextService.LowerMap(map);
            if (map == null || !cell.InBounds(map))
            {
                return default;
            }

            terrain = map.terrainGrid.TerrainAt(cell);
        }

        return map == null ? default : new LowerLevelCellSample(map, terrain, usedRecursiveVoidChain);
    }
}
