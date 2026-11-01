namespace DungeonSettlersDelvers.Core;

// The enemy abilities as plain data: which enemies roll bleeding, silence or sleep on a landed hit,
// and with which chance. Bleeding: rats, goblins and other biters, with their abyss variants;
// silence: goblin shamans and the white mawmurk, only with the named skill; sleep: the spore units.
// The forest bandits bleed with the chance and stacks of the settings (BanditBleed). No game types
// here; EnemyAbilities applies the rolls.
internal static class EnemyAbilityRules
{
    internal enum Kind
    {
        Bleed,
        Silence,
        Sleep
    }

    internal readonly struct Trigger
    {
        internal Kind Kind { get; }
        // Only hits of this skill roll; null for any skill.
        internal string Skill { get; }
        internal double Chance { get; }
        // Bleeding stacks per proc; 0 for the default (StacksOf).
        internal int Stacks { get; }

        internal Trigger(Kind kind, string skill, double chance, int stacks = 0)
        {
            Kind = kind;
            Skill = skill;
            Chance = chance;
            Stacks = stacks;
        }
    }

    internal const string BleedingKey = "AFFECTER_Bleeding";
    internal const string SilenceKey = CoreSaveEffects.Prefix + "Silence";
    internal const string SleepKey = CoreSaveEffects.Prefix + "Sleep";
    internal const float BleedSeconds = 6f;
    internal const float SilenceSeconds = 5f;
    internal const float SleepSeconds = 8f;
    internal const double SleepChance = 0.15;
    // The game's cap of bleeding stacks.
    internal const int MaxBleedStacks = 99;

    // An attacker under the abyss rebound rolls no bleeding.
    internal static readonly string[] ReboundKeys = { "AFFECTER_AbyssRebound", "AFFECTER_AbyssRebound2" };

    internal static readonly string[] BleedUnits =
    {
        "GreyRat", "WeakGreyRat", "RedRat", "GreyMonsterRat", "GlothPup", "ElderGloth", "TadpoleEvolve", "WildDog",
        "MushBiter", "Mawmurk", "SpikeTurtle", "HedgeHog", "NeedleSpider", "Bee", "Staghare", "MetalScarab",
        "GoblinWorker", "GoblinBrawler", "GoblinRider", "GoblinThrower", "BlueGoblin", "ChainKnightSentinel",
        "OldDragonClawWarrior", "FireDragonClawWarrior"
    };

    internal static readonly string[] BleedAbyssUnits =
    {
        "GreyRat", "WeakGreyRat", "RedRat", "GreyMonsterRat", "GlothPup", "ElderGloth", "TadpoleEvolve", "Mawmurk",
        "SpikeTurtle", "Bee", "MetalScarab", "GoblinWorker", "GoblinBrawler", "GoblinRider", "GoblinThrower",
        "ChainKnightSentinel"
    };

    private static readonly Dictionary<string, Trigger> Triggers = Build();

    // The forest bandits' bleeding, from the settings: chance in percent, stacks per proc.
    internal const string BanditBleedChanceKey = "bandits.bleed.chance";
    internal const string BanditBleedStacksKey = "bandits.bleed.stacks";
    // 40 % for the guard, archer and herbalist; the blade bleeds with its Serrated Blade (BanditSerratedRules).
    internal const double BanditBleedChance = 0.4;
    internal const int BanditBleedStacks = 2;
    internal const int MaxBanditBleedStacks = 10;
    private static Trigger banditBleed = BanditBleedOf(null, null);

    internal static Trigger BanditBleed => banditBleed;

    internal static int StacksOf(Trigger trigger) => trigger.Stacks > 0 ? trigger.Stacks : WoundRules.TraitBleedStacks;

    private static Dictionary<string, Trigger> Build()
    {
        var map = new Dictionary<string, Trigger>(StringComparer.Ordinal);
        var bleed = new Trigger(Kind.Bleed, null, WoundRules.TraitBleedChance);
        foreach (var unit in BleedUnits) map["UNIT_" + unit] = bleed;
        foreach (var unit in BleedAbyssUnits) map["UNIT_Abyss" + unit] = bleed;
        Both("GoblinShaman", new Trigger(Kind.Silence, "SKILL_GoblinCurse", 1.0));
        Both("WhiteMawmurk", new Trigger(Kind.Silence, "SKILL_ArcaneEco", 0.3));
        Both("GoblinChiefShaman", new Trigger(Kind.Silence, "SKILL_BlazingEchoGoblin", 0.1));
        var sleep = new Trigger(Kind.Sleep, null, SleepChance);
        Both("SporeInsect", sleep);
        Both("SmallSporeSlime", sleep);
        Both("MediumSporeSlime", sleep);
        map["UNIT_Cordyceps"] = sleep;
        return map;

        // The unit and its abyss variant.
        void Both(string unit, Trigger value)
        {
            map["UNIT_" + unit] = value;
            map["UNIT_Abyss" + unit] = value;
        }
    }

