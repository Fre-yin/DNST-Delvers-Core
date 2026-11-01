using DungeonSettlersDelvers.Core;
using static TestKit;

// Area skills weighed against the best other choice.
internal static class BanditAreaSection
{
    internal static void Run()
    {
        // An area skill with a fallen unit in it is weighed against the
        // best other choice; fallen units count neither way, a tie goes to the area; factors provisional 1.0 until measured.
        Check(BanditAreaRules.IgnoresDownedKey == "bandits.areaignoresdowned" && BanditAreaRules.Factor("SKILL_Anything") == BanditAreaRules.ProvisionalFactor
                && BanditAreaRules.ProvisionalFactor == 1.0
                && BanditAreaRules.AreaValue("SKILL_StunCharge", 2) == 4 && BanditAreaRules.AreaValue("SKILL_StunCharge", -1) == 0
                && BanditAreaRules.Factor("SKILL_SweepingCleave") == BanditAreaRules.ProvisionalFactor
                && BanditAreaRules.Factor("SKILL_Slay") == 4.5 && BanditAreaRules.Factor("SKILL_ArrowRain") == 2.4 && BanditAreaRules.Factor("SKILL_DestructiveSplit") == 3.2
                && BanditAreaRules.Pick(BanditAreaRules.AreaValue("SKILL_DestructiveSplit", 1), BanditAreaRules.SingleValue("SKILL_Slay")) == "alt"
                && BanditAreaRules.Pick(BanditAreaRules.AreaValue("SKILL_DestructiveSplit", 2), BanditAreaRules.SingleValue("SKILL_Slay")) == "area"
                && BanditAreaRules.Pick(BanditAreaRules.AreaValue("SKILL_StunCharge", 2), BanditAreaRules.SingleValue("SKILL_Slay")) == "alt"
                && BanditAreaRules.Pick(BanditAreaRules.AreaValue("SKILL_StunCharge", 3), BanditAreaRules.SingleValue("SKILL_Slay")) == "area"
                && BanditAreaRules.Pick(2, 1) == "area" && BanditAreaRules.Pick(1, 1) == "area" && BanditAreaRules.Pick(0, 1) == "alt"
                && BanditAreaRules.ValueLine("u", "SweepingCleave", 0, 2, 0, "Slay", 1, "alt", 9)
                    == "CORE_AI_BANDIT_AREA_VALUE unit=u skill=SweepingCleave standing=0 downed=2 value=0 alt=Slay altValue=1 pick=alt tick=9"
                && BanditAreaRules.SkillValuesLine("SKILL_X", "1/2", "a \"b\"", "Circle", 1, 3)
                    == "CORE_AI_BANDIT_SKILL_VALUES skill=SKILL_X values=1/2 comment=\"a 'b'\" area=Circle radius=1 range=3",
            "bandit areas: weighed against the best other choice, fallen units count neither way, tie to the area, provisional 1.0");
    }
}
