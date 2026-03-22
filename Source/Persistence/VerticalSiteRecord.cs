using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace DeepRim.Vertical.Persistence;

public sealed class VerticalSiteRecord : IExposable
{
    public string siteId;
    public string label;
    public PlanetTile tile = -1;
    public List<VerticalFloorRecord> floors = new();
    public List<VerticalPortalRecord> portals = new();

    private Dictionary<int, VerticalFloorRecord> floorsByLevel;

    public VerticalSiteRecord()
    {
    }

    public VerticalSiteRecord(string siteId, string label, PlanetTile tile)
    {
        this.siteId = siteId;
        this.label = label;
        this.tile = tile;
    }

    public IEnumerable<VerticalFloorRecord> OrderedFloors => floors.OrderBy(f => f.levelIndex);

    public VerticalFloorRecord FloorAt(int levelIndex)
    {
        RebuildCacheIfNeeded();
        floorsByLevel.TryGetValue(levelIndex, out var floor);
        return floor;
    }

    public void RegisterFloor(VerticalFloorRecord floor)
    {
        var existing = FloorAt(floor.levelIndex);
        if (existing != null)
        {
            existing.mapParent = floor.mapParent;
            existing.isSurfaceAnchor = floor.isSurfaceAnchor;
            existing.mapGeneratorDefName = floor.mapGeneratorDefName;
            existing.mapSizeX = floor.mapSizeX;
            existing.mapSizeZ = floor.mapSizeZ;
        }
        else
        {
            floors.Add(floor);
        }

        floorsByLevel = null;
    }

    public void RegisterPortal(VerticalPortalRecord portal)
    {
        if (!portals.Any(p => p.sourceLevel == portal.sourceLevel && p.targetLevel == portal.targetLevel && p.cellX == portal.cellX && p.cellZ == portal.cellZ))
        {
            portals.Add(portal);
        }
    }

    public void RebuildCacheIfNeeded()
    {
        if (floorsByLevel != null)
        {
            return;
        }

        floorsByLevel = new Dictionary<int, VerticalFloorRecord>();
        foreach (var floor in floors)
        {
            floorsByLevel[floor.levelIndex] = floor;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref siteId, "siteId");
        Scribe_Values.Look(ref label, "label");
        Scribe_Values.Look(ref tile, "tile", -1);
        Scribe_Collections.Look(ref floors, "floors", LookMode.Deep);
        Scribe_Collections.Look(ref portals, "portals", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            floors ??= new List<VerticalFloorRecord>();
            portals ??= new List<VerticalPortalRecord>();
            floorsByLevel = null;
        }
    }
}
