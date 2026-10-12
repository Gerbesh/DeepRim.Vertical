using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalUtilities;

public enum UtilityKind { ThermalIntake, ThermalDuct, ThermalOutlet, Power, Plumbing }

public sealed class VerticalUtilityExtension : DefModExtension
{
    public UtilityKind kind;
}

public sealed class Building_VerticalUtility : Building
{
    public bool ValveOpen = true;
    public int? SelectedSourceLevel;
    public string StatusKey = "DeepRimVertical.Utility.NoConnection";
    public int? SourceLevel;
    public float SourceTemperature;
    public float LastHeat;
    public UtilityKind Kind => def.GetModExtension<VerticalUtilityExtension>().kind;
    public bool Enabled => Spawned && ValveOpen && !this.IsBrokenDown();
    public bool Powered => Enabled && (this.TryGetComp<CompPowerTrader>()?.PowerOn ?? false);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        GameComponent_VerticalUtilities.Get()?.Register(this);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        GameComponent_VerticalUtilities.Get()?.Unregister(this);
        base.DeSpawn(mode);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ValveOpen, "verticalValveOpen", true);
        var savedSource = SelectedSourceLevel ?? int.MinValue;
        Scribe_Values.Look(ref savedSource, "verticalThermalSource", int.MinValue);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            SelectedSourceLevel = savedSource == int.MinValue ? (int?)null : savedSource;
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos()) yield return gizmo;
        yield return new Command_Toggle
        {
            defaultLabel = "DeepRimVertical.Utility.Valve".Translate(),
            defaultDesc = "DeepRimVertical.Utility.ValveDescription".Translate(),
            isActive = () => ValveOpen,
            toggleAction = () => { ValveOpen = !ValveOpen; GameComponent_VerticalUtilities.Get()?.Invalidate(Kind == UtilityKind.Plumbing); }
        };
        if (Kind != UtilityKind.ThermalOutlet) yield break;
        yield return new Command_Action
        {
            defaultLabel = "DeepRimVertical.Utility.SelectSource".Translate(),
            action = () =>
            {
                var options = new List<FloatMenuOption>
                {
                    new("DeepRimVertical.Utility.NearestSource".Translate(), () => SelectedSourceLevel = null)
                };
                foreach (var level in GameComponent_VerticalUtilities.Get()?.IntakeLevels(this) ?? new List<int>())
                {
                    var captured = level;
                    options.Add(new FloatMenuOption("DeepRimVertical.Utility.SourceFloor".Translate(level), () => SelectedSourceLevel = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
    }

    public override string GetInspectString()
    {
        if (Kind == UtilityKind.Power)
            StatusKey = GameComponent_VerticalUtilities.Get()?.GetPowerGroup(this.TryGetComp<CompPower>()?.PowerNet) != null
                ? "DeepRimVertical.Utility.Connected" : "DeepRimVertical.Utility.NoConnection";
        if (Kind == UtilityKind.Plumbing)
            StatusKey = !DubsHygieneAdapter.Available ? "DeepRimVertical.Utility.NoAdapter"
                : DubsHygieneAdapter.IsConnected(this) ? "DeepRimVertical.Utility.Connected" : "DeepRimVertical.Utility.NoConnection";
        if (Kind == UtilityKind.ThermalIntake)
            StatusKey = Powered ? "DeepRimVertical.Utility.Operating" : "DeepRimVertical.Utility.NoPower";
        if (Kind == UtilityKind.ThermalDuct) StatusKey = "DeepRimVertical.Utility.Insulated";
        var text = base.GetInspectString() + "\n" + (ValveOpen ? StatusKey : "DeepRimVertical.Utility.Closed").Translate();
        if (Kind == UtilityKind.ThermalOutlet && SourceLevel.HasValue)
            text += "\n" + "DeepRimVertical.Utility.ThermalReading".Translate(SourceLevel.Value,
                SourceTemperature.ToString("F1"), LastHeat.ToString("F1"));
        if (Kind == UtilityKind.ThermalIntake)
            text += "\n" + "DeepRimVertical.Utility.IntakeReading".Translate((this.GetRoom()?.Temperature ?? Map.mapTemperature.OutdoorTemp).ToString("F1"));
        return text.Trim();
    }
}
