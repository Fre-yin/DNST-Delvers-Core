using DungeonSettlersDelvers.Core;
using static TestKit;

// The seed measurement (BanditSeedTallyRules): a seed's bandits, heals and healing, a wasted seed, and case 1
// of the annoyed herbalist. A measurement only, no call.
internal static class BanditSeedTallySection
{
    internal static void Run()
    {
        var seed = new BanditSeedTallyRules.Seed("h", 10f);
        seed.Step(new[] { "a", "b" }, 5f);
        seed.Step(new[] { "a" }, 5f);
        seed.Step(System.Array.Empty<string>(), 5f);
        Check(seed.Stood.Count == 2 && seed.Steps == 3 && seed.Heals == 3 && Math.Abs(seed.Healed - 15f) < 0.001f && !seed.Wasted,
            "seed tally: a seed counts the bandits that stood in it once, each step and each heal");
        var empty = new BanditSeedTallyRules.Seed("h", 0f);
        empty.Step(null, 5f);
        Check(empty.Wasted && empty.Steps == 1 && empty.Stood.Count == 0, "seed tally: a seed that healed nobody is wasted (case 2)");
        var grown = new BanditSeedTallyRules.Seed("h", 0f) { Upgraded = true };
        Check(!grown.Wasted && BanditSeedTallyRules.EndLine("g", grown, 1f, 0, 1).Contains("wasted=0 upgraded=1"),
            "seed tally: a seed the herbalist grew soon after is upgraded, not wasted (life=1.0)");
        Check(BanditSeedTallyRules.EndLine("s", seed, 22.5f, 3, 99)
                == "CORE_SEED_TALLY_END seed=s summoner=h life=12.5 bandits=2 steps=3 heals=3 healed=15.0 wasted=0 upgraded=0 outsideHits=3 tick=99",
            "seed tally: the end line names life, bandits, steps, heals, healing, waste and the hits outside");
        Check(BanditSeedTallyRules.Annoys(0.3f, 1, false) && !BanditSeedTallyRules.Annoys(0.31f, 1, false)
                && !BanditSeedTallyRules.Annoys(0.1f, 0, false) && !BanditSeedTallyRules.Annoys(0.1f, 2, true),
            "seed tally: case 1 counts a bandit at or below 30 % hit outside every aura, only while a seed stands");
        Check(BanditSeedTallyRules.HitLine("u", 0.25f, 1, 7) == "CORE_SEED_TALLY_HIT unit=u share=0.25 seeds=1 tick=7"
                && BanditSeedTallyRules.BeginLine("s", "h", new[] { (3, 4), (-1, 2) }, 5) == "CORE_SEED_TALLY_BEGIN seed=s summoner=h cells=3,4|-1,2 tick=5",
            "seed tally: the hit line, and the begin line with the aura's cells");
    }
}
