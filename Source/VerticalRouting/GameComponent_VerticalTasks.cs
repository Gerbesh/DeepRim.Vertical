using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.VerticalWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public sealed class GameComponent_VerticalTasks : GameComponent
{
    private List<VerticalTaskRecord> tasks = new();
    private List<VerticalQueuedBinding> queuedMaps = new();
    private readonly Dictionary<Pawn, int> lastSearch = new();
    private readonly Dictionary<Pawn, int> lastEmergencySearch = new();
    private readonly Dictionary<Pawn, int> lastFoodSearch = new();
    private bool advancing;
    public GameComponent_VerticalTasks(Game game) { }
    public static GameComponent_VerticalTasks Get() => Current.Game?.GetComponent<GameComponent_VerticalTasks>();
    public IEnumerable<VerticalTaskRecord> Tasks => tasks;
    public bool HasTask(Pawn pawn) => tasks.Any(t => t.pawn == pawn);
    public bool CanSearchFood(Pawn pawn)
    {
        var tick = Find.TickManager.TicksGame;
        if (lastFoodSearch.TryGetValue(pawn, out var previous) && tick - previous < 180) return false;
        lastFoodSearch[pawn] = tick; return true;
    }
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref tasks, "verticalTasks", LookMode.Deep);
        Scribe_Collections.Look(ref queuedMaps, "verticalQueuedMaps", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { tasks ??= new(); queuedMaps ??= new(); lastSearch.Clear(); lastEmergencySearch.Clear(); lastFoodSearch.Clear(); }
    }
    public bool CanSearch(Pawn pawn, bool emergency = false)
    {
        var current = tasks.FirstOrDefault(t => t.pawn == pawn);
        if (current != null && (!emergency || current.forced || current.workGiver?.emergency == true)) return false;
        var tick = Find.TickManager.TicksGame;
        var searches = emergency ? lastEmergencySearch : lastSearch;
        if (searches.TryGetValue(pawn, out var previous) && tick - previous < 180) return false;
        searches[pawn] = tick;
        return true;
    }
    public override void GameComponentTick()
    {
        foreach (var task in tasks.ToList())
        {
            var pawn = task.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.InMentalState
                || !task.forced && pawn.Drafted || !TaskTargetValid(task)) { Cancel(task); continue; }
            if (pawn.CurJob?.loadID != task.expectedJobId)
            {
                if (Find.TickManager.TicksGame - task.createdTick > 1) Cancel(task);
                continue;
            }
            if (!task.ready) continue;
            try { Advance(task); }
            catch (Exception error)
            {
                Log.Error("[DeepRim Vertical] Task failed safely: " + error);
                Cancel(task);
            }
        }
        if (Find.TickManager.TicksGame % 120 == 0)
        {
            queuedMaps.RemoveAll(b => b.pawn == null || b.pawn.Destroyed || b.map?.Map == null
                || !b.pawn.jobs.jobQueue.Any(q => q.job.loadID == b.jobId));
            foreach (var pawn in lastSearch.Keys.Where(p => p == null || p.Destroyed || !p.Spawned).ToList()) lastSearch.Remove(pawn);
            foreach (var pawn in lastEmergencySearch.Keys.Where(p => p == null || p.Destroyed || !p.Spawned).ToList()) lastEmergencySearch.Remove(pawn);
            foreach (var pawn in lastFoodSearch.Keys.Where(p => p == null || p.Destroyed || !p.Spawned).ToList()) lastFoodSearch.Remove(pawn);
        }
    }
    private bool TaskTargetValid(VerticalTaskRecord task)
    {
        if (task.kind == VerticalTaskKind.Supply && task.destination?.Spawned != true && task.destinationMap?.Map != null)
            task.destination = task.destinationCell.GetThingList(task.destinationMap.Map).OfType<Frame>()
                .FirstOrDefault(f => f.BuildDef == task.destinationBuildDef && f.Stuff == task.destinationStuff && f.Faction == task.pawn.Faction);
        if (task.kind == VerticalTaskKind.Work) return VerticalRouteUtility.Allowed(task.pawn, task.destination);
        if (task.kind == VerticalTaskKind.Food) return task.pawn.needs.food != null && VerticalNeeds.FoodAllowed(task.pawn, task.destination);
        if (task.kind == VerticalTaskKind.Rest) return task.destination is Building_Bed bed && VerticalNeeds.BedAllowed(task.pawn, bed);
        if (task.kind is VerticalTaskKind.Haul or VerticalTaskKind.Supply or VerticalTaskKind.BillSupply or VerticalTaskKind.RefuelSupply)
        {
            if (!VerticalRuntime.Settings.allowCrossLevelHauling) return false;
            if (task.kind == VerticalTaskKind.RefuelSupply && (!VerticalRefuelSupply.Valid(task.destination)
                || !VerticalRouteUtility.Allowed(task.pawn, task.destination))) return false;
            if (task.kind == VerticalTaskKind.BillSupply && (!VerticalProductionSupply.BillValid(task)
                || !VerticalRouteUtility.Allowed(task.pawn, task.destination))) return false;
            if (task.kind == VerticalTaskKind.Supply && (!VerticalRouteUtility.Allowed(task.pawn, task.destination)
                || task.destination.Faction != task.pawn.Faction)) return false;
            if (task.stage == VerticalTaskStage.ToSource) return task.source?.Spawned == true && !task.source.Destroyed;
            if (task.pawn.carryTracker.CarriedThing == null) return false;
            return task.destinationMap?.Map != null && (task.kind != VerticalTaskKind.Supply || task.destination?.Spawned == true);
        }
        return task.destinationMap?.Map != null;
    }
    public Job Accept(VerticalTaskRecord task)
    {
        if (HasTask(task.pawn) || !TaskTargetValid(task)) return null;
        var job = NextJob(task);
        if (job == null) return null;
        task.expectedJobId = job.loadID; task.createdTick = Find.TickManager.TicksGame;
        tasks.Add(task); return job;
    }
    public bool QueueTravel(Pawn pawn, IntVec3 cell, Map destination, int targetLevel)
    {
        if (pawn?.Spawned != true || destination == null
            || VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, destination, cell,
                PathEndMode.OnCell, pawn.carryTracker.CarriedThing != null) == null) return false;
        var job = JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_Travel, cell);
        job.globalTarget = new GlobalTargetInfo(cell, destination);
        job.playerForced = true;
        return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
    public bool OnTravelStart(Pawn pawn, Job job)
    {
        if (tasks.Any(t => t.pawn == pawn && t.expectedJobId == job.loadID)) return true;
        if (!job.globalTarget.IsValid || job.globalTarget.Map == null || HasTask(pawn)) return false;
        var route = VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, job.globalTarget.Map,
            job.globalTarget.Cell, PathEndMode.OnCell, pawn.carryTracker.CarriedThing != null);
        if (route == null) return false;
        var task = new VerticalTaskRecord
        {
            pawn = pawn, kind = VerticalTaskKind.Travel, stage = VerticalTaskStage.ToDestination,
            destinationMap = job.globalTarget.Map.Parent, destinationCell = job.globalTarget.Cell, forced = true,
            expectedJobId = job.loadID, createdTick = Find.TickManager.TicksGame
        };
        if (route.Count > 0)
        {
            var hop = route[0]; task.hopCell = hop.Cell; task.hopDestination = hop.Destination.Parent; task.hopTargetLevel = hop.TargetLevel;
            job.targetA = hop.Cell; job.count = (int)Math.Ceiling(hop.Cost);
        }
        else { job.targetA = task.destinationCell; job.count = 1; }
        tasks.Add(task); return true;
    }
    public void CancelPawn(Pawn pawn)
    { foreach (var task in tasks.Where(t => t.pawn == pawn).ToList()) Cancel(task); }
    private void Cancel(VerticalTaskRecord task)
    {
        if (!tasks.Remove(task)) return;
        var pawn = task.pawn;
        if (pawn?.Spawned == true && (pawn.CurJob == null || pawn.CurJob.loadID == task.expectedJobId))
        {
            advancing = true;
            try { if (pawn.CurJob != null) pawn.jobs.EndCurrentJob(JobCondition.Incompletable, false); }
            finally { advancing = false; }
            if (pawn.carryTracker.CarriedThing != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
        }
        if (task.queuedJob != null) { JobMaker.ReturnToPool(task.queuedJob); task.queuedJob = null; }
    }
    public void JobEnded(Pawn pawn, Job job, JobCondition condition)
    {
        if (advancing || job == null) return;
        var task = tasks.FirstOrDefault(t => t.pawn == pawn && t.expectedJobId == job.loadID);
        if (task == null) return;
        // Cleanup is already in progress. Removing ownership must not recursively end the job.
        tasks.Remove(task);
        if (task.queuedJob != null) { JobMaker.ReturnToPool(task.queuedJob); task.queuedJob = null; }
    }
    public void MarkReady(Pawn pawn, int jobId)
    {
        var task = tasks.FirstOrDefault(t => t.pawn == pawn && t.expectedJobId == jobId);
        if (task != null)
        {
            task.ready = true;
            if (pawn.CurJob.def == VerticalRoutingDefOf.DeepRimVertical_Pickup && pawn.carryTracker.CarriedThing != null)
            { task.stage = VerticalTaskStage.ToDestination; task.count = pawn.carryTracker.CarriedThing.stackCount; }
        }
    }
    private void Advance(VerticalTaskRecord task)
    {
        var pawn = task.pawn;
        if (pawn.CurJob.def == VerticalRoutingDefOf.DeepRimVertical_Pickup)
        {
            if (pawn.carryTracker.CarriedThing == null) { Cancel(task); return; }
            task.stage = VerticalTaskStage.ToDestination;
            task.count = pawn.carryTracker.CarriedThing.stackCount;
        }
        else if (pawn.CurJob.def == VerticalRoutingDefOf.DeepRimVertical_Travel)
        {
            if (task.kind == VerticalTaskKind.Travel && pawn.Map == task.destinationMap?.Map && pawn.Position == task.destinationCell)
            { FinishLeg(pawn); tasks.Remove(task); return; }
            if (pawn.Position != task.hopCell || task.hopDestination?.Map == null
                || !VerticalSiteWorldComponent.TryGet(out var world) || !world.TryGetFloor(pawn.Map, out var site, out var floor))
            { Cancel(task); return; }
            var portal = site.portals.FirstOrDefault(p => p.sourceLevel == floor.levelIndex && p.targetLevel == task.hopTargetLevel && p.Cell == task.hopCell);
            if (portal == null || !VerticalRouteUtility.ValidHop(pawn, site, portal, pawn.carryTracker.CarriedThing != null))
            { Cancel(task); return; }
            var source = pawn.Map; var sourceCell = pawn.Position; var drafted = pawn.Drafted;
            RememberQueueMap(pawn, source.Parent);
            FinishLeg(pawn);
            try
            {
                // With curJob null, native DeSpawn skips StopAll. CarryTracker and queue survive.
                pawn.DeSpawn();
                GenSpawn.Spawn(pawn, task.hopCell, task.hopDestination.Map);
                if (drafted && pawn.drafter != null) pawn.drafter.Drafted = true;
            }
            catch
            {
                if (!pawn.Spawned) GenSpawn.Spawn(pawn, sourceCell, source);
                if (pawn.drafter != null) pawn.drafter.Drafted = drafted;
                throw;
            }
        }
        task.ready = false;
        var job = NextJob(task);
        if (job == null) { Cancel(task); return; }
        FinishLeg(pawn);
        task.expectedJobId = job.loadID; task.createdTick = Find.TickManager.TicksGame;
        advancing = true;
        try
        {
            pawn.jobs.StartJob(job, JobCondition.None, tag: task.forced ? JobTag.Misc : task.workGiver?.tagToGive ?? JobTag.Misc,
                keepCarryingThingOverride: true, preToilReservationsCanFail: true);
        }
        finally { advancing = false; }
        // Native local work owns its own reservations and continuation from here.
        if (task.kind == VerticalTaskKind.Work || task.kind == VerticalTaskKind.QueuedOrder || task.kind is VerticalTaskKind.Food or VerticalTaskKind.Rest
            || task.kind == VerticalTaskKind.Supply && job.def == JobDefOf.HaulToContainer
            || task.kind == VerticalTaskKind.RefuelSupply && job.def == VerticalRoutingDefOf.DeepRimVertical_RefuelCarried)
            if (job.def != VerticalRoutingDefOf.DeepRimVertical_Travel) tasks.Remove(task);
    }
    private void FinishLeg(Pawn pawn)
    {
        advancing = true;
        try { if (pawn.CurJob != null) pawn.jobs.EndCurrentJob(JobCondition.Succeeded, false); }
        finally { advancing = false; }
    }
    private Job NextJob(VerticalTaskRecord task)
    {
        var pawn = task.pawn;
        Map map; LocalTargetInfo target; PathEndMode mode;
        if (task.stage == VerticalTaskStage.ToSource)
        {
            if (!VerticalRouteUtility.Allowed(pawn, task.source)) return null;
            map = task.source.Map; target = task.source; mode = PathEndMode.ClosestTouch;
        }
        else
        {
            if (task.destination != null && !VerticalRouteUtility.Allowed(pawn, task.destination)) return null;
            map = task.destination?.Spawned == true ? task.destination.Map : task.destinationMap?.Map;
            target = task.destination != null ? task.destination : task.destinationCell;
            mode = task.kind == VerticalTaskKind.Work && task.workGiver?.Worker is WorkGiver_Scanner scanner
                ? scanner.PathEndMode : task.destination != null ? PathEndMode.Touch : PathEndMode.OnCell;
        }
        if (map == null) return null;
        var route = VerticalRouteUtility.Find(pawn, pawn.Map, pawn.Position, map, target, mode, pawn.carryTracker.CarriedThing != null || task.kind is VerticalTaskKind.Haul or VerticalTaskKind.Supply or VerticalTaskKind.BillSupply or VerticalTaskKind.RefuelSupply);
        if (route == null) return null;
        if (route.Count > 0)
        {
            var hop = route[0]; task.hopCell = hop.Cell; task.hopDestination = hop.Destination.Parent; task.hopTargetLevel = hop.TargetLevel;
            var travel = JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_Travel, hop.Cell);
            travel.count = (int)Math.Ceiling(hop.Cost); travel.playerForced = task.forced;
            return travel;
        }
        if (task.kind == VerticalTaskKind.Travel)
        {
            tasks.Remove(task);
            var arrival = JobMaker.MakeJob(JobDefOf.Goto, task.destinationCell);
            arrival.playerForced = task.forced;
            return arrival;
        }
        if (task.kind == VerticalTaskKind.Work)
        {
            if (!VerticalWorkSearch.CanUse(pawn, task.workGiver?.Worker)) return null;
            var scanner = task.workGiver.Worker as WorkGiver_Scanner;
            var work = scanner?.JobOnThing(pawn, task.destination);
            if (work != null) work.workGiverDef = task.workGiver;
            return work;
        }
        if (task.kind is VerticalTaskKind.Food or VerticalTaskKind.Rest) return VerticalNeeds.LocalJob(task);
        if (task.kind == VerticalTaskKind.QueuedOrder)
        {
            var queued = task.queuedJob;
            if (queued == null || !queued.CanBeginNow(pawn, false)) return null;
            task.queuedJob = null; return queued;
        }
        if (task.stage == VerticalTaskStage.ToSource)
        {
            if (!pawn.CanReserve(task.source, 1, task.count)) return null;
            var pickup = JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_Pickup, task.source);
            pickup.count = Math.Min(task.count, task.source.stackCount); return pickup;
        }
        var cargo = pawn.carryTracker.CarriedThing;
        if (cargo == null) return null;
        if (task.kind == VerticalTaskKind.RefuelSupply)
            return VerticalRefuelSupply.Valid(task.destination) ? JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_RefuelCarried, task.destination) : null;
        if (task.kind == VerticalTaskKind.BillSupply)
            return VerticalProductionSupply.BillValid(task)
                ? JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_DeliverIngredient, task.destinationCell, task.destination) : null;
        if (task.kind == VerticalTaskKind.Supply)
        {
            if (!(task.destination is IConstructible constructible) || constructible.ThingCountNeeded(cargo.def) <= 0) return null;
            var delivery = JobMaker.MakeJob(JobDefOf.HaulToContainer, cargo, task.destination);
            delivery.targetC = task.destination;
            delivery.count = Math.Min(cargo.stackCount, constructible.ThingCountNeeded(cargo.def));
            return delivery;
        }
        if (!VerticalWorkSearch.ValidStoreCell(pawn, map, task.destinationCell, cargo)) return null;
        return JobMaker.MakeJob(VerticalRoutingDefOf.DeepRimVertical_Deliver, task.destinationCell);
    }
    private void RememberQueueMap(Pawn pawn, MapParent map)
    {
        foreach (var queued in pawn.jobs.jobQueue)
            if (!queuedMaps.Any(b => b.pawn == pawn && b.jobId == queued.job.loadID))
                queuedMaps.Add(new VerticalQueuedBinding { pawn = pawn, jobId = queued.job.loadID, map = map });
    }
    public bool TryQueuedJob(Pawn pawn, ThinkNode source, JobIssueParams jobParams, bool inBedOnly, out ThinkResult result)
    {
        result = ThinkResult.NoJob;
        if (jobParams.ignoreQueue || pawn.Downed || !pawn.Spawned || pawn.jobs.jobQueue.Count == 0) return false;
        var queued = pawn.jobs.jobQueue.Peek();
        if (queued == null) return false;
        if (queued.job.def == VerticalRoutingDefOf.DeepRimVertical_Travel) return false;
        var binding = queuedMaps.FirstOrDefault(b => b.pawn == pawn && b.jobId == queued.job.loadID);
        var map = queued.job.targetA.Thing?.MapHeld ?? binding?.map?.Map;
        if (map == null || map == pawn.Map) return false;
        if (inBedOnly || HasTask(pawn)) return true;
        var task = new VerticalTaskRecord
        {
            pawn = pawn, kind = VerticalTaskKind.QueuedOrder, stage = VerticalTaskStage.ToDestination,
            destinationMap = map.Parent, destinationCell = queued.job.targetA.Cell,
            destination = queued.job.targetA.Thing, queuedJob = queued.job, forced = true
        };
        var job = Accept(task);
        pawn.jobs.jobQueue.Dequeue();
        if (binding != null) queuedMaps.Remove(binding);
        if (job == null) { queued.Cleanup(pawn, false); JobMaker.ReturnToPool(queued.job); return true; }
        result = new ThinkResult(job, source, queued.tag); return true;
    }
    public bool Claimed(Thing thing, Pawn except) => tasks.Any(t => t.pawn != except
        && (t.kind is VerticalTaskKind.Work or VerticalTaskKind.Food or VerticalTaskKind.Rest or VerticalTaskKind.RefuelSupply && t.destination == thing
            || t.stage == VerticalTaskStage.ToSource && t.source == thing));
    public int Promised(Thing thing, Pawn except) => tasks.Where(t => t.pawn != except && t.stage == VerticalTaskStage.ToSource && t.source == thing)
        .Where(t => !thing.Map.reservationManager.ReservationsReadOnly.Any(r => r.Claimant == t.pawn && r.Target.Thing == thing))
        .Sum(t => t.count);
    public bool ClaimedCell(Map map, IntVec3 cell, Pawn except) => tasks.Any(t => t.pawn != except
        && t.kind == VerticalTaskKind.Haul && t.destinationMap?.Map == map && t.destinationCell == cell);
    public int Incoming(Thing destination, ThingDef material) => tasks.Where(t => t.kind == VerticalTaskKind.Supply && t.destination == destination)
        .Where(t => (t.stage == VerticalTaskStage.ToSource ? t.source?.def : t.pawn?.carryTracker.CarriedThing?.def) == material).Sum(t => t.count);
}
