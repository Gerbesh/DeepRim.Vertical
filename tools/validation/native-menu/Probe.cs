
using System;
using System.IO;
using System.Reflection;
using DeepRim.Vertical.VerticalMaps;
using DeepRim.Vertical.VerticalWorld;
using UnityEngine;
using Verse;

public sealed class CompactFloorMenuProbe : GameComponent
{
    private bool started;
    public CompactFloorMenuProbe(Game game) {}
    public override void GameComponentUpdate()
    {
        if (started || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting)
            return;
        if (!GenFilePaths.SaveDataFolderPath.EndsWith("native-floor-menu", StringComparison.OrdinalIgnoreCase))
            return;
        started = true;
        var destination = Path.Combine(GenFilePaths.SaveDataFolderPath, "result.txt");
        try
        {
            var map = Find.CurrentMap;
            var site = Find.World.GetComponent<VerticalSiteWorldComponent>();
            if (site == null || site.GetOrCreateSiteForMap(map) == null)
                throw new InvalidOperationException("No vertical site");
            var method = typeof(VerticalFloorHud).GetMethod("OpenFloorMenu", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("Floor menu method missing");
            method.Invoke(null, new object[] { map, site, 0, false, false, map.Center });
            var menu = Find.WindowStack.WindowOfType<FloatMenu>();
            if (menu == null)
                throw new InvalidOperationException("Native FloatMenu did not appear in WindowStack");
            File.WriteAllText(destination, "PASS: native RimWorld FloatMenu opens from the compact floor control.");
            menu.Close(false);
        }
        catch (Exception error)
        {
            File.WriteAllText(destination, "FAIL: " + error);
            Log.Error("[COMPACT FLOOR MENU PROBE] " + error);
        }
        Application.Quit();
    }
}
