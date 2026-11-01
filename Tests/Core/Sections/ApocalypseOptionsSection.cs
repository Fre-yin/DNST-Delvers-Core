using DungeonSettlersDelvers.Core;
using static TestKit;

// Apocalypse rules for other campaigns.
internal static class ApocalypseOptionsSection
{
    internal static void Run()
    {
        // Single Apocalypse rules for other campaigns (the forest bandits too): five names; a combat
        // Apocalypse campaign has all of them, except that its pause rule follows the menu.
        Check(ApocalypseOptionRules.Names.SequenceEqual(new[] { "wounds", "no-pause", "instant-combat", "enemy-abilities", "bandits", "weak-bandits" })
                && ApocalypseOptionRules.IsOption("wounds") && !ApocalypseOptionRules.IsOption("Wounds") && !ApocalypseOptionRules.IsOption(null)
                && ApocalypseOptionRules.Effective(true, false, false, true, false, false) == (true, false, true, true)
                && ApocalypseOptionRules.Effective(true, true, false, false, false, false) == (true, true, true, true)
                && ApocalypseOptionRules.Effective(false, true, true, false, true, false) == (true, false, true, false),
            "rules: Apocalypse brings every rule, its pause rule from the menu; other campaigns take each option as set");
        Check(ApocalypseOptionRules.Bandits(true, null) && !ApocalypseOptionRules.Bandits(false, null)
                && ApocalypseOptionRules.Bandits(false, true) && !ApocalypseOptionRules.Bandits(true, false)
                && ApocalypseOptionRules.Bandits(false, null, true) && ApocalypseOptionRules.Bandits(true, null, false)
                && !ApocalypseOptionRules.Bandits(false, false, true) && ApocalypseOptionRules.IsOption("bandits"),
            "rules: the forest bandits with combat Apocalypse or their option; a Toolkit override wins for the session");
        Check(ApocalypseOptionRules.WeakBandits(true, true, false) && ApocalypseOptionRules.WeakBandits(false, true, true)
                && !ApocalypseOptionRules.WeakBandits(false, true, false) && !ApocalypseOptionRules.WeakBandits(true, false, true)
                && ApocalypseOptionRules.IsOption("weak-bandits"),
            "rules: the smaller bandit groups with combat Apocalypse or their option, and only while the forest bandits are active");
        Check(new[] { ApocalypseText.RulesSection, ApocalypseText.RulesWounds, ApocalypseText.RulesNoPause,
                    ApocalypseText.RulesInstantCombat, ApocalypseText.RulesEnemyAbilities, ApocalypseText.RulesWeakBandits, FirstHintText.MenuSection }
                .All(text => text.AllLanguages.Count == 10 && text.AllLanguages.All(line => !string.IsNullOrWhiteSpace(line)
                    && line.IndexOf('-') < 0 && line.IndexOf('–') < 0 && line.IndexOf('—') < 0))
                && ApocalypseText.RulesSection.German == "Apocalypse Optionen für andere Schwierigkeitsgrade",
            "rules: the menu's words in all ten languages without dashes");
    }
}
