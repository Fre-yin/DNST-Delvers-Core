namespace DungeonSettlersDelvers.Core;

// The pure part of ApocalypseRules, so the offline tests cover it: the names
// of the single rules a player can switch on for any campaign, and which rules are in effect. A
// combat Apocalypse campaign always has all of them; only its no-pause rule follows the player's
// Apocalypse setting (apocalypse.no-pause).
internal static class ApocalypseOptionRules
{
    internal const string Wounds = "wounds";
    internal const string NoPause = "no-pause";
    internal const string InstantCombat = "instant-combat";
    internal const string EnemyAbilities = "enemy-abilities";
    // The forest bandits too (camp and boss party with their loot), as one of the Apocalypse options for
    // other difficulties.
    internal const string ForestBandits = "bandits";
    // The smaller bandit groups (WeakBandits) as a rule of their own, the same way, and only with the forest
    // bandits.
    internal const string SmallerBandits = "weak-bandits";

    internal static readonly string[] Names = { Wounds, NoPause, InstantCombat, EnemyAbilities, ForestBandits, SmallerBandits };

    internal static bool IsOption(string name) => name != null && Array.IndexOf(Names, name) >= 0;

    // The forest bandits: always with combat Apocalypse, elsewhere with their option; a Toolkit override wins for the session,
    // null lifts it.
    internal static bool Bandits(bool apocalypseCombat, bool? testOverride, bool option = false)
        => testOverride ?? (apocalypseCombat || option);

    // The smaller bandit groups: while the forest bandits are active, always with combat Apocalypse, elsewhere with their option.
    internal static bool WeakBandits(bool apocalypseCombat, bool bandits, bool option)
        => bandits && (apocalypseCombat || option);

    internal static (bool Wounds, bool NoPause, bool InstantCombat, bool EnemyAbilities) Effective(bool apocalypseCombat,
        bool apocalypseNoPause, bool wounds, bool noPause, bool instantCombat, bool enemyAbilities)
        => apocalypseCombat
            ? (true, apocalypseNoPause, true, true)
            : (wounds, noPause, instantCombat, enemyAbilities);
}
