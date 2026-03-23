using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public static class LowerLevelBlurUtility
{
    private static readonly Dictionary<int, Material> OverlayMaterialsByArgb = new();
    private static readonly Dictionary<string, Graphic> TintedGraphicCache = new();

    public static float TerrainFadeForDepth(int distanceFromActive, float alpha)
    {
        var distancePenalty = 0.05f * Mathf.Max(0, distanceFromActive - 1);
        return Mathf.Clamp01(alpha * (0.94f - distancePenalty));
    }

    public static Color TerrainHazeForDepth(int distanceFromActive, float alpha)
    {
        var color = Color.Lerp(new Color(0.92f, 0.93f, 0.94f, 1f), new Color(0.84f, 0.86f, 0.88f, 1f), Mathf.Clamp01((distanceFromActive - 1) * 0.28f));
        color.a = Mathf.Clamp01(alpha * (0.025f + 0.02f * distanceFromActive));
        return color;
    }

    public static Color SnapshotThingColor(Color baseColor, int distanceFromActive, float alpha)
    {
        var source = baseColor.maxColorComponent <= 0f ? Color.white : baseColor;
        var desaturated = Color.Lerp(source, new Color(0.82f, 0.82f, 0.82f, 1f), Mathf.Clamp01(0.10f + distanceFromActive * 0.06f));
        var color = Color.Lerp(desaturated, new Color(0.90f, 0.91f, 0.92f, 1f), Mathf.Clamp01(0.05f + distanceFromActive * 0.03f));
        color.a = Mathf.Clamp01(alpha * (0.92f - 0.06f * Mathf.Max(0, distanceFromActive - 1)));
        return color;
    }

    public static Color PawnTintForDepth(int distanceFromActive, float alpha)
    {
        var color = Color.Lerp(new Color(0.92f, 0.94f, 0.96f, 1f), new Color(0.80f, 0.84f, 0.88f, 1f), Mathf.Clamp01((distanceFromActive - 1) * 0.3f));
        color.a = Mathf.Clamp01(alpha * 0.56f);
        return color;
    }

    public static Color SupportTintForDepth(int distanceFromActive, float alpha)
    {
        var color = Color.Lerp(new Color(0.40f, 0.88f, 1f, 1f), new Color(0.30f, 0.66f, 0.94f, 1f), Mathf.Clamp01((distanceFromActive - 1) * 0.25f));
        color.a = Mathf.Clamp01(alpha * 0.26f);
        return color;
    }

    public static Material GetOverlayMaterial(Color color)
    {
        var key = Color32ToInt((Color32)color);
        if (!OverlayMaterialsByArgb.TryGetValue(key, out var material))
        {
            material = SolidColorMaterials.SimpleSolidColorMaterial(color, true);
            OverlayMaterialsByArgb[key] = material;
        }

        return material;
    }

    public static bool TryGetTintedGraphic(Thing thing, int distanceFromActive, float alpha, out Graphic snapshotGraphic)
    {
        snapshotGraphic = null;
        if (thing?.def == null)
        {
            return false;
        }

        var graphic = thing.Graphic;
        if (graphic == null)
        {
            return false;
        }

        var graphicPath = thing.def.graphicData?.texPath;
        if (string.IsNullOrWhiteSpace(graphicPath))
        {
            return false;
        }

        try
        {
            var shader = graphic.MatSingle?.shader;
            if (shader == null)
            {
                return false;
            }

            var colorOne = SnapshotThingColor(graphic.Color, distanceFromActive, alpha);
            var colorTwo = SnapshotThingColor(graphic.ColorTwo, distanceFromActive, alpha);
            var cacheKey = BuildGraphicKey(graphicPath, shader, colorOne, colorTwo);
            if (!TintedGraphicCache.TryGetValue(cacheKey, out snapshotGraphic))
            {
                snapshotGraphic = graphic.GetColoredVersion(shader, colorOne, colorTwo);
                TintedGraphicCache[cacheKey] = snapshotGraphic;
            }

            return snapshotGraphic != null;
        }
        catch (System.ArgumentNullException)
        {
            return false;
        }
    }

    private static int Color32ToInt(Color32 color)
    {
        return color.a << 24 | color.r << 16 | color.g << 8 | color.b;
    }

    private static string BuildGraphicKey(string path, Shader shader, Color colorOne, Color colorTwo)
    {
        return string.Concat(
            path,
            "|",
            shader.name,
            "|",
            Color32ToInt((Color32)colorOne).ToString(),
            "|",
            Color32ToInt((Color32)colorTwo).ToString());
    }
}
