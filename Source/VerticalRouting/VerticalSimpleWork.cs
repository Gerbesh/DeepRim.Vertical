using System;
using System.Linq;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalColony;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalRouting;

public static class VerticalSimpleWork
{
    private static readonly Type Firefighter = HarmonyLib.AccessTools.TypeByName("RimWorld.WorkGiver_FightFires");
    public static VerticalTaskRecord Find(Pawn pawn, VerticalSiteRecord site, WorkGiver_Scanner giver)
    {
        if (giver is not WorkGiver_Repair && giver is not WorkGiver_CleanFilth && giver.GetType() != Firefighter
            && giver is not WorkGiver_PlantsCut && giver is not WorkGiver_TendOther && giver is not WorkGiver_Researcher) return null;
        foreach (var map in VerticalColonyUtility.Maps(site).Where(m => m != pawn.Map))
        foreach (var thing in VerticalWorkSearch.Scan(map.listerThings.ThingsMatching(giver.PotentialWorkThingRequest), 64, pawn))
        {
            if (!VerticalRouteUtility.Allowed(pawn, thing) || thing.Fogged() || GameComponent_VerticalTasks.Get().Claimed(thing, pawn)
                || map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant != pawn && r.Target.Thing == thing)) continue;
            if (giver is WorkGiver_Repair && (thing.Faction != pawn.Faction || !thing.def.useHitPoints || thing.HitPoints >= thing.MaxHitPoints || thing.IsBurning())) continue;
            if (giver is WorkGiver_CleanFilth && (thing is not Filth || !map.areaManager.Home[thing.Position])) continue;
            if (giver.GetType() == Firefighter && (thing is not Fire || !map.areaManager.Home[thing.Position])) continue;
            if (giver is WorkGiver_PlantsCut && (thing is not Plant || map.designationManager.DesignationOn(thing, DesignationDefOf.CutPlant) == null)) continue;
            if (giver is WorkGiver_TendOther)
            {
                if (thing is not Pawn patient || !HealthAIUtility.ShouldBeTendedNowByPlayer(patient)
                    || patient.Faction != pawn.Faction && patient.HostFaction != pawn.Faction
                    || giver is WorkGiver_TendOther_Humanlike && !patient.RaceProps.Humanlike
                    || giver.def.defName == "DoctorTendToAnimals" && !patient.IsAnimal
                    || giver is WorkGiver_TendOtherUrgent && patient.health.hediffSet.BleedRateTotal <= 0) continue;
            }
            if (giver is WorkGiver_Researcher && (thing is not Building_ResearchBench bench
                || Verse.Find.ResearchManager.GetProject()?.CanBeResearchedAt(bench, false) != true || thing.Faction != pawn.Faction)) continue;
            if (VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, map, thing, giver.PathEndMode) == null) continue;
            return new VerticalTaskRecord { pawn = pawn, kind = VerticalTaskKind.Work, stage = VerticalTaskStage.ToDestination,
                destination = thing, destinationMap = map.Parent, destinationCell = thing.Position, workGiver = giver.def };
        }
        return null;
    }
}
