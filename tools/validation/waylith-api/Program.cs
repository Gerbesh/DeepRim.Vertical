using System.Reflection;
using HarmonyLib;
using Verse;
using System.Linq;

static void AssertField(Type owner, string fieldName, Type expected)
{
    var field = owner.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    if (field is null || field.FieldType != expected)
        throw new InvalidOperationException($"Changed Waylith ABI: {owner.FullName}.{fieldName}: expected {expected}, actual {field?.FieldType}");
    Console.WriteLine($"PASS {owner.Name}.{fieldName}: {expected.Name}");
}
AssertField(typeof(MapDrawer), "map", typeof(Map));
AssertField(typeof(MapDrawer), "sections", typeof(Section[,]));
AssertField(typeof(MapTemperature), "map", typeof(Map));
AssertField(typeof(RoomTempTracker), "room", typeof(Room));
AssertField(typeof(RoomTempTracker), "thickRoofCoverage", typeof(float));

// Validate Harmony's cached delegates on the *installed* game and Harmony 3 DLLs,
// without running a user save or initializing Waylith/Vulkan.
_ = AccessTools.FieldRefAccess<MapDrawer, Map>("map");
_ = AccessTools.FieldRefAccess<MapDrawer, Section[,]>("sections");
_ = AccessTools.FieldRefAccess<MapTemperature, Map>("map");
_ = AccessTools.FieldRefAccess<RoomTempTracker, Room>("room");
_ = AccessTools.FieldRefAccess<RoomTempTracker, float>("thickRoofCoverage");
var harmonyVersion = typeof(Harmony).Assembly.GetName().Version;
Console.WriteLine($"PASS installed Harmony={harmonyVersion}; typed field delegates resolved");
// Resolve the base 1.7 texture identifiers from the game asset archive.
// The former SnowPolluted/WindsweptSandPolluted names do not ship in 1.7.
var gameDir = Environment.GetEnvironmentVariable("RIMWORLD_DIR") ?? @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld";
var pack = Path.Combine(gameDir, "Assets.zip");
if (!File.Exists(pack)) throw new FileNotFoundException("Set RIMWORLD_DIR to the installed RimWorld 1.7 directory", pack);
{
    using var archive = System.IO.Compression.ZipFile.OpenRead(pack);
    var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in new[] { "Resources/Textures/Other/Snow.psd.dds", "Resources/Textures/Other/Sand.psd.dds" })
    {
        if (!entries.Contains(entry)) throw new InvalidOperationException("Waylith texture resource missing: " + entry);
        Console.WriteLine("PASS native texture resource: " + entry);
    }
}
Console.WriteLine("PASS 1.7 Waylith private field API contract");


// Optional third-party compatibility: inspect the exact installed Dubs 1.7 binary.
// This only verifies reflection contracts; it deliberately does not patch or start a game.
var dubsDir = Environment.GetEnvironmentVariable("DUBS_17_DIR")
    ?? @"C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\836308268\1.7\Assemblies";
if (File.Exists(Path.Combine(dubsDir, "BadHygiene.dll")))
{
    var helper = Path.Combine(dubsDir, "0DubCore.dll");
    if (File.Exists(helper)) Assembly.LoadFrom(helper);
    var dubs = Assembly.LoadFrom(Path.Combine(dubsDir, "BadHygiene.dll"));
    Type DubsType(string name) => dubs.GetType("DubsBadHygiene." + name, throwOnError: true)!;
    var compPipe = DubsType("CompPipe");
    var net = DubsType("PlumbingNet");
    var manager = DubsType("HygienePipeMapComp");
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    void NeedField(Type type, string field)
    {
        if (type.GetField(field, flags) is null)
            throw new MissingFieldException(type.FullName, field);
    }
    void NeedMethod(Type type, string method)
    {
        if (!type.GetMethods(flags).Any(m => m.Name == method))
            throw new MissingMethodException(type.FullName, method);
    }
    NeedField(compPipe, "pipeNetRef");
    foreach(var name in new[] { "PipedThings", "MapComp", "NetType", "slave", "IP", "cells",
        "HeatingCap", "CoolingCap", "BoilerCapacitySum", "HeatStoreCapacitySum",
        "OutdoorUnitCapacitySum", "IndoorUnitCapacitySum", "WaterCap", "GroundWaterCapacitySum",
        "PumpingCapacitySum", "WaterPumpedPerTick" }) NeedField(net, name);
    foreach(var name in new[] { "MasterInternetID", "DBHComps" }) NeedField(manager, name);
    foreach(var name in new[] { "AllTileNets", "InitNet", "Tick" }) NeedMethod(net, name);
    foreach(var name in new[] { "RefreshInternetsOnTile", "RegenPipeGrids", "MapRemoved" }) NeedMethod(manager, name);
    Console.WriteLine("PASS installed Dubs Bad Hygiene 1.7 reflection contract");
}
else
{
    Console.WriteLine("SKIP optional Dubs Bad Hygiene: 1.7 assembly not installed");
}
