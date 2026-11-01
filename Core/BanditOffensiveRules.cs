using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// When no player has hit the bandits' herbalist for IdleSeconds, the guard pulls a water, nature or fire
// mage or a bow user ("if there is one"), whoever dealt the bandits the most damage in the last DamageWindowSeconds. That
// gives the player a window on the herbalist, and the pulled one must not be burst meanwhile. Then the guard's CC chain:
// pull, wait until the chain's root is off the target, stun, Hammering, then the slow ring with several players near; the
// archer focuses the pulled one for FocusSeconds ("only the archer"). The protect pull comes first; taunted, no pull.
// Rules without the game.
internal static class BanditOffensiveRules
{
    internal const string IdleKey = "bandits.chain.offensive.idle";
    internal const string CooldownKey = "bandits.chain.offensive.cooldown";
    internal const string FocusKey = "bandits.chain.offensive.focus";
    internal const double IdleDefault = 6.0;
    internal const double CooldownDefault = 20.0;
    internal const double FocusDefault = 4.0;
    internal const double DamageWindowSeconds = 6.0;

    // The chain's root on the pulled unit (SKILL_ChainGrap.SkillRoutine: AFFECTER_Rooting for {2}=1 s); the
    // stun waits until it is gone.
    internal const string RootKey = "AFFECTER_Rooting";

    internal static double Seconds(double? setting, double fallback, double max)
        => setting is double v && !double.IsNaN(v) ? Math.Clamp(v, 0.0, max) : fallback;

    // The game's ItemSubType of a weapon: Equipment_Bow 107, _FireStaff 108, _WaterStaff 109, _NatureStaff 110, _Sword 101,
    // _Greatsword 102, _Spear 106. Null for every other weapon ("a mage without an element: no"; dagger, blunt, axes).
    internal static string ClassOf(int itemSubType) => itemSubType switch
    {
        107 => "bow",
        108 => "fire",
        109 => "water",
        110 => "nature",
        101 => "sword",
        102 => "greatsword",
        106 => "spear",
        _ => null
    };

    // Tier 1 the mages and bows, tier 2 Equipment_Sword 101, _Greatsword 102 and _Spear 106, also with a
    // shield; tier 2 only when no tier 1 unit stands within reach. 0 for no tier.
    internal static int TierOf(string cls) => cls switch
    {
        "bow" or "fire" or "water" or "nature" => 1,
        "sword" or "greatsword" or "spear" => 2,
        _ => 0
    };

    // (an opening pull 1 s into every fight, idle=999): the herbalist's quiet time counts from the bandits'
    // fight start at the earliest (in the fight). Seconds since
    // the last hit on her (infinity for none) and since the guard's fight began (infinity outside a fight) -> the smaller.
    internal static double Idle(double sinceHit, double sinceFightStart) => Math.Min(sinceHit, sinceFightStart);

    // Whether the guard may pull offensively: the herbalist unpressed for idle seconds, the own lock passed, not taunted.
    internal static bool May(double idleSeconds, double idleNeeded, double sinceLast, double lockSeconds, bool taunted)
        => !taunted && idleSeconds >= idleNeeded && sinceLast >= lockSeconds;

    // The candidate with the most damage on the bandits in the window; on a tie the nearer one; null for none.
    internal static string Pick(IReadOnlyList<(string Guid, float Damage, int Distance)> candidates)
    {
        string best = null;
        var bestDamage = -1f;
        var bestDistance = int.MaxValue;
        foreach (var c in candidates)
        {
            if (c.Damage > bestDamage || (c.Damage == bestDamage && c.Distance < bestDistance))
            {
                best = c.Guid;
                bestDamage = c.Damage;
                bestDistance = c.Distance;
            }
        }
        return best;
    }

    // The chain's steps after the pull, in the author's order.
    internal enum Step { WaitRoot, Stun, Hammer, Done }

    internal static Step After(Step step) => step switch
    {
        Step.WaitRoot => Step.Stun,
        Step.Stun => Step.Hammer,
        _ => Step.Done
    };

    internal static string PullLine(string unit, string target, string cls, double idle, float damage, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_PULL unit={0} reason=offensive target={1} class={2} tier={3} idle={4:0.0} damage={5:0} tick={6}",
            unit, target, cls, TierOf(cls), idle, damage, tick);

    internal static string FocusLine(string unit, string target, double until, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_FOCUS unit={0} target={1} by=offensive-pull until={2:0.0} tick={3}", unit, target, until, tick);
}
