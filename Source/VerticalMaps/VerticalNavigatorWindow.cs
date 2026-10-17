using DeepRim.Vertical.VerticalWorld;
using UnityEngine;
using Verse;

namespace DeepRim.Vertical.VerticalMaps;

// Verse-managed window stack avoids the removed GUI.Window overload on Waylith.
public sealed class VerticalNavigatorWindow : Window
{
    public override Vector2 InitialSize => new(
        Mathf.Min(320f, UI.screenWidth - 32f),
        Mathf.Min(525f, UI.screenHeight - 85f));

    public VerticalNavigatorWindow()
    {
        optionalTitle = "DeepRimVertical.Navigator.Title".Translate();
        doCloseX = true;
        draggable = true;
        forcePause = false;
        preventCameraMotion = false;
        closeOnAccept = false;
        absorbInputAroundWindow = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            return;
        if (!VerticalSiteWorldComponent.TryGet(out var site))
            return;
        Find.CurrentMap.GetComponent<VerticalMapComponent>()?.DrawNavigatorContents(site, inRect);
    }

    public override void PostClose()
    {
        base.PostClose();
        VerticalMapComponent.NavigatorClosed();
    }
}
