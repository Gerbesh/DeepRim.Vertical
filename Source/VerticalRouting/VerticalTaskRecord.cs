using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace DeepRim.Vertical.VerticalRouting;

public enum VerticalTaskKind { Travel, Work, Haul, Supply, QueuedOrder, Food, Rest, BillSupply, RefuelSupply }
public enum VerticalTaskStage { ToSource, ToDestination }

public sealed class VerticalTaskRecord : IExposable
{
    public Pawn pawn;
    public VerticalTaskKind kind;
    public VerticalTaskStage stage;
    public Thing source, destination;
    public BuildableDef destinationBuildDef;
    public ThingDef destinationStuff;
    public MapParent sourceMap, destinationMap;
    public IntVec3 destinationCell;
    public int count;
    public WorkGiverDef workGiver;
    public Bill bill;
    public int expectedJobId = -1;
    public bool ready;
    public int createdTick;
    public int hopTargetLevel;
    public IntVec3 hopCell;
    public MapParent hopDestination;
    public Job queuedJob;
    public bool forced;

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn");
        Scribe_References.Look(ref source, "source"); Scribe_References.Look(ref destination, "destination");
        Scribe_References.Look(ref sourceMap, "sourceMap"); Scribe_References.Look(ref destinationMap, "destinationMap");
        Scribe_References.Look(ref hopDestination, "hopDestination");
        Scribe_Defs.Look(ref workGiver, "workGiver");
        Scribe_References.Look(ref bill, "bill");
        var building = destinationBuildDef as ThingDef; var terrain = destinationBuildDef as TerrainDef;
        Scribe_Defs.Look(ref building, "destinationBuildingDef"); Scribe_Defs.Look(ref terrain, "destinationTerrainDef");
        if (Scribe.mode == LoadSaveMode.LoadingVars) destinationBuildDef = (BuildableDef)building ?? terrain;
        Scribe_Defs.Look(ref destinationStuff, "destinationStuff");
        Scribe_Values.Look(ref kind, "kind"); Scribe_Values.Look(ref stage, "stage");
        Scribe_Values.Look(ref destinationCell, "destinationCell"); Scribe_Values.Look(ref count, "count");
        Scribe_Values.Look(ref expectedJobId, "expectedJobId", -1); Scribe_Values.Look(ref ready, "ready");
        Scribe_Values.Look(ref hopTargetLevel, "hopTargetLevel"); Scribe_Values.Look(ref hopCell, "hopCell");
        Scribe_Values.Look(ref createdTick, "createdTick"); Scribe_Values.Look(ref forced, "forced");
        Scribe_Deep.Look(ref queuedJob, "queuedJob");
    }
}

public sealed class VerticalQueuedBinding : IExposable
{
    public Pawn pawn;
    public int jobId;
    public MapParent map;
    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn"); Scribe_References.Look(ref map, "map");
        Scribe_Values.Look(ref jobId, "jobId");
    }
}

[DefOf]
public static class VerticalRoutingDefOf
{
    public static JobDef DeepRimVertical_Travel;
    public static JobDef DeepRimVertical_Pickup;
    public static JobDef DeepRimVertical_Deliver;
    public static JobDef DeepRimVertical_DeliverIngredient;
    public static JobDef DeepRimVertical_RefuelCarried;
    static VerticalRoutingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VerticalRoutingDefOf));
}
