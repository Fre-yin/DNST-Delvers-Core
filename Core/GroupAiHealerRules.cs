namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the bandits' herbalist and her protection: kiting, Lifebloom on herself, heal-below, the heal
// path and moving up to support, and the target the band protects her from.
internal static partial class GroupAiRules
{
    // ---- the bandits' healer and her protection, the party's shield wall ----

    // (a) A hostile within kiteDistance: vines and a step away come first, Lifebloom on herself after.
    internal static bool HealerKitesFirst(int closestDistance, int kiteDistance) => closestDistance <= kiteDistance;

    // (a) Lifebloom on herself ahead of the fight: one stack only.
    internal const int PrebloomStacks = 1;

    internal static bool PrebloomAllowed(int selfStacks) => selfStacks < PrebloomStacks;

    // (the herbalist stood still while Pamela walked up to her, and
    // bloomed herself twice with Pamela two cells off): Lifebloom on herself only while no hostile stands within
    // kiteDistance; with one there she steps away first.
    internal static bool SelfBloomAllowed(int closestDistance, int kiteDistance) => closestDistance > kiteDistance;

    // The bandits' healer never takes the enemy tree's own step toward the closest hostile (its move check answers
    // no). The check walks toward the closest hostile whatever her behaviour aims at.
    internal static bool HealerKeepsAway(bool healer) => healer;

    // A healer whose move order has not moved her yet decides again once a hostile stands next to her.
    internal static bool HealerBreaksMove(bool unmoved, int closestDistance) => unmoved && closestDistance <= 1;

    // Her next kite step right away when the last order did not move her or a hostile stands next to her.
    internal static bool KiteAgainNow(bool lastUnmoved, int closestDistance) => lastUnmoved || closestDistance <= 1;

    // Instead of a basic attack that walks her into melee she stands and decides again soon (CoreUnitCombat
    // gives the game nothing).
    internal const string HoldRule = "healer-hold";

    internal static bool HoldsStill(string rule) => rule == HoldRule || rule == SeedStayRules.HoldRule;

    // The guard's chain is no damage skill, only a peel: on someone within
    // PullNearHealerCells of the healer, and only when the pull takes it away from her (the guard farther from her).
    internal const int PullNearHealerCells = 3;

    // The guard's combo on that one (pull, the chain holds, then the stun, then the
    // slow): each step within GuardComboSeconds of game time after the one before, longer than the global cooldown
    // of about 5 s between the guard's skills.
    internal const double GuardComboSeconds = 8.0;

    internal static bool PullPeelsHealer(int targetToHealer, int guardToHealer)
        => targetToHealer <= PullNearHealerCells && guardToHealer > targetToHealer;

    // The herbalist's kit: she heals only below heal-below (start 0.9), the
    // emergency below EmergencyShare first, herself included; otherwise her basic attack (the green staff) when no
    // hostile stands within HealerBasicSafeCells and the target is in exact range: her attacker first, then the
    // bandits' focus, then the nearest capable unit.
    internal const string HealerHealBelowKey = "bandits.healer.heal-below";
    internal const double HealerHealBelowDefault = 0.9;
    internal const int HealerBasicSafeCells = 2;

    internal static double HealerHealBelow(double? setting)
        => setting is { } value && value > 0 ? Math.Min(value, 1.0) : HealerHealBelowDefault;

    // Shares are floats: 0.9f is a hair under 0.9 and still counts as at the threshold.
    internal static bool HealerHeals(float share, double below) => share < below - DropTolerance;

    internal static bool HealerBasicSafe(int closestHostile) => closestHostile > HealerBasicSafeCells;

    // ---- heal path, support, finishing, the band as one ----

    internal const int HealPathNormalSteps = 8;
    internal const int HealPathEmergencySteps = 12;
    internal const int SupportMinFoeDistance = 3;
    internal const double SupportSeconds = 2.0;

