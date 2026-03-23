using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public static class UpperLevelOcclusionUtility
{
    public static HashSet<IntVec3> BuildOcclusionMask(Map map, CellRect sectionRect)
    {
        var mask = new HashSet<IntVec3>();
        if (map == null)
        {
            return mask;
        }

        foreach (var cell in sectionRect.Cells)
        {
            foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
            {
                if (thing == null || thing.Destroyed || !thing.Spawned)
                {
                    continue;
                }

                if (thing is Pawn or Plant)
                {
                    continue;
                }

                if (thing.def.category is ThingCategory.Filth or ThingCategory.Mote or ThingCategory.Gas or ThingCategory.Attachment)
                {
                    continue;
                }

                if (thing.def.category != ThingCategory.Building && thing is not Frame && thing is not Blueprint)
                {
                    continue;
                }

                foreach (var occupiedCell in thing.OccupiedRect())
                {
                    if (occupiedCell.InBounds(map))
                    {
                        mask.Add(occupiedCell);
                    }
                }
            }
        }

        return mask;
    }
}
