namespace DungeonSettlersDelvers.Core;

// The arithmetic of Core's locked share of health and energy while a unit is capped (revival
// sickness or a down's after-effect). Pure, so the offline tests cover it; ReviveSickness writes
// the share, ReviveSicknessBars and the cap readers read it, and all of them use these rules.
//
// A share is a negative percent value Core adds to the game's percent modifier of a maximum, and
// mirrors in a marker stat, so Core can tell its own part from the part of traits and gear:
//   total percent = foreign percent + own share,  maximum = base x (1 + total percent / 100)
// The cap (the unlocked part of the maximum) follows: own = -(1 - cap) x (100 + foreign).
internal static class ReviveShareRules
{
    // A mirrored share smaller than this counts as no share.
    internal const float MinShare = 0.01f;

    // A cap from this on is lifted: the full maximum comes back and nothing is drawn locked.
    internal const float LiftedFrom = 0.995f;

    internal static bool HasShare(float own) => Math.Abs(own) >= MinShare;

    internal static bool IsLifted(float cap) => cap >= LiftedFrom;

    // The percent part that does not come from Core.
    internal static float Foreign(float totalPercent, float own) => totalPercent - own;

    // The cap the current share stands for, not limited to 0..1.
    internal static float CapOfShare(float own, float foreign) => 1f + own / Math.Max(1f, 100f + foreign);

    // The unlocked share of the maximum for the bars, null when Core has nothing locked.
    internal static float? LockedCap(float own, float totalPercent)
        => HasShare(own) ? Math.Clamp(CapOfShare(own, Foreign(totalPercent, own)), 0f, 1f) : null;

    // The share that holds the maximum at the given cap.
    internal static float ShareForCap(float cap, float foreign) => -(1f - cap) * (100f + foreign);

    // A maximum without Core's share. The game computes base x (1 + percent / 100), so the stat
    // read now is scaled from the total percent to the foreign percent. A corpse can still carry
    // the share of a down; leaving that share in made a revive start at 6.5 % of the full
    // maximum instead of 10 %.
    internal static float FullMax(float maxNow, float own, float totalPercent)
    {
        var divisor = 100f + totalPercent;
        return divisor <= 1f ? maxNow : maxNow * (100f + totalPercent - own) / divisor;
    }
}