    // The distances that count for GroupCapableNear: this bandit's own and those of its comrades in combat.
    internal static List<int> BandDistances(int self, IEnumerable<(int Distance, bool InCombat)> allies)
    {
        var distances = new List<int> { self };
        if (allies != null)
            foreach (var (distance, inCombat) in allies)
                if (inCombat) distances.Add(distance);
        return distances;
    }

    // A capable player counts as near when it stands within chase cells of any living bandit (bound aggro).
    internal static bool GroupCapableNear(IEnumerable<int> distancesToBandits, int chase)
    {
        if (distancesToBandits == null) return false;
        foreach (var distance in distancesToBandits)
            if (distance <= chase) return true;
        return false;
    }

    // The heal path to a comrade out of reach: "emergency" below EmergencyShare, "normal" below heal-below, else null.
    // Never taunted, never while attacked with a foe within 3 cells, only with a heal free now or within a second.
    internal static string HealPath(float lowestShare, double healBelow, bool healSoon, int distance, int reach, bool attacked, int closestFoe, bool taunted)
    {
        if (taunted || (attacked && closestFoe <= 3) || !healSoon || distance <= reach) return null;
        if (lowestShare < EmergencyShare) return "emergency";
        return HealerHeals(lowestShare, healBelow) ? "normal" : null;
    }

    // (arrival and path length were not measurable): how a heal path ended, null
    // while it runs. Gone or healed above the line first, then on the cell, then the comrade within reach before arriving;
    // a move that stopped short or ran HealPathMaxSeconds game seconds is aborted. "replaced" comes from a new path order.
    internal const float HealPathMaxSeconds = 6f;

    internal static string HealPathResult(bool atCell, int distanceToTarget, int reach, bool targetGone, bool overHeal, bool moving, float seconds)
        => targetGone || overHeal ? "aborted"
            : atCell ? "arrived"
            : distanceToTarget <= reach ? "inrange"
            : !moving || seconds >= HealPathMaxSeconds ? "aborted"
            : null;

    // Steps, foes the path may pass within one cell, and the end cell's least distance to a foe, per heal path mode.
    internal static (int Steps, int PassFoes, int MinFoeDistance) HealPathLimits(string mode)
        => mode == "emergency" ? (HealPathEmergencySteps, 1, 1) : (HealPathNormalSteps, 0, 2);

    // Support without a need to heal: only safe (no foe within 3 cells, not taunted, not held).
    internal static bool SupportSafe(int closestFoe, bool taunted, bool held) => closestFoe > SupportMinFoeDistance && !taunted && !held;

    // Whom she moves up to: the lowest comrade below heal-below, else the one fighting or attacked, else null (the
    // band's centre). comrades: (id, share, fighting).
    internal static string SupportTarget(IReadOnlyList<(string Guid, float Share, bool Fighting)> comrades, double healBelow)
    {
        if (comrades == null) return null;
        string lowest = null;
        var lowestShare = float.MaxValue;
        string fighting = null;
        foreach (var (guid, share, isFighting) in comrades)
        {
            if (HealerHeals(share, healBelow) && share < lowestShare)
            {
                lowest = guid;
                lowestShare = share;
            }
            if (isFighting && fighting == null) fighting = guid;
        }
        return lowest ?? fighting;
    }

    // The herbalist finishes fallen units only when all are down, or when nobody capable stands near any
    // bandit, no comrade is below 90 % and she cannot move up.
    internal static bool HealerMayFinish(bool allDown, bool groupCapableNear, bool allyBelow90, bool regroupPossible)
        => allDown || (!groupCapableNear && !allyBelow90 && !regroupPossible);

    // ordered: attacker, focus, nearest (any may be missing); the first within range, else null.
    internal static string HealerBasicPick(IReadOnlyList<(string Guid, int Distance)> ordered, int range)
    {
        if (ordered == null) return null;
        foreach (var (guid, distance) in ordered)
            if (guid != null && distance > 0 && distance <= range) return guid;
        return null;
    }

