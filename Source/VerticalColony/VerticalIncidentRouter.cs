using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

public static class VerticalIncidentRouter
{
    [ThreadStatic] private static bool checking;
    private static readonly HashSet<string> LocalEvents = new() { "Infestation", "DeepDrillInfestation", "ShortCircuit", "CropBlight" };
    public static bool Local(IncidentDef def) => def != null && LocalEvents.Contains(def.defName);
    public static Map Select(IncidentWorker worker, IncidentParms parms)
    {
        if (checking || parms.quest != null || parms.target is not Map map || !VerticalColonyUtility.TrySite(map, out var site)) return null;
        checking = true;
        try
        {
            foreach (var floor in VerticalColonyUtility.Maps(site).OrderBy(m => m == map ? 0 : 1))
            {
                var trial = parms.ShallowCopy(); trial.target = floor;
                if (worker.CanFireNow(trial)) return floor;
            }
            return null;
        }
        finally { checking = false; }
    }
    public static bool Checking => checking;
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class VerticalColonyIncidentAvailabilityPatch
{
    public static void Prefix(IncidentParms parms, out int __state)
    {
        __state = ColonyQueryScope.Disease;
        if (parms.quest == null) ColonyQueryScope.Disease++;
    }
    public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (!__result && !VerticalIncidentRouter.Checking && VerticalIncidentRouter.Local(__instance.def))
            __result = VerticalIncidentRouter.Select(__instance, parms) != null;
    }
    public static void Finalizer(int __state) => ColonyQueryScope.Disease = __state;
}
[HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
public static class VerticalColonyIncidentExecutionPatch
{
    public static void Prefix(FiringIncident fi)
    {
        if (fi?.parms?.target is not Map map || fi.parms.quest != null || fi.sourceQuestPart != null
            || !VerticalColonyUtility.TrySite(map, out var site)) return;
        var anchor = VerticalColonyUtility.Anchor(site);
        if (anchor?.IsPlayerHome != true) return;
        if (VerticalIncidentRouter.Local(fi.def))
        {
            var target = VerticalIncidentRouter.Select(fi.def.Worker, fi.parms);
            if (target != null) fi.parms.target = target;
        }
        else fi.parms.target = anchor;
    }
}
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
public static class VerticalColonyDiseaseExecutionScopePatch
{
    public static void Prefix(IncidentParms parms, out int __state)
    { __state = ColonyQueryScope.Disease; if (parms.quest == null) ColonyQueryScope.Disease++; }
    public static void Finalizer(int __state) => ColonyQueryScope.Disease = __state;
}
[HarmonyPatch]
public static class VerticalColonyDiseaseCandidatesPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IncidentWorker_DiseaseHuman), "PotentialVictimCandidates");
        yield return AccessTools.Method(typeof(IncidentWorker_DiseaseAnimal), "PotentialVictimCandidates");
    }
    public static void Postfix(IncidentWorker_Disease __instance, IIncidentTarget target, ref IEnumerable<Pawn> __result)
    {
        if (ColonyQueryScope.Disease <= 0 || target is not Map map || !VerticalColonyUtility.TrySite(map, out var site)) return;
        if (__instance is IncidentWorker_DiseaseHuman)
            __result = VerticalColonyUtility.Maps(site).SelectMany(m => m.mapPawns.FreeColonistsAndPrisoners)
                .Where(p => p.ParentHolder is not CompBiosculpterPod).Distinct();
        else __result = VerticalColonyUtility.PlayerPawns(site).Where(p => p.HostFaction == null && !p.RaceProps.Humanlike);
    }
}
