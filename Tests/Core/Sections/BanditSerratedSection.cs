using DungeonSettlersDelvers.Core;
using static TestKit;

// The Serrated Blade of the bandits.
internal static class BanditSerratedSection
{
    internal static void Run()
    {
        SerratedBlade();
        BladeGrindingBuff();
    }

    static void SerratedBlade()
    {
        // (18./): Serrated Blade, Blade Grinding's effect plus the blade's bleeding, in Blade Grinding's place.
        Check(BanditSerratedRules.SkillKey == "SKILL_DelversCoreSerratedBlade" && BanditSerratedRules.IconFile == "SerratedBlade_32x32.png"
                && BanditSerratedRules.BladeUnitKey == "UNIT_DelversCoreForestBanditBlade"
                && BanditSerratedRules.Of(null, null) == (0.75, 3) && BanditSerratedRules.Of(null, 2) == (0.75, 2) && BanditSerratedRules.Of(30, null) == (0.3, 3)
                && BanditSerratedRules.Of(30, 2) == (0.3, 2) && BanditSerratedRules.Of(150, 99) == (1.0, 10) && BanditSerratedRules.Of(-5, 0) == (0.0, 1)
                && BanditSerratedRules.TooltipValues(50, 30, null).SequenceEqual(new[] { 50f, 30f, 0f, 0f })
                && BanditSerratedRules.TooltipValues(50, 30, (0.3, 2)).SequenceEqual(new[] { 50f, 30f, 2f, 30f })
                && Array.IndexOf(CoreSaveSkills.Keys, BanditSerratedRules.SkillKey) >= 0
                && CoreUnitRules.Units.First(u => u.Role == "blade").Skills.SequenceEqual(new[] { "SKILL_BasicGreatswordAttack", "SKILL_Slay", BanditSerratedRules.SkillKey, "SKILL_DeadlyFear", "SKILL_DestructiveSplit" })
                && BanditSerratedRules.Migration.SequenceEqual(new[] { ("SKILL_BladeGrinding", BanditSerratedRules.SkillKey), ("SKILL_SweepingCleave", "SKILL_DeadlyFear") })
                && BanditSerratedRules.MigrateLine("u", "a>b") == "CORE_BANDIT_BLADE_KIT_MIGRATED unit=u changes=a>b"
                && BanditSerratedRules.Description.German == "Der nächste Nahkampfskill verursacht {0}% mehr Schaden und hat {1}% mehr Treffergenauigkeit. Jeder Treffer hinterlässt mit {3}% Chance {2} [AFFECTER_Bleeding]."
                && BanditSerratedRules.Description.AllLanguages.All(text => text.Contains("{0}") && text.Contains("{1}") && text.Contains("{2}") && text.Contains("{3}"))
                && BanditSerratedRules.Name.AllLanguages.All(text => text == "Serrated Blade")
                && BanditSerratedRules.All.All(entry => entry.Text.AllLanguages.Count == 10
                    && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('-') < 0 && text.IndexOf('–') < 0 && text.IndexOf('—') < 0))
                && BanditSerratedRules.Description.English.Contains("{0}") && BanditSerratedRules.Description.English.Contains("[AFFECTER_Bleeding]")
                && BanditSerratedRules.TooltipValues(50, 30, BanditSerratedRules.Of(null, null)).SequenceEqual(new[] { 50f, 30f, 3f, 75f }),
            "bandit blade: Serrated Blade in Blade Grinding's place, Deadly Fear in Sweeping Cleave's, old kits migrate, 75 % for 3 stacks by default");
    }

    static void BladeGrindingBuff()
    {
        // The game's Blade Grinding buff skipped only its own key; the copy is skipped too, old penalties undone.
        Check(BanditSerratedRules.BuffSkips("SKILL_DelversCoreSerratedBlade") && !BanditSerratedRules.BuffSkips("SKILL_Slay") && !BanditSerratedRules.BuffSkips(null)
                && BanditSerratedRules.BuffLine("end", "u", "SKILL_DelversCoreSerratedBlade", true) == "CORE_BANDIT_SERRATED_BUFF at=end owner=u skill=SKILL_DelversCoreSerratedBlade skipped=1"
                && BanditSerratedRules.OldPenalty(40, 100, -100, 0, 30, 50) == 2 && BanditSerratedRules.OldPenalty(70, 100, -50, 0, 30, 50) == 1
                && BanditSerratedRules.OldPenalty(100, 100, 0, 0, 30, 50) == 0 && BanditSerratedRules.OldPenalty(70, 100, 0, 0, 30, 50) == 0
                && BanditSerratedRules.OldPenalty(70, 100, -100, 0, 30, 50) == 0 && BanditSerratedRules.OldPenalty(85, 100, -25, 0, 30, 50) == 0
                && BanditSerratedRules.OldPenalty(130, 100, 50, 0, 30, 50) == 0 && BanditSerratedRules.OldPenalty(40, 100, -100, 0, 0, 50) == 0
                && BanditSerratedRules.PenaltyLine("u", 2, 40, -100) == "CORE_BANDIT_SERRATED_PENALTY_RESET unit=u steps=2 hit=40 dealt=-100",
            "bandit blade: Serrated Blade is skipped by the game's Blade Grinding buff like the original; only the bug's exact penalty is given back");
    }
}
