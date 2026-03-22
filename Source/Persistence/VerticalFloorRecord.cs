using RimWorld.Planet;
using Verse;

namespace DeepRim.Vertical.Persistence;

public sealed class VerticalFloorRecord : IExposable
{
    public int levelIndex;
    public bool isSurfaceAnchor;
    public string mapGeneratorDefName;
    public int mapSizeX;
    public int mapSizeZ;
    public MapParent mapParent;

    public VerticalFloorRecord()
    {
    }

    public VerticalFloorRecord(int levelIndex, Map map, bool isSurfaceAnchor)
    {
        this.levelIndex = levelIndex;
        this.isSurfaceAnchor = isSurfaceAnchor;
        mapParent = map.Parent;
        mapGeneratorDefName = map.Parent?.MapGeneratorDef?.defName;
        mapSizeX = map.Size.x;
        mapSizeZ = map.Size.z;
    }

    public Map Map => mapParent?.Map;

    public IntVec3 MapSize => new(mapSizeX, 1, mapSizeZ);

    public void ExposeData()
    {
        Scribe_Values.Look(ref levelIndex, "levelIndex", 0);
        Scribe_Values.Look(ref isSurfaceAnchor, "isSurfaceAnchor", false);
        Scribe_Values.Look(ref mapGeneratorDefName, "mapGeneratorDefName");
        Scribe_Values.Look(ref mapSizeX, "mapSizeX", 0);
        Scribe_Values.Look(ref mapSizeZ, "mapSizeZ", 0);
        Scribe_References.Look(ref mapParent, "mapParent");
    }
}
