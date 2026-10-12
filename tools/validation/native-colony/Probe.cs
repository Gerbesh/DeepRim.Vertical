// Opt-in test mod, never included in DeepRim's production assembly.
using System;
using System.IO;
using System.Linq;
using DeepRim.Vertical.VerticalConstruction;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalRouting;
using DeepRim.Vertical.VerticalProfiling;
using DeepRim.Vertical.VerticalWorld;
using DeepRim.Vertical.VerticalColony;
using DeepRim.Vertical.VerticalCombat;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

public sealed class VerticalColonyNativeProbe : GameComponent
{
    private int phase, elapsed;
    private bool reloadedMining, reloadedCargo;
    private Pawn worker;
    private MapParent surface, middle, bottom;
    private Thing mineable, steel, blueprint;
    private Building_Bed bed;
    private Thing bench;
    private Bill_Production productionBill;
    private MapParent upper;
    private Pawn combatTarget;
    private Thing reverseTarget;
    private bool reloadedFlight;
    private Thing fuelTarget;
    public VerticalColonyNativeProbe(Game game) { Log.Message("[COLONY PROBE] Created"); }
    public override void ExposeData()
    {
        Scribe_Values.Look(ref phase, "probePhase"); Scribe_Values.Look(ref elapsed, "probeElapsed");
        Scribe_Values.Look(ref reloadedMining, "reloadedMining"); Scribe_Values.Look(ref reloadedCargo, "reloadedCargo");
        Scribe_References.Look(ref worker, "worker"); Scribe_References.Look(ref surface, "surface");
        Scribe_References.Look(ref middle, "middle"); Scribe_References.Look(ref bottom, "bottom");
        Scribe_References.Look(ref mineable, "mineable"); Scribe_References.Look(ref steel, "steel"); Scribe_References.Look(ref blueprint, "blueprint");
        Scribe_References.Look(ref bed, "bed"); Scribe_References.Look(ref bench, "bench"); Scribe_References.Look(ref productionBill, "productionBill");
        Scribe_References.Look(ref upper, "upper"); Scribe_References.Look(ref combatTarget, "combatTarget"); Scribe_References.Look(ref reverseTarget, "reverseTarget");
        Scribe_Values.Look(ref reloadedFlight, "reloadedFlight");
        Scribe_References.Look(ref fuelTarget, "fuelTarget");
    }
    private static void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
    private static Thing Spawn(string name, Map map, int x, int z, int count = 1)
    {
        var def = DefDatabase<ThingDef>.GetNamed(name);
        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null);
        thing.stackCount = count;
        if (def.category == ThingCategory.Building) thing.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(thing, new IntVec3(x, 0, z), map, Rot4.North, WipeMode.Vanish,
            respawningAfterLoad: thing is Building_VerticalStair);
        return thing;
    }
    public override void GameComponentUpdate()
    {
        // Never manually tick a partially loaded game, including standard-window events.
        if (Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        if (!GenFilePaths.SaveDataFolderPath.EndsWith("native-smoke-colony", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (phase == 0) { Setup(); return; }
            if (phase == 1)
            {
                if (++elapsed % 300 == 0) Log.Message("[COLONY PROBE] Waiting for stair links");
                Require(elapsed < 3000, "Stair linking timed out");
                if (surface.Map.listerThings.AllThings.OfType<Building_VerticalStair>().Any(s => s.LinkedStair == null)
                    || middle.Map.listerThings.AllThings.OfType<Building_VerticalStair>().Any(s => s.LinkedStair == null)) return;
                foreach (var map in new[] { surface.Map, middle.Map, bottom.Map }) map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                Require(VerticalRouteUtility.Find(worker, surface.Map, worker.Position, bottom.Map, mineable, PathEndMode.Touch)?.Count == 2, "Two-hop route with different intermediate stair cells");
                var originalMap = worker.Map;
                Require(VerticalRouteUtility.Reach(worker, bottom.Map, new IntVec3(44, 0, 24), mineable, PathEndMode.Touch) && worker.Map == originalMap, "Remote preflight must retain pawn map");
                TestColonyAccounting(); StartWork("mining"); phase = 2; elapsed = 0;
            }
            for (var i = 0; i < 100; i++)
            {
                Find.TickManager.DoSingleTick(); elapsed++;
                if (elapsed > 16000) throw new InvalidOperationException("Phase " + phase + " timed out at " + worker.Map?.uniqueID + " " + worker.Position + " job " + worker.CurJob + " tasks " + GameComponent_VerticalTasks.Get().Tasks.Count());
                if (phase == 2)
                {
                    if (!reloadedMining && worker.Map == middle.Map && worker.Position.x > 30)
                    {
                        reloadedMining = true; SaveReload("routing-mining"); return;
                    }
                    if (mineable == null || mineable.Destroyed)
                    {
                        Require(reloadedMining && worker.Map == bottom.Map, "Mining did not complete after en-route load");
                        Log.Message("[COLONY PROBE] Autonomous mining + intermediate walk + load PASS");
                        SetupHaul(); phase = 3; elapsed = 0;
                    }
                }
                else if (phase == 3)
                {
                    if (!reloadedCargo && worker.carryTracker.CarriedThing?.def == ThingDefOf.Steel && worker.Map == middle.Map && worker.Position.x > 30)
                    { reloadedCargo = true; SaveReload("routing-cargo"); return; }
                    var stored = bottom.Map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(t => t.IsInValidStorage()).Sum(t => t.stackCount);
                    if (stored == 40)
                    {
                        Require(reloadedCargo && Total(ThingDefOf.Steel) == 40 && worker.carryTracker.CarriedThing == null, "Haul cargo conservation/load");
                        Log.Message("[COLONY PROBE] Remote stockpile + cargo/load + conservation PASS");
                        SetupConstruction(); phase = 4; elapsed = 0;
                    }
                }
                else if (phase == 4)
                {
                    var wall = new IntVec3(46, 0, 28).GetEdifice(bottom.Map);
                    if (wall?.def == ThingDefOf.Wall)
                    {
                        Require(wall.Stuff == ThingDefOf.WoodLog, "Wrong construction material");
                        Require(Total(ThingDefOf.WoodLog) == 35, "Construction did not consume exactly five logs: " + Total(ThingDefOf.WoodLog));
                        Log.Message("[COLONY PROBE] Cross-floor blueprint supply + native completion PASS");
                        TestStoragePriorities();
                        SetupFood(); phase = 6; elapsed = 0;
                    }
                }
                else if (phase == 6 && worker.needs.food.CurLevelPercentage > .75f)
                {
                    Require(worker.Map == surface.Map, "Remote food was consumed without travel");
                    Log.Message("[COLONY PROBE] Autonomous remote food, native ingestion and diet policy PASS");
                    SetupRest(); phase = 7; elapsed = 0;
                }
                else if (phase == 7 && worker.CurrentBed() == bed && worker.needs.rest.CurLevelPercentage > .2f)
                {
                    Require(worker.Map == bottom.Map, "Remote bed was used without travel");
                    Log.Message("[COLONY PROBE] Autonomous owned bed travel and native sleep PASS");
                    SetupProduction(); phase = 8; elapsed = 0;
                }
                else if (phase == 8 && Total(DefDatabase<ThingDef>.GetNamed("BlocksGranite")) == 20 && !productionBill.ShouldDoNow())
                {
                    Require(Total(DefDatabase<ThingDef>.GetNamed("ChunkGranite")) == 0 && !productionBill.ShouldDoNow(), "Physical recipe consumption / until-X scope");
                    Log.Message("[COLONY PROBE] Remote ingredient supply, native recipe and shared until-X PASS");
                    SetupRefuel(); phase = 11; elapsed = 0;
                }
                else if (phase == 11 && fuelTarget.TryGetComp<CompRefuelable>().Fuel > 18)
                {
                    Require(Total(ThingDefOf.WoodLog) == 15, "Refueling did not consume exactly 20 physical logs");
                    Log.Message("[COLONY PROBE] Physical remote fuel pickup, transfer and native consumption PASS");
                    TestAdditionalWork();
                    SetupCombat(); phase = 9; elapsed = 0;
                }
                else if (phase == 9)
                {
                    if (reloadedFlight)
                    {
                        var roofCell = new IntVec3(23, 0, 24);
                        Require(surface.Map.roofGrid.RoofAt(roofCell) == RoofDefOf.RoofConstructed
                            && surface.Map.GetComponent<VerticalCeilingComponent>().Owns(roofCell),
                            "Owned ceiling did not survive native save/load");
                    }
                    if (!reloadedFlight && Find.Maps.SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.Projectile))
                        .OfType<Projectile>().Any(p => p.GetComp<CompVerticalFlight>()?.Active == true))
                    { reloadedFlight = true; SaveReload("colony-flight"); return; }
                    if (combatTarget.Dead || combatTarget.health.hediffSet.hediffs.Any(h => h is Hediff_Injury))
                    {
                        Require(reloadedFlight, "Vertical flight did not reload");
                        Log.Message("[COLONY PROBE] Native downward projectile damage and mid-flight save/load PASS");
                        SetupReverseShot(); phase = 10; elapsed = 0;
                    }
                    else if (elapsed % 120 == 0) worker.TryGetAttackVerb(combatTarget, true).TryStartCastOn(combatTarget);
                }
                else if (phase == 10 && reverseTarget.HitPoints < reverseTarget.MaxHitPoints)
                {
                    Log.Message("[COLONY PROBE] Native upward projectile transfer and damage PASS");
                    worker.jobs.StopAll(); worker.drafter.Drafted = false; worker.stances.CancelBusyStanceHard();
                    worker.DeSpawn(); GenSpawn.Spawn(worker, new IntVec3(45, 0, 29), bottom.Map);
                    combatTarget.Destroy(); reverseTarget.Destroy();
                    TestQueue(); phase = 5; elapsed = 0;
                }
                else if (phase == 5 && worker.Map == bottom.Map && worker.Position == new IntVec3(45, 0, 29) && worker.jobs.jobQueue.Count == 0)
                {
                    Log.Message("[COLONY PROBE] Queued cell order returns to its original map PASS");
                    TestDraftTransfer();
                    TestClosedRoute();
                    TestCargoCancellation();
                    TestProfiler();
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "PASS: native four-floor colony; shared map/world/history wealth, population, story state, resource readout and disease candidates; autonomous mining, physical hauling and reservations, construction, food/rest, recipe ingredients and global until-X, fuel, repair/clean/tend/research; bidirectional projectiles and blocked LOS; three save/load cycles including mid-flight; original-map queued orders, drafted travel with independent destination geometry, closed-route cargo conservation; four-floor profiler, 23 hooks, exports and cleanup.");
                    Log.Message("[COLONY PROBE] PASS"); Application.Quit(); return;
                }
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "FAIL: " + error);
            Log.Error("[COLONY PROBE] FAIL " + error); Application.Quit();
        }
    }
    private void Setup()
    {
        Log.Message("[COLONY PROBE] Setup");
        var upper = Find.CurrentMap;
        surface = upper.Parent;
        Require(VerticalMapCreationService.TryCreateFloor(upper, -1, out var mid, false), "Middle floor");
        Require(VerticalMapCreationService.TryCreateFloor(mid, -1, out var low, false), "Bottom floor");
        middle = mid.Parent; bottom = low.Parent;
        foreach (var map in new[] { upper, mid, low })
            foreach (var item in map.listerThings.AllThings.Where(t => t.def == ThingDefOf.Steel || t.def == ThingDefOf.WoodLog).ToList()) item.Destroy();
        foreach (var map in new[] { upper, mid, low })
        for (var x = 20; x <= 50; x++)
        for (var z = 20; z <= 32; z++)
        {
            var cell = new IntVec3(x, 0, z);
            foreach (var thing in cell.GetThingList(map).Where(t => t is not Pawn).ToList()) thing.Destroy();
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete); map.fogGrid.Unfog(cell);
        }
        var requiredWork = new[] { WorkTypeDefOf.Mining, WorkTypeDefOf.Construction, WorkTypeDefOf.Hauling,
            WorkTypeDefOf.Crafting, WorkTypeDefOf.Cleaning, WorkTypeDefOf.Doctor, WorkTypeDefOf.Research };
        Func<Pawn, bool> capable = p => p.DevelopmentalStage == DevelopmentalStage.Adult
            && requiredWork.All(w => !p.WorkTypeIsDisabled(w)) && !p.WorkTagIsDisabled(WorkTags.Violent);
        worker = upper.mapPawns.FreeColonists.FirstOrDefault(capable);
        if (worker == null)
        {
            for (var attempt = 0; attempt < 100 && worker == null; attempt++)
            {
                var generated = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (capable(generated)) worker = generated; else generated.Destroy();
            }
            Require(worker != null, "No fully capable test worker");
            // Replace one starter, preserving the three-colonist accounting fixture.
            upper.mapPawns.FreeColonists.First().Destroy(); GenSpawn.Spawn(worker, new IntVec3(23, 0, 24), upper);
        }
        foreach (var pawn in upper.mapPawns.FreeColonists)
        {
            pawn.jobs.StopAll();
            foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) pawn.workSettings.SetPriority(work, 0);
            if (pawn != worker) pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 100000));
        }
        worker.Position = new IntVec3(23, 0, 24);
        // The isolated logistics probe is not an incident survival test. Random raids must not
        // replace remote jobs with native FleeAndCower during a several-thousand-tick run.
        if (worker.playerSettings != null) worker.playerSettings.hostilityResponse = HostilityResponseMode.Ignore;
        worker.workSettings.SetPriority(WorkTypeDefOf.Mining, 1); worker.workSettings.SetPriority(WorkTypeDefOf.Construction, 2); worker.workSettings.SetPriority(WorkTypeDefOf.Hauling, 3);
        worker.skills.GetSkill(SkillDefOf.Mining).Level = 20; worker.skills.GetSkill(SkillDefOf.Construction).Level = 20;
        for (var hour = 0; hour < 24; hour++) worker.timetable.times[hour] = TimeAssignmentDefOf.Work;
        foreach (var need in worker.needs.AllNeeds) need.CurLevelPercentage = 1f;
        // Batch mode does not display the loading window awaited by queued stair connections.
        // Invoke the same production connection service directly on the main thread.
        Require(VerticalStairService.Connect((Building_VerticalStair)Spawn("DeepRimVertical_StairsDown", upper, 24, 24)), "First stair pair");
        Require(VerticalStairService.Connect((Building_VerticalStair)Spawn("DeepRimVertical_StairsDown", mid, 44, 24)), "Second stair pair");
        mineable = Spawn("Granite", low, 45, 25); mineable.HitPoints = 1;
        low.designationManager.AddDesignation(new Designation(mineable.Position, DesignationDefOf.Mine));
        phase = 1;
        Log.Message("[COLONY PROBE] Waiting for links on three floors");
    }
    private void StartWork(string expected)
    {
        worker.jobs.StopAll();
        worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 10000));
        var result = new JobGiver_Work().TryIssueJobPackage(worker, default);
        for (var attempt = 0; result.Job == null && attempt < 16; attempt++)
        {
            for (var tick = 0; tick < 181; tick++) Find.TickManager.DoSingleTick();
            result = new JobGiver_Work().TryIssueJobPackage(worker, default);
        }
        if (result.Job == null)
        {
            Log.Message("[COLONY PROBE] No job: eligible=" + VerticalWorkSearch.Eligible(worker)
                + " carry=" + worker.carryTracker.CarriedThing + " haulingDisabled=" + worker.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
                + " givers=" + string.Join(",", worker.workSettings.WorkGiversInOrderNormal.Select(g => g.def.defName)));
            if (steel?.Spawned == true)
                Log.Message("[COLONY PROBE] Steel allowed=" + VerticalRouteUtility.Allowed(worker, steel)
                    + " listed=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Contains(steel)
                    + " pickupRoute=" + VerticalRouteUtility.Find(worker, worker.Map, worker.Position, steel.Map, steel, PathEndMode.ClosestTouch, true)?.Count
                    + " cargoRoute=" + VerticalRouteUtility.Find(worker, steel.Map, steel.Position, bottom.Map, new IntVec3(45, 0, 27), PathEndMode.OnCell, true)?.Count
                    + " storage=" + VerticalWorkSearch.ValidStoreCell(worker, bottom.Map, new IntVec3(45, 0, 27), steel));
            if (steel?.Spawned == true)
            {
                Log.Message("[COLONY PROBE] Candidate count=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Count
                    + " steelIndex=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).IndexOf(steel)
                    + " always=" + steel.def.alwaysHaulable + " floorDistance=" + DeepRim.Vertical.VerticalRuntime.Settings.crossLevelHaulMaxFloorDistance);
                foreach (var group in bottom.Map.haulDestinationManager.AllGroupsListInPriorityOrder)
                    Log.Message("[COLONY PROBE] Store group priority=" + group.Settings.Priority + " cells=" + group.CellsList.Count + " accepts=" + group.parent.Accepts(steel));
                Log.Message("[COLONY PROBE] Source usable=" + VerticalWorkSearch.SourceUsable(worker, steel)
                    + " capacity=" + worker.carryTracker.MaxStackSpaceEver(steel.def) + " currentPriority=" + StoreUtility.CurrentStoragePriorityOf(steel)
                    + " haulEnabled=" + DeepRim.Vertical.VerticalRuntime.Settings.allowCrossLevelHauling
                    + " haulPlan=" + typeof(VerticalWorkSearch).GetMethod("FindHaul", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null,
                        new object[] { worker, Find.World.GetComponent<VerticalSiteWorldComponent>().Sites.First(), DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral") }));
            }
        }
        Require(result.Job != null, "No automatic " + expected + " job");
        Log.Message("[COLONY PROBE] Start " + expected + ": " + result.Job);
        worker.jobs.StartJob(result.Job, JobCondition.InterruptForced, result.SourceNode, tag: result.Tag, preToilReservationsCanFail: true);
    }
    private void SetupHaul()
    {
        worker.jobs.StopAll(); GameComponent_VerticalTasks.Get().CancelPawn(worker);
        steel = Spawn("Steel", surface.Map, 26, 25, 40);
        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, bottom.Map.zoneManager); bottom.Map.zoneManager.RegisterZone(zone);
        zone.AddCell(new IntVec3(45, 0, 27)); zone.settings.filter.SetDisallowAll(); zone.settings.filter.SetAllow(ThingDefOf.Steel, true); zone.settings.Priority = StoragePriority.Critical;
        foreach (var need in worker.needs.AllNeeds) need.CurLevelPercentage = 1f;
        // The worker is on -2; both source pickup and delivery must route autonomously.
        StartWork("hauling");
        var peer = surface.Map.mapPawns.FreeColonists.First(p => p != worker);
        Require(GameComponent_VerticalTasks.Get().Claimed(steel, peer) && !peer.CanReserve(steel), "Remote resource claim must block local competition");
    }
    private void SetupConstruction()
    {
        worker.jobs.StopAll(); GameComponent_VerticalTasks.Get().CancelPawn(worker);
        Spawn("WoodLog", surface.Map, 26, 26, 40);
        blueprint = GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, new IntVec3(46, 0, 28), bottom.Map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
        foreach (var need in worker.needs.AllNeeds) need.CurLevelPercentage = 1f;
        StartWork("construction supply");
    }
    private void TestQueue()
    {
        worker.jobs.StopAll();
        Require(GameComponent_VerticalTasks.Get().QueueTravel(worker, new IntVec3(44, 0, 24), surface.Map, 0), "Manual return route");
        worker.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Goto, new IntVec3(45, 0, 29)), JobTag.Misc);
    }
    private void TestColonyAccounting()
    {
        Require(VerticalColonyUtility.TrySite(surface.Map, out var site), "Colony context");
        var expectedWealth = new[] { surface.Map, middle.Map, bottom.Map }.Sum(m => m.wealthWatcher.WealthItems + .5f * m.wealthWatcher.WealthBuildings + m.wealthWatcher.WealthPawns);
        Current.Game.GetComponent<GameComponent_VerticalColony>().Invalidate();
        var wealth = surface.Map.PlayerWealthForStoryteller;
        expectedWealth = new[] { surface.Map, middle.Map, bottom.Map }.Sum(m => m.wealthWatcher.WealthItems + .5f * m.wealthWatcher.WealthBuildings + m.wealthWatcher.WealthPawns);
        Require(Math.Abs(wealth - expectedWealth) < .1 && Math.Abs(wealth - bottom.Map.PlayerWealthForStoryteller) < .1, "Shared wealth / local counter ownership");
        Require(Math.Abs(Find.World.PlayerWealthForStoryteller - wealth) < .1, "World sums the same colony more than once");
        Require(Math.Abs(new HistoryAutoRecorderWorker_WealthTotal().PullRecord()
            - new[] { surface.Map, middle.Map, bottom.Map }.Sum(m => m.wealthWatcher.WealthTotal)) < .1, "Wealth history uses a different ownership snapshot");
        Require(ReferenceEquals(surface.Map.StoryState, bottom.Map.StoryState), "Shared story state");
        Require(Find.Storyteller.AllIncidentTargets.OfType<Map>().Count(m => VerticalColonyUtility.Maps(site).Contains(m)) == 1, "Extra floor incident scheduling");
        Require(bottom.Map.PlayerPawnsForStoryteller.Count() == surface.Map.mapPawns.PawnsInFaction(Faction.OfPlayer).Count(), "Colony population duplicated");
        var fixedMode = Find.Storyteller.difficulty.fixedWealthMode; Find.Storyteller.difficulty.fixedWealthMode = true;
        Require(Math.Abs(bottom.Map.PlayerWealthForStoryteller - StorytellerUtility.FixedWealthModeMapWealthFromTimeCurve.Evaluate(surface.Map.AgeInDays * Find.Storyteller.difficulty.fixedWealthTimeFactor)) < .1, "Fixed wealth floor age");
        Find.Storyteller.difficulty.fixedWealthMode = fixedMode;
        var oldPosition = worker.Position; worker.DeSpawn(); GenSpawn.Spawn(worker, new IntVec3(22, 0, 24), middle.Map);
        Require(surface.Map.PlayerPawnsForStoryteller.Contains(worker), "Remote pawn missing from colony population");
        VerticalColonyIncidentAvailabilityPatch.Prefix(new IncidentParms { target = surface.Map }, out var diseaseState);
        var victims = (System.Collections.Generic.IEnumerable<Pawn>)typeof(IncidentWorker_DiseaseHuman).GetMethod("PotentialVictimCandidates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(new IncidentWorker_DiseaseHuman(), new object[] { surface.Map });
        Require(victims.Contains(worker) && victims.Count() == 3, "Disease ignores underground colonists");
        VerticalColonyIncidentAvailabilityPatch.Finalizer(diseaseState);
        worker.DeSpawn(); GenSpawn.Spawn(worker, oldPosition, surface.Map);
        var first = Spawn("Steel", surface.Map, 28, 28, 10); var second = Spawn("Steel", middle.Map, 28, 28, 20);
        foreach (var map in new[] { surface.Map, middle.Map })
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager); map.zoneManager.RegisterZone(zone); zone.AddCell(new IntVec3(28, 0, 28));
            zone.settings.filter.SetDisallowAll(); zone.settings.filter.SetAllow(ThingDefOf.Steel, true); map.resourceCounter.UpdateResourceCounts();
        }
        Require(VerticalColonyUtility.Resources(bottom.Map)[ThingDefOf.Steel] == 30, "Shared readout total");
        Require(surface.Map.resourceCounter.GetCount(ThingDefOf.Steel) == 10 && middle.Map.resourceCounter.GetCount(ThingDefOf.Steel) == 20, "Global readout leaked into local resource availability");
        VerticalColonyResourceReadoutPatch.Prefix(out var state);
        Require(bottom.Map.resourceCounter.GetCount(ThingDefOf.Steel) == 30, "Readout query scope");
        VerticalColonyResourceReadoutPatch.Finalizer(state);
        first.Destroy(); second.Destroy();
        Log.Message("[COLONY PROBE] Shared wealth/population/story state, single incident target, fixed mode and isolated readout PASS");
    }
    private void SetupFood()
    {
        worker.jobs.StopAll();
        foreach (var map in new[] { surface.Map, middle.Map, bottom.Map })
        foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource).Where(t => t is not Pawn && t is not Plant).ToList()) thing.Destroy();
        foreach (var item in worker.inventory.innerContainer.Where(t => t.def.IsNutritionGivingIngestible).ToList()) item.Destroy();
        Spawn("MealSimple", surface.Map, 26, 27, 2); worker.needs.food.CurLevelPercentage = .05f;
        var result = new JobGiver_GetFood().TryIssueJobPackage(worker, default);
        Require(result.Job?.def == VerticalRoutingDefOf.DeepRimVertical_Travel, "No autonomous remote food route");
        worker.jobs.StartJob(result.Job, JobCondition.InterruptForced, result.SourceNode);
    }
    private void SetupRest()
    {
        worker.jobs.StopAll(); worker.needs.food.CurLevelPercentage = 1;
        foreach (var map in new[] { surface.Map, middle.Map, bottom.Map })
        foreach (var existing in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().ToList()) existing.Destroy();
        bed = (Building_Bed)Spawn("Bed", bottom.Map, 46, 30); worker.ownership.ClaimBedIfNonMedical(bed);
        worker.needs.rest.CurLevelPercentage = .05f;
        for (var hour = 0; hour < 24; hour++) worker.timetable.times[hour] = TimeAssignmentDefOf.Sleep;
        var result = new JobGiver_GetRest().TryIssueJobPackage(worker, default);
        Require(result.Job?.def == VerticalRoutingDefOf.DeepRimVertical_Travel, "No autonomous remote bed route");
        worker.jobs.StartJob(result.Job, JobCondition.InterruptForced, result.SourceNode);
    }
    private void SetupProduction()
    {
        worker.jobs.StopAll();
        foreach (var need in worker.needs.AllNeeds) need.CurLevelPercentage = 1;
        for (var hour = 0; hour < 24; hour++) worker.timetable.times[hour] = TimeAssignmentDefOf.Work;
        foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) worker.workSettings.SetPriority(work, 0);
        worker.workSettings.SetPriority(WorkTypeDefOf.Crafting, 1);
        foreach (var map in new[] { surface.Map, middle.Map, bottom.Map })
        foreach (var chunk in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t => t.def.thingCategories?.Contains(ThingCategoryDefOf.StoneChunks) == true).ToList()) chunk.Destroy();
        bench = Spawn("TableStonecutter", bottom.Map, 48, 30);
        var recipe = bench.def.AllRecipes.First(r => r.products?.Any(p => p.thingDef.defName == "BlocksGranite") == true);
        productionBill = (Bill_Production)recipe.MakeNewBill(); ((IBillGiver)bench).BillStack.AddBill(productionBill);
        productionBill.repeatMode = BillRepeatModeDefOf.TargetCount; productionBill.targetCount = 20;
        var blocks = Spawn("BlocksGranite", middle.Map, 29, 28, 20);
        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, middle.Map.zoneManager); middle.Map.zoneManager.RegisterZone(zone); zone.AddCell(blocks.Position);
        zone.settings.filter.SetDisallowAll(); zone.settings.filter.SetAllow(blocks.def, true); middle.Map.resourceCounter.UpdateResourceCounts();
        Require(!productionBill.ShouldDoNow() && recipe.WorkerCounter.CountProducts(productionBill) == 20, "Until-X does not count another floor");
        blocks.Destroy(); middle.Map.resourceCounter.UpdateResourceCounts();
        Spawn("ChunkGranite", surface.Map, 26, 28);
        var outputZone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, bottom.Map.zoneManager);
        bottom.Map.zoneManager.RegisterZone(outputZone); outputZone.AddCell(new IntVec3(50, 0, 27));
        outputZone.settings.filter.SetDisallowAll(); outputZone.settings.filter.SetAllow(DefDatabase<ThingDef>.GetNamed("BlocksGranite"), true);
        outputZone.settings.Priority = StoragePriority.Critical;
        Require(productionBill.ShouldDoNow(), "Until-X did not resume after product removal");
        StartWork("remote recipe supply");
    }
    private void SetupCombat()
    {
        worker.jobs.StopAll(); worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out _);
        Require(VerticalMapCreationService.TryCreateFloor(surface.Map, 1, out var high, false), "Combat upper floor"); upper = high.Parent;
        for (var x = 20; x <= 30; x++) for (var z = 20; z <= 28; z++)
        { high.fogGrid.Unfog(new IntVec3(x, 0, z)); surface.Map.roofGrid.SetRoof(new IntVec3(x, 0, z), null); }
        TestCeilingOwnership(high);
        Require(VerticalHeightAdvantage.RangeMultiplier(0) == 1f && VerticalHeightAdvantage.RangeMultiplier(1) == 1.3f
            && VerticalHeightAdvantage.RangeMultiplier(2) == 1.6f && VerticalHeightAdvantage.RangeMultiplier(3) == 2f
            && VerticalHeightAdvantage.RangeMultiplier(9) == 2f && VerticalHeightAdvantage.RangeMultiplier(-1) == 1f, "Height-based range tier table");
        Require(Mathf.Abs(VerticalHeightAdvantage.CoverBlockMultiplier(0) - 1f) < .0001f
            && Mathf.Abs(VerticalHeightAdvantage.CoverBlockMultiplier(1) - .7f) < .0001f
            && Mathf.Abs(VerticalHeightAdvantage.CoverBlockMultiplier(2) - .4f) < .0001f
            && Mathf.Abs(VerticalHeightAdvantage.CoverBlockMultiplier(3)) < .0001f,
            "Height-based cover penalty reduction");
        Log.Message("[COLONY PROBE] Height bonuses 0/1/2/3+ (range and cover) PASS");
        high.terrainGrid.SetTerrain(new IntVec3(23, 0, 24), VerticalDefOf.DeepRimVertical_UpperDeck);
        worker.DeSpawn(); GenSpawn.Spawn(worker, new IntVec3(23, 0, 24), high); worker.drafter.Drafted = true;
        worker.equipment.DestroyAllEquipment();
        worker.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle")));
        worker.skills.GetSkill(SkillDefOf.Shooting).Level = 20;
        var enemyFaction = Find.FactionManager.AllFactionsListForReading.First(f => !f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            combatTarget = PawnGenerator.GeneratePawn(PawnKindDefOf.Pirate, enemyFaction);
            if (!combatTarget.WorkTagIsDisabled(WorkTags.Violent)) break;
            combatTarget.Destroy(); combatTarget = null;
        }
        Require(combatTarget != null, "No combat-capable enemy fixture");
        GenSpawn.Spawn(combatTarget, new IntVec3(26, 0, 24), surface.Map);
        Require(combatTarget.HostileTo(worker), "Combat test spawned a neutral pawn");
        foreach (var apparel in combatTarget.apparel.WornApparel.ToList()) { combatTarget.apparel.Remove(apparel); apparel.Destroy(); }
        foreach (var injury in combatTarget.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) combatTarget.health.RemoveHediff(injury);
        combatTarget.equipment.DestroyAllEquipment();
        combatTarget.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle")));
        combatTarget.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 100000));
        var verb = worker.TryGetAttackVerb(combatTarget, true);
        Log.Message("[COLONY PROBE] Initial LOS: down=" + verb.CanHitTarget(combatTarget)
            + " up=" + combatTarget.TryGetAttackVerb(worker, true).CanHitTarget(worker)
            + " enemyViolenceDisabled=" + combatTarget.WorkTagIsDisabled(WorkTags.Violent)
            + " targetFog=" + combatTarget.Position.Fogged(surface.Map));
        Require(verb.CanHitTarget(combatTarget) && combatTarget.TryGetAttackVerb(worker, true).CanHitTarget(worker), "Bidirectional open-floor line of fire");
        var originalRange = verb.verbProps.range;
        try
        {
            verb.verbProps.range = 3.5f;
            Require(VerticalSight.CanShoot(verb, worker.Position, combatTarget),
                "Downward shot was rejected despite higher-ground extended range");
            Require(!VerticalSight.CanShoot(combatTarget.TryGetAttackVerb(worker, true), combatTarget.Position, worker),
                "Upward shot received an unintended higher-ground range bonus");
        }
        finally { verb.verbProps.range = originalRange; }

        VerticalColonyUtility.TrySite(high, out var combatSite);
        var crossing = VerticalSight.Segments(combatSite, 1, 0, worker.Position.ToVector3Shifted(), combatTarget.Position.ToVector3Shifted()).First(s => s.NextMap != null).End.ToIntVec3();
        high.terrainGrid.SetTerrain(crossing, VerticalDefOf.DeepRimVertical_UpperDeck);
        Require(!verb.CanHitTarget(combatTarget), "Solid deck was penetrated");
        high.terrainGrid.RemoveFoundation(crossing, false);
        high.terrainGrid.SetTerrain(crossing, VerticalDefOf.DeepRimVertical_UpperVoid);
        surface.Map.roofGrid.SetRoof(crossing, RoofDefOf.RoofConstructed);
        Require(!verb.CanHitTarget(combatTarget), "Lower roof was penetrated"); surface.Map.roofGrid.SetRoof(crossing, null);
        var wall = Spawn("Wall", surface.Map, 25, 24); Require(!verb.CanHitTarget(combatTarget), "Intermediate wall was penetrated"); wall.Destroy();
        foreach (var segment in VerticalSight.Segments(combatSite, 1, 0, worker.Position.ToVector3Shifted(), combatTarget.Position.ToVector3Shifted()))
            Log.Message("[COLONY PROBE] Ray " + segment.Map.uniqueID + " " + segment.Start.ToIntVec3() + " -> " + segment.End.ToIntVec3()
                + " terrain=" + high.terrainGrid.TerrainAt(segment.End.ToIntVec3()).defName + " roof=" + segment.End.ToIntVec3().GetRoof(surface.Map)
                + " upperEdifice=" + segment.End.ToIntVec3().GetEdifice(high) + " lowerEdifice=" + segment.End.ToIntVec3().GetEdifice(surface.Map)
                + " open=" + (segment.NextMap == null || VerticalSight.Open(combatSite, segment.LowerBoundary, segment.End.ToIntVec3())));
        var selectedTarget = AttackTargetFinder.BestAttackTarget(worker, TargetScanFlags.NeedThreat, maxDist: 50, onlyRanged: true);
        foreach (var candidate in surface.Map.attackTargetsCache.GetPotentialTargetsFor(worker).ToArray())
            Log.Message("[COLONY PROBE] Candidate=" + candidate.Thing + " map=" + candidate.Thing.Map?.uniqueID
                + " hostile=" + worker.HostileTo(candidate.Thing) + " disabled=" + candidate.ThreatDisabled(worker)
                + " canHit=" + worker.CurrentEffectiveVerb.CanHitTarget(candidate.Thing) + " distance=" + VerticalSight.Distance(worker, candidate.Thing));
        IAttackTarget directTarget = null;
        VerticalAttackTargetPatch.Postfix(worker, TargetScanFlags.NeedThreat, null, 0, 50, default, float.MaxValue, ref directTarget);
        Log.Message("[COLONY PROBE] Target scan: selected=" + selectedTarget?.Thing + " potential=" + surface.Map.attackTargetsCache.GetPotentialTargetsFor(worker).Count
            + " threatDisabled=" + combatTarget.ThreatDisabled(worker) + " canHitAfterWall=" + verb.CanHitTarget(combatTarget)
            + " effectiveVerb=" + worker.CurrentEffectiveVerb?.GetType().Name + " direct=" + directTarget?.Thing);
        Require(selectedTarget?.Thing == combatTarget, "Automatic cross-floor target selection");
        worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 100000));
        // Isolated fixture only: a slow round makes in-flight capture deterministic.
        ((Verb_LaunchProjectile)verb).Projectile.projectile.speed = 5;
        Require(VerticalCombatCommands.Attack(worker, combatTarget), "Ordered cross-floor AttackStatic job");
        Log.Message("[COLONY PROBE] Open-floor LOS, solid decks/roofs/walls, auto target and manual combat job PASS");
    }
    private void TestCeilingOwnership(Map high)
    {
        var lower = surface.Map;
        var ceiling = lower.GetComponent<VerticalCeilingComponent>();
        Require(ceiling != null, "Vertical ceiling map component not initialized");
        var fresh = new IntVec3(29, 0, 27);
        var native = new IntVec3(30, 0, 27);
        Require(lower.roofGrid.RoofAt(fresh) == null, "Ceiling fixture must start without roof");
        high.terrainGrid.SetTerrain(fresh, VerticalDefOf.DeepRimVertical_UpperDeck);
        ceiling.MapComponentTick();
        Require(ceiling.Owns(fresh) && lower.roofGrid.RoofAt(fresh) == RoofDefOf.RoofConstructed,
            "Upper deck did not create lower owned ceiling");
        high.terrainGrid.RemoveFoundation(fresh, false);
        high.terrainGrid.SetTerrain(fresh, VerticalDefOf.DeepRimVertical_UpperVoid);
        ceiling.MapComponentTick();
        Require(!ceiling.Owns(fresh) && lower.roofGrid.RoofAt(fresh) == null,
            "Removing upper deck did not remove owned ceiling");

        lower.roofGrid.SetRoof(native, RoofDefOf.RoofConstructed);
        high.terrainGrid.SetTerrain(native, VerticalDefOf.DeepRimVertical_UpperDeck);
        ceiling.MapComponentTick();
        Require(!ceiling.Owns(native), "Existing native roof was incorrectly claimed");
        high.terrainGrid.RemoveFoundation(native, false);
        high.terrainGrid.SetTerrain(native, VerticalDefOf.DeepRimVertical_UpperVoid);
        ceiling.MapComponentTick();
        Require(lower.roofGrid.RoofAt(native) == RoofDefOf.RoofConstructed,
            "Upper deck removal destroyed native player roof");
        lower.roofGrid.SetRoof(native, null);

        var wallCell = new IntVec3(29, 0, 26);
        Require(lower.roofGrid.RoofAt(wallCell) == null, "Upper block fixture must start without roof");
        var block = Spawn("Wall", high, wallCell.x, wallCell.z);
        ceiling.MapComponentTick();
        Require(ceiling.Owns(wallCell) && lower.roofGrid.RoofAt(wallCell) == RoofDefOf.RoofConstructed,
            "Solid upper block did not create lower ceiling");
        block.Destroy();
        ceiling.MapComponentTick();
        Require(!ceiling.Owns(wallCell) && lower.roofGrid.RoofAt(wallCell) == null,
            "Solid upper block removal did not remove owned ceiling");

        var natural = new IntVec3(30, 0, 26);
        lower.roofGrid.SetRoof(natural, RoofDefOf.RoofRockThick);
        high.terrainGrid.SetTerrain(natural, VerticalDefOf.DeepRimVertical_UpperDeck);
        ceiling.MapComponentTick();
        Require(!ceiling.Owns(natural) && lower.roofGrid.RoofAt(natural) == RoofDefOf.RoofRockThick,
            "Native rock roof was replaced or claimed by deck");
        high.terrainGrid.RemoveFoundation(natural, false);
        high.terrainGrid.SetTerrain(natural, VerticalDefOf.DeepRimVertical_UpperVoid);
        ceiling.MapComponentTick();
        Require(lower.roofGrid.RoofAt(natural) == RoofDefOf.RoofRockThick,
            "Natural roof was deleted with the upper deck");
        lower.roofGrid.SetRoof(natural, null);
        Log.Message("[COLONY PROBE] Upper deck/full block ceilings, removal and native/player roof preservation PASS");
    }
    private void MoveWorker(Map map, IntVec3 cell)
    {
        worker.jobs.StopAll(); worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out _);
        worker.DeSpawn(); GenSpawn.Spawn(worker, cell, map);
        foreach (var need in worker.needs.AllNeeds) need.CurLevelPercentage = 1;
    }
    private void RunWork(string label, WorkTypeDef type, Map destination, Func<bool> complete)
    {
        foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) worker.workSettings.SetPriority(work, 0);
        worker.workSettings.SetPriority(type, 1); StartWork(label);
        for (var tick = 0; tick < 16000 && !complete(); tick++) Find.TickManager.DoSingleTick();
        Require(complete() && worker.Map == destination, label + " did not complete on its actual map");
        Log.Message("[COLONY PROBE] Native remote " + label + " PASS");
    }
    private void TestAdditionalWork()
    {
        MoveWorker(surface.Map, new IntVec3(23, 0, 24));
        var repair = new IntVec3(46, 0, 28).GetEdifice(bottom.Map); repair.HitPoints -= 20;
        RunWork("repair", WorkTypeDefOf.Construction, bottom.Map, () => repair.HitPoints == repair.MaxHitPoints);
        var cleanCell = new IntVec3(36, 0, 27); middle.Map.areaManager.Home[cleanCell] = true;
        Require(FilthMaker.TryMakeFilth(cleanCell, middle.Map, ThingDefOf.Filth_Dirt), "Test filth");
        RunWork("cleaning", WorkTypeDefOf.Cleaning, middle.Map, () => cleanCell.GetFirstThing(middle.Map, ThingDefOf.Filth_Dirt) == null);
        var patient = surface.Map.mapPawns.FreeColonists.First(p => p != worker);
        patient.jobs.StopAll(); patient.DeSpawn(); GenSpawn.Spawn(patient, bed.Position, bottom.Map);
        bed.Medical = true; patient.playerSettings.medCare = MedicalCareCategory.NoMeds;
        var arm = patient.RaceProps.body.AllParts.First(p => p.def == BodyPartDefOf.Arm);
        patient.health.AddHediff(HediffDefOf.Cut, arm).Severity = 1;
        patient.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed));
        worker.skills.GetSkill(SkillDefOf.Medicine).Level = 20;
        Require(HealthAIUtility.ShouldBeTendedNowByPlayer(patient), "Patient did not need tending");
        RunWork("tending", WorkTypeDefOf.Doctor, bottom.Map, () => !HealthAIUtility.ShouldBeTendedNowByPlayer(patient));
        var researchBench = (Building_ResearchBench)Spawn("SimpleResearchBench", surface.Map, 30, 30);
        var project = DefDatabase<ResearchProjectDef>.AllDefs.First(p => !p.IsFinished && p.CanBeResearchedAt(researchBench, false));
        Find.ResearchManager.SetCurrentProject(project);
        var before = Find.ResearchManager.GetProgress(project); worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
        RunWork("research", WorkTypeDefOf.Research, surface.Map, () => Find.ResearchManager.GetProgress(project) > before);
        worker.jobs.StopAll();
        foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) worker.workSettings.SetPriority(work, 0);
    }
    private void SetupRefuel()
    {
        worker.jobs.StopAll(); foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) worker.workSettings.SetPriority(work, 0);
        worker.workSettings.SetPriority(WorkTypeDefOf.Hauling, 1);
        fuelTarget = Spawn("Campfire", middle.Map, 42, 27);
        var fuel = fuelTarget.TryGetComp<CompRefuelable>(); fuel.ConsumeFuel(fuel.Fuel); fuel.TargetFuelLevel = 20;
        Require(Total(ThingDefOf.WoodLog) == 35, "Refuel baseline wood");
        StartWork("remote fuel supply");
    }
    private void SetupReverseShot()
    {
        worker.jobs.StopAll(); worker.stances.CancelBusyStanceHard();
        upper.Map.terrainGrid.SetTerrain(new IntVec3(22, 0, 24), VerticalDefOf.DeepRimVertical_UpperDeck);
        reverseTarget = Spawn("Wall", upper.Map, 22, 24);
        var reverse = (Projectile)ThingMaker.MakeThing(worker.TryGetAttackVerb(null, true) is Verb_LaunchProjectile verb ? verb.Projectile : null);
        GenSpawn.Spawn(reverse, new IntVec3(26, 0, 24), surface.Map);
        reverse.GetComp<CompVerticalFlight>().Begin(surface.Map, upper.Map);
        reverse.Launch(worker, new IntVec3(26, 0, 24).ToVector3Shifted(), reverseTarget, reverseTarget, ProjectileHitFlags.IntendedTarget, true, worker.equipment.Primary);
    }
    private void TestClosedRoute()
    {
        worker.jobs.StopAll();
        var walls = Enumerable.Range(20, 13).Select(z => Spawn("Wall", middle.Map, 34, z)).ToList();
        middle.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        Require(VerticalRouteUtility.Find(worker, bottom.Map, worker.Position, surface.Map, new IntVec3(24, 0, 24), PathEndMode.OnCell) == null, "Blocked intermediate walk was crossed");
        foreach (var wall in walls) wall.Destroy();
        middle.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        Require(VerticalRouteUtility.Find(worker, bottom.Map, worker.Position, surface.Map, new IntVec3(24, 0, 24), PathEndMode.OnCell) != null, "Reopened intermediate walk remained blocked");
        var stair = middle.Map.listerThings.AllThings.OfType<Building_VerticalStair>().First(s => s.LinkedStair?.Map == bottom.Map);
        stair.SetForbidden(true, false);
        Require(stair.IsForbidden(Faction.OfPlayer), "Stair is missing its native forbid component");
        Require(VerticalRouteUtility.Find(worker, bottom.Map, worker.Position, surface.Map, new IntVec3(24, 0, 24), PathEndMode.OnCell) == null, "Forbidden stair was crossed");
        stair.SetForbidden(false, false);
        Require(VerticalRouteUtility.Find(worker, bottom.Map, worker.Position, surface.Map, new IntVec3(24, 0, 24), PathEndMode.OnCell) != null, "Allowed stair remained blocked");
        stair.Destroy();
        Require(VerticalRouteUtility.Find(worker, bottom.Map, worker.Position, surface.Map, new IntVec3(24, 0, 24), PathEndMode.OnCell) == null, "Missing stair was crossed");
        Log.Message("[COLONY PROBE] Blocked intermediate walk, forbidden and missing stair rejection PASS");
    }
    private void TestStoragePriorities()
    {
        var stored = bottom.Map.listerThings.ThingsOfDef(ThingDefOf.Steel).First(t => t.IsInValidStorage());
        var origin = stored.GetSlotGroup(); origin.Settings.Priority = StoragePriority.Normal;
        foreach (var targetMap in new[] { middle.Map, surface.Map })
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, targetMap.zoneManager);
            targetMap.zoneManager.RegisterZone(zone); zone.AddCell(new IntVec3(28, 0, 27));
            zone.settings.filter.SetDisallowAll(); zone.settings.filter.SetAllow(ThingDefOf.Steel, true);
            zone.settings.Priority = targetMap == surface.Map ? StoragePriority.Critical : StoragePriority.Preferred;
        }
        var plan = (VerticalTaskRecord)typeof(VerticalWorkSearch).GetMethod("FindHaul", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null,
            new object[] { worker, Find.World.GetComponent<VerticalSiteWorldComponent>().Sites.First(), DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral") });
        Require(plan?.source == stored && plan.destinationMap == surface, "A lower-priority earlier floor displaced the best remote stockpile");
        origin.Settings.Priority = StoragePriority.Critical;
        Log.Message("[COLONY PROBE] Global remote stockpile priority PASS");
    }
    private void TestProfiler()
    {
        Require(VerticalProfiler.Start(out var reason, 30), "Native profiler installation: " + reason);
        Require(VerticalProfilerPatches.InstalledCount == 23, "Native profiler hook count");
        for (var tick = 0; tick < 200; tick++) Find.TickManager.DoSingleTick();
        VerticalProfiler.Stop("routing-probe");
        var snapshot = VerticalProfiler.Snapshot();
        Require(snapshot != null && snapshot.Ticks == 200, "Native profiler tick count");
        foreach (var map in new[] { surface.Map, middle.Map, bottom.Map, upper.Map })
            Require(snapshot.Rows.Any(r => r.Key.MapId == map.uniqueID && r.Stats.Calls > 0), "Missing hidden-floor profiling rows");
        Require(File.Exists(VerticalProfiler.LastCsvPath) && File.Exists(VerticalProfiler.LastTextPath)
            && VerticalProfilerPatches.InstalledCount == 0, "Profiler exports/cleanup");
        Log.Message("[COLONY PROBE] Native profiler: four floors, 23 hooks, 200 ticks, exports and cleanup PASS");
    }
    private void TestDraftTransfer()
    {
        worker.jobs.StopAll(); worker.drafter.Drafted = true;
        var obstruction = Spawn("Wall", worker.Map, 46, 29);
        Require(GameComponent_VerticalTasks.Get().QueueTravel(worker, new IntVec3(46, 0, 29), middle.Map, -1), "Remote goal incorrectly checked on the pawn's current map");
        for (var tick = 0; tick < 2000 && (worker.Map != middle.Map || worker.Position != new IntVec3(46, 0, 29) || GameComponent_VerticalTasks.Get().HasTask(worker)); tick++) Find.TickManager.DoSingleTick();
        Require(worker.Map == middle.Map && worker.Position == new IntVec3(46, 0, 29) && worker.Drafted, "Travel with different destination geometry failed");
        obstruction.Destroy();
        foreach (var destination in new[] { middle.Map, bottom.Map })
        {
            Require(GameComponent_VerticalTasks.Get().QueueTravel(worker, new IntVec3(44, 0, 24), destination, 0), "Drafted ordered travel");
            for (var tick = 0; tick < 2000 && (worker.Map != destination || GameComponent_VerticalTasks.Get().HasTask(worker)); tick++) Find.TickManager.DoSingleTick();
            Require(worker.Map == destination && worker.Drafted && !GameComponent_VerticalTasks.Get().HasTask(worker), "Draft status lost in transfer");
        }
        worker.drafter.Drafted = false;
        Log.Message("[COLONY PROBE] Drafted manual round trip preserves draft status PASS");
    }
    private void TestCargoCancellation()
    {
        worker.jobs.StopAll();
        Require(VerticalStairService.Connect((Building_VerticalStair)Spawn("DeepRimVertical_StairsDown", middle.Map, 44, 24)), "Repaired connection");
        var cargo = Spawn("Steel", bottom.Map, 45, 30, 7);
        Require(worker.carryTracker.TryStartCarry(cargo, 7) == 7, "Cancellation test cargo");
        var plan = new VerticalTaskRecord
        {
            pawn = worker, kind = VerticalTaskKind.Haul, stage = VerticalTaskStage.ToDestination,
            destinationMap = surface, destinationCell = new IntVec3(28, 0, 27), count = 7,
            workGiver = DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral")
        };
        var job = GameComponent_VerticalTasks.Get().Accept(plan); Require(job != null, "Cancellation test route");
        worker.jobs.StartJob(job, keepCarryingThingOverride: true, preToilReservationsCanFail: true);
        middle.Map.listerThings.AllThings.OfType<Building_VerticalStair>().First(s => s.LinkedStair?.Map == bottom.Map).Destroy();
        for (var tick = 0; tick < 2000 && GameComponent_VerticalTasks.Get().HasTask(worker); tick++) Find.TickManager.DoSingleTick();
        Require(!GameComponent_VerticalTasks.Get().HasTask(worker) && worker.carryTracker.CarriedThing == null
            && Total(ThingDefOf.Steel) == 47, "Closed-route cancellation lost cargo or ownership");
        Log.Message("[COLONY PROBE] Destroyed route cancels safely and preserves all seven carried units PASS");
    }
    private int Total(ThingDef def) => new[] { surface.Map, middle.Map, bottom.Map }.Sum(m => m.listerThings.ThingsOfDef(def).Sum(t => t.stackCount))
        + (worker.carryTracker.CarriedThing?.def == def ? worker.carryTracker.CarriedThing.stackCount : 0);
    private void SaveReload(string name)
    {
        GameDataSaveLoader.SaveGame(name); Require(File.Exists(GenFilePaths.FilePathForSavedGame(name)), "Native save file");
        Log.Message("[COLONY PROBE] Reload " + name); GameDataSaveLoader.LoadGame(name);
    }
}
