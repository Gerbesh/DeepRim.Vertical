using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalSupports;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class UpperFloorGenerationService
{
    private static readonly HashSet<int> PendingMaps = new();

    public static void PrepareForGeneration(Map map, Map sourceMap, int levelIndex)
    {
        if (map == null || sourceMap == null || levelIndex <= 0)
        {
            return;
        }

        PendingMaps.Add(map.uniqueID);
    }

    public static void InitializeEmptyConstructionLayer(Map map)
    {
        if (map == null || !PendingMaps.Contains(map.uniqueID))
        {
            return;
        }

        map.regionAndRoomUpdater.Enabled = false;
        try
        {
            using (map.pathing.DisableIncrementalScope())
            {
                foreach (var thing in map.listerThings.AllThings.ToList())
                {
                    thing.Destroy(DestroyMode.Vanish);
                }

                foreach (var cell in map.AllCells)
                {
                    map.roofGrid.SetRoof(cell, null);
                    map.snowGrid.SetDepth(cell, 0f);
                }
            }
        }
        finally
        {
            map.regionAndRoomUpdater.Enabled = true;
        }

        map.fogGrid.ClearAllFog();
        var voidTerrain = VerticalDefOf.DeepRimVertical_UpperVoid ?? TerrainDefOf.Concrete;
        foreach (var cell in map.AllCells)
        {
            map.terrainGrid.SetTerrain(cell, voidTerrain);
        }

        MapGenerator.PlayerStartSpot = map.Center;
        PendingMaps.Remove(map.uniqueID);
    }
}

public sealed class GenStep_DeepRimVerticalUpperFloorInit : GenStep
{
    public override int SeedPart => 421503401;

    public override void Generate(Map map, GenStepParams parms)
    {
        UpperFloorGenerationService.InitializeEmptyConstructionLayer(map);
    }
}
