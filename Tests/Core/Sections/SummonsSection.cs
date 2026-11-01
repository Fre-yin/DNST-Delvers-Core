using DungeonSettlersDelvers.Core;
using static TestKit;

// The summons' owners (DelversSummons, SummonOwnerRules): one way from a summon to its owner and back, for the players and the
// bandits alike; and the side a Core owner's summon takes (SummonSideRules).
internal static class SummonsSection
{
    internal static void Run()
    {
        Owners();
        Side();
        Limit();
        SeedUpgrade();
        Follow();
        Tint();
    }

    // DelversSummons.SummonsOf's order and owner match (SummonOwnerRules), the one way for players and bandits.
    static void Owners()
    {
        var order = new SummonOwnerRules();
        order.Note("b");
        order.Note("a");
        var sorted = order.Sort(new[] { "c", "a", "b", "a", "" });
        Check(sorted.Count == 3 && sorted[0] == "b" && sorted[1] == "a" && sorted[2] == "c",
            "summon owners: the summons stand in the order they appeared, a new one last, each once");
        Check(order.Note("a") == order.Note("a") && order.Note("d") > order.Note("c"), "summon owners: a summon keeps its running number");
        Check(SummonOwnerRules.IsOwnedBy("3F2504E0-4F89-11D3-9A0C-0305E82C3301", "3f2504e0-4f89-11d3-9a0c-0305e82c3301")
                && !SummonOwnerRules.IsOwnedBy("", "") && !SummonOwnerRules.IsOwnedBy("x", "") && !SummonOwnerRules.IsOwnedBy("x", "y"),
            "summon owners: an owner matches by GUID; no summoner owns nothing");
        for (var i = 0; i < SummonOwnerRules.Cap + 10; i++) order.Note("n" + i);
        order.Keep(new HashSet<string> { "a", "n3" });
        Check(order.Count == 2 && order.Sort(new[] { "n3", "a" })[0] == "a", "summon owners: past the cap only the summons still seen stay, in their order");
        order.Keep(null);
        order.Clear();
        Check(order.Count == 0 && order.Sort(new[] { "z", "y" })[0] == "z", "summon owners: the order starts afresh with the campaign");
    }

    // A Core owner's summon takes its side (SummonSideRules), and the probe's entry for the toolkit block.
    static void Side()
    {
        Check(SummonSideRules.Applies(true, "UNIT_Bubbleling") && !SummonSideRules.Applies(false, "UNIT_Bubbleling")
                && !SummonSideRules.Applies(true, "BUILDING_StrangeSeed") && !SummonSideRules.Applies(true, null),
            "summon side: only a unit summoned by a Core owner takes its side; player summons and plants stay as they are");
        Check(SummonSideRules.Line("sub", "s", "UNIT_Bubbleling", "o", 3, 2, true) == "CORE_SUMMON_SIDE stage=sub summon=s key=UNIT_Bubbleling owner=o faction=3 sub=2 changed=1"
                && SummonSideRules.DeathLine("o", 2) == "CORE_SUMMON_SIDE stage=death owner=o summons=2"
                && SummonSideRules.SaveLine(1) == "CORE_SUMMON_SIDE stage=save dead-left-out=1"
                && SummonSideRules.EffectLine("s", "core", "t", "party", "damage", 12.34f) == "CORE_SUMMON_EFFECT summon=s owner-kind=core target=t side=party kind=damage amount=12.3"
                && SummonSideRules.Entry("s", "UNIT_Bubbleling", 3, 2, false, 1, "Hostile", true, "Ally", 4, 0, SummonSideRules.Pair(3, 2))
                    == "s:UNIT_Bubbleling:3:2:0:1:Hostile:1:Ally:4:0:3,2"
                && SummonSideRules.Entry("s", "k", 2, 1, true, -1, null, false, null, 0, 0, null) == "s:k:2:1:1:-1:-:0:-:0:0:-",
            "summon side: the log line and the probe's entry");
    }

    // The seed upgrades keep to their side (SeedUpgradeRules): no party member grows a bandit's seed, no bandit a party seed.
    static void SeedUpgrade()
    {
        Check(SeedUpgradeRules.Allows(true, true) && SeedUpgradeRules.Allows(false, false)
                && !SeedUpgradeRules.Allows(false, true) && !SeedUpgradeRules.Allows(true, false),
            "seed upgrade: a seed grows only for its own side, the game's units among themselves as before");
        Check(SeedUpgradeRules.RefusedLine("c", "s", "BUILDING_StrangeSeed", false, true)
                == "CORE_SEED_UPGRADE_SIDE caster=c seed=s key=BUILDING_StrangeSeed caster-side=party seed-side=core result=refused"
                && SeedUpgradeRules.RefusedLine(null, null, null, true, false)
                == "CORE_SEED_UPGRADE_SIDE caster=- seed=- key=- caster-side=core seed-side=party result=refused",
            "seed upgrade: the log line of a refusal");
    }

