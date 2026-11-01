using DungeonSettlersDelvers.Core;
using static TestKit;

// The guard's chain pull and offensive pull.
internal static class BanditChainSection
{
    internal static void Run()
    {
        ChainPull();
        OffensivePull();
    }

    static void ChainPull()
    {
        // The guard's chain pull is a copy under its own key with its own icon; a
        // saved guard with the game's key gets the copy after loading, the boss never; the key never reaches the game's save.
        Check(BanditChainRules.GameKey == "SKILL_ChainGrap" && BanditChainRules.SkillKey == "SKILL_DelversCoreBanditChainGrap"
                && BanditChainRules.IconFile == "Banditen_Kettenzug_32x32.png"
                && BanditChainRules.NameKey(BanditChainRules.SkillKey) == "TEXTKEY_SKILL_DelversCoreBanditChainGrap_NAME"
                && BanditChainRules.Migrates(true, true, true, false) && !BanditChainRules.Migrates(false, true, true, false)
                && !BanditChainRules.Migrates(true, false, true, false) && !BanditChainRules.Migrates(true, true, false, false)
                && !BanditChainRules.Migrates(true, true, true, true)
                && BanditChainRules.MigrateLine("u", "K") == "CORE_BANDIT_CHAIN_MIGRATED unit=u key=K from=SKILL_ChainGrap to=SKILL_DelversCoreBanditChainGrap"
                && Array.IndexOf(CoreSaveSkills.Keys, BanditChainRules.SkillKey) >= 0
                && Array.IndexOf(CoreUnitRules.Units.First(u => u.Role == "guard").Skills, BanditChainRules.SkillKey) >= 0
                && CoreUnitRules.Units.All(u => Array.IndexOf(u.Skills, BanditChainRules.GameKey) < 0),
            "bandit chain: own key and icon, the guard's kit carries it, old guards migrate, never the game's save");
        Check(BanditChainRules.PullSkip(true, true, true, 5, 3, true, true) == null && BanditChainRules.PullSkip(false, false, true, 5, 3, true, true) == "nodata"
                && BanditChainRules.PullSkip(true, true, false, 5, 3, true, true) == "cooldown" && BanditChainRules.PullSkip(true, true, true, 2, 3, true, true) == "close"
                && BanditChainRules.PullSkip(true, true, true, 9, 3, false, true) == "range" && BanditChainRules.PullSkip(true, true, true, 5, 3, true, false) == "nopeel"
                && BanditChainRules.PullSkipLine("u", "close", 9) == "CORE_AI_BANDIT_PULL_SKIP unit=u reason=close tick=9",
            "bandit chain: why a guard does not pull, in the order the rule checks it");
        Check(BanditChainRules.RangeKey == "bandits.chain.range" && BanditChainRules.Range(null) == 7 && BanditChainRules.Range(10) == 10
                && BanditChainRules.Range(25) == 10 && BanditChainRules.Range(0) == 1 && BanditChainRules.Range(5.4) == 5,
            "bandit chain: the copy reaches 7 cells by default, 1 to 10 by setting");
    }

    static void OffensivePull()
    {
        // The guard's offensive pull on a water, nature or fire mage or a bow user, the most damage first,
        // when the herbalist is left alone for 6 s; then wait for the root, stun, Hammering; the archer focuses 4 s.
        Check(BanditOffensiveRules.ClassOf(107) == "bow" && BanditOffensiveRules.ClassOf(108) == "fire" && BanditOffensiveRules.ClassOf(109) == "water"
                && BanditOffensiveRules.ClassOf(110) == "nature" && BanditOffensiveRules.ClassOf(106) == "spear" && BanditOffensiveRules.ClassOf(101) == "sword"
                && BanditOffensiveRules.ClassOf(102) == "greatsword" && BanditOffensiveRules.ClassOf(103) == null && BanditOffensiveRules.ClassOf(104) == null
                && BanditOffensiveRules.ClassOf(105) == null && BanditOffensiveRules.ClassOf(0) == null
                && BanditOffensiveRules.TierOf("fire") == 1 && BanditOffensiveRules.TierOf("bow") == 1 && BanditOffensiveRules.TierOf("sword") == 2
                && BanditOffensiveRules.TierOf("spear") == 2 && BanditOffensiveRules.TierOf(null) == 0
                && BanditOffensiveRules.May(6, 6, 20, 20, false) && !BanditOffensiveRules.May(5.9, 6, 99, 20, false)
                && !BanditOffensiveRules.May(99, 6, 19, 20, false) && !BanditOffensiveRules.May(99, 6, 99, 20, true)
                && BanditOffensiveRules.May(double.PositiveInfinity, 6, double.PositiveInfinity, 20, false)
                && BanditOffensiveRules.Idle(double.PositiveInfinity, 1) == 1 && !BanditOffensiveRules.May(BanditOffensiveRules.Idle(double.PositiveInfinity, 1), 6, 99, 20, false)
                && BanditOffensiveRules.Idle(double.PositiveInfinity, 6.5) == 6.5 && BanditOffensiveRules.Idle(2, 30) == 2
                && BanditOffensiveRules.Pick(new[] { ("a", 50f, 6), ("b", 120f, 7), ("c", 120f, 4) }) == "c"
                && BanditOffensiveRules.Pick(new (string, float, int)[0]) == null
                && BanditOffensiveRules.After(BanditOffensiveRules.Step.WaitRoot) == BanditOffensiveRules.Step.Stun
                && BanditOffensiveRules.After(BanditOffensiveRules.Step.Stun) == BanditOffensiveRules.Step.Hammer
                && BanditOffensiveRules.After(BanditOffensiveRules.Step.Hammer) == BanditOffensiveRules.Step.Done
                && BanditOffensiveRules.RootKey == "AFFECTER_Rooting" && BanditOffensiveRules.IdleDefault == 6 && BanditOffensiveRules.FocusDefault == 4
                && BanditOffensiveRules.CooldownDefault == 20 && BanditOffensiveRules.Seconds(null, 6, 60) == 6 && BanditOffensiveRules.Seconds(99, 6, 60) == 60
                && BanditOffensiveRules.PullLine("g", "p", "fire", 7.25, 340, 9) == "CORE_AI_BANDIT_PULL unit=g reason=offensive target=p class=fire tier=1 idle=7.3 damage=340 tick=9",
            "bandit offensive pull: classes by weapon, idle and lock, the most damage then the nearer, the chain's order");
        Check(GroupAiRules.NewFight(double.NegativeInfinity, 5, 10) && GroupAiRules.NewFight(100, 110, 10) && !GroupAiRules.NewFight(100, 109.9, 10)
                && GroupAiRules.NewFight(100, 50, 10),
            "bandit log: a combat decision after 10 s without one starts a new fight (the per fight line cap resets)");
    }
}
