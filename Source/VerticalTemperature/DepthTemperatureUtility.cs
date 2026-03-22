using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalTemperature;

public static class DepthTemperatureUtility
{
    public static float EvaluateGeologicalTarget(int levelIndex)
    {
        if (!VerticalRuntime.Settings.enableDepthThermalProfile)
        {
            return 0f;
        }

        var depth = levelIndex >= 0 ? 0 : -levelIndex;
        if (depth <= 0)
        {
            return 0f;
        }

        var settings = VerticalRuntime.Settings;
        var coldDepth = -settings.coldDepthLevel;
        var hotDepth = -settings.hotDepthLevel;
        if (depth <= coldDepth)
        {
            return Lerp(0f, settings.coldDepthTemp, depth / (float)coldDepth);
        }

        if (depth <= hotDepth)
        {
            return Lerp(settings.coldDepthTemp, settings.hotDepthTemp, (depth - coldDepth) / (float)(hotDepth - coldDepth));
        }

        return settings.hotDepthTemp;
    }

    public static bool TryGetGeologicalTarget(Map map, out int levelIndex, out float target)
    {
        levelIndex = 0;
        target = 0f;
        if (map == null)
        {
            return false;
        }

        if (VerticalSiteWorldComponent.TryGet(out var component) && component.TryGetFloor(map, out _, out var floor))
        {
            levelIndex = floor.levelIndex;
        }
        else if (map.Parent is VerticalWorld.VerticalSiteMapParent mapParent)
        {
            levelIndex = mapParent.LevelIndex;
        }
        else
        {
            return false;
        }

        if (levelIndex >= 0)
        {
            return false;
        }

        target = EvaluateGeologicalTarget(levelIndex);
        return true;
    }

    public static float ApplyGeologicalPressure(Map map, IntVec3 cell, float currentTemperature)
    {
        if (!TryGetGeologicalTarget(map, out _, out var target))
        {
            return currentTemperature;
        }

        var roof = map.roofGrid.RoofAt(cell);
        float influence;
        if (roof == RoofDefOf.RoofRockThick)
        {
            influence = 0.92f;
        }
        else if (roof != null)
        {
            influence = 0.75f;
        }
        else
        {
            influence = 0.25f;
        }

        return Mathf.Lerp(currentTemperature, target, influence);
    }

    private static float Lerp(float min, float max, float t)
    {
        if (t < 0f)
        {
            t = 0f;
        }
        else if (t > 1f)
        {
            t = 1f;
        }

        return min + (max - min) * t;
    }
}
