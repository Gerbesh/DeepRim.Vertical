using DeepRim.Vertical.VerticalRendering;
using Verse;

namespace DeepRim.Vertical.VerticalOverlay;

public static class VerticalPawnVisibilityService
{
    public static bool CanSeePawn(Map activeMap, Pawn pawn)
    {
        if (activeMap == null || pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Map == activeMap)
        {
            return false;
        }

        if (!VerticalRenderContextService.SharesSite(activeMap, pawn.Map))
        {
            return false;
        }

        var activeLevel = VerticalRenderContextService.GetLevel(activeMap);
        var pawnLevel = VerticalRenderContextService.GetLevel(pawn.Map);
        if (activeLevel <= pawnLevel)
        {
            return false;
        }

        var probeMap = activeMap;
        while (probeMap != null && probeMap != pawn.Map)
        {
            var mask = UpperOverlayVisibilityMask.GetFor(probeMap);
            if (mask == null || !mask.GetCellState(pawn.Position, overlayEnabled: true).ShowLower)
            {
                return false;
            }

            probeMap = VerticalRenderContextService.LowerMap(probeMap);
        }

        return probeMap == pawn.Map;
    }
}
