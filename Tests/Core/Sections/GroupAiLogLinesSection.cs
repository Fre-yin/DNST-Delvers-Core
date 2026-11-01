using System.Globalization;
using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: the CORE_AI_ lines that GroupAiRules builds, each held as a literal. The Toolkit's parsers read these texts.
internal static class GroupAiLogLinesSection
{
    internal static void Run()
    {
        BanditRule();
        PartyRule();
        TargetAndSeedLines();
        AnnounceAndRole();
        BanditKitAndBandageLines();
        UnderGermanCulture();
    }

    static void BanditRule()
    {
        // unit= kit= rule= skill= target= in this order (the Toolkit reads it); a skipped friendly-fire skill sits before lowestUnit=.
        var line = GroupAiRules.BanditRuleLine("b1", "Healer", "heal-ally", "SKILL_Mend", "t1", 0.5f, 0.25f, 3, 1, "f1", true, "InCombat",
            "Q:Ready+W:Cooldown", null, null, null, "l1", 42);
        Check(line == "CORE_AI_BANDIT_RULE unit=b1 kit=Healer rule=heal-ally skill=Mend target=t1 hp=0.50 lowest=0.25 hostiles=3 downed=1 focus=f1 gcd=1 state=InCombat slots=Q:Ready+W:Cooldown lowestUnit=l1 tick=42",
            "bandit rule line: unit kit rule skill target in this order, hp with a point and two decimals");
        Check(GroupAiRules.BanditRuleLine("b1", "Blade", "bandit-basic", "", "-", 1f, 1f, 0, 0, "-", false, "InCombat", "", "SKILL_ArrowRain", "a1", null, "-", 7)
                == "CORE_AI_BANDIT_RULE unit=b1 kit=Blade rule=bandit-basic skill=basic target=- hp=1.00 lowest=1.00 hostiles=0 downed=0 focus=- gcd=0 state=InCombat slots= skipped=friendly-fire ffSkill=ArrowRain ffAlly=a1 ffFlag=- lowestUnit=- tick=7",
            "bandit rule line: the basic attack as skill=basic, a skipped friendly-fire skill before lowestUnit");
        var kit = line.IndexOf(" kit=", StringComparison.Ordinal);
        var rule = line.IndexOf(" rule=", StringComparison.Ordinal);
        Check(line.IndexOf(" unit=", StringComparison.Ordinal) < kit && kit < rule && rule < line.IndexOf(" skill=", StringComparison.Ordinal)
                && line.IndexOf(" skill=", StringComparison.Ordinal) < line.IndexOf(" target=", StringComparison.Ordinal),
            "bandit rule line: the field order unit kit rule skill target");
    }

    static void PartyRule()
    {
        // The party's CORE_AI_RULE has two formats; both begin with unit= role= rule= skill= target=.
        var line = GroupAiRules.PartyRuleLine("p1", GroupAiRole.Heal, "heal-ally", "SKILL_Mend", "t2", 0.5f, 0.3f, 12.4f, 30f, false, "InCombat",
            "Q:Ready", "l2", null, null, null);
        Check(line == "CORE_AI_RULE unit=p1 role=Heal rule=heal-ally skill=Mend target=t2 hp=0.50 lowest=0.30 lowestUnit=l2 energy=12/30 gcd=0 state=InCombat slots=Q:Ready",
            "party rule line: unit role rule skill target, hp with a point and energy as whole numbers");
        Check(GroupAiRules.PartyRuleLine("p1", GroupAiRole.Melee, "melee-basic", "", "-", 1f, 1f, 0f, 0f, true, "InCombat", "", "-", "SKILL_ArrowRain", "a1", "ffFlag=x")
                == "CORE_AI_RULE unit=p1 role=Melee rule=melee-basic skill=basic target=- hp=1.00 lowest=1.00 lowestUnit=- energy=0/0 gcd=1 state=InCombat slots= skipped=friendly-fire ffSkill=ArrowRain ffAlly=a1 ffFlag=x",
            "party rule line: the basic attack as skill=basic, a skipped friendly-fire skill at the end");
        Check(GroupAiRules.OffGcdRuleLine("p1", "heal-lifebloom", "SKILL_DelversLifebloom", "t2", "l2")
                == "CORE_AI_RULE unit=p1 role=Heal rule=heal-lifebloom skill=DelversLifebloom target=t2 lowestUnit=l2 gcd=1 offgcd=1",
            "party rule line outside the global cooldown: the second format of CORE_AI_RULE");
    }