    // One protected target per healer for all bandits, held ProtectHoldSeconds
    // while it still threatens her (hit her in the window or targets her); the chain pulls only this one.
    internal const double ProtectHoldSeconds = 1.0;

    // The threat to protect the healer from: the most damage on her in the window, then the nearest to her; without hits
    // the one targeting her within ProtectTargetsReach, the nearest first, then the least health, then the lowest id, so
    // every bandit picks the same whatever the order of its hostiles. Null when nobody threatens her.
    // (Frieren fell in 10 of 10 runs after 9.9 s, picked by=targets dmg3s=0 from far away the
    // moment the party's focus aimed her at the herbalist): aiming alone counts only within 3 cells; a hit counts at any
    // distance, so the party's archer and Zoltraak stay threats once they hit.
    internal const int ProtectTargetsReach = 3;

    internal static bool ProtectThreat(float damage, bool targetsHealer, int distance)
        => damage > 0f || (targetsHealer && distance <= ProtectTargetsReach);

    internal static string ProtectPick(IEnumerable<(string Guid, float Share, int Distance, float Damage, bool TargetsHealer)> candidates)
    {
        var list = new List<(string Guid, float Share, int Distance, float Damage, bool TargetsHealer)>(candidates);
        var byDamage = list.Exists(c => c.Damage > 0f);
        (string Guid, float Share, int Distance, float Damage, bool TargetsHealer)? best = null;
        foreach (var c in list)
        {
            if (string.IsNullOrEmpty(c.Guid) || (byDamage ? !(c.Damage > 0f) : !ProtectThreat(c.Damage, c.TargetsHealer, c.Distance))) continue;
            if (best == null || ProtectBefore(c, best.Value, byDamage)) best = c;
        }
        return best?.Guid;
    }

    private static bool ProtectBefore((string Guid, float Share, int Distance, float Damage, bool TargetsHealer) a,
        (string Guid, float Share, int Distance, float Damage, bool TargetsHealer) b, bool byDamage)
    {
        if (byDamage && a.Damage != b.Damage) return a.Damage > b.Damage;
        if (a.Distance != b.Distance) return a.Distance < b.Distance;
        if (a.Share != b.Share) return a.Share < b.Share;
        return string.CompareOrdinal(a.Guid, b.Guid) < 0;
    }

    // Fallen last goes before protection. A fallen player unit is a protect candidate only
    // while fallen units may be finished (FinishReason not null).
    internal static bool ProtectCandidate(bool player, bool fallen, bool finishAllowed) => player && (!fallen || finishAllowed);

    // The held target stays while its hold runs and it still threatens the healer.
    internal static bool KeepProtect(string held, double heldUntil, double now, bool stillThreat)
        => !string.IsNullOrEmpty(held) && now < heldUntil && stillThreat;

    // (b) Chain and counter-attack go for whoever dealt the healer the most damage within the window;
    // null when nobody hit her in it (then the attacker targeting her counts, as before). Ties: by id.
    internal const float ProtectWindowSeconds = 3f;

    internal static string TopAttacker(IEnumerable<(string Attacker, float Amount, float At)> hits, float now, float window, out float damage, out int hitters)
    {
        var sums = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var (attacker, amount, at) in hits)
        {
            if (string.IsNullOrEmpty(attacker) || amount <= 0f || at > now || now - at > window) continue;
            sums[attacker] = (sums.TryGetValue(attacker, out var sum) ? sum : 0f) + amount;
        }
        string top = null;
        damage = 0f;
        foreach (var entry in sums)
        {
            if (top == null || entry.Value > damage || (entry.Value == damage && string.CompareOrdinal(entry.Key, top) < 0))
            {
                top = entry.Key;
                damage = entry.Value;
            }
        }
        hitters = sums.Count;
        return top;
    }
}
