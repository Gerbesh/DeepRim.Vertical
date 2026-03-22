using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalTemperature;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class UndergroundGenerationService
{
    private static readonly string[] ResourceRockDefNames =
    {
        "MineableSteel",
        "MineableComponentsIndustrial",
        "MineablePlasteel",
        "MineableUranium",
        "MineableGold",
        "MineableSilver",
        "MineableJade"
    };

    private static readonly Dictionary<int, UndergroundGenerationContext> PendingContexts = new();

    public static void PrepareForGeneration(Map map, Map sourceMap, IntVec3 anchorCell, int levelIndex)
    {
        if (map == null || sourceMap == null || levelIndex >= 0)
        {
            return;
        }

        var primaryRock = ChoosePrimaryRockDef(sourceMap);
        var baseTerrain = ResolveBaseTerrain(primaryRock);
        var context = new UndergroundGenerationContext(
            map.cellIndices.NumGridCells,
            anchorCell,
            levelIndex,
            primaryRock,
            baseTerrain,
            DefDatabase<TerrainDef>.GetNamedSilentFail("VolcanicRock") ?? baseTerrain,
            DefDatabase<TerrainDef>.GetNamedSilentFail("LavaDeep"));

        PendingContexts[map.uniqueID] = context;
    }

    public static void GenerateLayout(Map map)
    {
        var context = GetContext(map);
        if (context == null)
        {
            return;
        }

        MarkEntryPocket(map, context);

        if (context.LevelIndex >= -2)
        {
            MarkEarlyDepthCaverns(map, context);
        }

        MarkLavaRivers(map, context);
        MarkResourceDeposits(map, context);
        SealMapEdges(map, context);
    }

    public static void Materialize(Map map)
    {
        var context = GetContext(map);
        if (context == null)
        {
            return;
        }

        map.regionAndRoomUpdater.Enabled = false;
        try
        {
            using (map.pathing.DisableIncrementalScope())
            {
                foreach (var cell in map.AllCells)
                {
                    var index = map.cellIndices.CellToIndex(cell);
                    var roof = context.OpenMask[index] || context.LavaMask[index]
                        ? RoofDefOf.RoofRockThick
                        : RoofDefOf.RoofRockThick;

                    map.roofGrid.SetRoof(cell, roof);
                    map.terrainGrid.SetTerrain(cell, ResolveTerrainForCell(context, index));

                    if (context.OpenMask[index] || context.LavaMask[index])
                    {
                        continue;
                    }

                    var thingDef = context.ResourceMask[index] ?? context.PrimaryRock;
                    GenSpawn.Spawn(thingDef, cell, map, WipeMode.Vanish);
                }
            }
        }
        finally
        {
            map.regionAndRoomUpdater.Enabled = true;
        }
    }

    public static void InitializeFog(Map map)
    {
        var context = GetContext(map);
        if (context == null)
        {
            return;
        }

        MapGenerator.PlayerStartSpot = context.EntryCell;
        map.fogGrid.Refog(new CellRect(0, 0, map.Size.x, map.Size.z));

        foreach (var cell in CellRect.CenteredOn(context.EntryCell, 3).ClipInsideMap(map).Cells)
        {
            var index = map.cellIndices.CellToIndex(cell);
            if (context.LavaMask[index])
            {
                continue;
            }

            map.fogGrid.Unfog(cell);
        }

        PendingContexts.Remove(map.uniqueID);
    }

    private static UndergroundGenerationContext GetContext(Map map)
    {
        if (map == null)
        {
            return null;
        }

        if (PendingContexts.TryGetValue(map.uniqueID, out var context))
        {
            return context;
        }

        Log.Error($"[DeepRim Vertical] Missing underground generation context for map {map.uniqueID}.");
        return null;
    }

    private static ThingDef ChoosePrimaryRockDef(Map sourceMap)
    {
        var localRock = sourceMap.listerThings.AllThings
            .Where(thing => IsPrimaryNaturalRock(thing.def))
            .GroupBy(thing => thing.def)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

        if (localRock != null)
        {
            return localRock;
        }

        if (sourceMap.Biome?.forceRockTypes != null && sourceMap.Biome.forceRockTypes.Count > 0)
        {
            return sourceMap.Biome.forceRockTypes
                .Where(IsPrimaryNaturalRock)
                .OrderBy(def => def.defName)
                .FirstOrDefault();
        }

        var candidates = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(IsPrimaryNaturalRock)
            .OrderBy(def => def.defName)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No natural rock defs available for underground generation.");
        }

        return candidates[Mathf.Abs(sourceMap.Tile.GetHashCode()) % candidates.Count];
    }

    private static bool IsPrimaryNaturalRock(ThingDef def)
    {
        return def?.building != null
               && def.building.isNaturalRock
               && !def.building.isResourceRock
               && def.defName != "CollapsedRocks"
               && !def.defName.StartsWith("Smoothed", StringComparison.Ordinal);
    }

    private static TerrainDef ResolveBaseTerrain(ThingDef primaryRock)
    {
        var naturalTerrain = primaryRock?.building?.naturalTerrain;
        if (naturalTerrain != null)
        {
            return naturalTerrain;
        }

        return DefDatabase<TerrainDef>.AllDefsListForReading.FirstOrDefault(def =>
                   def.affordances != null
                   && def.affordances.Contains(TerrainAffordanceDefOf.SmoothableStone)
                   && def.fertility <= 0f)
               ?? TerrainDefOf.Soil;
    }

    private static TerrainDef ResolveTerrainForCell(UndergroundGenerationContext context, int index)
    {
        if (context.LavaMask[index] && context.LavaTerrain != null)
        {
            return context.LavaTerrain;
        }

        if (context.VolcanicMask[index] && context.VolcanicTerrain != null)
        {
            return context.VolcanicTerrain;
        }

        return context.BaseTerrain;
    }

    private static void MarkEntryPocket(Map map, UndergroundGenerationContext context)
    {
        foreach (var cell in CellRect.CenteredOn(context.EntryCell, 2).ClipInsideMap(map).Cells)
        {
            if (cell.DistanceTo(context.EntryCell) <= 3.1f)
            {
                MarkOpen(map, context, cell);
            }
        }
    }

    private static void MarkEarlyDepthCaverns(Map map, UndergroundGenerationContext context)
    {
        var cavernCount = context.LevelIndex == -1 ? 3 : 2;
        var radius = context.LevelIndex == -1 ? 6 : 4;

        for (var i = 0; i < cavernCount; i++)
        {
            if (!TryFindInteriorSolidCell(map, context, out var center, 220))
            {
                continue;
            }

            MarkBlob(map, context, center, radius + Rand.RangeInclusive(0, 3));
        }
    }

    private static void MarkResourceDeposits(Map map, UndergroundGenerationContext context)
    {
        var depth = -context.LevelIndex;
        var mapFactor = Mathf.Max(1f, map.cellIndices.NumGridCells / 14000f);
        var depositCount = Mathf.RoundToInt((8f + depth * 3.5f) * mapFactor);
        var depositSizeMin = Mathf.Clamp(4 + depth / 2, 4, 14);
        var depositSizeMax = Mathf.Clamp(10 + depth, 10, 26);

        for (var i = 0; i < depositCount; i++)
        {
            if (!TryFindResourceStart(map, context, out var start, 320))
            {
                continue;
            }

            var def = PickResourceDef(context.LevelIndex);
            MarkResourceBlob(map, context, start, def, Rand.RangeInclusive(depositSizeMin, depositSizeMax));
        }
    }

    private static ThingDef PickResourceDef(int levelIndex)
    {
        var weights = new Dictionary<string, float>
        {
            ["MineableSteel"] = 8f,
            ["MineableComponentsIndustrial"] = 5f,
            ["MineableSilver"] = 2f,
            ["MineableGold"] = levelIndex <= -5 ? 1.5f : 0.5f,
            ["MineablePlasteel"] = levelIndex <= -6 ? 2f : 0.5f,
            ["MineableUranium"] = levelIndex <= -8 ? 2f : 0.5f,
            ["MineableJade"] = levelIndex <= -4 ? 1.5f : 0.5f
        };

        var total = weights.Values.Sum();
        var roll = Rand.Value * total;
        foreach (var pair in weights)
        {
            roll -= pair.Value;
            if (roll <= 0f)
            {
                return DefDatabase<ThingDef>.GetNamed(pair.Key);
            }
        }

        return DefDatabase<ThingDef>.GetNamed(ResourceRockDefNames[0]);
    }

    private static void MarkResourceBlob(Map map, UndergroundGenerationContext context, IntVec3 start, ThingDef def, int size)
    {
        var frontier = new Queue<IntVec3>();
        var visited = new HashSet<IntVec3>();
        frontier.Enqueue(start);

        while (frontier.Count > 0 && visited.Count < size)
        {
            var cell = frontier.Dequeue();
            if (!cell.InBounds(map) || !visited.Add(cell) || cell.DistanceToEdge(map) <= 1)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (context.OpenMask[index] || context.LavaMask[index])
            {
                continue;
            }

            context.ResourceMask[index] = def;

            foreach (var next in GenAdjFast.AdjacentCellsCardinal(cell))
            {
                if (next.InBounds(map) && Rand.Chance(0.72f))
                {
                    frontier.Enqueue(next);
                }
            }
        }
    }

    private static void MarkLavaRivers(Map map, UndergroundGenerationContext context)
    {
        if (context.LavaTerrain == null || context.LevelIndex > VerticalRuntime.Settings.hotDepthLevel)
        {
            return;
        }

        var depthPastThreshold = Mathf.Max(0, -context.LevelIndex - -VerticalRuntime.Settings.hotDepthLevel);
        var riverCount = 2 + depthPastThreshold / 3;
        var halfWidth = 1 + depthPastThreshold / 4;

        for (var i = 0; i < riverCount; i++)
        {
            var z = Rand.RangeInclusive(12, map.Size.z - 12);
            var current = new IntVec3(6, 0, z);
            while (current.x < map.Size.x - 7)
            {
                for (var width = -halfWidth; width <= halfWidth; width++)
                {
                    var cell = new IntVec3(current.x, 0, current.z + width);
                    if (!IsInteriorCell(cell, map, 1))
                    {
                        continue;
                    }

                    var index = map.cellIndices.CellToIndex(cell);
                    context.OpenMask[index] = true;
                    context.LavaMask[index] = true;
                }

                foreach (var side in GenAdjFast.AdjacentCells8Way(current))
                {
                    if (!IsInteriorCell(side, map, 1))
                    {
                        continue;
                    }

                    var index = map.cellIndices.CellToIndex(side);
                    if (!context.LavaMask[index])
                    {
                        context.VolcanicMask[index] = true;
                    }
                }

                current.x += 1;
                current.z += Rand.RangeInclusive(-2, 2);
                current.z = Mathf.Clamp(current.z, 8, map.Size.z - 9);
            }
        }
    }

    private static void SealMapEdges(Map map, UndergroundGenerationContext context)
    {
        foreach (var cell in map.AllCells)
        {
            if (cell.DistanceToEdge(map) > 0)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            context.OpenMask[index] = false;
            context.LavaMask[index] = false;
            context.VolcanicMask[index] = false;
            context.ResourceMask[index] = null;
        }
    }

    private static bool TryFindInteriorSolidCell(Map map, UndergroundGenerationContext context, out IntVec3 result, int tries)
    {
        for (var i = 0; i < tries; i++)
        {
            var cell = new IntVec3(Rand.RangeInclusive(8, map.Size.x - 9), 0, Rand.RangeInclusive(8, map.Size.z - 9));
            if (cell.DistanceTo(context.EntryCell) <= 14f)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (!context.OpenMask[index] && !context.LavaMask[index])
            {
                result = cell;
                return true;
            }
        }

        result = IntVec3.Invalid;
        return false;
    }

    private static bool TryFindResourceStart(Map map, UndergroundGenerationContext context, out IntVec3 result, int tries)
    {
        for (var i = 0; i < tries; i++)
        {
            var cell = new IntVec3(Rand.RangeInclusive(4, map.Size.x - 5), 0, Rand.RangeInclusive(4, map.Size.z - 5));
            if (cell.DistanceTo(context.EntryCell) <= 10f)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (!context.OpenMask[index] && !context.LavaMask[index] && context.ResourceMask[index] == null)
            {
                result = cell;
                return true;
            }
        }

        result = IntVec3.Invalid;
        return false;
    }

    private static void MarkBlob(Map map, UndergroundGenerationContext context, IntVec3 center, int radius)
    {
        foreach (var cell in CellRect.CenteredOn(center, radius).ClipInsideMap(map).Cells)
        {
            if (cell.DistanceToEdge(map) > 0 && cell.DistanceTo(center) <= radius + Rand.Value * 1.5f)
            {
                MarkOpen(map, context, cell);
            }
        }
    }

    private static void MarkOpen(Map map, UndergroundGenerationContext context, IntVec3 cell)
    {
        if (!IsInteriorCell(cell, map, 1))
        {
            return;
        }

        context.OpenMask[map.cellIndices.CellToIndex(cell)] = true;
    }

    private static bool IsInteriorCell(IntVec3 cell, Map map, int minDistanceToEdge)
    {
        return cell.InBounds(map) && cell.DistanceToEdge(map) >= minDistanceToEdge;
    }

    private sealed class UndergroundGenerationContext
    {
        public UndergroundGenerationContext(
            int cellCount,
            IntVec3 entryCell,
            int levelIndex,
            ThingDef primaryRock,
            TerrainDef baseTerrain,
            TerrainDef volcanicTerrain,
            TerrainDef lavaTerrain)
        {
            EntryCell = entryCell;
            LevelIndex = levelIndex;
            PrimaryRock = primaryRock;
            BaseTerrain = baseTerrain;
            VolcanicTerrain = volcanicTerrain;
            LavaTerrain = lavaTerrain;
            OpenMask = new bool[cellCount];
            LavaMask = new bool[cellCount];
            VolcanicMask = new bool[cellCount];
            ResourceMask = new ThingDef[cellCount];
        }

        public IntVec3 EntryCell { get; }

        public int LevelIndex { get; }

        public ThingDef PrimaryRock { get; }

        public TerrainDef BaseTerrain { get; }

        public TerrainDef VolcanicTerrain { get; }

        public TerrainDef LavaTerrain { get; }

        public bool[] OpenMask { get; }

        public bool[] LavaMask { get; }

        public bool[] VolcanicMask { get; }

        public ThingDef[] ResourceMask { get; }
    }
}

public sealed class GenStep_DeepRimVerticalUndergroundLayout : GenStep
{
    public override int SeedPart => 421503301;

    public override void Generate(Map map, GenStepParams parms)
    {
        UndergroundGenerationService.GenerateLayout(map);
    }
}

public sealed class GenStep_DeepRimVerticalUndergroundMaterialize : GenStep
{
    public override int SeedPart => 421503302;

    public override void Generate(Map map, GenStepParams parms)
    {
        UndergroundGenerationService.Materialize(map);
    }
}

public sealed class GenStep_DeepRimVerticalUndergroundFog : GenStep
{
    public override int SeedPart => 421503303;

    public override void Generate(Map map, GenStepParams parms)
    {
        UndergroundGenerationService.InitializeFog(map);
    }
}
