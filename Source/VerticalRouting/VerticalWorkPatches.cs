using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
public static class VerticalWorkPackagePatch
{
    public static void Postfix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
    {
        var job = VerticalWorkSearch.Find(pawn, __result.Job, __instance.emergency);
        if (job == null) return;
        if (__result.Job != null) JobMaker.ReturnToPool(__result.Job);
        __result = new ThinkResult(job, __instance, job.workGiverDef?.tagToGive ?? JobTag.Misc);
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
public static class VerticalTaskEndPatch
{
    private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
    public static void Prefix(Pawn_JobTracker __instance, JobCondition condition) =>
        GameComponent_VerticalTasks.Get()?.JobEnded(PawnField(__instance), __instance.curJob, condition);
}

[HarmonyPatch(typeof(ThinkNode_QueuedJob), nameof(ThinkNode_QueuedJob.TryIssueJobPackage))]
public static class VerticalQueuedJobPatch
{
    public static bool Prefix(ThinkNode_QueuedJob __instance, Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
    {
        var service = GameComponent_VerticalTasks.Get();
        if (service?.TryQueuedJob(pawn, __instance, jobParams, __instance.inBedOnly, out var result) != true) return true;
        __result = result; return false;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
public static class VerticalClaimReservationPatch
{
    private static readonly AccessTools.FieldRef<ReservationManager, Map> MapField = AccessTools.FieldRefAccess<ReservationManager, Map>("map");
    public static void Postfix(ReservationManager __instance, Pawn claimant, LocalTargetInfo target, ref bool __result)
    {
        if (!__result) return;
        var service = GameComponent_VerticalTasks.Get();
        if (service == null) return;
        if (target.HasThing ? service.Claimed(target.Thing, claimant) : service.ClaimedCell(MapField(__instance), target.Cell, claimant)) __result = false;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserveStack))]
public static class VerticalClaimStackPatch
{
    public static void Postfix(Pawn claimant, LocalTargetInfo target, ref int __result)
    {
        if (__result > 0 && target.HasThing)
            __result = System.Math.Max(0, __result - (GameComponent_VerticalTasks.Get()?.Promised(target.Thing, claimant) ?? 0));
    }
}
