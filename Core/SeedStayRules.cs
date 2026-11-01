using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// "Im Samen bleiben": a unit low on health walks once into the aura of a seed of its own side and
// stays there until it is healed up. It enters at seeds.stay.below (0.30) of its health and leaves at seeds.stay.until (0.50);
// the party's emergency share (GroupAiRules.EmergencyShare, 0.35) is another setting and stays as it is. One move order per
// stay and seed, never sent again: when it ends outside the aura, the stay at that seed is over. Inside the aura the unit
// attacks what is in reach, otherwise it holds. Rules without the game.
internal static class SeedStayRules
{
    internal const string BelowKey = "seeds.stay.below";
    internal const string UntilKey = "seeds.stay.until";
    internal const double BelowDefault = 0.30;
    internal const double UntilDefault = 0.50;

    // A seed with less left than this is not worth the walk (its aura ends on the way).
    internal const float MinSecondsLeft = 2f;

    internal const int LogCap = 120;

    // The rule of a staying unit that holds: the game gets no action (GroupAiRules.HoldsStill), so it does not walk to a foe.
    internal const string HoldRule = "seed-hold";

    internal static double Below(double? setting) => Clamp(setting, BelowDefault);

    // Never below the entry share, so a stay always ends above where it started.
    internal static double Until(double? setting, double below) => Math.Max(Clamp(setting, UntilDefault), below);

    private static double Clamp(double? setting, double fallback)
        => setting is double value && !double.IsNaN(value) ? Math.Clamp(value, 0.0, 1.0) : fallback;

    // Whether the unit stays (or starts to stay): it enters at or below the entry share and keeps staying below the exit share.
    // Health shares are floats; the shares of the settings are compared as floats too, so 30 % is "at" 0.30.
    internal static bool Stays(bool staying, float share, double below, double until)
        => staying ? share < (float)until : share <= (float)below;

    // A seed is worth walking to while its life is unknown (0: aura not seen yet) or at least MinSecondsLeft.
    internal static bool SeedUsable(float secondsLeft, float life) => life < 0f || secondsLeft >= MinSecondsLeft;

    // The cell to walk to: the free cell of the area closest to the unit, ties to the first in the list; null without one.
    internal static (int X, int Y)? PickCell(IReadOnlyList<(int X, int Y)> area, ICollection<(int X, int Y)> taken, int fromX, int fromY)
    {
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        if (area == null) return null;
        foreach (var cell in area)
        {
            if (taken != null && taken.Contains(cell)) continue;
            var distance = GroupAiRules.Distance(fromX, fromY, cell.X, cell.Y);
            if (distance >= bestDistance) continue;
            best = cell;
            bestDistance = distance;
        }
        return best;
    }

    internal static bool InArea(IReadOnlyList<(int X, int Y)> area, int x, int y)
    {
        if (area == null) return false;
        foreach (var cell in area)
            if (cell.X == x && cell.Y == y) return true;
        return false;
    }

    // What a staying unit does: "walk" (the one order), "attack" (something in reach), "hold", or "end" (the order ended outside
    // the aura, or no cell to walk to).
    internal static string Step(bool inArea, bool ordered, bool hasCell, bool inReach)
        => inArea ? (inReach ? "attack" : "hold") : ordered || !hasCell ? "end" : "walk";

    // Life Burst for the bandits: the herbalist grows her own seed at once when a comrade on it or next
    // to it is at or below the stay's entry share (seeds.stay.below; the party's 0.35 is the party's). The mucus sprayer, the solar
    // bloom when the sprayer is not free (a bandit's bloom shoots the party, BanditSolarBloom); null: neither.
    internal static string BurstSkill(bool sprayerFree, bool bloomFree)
        => sprayerFree ? GroupAiHealerSeedRules.SprayerSkill : bloomFree ? BloomSkill : null;

    internal const string BloomSkill = "SKILL_SolarBloom";

    internal static bool BurstNow(float? lowestAtSeed, double below) => lowestAtSeed is { } share && share <= (float)below;

    internal static string StayLine(string side, string unit, string seed, string step, float share, int x, int y, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_SEED_STAY side={0} unit={1} seed={2} step={3} share={4:0.00} at={5},{6} tick={7}",
            side, unit ?? "-", seed ?? "-", step, share, x, y, tick);

    internal static string NoSeedLine(string side, string unit, int seeds, int ofSide, int usable, float share, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_SEED_STAY side={0} unit={1} step=noseed seeds={2} ofSide={3} usable={4} share={5:0.00} tick={6}",
            side, unit ?? "-", seeds, ofSide, usable, share, tick);

    internal static string BanditBurstLine(string unit, string seed, string skill, float lowest, string comrade, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SEED_BURST unit={0} seed={1} skill={2} lowest={3:0.00} comrade={4} tick={5}",
            unit ?? "-", seed ?? "-", GroupAiRules.Short(skill), lowest, comrade ?? "-", tick);
}
