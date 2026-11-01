using DungeonSettlersDelvers.Core;
using static TestKit;

// Enemy abilities and Core's own enemies.
internal static class EnemyAbilitiesSection
{
    internal static void Run()
    {
        AbilityRolls();
        RollPrecedence();
        CoreEnemies();
    }

    static void AbilityRolls()
    {
        // Enemy abilities: bleeding hits, silence and sleep by unit; a forced outcome wins over the roll.
        {
            var known = EnemyAbilityRules.TryGet("UNIT_Cordyceps", out var sleep) && sleep.Kind == EnemyAbilityRules.Kind.Sleep
                && EnemyAbilityRules.TryGet("UNIT_GoblinShaman", out var silence) && silence.Kind == EnemyAbilityRules.Kind.Silence
                && EnemyAbilityRules.Matches(silence, "SKILL_GoblinCurse") && !EnemyAbilityRules.Matches(silence, "SKILL_Other")
                && EnemyAbilityRules.TryGet("UNIT_AbyssGoblinShaman", out _) && !EnemyAbilityRules.TryGet("UNIT_Nobody", out _)
                && !EnemyAbilityRules.TryGet(null, out _);
            var bandit = EnemyAbilityRules.TryGet(CoreUnitRules.Prefix + "ForestBanditBlade", out var banditTrigger)
                && banditTrigger.Kind == EnemyAbilityRules.Kind.Bleed;
            Check(known && bandit && EnemyAbilityRules.SilenceKey == "AFFECTER_DelversCoreSilence" && EnemyAbilityRules.SleepKey == "AFFECTER_DelversCoreSleep",
                "enemy abilities: spore units put to sleep, shamans silence with their curse, Core's bandits bleed");
        }
        Check(EnemyAbilityRules.Fires(0.1, 0.15, null) && !EnemyAbilityRules.Fires(0.2, 0.15, null)
                && EnemyAbilityRules.Fires(0.99, 0.15, true) && !EnemyAbilityRules.Fires(0.0, 0.15, false)
                && EnemyAbilityRules.StacksAfter(98, 5) == EnemyAbilityRules.MaxBleedStacks && EnemyAbilityRules.StacksAfter(-3, 2) == 2
                && EnemyAbilityRules.IsLandedHit(true, 1, 1, 3f) && !EnemyAbilityRules.IsLandedHit(true, 0, 0, 3f)
                && !EnemyAbilityRules.IsLandedHit(true, 1, 2, 3f) && !EnemyAbilityRules.IsLandedHit(false, 1, 0, 3f),
            "enemy abilities: the roll, a forced outcome, at most 99 bleeding stacks, only landed skill hits count");
        Check(EnemyAbilityRules.RollLine("U_Blade", "SKILL_WildFlurry", 1, 0, 0.8123, 0.75, false, true)
                == "CORE_ENEMY_ABILITY_ROLL kind=bleed attacker=U_Blade skill=SKILL_WildFlurry attackType=1 result=0 roll=0.812 chance=0.75 hit=0 serrated=1"
            && EnemyAbilityRules.RollLine("U_Guard", "SKILL_ArrowRain", 2, 1, 0.1, 0.4, true, false).EndsWith("hit=1 serrated=0", StringComparison.Ordinal),
            "enemy abilities: one log line per bleeding roll, a miss too, with skill and attack type");
        {
            var standard = EnemyAbilityRules.BanditBleedOf(null, null);
            var set = EnemyAbilityRules.BanditBleedOf(25, 3.4);
            var high = EnemyAbilityRules.BanditBleedOf(250, 40);
            Check(standard.Chance == 0.4 && EnemyAbilityRules.StacksOf(standard) == 2 && set.Chance == 0.25 && EnemyAbilityRules.StacksOf(set) == 3
                    && high.Chance == 1.0 && EnemyAbilityRules.StacksOf(high) == EnemyAbilityRules.MaxBanditBleedStacks
                    && EnemyAbilityRules.TryParse("sleep", out var parsed) && parsed == EnemyAbilityRules.Kind.Sleep && !EnemyAbilityRules.TryParse("stun", out _),
                "enemy abilities: the bandits' bleeding from the settings in percent and stacks, clamped");
        }
        Check(EnemyAbilityText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(line => !string.IsNullOrWhiteSpace(line) && line.IndexOf('–') < 0 && line.IndexOf('—') < 0)),
            "enemy abilities: names and descriptions of silence and sleep in all ten languages");
    }

    static void RollPrecedence()
    {
        var bandit = EnemyAbilityRules.BanditBleedOf(null, null);
        var rat = new EnemyAbilityRules.Trigger(EnemyAbilityRules.Kind.Bleed, null, WoundRules.TraitBleedChance);
        var blade = BanditSerratedRules.Of(null, null);
        // The gate: the option opens it for every listed enemy; without it only Core's bandits roll, and only while they are in play.
        Check(EnemyAbilityRules.MayRoll(true, false) && EnemyAbilityRules.MayRoll(false, true) && !EnemyAbilityRules.MayRoll(false, false)
                && EnemyAbilityRules.Rolls(true, false, false) && EnemyAbilityRules.Rolls(true, true, false)
                && EnemyAbilityRules.Rolls(false, true, true) && !EnemyAbilityRules.Rolls(false, true, false)
                && !EnemyAbilityRules.Rolls(false, false, true) && !EnemyAbilityRules.Rolls(false, false, false),
            "enemy abilities: the option lets every listed enemy roll, without it only Core's bandits while they are in play");
        // The chance: a self-test value first, then the blade's, then the table's.
        Check(EnemyAbilityRules.ChanceOf(null, null, bandit) == 0.4 && EnemyAbilityRules.ChanceOf(null, null, rat) == WoundRules.TraitBleedChance
                && EnemyAbilityRules.ChanceOf(null, blade, bandit) == 0.75 && EnemyAbilityRules.ChanceOf(0.1, blade, bandit) == 0.1
                && EnemyAbilityRules.ChanceOf(0.0, null, bandit) == 0.0 && EnemyAbilityRules.ChanceOf(0.1, null, bandit) == 0.1,
            "enemy abilities: the chance of a roll is the test value, else the blade's 75 percent, else the table's (40 percent for the other bandits)");
        // The stacks, in the same order.
        Check(EnemyAbilityRules.StacksPerProc(null, null, bandit) == 2 && EnemyAbilityRules.StacksPerProc(null, null, rat) == WoundRules.TraitBleedStacks
                && EnemyAbilityRules.StacksPerProc(null, blade, bandit) == 3 && EnemyAbilityRules.StacksPerProc(5, blade, bandit) == 5
                && EnemyAbilityRules.StacksPerProc(5, null, bandit) == 5,
            "enemy abilities: the stacks of a proc are the test value, else the blade's 3, else the table's (2 for the other bandits)");
        // The blade's roll is for bleeding only; with the settings it follows them.
        var custom = BanditSerratedRules.Of(20, 4);
        Check(EnemyAbilityRules.UsesSerrated(EnemyAbilityRules.Kind.Bleed, true) && !EnemyAbilityRules.UsesSerrated(EnemyAbilityRules.Kind.Bleed, false)
                && !EnemyAbilityRules.UsesSerrated(EnemyAbilityRules.Kind.Silence, true) && !EnemyAbilityRules.UsesSerrated(EnemyAbilityRules.Kind.Sleep, true)
                && EnemyAbilityRules.ChanceOf(null, custom, bandit) == 0.2 && EnemyAbilityRules.StacksPerProc(null, custom, bandit) == 4,
            "enemy abilities: the blade's own roll applies to bleeding by the blade only and follows its settings");
    }

    static void CoreEnemies()
    {
        // Core's own enemies, the forest bandits: four roles, keys under Core's prefix, the goblin brawler as
        // the stand-in in a save.
        var roles = CoreUnitRules.Units.Select(unit => unit.Role).ToArray();
        var healer = CoreUnitRules.Units.First(unit => unit.Role == "healer");
        Check(roles.SequenceEqual(new[] { "blade", "guard", "archer", "healer" })
                && CoreUnitRules.Units.All(unit => CoreUnitRules.IsCoreUnit(unit.Key) && CoreUnitRules.TryGet(unit.Key, out var found) && found == unit)
                && !CoreUnitRules.IsCoreUnit("UNIT_GoblinBrawler") && !CoreUnitRules.IsCoreUnit(null)
                && !CoreUnitRules.TryGet(CoreUnitRules.Prefix + "Nobody", out _)
                && CoreUnitRules.SkillsFor(healer, true).Last() == LifebloomRules.SkillKey
                && !CoreUnitRules.SkillsFor(healer, false).Contains(LifebloomRules.SkillKey)
                && CoreUnitRules.HealthOf(120f, null) == 120f && CoreUnitRules.HealthOf(120f, 0.5) == 120f && CoreUnitRules.HealthOf(120f, 300) == 300f
                && CoreUnitRules.HealthKey(healer) == "bandits.health.healer"
                && CoreUnitRules.EquipInfo(CoreUnitRules.Units[0]).Count == CoreUnitRules.EquipSlots.Length,
            "Core units: four bandit roles under Core's prefix, Lifebloom only for the healer when on, health from the settings");
    }
}
