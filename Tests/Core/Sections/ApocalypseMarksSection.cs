using DungeonSettlersDelvers.Core;
using static TestKit;

// The Apocalypse texts. The mark itself is checked with the save data (CoreSaveSection).
internal static class ApocalypseMarksSection
{
    internal static void Run()
    {
        Check(ApocalypseText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('–') < 0 && text.IndexOf('—') < 0))
                && ApocalypseText.TitleKey.StartsWith("TEXTKEY_CreateCampaign_", StringComparison.Ordinal),
            "Apocalypse has its words in all ten languages, without long dashes, under the campaign screen's key pattern");
        // Like the game's Downfall, whose highlights are red where the steps below use #ccbeb8.
        var highlight = "<color=" + ApocalypseText.Colour + ">";
        Check(new[] { ApocalypseText.CombatDescription, ApocalypseText.ManagementDescription }.All(description => description.AllLanguages.All(text =>
                text.Contains(highlight, StringComparison.Ordinal) && !text.Contains("#ccbeb8", StringComparison.Ordinal))),
            "Apocalypse's descriptions highlight in Apocalypse's colour in every language, none in the light description colour");
    }
}
