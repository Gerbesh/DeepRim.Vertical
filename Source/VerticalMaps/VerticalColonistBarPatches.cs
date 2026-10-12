using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

public static class VerticalColonistBarUtility
{
    // Keep the real pawn/map pair: selection and camera jumps must still find its floor.
    // Only location grouping and empty location placeholders are combined.
    public static int GroupEntries(List<ColonistBar.Entry> entries, Func<Map, object> locationKey)
    {
        var groups = new List<List<ColonistBar.Entry>>();
        var groupsByLocation = new Dictionary<object, List<ColonistBar.Entry>>();
        foreach (var entry in entries)
        {
            // Caravan entries have no map; their original group remains independent.
            var key = entry.map == null ? (object)entry.group : locationKey(entry.map) ?? entry.map;
            if (!groupsByLocation.TryGetValue(key, out var group))
            {
                group = new List<ColonistBar.Entry>();
                groupsByLocation.Add(key, group);
                groups.Add(group);
            }

            group.Add(entry);
        }

        entries.Clear();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            var pawns = group.Where(entry => entry.pawn != null)
                .OrderBy(entry => entry.pawn.playerSettings?.displayOrder ?? int.MaxValue).ToList();
            if (pawns.Count == 0)
            {
                // An empty colony still has one clickable location; an empty floor does not.
                entries.Add(new ColonistBar.Entry(null, group[0].map, groupIndex));
                continue;
            }

            foreach (var entry in pawns)
            {
                // Recreate delegates too: they capture the group number for portrait dragging.
                entries.Add(new ColonistBar.Entry(entry.pawn, entry.map, groupIndex));
            }
        }

        return groups.Count;
    }

    public static Map ResolveFrameMap(Map map)
    {
        if (map == null || !VerticalSiteWorldComponent.TryGet(out var component)
            || !component.TryGetFloor(map, out var site, out _))
        {
            return map;
        }

        var current = Find.CurrentMap;
        if (current != null && component.TryGetFloor(current, out var currentSite, out _)
            && currentSite == site)
        {
            return current;
        }

        return site.FloorAt(0)?.Map ?? map;
    }
}

[HarmonyPatch]
public static class ColonistBarDrawLocsFinder_CalculateDrawLocs_VerticalSitePatch
{
    public static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(
        typeof(ColonistBarDrawLocsFinder), nameof(ColonistBarDrawLocsFinder.CalculateDrawLocs),
        new[] { typeof(List<UnityEngine.Vector2>), typeof(float).MakeByRefType(), typeof(int) });

    private static readonly AccessTools.FieldRef<ColonistBar, List<int>> ReorderableGroups =
        AccessTools.FieldRefAccess<ColonistBar, List<int>>("cachedReorderableGroups");

    public static void Prefix(ref int groupsCount)
    {
        if (!VerticalSiteWorldComponent.TryGet(out var component))
        {
            return;
        }

        var bar = Find.ColonistBar;
        // Vanilla has finished rebuilding entries and cleared entriesDirty before calculating layout.
        var entries = bar.Entries;
        if (!entries.Any(entry => entry.map?.Parent is VerticalSiteMapParent))
        {
            return;
        }

        groupsCount = VerticalColonistBarUtility.GroupEntries(entries, map =>
            component.TryGetFloor(map, out var site, out _) ? (object)site : map);

        var reorderableGroups = ReorderableGroups(bar);
        reorderableGroups.Clear();
        for (var i = 0; i < entries.Count; i++)
        {
            reorderableGroups.Add(-1);
        }
    }
}

[HarmonyPatch(typeof(ColonistBarColonistDrawer), "ApplyEntryInAnotherMapAlphaFactor")]
public static class ColonistBarColonistDrawer_ApplyEntryAlpha_VerticalSitePatch
{
    public static void Prefix(ref Map map)
    {
        map = VerticalColonistBarUtility.ResolveFrameMap(map);
    }
}

[HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawGroupFrame))]
public static class ColonistBarColonistDrawer_DrawGroupFrame_VerticalSitePatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ResolveGroupMap(instructions);
    }

    public static IEnumerable<CodeInstruction> ResolveGroupMap(IEnumerable<CodeInstruction> instructions)
    {
        var mapField = AccessTools.Field(typeof(ColonistBar.Entry), nameof(ColonistBar.Entry.map));
        var resolver = AccessTools.Method(typeof(VerticalColonistBarUtility), nameof(VerticalColonistBarUtility.ResolveFrameMap));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, mapField))
            {
                yield return new CodeInstruction(OpCodes.Call, resolver);
            }
        }
    }
}

[HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.HandleGroupFrameClicks))]
public static class ColonistBarColonistDrawer_HandleGroupFrameClicks_VerticalSitePatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ColonistBarColonistDrawer_DrawGroupFrame_VerticalSitePatch.ResolveGroupMap(instructions);
    }
}
