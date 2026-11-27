using DungeonSettlersDelvers.Core;
using static TestKit;

// Core's chance on the seed: the same inputs give the same value, every input moves it, the streams keep
// their numbers, and the values spread evenly, so every chance keeps its odds.
internal static class CoreRandomSection
{
    internal static void Run()
    {
        var campaign = Guid.Parse("4799f5bf-0000-4000-8000-000000000001");
        var unit = Guid.Parse("7777cd79-53b6-4675-8f0f-6ffd053b9d3f");
        var first = CoreRandomRules.Value(campaign, 4096528, unit, CoreRandomRules.Wounds, 0);
        Check(first == CoreRandomRules.Value(campaign, 4096528, unit, CoreRandomRules.Wounds, 0) && first >= 0 && first < 1
                && first != CoreRandomRules.Value(campaign, 4096529, unit, CoreRandomRules.Wounds, 0)
                && first != CoreRandomRules.Value(campaign, 4096528, Guid.Empty, CoreRandomRules.Wounds, 0)
                && first != CoreRandomRules.Value(campaign, 4096528, unit, CoreRandomRules.EnemyAbilities, 0)
                && first != CoreRandomRules.Value(campaign, 4096528, unit, CoreRandomRules.Wounds, 1)
                && first != CoreRandomRules.Value(Guid.NewGuid(), 4096528, unit, CoreRandomRules.Wounds, 0),
            "core random: the same campaign, tick, unit, stream and count give the same value; each of them moves it");
        Check(CoreRandomRules.Streams.Select(s => s.Stream).SequenceEqual(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
                && CoreRandomRules.Streams.Select(s => s.Function).SequenceEqual(new[] { "wounds", "enemy-abilities", "kit-drops", "founders", "bandit-wake", "bandit-traits", "bandit-combos", "bandit-calls", "bandit-patrol" })
                && CoreRandomRules.ModId == "delvers" && CoreRandomRules.Key(Guid.Empty) == CoreRandomRules.Key(Guid.Empty),
            "core random: the streams keep their numbers and functions for good, the bridge's mod id is delvers");
        // The odds: 20000 draws over ticks and units, every tenth of [0, 1) holds about a tenth, and a 25 % chance fires about a quarter.
        var buckets = new int[10];
        var below = 0;
        for (var i = 0; i < 20000; i++)
        {
            var value = CoreRandomRules.Value(campaign, 1000 + i / 7, new Guid(i, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10), CoreRandomRules.EnemyAbilities, (uint)(i % 7));
            buckets[(int)(value * 10)]++;
            if (value < 0.25) below++;
        }
        Check(buckets.All(count => count > 1800 && count < 2200) && below > 4700 && below < 5300,
            "core random: the values spread evenly over [0, 1), so a chance keeps its odds");
        Check(CoreRandomRules.Line("seed", "no-bridge") == "CORE_RANDOM source=seed reason=no-bridge",
            "core random: its line names the source");
    }
}
