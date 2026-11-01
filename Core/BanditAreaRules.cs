using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A fallen unit in a skill's own
// area no longer rules the skill out (bandits.areaignoresdowned, on by default; off keeps HitsDowned as before). When an area
// skill would take a fallen unit, the bandit weighs it: factor × standing foes it takes against the best other choice (a
// single target skill of the kit's order, else the basic attack). Fallen units count neither way; a tie goes to the area.
// Without a fallen unit in the area the kit's order stays as it was. Rules without the game.
internal static class BanditAreaRules
{
    internal const string IgnoresDownedKey = "bandits.areaignoresdowned";

    // A skill without a measured factor counts 1.0 per unit it takes ("balance only on measured values").
    internal const double ProvisionalFactor = 1.0;

    // Measured (CORE_AI_BANDIT_SKILL_VALUES, sandbox 8DFDA23C), each read from the skill's DevComment: the damage
    // share per unit it takes ÷ 100, times the hits. Relative to the bandit's own attack power, so only compared within a kit.
    internal static readonly Dictionary<string, double> Factors = new(StringComparer.Ordinal)
    {
        // Blade
        ["SKILL_Slay"] = 4.5,                 // {0}=450 % to a single foe
        ["SKILL_DeadlyFear"] = 0.3,           // {0}=30 % to one foe, then {2}=3 ExposedFlaw and Slow (BanditFearRules)
        ["SKILL_DestructiveSplit"] = 3.2,     // {0}=320 % to the target's tile and the tile behind it
        ["SKILL_BasicGreatswordAttack"] = 1.0, // {0}=100 % to the target ({1}=40 % to its sides)
        // Shieldbearer
        ["SKILL_Hammering"] = 2.6,            // {0}=260 %
        ["SKILL_WildFlurry"] = 3.2,           // {1}=80 % {2}=4 times, at {0}=30 % less hit chance
        ["SKILL_StunCharge"] = 2.0,           // {0}=200 %
        ["SKILL_BasicClubAttack"] = 0.8,      // {0}=80 %
        [BanditChainRules.SkillKey] = 1.0,    // {0}=100 %, then the pull
        // Archer
        ["SKILL_SnipingShot"] = 1.8,          // {0}=180 %, plus {1}=30 % × distance (not counted)
        ["SKILL_AnkleBreak"] = 1.8,           // {0}=180 %
        ["SKILL_ArrowRain"] = 2.4,            // {0}=40 % to every foe in the area, the rain lasts {1}=6 times
        ["SKILL_BasicBowAttack"] = 1.0,       // {0}=100 % ({2}=50 % less within {1}=1 tile)
        // Herbalist
        ["SKILL_ClingingVines"] = 1.5,        // {0}=150 % magic to foes along the line
        ["SKILL_BasicMagicMissile"] = 1.0     // {0}=100 % magic
    };

    internal static double Factor(string skill) => skill != null && Factors.TryGetValue(skill, out var f) ? f : ProvisionalFactor;

    // The area's value: factor × standing foes it takes; fallen ones add nothing.
    internal static double AreaValue(string skill, int standing) => Factor(skill) * Math.Max(0, standing);

    // A single target's value: the skill's factor once.
    internal static double SingleValue(string skill) => Factor(skill);

    // "area" when the area is worth at least the alternative (a tie goes to the area), else "alt".
    internal static string Pick(double area, double alternative) => area >= alternative ? "area" : "alt";

    internal static string ValueLine(string unit, string skill, int standing, int downed, double value, string alt, double altValue, string pick, long tick)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_AREA_VALUE unit={0} skill={1} standing={2} downed={3} value={4:0.##} alt={5} altValue={6:0.##} pick={7} tick={8}",
            unit, skill, standing, downed, value, alt ?? "-", altValue, pick, tick);

    // Once per session: what the game's data says about a bandit skill, so the factors can be entered from it.
    internal static string SkillValuesLine(string skill, string floatValues, string comment, string areaType, int radius, int range)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_SKILL_VALUES skill={0} values={1} comment=\"{2}\" area={3} radius={4} range={5}",
            skill, floatValues ?? "-", (comment ?? "-").Replace('"', '\''), areaType ?? "-", radius, range);
}
