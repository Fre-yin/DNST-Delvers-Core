namespace DungeonSettlersDelvers.Core;

// The bandit guard's chain pull gets the author's own icon. The bandits use a
// copy of the game's SKILL_ChainGrap under their own key; it runs, looks and sounds the same, only key and icon are new.
// The Chain Knight boss keeps the game's skill and icon. Rules without the game.
internal static class BanditChainRules
{
    internal const string GameKey = "SKILL_ChainGrap";
    internal const string SkillKey = "SKILL_DelversCoreBanditChainGrap";
    internal const string IconFile = "Banditen_Kettenzug_32x32.png";

    // The copy reaches 7 cells, the boss's
    // chain keeps the game's 10. Setting bandits.chain.range, 1 to 10.
    internal const string RangeKey = "bandits.chain.range";
    internal const int RangeDefault = 7;

    internal static int Range(double? setting) => (int)Math.Clamp(Math.Round(setting ?? RangeDefault), 1.0, 10.0);

    internal static string NameKey(string skill) => "TEXTKEY_" + skill + "_NAME";
    internal static string DescKey(string skill) => "TEXTKEY_" + skill + "_DESC";

    // A guard saved before the copy carries the game's key in its skills: after loading it gets the bandits' key. Only for
    // Core's own units whose kit holds the bandits' key (never the boss).
    internal static bool Migrates(bool coreUnit, bool kitHasCopy, bool hasGameKey, bool hasCopy)
        => coreUnit && kitHasCopy && hasGameKey && !hasCopy;

    // Why a guard with the chain does not pull in the protection: null when it pulls.
    internal static string PullSkip(bool hasSkill, bool hasData, bool free, int distance, int minDistance, bool inRange, bool peels)
    {
        if (!hasSkill || !hasData) return "nodata";
        if (!free) return "cooldown";
        if (distance < minDistance) return "close";
        if (!inRange) return "range";
        return peels ? null : "nopeel";
    }

    internal static string PullSkipLine(string unit, string reason, long tick)
        => "CORE_AI_BANDIT_PULL_SKIP unit=" + unit + " reason=" + reason + " tick=" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string MigrateLine(string unit, string key)
        => "CORE_BANDIT_CHAIN_MIGRATED unit=" + unit + " key=" + key + " from=" + GameKey + " to=" + SkillKey;
}
