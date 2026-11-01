using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The blade's Deadly Fear (SKILL_DeadlyFear, the game's greatsword skill):
// {0}=30 % physical attack to one target in range 5, {2}=3 AFFECTER_ExposedFlaw (no dodge, a sure critical hit per stack)
// and AFFECTER_Slow for {1}=10 s, cooldown 15 s. The blade casts it only to set up a big hit: the target carries no
// ExposedFlaw yet and Slay or Destructive Split is ready or ready within SetupSeconds. Never on a downed target, never out
// of range. CORE_AI_BANDIT_RULE rule=blade-fear-setup; CORE_AI_BANDIT_FEAR_SKIP reason= while it is ready and not cast.
// Rules without the game.
internal static class BanditFearRules
{
    internal const string SkillKey = "SKILL_DeadlyFear";
    internal const string FlawKey = "AFFECTER_ExposedFlaw";
    internal const string Rule = "blade-fear-setup";
    internal const double SetupSeconds = 1.0;
    internal static readonly string[] BigSkills = { "SKILL_Slay", "SKILL_DestructiveSplit" };

    // When a big skill can hit: 0 when free now, its cooldown left when it waits on that, null when it is missing or blocked.
    internal static double? BigReadyIn(bool present, bool free, bool onCooldown, double cooldownLeft)
    {
        if (!present) return null;
        if (free) return 0.0;
        return onCooldown ? Math.Max(0.0, cooldownLeft) : null;
    }

    // Why Deadly Fear is not cast now, or null to cast it.
    internal static string Skip(bool hasTarget, bool targetDowned, bool inRange, bool targetHasFlaw, double? soonestBig)
    {
        if (!hasTarget) return "target";
        if (targetDowned) return "downed";
        if (!inRange) return "range";
        if (targetHasFlaw) return "flaw";
        if (soonestBig is not double big || big > SetupSeconds) return "nobig";
        return null;
    }

    internal static double? Soonest(double? a, double? b)
        => a is double x ? (b is double y ? Math.Min(x, y) : x) : b;

    internal static string SkipLine(string unit, string target, string reason, double? soonestBig, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_FEAR_SKIP unit={0} target={1} reason={2} big={3} tick={4}",
            unit, target ?? "-", reason, soonestBig is double b ? b.ToString("0.0", CultureInfo.InvariantCulture) : "-", tick);
}
