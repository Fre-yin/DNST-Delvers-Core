using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A measurement only, no call and no change to the fight (before the bandits' calls): how standing in the
// herbalist's seed relates to its healing, and how often the annoyed herbalist (call 9) would fire. Per Core seed its life in game
// seconds, the bandits that stood in its aura at a heal step, its steps, heals and healing; "wasted" a seed that ended without a
// heal (case 2). Case 1, a bandit at or below AnnoyedShare hit outside every aura of a living Core seed, is counted by
// BanditSeedTally. Rules without the game.
internal static class BanditSeedTallyRules
{
    internal const float AnnoyedShare = 0.3f;
    internal const float LookSeconds = 1f;
    internal const int LineCap = 200;

    internal sealed class Seed
    {
        internal Seed(string summoner, float bornAt)
        {
            Summoner = summoner ?? "-";
            BornAt = bornAt;
        }

        internal string Summoner { get; }
        internal float BornAt { get; }
        internal HashSet<string> Stood { get; } = new(StringComparer.Ordinal);
        internal int Steps { get; private set; }
        // The herbalist grew it (Life Burst or the sprayer): it ends as upgraded, not as unused.
        internal bool Upgraded { get; set; }
        internal int Heals { get; private set; }
        internal float Healed { get; private set; }
        internal bool Wasted => Heals == 0 && !Upgraded;

        // One step of the aura: the bandits standing in it, each healed by amount.
        internal void Step(IEnumerable<string> units, float amount)
        {
            Steps++;
            foreach (var unit in units ?? Array.Empty<string>())
            {
                Stood.Add(unit);
                Heals++;
                Healed += Math.Max(0f, amount);
            }
        }
    }

    // Case 1: low, hit, and outside every living Core seed's aura while at least one stands.
    internal static bool Annoys(float share, int seeds, bool inAnyAura) => seeds > 0 && share <= AnnoyedShare && !inAnyAura;

    // cells: the aura's cells as the game keeps them (no fixed radius), so "stands in the seed" can be checked from the log.
    internal static string BeginLine(string seed, string summoner, IEnumerable<(int X, int Y)> cells, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TALLY_BEGIN seed={0} summoner={1} cells={2} tick={3}", seed ?? "-", summoner ?? "-",
            cells == null ? "-" : string.Join("|", cells.Select(c => c.X.ToString(CultureInfo.InvariantCulture) + "," + c.Y.ToString(CultureInfo.InvariantCulture))), tick);

    internal static string EndLine(string seed, Seed tally, float endedAt, int hitsOutside, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_SEED_TALLY_END seed={0} summoner={1} life={2:0.0} bandits={3} steps={4} heals={5} healed={6:0.0} wasted={7} upgraded={8} outsideHits={9} tick={10}",
            seed ?? "-", tally.Summoner, Math.Max(0f, endedAt - tally.BornAt), tally.Stood.Count, tally.Steps, tally.Heals, tally.Healed,
            tally.Wasted ? 1 : 0, tally.Upgraded ? 1 : 0, hitsOutside, tick);

    internal static string HitLine(string unit, float share, int seeds, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TALLY_HIT unit={0} share={1:0.00} seeds={2} tick={3}", unit ?? "-", share, seeds, tick);
}
