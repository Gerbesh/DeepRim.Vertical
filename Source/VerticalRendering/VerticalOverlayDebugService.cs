using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public static class VerticalOverlayDebugService
{
    private static readonly HashSet<string> OnceKeys = [];
    private static readonly Material CursorShowLowerMaterial = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.2f, 0.9f, 0.35f, 0.22f), true);
    private static readonly Material CursorNeutralBaseMaterial = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.92f, 0.78f, 0.2f, 0.22f), true);
    private static readonly Material CursorBlockedMaterial = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.95f, 0.25f, 0.25f, 0.22f), true);

    public static void Log(string message)
    {
        if (VerticalRuntime.Settings?.enableUpperOverlayDebugLogging != true)
        {
            return;
        }

        Verse.Log.Message($"[DeepRim Vertical][UpperOverlay] {message}");
    }

    public static void LogOnce(string key, string message)
    {
        if (VerticalRuntime.Settings?.enableUpperOverlayDebugLogging != true || !OnceKeys.Add(key))
        {
            return;
        }

        Verse.Log.Message($"[DeepRim Vertical][UpperOverlay] {message}");
    }

    public static void LogVerbose(string message)
    {
        if (VerticalRuntime.Settings?.enableUpperOverlayDebugLogging == true
            && VerticalRuntime.Settings.enableUpperOverlayDebugVerbose)
        {
            Verse.Log.Message($"[DeepRim Vertical][UpperOverlay] {message}");
        }
    }

    public static void DrawCursorCell(Map map, IntVec3 cell, UpperOverlayCellState cellState)
    {
        if (VerticalRuntime.Settings?.enableUpperOverlayDebugOverlay != true || map == null || !cell.InBounds(map))
        {
            return;
        }

        CellRenderer.RenderCell(cell, cellState.ShowLower
            ? CursorShowLowerMaterial
            : cellState.ShowNeutralBase
                ? CursorNeutralBaseMaterial
                : CursorBlockedMaterial);
    }

    public static string DescribeLowerSource(UpperOverlayCellState cellState, LowerLevelCellSample sample)
    {
        if (!cellState.ShowLower)
        {
            return "none";
        }

        return sample.UsedRecursiveVoidChain
            ? "recursive-void-chain"
            : sample.SourceMap != null
                ? "nearest-lower"
                : "none";
    }
}
