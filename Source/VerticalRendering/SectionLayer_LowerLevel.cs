using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

[StaticConstructorOnStartup]
public sealed class SectionLayer_LowerLevel : SectionLayer
{
    private static readonly CachedTexture PollutedSnowTex = new("Other/SnowPolluted");
    private static readonly CachedTexture PollutedSandTex = new("Other/WindsweptSandPolluted");

    private readonly CellTerrain[] adjTerrains = new CellTerrain[8];
    private readonly HashSet<CellTerrain> adjDifferentTerrainCells = [];
    private readonly bool[] adjTransit = new bool[8];

    private Map sourceMap;
    private int sourceLevel;
    private int distanceFromActive;
    private float alpha;
    private UpperOverlayVisibilityMask overlayMask;
    private bool overlayEnabled;

    public SectionLayer_LowerLevel(Section section)
        : base(section)
    {
    }

    public void Prepare(Map sourceMap, int sourceLevel, UpperLevelTerrainGrid terrainGrid, int distanceFromActive, float alpha, UpperOverlayVisibilityMask overlayMask, bool overlayEnabled)
    {
        _ = terrainGrid;
        this.sourceMap = sourceMap;
        this.sourceLevel = sourceLevel;
        this.distanceFromActive = distanceFromActive;
        this.alpha = alpha;
        this.overlayMask = overlayMask;
        this.overlayEnabled = overlayEnabled;
    }

    public override void Regenerate()
    {
        if (Map == null || sourceMap == null || sourceLevel >= VerticalRenderContextService.GetLevel(Map))
        {
            return;
        }

        ClearSubMeshes(MeshParts.All);
        var terrainGrid = sourceMap.terrainGrid;
        var cellRect = section.CellRect;
        var snowSubMesh = SetupSnowSubMesh();
        var showSnowSubMesh = false;
        var sandSubMesh = SetupSandSubMesh();
        var showSandSubMesh = false;
        var blurSubMesh = SetupBlurSubMesh();
        blurSubMesh.SetBlurringSubMesh(true);
        var showBlurSubMesh = false;
        var shouldBlur = false;

        foreach (var cell in EnumerateSection(cellRect))
        {
            if (!ShouldRevealLowerLevelAt(cell))
            {
                SetupColoredSubMeshes(
                    transparent: true,
                    null,
                    cell,
                    ref showSnowSubMesh,
                    snowSubMesh,
                    ref showSandSubMesh,
                    sandSubMesh,
                    shouldBlur,
                    blurTransparent: true,
                    blurSubMesh,
                    ref showBlurSubMesh);
                continue;
            }

            var sample = LowerLevelCellSampler.Sample(sourceMap, cell);
            if (!sample.HasRenderableTerrain)
            {
                VerticalSectionLayerUtility.GenerateColorMeshes(snowSubMesh, transparent: true);
                if (ModsConfig.OdysseyActive)
                {
                    VerticalSectionLayerUtility.GenerateColorMeshes(sandSubMesh, transparent: true);
                }

                VerticalSectionLayerUtility.GenerateColorMeshes(blurSubMesh, transparent: true);
                continue;
            }

            var map = sample.SourceMap;
            var terrain = sample.Terrain;
            var roofBlocked = map.roofGrid.RoofAt(cell) == RoofDefOf.RoofConstructed;
            if (!roofBlocked && terrain != null)
            {
                var cellTerrain = new CellTerrain
                {
                    def = terrain,
                    polluted = GridsUtility.IsPolluted(cell, map),
                    snowCoverage = map.snowGrid.GetDepth(cell),
                    sandCoverage = GridsUtility.GetSandDepth(cell, map),
                    color = map.terrainGrid.ColorAt(cell)
                };
                GenerateTerrainMainMeshes(map, cell, cellTerrain);
                RegenerateTerrainTransSubMeshes(map, cell, cellTerrain);
                PrintVisibleLowerThings(map, cell);
            }
            else
            {
                SetupColoredSubMeshes(
                    transparent: true,
                    null,
                    cell,
                    ref showSnowSubMesh,
                    snowSubMesh,
                    ref showSandSubMesh,
                    sandSubMesh,
                    shouldBlur,
                    blurTransparent: true,
                    blurSubMesh,
                    ref showBlurSubMesh);
                continue;
            }

            SetupColoredSubMeshes(
                transparent: roofBlocked,
                map,
                cell,
                ref showSnowSubMesh,
                snowSubMesh,
                ref showSandSubMesh,
                sandSubMesh,
                shouldBlur,
                roofBlocked,
                blurSubMesh,
                ref showBlurSubMesh);
        }

        FinalizeMesh(MeshParts.All);
        FinalizeSubMesh(showSnowSubMesh, snowSubMesh);
        FinalizeSubMesh(showSandSubMesh, sandSubMesh);
        FinalizeSubMesh(showBlurSubMesh, blurSubMesh);
    }