    internal static Trigger BanditBleedOf(double? chancePercent, double? stacks)
    {
        var chance = chancePercent is double percent ? Math.Clamp(percent, 0.0, 100.0) / 100.0 : BanditBleedChance;
        var count = stacks is double value ? (int)Math.Clamp(Math.Round(value), 1.0, MaxBanditBleedStacks) : BanditBleedStacks;
        return new Trigger(Kind.Bleed, null, chance, count);
    }

    internal static void UseBanditBleed(Trigger trigger) => banditBleed = trigger;

    internal static bool TryGet(string unitKey, out Trigger trigger)
    {
        trigger = default;
        if (unitKey == null) return false;
        if (CoreUnitRules.TryGet(unitKey, out _))
        {
            trigger = banditBleed;
            return true;
        }
        return Triggers.TryGetValue(unitKey, out trigger);
    }

    internal static IEnumerable<string> Units(Kind kind)
    {
        foreach (var trigger in Triggers)
            if (trigger.Value.Kind == kind) yield return trigger.Key;
    }

    internal static bool Matches(Trigger trigger, string skillKey) => trigger.Skill == null || trigger.Skill == skillKey;

    // A hit of a skill, melee or ranged, normal or critical, with damage.
    internal static bool IsLandedHit(bool fromSkill, int attackType, int result, float damage)
        => fromSkill && attackType != 0 && (result == 0 || result == 1) && damage > 0f;

    // One line per bleeding roll, a miss too (hit=0), at most RollLogCap per session.
    internal const int RollLogCap = 5000;

    internal static string RollLine(string attacker, string skill, int attackType, int result, double roll, double chance, bool hit, bool serrated)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "CORE_ENEMY_ABILITY_ROLL kind=bleed attacker={0} skill={1} attackType={2} result={3} roll={4:0.000} chance={5:0.00} hit={6} serrated={7}",
            attacker, skill, attackType, result, roll, chance, hit ? 1 : 0, serrated ? 1 : 0);

    // Whether any enemy may roll at all: with the option on, or while Core's bandits are in play (their bleeding works without it).
    internal static bool MayRoll(bool option, bool banditsActive) => option || banditsActive;

    // Whether this attacker rolls: with the option on every listed enemy does; without it only a Core bandit, and only while the
    // bandits are in play.
    internal static bool Rolls(bool option, bool banditsActive, bool attackerIsBandit) => MayRoll(option, banditsActive) && (option || attackerIsBandit);

    // The blade's own bleeding (BanditSerratedRules) replaces the common roll of a bleeding hit by the blade.
    internal static bool UsesSerrated(Kind kind, bool attackerIsBlade) => kind == Kind.Bleed && attackerIsBlade;

    // The chance of a roll: a self-test value first, then the blade's, then the table's (the bandits' from the settings).
    internal static double ChanceOf(double? test, (double Chance, int Stacks)? serrated, Trigger trigger) => test ?? serrated?.Chance ?? trigger.Chance;

    // The bleeding stacks of a proc, in the same order.
    internal static int StacksPerProc(int? test, (double Chance, int Stacks)? serrated, Trigger trigger) => test ?? serrated?.Stacks ?? StacksOf(trigger);

    // A forced outcome (self-test) wins over the roll.
    internal static bool Fires(double roll, double chance, bool? forced) => forced ?? roll < chance;

    internal static int StacksAfter(int before, int stacks) => Math.Min(MaxBleedStacks, Math.Max(0, before) + Math.Max(0, stacks));

    internal static string Describe(Kind kind) => kind switch
    {
        Kind.Silence => "silence",
        Kind.Sleep => "sleep",
        _ => "bleed"
    };

    internal static bool TryParse(string text, out Kind kind)
    {
        switch (text)
        {
            case "bleed":
                kind = Kind.Bleed;
                return true;
            case "silence":
                kind = Kind.Silence;
                return true;
            case "sleep":
                kind = Kind.Sleep;
                return true;
            default:
                kind = Kind.Bleed;
                return false;
        }
    }
}
