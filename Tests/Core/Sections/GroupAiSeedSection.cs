using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: the herbalist's seed.
internal static class GroupAiSeedSection
{
    internal static void Run()
    {
        // The herbalist's seed next to comrades below 0.9, the upgrade at 5 s left, the bandits'
        // Life Burst 12 % of the target's max health; her kit carries the three skills.
        var herbalist = CoreUnitRules.Units.First(unit => unit.Role == "healer");
        Check(herbalist.Skills.Contains(GroupAiHealerSeedRules.SeedSkill) && herbalist.Skills.Contains(GroupAiHealerSeedRules.SprayerSkill)
                && herbalist.Skills.Contains(GroupAiHealerSeedRules.LifeBurstSkill) && herbalist.Skills[0] == "SKILL_BasicMagicMissile"
                && !CoreUnitRules.Units.Where(unit => unit.Role != "healer").Any(unit => unit.Skills.Contains(GroupAiHealerSeedRules.SeedSkill))
                && GroupAiHealerSeedRules.LifeBurstShare(null) == 0.12 && GroupAiHealerSeedRules.LifeBurstShare(0.2) == 0.2
                && GroupAiHealerSeedRules.LifeBurstShare(3) == 1.0 && GroupAiHealerSeedRules.LifeBurstShare(-1) == 0.0
                && GroupAiHealerSeedRules.LifeBurstShare(double.NaN) == 0.12
                && Math.Abs(GroupAiHealerSeedRules.LifeBurstHeal(500f, 0.12) - 60f) < 1e-3f && GroupAiHealerSeedRules.LifeBurstHeal(0f, 0.12) == 0f
                && GroupAiHealerSeedRules.PickAnchor(new[] { (0, 4, 1), (1, 3, 2), (2, 2, 2) }) == 2
                && GroupAiHealerSeedRules.PickAnchor(new[] { (0, 4, 0), (1, 3, 0) }) == -1 && GroupAiHealerSeedRules.PickAnchor(null) == -1
                && GroupAiHealerSeedRules.PickAnchor(new[] { (0, 2, 1), (1, 1, 1) }) == 1
                && GroupAiHealerSeedRules.Below(0.89f) && !GroupAiHealerSeedRules.Below(0.9f)
                && GroupAiHealerSeedRules.UpgradeDue(5f) && GroupAiHealerSeedRules.UpgradeDue(0.5f) && !GroupAiHealerSeedRules.UpgradeDue(5.1f)
                && !GroupAiHealerSeedRules.UpgradeDue(0f),
            "C15a: the herbalist's seed, its upgrade and the bandits' Life Burst");
        Check(GroupAiHealerSeedRules.SeedLine("h", "p", 2, "healer-seed", 5) == "CORE_AI_BANDIT_SEED unit=h target=p below=2 rule=healer-seed tick=5"
                && GroupAiHealerSeedRules.UpgradeLine("h", "s", 4.2f, 15f, 10.8f, "upgrade", 6) == "CORE_AI_BANDIT_SEED_UPGRADE unit=h seed=s left=4.2 life=15 counter=10.8 result=upgrade tick=6"
                && GroupAiHealerSeedRules.Left(15f, 11f) == 4f && GroupAiHealerSeedRules.Left(15f, 20f) == 0f && GroupAiHealerSeedRules.Left(-1f, -1f) == 0f
                && GroupAiHealerSeedRules.LifeValueIndex == 2
                && GroupAiHealerSeedRules.AroundCells(5, 7).Count() == 9 && GroupAiHealerSeedRules.AroundCells(5, 7).Contains((4, 8)) && GroupAiHealerSeedRules.AroundCells(5, 7).Contains((5, 7))
                && GroupAiHealerSeedRules.AmountSkip(16f, 0f) == "amount:matk=16,value=0" && GroupAiHealerSeedRules.TargetSkip(9, 3, true) == "no-targets:area=9,core=3,fallback=1"
                && GroupAiHealerSeedRules.HealSkipLine("s", "no-map", 4) == "CORE_SEED_HEAL_SKIP seed=s reason=no-map tick=4"
                && GroupAiHealerSeedRules.LifeBurstNoneLine("h", "s", 5) == "CORE_LIFEBURST_HEAL caster=h seed=s result=none tick=5"
                && GroupAiHealerSeedRules.MonsterDifficultyKey(1) == "AFFECTER_MonsterDifficultyNormal" && GroupAiHealerSeedRules.MonsterDifficultyKey(2) == null
                && GroupAiHealerSeedRules.MonsterDifficultyKey(5) == "AFFECTER_MonsterDifficultyDownfall"
                && GroupAiHealerSeedRules.MagicProbeLine("h", 5, "A", true, -60f, null, 40f, 16f, -60f) == "CORE_UNIT_MAGIC_PROBE unit=h level=5 affecter=A carries=1 magicAdjust=-60 physicalAdjust=- row=40 actual=16 ownPerModifier=-60"
                && GroupAiHealerSeedRules.MagicSetLine(40f, -60f, 100f) == "CORE_UNIT_MAGIC set=40 normalPerModifier=-60 row=100"
                && GroupAiHealerSeedRules.MagicSetLine(40f, null, 40f) == "CORE_UNIT_MAGIC set=40 normalPerModifier=unknown row=40"
                && GroupAiHealerSeedRules.AreaLine("s", true, new[] { (1, 2), (3, 4) }, new[] { "5,6", "7,8:foe" }) == "CORE_SEED_AREA seed=s core=1 cells=1,2|3,4 units=5,6|7,8:foe"
                && WoundRules.FxSkipLine("u", "excluded") == "CORE_WOUND_FX_SKIP unit=u reason=excluded"
                && GroupAiHealerSeedRules.MonsterDifficultyKeys.Length == 4 && GroupAiHealerSeedRules.MonsterDifficultyKeys.Contains(GroupAiHealerSeedRules.MonsterDifficultyKey(1))
                && GroupAiHealerSeedRules.MagicAffecterLine("A", true, -0.6f, null) == "CORE_UNIT_MAGIC_PROBE affecter=A found=1 magicAdjust=-0.6 physicalAdjust=-"
                && Math.Abs(GroupAiHealerSeedRules.SeedHeal(40f, 50f) - 20f) < 1e-4f && GroupAiHealerSeedRules.SeedHeal(123f, 15f) == 18f
        && GroupAiHealerSeedRules.SeedHeal(6f, 15f) == 0f && GroupAiHealerSeedRules.SeedHeal(0f, 50f) == 0f && GroupAiHealerSeedRules.SeedHeal(40f, 0f) == 0f
                && GroupAiHealerSeedRules.HealValueIndex == 1 && GroupAiHealerSeedRules.HealScale == 0.01f
                && GroupAiHealerSeedRules.SeedHealLine("s", "h", "b", 20f, true, 5) == "CORE_SEED_HEAL seed=s summoner=h target=b amount=20.0 result=published tick=5"
                && GroupAiHealerSeedRules.LifeBurstKeeps(false, false) && GroupAiHealerSeedRules.LifeBurstKeeps(true, true) && !GroupAiHealerSeedRules.LifeBurstKeeps(true, false)
                && GroupAiHealerSeedRules.LifeBurstDropLine("h", "p", 6) == "CORE_LIFEBURST_HEAL caster=h target=p dropped=player tick=6"
                && GroupAiHealerSeedRules.UpgradeResult(false, true, true) == "range" && GroupAiHealerSeedRules.UpgradeResult(true, false, false) == "wait:unknown"
                && GroupAiHealerSeedRules.UpgradeResult(true, true, true) == "upgrade" && GroupAiHealerSeedRules.UpgradeResult(true, true, false) == "wait"
                && CoreUnitRules.MagicAttackOf(herbalist, 6.4f, null) == 100f && CoreUnitRules.MagicAttackOf(herbalist, 6.4f, 55) == 55f
                && CoreUnitRules.MagicAttackOf(herbalist, 6.4f, -1) == 100f && Math.Abs(CoreUnitRules.EffectiveOf(100f, -60f) - 40f) < 1e-3f && Math.Abs(CoreUnitRules.EffectiveOf(100f, 50f) - 150f) < 1e-3f && Math.Abs(CoreUnitRules.EffectiveOf(100f, -85f) - 15f) < 1e-3f && CoreUnitRules.HealerMagicKey == "bandits.healer.matk"
                && CoreUnitRules.MagicAttackOf(CoreUnitRules.Units.First(unit => unit.Role == "guard"), 6.4f, 55) == 6.4f
                && GroupAiHealerSeedRules.HealProbeLine("s", "h", true, 3, 2, 0, 0f, 7) == "CORE_SEED_HEAL_PROBE seed=s summoner=h core=1 allow=3 yield=2 applied=0 amount=0.0 tick=7"
                && GroupAiHealerSeedRules.HealProbeLine("s", null, false, 1, 1, 1, 12.5f, 8).Contains(" summoner=- core=0 ")
                && GroupAiHealerSeedRules.PushLine("s", "b", "bandit", 9) == "CORE_SEED_PUSH seed=s unit=b side=bandit tick=9"
                && GroupAiHealerSeedRules.LifeBurstProbeLine("h", true, "1", "0", "register") == "CORE_LIFEBURST_PROBE unit=h listed=1 registered=1 passive=0 action=register"
                && GroupAiHealerSeedRules.RegisterDecision(true, false) == "register" && GroupAiHealerSeedRules.RegisterDecision(true, true) == "skip:present"
                && GroupAiHealerSeedRules.RegisterDecision(false, false) == "skip:not-registered" && GroupAiHealerSeedRules.RegisterDecision(false, true) == "skip:present"
                && GroupAiHealerSeedRules.RegisterLine("h", false, true, "done") == "CORE_LIFEBURST_REGISTER unit=h before=0 after=1 result=done"
                && GroupAiHealerSeedRules.LifeBurstTryLine("h", "s", "BUILDING_StrangeSeed", 9) == "CORE_LIFEBURST_PROBE kind=try owner=h target=s key=BUILDING_StrangeSeed tick=9"
                && GroupAiHealerSeedRules.AuraFilterLine("h", true, "p", true, false) == "CORE_SEED_AURA_FILTER summoner=h changed=1 unit=p native=1 filtered=0"
                && GroupAiHealerSeedRules.LifeBurstLine("h", "b", 500f, 0.12, 60f, 7) == "CORE_LIFEBURST_HEAL caster=h target=b max=500 share=0.12 heal=60.0 tick=7"
                && GroupAiHealerSeedRules.SprayerHitLine("h", "p", "player", 8) == "CORE_MUCUS_SPRAYER_HIT summoner=h target=p side=player tick=8"
                && GroupAiHealerSeedRules.SideLine("s", null, false, "1", null) == "CORE_SEED_SIDE_PROBE seed=s summoner=- core=0 hostileToPlayer=1 hostileToBandit=-"
                && GroupAiHealerSeedRules.FxLine("s", "building/Seed", "Transform+SpriteRenderer", "SpriteRenderer", "Sprites-Default", "1.00,1.00,1.00,1.00", 0)
                    == "CORE_SEED_FX_PROBE seed=s path=building/Seed components=Transform+SpriteRenderer renderer=SpriteRenderer material=Sprites-Default color=1.00,1.00,1.00,1.00 pooled=0",
            "C15a/C22a: the seed's log lines");
        SeedStay();
    }

