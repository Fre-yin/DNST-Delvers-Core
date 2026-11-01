using DungeonSettlersDelvers.Core;
using static TestKit;

// The five first hints.
internal static class FirstHintsSection
{
    internal static void Run()
    {
        // First hints: five hints, each a title and a text in ten languages, keys of the game's guide.
        Check(FirstHintText.Keys.Length == 5 && FirstHintText.Keys.Distinct().Count() == 5
                && FirstHintText.NameKey(FirstHintText.Wounds) == "TEXTKEY_GAMEGUIDE_DelversCoreWounds_NAME"
                && FirstHintText.DescKey(FirstHintText.Revive) == "TEXTKEY_GAMEGUIDE_DelversCoreRevive_DESC"
                && FirstHintText.All.All(entry => entry.Text.AllLanguages.Count == 10
                    && entry.Text.AllLanguages.All(line => !string.IsNullOrWhiteSpace(line) && line.IndexOf('–') < 0 && line.IndexOf('—') < 0))
                && FirstHintText.WoundsDescription.English.Contains(FirstHintText.PageBreak),
            "hints: five hints with guide keys, words in all ten languages, the wound hint in pages");
        Check(FirstHintText.EffectiveEnabled(null, true) && !FirstHintText.EffectiveEnabled(null, false)
                && FirstHintText.EffectiveEnabled(true, false) && !FirstHintText.EffectiveEnabled(false, true),
            "hints: a Toolkit override wins for the session; without it (cleared) the menu switch decides");
    }
}
