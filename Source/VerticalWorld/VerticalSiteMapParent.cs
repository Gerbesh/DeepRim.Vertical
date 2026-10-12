using Verse;
using RimWorld.Planet;
using System.Collections.Generic;
using RimWorld;

namespace DeepRim.Vertical.VerticalWorld;

public sealed class VerticalSiteMapParent : MapParent
{
    private string siteId;
    private int levelIndex;
    private int mapSizeX;
    private int mapSizeZ;
    private string mapGeneratorDefName;

    public string SiteId
    {
        get => siteId;
        set => siteId = value;
    }

    public int LevelIndex
    {
        get => levelIndex;
        set => levelIndex = value;
    }

    public IntVec3 StoredMapSize
    {
        get => new(mapSizeX, 1, mapSizeZ);
        set
        {
            mapSizeX = value.x;
            mapSizeZ = value.z;
        }
    }

    public string StoredMapGeneratorDefName
    {
        get => mapGeneratorDefName;
        set => mapGeneratorDefName = value;
    }

    public override MapGeneratorDef MapGeneratorDef => DefDatabase<MapGeneratorDef>.GetNamedSilentFail(mapGeneratorDefName) ?? base.MapGeneratorDef;

    // This parent owns a floor's map and save data, not another world settlement.
    public override bool SelectableNow => false;

    public override void Draw()
    {
    }

    public override void DrawExtraSelectionOverlays()
    {
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        yield break;
    }

    public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
    {
        yield break;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref siteId, "siteId");
        Scribe_Values.Look(ref levelIndex, "levelIndex", 0);
        Scribe_Values.Look(ref mapSizeX, "mapSizeX", 0);
        Scribe_Values.Look(ref mapSizeZ, "mapSizeZ", 0);
        Scribe_Values.Look(ref mapGeneratorDefName, "mapGeneratorDefName");
    }

    public override void PostMapGenerate()
    {
        base.PostMapGenerate();
        VerticalSiteWorldComponent.TryGet(out var component);
        component?.NotifyMapParentGenerated(this);
    }

    public override string GetInspectString()
    {
        return base.GetInspectString() + "\n" + "DeepRimVertical.Navigator.CurrentFloor".Translate(VerticalFloorLabel.Format(levelIndex));
    }

    public override IEnumerable<IncidentTargetTagDef> IncidentTargetTags()
    {
        if (levelIndex < 0)
        {
            yield return IncidentTargetTagDefOf.Map_Misc;
            yield break;
        }

        foreach (var tag in base.IncidentTargetTags())
        {
            yield return tag;
        }
    }
}
