using System;
using System.Collections.Generic;
using System.Linq;
using DeepRim.Vertical.Persistence;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DeepRim.Vertical.VerticalWorld;

public sealed class VerticalSiteWorldComponent : WorldComponent
{
    private List<VerticalSiteRecord> sites = new();
    private Dictionary<string, VerticalSiteRecord> sitesById = new();
    private Dictionary<MapParent, VerticalFloorRecord> floorsByParent = new();

    public VerticalSiteWorldComponent(World world)
        : base(world)
    {
    }

    public IEnumerable<VerticalSiteRecord> Sites => sites;

    public static bool TryGet(out VerticalSiteWorldComponent component)
    {
        component = Find.World?.GetComponent<VerticalSiteWorldComponent>();
        return component != null;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref sites, "sites", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            sites ??= new List<VerticalSiteRecord>();
            RebuildCaches();
        }
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        RebuildCaches();
    }

    public VerticalSiteRecord GetOrCreateSiteForMap(Map map)
    {
        if (TryGetFloor(map, out var site, out _))
        {
            return site;
        }

        if (map?.Parent == null || !VerticalRuntime.Settings.adoptSurfaceMapOnDemand)
        {
            return null;
        }

        var newSite = new VerticalSiteRecord(Guid.NewGuid().ToString("N"), map.Parent.LabelCap, map.Tile);
        newSite.RegisterFloor(new VerticalFloorRecord(0, map, true));
        sites.Add(newSite);
        RebuildCaches();
        return newSite;
    }

    public void NotifyMapParentGenerated(VerticalSiteMapParent mapParent)
    {
        if (mapParent == null || string.IsNullOrEmpty(mapParent.SiteId))
        {
            return;
        }

        if (!sitesById.TryGetValue(mapParent.SiteId, out var site))
        {
            site = new VerticalSiteRecord(mapParent.SiteId, mapParent.LabelCap, mapParent.Tile);
            sites.Add(site);
        }

        site.RegisterFloor(new VerticalFloorRecord
        {
            levelIndex = mapParent.LevelIndex,
            isSurfaceAnchor = mapParent.LevelIndex == 0,
            mapGeneratorDefName = mapParent.StoredMapGeneratorDefName,
            mapParent = mapParent,
            mapSizeX = mapParent.StoredMapSize.x,
            mapSizeZ = mapParent.StoredMapSize.z
        });

        RebuildCaches();
    }

    public void RegisterGeneratedFloor(VerticalSiteRecord site, VerticalSiteMapParent parent, int levelIndex, Map sourceMap)
    {
        parent.SiteId = site.siteId;
        parent.LevelIndex = levelIndex;
        parent.StoredMapSize = sourceMap.Size;
        parent.StoredMapGeneratorDefName = levelIndex < 0
            ? VerticalDefOf.DeepRimVertical_Underground.defName
            : sourceMap.Parent?.MapGeneratorDef?.defName;
        parent.Tile = sourceMap.Tile;
        parent.SetFaction(Faction.OfPlayer);

        site.RegisterFloor(new VerticalFloorRecord
        {
            levelIndex = levelIndex,
            isSurfaceAnchor = levelIndex == 0,
            mapGeneratorDefName = parent.StoredMapGeneratorDefName,
            mapParent = parent,
            mapSizeX = sourceMap.Size.x,
            mapSizeZ = sourceMap.Size.z
        });

        RebuildCaches();
    }

    public void RegisterPortal(Map map, int sourceLevel, int targetLevel, IntVec3 cell)
    {
        if (!TryGetFloor(map, out var site, out _))
        {
            return;
        }

        site.RegisterPortal(new VerticalPortalRecord(sourceLevel, targetLevel, cell));
        site.RegisterPortal(new VerticalPortalRecord(targetLevel, sourceLevel, cell));
    }

    public bool TryGetFloor(Map map, out VerticalSiteRecord site, out VerticalFloorRecord floor)
    {
        site = null;
        floor = null;
        if (map?.Parent == null)
        {
            return false;
        }

        if (!floorsByParent.TryGetValue(map.Parent, out floor))
        {
            return false;
        }

        foreach (var candidateSite in sites)
        {
            if (candidateSite.floors.Contains(floor))
            {
                site = candidateSite;
                return true;
            }
        }

        return false;
    }

    public bool TryGetAdjacentFloor(Map map, int delta, out VerticalSiteRecord site, out VerticalFloorRecord floor)
    {
        site = null;
        floor = null;
        if (!TryGetFloor(map, out site, out var currentFloor))
        {
            return false;
        }

        floor = site.FloorAt(currentFloor.levelIndex + delta);
        return floor != null;
    }

    public IEnumerable<VerticalFloorRecord> GetOrderedFloors(Map map)
    {
        return TryGetFloor(map, out var site, out _) ? site.OrderedFloors : Enumerable.Empty<VerticalFloorRecord>();
    }

    public IEnumerable<VerticalPortalRecord> PortalsAt(Map map, IntVec3 cell)
    {
        if (!TryGetFloor(map, out var site, out var floor))
        {
            return Enumerable.Empty<VerticalPortalRecord>();
        }

        return site.portals.Where(p => p.sourceLevel == floor.levelIndex && p.cellX == cell.x && p.cellZ == cell.z);
    }

    private void RebuildCaches()
    {
        sitesById = new Dictionary<string, VerticalSiteRecord>();
        floorsByParent = new Dictionary<MapParent, VerticalFloorRecord>();

        foreach (var site in sites)
        {
            if (string.IsNullOrEmpty(site.siteId))
            {
                site.siteId = Guid.NewGuid().ToString("N");
            }

            site.RebuildCacheIfNeeded();
            sitesById[site.siteId] = site;
            foreach (var floor in site.floors.Where(f => f?.mapParent != null))
            {
                floorsByParent[floor.mapParent] = floor;
            }
        }
    }
}
