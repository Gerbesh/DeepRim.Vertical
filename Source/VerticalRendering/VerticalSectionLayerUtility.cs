using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

[StaticConstructorOnStartup]
public static class VerticalSectionLayerUtility
{
    private const float MinOpacity = 0.075f;
    private const float OpacityPerLevel = 0.075f;
    private const float MaxOpacity = 0.3f;

    private static readonly Color32 ColorWhite = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    private static readonly Color32 ColorClear = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0);
    private static readonly List<List<int>> VertexWeights =
    [
        [0, 1, 2, 8],
        [2, 8],
        [2, 3, 4, 8],
        [4, 8],
        [4, 5, 6, 8],
        [6, 8],
        [6, 7, 0, 8],
        [0, 8],
        [8]
    ];

    private static readonly float[] AdjDepth = new float[9];
    private static readonly List<float> AdjOpacityTemp = [];
    private static readonly Dictionary<LayerSubMesh, bool> BlurSubMeshFlags = new();
    private static readonly Dictionary<LayerSubMesh, bool> WaterDepthShaderFlags = new();

    public static readonly Material TintMaterial = BuildTintMaterial();
    public static readonly Material UpperVoidNeutralMaterial = BuildSolidMaterial(new Color(0.56f, 0.56f, 0.56f, 1f), 2998);
    public static readonly Material UpperVoidTransparentMaterial = BuildSolidMaterial(new Color(1f, 1f, 1f, 0f), 2998);
    public static readonly Mesh SectionMesh = MeshPool.GridPlane(new Vector2(17f, 17f));
    public static readonly Material RoofMaterial = GraphicDatabase.Get<Graphic_Single>("Things/Mote/TempRoof", ShaderDatabase.CutoutComplex).MatSingle;

    public static void ToSection(this IntVec3 cell, out int x, out int y)
    {
        x = cell.x / 17;
        y = cell.z / 17;
    }

    public static Vector3 GetCenter(this Section section)
    {
        return section.botLeft.ToVector3() + new IntVec3(17, 0, 17).ToVector3() / 2f;
    }

    public static void SetTintMaterialColor(int levelDiff, float alphaMultiplier = 1f)
    {
        var alpha = Mathf.Clamp(OpacityPerLevel * levelDiff, MinOpacity, MaxOpacity) * alphaMultiplier;
        TintMaterial.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
    }

    public static bool IsBlurringSubMesh(this LayerSubMesh subMesh)
    {
        return subMesh != null && BlurSubMeshFlags.TryGetValue(subMesh, out var value) && value;
    }

    public static void SetBlurringSubMesh(this LayerSubMesh subMesh, bool value)
    {
        if (subMesh != null)
        {
            BlurSubMeshFlags[subMesh] = value;
        }
    }

    public static bool UseWaterDepthShader(this LayerSubMesh subMesh)
    {
        return subMesh != null && WaterDepthShaderFlags.TryGetValue(subMesh, out var value) && value;
    }

    public static void SetUseWaterDepthShader(this LayerSubMesh subMesh, bool value)
    {
        if (subMesh != null)
        {
            WaterDepthShaderFlags[subMesh] = value;
        }
    }

    public static void FillTerrainMainLayerSubMesh(LayerSubMesh mainMesh, IntVec3 cell)
    {
        var y = Altitudes.AltitudeFor(AltitudeLayer.Terrain);
        var count = mainMesh.verts.Count;
        mainMesh.verts.Add(new Vector3(cell.x, y, cell.z));
        mainMesh.verts.Add(new Vector3(cell.x, y, cell.z + 1));
        mainMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z + 1));
        mainMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z));
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.tris.Add(count);
        mainMesh.tris.Add(count + 1);
        mainMesh.tris.Add(count + 2);
        mainMesh.tris.Add(count);
        mainMesh.tris.Add(count + 2);
        mainMesh.tris.Add(count + 3);
    }

    public static void FillRoofPlaneSubMesh(LayerSubMesh mainMesh, IntVec3 cell)
    {
        var y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
        var count = mainMesh.verts.Count;
        mainMesh.verts.Add(new Vector3(cell.x, y, cell.z));
        mainMesh.verts.Add(new Vector3(cell.x, y, cell.z + 1));
        mainMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z + 1));
        mainMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z));
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.colors.Add(ColorWhite);
        mainMesh.tris.Add(count);
        mainMesh.tris.Add(count + 1);
        mainMesh.tris.Add(count + 2);
        mainMesh.tris.Add(count);
        mainMesh.tris.Add(count + 2);
        mainMesh.tris.Add(count + 3);
    }

    public static void FillAdjTransLayerSubMesh(LayerSubMesh transSubMesh, IntVec3 cell, bool[] adjTransit, CellTerrain[] adjTerrains, CellTerrain adjDiffTerrain)
    {
        if (transSubMesh == null)
        {
            return;
        }

        var count = transSubMesh.verts.Count;
        transSubMesh.verts.Add(new Vector3(cell.x + 0.5f, 0f, cell.z));
        transSubMesh.verts.Add(new Vector3(cell.x, 0f, cell.z));
        transSubMesh.verts.Add(new Vector3(cell.x, 0f, cell.z + 0.5f));
        transSubMesh.verts.Add(new Vector3(cell.x, 0f, cell.z + 1));
        transSubMesh.verts.Add(new Vector3(cell.x + 0.5f, 0f, cell.z + 1));
        transSubMesh.verts.Add(new Vector3(cell.x + 1, 0f, cell.z + 1));
        transSubMesh.verts.Add(new Vector3(cell.x + 1, 0f, cell.z + 0.5f));
        transSubMesh.verts.Add(new Vector3(cell.x + 1, 0f, cell.z));
        transSubMesh.verts.Add(new Vector3(cell.x + 0.5f, 0f, cell.z + 0.5f));

        for (var i = 0; i < 8; i++)
        {
            adjTransit[i] = false;
        }

        for (var i = 0; i < 8; i++)
        {
            if (i % 2 == 0)
            {
                if (adjTerrains[i].Equals(adjDiffTerrain))
                {
                    adjTransit[(i - 1 + 8) % 8] = true;
                    adjTransit[i] = true;
                    adjTransit[(i + 1) % 8] = true;
                }
            }
            else if (adjTerrains[i].Equals(adjDiffTerrain))
            {
                adjTransit[i] = true;
            }
        }

        for (var i = 0; i < 8; i++)
        {
            transSubMesh.colors.Add(adjTransit[i] ? ColorWhite : ColorClear);
        }

        transSubMesh.colors.Add(ColorClear);
        for (var i = 0; i < 8; i++)
        {
            transSubMesh.tris.Add(count + i);
            transSubMesh.tris.Add(count + (i + 1) % 8);
            transSubMesh.tris.Add(count + 8);
        }
    }

    public static void GenerateColorMeshes(LayerSubMesh subMesh, bool transparent = false)
    {
        for (var i = 0; i < 9; i++)
        {
            subMesh.colors.Add(new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, Convert.ToByte((transparent ? 0f : 1f) * 255f)));
        }
    }

    public static void GenerateColorMeshes(Map map, IntVec3 cell, Func<IntVec3, float> depthGetter, LayerSubMesh subMesh, ref bool showSubMesh)
    {
        AdjOpacityTemp.Clear();
        var depth = depthGetter(cell);
        for (var i = 0; i < 9; i++)
        {
            var adjCell = cell + GenAdj.AdjacentCellsAndInsideForUV[i];
            AdjDepth[i] = adjCell.InBounds(map) ? depthGetter(adjCell) : depth;
        }

        for (var i = 0; i < 9; i++)
        {
            var avgDepth = 0f;
            for (var j = 0; j < VertexWeights[i].Count; j++)
            {
                avgDepth += AdjDepth[VertexWeights[i][j]];
            }

            avgDepth /= VertexWeights[i].Count;
            if (avgDepth > 0.01f)
            {
                showSubMesh = true;
            }

            AdjOpacityTemp.Add(avgDepth);
        }

        for (var i = 0; i < 9; i++)
        {
            var adjCell = cell + GenAdj.AdjacentCellsAndInsideForUV[i];
            AdjDepth[i] = GridsUtility.IsPolluted(adjCell, map) ? 1f : 0f;
        }

        for (var i = 0; i < 9; i++)
        {
            var polluted = 0f;
            for (var j = 0; j < VertexWeights[i].Count; j++)
            {
                polluted += AdjDepth[VertexWeights[i][j]];
            }

            polluted /= VertexWeights[i].Count;
            var opacity = AdjOpacityTemp[i];
            subMesh.colors.Add(new Color32(Convert.ToByte(polluted * 255f), byte.MaxValue, byte.MaxValue, Convert.ToByte(opacity * 255f)));
        }
    }

    public static bool ShouldPrintThing(Map map, IntVec3 cell, Thing thing)
    {
        if (thing.Position.x == cell.x
            && thing.Position.z == cell.z
            && thing.def.drawerType is not DrawerType.None)
        {
            return thing.def.seeThroughFog || !map.fogGrid.IsFogged(thing.Position);
        }

        return false;
    }

    public static bool NotInAnyStorage(Thing thing)
    {
        var storingThing = StoreUtility.StoringThing(thing);
        return storingThing == null || thing == storingThing;
    }

    public static bool PlantNotShowInFrozenWater(Map map, Thing thing)
    {
        var plant = thing.def.plant;
        return plant != null && !plant.showInFrozenWater && GridsUtility.GetTerrain(thing.Position, map) == TerrainDefOf.ThinIce;
    }

    public static bool HideBySnowOrSand(Map map, Thing thing)
    {
        return thing.def.hideAtSnowOrSandDepth <= 1f
               && Math.Max(GridsUtility.GetSnowDepth(thing.Position, map), GridsUtility.GetSandDepth(thing.Position, map)) > thing.def.hideAtSnowOrSandDepth;
    }

    private static Material BuildTintMaterial()
    {
        return BuildSolidMaterial(Color.white, 3150);
    }

    private static Material BuildSolidMaterial(Color color, int renderQueue)
    {
        var material = SolidColorMaterials.NewSolidColorMaterial(color, ShaderDatabase.Transparent);
        material.renderQueue = renderQueue;
        return material;
    }
}