    // "Im Samen bleiben": in at 0.30, out at 0.50, one order, attack in reach or hold; the bandits'
    // Life Burst at the same 0.30 with the sprayer.
    private static void SeedStay()
    {
        var area = new List<(int X, int Y)>(GroupAiHealerSeedRules.AroundCells(5, 5));
        Check(SeedStayRules.Below(null) == 0.30 && SeedStayRules.Below(0.2) == 0.2 && SeedStayRules.Below(2) == 1.0 && SeedStayRules.Below(double.NaN) == 0.30
                && SeedStayRules.Until(null, 0.30) == 0.50 && SeedStayRules.Until(0.2, 0.30) == 0.30 && SeedStayRules.Until(0.6, 0.30) == 0.6
                && SeedStayRules.Stays(false, 0.30f, 0.30, 0.50) && !SeedStayRules.Stays(false, 0.31f, 0.30, 0.50)
                && SeedStayRules.Stays(true, 0.49f, 0.30, 0.50) && !SeedStayRules.Stays(true, 0.50f, 0.30, 0.50)
                && GroupAiRules.EmergencyShare == 0.35f
                && SeedStayRules.SeedUsable(0f, -1f) && SeedStayRules.SeedUsable(2f, 15f) && !SeedStayRules.SeedUsable(1.9f, 15f),
            "Seed stay: entry at 0.30, exit at 0.50, the party's emergency share stays 0.35");
        Check(SeedStayRules.PickCell(area, null, 9, 5) == (6, 4)
                && SeedStayRules.PickCell(area, new HashSet<(int X, int Y)> { (6, 4), (6, 5), (6, 6) }, 9, 5) == (5, 4)
                && SeedStayRules.PickCell(area, new HashSet<(int X, int Y)>(area), 9, 5) == null && SeedStayRules.PickCell(null, null, 0, 0) == null
                && SeedStayRules.InArea(area, 4, 6) && !SeedStayRules.InArea(area, 7, 5) && !SeedStayRules.InArea(null, 0, 0)
                && SeedStayRules.Step(false, false, true, false) == "walk" && SeedStayRules.Step(false, true, true, false) == "end"
                && SeedStayRules.Step(false, false, false, false) == "end" && SeedStayRules.Step(true, true, false, true) == "attack"
                && SeedStayRules.Step(true, true, false, false) == "hold"
                && GroupAiRules.HoldsStill(SeedStayRules.HoldRule) && !GroupAiRules.HoldsStill("seed-attack"),
            "Seed stay: the closest free cell, one order, attack in reach, otherwise hold");
        Check(SeedStayRules.BurstSkill(true, true) == GroupAiHealerSeedRules.SprayerSkill
                && SeedStayRules.BurstSkill(false, true) == "SKILL_SolarBloom"
                && SeedStayRules.BurstSkill(false, false) == null
                && SeedStayRules.BurstNow(0.30f, 0.30) && !SeedStayRules.BurstNow(0.31f, 0.30) && !SeedStayRules.BurstNow(null, 0.30)
                && WeakBanditRules.Classes.Any(c => c.Role == "healer" && c.TreeSkills.Contains(SeedStayRules.BloomSkill))
                && SeedStayRules.StayLine("bandit", "u", "s", "walk", 0.25f, 4, 6, 9) == "CORE_AI_SEED_STAY side=bandit unit=u seed=s step=walk share=0.25 at=4,6 tick=9"
                && SeedStayRules.NoSeedLine("bandit", "u", 2, 1, 0, 0.27f, 9) == "CORE_AI_SEED_STAY side=bandit unit=u step=noseed seeds=2 ofSide=1 usable=0 share=0.27 tick=9"
                && SeedStayRules.BanditBurstLine("h", "s", "SKILL_MucusSprayer", 0.2f, "self", 7) == "CORE_AI_BANDIT_SEED_BURST unit=h seed=s skill=MucusSprayer lowest=0.20 comrade=self tick=7",
            "Bandit seed burst: at the stay's entry share, the sprayer first, the solar bloom second");
    }
}
