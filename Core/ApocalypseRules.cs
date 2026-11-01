namespace DungeonSettlersDelvers.Core;

// The single Apocalypse rules in effect for the bound campaign: wounds, no
// pause in combat, instant combat and enemy abilities. Each can be switched on for any campaign in
// Core's settings (rules.<name>); a combat Apocalypse campaign always has them
// (ApocalypseOptionRules.Effective). The switches are read when a campaign starts or is loaded
// (Apply, from ApocalypseDifficulty), so a switch changed in the menu applies from the next start or load.
// The forest bandits (Bandits) come with combat Apocalypse and, elsewhere, with their option (rules.bandits), read the
// same way; their smaller groups (WeakBandits, rules.weak-bandits) the same, and only with them.
internal static class ApocalypseRules
{
    private static bool woundsOption;
    private static bool noPauseOption;
    private static bool instantCombatOption;
    private static bool enemyAbilitiesOption;
    private static bool banditsOption;
    private static bool weakBanditsOption;
    private static bool? banditsTest;

    internal static bool Wounds => Effective(false).Wounds;

    internal static bool NoPause => Effective(ApocalypseDifficulty.ActiveCombat && CoreSettings.ApocalypseNoPause).NoPause;

    internal static bool InstantCombat => Effective(false).InstantCombat;

    internal static bool EnemyAbilities => Effective(false).EnemyAbilities;

    internal static bool Bandits => ApocalypseOptionRules.Bandits(ApocalypseDifficulty.ActiveCombat, banditsTest, banditsOption);

    internal static bool WeakBandits => ApocalypseOptionRules.WeakBandits(ApocalypseDifficulty.ActiveCombat, Bandits, weakBanditsOption);

    // The Toolkit's override of Bandits for the session; null lifts it.
    internal static void SetBanditsTest(bool? on)
    {
        banditsTest = on;
        DelversHost.Info("CORE_TEST_BANDITS override=" + (on.HasValue ? (on.Value ? "on" : "off") : "cleared")
            + " effective=" + (Bandits ? "true" : "false"));
    }

    private static (bool Wounds, bool NoPause, bool InstantCombat, bool EnemyAbilities) Effective(bool apocalypseNoPause)
        => ApocalypseOptionRules.Effective(ApocalypseDifficulty.ActiveCombat, apocalypseNoPause,
            woundsOption, noPauseOption, instantCombatOption, enemyAbilitiesOption);

    internal static void ResetLifecycle()
    {
        woundsOption = noPauseOption = instantCombatOption = enemyAbilitiesOption = banditsOption = weakBanditsOption = false;
        banditsTest = null;
    }

    // game: the game's own combat and management difficulty of the campaign that starts, when the caller knows it before the game
    // has applied it (a request, a save); without it the campaign setting is read.
    internal static void Apply(bool report, (int Combat, int Management)? game = null)
    {
        woundsOption = CoreSettings.Option(ApocalypseOptionRules.Wounds);
        noPauseOption = CoreSettings.Option(ApocalypseOptionRules.NoPause);
        instantCombatOption = CoreSettings.Option(ApocalypseOptionRules.InstantCombat);
        enemyAbilitiesOption = CoreSettings.Option(ApocalypseOptionRules.EnemyAbilities);
        banditsOption = CoreSettings.Option(ApocalypseOptionRules.ForestBandits);
        weakBanditsOption = CoreSettings.Option(ApocalypseOptionRules.SmallerBandits);
        if (report)
            DelversHost.Info("CORE_OPTIONS_APPLIED campaign=" + (ApocalypseDifficulty.ActiveCombat ? "apocalypse" : "normal")
                + " wounds=" + Flag(Wounds) + " noPause=" + Flag(NoPause) + " instantCombat=" + Flag(InstantCombat)
                + " enemyAbilities=" + Flag(EnemyAbilities) + " bandits=" + Flag(Bandits) + " weakBandits=" + Flag(WeakBandits)
                + " gameCombat=" + GameDifficulty(game, true) + " gameManagement=" + GameDifficulty(game, false));

        static string Flag(bool value) => value ? "true" : "false";

        // The game's own difficulty values of the campaign (0 Easy to 4 Downfall; an Apocalypse campaign keeps Downfall, which the
        // game's monster affecters and iron mode follow), "-" when neither the caller nor a campaign setting has them. At a load the
        // setting still holds the campaign before (gameCombat=1 while the loaded campaign read Downfall), so
        // the load passes the save's values.
        static string GameDifficulty((int Combat, int Management)? game, bool combat)
        {
            if (game is { } known) return (combat ? known.Combat : known.Management).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var setting = CoreGame.Flow?._campaignSetting?.CampaignStartSetting;
            if (setting == null) return "-";
            return (combat ? (int)setting.GetCombatDifficulty() : (int)setting.GetManagementDifficulty()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
