using Verse;

namespace DeepRim.Vertical.VerticalOverlay;

public static class VerticalOverlayLabels
{
    public static VerticalOverlayMode NextMode(VerticalOverlayMode current)
    {
        return current switch
        {
            VerticalOverlayMode.Supports => VerticalOverlayMode.Structure,
            VerticalOverlayMode.Structure => VerticalOverlayMode.Full,
            _ => VerticalOverlayMode.Supports
        };
    }

    public static string TranslateMode(VerticalOverlayMode mode)
    {
        return mode switch
        {
            VerticalOverlayMode.Supports => "DeepRimVertical.Overlay.Mode.Supports".Translate(),
            VerticalOverlayMode.Structure => "DeepRimVertical.Overlay.Mode.Structure".Translate(),
            VerticalOverlayMode.Full => "DeepRimVertical.Overlay.Mode.Full".Translate(),
            _ => "DeepRimVertical.Overlay.Mode.Supports".Translate()
        };
    }

    public static string TranslateEnabledState(bool enabled)
    {
        return enabled
            ? "DeepRimVertical.Overlay.State.On".Translate()
            : "DeepRimVertical.Overlay.State.Off".Translate();
    }
}
