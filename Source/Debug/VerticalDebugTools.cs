using DeepRim.Vertical.VerticalMaps;
using Verse;

namespace DeepRim.Vertical.Debug;

public static class VerticalDebugTools
{
    public static bool TryCreateDebugFloor(int delta)
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            return false;
        }

        return VerticalMapCreationService.TryCreateFloor(map, delta, out _);
    }
}
