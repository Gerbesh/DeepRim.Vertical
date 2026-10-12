using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalColony;
using DeepRim.Vertical.VerticalWorld;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalCombat;

public static class VerticalCombatCommands
{
    public static bool Attack(Pawn pawn, Thing target)
    {
        var verb = pawn.TryGetAttackVerb(target, true);
        if (!pawn.Drafted || !VerticalSight.CanShoot(verb, pawn.Position, target)) return false;
        var job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
        job.playerForced = true; job.endIfCantShootTargetFromCurPos = true;
        return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
    public static IEnumerable<Gizmo> Append(Pawn pawn, IEnumerable<Gizmo> original)
    {
        foreach (var gizmo in original) yield return gizmo;
        if (!pawn.Drafted || pawn.Faction != Faction.OfPlayer || pawn.TryGetAttackVerb(null, true) is not Verb_LaunchProjectile
            || !VerticalColonyUtility.TrySite(pawn.Map, out var site) || site.floors.Count < 2) yield break;
        yield return new Command_Action
        {
            defaultLabel = "DeepRimVertical.Combat.Attack".Translate(),
            defaultDesc = "DeepRimVertical.Combat.AttackDescription".Translate(),
            icon = TexCommand.Attack,
            action = () =>
            {
                var options = new List<FloatMenuOption>();
                var verb = pawn.TryGetAttackVerb(null, true);
                foreach (var target in VerticalColonyUtility.Maps(site).Where(m => m != pawn.Map)
                    .SelectMany(m => m.attackTargetsCache.GetPotentialTargetsFor(pawn).ToArray()).Select(t => t.Thing)
                    .Where(t => t.Spawned && pawn.HostileTo(t) && verb.CanHitTarget(t))
                    .OrderByDescending(t => VerticalHeightAdvantage.Levels(site, pawn.Map, t.Map))
                    .ThenBy(t => VerticalSight.Distance(pawn, t)).Take(64))
                {
                    var captured = target;
                    var level = site.floors.First(f => f.Map == target.Map).levelIndex;
                    options.Add(new FloatMenuOption(target.LabelCap + " (" + VerticalFloorLabel.Format(level) + ")", () => Attack(pawn, captured)));
                }
                if (options.Count == 0) options.Add(new FloatMenuOption("DeepRimVertical.Combat.NoTargets".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class VerticalPawnCombatGizmosPatch
{
    public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result) => __result = VerticalCombatCommands.Append(__instance, __result);
}
