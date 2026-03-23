using System.Collections.Generic;
using DeepRim.Vertical.VerticalRendering;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalOverlay;

public static class VerticalGhostRenderer
{
    public static void MarkDirty(Map map)
    {
        _ = map;
    }

    public static void DrawFor(Map map)
    {
        if (!ShouldDrawFor(map))
        {
            return;
        }

        var viewRect = Find.CameraDriver.CurrentViewRect;
        var mode = (VerticalOverlayMode)VerticalRuntime.Settings.upperFloorOverlayMode;

        if (mode == VerticalOverlayMode.Full)
        {
            if (!VerticalRenderContextService.TryGetContext(map, out var context))
            {
                return;
            }

            foreach (var floor in context.LowerFloors)
            {
                foreach (var pawn in floor.Floor.Map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn != null
                        && pawn.Spawned
                        && viewRect.Contains(pawn.Position)
                        && VerticalPawnVisibilityService.CanSeePawn(map, pawn))
                    {
                        VerticalOverlayDebugService.LogVerbose(
                            $"Rendering lower pawn {pawn.LabelShortCap} from level {VerticalRenderContextService.GetLevel(pawn.Map)} " +
                            $"into level {VerticalRenderContextService.GetLevel(map)} at {pawn.Position}.");
                        DrawPawnGhost(pawn, floor.DistanceFromActive, floor.Alpha);
                    }
                }
            }
        }

        if (mode == VerticalOverlayMode.Supports)
        {
            DrawBuildableMask(map, viewRect);
        }
    }

    private static bool ShouldDrawFor(Map map)
    {
        return map != null
               && Find.CurrentMap == map
               && Current.ProgramState == ProgramState.Playing
               && VerticalRuntime.Settings.enableUpperFloorOverlay
               && VerticalRenderContextService.TryGetContext(map, out var context)
               && context.CurrentFloor.levelIndex > 0;
    }

    private static void DrawBuildableMask(Map map, CellRect viewRect)
    {
        var material = LowerLevelBlurUtility.GetOverlayMaterial(new Color(0.35f, 0.9f, 0.95f, 0.028f));
        foreach (var cell in VerticalSupports.VerticalSupportService.GetBuildableCells(map))
        {
            if (viewRect.Contains(cell))
            {
                CellRenderer.RenderCell(cell, material);
            }
        }
    }

    private static void DrawPawnGhost(Pawn pawn, int distanceFromActive, float alpha)
    {
        if (pawn?.Drawer?.renderer == null)
        {
            return;
        }

        try
        {
            _ = LowerLevelBlurUtility.PawnTintForDepth(distanceFromActive, alpha);
            pawn.Drawer.renderer.RenderPawnAt(pawn.DrawPos.WithY(Altitudes.AltitudeFor(AltitudeLayer.Pawn)));
        }
        catch (System.ArgumentNullException)
        {
        }
    }
}
