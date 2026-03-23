using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public static class VerticalRenderContextService
{
    public static bool TryGetContext(Map map, out VerticalRenderContext context)
    {
        context = null;
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out var currentFloor))
        {
            return false;
        }

        var lowerFloors = site.OrderedFloors
            .Where(f => f?.Map != null && f.levelIndex < currentFloor.levelIndex)
            .Select(f => new VerticalRenderFloor(
                f,
                currentFloor.levelIndex - f.levelIndex,
                OpacityForDistance(currentFloor.levelIndex - f.levelIndex)))
            .ToList();

        context = new VerticalRenderContext(site, currentFloor, lowerFloors);
        return true;
    }

    private static float OpacityForDistance(int distance)
    {
        return distance switch
        {
            1 => 0.86f,
            2 => 0.68f,
            3 => 0.52f,
            _ => 0.42f
        };
    }

    public static int GetLevel(Map map)
    {
        return TryGetContext(map, out var context) ? context.CurrentFloor.levelIndex : 0;
    }

    public static Map LowerMap(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out var floor))
        {
            return null;
        }

        return site.FloorAt(floor.levelIndex - 1)?.Map;
    }

    public static Map UpperMap(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out var floor))
        {
            return null;
        }

        return site.FloorAt(floor.levelIndex + 1)?.Map;
    }

    public static Map GroundMap(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component) || !component.TryGetFloor(map, out var site, out _))
        {
            return null;
        }

        return site.FloorAt(0)?.Map;
    }

    public static bool SharesSite(Map left, Map right)
    {
        if (left == null || right == null || !VerticalSiteWorldComponent.TryGet(out var component))
        {
            return false;
        }

        return component.TryGetFloor(left, out var leftSite, out _)
               && component.TryGetFloor(right, out var rightSite, out _)
               && leftSite.siteId == rightSite.siteId;
    }
}

public sealed class VerticalRenderContext
{
    public VerticalRenderContext(VerticalSiteRecord site, VerticalFloorRecord currentFloor, IReadOnlyList<VerticalRenderFloor> lowerFloors)
    {
        Site = site;
        CurrentFloor = currentFloor;
        LowerFloors = lowerFloors;
    }

    public VerticalSiteRecord Site { get; }

    public VerticalFloorRecord CurrentFloor { get; }

    public IReadOnlyList<VerticalRenderFloor> LowerFloors { get; }
}

public sealed class VerticalRenderFloor
{
    public VerticalRenderFloor(VerticalFloorRecord floor, int distanceFromActive, float alpha)
    {
        Floor = floor;
        DistanceFromActive = distanceFromActive;
        Alpha = alpha;
    }

    public VerticalFloorRecord Floor { get; }

    public int DistanceFromActive { get; }

    public float Alpha { get; }
}
