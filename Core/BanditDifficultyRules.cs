using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Core's bandits and the game's difficulty (the bandits as measured on the game's Normal are Apocalypse's
// standard, and below it they are weaker still). The game gives every enemy at its spawn the monster affecter of the combat difficulty
// (UnitSpawner.SetEnemyAffecter): Easy, Normal, VeryHard and Downfall rows, none on Hard. Core's bandits, weak
// ones and the boss party, carry one of Core's rows instead, from this table by the game's combat difficulty (0 Easy to 4
// Downfall; Apocalypse plays as Downfall). The steps below Downfall come from the measured standard, the values of
// AFFECTER_MonsterDifficultyNormal (attack and magic times 0.4, health as measured): attack and magic follow the game's own steps,
// the game's factor divided by Downfall's 1.5 (Easy 0.15, Normal 0.4, Hard 1.0, VeryHard 1.25), and health the game's steps the
// same way (VeryHard 1.25, the others 1.0). None of them has speed, crit, crit damage or dodge: the bandits lose the game's.
// Downfall keeps the standard's attack and magic and gets what the game's Downfall gives the party, AFFECTER_PlayerDifficultyDownfall:
// crit +10, crit damage +50, dodge +10, no speed and no health. That takes away what the measurement
// named as the cause (87 % wins with the party's Downfall values, 77 % without them, the aim about 75 %; attack -50 instead
// of -60 moved it to 90 %, within the noise). A bandit's summoned unit
// (its bubblelings and toad, units of the game's keys) gets the game's row at its spawn as well and takes
// its owner's row. Rules without the game.
internal static class BanditDifficultyRules
{
    internal const string KeyPrefix = CoreSaveEffects.Prefix + "BanditDifficulty";

    // The game's row the Core rows are made from (type, categories, sign and stacks as the game's difficulty rows).
    internal const string Template = "AFFECTER_MonsterDifficultyNormal";

    internal const string AttackStat = "PhysicalAttackPowerPerModifier";
    internal const string MagicStat = "MagicAttackPowerPerModifier";
    internal const string HealthStat = "MaxHealthTotalPerModifier";

    internal const int Downfall = 4;

    internal const string CritStat = "CriticalChance";
    internal const string CritDamageStat = "CriticalDamageBonus";
    internal const string DodgeStat = "DodgeChance";

    // Attack and magic share one modifier, as in the game's rows; Extra: the party's Downfall values on Downfall's row.
    internal sealed class Row
    {
        internal readonly int Level;
        internal readonly string Name;
        internal readonly float Attack;
        internal readonly float Health;
        internal readonly (string Stat, float Value)[] Extra;

        internal Row(int level, string name, float attack, float health, params (string Stat, float Value)[] extra)
        {
            Level = level;
            Name = name;
            Attack = attack;
            Health = health;
            Extra = extra;
        }

        internal string Key => KeyPrefix + Name;
    }

    internal static readonly Row[] Table =
    {
        new(0, "Easy", -96f, -33f),
        new(1, "Normal", -89f, -33f),
        new(2, "Hard", -73f, -33f),
        new(3, "VeryHard", -67f, -17f),
        new(Downfall, "Downfall", -60f, 0f, (CritStat, 10f), (CritDamageStat, 50f), (DodgeStat, 10f))
    };

    // The row for a combat difficulty; above Downfall (Apocalypse's own value) Downfall's.
    internal static Row RowOf(int level)
    {
        var wanted = Math.Min(level, Downfall);
        foreach (var row in Table)
            if (row.Level == wanted) return row;
        return null;
    }

    internal static bool IsKey(string key) => key != null && key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    // The game's monster difficulty row of a combat difficulty, null on Hard (the game gives none there).
    internal static string GameKeyOf(int level) => GroupAiHealerSeedRules.MonsterDifficultyKey(level);

    // The row's values as the game's text form ("Stat_value"), a stat with 0 left out.
    internal static string[] StatsOf(Row row)
    {
        var stats = new List<string>();
        if (row.Attack != 0f)
        {
            stats.Add(Stat(AttackStat, row.Attack));
            stats.Add(Stat(MagicStat, row.Attack));
        }
        if (row.Health != 0f) stats.Add(Stat(HealthStat, row.Health));
        foreach (var (stat, value) in row.Extra)
            if (value != 0f) stats.Add(Stat(stat, value));
        return stats.ToArray();
    }

    private static string Stat(string name, float value) => name + "_" + value.ToString("0.##", CultureInfo.InvariantCulture);

    // The game moves a unit's current health only when the value itself changes, and clamps it to its maximum then
    // (StatusApplier.CalculateSideEffect reacts to the health values, never to a change of their maximum). A row that lowers the
    // maximum leaves the health above it (a camp boss at 6120 of 4100). So behind every swap a change this
    // small goes to head and body: the game clamps the health to the new maximum, and a unit below it loses a hundredth.
    internal const float HealthTouch = -0.01f;

    // What the game makes of the touch: the health after it, never above the maximum.
    internal static float AfterTouch(float health, float max) => Math.Min(health + HealthTouch, max);

    // A swap keeps the share of health in both directions (a raised maximum left a full camp boss at 4113 of
    // 6120, since the game moves the health no more on a rise than on a fall). The game computes a maximum as base times
    // (1 + percent / 100) of MaxHealthTotalPerModifier (ReviveShareRules.FullMax); the rows change that percent by what the row taken
    // away gave (removed, 0 while the game's row still waits in the queue) and what Core's row gives (added). The health a part
    // should have then: its share now times its maximum then.
    internal static float HealthAfter(float health, float max, float totalPercent, float removedPercent, float addedPercent)
    {
        var divisor = 100f + totalPercent;
        if (!(max > 0f) || divisor <= 1f) return health;
        var maxAfter = max * (divisor - removedPercent + addedPercent) / divisor;
        return Math.Max(0f, health / max * maxAfter);
    }

    internal const int LogCap = 60;

    internal static string SwapLine(string unit, string reason, int level, string removed, string given, string map)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_DIFFICULTY unit={0} reason={1} level={2} removed={3} given={4} map={5}",
            unit ?? "-", reason, level, removed ?? "-", given ?? "-", map ?? "-");

    // Head and body with their maximum before the queue runs, and the health Core sends them to (after the rows' change).
    internal static string HealthLine(string unit, string reason, float head, float maxHead, float headTo, float body, float maxBody, float bodyTo)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_DIFFICULTY_HEALTH unit={0} reason={1} head={2:0.#}/{3:0.#}->{4:0.#} body={5:0.#}/{6:0.#}->{7:0.#}",
            unit ?? "-", reason, head, maxHead, headTo, body, maxBody, bodyTo);

    internal static string RestoreLine(int units, int given, int kept, int level)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_DIFFICULTY_RESTORED units={0} given={1} keptGame={2} level={3}",
            units, given, kept, level);

    internal static string RegisteredLine(string key, string stats, bool ok)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_DIFFICULTY_REGISTERED key={0} stats={1} ok={2}", key, stats, ok ? 1 : 0);
}
