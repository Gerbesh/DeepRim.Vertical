// Test-only mod. Enable exclusively with the isolated profile named below.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalUtilities;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

public sealed class VerticalUtilitiesNativeProbe : GameComponent
{
    private bool attempted;
    private int phase;
    public VerticalUtilitiesNativeProbe(Game game) { }
    public override void ExposeData() => Scribe_Values.Look(ref phase, "utilityProbePhase", 0);
    public override void GameComponentUpdate()
    {
        if (attempted || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
        attempted = true;
        // Refuse to alter any normal user profile.
        if (!GenFilePaths.SaveDataFolderPath.EndsWith("native-smoke-utilities", StringComparison.OrdinalIgnoreCase))
        { Log.Error("[UTILITY PROBE] Refused non-test profile"); return; }
        try
        {
            if (phase == 1)
            {
                TestReload();
                File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "PASS: native power solver, parallel links, ownership, shared battery draw, heat conservation/valve, Dubs native water draw/quality/disconnection, real game save/load and network reconstruction.");
                Log.Message("[UTILITY PROBE] Save/load PASS");
                Application.Quit();
                return;
            }
            var upper = Find.CurrentMap;
            Require(DubsHygieneAdapter.Available, "Dubs adapter unavailable");
            Require(VerticalMapCreationService.TryCreateFloor(upper, -1, out var lower, false), "Floor creation");
            PrepareRoom(upper); PrepareRoom(lower);
            TestPower(upper, lower);
            TestHeat(upper, lower);
            TestPlumbing(upper, lower);
            GameComponent_VerticalUtilities.Get().Buildings.Single(x => x.Kind == UtilityKind.ThermalOutlet).SelectedSourceLevel = -1;
            phase = 1;
            GameDataSaveLoader.SaveGame("utility-probe");
            Require(File.Exists(GenFilePaths.FilePathForSavedGame("utility-probe")), "Native save file");
            Log.Message("[UTILITY PROBE] Loading native save");
            GameDataSaveLoader.LoadGame("utility-probe");
            return;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt"), "FAIL: " + error);
            Log.Error("[UTILITY PROBE] FAIL " + error);
        }
        Application.Quit();
    }
    private static void TestReload()
    {
        var service = GameComponent_VerticalUtilities.Get();
        var sections = service.Buildings.ToList();
        Require(sections.Count == 10, "Utility registration after load: " + sections.Count);
        Require(!sections.Single(x => x.Kind == UtilityKind.ThermalIntake).ValveOpen, "Thermal valve persistence");
        Require(sections.Single(x => x.Kind == UtilityKind.ThermalOutlet).SelectedSourceLevel == -1, "Source selection persistence");
        Require(sections.Where(x => x.Kind == UtilityKind.Plumbing).All(x => !x.ValveOpen), "Plumbing valve persistence");
        Require(service.GetPowerGroup(sections.First(x => x.Kind == UtilityKind.Power).GetComp<CompPower>().PowerNet)?.BuildView().batteryComps.Count == 2, "Power graph after load");
        service.FlushPlumbing();
        var tanks = Find.Maps.SelectMany(m => m.listerThings.AllThings.OfType<ThingWithComps>())
            .Select(t => t.GetComp<CompWaterStorage>()).Where(c => c != null).ToList();
        Require(tanks.Count == 2 && Math.Abs(tanks.Sum(t => t.WaterStorage) - 40f) < 0.001f, "Native water storage after load");
        Require(tanks.All(t => t.PipeComp.pipeNet.WaterTowers.Count == 1), "Disconnected plumbing after load");
        foreach (var riser in sections.Where(x => x.Kind == UtilityKind.Plumbing)) riser.ValveOpen = true;
        service.Invalidate(); service.FlushPlumbing();
        Require(tanks[0].PipeComp.pipeNet.WaterTowers.Count == 2 && tanks[0].PipeComp.pipeNet.PullWater(10f, out _), "Reconnected plumbing after load");
        Require(Math.Abs(tanks.Sum(t => t.WaterStorage) - 30f) < 0.001f, "Native water conservation after load");
    }
    private static void Require(bool pass, string message)
    { if (!pass) throw new InvalidOperationException(message); }
    private static ThingWithComps Spawn(string name, Map map, int x, int z)
    {
        var def = DefDatabase<ThingDef>.GetNamed(name);
        var thing = (ThingWithComps)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null);
        thing.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(thing, new IntVec3(x, 0, z), map);
        return thing;
    }
    private static void PrepareRoom(Map map)
    {
        for (var x = 20; x <= 30; x++)
        for (var z = 20; z <= 30; z++)
        {
            var cell = new IntVec3(x, 0, z);
            foreach (var thing in cell.GetThingList(map).ToList()) thing.Destroy();
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            if (x == 20 || x == 30 || z == 20 || z == 30) Spawn("Wall", map, x, z);
        }
        map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
    }
    private static Building_VerticalUtility Riser(string name, Map map, int x, int z) =>
        (Building_VerticalUtility)Spawn("DeepRimVertical_" + name, map, x, z);
    private static void TestPower(Map a, Map b)
    {
        var batteryA = Spawn("Battery", a, 22, 22).GetComp<CompPowerBattery>();
        var batteryB = Spawn("Battery", b, 22, 22).GetComp<CompPowerBattery>();
        Riser("PowerRiser", a, 23, 22); Riser("PowerRiser", b, 23, 22);
        Riser("PowerRiser", a, 23, 23); Riser("PowerRiser", b, 23, 23);
        var consumer = Spawn("StandingLamp", b, 24, 22).GetComp<CompPowerTrader>();
        a.powerNetManager.UpdatePowerNetsAndConnections_First();
        b.powerNetManager.UpdatePowerNetsAndConnections_First();
        var service = GameComponent_VerticalUtilities.Get();
        service.InvalidatePower();
        var na = batteryA.PowerNet; var nb = batteryB.PowerNet;
        Require(na != null && nb != null && na != nb, "Local power ownership");
        var group = service.GetPowerGroup(na);
        Require(group != null && group.Nets.Count == 2 && group.BuildView().batteryComps.Count == 2, "Parallel power union");
        batteryA.SetStoredEnergyPct(0.5f); batteryB.SetStoredEnergyPct(0f);
        Require(nb.CanPowerNow(consumer), "Remote battery availability");
        consumer.PowerOn = true; consumer.PowerOutput = -100f;
        var before = batteryA.StoredEnergy + batteryB.StoredEnergy;
        na.PowerNetTick(); nb.PowerNetTick();
        var spent = before - batteryA.StoredEnergy - batteryB.StoredEnergy;
        Require(Math.Abs(spent - 100f / 60000f) < 0.0001f, "Native battery draw once: " + spent);
        Require(batteryA.PowerNet == na && batteryB.PowerNet == nb, "Native ownership after solving");
        Log.Message("[UTILITY PROBE] Power PASS");
    }
    private static void TestHeat(Map a, Map b)
    {
        var source = Riser("ThermalIntake", b, 25, 25);
        var outlet = Riser("ThermalOutlet", a, 25, 25);
        source.GetComp<CompPowerTrader>().PowerOn = true;
        outlet.GetComp<CompPowerTrader>().PowerOn = true;
        a.regionAndRoomUpdater.RebuildAllRegionsAndRooms(); b.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        var cold = source.GetRoom(); var warm = outlet.GetRoom();
        Require(cold != null && warm != null && !cold.UsesOutdoorTemperature && !warm.UsesOutdoorTemperature, "Enclosed rooms");
        cold.Temperature = -20f; warm.Temperature = 30f;
        var before = cold.Temperature * cold.CellCount + warm.Temperature * warm.CellCount;
        var exchange = typeof(GameComponent_VerticalUtilities).GetMethod("ExchangeHeat", BindingFlags.Instance | BindingFlags.NonPublic);
        var service = GameComponent_VerticalUtilities.Get();
        exchange.Invoke(service, null);
        Require(warm.Temperature < 30f && cold.Temperature > -20f && outlet.SourceLevel == -1, "Native thermal transfer");
        Require(Math.Abs(before - cold.Temperature * cold.CellCount - warm.Temperature * warm.CellCount) < 0.01f, "Native heat conservation");
        source.ValveOpen = false; service.Invalidate(false);
        var closed = warm.Temperature; exchange.Invoke(service, null);
        Require(warm.Temperature == closed && outlet.SourceLevel == null, "Closed thermal intake");
        Log.Message("[UTILITY PROBE] Heat PASS");
    }
    private static void TestPlumbing(Map a, Map b)
    {
        var tankA = Spawn("WaterButt", a, 27, 23).GetComp<CompWaterStorage>();
        var tankB = Spawn("WaterButt", b, 27, 23).GetComp<CompWaterStorage>();
        var ra = Riser("PlumbingRiser", a, 27, 24); var rb = Riser("PlumbingRiser", b, 27, 24);
        ra.ValveOpen = rb.ValveOpen = false;
        var service = GameComponent_VerticalUtilities.Get();
        service.Invalidate(); service.FlushPlumbing();
        tankA.WaterStorage = 50f; tankB.WaterStorage = 0f;
        tankA.WaterQuality = ContaminationLevel.Untreated;
        Require(!tankB.PipeComp.pipeNet.PullWater(10f, out _), "No automatic coincident-XY water link");
        ra.ValveOpen = rb.ValveOpen = true; service.Invalidate(); service.FlushPlumbing();
        var netA = tankA.PipeComp.pipeNet; var netB = tankB.PipeComp.pipeNet;
        Require(netA != netB && netA.WaterTowers.Count == 2 && netB.WaterTowers.Count == 2, "Native plumbing union");
        Require(netB.PullWater(10f, out var quality) && quality == ContaminationLevel.Untreated, "Remote native water/quality draw");
        Require(Math.Abs(tankA.WaterStorage + tankB.WaterStorage - 40f) < 0.001f, "No water duplication");
        var parallelA = Riser("PlumbingRiser", a, 26, 24); var parallelB = Riser("PlumbingRiser", b, 26, 24);
        service.Invalidate(); service.FlushPlumbing();
        Require(tankB.PipeComp.pipeNet.WaterTowers.Count == 2, "Parallel plumbing deduplication");
        ra.ValveOpen = rb.ValveOpen = parallelA.ValveOpen = parallelB.ValveOpen = false;
        service.Invalidate(); service.FlushPlumbing();
        Require(tankB.PipeComp.pipeNet.WaterTowers.Count == 1 && !tankB.PipeComp.pipeNet.PullWater(1f, out _), "Plumbing disconnection");
        Require(Math.Abs(tankA.WaterStorage - 40f) < 0.001f, "Disconnected source storage retained");
        Log.Message("[UTILITY PROBE] Plumbing PASS");
    }
}
