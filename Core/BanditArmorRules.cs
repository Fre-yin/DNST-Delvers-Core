namespace DungeonSettlersDelvers.Core;

// Bandit health and armour. The author chose "stage B" for the forest's boss
// party; the settings still override:
// - bandits.health.factor scales the row health of every bandit (after bandits.health.<role>), DefaultHealthFactor (2) by
//   default;
// - bandits.armor.physical / .magic: resistance added to the template's for every bandit, DefaultResistance (50) by default;
//   bandits.armor.<role>.physical / .magic for one role override it. A later party (a weaker one) gets its own prefix the
//   same way (Prefix(party)).
// The game's damage takes x 100 / (100 + resistance) (DamageSimulator.GetResistMultiplier):
// 50 is x0.67, 100 is x0.5. Rules without the game.
internal static class BanditArmorRules
{
    internal const string HealthFactorKey = "bandits.health.factor";
    internal const double MaxFactor = 20.0;
    internal const double MaxResistance = 1000.0;
    // Stage B: twice the row health, +50 physical and magic resistance for all four roles.
    internal const double DefaultHealthFactor = 2.0;
    internal const double DefaultResistance = 50.0;

    internal static double HealthFactor(double? setting)
        => setting is double v && !double.IsNaN(v) && v > 0 ? Math.Min(v, MaxFactor) : DefaultHealthFactor;

    internal static float ScaledHealth(float health, double factor) => (float)Math.Round(health * factor);

    // The settings prefix of a party; "bandits" is the forest's boss party.
    internal static string Prefix(string party) => string.IsNullOrEmpty(party) ? "bandits" : party;

    internal static string Key(string party, string role, string kind)
        => role == null ? Prefix(party) + ".armor." + kind : Prefix(party) + ".armor." + role + "." + kind;

    // The resistance of a role: its own setting, else the party's, else DefaultResistance; clamped.
    internal static double Resistance(double? roleSetting, double? partySetting)
    {
        var v = roleSetting is double r && !double.IsNaN(r) ? r : partySetting is double p && !double.IsNaN(p) ? p : DefaultResistance;
        return Math.Clamp(v, 0.0, MaxResistance);
    }

    // The row's resistance: the template's plus the role's (else the party's) setting. The difficulty affecters change no
    // resistance (CORE_BANDIT_STATS_PROBE: AdjustStatsData "Stat_value", none names PhysicalResistance or MagicResistance),
    // so the row value is what a bandit has; a setting of 0 leaves the template's.
    internal static float RowResistance(float template, double? roleSetting, double? partySetting)
        => (float)Math.Clamp(template + Resistance(roleSetting, partySetting), 0.0, MaxResistance);

    // The damage share left after a resistance (the game's formula, without a floor: a resistance below 0, e.g. from Zoltraak's
    // break of up to 30 stacks, raises the damage).
    internal static double DamageTaken(double resistance) => 100.0 / (100.0 + resistance);
}
