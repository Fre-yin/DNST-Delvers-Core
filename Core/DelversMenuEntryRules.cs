namespace DungeonSettlersDelvers.Core;

// The way into the Delvers menu, rules without the game (DelversMenuOptionsTab): the last entry of the game's options window
// (the menu as it is, only its entry moves there; the title menu's button is gone). In a running campaign
// the entry stays visible but locked, with a short hint under its name.
internal static class DelversMenuEntryRules
{
    internal const string TabName = "Btn_Delvers";
    internal const string Caption = "Delvers";
    // The locked entry: its graphics dimmed to this share, the hint smaller than the name.
    internal const float LockedAlpha = 0.5f;
    internal const int HintSizePercent = 65;

    internal static bool Usable(bool campaignStarted) => !campaignStarted;

    internal static string Label(bool usable, string hint)
        => usable || string.IsNullOrEmpty(hint) ? Caption : Caption + "\n<size=" + HintSizePercent + "%>" + hint + "</size>";
}
