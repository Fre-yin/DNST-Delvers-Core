using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A party member's part in the group AI (GroupAiRules.DetectRole); GroupAiPolicy picks its rules by it.
internal enum GroupAiRole
{
    Melee,
    Ranged,
    Caster,
    Shield,
    Heal
}

// The group AI's pure rules without game types, shared by the party (GroupAiPolicy) and the
// bandits (GroupAiBandits): roles, thresholds, distances on the grid (Chebyshev, like the game's
// skill range; an area has its skill's own shape, e.g. ArrowRain Manhattan 2), friendly fire of area skills and the status line for the
// Toolkit. AffectTarget and AreaType come in as the game's enum values.
internal static partial class GroupAiRules
{
    // An area skill that would hit allies: wait and announce it, cast once the area is free,
    // skip it when the allies are still inside after HoldSeconds.
    internal enum AreaPhase
    {
        Cast,
        Wait,
        Skip
    }

    internal const string EnabledKey = "groupai.enabled";
    internal const string AddsFirstKey = "groupai.adds-first";
    internal const float EmergencyShare = 0.35f;
    // The party healer (a dedicated healer who deals damage when a window opens): she waits for a
    // heal only when one is free within HealWaitSeconds; no Lifebloom on a target above BloomFullShare.
    internal const double HealWaitSeconds = 1.0;
    internal const float BloomFullShare = 0.9f;
    internal const float HealShare = 0.6f;
    internal const float SeedShare = 0.7f;
    internal const float SeedNearShare = 0.8f;
    internal const float ShieldUpShare = 0.6f;
    internal const float DamageReserve = 0.2f;
    internal const float ShieldReserve = 0.25f;
    internal const float HoldSeconds = 2f;
    internal const int AddsRadius = 6;
    internal const int LogCap = 4000;
    internal const int WarningCap = 3;

    internal static readonly HashSet<string> ShieldSkills = new(StringComparer.Ordinal)
    {
        "SKILL_ShieldUp", "SKILL_GuardiansTaunt", "SKILL_IronClad", "SKILL_BraceStance", "SKILL_ShieldHandling", "SKILL_GuardBarrier"
    };

    internal static readonly HashSet<string> HealSkills = new(StringComparer.Ordinal)
    {
        "SKILL_StrangeSeed", "SKILL_DelversLifebloom", "SKILL_HealingTouchAttack"
    };

    internal const int MaxDodgeRing = 3;

    // A heal or ally skill makes a healer, a shield skill a shield; otherwise the basic attack decides.
    internal static GroupAiRole DetectRole(IReadOnlyList<string> slots, string basicAttack, Func<string, bool> isAllySkill, bool basicRanged, bool basicMagic)
    {
        if (basicAttack != null && HealSkills.Contains(basicAttack)) return GroupAiRole.Heal;
        foreach (var slot in slots)
            if (slot != null && (HealSkills.Contains(slot) || isAllySkill(slot))) return GroupAiRole.Heal;
        foreach (var slot in slots)
            if (slot != null && ShieldSkills.Contains(slot)) return GroupAiRole.Shield;
        if (basicRanged) return GroupAiRole.Ranged;
        return basicMagic ? GroupAiRole.Caster : GroupAiRole.Melee;
    }

    internal static string Short(string key) => (key ?? "").Replace("SKILL_", "");

    internal static string RuleFor(string prefix, string skill) => prefix + "-" + Short(skill).ToLowerInvariant();

