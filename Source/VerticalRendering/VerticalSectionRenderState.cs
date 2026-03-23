using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DeepRim.Vertical.VerticalRendering;

public sealed class VerticalSectionRenderState
{
    private readonly Map activeMap;
    private readonly Dictionary<WorkerKey, LowerLevelSectionWorker> workers = new();

    public VerticalSectionRenderState(Map activeMap)
    {
        this.activeMap = activeMap;
    }

    public IEnumerable<LowerLevelSectionWorker> GetActiveWorkers()
    {
        return workers.Values;
    }

    public void RebuildWorkers(Map map, VerticalRenderContext context, UpperLevelTerrainGrid terrainGrid, UpperOverlayVisibilityMask overlayMask, CellRect visibleSections)
    {
        _ = terrainGrid;
        var nextKeys = new HashSet<WorkerKey>();
        var renderFloor = context.LowerFloors.OrderByDescending(f => f.Floor.levelIndex).FirstOrDefault();
        if (renderFloor != null)
        {
            for (var sectionX = visibleSections.minX; sectionX <= visibleSections.maxX; sectionX++)
            {
                for (var sectionZ = visibleSections.minZ; sectionZ <= visibleSections.maxZ; sectionZ++)
                {
                    var sectionCoord = new IntVec2(sectionX, sectionZ);
                    if (overlayMask != null && !overlayMask.SectionHasRevealCells(sectionCoord))
                    {
                        continue;
                    }

                    var probeCell = new IntVec3(sectionCoord.x * Section.Size, 0, sectionCoord.z * Section.Size);
                    if (!probeCell.InBounds(map))
                    {
                        continue;
                    }

                    var section = map.mapDrawer.SectionAt(probeCell);
                    if (section == null)
                    {
                        continue;
                    }

                    var key = new WorkerKey(renderFloor.Floor.Map.uniqueID, sectionCoord);
                    nextKeys.Add(key);
                    if (!workers.TryGetValue(key, out var worker))
                    {
                        worker = new LowerLevelSectionWorker(section, renderFloor.Floor.Map, sectionCoord);
                        workers[key] = worker;
                    }

                    worker.SourceMap = renderFloor.Floor.Map;
                    worker.LevelIndex = renderFloor.Floor.levelIndex;
                    worker.DistanceFromActive = renderFloor.DistanceFromActive;
                    worker.Alpha = renderFloor.Alpha;
                }
            }
        }

        foreach (var key in workers.Keys.ToList())
        {
            if (!nextKeys.Contains(key))
            {
                workers.Remove(key);
            }
        }
    }

    public void MarkDirty(IntVec3 loc)
    {
        if (!loc.InBounds(activeMap))
        {
            return;
        }

        var sectionCoord = new IntVec2(loc.x / Section.Size, loc.z / Section.Size);
        foreach (var pair in workers)
        {
            if (pair.Key.SectionCoord == sectionCoord)
            {
                pair.Value.Dirty = true;
            }
        }
    }

    private readonly struct WorkerKey
    {
        public WorkerKey(int sourceMapId, IntVec2 sectionCoord)
        {
            SourceMapId = sourceMapId;
            SectionCoord = sectionCoord;
        }

        public int SourceMapId { get; }

        public IntVec2 SectionCoord { get; }
    }
}

public sealed class LowerLevelSectionWorker
{
    public LowerLevelSectionWorker(Section section, Map sourceMap, IntVec2 sectionCoord)
    {
        ActiveSection = section;
        SourceMap = sourceMap;
        SectionCoord = sectionCoord;
        Layer = new SectionLayer_LowerLevel(section);
    }

    public Section ActiveSection { get; }

    public Map SourceMap { get; set; }

    public IntVec2 SectionCoord { get; }

    public SectionLayer_LowerLevel Layer { get; }

    public int LevelIndex { get; set; }

    public int DistanceFromActive { get; set; }

    public float Alpha { get; set; }

    public bool Dirty { get; set; } = true;
}
