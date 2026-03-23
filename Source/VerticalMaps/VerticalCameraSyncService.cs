using DeepRim.Vertical.VerticalRendering;
using DeepRim.Vertical.VerticalMaps;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class VerticalCameraSyncService
{
    private static readonly FieldInfo RootPosField = AccessTools.Field(typeof(CameraDriver), "rootPos");
    private static readonly FieldInfo RootSizeField = AccessTools.Field(typeof(CameraDriver), "rootSize");
    private static readonly FieldInfo DesiredSizeField = AccessTools.Field(typeof(CameraDriver), "desiredSize");
    private static readonly FieldInfo DesiredDollyField = AccessTools.Field(typeof(CameraDriver), "desiredDolly");
    private static readonly FieldInfo DesiredDollyRawField = AccessTools.Field(typeof(CameraDriver), "desiredDollyRaw");
    private static readonly MethodInfo ApplyPositionToGameObjectMethod = AccessTools.Method(typeof(CameraDriver), "ApplyPositionToGameObject");

    public static void JumpPreservingView(Map sourceMap, Map targetMap, IntVec3 fallbackCell)
    {
        if (targetMap == null)
        {
            return;
        }

        VerticalOverlayDebugService.Log(
            $"JumpPreservingView sourceMap={(sourceMap == null ? "null" : sourceMap.uniqueID.ToString())} " +
            $"targetMap={targetMap.uniqueID} sourceLevel={VerticalRenderContextService.GetLevel(sourceMap)} targetLevel={VerticalRenderContextService.GetLevel(targetMap)} " +
            $"sourceParent={sourceMap?.Parent?.GetUniqueLoadID() ?? "null"} targetParent={targetMap.Parent?.GetUniqueLoadID() ?? "null"} fallback={fallbackCell}.");

        if (sourceMap == null || Find.CameraDriver == null || !VerticalRenderContextService.SharesSite(sourceMap, targetMap))
        {
            CameraJumper.TryJump(fallbackCell, targetMap, CameraJumper.MovementMode.Cut);
            VerticalOverlayDebugService.Log($"JumpPreservingView direct jump to map {targetMap.uniqueID}.");
            return;
        }

        var driver = Find.CameraDriver;
        var view = Capture(driver, targetMap);
        var jumpCell = view.MapCell.InBounds(targetMap) ? view.MapCell : ClampToMap(targetMap, fallbackCell);

        CameraJumper.TryJump(jumpCell, targetMap, CameraJumper.MovementMode.Cut);
        Restore(driver, targetMap, view);
        VerticalMapInvalidationService.MarkSiteDirty(targetMap);
        VerticalOverlayDebugService.Log(
            $"JumpPreservingView completed currentMap={(Find.CurrentMap == null ? "null" : Find.CurrentMap.uniqueID.ToString())} jumpCell={jumpCell}.");
    }

    private static CameraViewState Capture(CameraDriver driver, Map targetMap)
    {
        var realPosition = RootPosField != null
            ? (Vector3)RootPosField.GetValue(driver)
            : Vector3.zero;
        var clampedPosition = ClampToMap(targetMap, realPosition);
        var rootSize = RootSizeField != null
            ? (float)RootSizeField.GetValue(driver)
            : 24f;
        return new CameraViewState(clampedPosition, rootSize, new IntVec3(Mathf.RoundToInt(clampedPosition.x), 0, Mathf.RoundToInt(clampedPosition.z)));
    }

    private static void Restore(CameraDriver driver, Map targetMap, CameraViewState view)
    {
        var clampedPosition = ClampToMap(targetMap, view.Position);
        RootPosField?.SetValue(driver, clampedPosition);
        DesiredDollyField?.SetValue(driver, Vector2.zero);
        DesiredDollyRawField?.SetValue(driver, Vector2.zero);
        DesiredSizeField?.SetValue(driver, view.RootSize);
        RootSizeField?.SetValue(driver, view.RootSize);
        ApplyPositionToGameObjectMethod?.Invoke(driver, null);
    }

    private static IntVec3 ClampToMap(Map map, IntVec3 cell)
    {
        if (map == null)
        {
            return cell;
        }

        return new IntVec3(
            Mathf.Clamp(cell.x, 0, map.Size.x - 1),
            0,
            Mathf.Clamp(cell.z, 0, map.Size.z - 1));
    }

    private static Vector3 ClampToMap(Map map, Vector3 position)
    {
        if (map == null)
        {
            return position;
        }

        return new Vector3(
            Mathf.Clamp(position.x, 0f, map.Size.x),
            position.y,
            Mathf.Clamp(position.z, 0f, map.Size.z));
    }

    private readonly struct CameraViewState
    {
        public CameraViewState(Vector3 position, float rootSize, IntVec3 mapCell)
        {
            Position = position;
            RootSize = rootSize;
            MapCell = mapCell;
        }

        public Vector3 Position { get; }

        public float RootSize { get; }

        public IntVec3 MapCell { get; }
    }
}
