using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The condition of a bandit's kit when its corpse is undressed: a boss part in good but not perfect
// condition, the rest of a bandit's gear worn down (the author's rule from the bandit planning). A party member's equipment
// keeps what it has. The game lays each dropped item on the corpse's cell or a free one beside it; Core finds it again by key
// and distance (MatchDrop). Rules without the game.
internal static class KitDropRules
{
    internal const double BossShareMin = 0.75;
    internal const double BossShareMax = 0.95;
    internal const double BanditShareMin = 0.23;
    internal const double BanditShareMax = 0.34;

    // The durability a dropped kit item gets: a share of its maximum picked by roll (0..1) in its range.
    internal static double DropDurability(double max, double roll, bool boss)
    {
        if (max <= 0.0) return 0.0;
        var (min, top) = boss ? (BossShareMin, BossShareMax) : (BanditShareMin, BanditShareMax);
        return Math.Round(max * (min + (top - min) * Math.Clamp(roll, 0.0, 1.0)));
    }

    // No item spawned on the noted cell itself. A spawned item belongs to the noted drop of the same
    // key whose cell is nearest, at most DropRadius cells away (the larger of the two distances); the oldest wins a tie. -1
    // for none.
    internal const int DropRadius = 3;

    internal static int MatchDrop(IReadOnlyList<(string Key, int X, int Y)> pending, string key, int x, int y)
    {
        var best = -1;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < pending.Count; i++)
        {
            if (pending[i].Key != key) continue;
            var distance = Math.Max(Math.Abs(pending[i].X - x), Math.Abs(pending[i].Y - y));
            if (distance > DropRadius || distance >= bestDistance) continue;
            best = i;
            bestDistance = distance;
        }
        return best;
    }

    // CORE_KIT_DROPPED: a dropped kit item's durability before and after; -1 when the item had no status.
    internal static string DropLine(string key, int x, int y, double before, double max, double after)
        => string.Format(CultureInfo.InvariantCulture, "CORE_KIT_DROPPED key={0} at={1},{2} durability={3:0.#}->{4:0.#} max={5:0.#}",
            key, x, y, before, after, max);

    // CORE_KIT_SPAWN_UNMATCHED: a boss part the game spawned without a noted drop near it (a trade, a load, a far cell).
    internal static string UnmatchedLine(string key, int x, int y, int pending)
        => string.Format(CultureInfo.InvariantCulture, "CORE_KIT_SPAWN_UNMATCHED key={0} at={1},{2} pending={3}", key, x, y, pending);
}
