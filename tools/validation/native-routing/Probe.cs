// Opt-in test mod, never included in DeepRim's production assembly.
using System;
using System.IO;
using System.Linq;
using DeepRim.Vertical.VerticalConstruction;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalRouting;
using DeepRim.Vertical.VerticalProfiling;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

public sealed class VerticalRoutingNativeProbe : GameComponent
{
    private int phase, elapsed;
    private bool reloadedMining, reloadedCargo;
    private Pawn worker;
    private MapParent surface, middle, bottom;
    private Thing mineable, steel, blueprint;
    public VerticalRoutingNativeProbe(Game game) { Log.Message("[ROUTING PROBE] Created"); }
    public override void ExposeData()
    {
        Scribe_Values.Look(ref phase, "probePhase"); Scribe_Values.Look(ref elapsed, "probeElapsed");
        Scribe_Values.Look(ref reloadedMining, "reloadedMining"); Scribe_Values.Look(ref reloadedCargo, "reloadedCargo");
        Scribe_References.Look(ref worker, "worker"); Scribe_References.Look(ref surface, "surface");
        Scribe_References.Look(ref middle, "middle"); Scribe_References.Look(ref bottom, "bottom");
        Scribe_References.Look(ref mineable, "mineable"); Scribe_References.Look(ref steel, "steel"); Scribe_References.Look(ref blueprint, "blueprint");
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
        if (!GenFilePaths.SaveDataFolderPath.EndsWith("native-smoke-routing", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (phase == 0) { Setup(); return; }
            if (phase == 1)
            {
                if (++elapsed % 300 == 0) Log.Message("[ROUTING PROBE] Waiting for stair links");
                Require(elapsed < 3000, "Stair linking timed out");
                if (surface.Map.listerThings.AllThings.OfType<Building_VerticalStair>().Any(s => s.LinkedStair == null)
                    || middle.Map.listerThings.AllThings.OfType<Building_VerticalStair>().Any(s => s.LinkedStair == null)) return;
                foreach (var map in new[] { surface.Map, middle.Map, bottom.Map }) map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                Require(VerticalRouteUtility.Find(worker, surface.Map, worker.Position, bottom.Map, mineable, PathEndMode.Touch)?.Count == 2, "Two-hop route with different intermediate stair cells");
                var originalMap = worker.Map;
                Require(VerticalRouteUtility.Reach(worker, bottom.Map, new IntVec3(44, 0, 24), mineable, PathEndMode.Touch) && worker.Map == originalMap, "Remote preflight must retain pawn map");
                StartWork("mining"); phase = 2; elapsed = 0;
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
                        Log.Message("[ROUTING PROBE] Autonomous mining + intermediate walk + load PASS");
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
                        Log.Message("[ROUTING PROBE] Remote stockpile + cargo/load + conservation PASS");
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
                        Log.Message("[ROUTING PROBE] Cross-floor blueprint supply + native completion PASS");
                        TestStoragePriorities();
                        TestQueue(); phase = 5; elapsed = 0;
                    }
                }
                else if (phase == 5 && worker.Map == bottom.Map && worker.Position == new IntVec3(45, 0, 29) && worker.jobs.jobQueue.Count == 0)
                {
                    Log.Message("[ROUTING PROBE] Queued cell order returns to its original map PASS");
                    TestDraftTransfer();
                    TestClosedRoute();
                    TestCargoCancellation();
                    TestProfiler();
                    File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "PASS: native three-floor routing, autonomous mining and intermediate walking, two en-route save/load cycles, physical hauling and resource claims, global storage priorities, blueprint supply/native construction, cargo/material conservation, original-map queued cell orders, drafted manual transfer, blocked/forbidden/missing routes, cancellation with carried cargo, native profiler hooks/exports/cleanup.");
                    Log.Message("[ROUTING PROBE] PASS"); Application.Quit(); return;
                }
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "FAIL: " + error);
            Log.Error("[ROUTING PROBE] FAIL " + error); Application.Quit();
        }
    }
    private void Setup()
    {
        Log.Message("[ROUTING PROBE] Setup");
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
        worker = upper.mapPawns.FreeColonists.First(p => p.DevelopmentalStage == DevelopmentalStage.Adult
            && !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
            && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling));
        foreach (var pawn in upper.mapPawns.FreeColonists)
        {
            pawn.jobs.StopAll();
            foreach (var work in DefDatabase<WorkTypeDef>.AllDefs) pawn.workSettings.SetPriority(work, 0);
            if (pawn != worker) pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 100000));
        }
        worker.Position = new IntVec3(23, 0, 24);
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
        Log.Message("[ROUTING PROBE] Waiting for links on three floors");
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
            Log.Message("[ROUTING PROBE] No job: eligible=" + VerticalWorkSearch.Eligible(worker)
                + " carry=" + worker.carryTracker.CarriedThing + " haulingDisabled=" + worker.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
                + " givers=" + string.Join(",", worker.workSettings.WorkGiversInOrderNormal.Select(g => g.def.defName)));
            if (steel?.Spawned == true)
                Log.Message("[ROUTING PROBE] Steel allowed=" + VerticalRouteUtility.Allowed(worker, steel)
                    + " listed=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Contains(steel)
                    + " pickupRoute=" + VerticalRouteUtility.Find(worker, worker.Map, worker.Position, steel.Map, steel, PathEndMode.ClosestTouch, true)?.Count
                    + " cargoRoute=" + VerticalRouteUtility.Find(worker, steel.Map, steel.Position, bottom.Map, new IntVec3(45, 0, 27), PathEndMode.OnCell, true)?.Count
                    + " storage=" + VerticalWorkSearch.ValidStoreCell(worker, bottom.Map, new IntVec3(45, 0, 27), steel));
            if (steel?.Spawned == true)
            {
                Log.Message("[ROUTING PROBE] Candidate count=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Count
                    + " steelIndex=" + steel.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).IndexOf(steel)
                    + " always=" + steel.def.alwaysHaulable + " floorDistance=" + DeepRim.Vertical.VerticalRuntime.Settings.crossLevelHaulMaxFloorDistance);
                foreach (var group in bottom.Map.haulDestinationManager.AllGroupsListInPriorityOrder)
                    Log.Message("[ROUTING PROBE] Store group priority=" + group.Settings.Priority + " cells=" + group.CellsList.Count + " accepts=" + group.parent.Accepts(steel));
                Log.Message("[ROUTING PROBE] Source usable=" + typeof(VerticalWorkSearch).GetMethod("SourceUsable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { worker, steel })
                    + " capacity=" + worker.carryTracker.MaxStackSpaceEver(steel.def) + " currentPriority=" + StoreUtility.CurrentStoragePriorityOf(steel)
                    + " haulEnabled=" + DeepRim.Vertical.VerticalRuntime.Settings.allowCrossLevelHauling
                    + " haulPlan=" + typeof(VerticalWorkSearch).GetMethod("FindHaul", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null,
                        new object[] { worker, Find.World.GetComponent<VerticalSiteWorldComponent>().Sites.First(), DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral") }));
            }
        }
        Require(result.Job != null, "No automatic " + expected + " job");
        Log.Message("[ROUTING PROBE] Start " + expected + ": " + result.Job);
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
        Log.Message("[ROUTING PROBE] Blocked intermediate walk, forbidden and missing stair rejection PASS");
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
        Log.Message("[ROUTING PROBE] Global remote stockpile priority PASS");
    }
    private void TestProfiler()
    {
        Require(VerticalProfiler.Start(out var reason, 30), "Native profiler installation: " + reason);
        Require(VerticalProfilerPatches.InstalledCount == 23, "Native profiler hook count");
        for (var tick = 0; tick < 200; tick++) Find.TickManager.DoSingleTick();
        VerticalProfiler.Stop("routing-probe");
        var snapshot = VerticalProfiler.Snapshot();
        Require(snapshot != null && snapshot.Ticks == 200, "Native profiler tick count");
        foreach (var map in new[] { surface.Map, middle.Map, bottom.Map })
            Require(snapshot.Rows.Any(r => r.Key.MapId == map.uniqueID && r.Stats.Calls > 0), "Missing hidden-floor profiling rows");
        Require(File.Exists(VerticalProfiler.LastCsvPath) && File.Exists(VerticalProfiler.LastTextPath)
            && VerticalProfilerPatches.InstalledCount == 0, "Profiler exports/cleanup");
        Log.Message("[ROUTING PROBE] Native profiler: three floors, 23 hooks, 200 ticks, exports and cleanup PASS");
    }
    private void TestDraftTransfer()
    {
        worker.jobs.StopAll(); worker.drafter.Drafted = true;
        foreach (var destination in new[] { middle.Map, bottom.Map })
        {
            Require(GameComponent_VerticalTasks.Get().QueueTravel(worker, new IntVec3(44, 0, 24), destination, 0), "Drafted ordered travel");
            for (var tick = 0; tick < 2000 && (worker.Map != destination || GameComponent_VerticalTasks.Get().HasTask(worker)); tick++) Find.TickManager.DoSingleTick();
            Require(worker.Map == destination && worker.Drafted && !GameComponent_VerticalTasks.Get().HasTask(worker), "Draft status lost in transfer");
        }
        worker.drafter.Drafted = false;
        Log.Message("[ROUTING PROBE] Drafted manual round trip preserves draft status PASS");
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
        Log.Message("[ROUTING PROBE] Destroyed route cancels safely and preserves all seven carried units PASS");
    }
    private int Total(ThingDef def) => new[] { surface.Map, middle.Map, bottom.Map }.Sum(m => m.listerThings.ThingsOfDef(def).Sum(t => t.stackCount))
        + (worker.carryTracker.CarriedThing?.def == def ? worker.carryTracker.CarriedThing.stackCount : 0);
    private void SaveReload(string name)
    {
        GameDataSaveLoader.SaveGame(name); Require(File.Exists(GenFilePaths.FilePathForSavedGame(name)), "Native save file");
        Log.Message("[ROUTING PROBE] Reload " + name); GameDataSaveLoader.LoadGame(name);
    }
}