    internal static int Distance(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    // Share of the whole health (head and body) of the maximum.
    internal static float Share(float head, float body, float max)
    {
        if (max > 0f) return Math.Clamp((head + body) / max, 0f, 1f);
        return 1f;
    }

    internal static bool HasReserve(float energy, float maxEnergy, float cost, float reserve)
        => cost <= 0f || energy - cost >= reserve * maxEnergy;

    // In game seconds; a stamp after now (the tick jumped back) counts as expired.
    internal static bool HoldExpired(float since, float now) => GameClockRules.Elapsed(now, since, HoldSeconds);

    // SingleAny, MultipleAny and MultipleAnyExceptMe also hit the caster's own side.
    // heal-wait kept her from Zoltraak in 3 of 5 runs. She waits only while someone is low and a
    // heal comes free within HealWaitSeconds (0 = free now but not castable here, which is no reason to wait).
    internal static bool HealWaits(bool anyAllyLow, IReadOnlyList<double> secondsUntilFree)
    {
        if (!anyAllyLow || secondsUntilFree == null) return false;
        foreach (var seconds in secondsUntilFree)
            if (seconds > 0 && seconds <= HealWaitSeconds) return true;
        return false;
    }

    // Frieren grows her own seed at once (mucus sprayer or solar bloom), which
    // sets off the Life Burst on everyone next to it, when someone there is below EmergencyShare, or below HealShare
    // while Lifebloom is not free. lowestAtSeed is the lowest share next to the seed (null when nobody stands there).
    internal static bool SeedBurstNow(float? lowestAtSeed, bool bloomFree)
        => lowestAtSeed is { } share && (share < EmergencyShare || (share < HealShare && !bloomFree));

    // Where the seed burst stopped, in the order it checks: no free upgrade on the bar,
    // no own seed, the own seed out of the upgrade's range, nobody next to it, SeedBurstNow false; "burst" when it grows.
    internal static readonly string[] SeedCheckStages = { "slot", "seed", "range", "adjacent", "rule", "burst" };

    internal const float SeedCheckGap = 2f;

    // The check line is written when the stage changes for a caster, at most once per SeedCheckGap game seconds.
    internal static bool SeedCheckDue(string lastStage, float lastAt, string stage, float now)
        => stage != lastStage && (lastStage == null || GameClockRules.Elapsed(now, lastAt, SeedCheckGap));

    // A seed is next to a cell when it stands on one of the eight cells around it (the game's GetAdjacentWithDiagonal).
    internal static bool NextToSeed(int seedX, int seedY, int x, int y)
        => (seedX != x || seedY != y) && Math.Abs(seedX - x) <= 1 && Math.Abs(seedY - y) <= 1;

    // 22 to 45 % of the party's Lifeblooms landed on full health.
    internal static bool BloomTargetUseful(float share) => share <= BloomFullShare;

    // The emergency heal goes to the lowest of the party, the healer herself included: "self", "ally" or null.
    internal static string EmergencyPick(float? lowestAllyShare, float selfShare)
    {
        var ally = lowestAllyShare ?? float.MaxValue;
        if (selfShare < EmergencyShare && selfShare < ally) return "self";
        return ally < EmergencyShare ? "ally" : null;
    }

    internal static bool CanHitAllies(int affectTarget) => affectTarget == 2 || affectTarget == 4 || affectTarget == 5;

    internal static string FlagText(int affectTarget)
        => "ffFlag=AffectTarget:" + affectTarget switch
        {
            2 => "SingleAny",
            4 => "MultipleAny",
            5 => "MultipleAnyExceptMe",
            _ => affectTarget.ToString(CultureInfo.InvariantCulture)
        };

    // "Ansage einlösen": an announced area skill comes first at the
    // next decision, before the order of the bar; once its area is free it is cast. It is dropped
    // when it cannot be: the reason, or null when it can.
    internal static string RedeemBlocker(bool ready, bool energy, bool hasTarget, bool inRange)
        => !ready ? "cooldown" : !energy ? "energy" : !hasTarget ? "target" : !inRange ? "range" : null;

    internal static AreaPhase AreaPhaseOf(int alliesInArea, bool marked, float since, float now)
    {
        if (alliesInArea > 0)
        {
            if (marked && HoldExpired(since, now)) return AreaPhase.Skip;
            return AreaPhase.Wait;
        }
        return AreaPhase.Cast;
    }

    internal static long CellKey(int x, int y) => ((long)x << 32) | (uint)y;

    // The cell the unit casts from: it walks straight towards the target until the skill's range
    // reaches it.
    internal static (int X, int Y) CastCell(int fromX, int fromY, int targetX, int targetY, int range)
    {
        if (range <= 0) return (fromX, fromY);
        for (var i = 0; i < 64; i++)
        {
            if (Distance(fromX, fromY, targetX, targetY) <= range) break;
            fromX += Math.Sign(targetX - fromX);
            fromY += Math.Sign(targetY - fromY);
        }
        return (fromX, fromY);
    }

    // The nearest usable cell in rings around the unit; a melee unit with an anchor (its target)
    // prefers a cell next to it.
    internal static bool PickDodgeCell(int x, int y, bool hasAnchor, int ax, int ay, Func<int, int, bool> usable, int maxRing, out int tx, out int ty)
    {
        tx = x;
        ty = y;
        var found = false;
        var foundNear = false;
        var bestRing = int.MaxValue;
        for (var ring = 1; ring <= maxRing; ring++)
        {
            for (var dy = ring; dy >= -ring; dy--)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                    var cx = x + dx;
                    var cy = y + dy;
                    if (!usable(cx, cy)) continue;
                    var near = hasAnchor && Distance(cx, cy, ax, ay) == 1;
                    if (!found || ((!foundNear || near) && (foundNear != near || ring < bestRing)))
                    {
                        found = true;
                        foundNear = near;
                        bestRing = ring;
                        tx = cx;
                        ty = cy;
                    }
                }
            }
        }
        return found;
    }

    internal static bool InRange(int distance, int range) => range <= 0 || distance <= range + 1;

    // The exact range, without the game's extra cell (the party healer).
    internal static bool InRangeExact(int distance, int range) => range <= 0 || distance <= range;

    // In game seconds; after a jump back of the tick the buff counts as ended (until lies too far ahead).
    internal static bool BuffRunning(float until, float now, float duration) => now < until && until - now <= duration;

    // Line areas (AreaType 7 and 8) need a target to aim at even without a combat target.
    internal static bool IsLine(int areaType) => areaType == 7 || areaType == 8;

    internal static string Status(bool enabled, int decisions, int holds, int dodges, int friendlyFireSkips, int shieldUpOutsideBoss, int tauntLate,
        int damageWhileAllyLow, float lowestShare, int seeds, int lifeblooms, int announcedCast, int announcedDropped, IReadOnlyDictionary<string, int> rules)
    {
        var sorted = new List<KeyValuePair<string, int>>(rules);
        sorted.Sort(CompareRuleCounts);
        var parts = new List<string>();
        foreach (var rule in sorted) parts.Add(rule.Key + ":" + rule.Value.ToString(CultureInfo.InvariantCulture));
        return string.Format(CultureInfo.InvariantCulture,
            "enabled={0} decisions={1} holds={2} dodges={3} friendlyFireSkips={4} shieldUpOutsideBoss={5} tauntLate={6} damageWhileAllyLow={7} lowestShare={8:0.00} seeds={9} lifeblooms={10} announcedCast={11} announcedDropped={12} rules={13}",
            enabled ? "true" : "false", decisions, holds, dodges, friendlyFireSkips, shieldUpOutsideBoss, tauntLate, damageWhileAllyLow, lowestShare, seeds,
            lifeblooms, announcedCast, announcedDropped, parts.Count == 0 ? "-" : string.Join(",", parts));
    }

    // The bandits' log cap per fight (GroupAiBandits.Log): a combat decision after gap seconds without one starts a new fight.
    internal static bool NewFight(double lastCombatDecision, double now, double gap) => double.IsNegativeInfinity(lastCombatDecision) || now - lastCombatDecision >= gap || now < lastCombatDecision;

    // The party's shield wall: when the fight's leader targets the shield (a boss, else the strongest
    // enemy), and now also when an elite enemy does. Only this rule; the target choice stays as it is.
    internal static string ShieldWallReason(bool leaderTargetsMe, bool leaderIsBoss, bool eliteTargetsMe)
        => leaderTargetsMe ? (leaderIsBoss ? "boss" : "leader") : eliteTargetsMe ? "elite" : null;

    // A shield wall the unit ends up with, Core's or the game's, while neither a boss nor an elite is
    // on it (ShieldWallReason gives boss or elite). The counter was never raised.
    internal static bool ShieldUpOutsideBoss(string skill, string wallReason)
        => skill == "SKILL_ShieldUp" && wallReason is not ("boss" or "elite");

    // Most used first, then by name.
    private static int CompareRuleCounts(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
    {
        var byCount = b.Value.CompareTo(a.Value);
        if (byCount == 0) return string.CompareOrdinal(a.Key, b.Key);
        return byCount;
    }
}
