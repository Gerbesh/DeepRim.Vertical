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

        var focusCell = UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center;
        return VerticalMapCreationService.TryCreateFloor(map, focusCell, delta, out _);
    }
}