    public override void DrawLayer()
    {
        if (!Visible)
        {
            return;
        }

        var count = subMeshes.Count;
        for (var i = 0; i < count; i++)
        {
            var subMesh = subMeshes[i];
            if (subMesh.finalized && !subMesh.disabled)
            {
                Graphics.DrawMesh(subMesh.mesh, Matrix4x4.identity, subMesh.material, subMesh.UseWaterDepthShader() ? SubcameraDefOf.WaterDepth.LayerId : 0);
            }
        }
    }

    public void DrawLowerLevel(CellRect viewRect)
    {
        _ = viewRect;
        VerticalSectionLayerUtility.SetTintMaterialColor(distanceFromActive, alpha);
        DrawLayer();
    }

    private static bool ShouldRenderLowerLevel(TerrainDef terrain)
    {
        return terrain == VerticalWorld.VerticalDefOf.DeepRimVertical_UpperVoid;
    }

    private bool ShouldRevealLowerLevelAt(IntVec3 cell)
    {
        if (!cell.InBounds(Map) || overlayMask == null)
        {
            return false;
        }

        return overlayMask.GetCellState(cell, overlayEnabled).ShowLower;
    }

    private static IEnumerable<IntVec3> EnumerateSection(CellRect cellRect)
    {
        for (var x = cellRect.minX; x <= cellRect.maxX; x++)
        {
            for (var z = cellRect.minZ; z <= cellRect.maxZ; z++)
            {
                yield return new IntVec3(x, 0, z);
            }
        }
    }

    private LayerSubMesh GetColorSubMesh(string name, Material mat, Texture2D pollutedTex = null)
    {
        var mesh = new Mesh();
        if (UnityData.isEditor)
        {
            mesh.name = $"SectionLayerSubMesh_{GetType().Name}_{name}_{Map.Tile}";
        }

        var subMesh = new LayerSubMesh(mesh, mat, null);
        if (ModsConfig.BiotechActive && pollutedTex != null)
        {
            subMesh.material.SetTexture(ShaderPropertyIDs.PollutedTex, pollutedTex);
        }

        if (subMesh.mesh.vertexCount == 0)
        {
            SectionLayerGeometryMaker_Solid.MakeBaseGeometry(section, subMesh, pollutedTex == null ? AltitudeLayer.Terrain : AltitudeLayer.MetaOverlays);
        }

        subMesh.Clear(MeshParts.Colors);
        return subMesh;
    }

    private LayerSubMesh SetupSnowSubMesh()
    {
        return GetColorSubMesh("Snow", MatBases.Snow, PollutedSnowTex.Texture);
    }

    private LayerSubMesh SetupSandSubMesh()
    {
        var subMesh = GetColorSubMesh("Sand", MatBases.Sand, PollutedSandTex.Texture);
        if (!ModsConfig.OdysseyActive)
        {
            subMesh.disabled = true;
        }

        return subMesh;
    }

    private LayerSubMesh SetupBlurSubMesh()
    {
        return GetColorSubMesh("Blur", VerticalSectionLayerUtility.TintMaterial);
    }

    private static void SetupColoredSubMeshes(bool transparent, Map curMap, IntVec3 cell, ref bool showSnowSubMesh, LayerSubMesh snowSubMesh, ref bool showSandSubMesh, LayerSubMesh sandSubMesh, bool shouldBlur, bool blurTransparent, LayerSubMesh blurSubMesh, ref bool showBlurSubMesh)
    {
        if (!transparent && curMap != null)
        {
            VerticalSectionLayerUtility.GenerateColorMeshes(curMap, cell, c => GridsUtility.GetSnowDepth(c, curMap), snowSubMesh, ref showSnowSubMesh);
            if (ModsConfig.OdysseyActive)
            {
                VerticalSectionLayerUtility.GenerateColorMeshes(curMap, cell, c => GridsUtility.GetSandDepth(c, curMap), sandSubMesh, ref showSandSubMesh);
            }
        }
        else
        {
            VerticalSectionLayerUtility.GenerateColorMeshes(snowSubMesh, transparent: true);
            if (ModsConfig.OdysseyActive)
            {
                VerticalSectionLayerUtility.GenerateColorMeshes(sandSubMesh, transparent: true);
            }
        }

        if (shouldBlur)
        {
            VerticalSectionLayerUtility.GenerateColorMeshes(blurSubMesh, blurTransparent);
            if (!blurTransparent)
            {
                showBlurSubMesh = true;
            }
        }
    }

    private void FinalizeSubMesh(bool showSubMesh, LayerSubMesh layerSubMesh)
    {
        if (showSubMesh)
        {
            layerSubMesh.disabled = false;
            layerSubMesh.FinalizeMesh(MeshParts.Colors);
            subMeshes.Add(layerSubMesh);
        }
    }

