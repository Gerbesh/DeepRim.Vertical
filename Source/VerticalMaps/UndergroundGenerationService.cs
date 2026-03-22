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

    public static void Generate(Map map, IntVec3 anchorCell, int levelIndex)
    {
        WipeMapFast(map);

        var primaryRock = ChoosePrimaryRockDef();
        var cellCount = map.cellIndices.NumGridCells;
        var openMask = new bool[cellCount];
        var lavaMask = new bool[cellCount];
        var volcanicMask = new bool[cellCount];
        var resourceMask = new ThingDef[cellCount];

        MarkEntryPocket(map, anchorCell, openMask);

        if (levelIndex >= -2)
        {
            MarkEarlyDepthCaverns(map, anchorCell, levelIndex, openMask);
        }

        MarkLavaRivers(map, levelIndex, openMask, lavaMask, volcanicMask);
        MarkResourceDeposits(map, anchorCell, levelIndex, openMask, lavaMask, resourceMask);
        ApplyFinalState(map, primaryRock, openMask, lavaMask, volcanicMask, resourceMask);
    }

    private static void WipeMapFast(Map map)
    {
        var things = map.listerThings.AllThings.ToList();
        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            if (thing is Pawn)
            {
                continue;
            }

            thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static ThingDef ChoosePrimaryRockDef()
    {
        var candidates = DefDatabase<ThingDef>.AllDefs
            .Where(def => def?.building != null
                          && def.building.isNaturalRock
                          && !def.building.isResourceRock
                          && def.defName != "CollapsedRocks"
                          && !def.defName.StartsWith("Smoothed", StringComparison.Ordinal))
            .ToList();

        return candidates.RandomElement();
    }

    private static void MarkEntryPocket(Map map, IntVec3 anchorCell, bool[] openMask)
    {
        MarkOpen(map, openMask, anchorCell);
    }

    private static void MarkEarlyDepthCaverns(Map map, IntVec3 anchorCell, int levelIndex, bool[] openMask)
    {
        var cavernCount = levelIndex == -1 ? 3 : 2;
        var radius = levelIndex == -1 ? 6 : 4;

        for (var i = 0; i < cavernCount; i++)
        {
            if (!TryFindInteriorSolidCell(map, anchorCell, openMask, out var center, 220))
            {
                continue;
            }

            MarkBlob(map, openMask, center, radius + Rand.RangeInclusive(0, 3));
        }
    }

    private static void MarkResourceDeposits(Map map, IntVec3 anchorCell, int levelIndex, bool[] openMask, bool[] lavaMask, ThingDef[] resourceMask)
    {
        var depositCount = Math.Max(6, 10 + (-levelIndex * 2));
        for (var i = 0; i < depositCount; i++)
        {
            if (!TryFindResourceStart(map, anchorCell, openMask, lavaMask, resourceMask, out var start, 320))
            {
                continue;
            }

            var def = PickResourceDef(levelIndex);
            MarkResourceBlob(map, start, def, Rand.RangeInclusive(4, 12), openMask, lavaMask, resourceMask);
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

    private static void MarkResourceBlob(Map map, IntVec3 start, ThingDef def, int size, bool[] openMask, bool[] lavaMask, ThingDef[] resourceMask)
    {
        var frontier = new Queue<IntVec3>();
        var visited = new HashSet<IntVec3>();
        frontier.Enqueue(start);

        while (frontier.Count > 0 && visited.Count < size)
        {
            var cell = frontier.Dequeue();
            if (!cell.InBounds(map) || !visited.Add(cell))
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (openMask[index] || lavaMask[index])
            {
                continue;
            }

            resourceMask[index] = def;

            foreach (var next in GenAdjFast.AdjacentCellsCardinal(cell))
            {
                if (next.InBounds(map) && Rand.Chance(0.72f))
                {
                    frontier.Enqueue(next);
                }
            }
        }
    }

    private static void MarkLavaRivers(Map map, int levelIndex, bool[] openMask, bool[] lavaMask, bool[] volcanicMask)
    {
        var target = DepthTemperatureUtility.EvaluateGeologicalTarget(levelIndex);
        if (target < VerticalRuntime.Settings.hotDepthTemp - 0.01f)
        {
            return;
        }

        var lavaDeep = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaDeep");
        if (lavaDeep == null)
        {
            return;
        }

        var riverCount = 2;
        for (var i = 0; i < riverCount; i++)
        {
            var z = Rand.RangeInclusive(12, map.Size.z - 12);
            var current = new IntVec3(0, 0, z);
            while (current.x < map.Size.x - 1)
            {
                for (var width = -1; width <= 1; width++)
                {
                    var cell = new IntVec3(current.x, 0, current.z + width);
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    var index = map.cellIndices.CellToIndex(cell);
                    openMask[index] = true;
                    lavaMask[index] = true;
                }

                foreach (var side in GenAdjFast.AdjacentCells8Way(current))
                {
                    if (!side.InBounds(map))
                    {
                        continue;
                    }

                    var index = map.cellIndices.CellToIndex(side);
                    if (!lavaMask[index])
                    {
                        volcanicMask[index] = true;
                    }
                }

                current.x += 1;
                current.z += Rand.RangeInclusive(-1, 1);
                current.z = Mathf.Clamp(current.z, 6, map.Size.z - 7);
            }
        }
    }

    private static void ApplyFinalState(Map map, ThingDef primaryRock, bool[] openMask, bool[] lavaMask, bool[] volcanicMask, ThingDef[] resourceMask)
    {
        var lavaDeep = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaDeep");
        var volcanicRock = DefDatabase<TerrainDef>.GetNamedSilentFail("VolcanicRock");

        foreach (var cell in map.AllCells)
        {
            var index = map.cellIndices.CellToIndex(cell);

            if (lavaMask[index])
            {
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
                if (lavaDeep != null)
                {
                    map.terrainGrid.SetTerrain(cell, lavaDeep);
                }

                continue;
            }

            if (volcanicMask[index] && volcanicRock != null)
            {
                map.terrainGrid.SetTerrain(cell, volcanicRock);
            }

            if (openMask[index])
            {
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
                continue;
            }

            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
            var thingDef = resourceMask[index] ?? primaryRock;
            GenSpawn.Spawn(thingDef, cell, map, WipeMode.Vanish);
        }
    }

    private static bool TryFindInteriorSolidCell(Map map, IntVec3 anchorCell, bool[] openMask, out IntVec3 result, int tries)
    {
        for (var i = 0; i < tries; i++)
        {
            var cell = new IntVec3(Rand.RangeInclusive(8, map.Size.x - 9), 0, Rand.RangeInclusive(8, map.Size.z - 9));
            if (cell.DistanceTo(anchorCell) <= 14f)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (!openMask[index])
            {
                result = cell;
                return true;
            }
        }

        result = IntVec3.Invalid;
        return false;
    }

    private static bool TryFindResourceStart(Map map, IntVec3 anchorCell, bool[] openMask, bool[] lavaMask, ThingDef[] resourceMask, out IntVec3 result, int tries)
    {
        for (var i = 0; i < tries; i++)
        {
            var cell = new IntVec3(Rand.RangeInclusive(4, map.Size.x - 5), 0, Rand.RangeInclusive(4, map.Size.z - 5));
            if (cell.DistanceTo(anchorCell) <= 10f)
            {
                continue;
            }

            var index = map.cellIndices.CellToIndex(cell);
            if (!openMask[index] && !lavaMask[index] && resourceMask[index] == null)
            {
                result = cell;
                return true;
            }
        }

        result = IntVec3.Invalid;
        return false;
    }

    private static void MarkBlob(Map map, bool[] openMask, IntVec3 center, int radius)
    {
        foreach (var cell in CellRect.CenteredOn(center, radius).ClipInsideMap(map).Cells)
        {
            if (cell.DistanceTo(center) <= radius + Rand.Value * 1.5f)
            {
                MarkOpen(map, openMask, cell);
            }
        }
    }

    private static void MarkOpen(Map map, bool[] openMask, IntVec3 cell)
    {
        if (!cell.InBounds(map))
        {
            return;
        }

        openMask[map.cellIndices.CellToIndex(cell)] = true;
    }
}