    // The map's summon cap per side (SummonLimit): a bandit's bubbleling and the party's do not count together.
    static void Limit()
    {
        Check(SummonSideRules.CountsTogether(true, true) && SummonSideRules.CountsTogether(false, false)
                && !SummonSideRules.CountsTogether(true, false) && !SummonSideRules.CountsTogether(false, true),
            "summon limit: the map's cap counts a summon's own side only, the party's alone as in the game");
        Check(SummonSideRules.LimitLine("s", "UNIT_Bubbleling", "o", true) == "CORE_SUMMON_LIMIT summon=s key=UNIT_Bubbleling left-out=o side=core"
                && SummonSideRules.LimitDeathLine("UNIT_Bubbleling", "d", false, true, true)
                    == "CORE_SUMMON_LIMIT death key=UNIT_Bubbleling dies=d dies-side=core summon-side=party per-side=1",
            "summon limit: the log line");
    }

    // A water mage's bubblelings attack the mage's target (SummonFollowRules): a bandit's always, a party mage's with the option.
    static void Follow()
    {
        Check(SummonFollowRules.Follows(true, false, false, true) && SummonFollowRules.Follows(false, true, true, true)
                && !SummonFollowRules.Follows(false, true, false, true) && !SummonFollowRules.Follows(true, false, true, false)
                && !SummonFollowRules.Follows(false, false, true, true),
            "summon follow: a bandit's bubbleling always, a party mage's with the option, never without a usable target");
        Check(SummonFollowRules.Line("s", "o", "t", true) == "CORE_SUMMON_FOLLOW summon=s owner=o target=t owner-kind=core",
            "summon follow: the log line");
        Check(SummonFollowRules.Forced(null, null, "t") == SummonFollowRules.ForcedStep.Set
                && SummonFollowRules.Forced("t", "t", "t") == SummonFollowRules.ForcedStep.None
                && SummonFollowRules.Forced("t", "t", "u") == SummonFollowRules.ForcedStep.Set
                && SummonFollowRules.Forced("t", "t", null) == SummonFollowRules.ForcedStep.Clear
                && SummonFollowRules.Forced(null, null, null) == SummonFollowRules.ForcedStep.None,
            "summon follow: Core's forced target follows the owner's and goes when there is none");
        Check(SummonFollowRules.Forced("p", null, "t") == SummonFollowRules.ForcedStep.Order
                && SummonFollowRules.Forced("p", "t", null) == SummonFollowRules.ForcedStep.Order,
            "summon follow: a forced target of the player's is never overwritten nor cleared");
    }

    // A bandit's bubbleling in the bandits' orange (BanditTintRules.SummonPixel): its blue turns, grey, white, green and clear stay.
    static void Tint()
    {
        var hue = BanditTintRules.DefaultHue;
        var blue = BanditTintRules.SummonPixel(40, 120, 230, 255, hue);
        BanditTintRules.ToHsv(blue.R / 255.0, blue.G / 255.0, blue.B / 255.0, out var h, out _, out _);
        Check(Math.Abs(h - hue) < 1.0 && blue.A == 255, "summon tint: a blue pixel takes the bandits' hue, alpha kept");
        Check(BanditTintRules.SummonPixel(250, 250, 250, 255, hue) == (250, 250, 250, 255)
                && BanditTintRules.SummonPixel(40, 200, 60, 255, hue) == (40, 200, 60, 255)
                && BanditTintRules.SummonPixel(40, 120, 230, 0, hue) == (40, 120, 230, 0),
            "summon tint: white, green and a clear pixel stay as they are");
        var green = BanditTintRules.SkillPixel(40, 200, 60, 200, hue);
        var water = BanditTintRules.SkillPixel(40, 120, 230, 255, hue);
        BanditTintRules.ToHsv(green.R / 255.0, green.G / 255.0, green.B / 255.0, out var hg, out _, out _);
        BanditTintRules.ToHsv(water.R / 255.0, water.G / 255.0, water.B / 255.0, out var hw, out _, out _);
        Check(Math.Abs(hg - hue) < 1.0 && Math.Abs(hw - hue) < 1.0 && green.A == 200,
            "skill tint: a vine's green and a bubble's blue both take the bandits' hue, alpha kept");
        Check(BanditTintRules.SkillPixel(250, 250, 250, 255, hue) == (250, 250, 250, 255)
                && BanditTintRules.SkillPixel(40, 200, 60, 0, hue) == (40, 200, 60, 0),
            "skill tint: white and a clear pixel stay as they are");
        Check(BanditTintRules.SkillLine(131, "f", "c", "UNIT_Bubbleling") == "CORE_SEED_TINT_SKILL id=131 fx=f caster=c key=UNIT_Bubbleling",
            "skill tint: the log line");
    }
}