    private void GenerateTerrainMainMeshes(Map map, IntVec3 cell, CellTerrain cellTerrain)
    {
        var subMesh = GetSubMesh(cellTerrain.def.dontRender ? MatBases.ShadowMask : GetMaterialFor(cellTerrain, map));
        if (subMesh != null)
        {
            VerticalSectionLayerUtility.FillTerrainMainLayerSubMesh(subMesh, cell);
        }

        if (IsWaterTerrain(cellTerrain) && cellTerrain.def.waterDepthMaterial != null)
        {
            var waterSubMesh = GetSubMesh(cellTerrain.def.waterDepthMaterial);
            if (waterSubMesh != null)
            {
                waterSubMesh.SetUseWaterDepthShader(true);
                VerticalSectionLayerUtility.FillTerrainMainLayerSubMesh(waterSubMesh, cell);
            }
        }
    }

    private void RegenerateTerrainTransSubMeshes(Map map, IntVec3 cell, CellTerrain cellTerrain)
    {
        for (var i = 0; i < 8; i++)
        {
            var adjCell = cell + GenAdj.AdjacentCellsAroundBottom[i];
            if (!adjCell.InBounds(map))
            {
                adjTerrains[i] = cellTerrain;
                continue;
            }

            var adjTerrain = new CellTerrain
            {
                def = map.terrainGrid.TerrainAt(adjCell),
                polluted = GridsUtility.IsPolluted(adjCell, map),
                snowCoverage = GridsUtility.GetSnowDepth(adjCell, map),
                sandCoverage = GridsUtility.GetSandDepth(adjCell, map),
                color = map.terrainGrid.ColorAt(adjCell)
            };

            var edifice = GridsUtility.GetEdifice(adjCell, map);
            if (edifice != null && edifice.def.coversFloor)
            {
                adjTerrain.def = TerrainDefOf.Underwall;
            }

            adjTerrains[i] = adjTerrain;
            if (!adjTerrain.Equals(cellTerrain)
                && (int)adjTerrain.def.edgeType != 0
                && map.terrainGrid.FoundationAt(cell) == null
                && map.terrainGrid.FoundationAt(adjCell) == null
                && adjTerrain.def.renderPrecedence >= cellTerrain.def.renderPrecedence)
            {
                adjDifferentTerrainCells.Add(adjTerrain);
            }
        }

        foreach (var adjTerrain in adjDifferentTerrainCells)
        {
            VerticalSectionLayerUtility.FillAdjTransLayerSubMesh(GetSubMesh(GetMaterialFor(adjTerrain, map)), cell, adjTransit, adjTerrains, adjTerrain);
            if (IsWaterTerrain(adjTerrain) && adjTerrain.def.waterDepthMaterial != null)
            {
                var waterSubMesh = GetSubMesh(adjTerrain.def.waterDepthMaterial);
                waterSubMesh.SetUseWaterDepthShader(true);
                VerticalSectionLayerUtility.FillAdjTransLayerSubMesh(waterSubMesh, cell, adjTransit, adjTerrains, adjTerrain);
            }
        }

        adjDifferentTerrainCells.Clear();
    }

    private void PrintVisibleLowerThings(Map map, IntVec3 cell)
    {
        var things = map.thingGrid.ThingsListAt(cell);
        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            if (!ShouldPrintLowerThing(thing))
            {
                continue;
            }

            if (!VerticalSectionLayerUtility.ShouldPrintThing(map, cell, thing))
            {
                continue;
            }

            var hideBySnow = VerticalSectionLayerUtility.HideBySnowOrSand(map, thing);
            var hideFrozenPlant = VerticalSectionLayerUtility.PlantNotShowInFrozenWater(map, thing);
            if (!VerticalSectionLayerUtility.NotInAnyStorage(thing) || hideBySnow || hideFrozenPlant)
            {
                continue;
            }

            try
            {
                thing.Print(this);
            }
            catch (Exception ex)
            {
                Log.Error($"Exception printing {thing} at {thing.Position}: {ex}");
            }
        }
    }

    private static bool ShouldPrintLowerThing(Thing thing)
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

        if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant)
        {
            return true;
        }

        return thing.def.category == ThingCategory.Item && thing.def.drawerType is not DrawerType.None;
    }

    private static Material GetMaterialFor(CellTerrain cellTerrain, Map map)
    {
        var polluted = cellTerrain.polluted
                        && cellTerrain.snowCoverage < 0.4f
                        && cellTerrain.sandCoverage < 0.4f
                        && cellTerrain.def.graphicPolluted != BaseContent.BadGraphic;
        return map.terrainGrid.GetMaterial(cellTerrain.def, polluted, cellTerrain.color);
    }

    private static bool IsWaterTerrain(CellTerrain terrain)
    {
        return terrain.def.IsWater || terrain.def.IsRiver || terrain.def.IsOcean;
    }
}
