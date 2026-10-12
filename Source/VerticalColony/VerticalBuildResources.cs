using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
public static class VerticalResourceRefresh
{
    public static void Postfix() => Current.Game?.GetComponent<GameComponent_VerticalColony>()?.InvalidateResources();
}

// Only designator queries see common stocks. Native jobs retain physical map-local counters.
[HarmonyPatch]
public static class VerticalBuildResourceScope
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "ProcessInput", "get_StuffDef", "DrawPanelReadout", "DrawPlaceMouseAttachments" })
            yield return AccessTools.Method(typeof(Designator_Build), name);
    }
    public static void Prefix(out int __state) => VerticalColonyResourceReadoutPatch.Prefix(out __state);
    public static void Finalizer(int __state) => VerticalColonyResourceReadoutPatch.Finalizer(__state);
}

[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.ProcessInput))]
public static class VerticalBuildStuffPresence
{
    public static List<Thing> ThingsOfDef(ListerThings lister, ThingDef def)
    {
        var map = Find.CurrentMap?.listerThings == lister ? Find.CurrentMap : Find.Maps.FirstOrDefault(m => m.listerThings == lister);
        return map == null || !VerticalColonyUtility.TrySite(map, out var site) ? lister.ThingsOfDef(def)
            : VerticalColonyUtility.Maps(site).SelectMany(m => m.listerThings.ThingsOfDef(def)).Where(t => t.Spawned && !t.Destroyed).ToList();
    }
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsOfDef));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(VerticalBuildStuffPresence), nameof(ThingsOfDef)); }
            yield return instruction;
        }
    }
}
