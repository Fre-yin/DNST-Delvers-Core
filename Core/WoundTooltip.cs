namespace DungeonSettlersDelvers.Core;

// The wound line in the health bar's open tooltip. The game fills
// Tooltip_Status once when it opens; WoundStatusTooltipPatch keeps the game's own text here (Opened),
// and Refresh writes it again with the current line of Wounds.StatusLine, so the line follows the
// wound while the tooltip stays open and goes when the wound closes. Refresh runs on Core's clock
// (ReviveCampaign) and when a wound closes; it ends by itself when the tooltip is gone.
internal static class WoundTooltip
{
    private static Tooltip_Status open;
    private static string baseText;
    private static bool baseActive;

    internal static void Opened(Tooltip_Status tooltip, string text, bool active)
    {
        open = tooltip;
        baseText = text ?? "";
        baseActive = active;
    }

    // The same tooltip opened for another status.
    internal static void Forget(Tooltip_Status tooltip)
    {
        if (open != null && tooltip != null && open.Pointer == tooltip.Pointer) ResetLifecycle();
    }

    internal static void ResetLifecycle()
    {
        open = null;
        baseText = null;
    }

    internal static void Refresh()
    {
        if (open == null) return;
        try
        {
            var text = open._descText;
            if (text == null || !open.gameObject.activeInHierarchy)
            {
                ResetLifecycle();
                return;
            }
            var line = Wounds.StatusLine(WoundBars.PanelUnit);
            var shown = line == null ? baseText : string.IsNullOrEmpty(baseText) ? line : baseText + "\n\n" + line;
            if (text.text != shown) text.text = shown;
            text.gameObject.SetActive(line != null || baseActive);
        }
        catch (Exception) { ResetLifecycle(); }
    }
}
