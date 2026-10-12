using System;
using DeepRim.Vertical.Persistence;
using Verse;

namespace DeepRim.Vertical.VerticalCombat;

/// <summary>Only shots from a higher level into a lower level gain range and cover advantages.</summary>
public static class VerticalHeightAdvantage
{
    public static int Levels(VerticalSiteRecord site, Map shooter, Map target)
    {
        if (site == null || shooter == null || target == null) return 0;
        var from = site.floors.Find(f => f.Map == shooter);
        var to = site.floors.Find(f => f.Map == target);
        return from == null || to == null ? 0 : Math.Max(0, from.levelIndex - to.levelIndex);
    }

    public static float Bonus(int levels) => levels <= 0 ? 0f : levels == 1 ? .3f : levels == 2 ? .6f : 1f;
    public static float RangeMultiplier(int levels) => 1f + Bonus(levels);
    public static float CoverBlockMultiplier(int levels) => 1f - Bonus(levels);
}
