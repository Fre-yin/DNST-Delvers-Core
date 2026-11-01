using DungeonSettlersDelvers.Core;
using static TestKit;

// Deadly Fear and the test seed.
internal static class BanditFearSection
{
    internal static void Run()
    {
        TestSeed();
        DeadlyFear();
    }

    static void TestSeed()
    {
        // Test tool: a herbalist plants her seed or grows it into the sprayer on request.
        Check(GroupAiHealerSeedRules.TestSeedLine("h", "t", true, "cast") == "CORE_TEST_BANDIT_SEED unit=h target=t upgrade=1 result=cast"
                && GroupAiHealerSeedRules.TestSeedLine(null, null, false, "nohealer") == "CORE_TEST_BANDIT_SEED unit=- target=- upgrade=0 result=nohealer"
                && GroupAiHealerSeedRules.TestSeedResult(true, true, true, true) == "cast" && GroupAiHealerSeedRules.TestSeedResult(false, true, true, true) == "noskill"
                && GroupAiHealerSeedRules.TestSeedResult(true, false, true, true) == "cooldown" && GroupAiHealerSeedRules.TestSeedResult(true, true, false, true) == "notarget"
                && GroupAiHealerSeedRules.TestSeedResult(true, true, true, false) == "range",
            "bandit seed test tool: one line per request, the reason when it cannot cast");
    }

    static void DeadlyFear()
    {
        // Deadly Fear sets up Slay or Destructive Split.
        Check(BanditFearRules.SkillKey == "SKILL_DeadlyFear" && BanditFearRules.FlawKey == "AFFECTER_ExposedFlaw" && BanditFearRules.Rule == "blade-fear-setup"
                && BanditFearRules.BigReadyIn(false, true, false, 0) == null && BanditFearRules.BigReadyIn(true, true, false, 5) == 0
                && BanditFearRules.BigReadyIn(true, false, true, 0.6) == 0.6 && BanditFearRules.BigReadyIn(true, false, false, 0) == null
                && BanditFearRules.Soonest(null, 2) == 2 && BanditFearRules.Soonest(0.5, 2) == 0.5 && BanditFearRules.Soonest(null, null) == null
                && BanditFearRules.Skip(true, false, true, false, 0) == null && BanditFearRules.Skip(true, false, true, false, 1.0) == null
                && BanditFearRules.Skip(true, false, true, false, 1.5) == "nobig" && BanditFearRules.Skip(true, false, true, false, null) == "nobig"
                && BanditFearRules.Skip(true, false, true, true, 0) == "flaw" && BanditFearRules.Skip(true, false, false, false, 0) == "range"
                && BanditFearRules.Skip(true, true, true, false, 0) == "downed" && BanditFearRules.Skip(false, false, false, false, 0) == "target"
                && BanditFearRules.SkipLine("u", "t", "nobig", 4.24, 9) == "CORE_AI_BANDIT_FEAR_SKIP unit=u target=t reason=nobig big=4.2 tick=9"
                && BanditFearRules.SkipLine("u", null, "target", null, 9) == "CORE_AI_BANDIT_FEAR_SKIP unit=u target=- reason=target big=- tick=9",
            "bandit blade: Deadly Fear only on a target without ExposedFlaw while Slay or Destructive Split is ready within 1 s, never on the downed");
    }
}
