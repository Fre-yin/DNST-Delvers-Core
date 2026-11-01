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
    }
}