    static void TargetAndSeedLines()
    {
        Check(GroupAiRules.AddsFirstLine("p1", "add1", "boss1") == "CORE_AI_TARGET unit=p1 rule=adds-first target=add1 boss=boss1",
            "adds-first line: unit, the add and the boss");
        Check(GroupAiRules.SeedBurstLine("p1", "seed1", "SKILL_LifeBurst", 0.5f, 77) == "CORE_AI_SEED_BURST unit=p1 seed=seed1 skill=LifeBurst lowest=0.50 tick=77"
                && GroupAiRules.SeedBurstLine("p1", "seed1", "SKILL_LifeBurst", null, 78) == "CORE_AI_SEED_BURST unit=p1 seed=seed1 skill=LifeBurst lowest=1.00 tick=78",
            "seed burst line: the lowest share with two decimals, 1.00 when none");
        Check(GroupAiRules.LifebloomLine("p1", "t2", 3, 4.5, 2, "heal-lifebloom") == "CORE_AI_LIFEBLOOM unit=p1 target=t2 stacks=3 left=4.5 charges=2 rule=heal-lifebloom",
            "Lifebloom line: stacks, seconds left with one decimal, charges and rule");
    }

    static void AnnounceAndRole()
    {
        var allies = new[] { "a1", "a2" };
        Check(GroupAiRules.AnnounceLine(false, "p1", "SKILL_ArrowRain", allies, "area=1") == "CORE_AI_ANSAGE unit=p1 skill=ArrowRain allies=a1+a2 area=1 hold=2s"
                && GroupAiRules.AnnounceLine(true, "b1", "SKILL_ArrowRain", allies, "area=1") == "CORE_AI_BANDIT_ANSAGE unit=b1 skill=ArrowRain allies=a1+a2 area=1 hold=2s",
            "announcement lines: the party's and the bandits' mark, allies joined by plus, the hold in whole seconds");
        Check(GroupAiRules.RoleLine("p1", GroupAiRole.Shield, "SKILL_BasicAttack", new[] { "SKILL_Wall", "SKILL_Guard" })
                == "CORE_AI_ROLE unit=p1 role=Shield basic=SKILL_BasicAttack slots=Wall+Guard",
            "role line: the role, the basic attack and the short slot keys");
    }

    static void BanditKitAndBandageLines()
    {
        Check(GroupAiRules.BanditKitLine("g1", "CoreBanditGuard", "Guard") == "CORE_AI_BANDIT_KIT unit=g1 key=CoreBanditGuard kit=Guard",
            "bandit kit line: unit, unit key and kit");
        Check(GroupAiRules.BandageGateLine("g1", "gcd", WoundRules.Stage.Fatal) == "CORE_AI_BANDAGE_GATE unit=g1 reason=gcd stage=lethal"
                && GroupAiRules.BandageGateLine("g1", "held", WoundRules.Stage.Heavy) == "CORE_AI_BANDAGE_GATE unit=g1 reason=held stage=heavy",
            "bandage gate line: reason and wound stage");
        Check(GroupAiRules.BandageSlotsLine("none") == "CORE_AI_BANDAGE_SLOTS extra=none"
                && GroupAiRules.BandageSlotsLine("Some.Reader.Read") == "CORE_AI_BANDAGE_SLOTS extra=Some.Reader.Read",
            "bandage slots line: the reader or none");
        Check(GroupAiRules.VinesProbeLine(3, -4) == "CORE_AI_VINES_PROBE cell=3,-4 blocked=1 occupied=1",
            "vines probe line: the cell, blocked and occupied");
    }

    static void UnderGermanCulture()
    {
        // The Toolkit parses decimals with a point; a German thread culture must not turn hp=0.50 into hp=0,50.
        var saved = CultureInfo.CurrentCulture;
        try
        {
            var german = new CultureInfo("de-DE");
            Check(german.NumberFormat.NumberDecimalSeparator == ",", "de-DE culture is available and writes a comma");
            CultureInfo.CurrentCulture = german;
            var bandit = GroupAiRules.BanditRuleLine("b1", "Healer", "heal-ally", "SKILL_Mend", "t1", 0.5f, 0.25f, 3, 1, "f1", true, "InCombat", "Q", null, null, null, "l1", 42);
            var party = GroupAiRules.PartyRuleLine("p1", GroupAiRole.Heal, "heal-ally", "SKILL_Mend", "t2", 0.5f, 0.3f, 12.4f, 30f, false, "InCombat", "Q", "l2", null, null, null);
            Check(bandit.Contains(" hp=0.50 lowest=0.25 ", StringComparison.Ordinal) && party.Contains(" hp=0.50 lowest=0.30 ", StringComparison.Ordinal)
                    && GroupAiRules.SeedBurstLine("p1", "seed1", "SKILL_LifeBurst", 0.5f, 77).Contains("lowest=0.50 ", StringComparison.Ordinal)
                    && GroupAiRules.LifebloomLine("p1", "t2", 3, 4.5, 2, "heal-lifebloom").Contains("left=4.5 ", StringComparison.Ordinal)
                    && GroupAiRules.AnnounceLine(true, "b1", "SKILL_ArrowRain", new[] { "a1" }, "area=1").EndsWith("hold=2s", StringComparison.Ordinal),
                "rule lines under the German thread culture: decimals keep their point (hp=0.50)");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}
