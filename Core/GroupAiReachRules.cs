using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The shield pulls attackers off the healer and off any party member below
// CounterShare (the healer first), with a time limit per attacker against taunt spam; a ranged unit with no foe in its
// kit's reach walks toward its focus until it is in reach, while no foe stands within ReachMinEnemyDistance (checked at
// every step); ranged units see foes up to FocusReachRanged. A player's order still goes first (StancePlayerQuiet).
internal static class GroupAiReachRules
{
    internal const float CounterSecondsDefault = 8f;
    internal const string CounterSecondsKey = "groupai.shield.counterSeconds";
    internal const float CounterShare = 0.35f;
    // An attacker must stand on the victim this long before the shield turns its own target to it (no swinging).
    internal const float CounterHoldSeconds = 1f;
    internal const int FocusReachRanged = 8;
    internal const int ReachMinEnemyDistance = 3;
    internal const int ReachSteps = 2;

    internal static float CounterSeconds(double? setting)
        => setting is double value && value >= 1 && value <= 60 ? (float)value : CounterSecondsDefault;

    // The party member to defend: the healer while attacked, else the lowest one below CounterShare that is attacked.
    internal static string CounterVictim(string healer, IReadOnlyList<(string Guid, float Share, int Attackers)> allies)
    {
        if (allies == null) return null;
        string low = null;
        var lowShare = float.MaxValue;
        foreach (var ally in allies)
        {
            if (ally.Attackers <= 0) continue;
            if (ally.Guid == healer) return ally.Guid;
            if (ally.Share < CounterShare && ally.Share < lowShare)
            {
                low = ally.Guid;
                lowShare = ally.Share;
            }
        }
        return low;
    }

    // "taunt": the taunt is free, the attacker in its reach and its time limit over. Otherwise, (the shield decides about
    // every 6 s, so a 1 s wait became 6): already on the picked attacker "wait" (on-attacker); on
    // another attacker of the victim it switches only once the picked one stood CounterHoldSeconds ("hold" before);
    // on none of them it turns at once ("free").
    internal static (string Action, string Why) CounterAction(bool tauntFree, bool inTauntReach, bool tauntDue, float onVictimSeconds, bool onPick,
        bool onOtherAttacker)
    {
        if (tauntFree && inTauntReach && tauntDue) return ("taunt", "taunt");
        if (onPick) return ("wait", "on-attacker");
        if (onOtherAttacker) return onVictimSeconds >= CounterHoldSeconds ? ("retarget", "switch") : ("wait", "hold");
        return ("retarget", "free");
    }

    // A: a taunt for the party's healer is always due; for anyone else the time limit per attacker holds.
    internal static bool CounterDue(bool forHealer, bool tauntedBefore, float now, float lastAt, float seconds)
        => forHealer || !tauntedBefore || GameClockRules.Elapsed(now, lastAt, seconds);

    // Focus reach of a ranged unit: up to FocusReachRanged; beyond its skill's range only while it may walk in.
    // (the focus swung between the herbalist and another bandit every 1.5 to 4 s): keep, for the
    // foe it holds and for an enemy healer, ignores the nearness limit, so a foe stepping close no longer drops it.
    internal static bool RangedReachable(int distance, int skillRange, int nearestDistance, bool keep = false)
        => distance <= FocusReachRanged && (distance <= Math.Max(1, skillRange) || keep || nearestDistance > ReachMinEnemyDistance);

    // The focus stands beyond the skill's range and walking in is barred (a foe within ReachMinEnemyDistance): the
    // single shot goes to the nearest foe in range instead, the focus stays.
    internal static bool RangedInterim(int focusDistance, int skillRange, int nearestDistance)
        => focusDistance > Math.Max(1, skillRange) && nearestDistance <= ReachMinEnemyDistance;

    // The arrow rain goes on a held focus of rank 0 (healer) or 1 in reach, also when it stands alone.
    internal static bool RainOnFocus(int rank, bool inRange) => rank >= 0 && rank <= 1 && inRange;

    // A taunt is wasted when every foe in its reach would break it at once.
    internal static bool TauntWasted(int inReach, int breaking) => inReach > 0 && breaking >= inReach;

    // The bandits' healer steps away only while a capable hostile is left.
    internal static bool KiteAllowed(int capableHostiles) => capableHostiles > 0;

    // "none": a foe is in the kit's reach or there is no target; "blocked": a foe stands within ReachMinEnemyDistance;
    // "walk": toward the target.
    internal static string ReachAction(bool hasTarget, int targetDistance, int kitRange, int nearestDistance)
    {
        if (!hasTarget || targetDistance <= Math.Max(1, kitRange)) return "none";
        if (nearestDistance <= ReachMinEnemyDistance) return "blocked";
        return "walk";
    }

    // The cell ReachSteps toward the target (diagonals count as one), never nearer than the kit's range.
    internal static (int X, int Y) ReachStep(int fromX, int fromY, int toX, int toY, int kitRange)
    {
        var distance = Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY));
        var steps = Math.Min(ReachSteps, Math.Max(0, distance - Math.Max(1, kitRange)));
        var x = fromX;
        var y = fromY;
        for (var i = 0; i < steps; i++)
        {
            x += Math.Sign(toX - x);
            y += Math.Sign(toY - y);
        }
        return (x, y);
    }

    internal static string CounterLine(string unit, string victim, string attacker, string action, string reason, string why, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_COUNTERTANK unit={0} victim={1} attacker={2} action={3} reason={4} why={5} tick={6}",
            unit, victim ?? "-", attacker ?? "-", action, reason ?? "-", why ?? "-", tick);

    internal static string ReachLine(string unit, string action, string target, string to, int distance, int range, int nearest, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_REACH unit={0} action={1} target={2} to={3} distance={4} range={5} nearest={6} tick={7}",
            unit, action, target ?? "-", to ?? "-", distance, range, nearest == int.MaxValue ? -1 : nearest, tick);
}
