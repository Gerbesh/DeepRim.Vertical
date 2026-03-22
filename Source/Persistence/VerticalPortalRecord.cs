using Verse;

namespace DeepRim.Vertical.Persistence;

public sealed class VerticalPortalRecord : IExposable
{
    public int sourceLevel;
    public int targetLevel;
    public int cellX;
    public int cellZ;
    public bool allowsPawnMove = true;
    public bool allowsItemTransfer = true;
    public bool allowsSight = true;
    public bool allowsProjectilePass = true;
    public bool allowsHeatExchange = true;
    public bool allowsPowerTransfer = true;
    public bool allowsGasOrPipeTransfer;
    public float traversalCost = 45f;
    public bool doorLikeBlockingState;

    public VerticalPortalRecord()
    {
    }

    public VerticalPortalRecord(int sourceLevel, int targetLevel, IntVec3 cell)
    {
        this.sourceLevel = sourceLevel;
        this.targetLevel = targetLevel;
        cellX = cell.x;
        cellZ = cell.z;
    }

    public IntVec3 Cell => new(cellX, 0, cellZ);

    public void ExposeData()
    {
        Scribe_Values.Look(ref sourceLevel, "sourceLevel", 0);
        Scribe_Values.Look(ref targetLevel, "targetLevel", 0);
        Scribe_Values.Look(ref cellX, "cellX", 0);
        Scribe_Values.Look(ref cellZ, "cellZ", 0);
        Scribe_Values.Look(ref allowsPawnMove, "allowsPawnMove", true);
        Scribe_Values.Look(ref allowsItemTransfer, "allowsItemTransfer", true);
        Scribe_Values.Look(ref allowsSight, "allowsSight", true);
        Scribe_Values.Look(ref allowsProjectilePass, "allowsProjectilePass", true);
        Scribe_Values.Look(ref allowsHeatExchange, "allowsHeatExchange", true);
        Scribe_Values.Look(ref allowsPowerTransfer, "allowsPowerTransfer", true);
        Scribe_Values.Look(ref allowsGasOrPipeTransfer, "allowsGasOrPipeTransfer", false);
        Scribe_Values.Look(ref traversalCost, "traversalCost", 45f);
        Scribe_Values.Look(ref doorLikeBlockingState, "doorLikeBlockingState", false);
    }
}
