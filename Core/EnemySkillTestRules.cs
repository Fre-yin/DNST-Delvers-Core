using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Test tool for Toolkit's area block (a UnitOrderRequested skill order does nothing for enemies):
// DelversCoreRuntime.UseEnemySkillForTest sets an enemy's next combat decision to exactly one skill at a unit or a cell. Core
// hooks only the pattern class of that enemy, on the first call (EnemySkillTest). Rules without the game.
internal static class EnemySkillTestRules
{
    // A target is a unit GUID or a cell "x,y". Null or blank: none (only for skills on the user itself).
    internal static bool TryParseTarget(string target, out Guid unit, out (int X, int Y)? cell)
    {
        unit = Guid.Empty;
        cell = null;
        if (string.IsNullOrWhiteSpace(target)) return true;
        if (Guid.TryParse(target.Trim(), out unit)) return unit != Guid.Empty;
        var parts = target.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            cell = (x, y);
            return true;
        }
        return false;
    }

    // What a request aims at, in this order: the user itself for a skill on the user, else the unit of the request, else its cell.
    internal enum Aim
    {
        None,
        Self,
        Unit,
        Cell
    }

    internal static Aim AimOf(bool skillOnSelf, Guid unit, (int X, int Y)? cell)
        => skillOnSelf ? Aim.Self : unit != Guid.Empty ? Aim.Unit : cell != null ? Aim.Cell : Aim.None;

    // Whether the aim found something to cast at. A unit must exist; a cell serves a skill on cells with its grid, and a skill
    // that needs a unit (AimsAtUnit) needs the unit standing on the cell as well; a skill on units aimed at a cell takes the
    // unit or building standing there (a seed's upgrade at the seed's cell). unitFound is the unit of the
    // request or the unit or building on the cell; gridFound is only looked at for a cell.
    internal static bool HasTarget(Aim aim, bool skillOnCell, string skill, bool gridFound, bool unitFound) => aim switch
    {
        Aim.Self => true,
        Aim.Unit => unitFound,
        Aim.Cell => skillOnCell ? gridFound && (!AimsAtUnit(skill) || unitFound) : unitFound,
        _ => false
    };

    // A forced (taunted) unit loses a cell aim, so a cell skill that is not aimed at a unit is refused for it (see Outcome).
    internal static bool ForcedOnCell(Aim aim, bool skillOnCell, bool onUnit, bool forced)
        => aim != Aim.Self && !onUnit && skillOnCell && forced;

    internal enum SelectorKind
    {
        Self,
        Cell,
        Unit
    }

    // The selector handed to the game: the user itself, the cell for a skill on cells, else the unit (a charge aimed at the
    // unit on the cell, onUnit, takes the unit).
    internal static SelectorKind SelectorFor(Aim aim, bool skillOnCell, bool onUnit)
        => aim == Aim.Self ? SelectorKind.Self : skillOnCell && !onUnit ? SelectorKind.Cell : SelectorKind.Unit;

    // The outcome at the decision, checked in this order: the skill, its cooldown, the target, the range, and for a skill on a
    // cell a forced (taunted) unit. The game's DecideNonPlayerCombatBehaviour swaps a forced unit's selector for the forced
    // target, the cell is lost, and a cell skill's routine throws on context.TargetGrid (block flaechen: six
    // NullReferenceException in SKILL_ArrowRain.DamageRoutine, one per volley; the routine reads
    // ExecutionContext.TargetGrid without a check, the archer was taunted by Darius just before). CoreUnitCombat falls back the same way for bandits.
    // A unit aimed at out of reach (-I3: twenty melee skills stayed "range") is "approach": the skill goes to the game
    // with that unit, and the game's own pattern walks the user into reach as for any of its decisions. A cell stays "range".
    internal static string Outcome(bool hasSkill, bool onCooldown, bool hasTarget, bool inRange, bool forcedOnCell = false, bool unitTarget = false)
        => !hasSkill ? "noskill" : onCooldown ? "cooldown" : !hasTarget ? "notarget" : !inRange ? (unitTarget ? "approach" : "range")
            : forcedOnCell ? "forced" : "cast";

    // Whether the request goes to the game: cast, or approach a unit out of reach.
    internal static bool Casts(string outcome) => outcome == "cast" || outcome == "approach";

    // Charges whose row aims at a cell but whose routine needs a unit: the game's own patterns aim them with an entity selector
    // (Pattern_ElderGloth: EntityTargetSelector.ClosestHostile), and each SkillRoutine reads context.TargetEntity
    // early without a check (ElderGloth after its first FX step, MetalCharge after 1 s, StoneCharge after 0.7 s).
    // A cell gives ExecutionContext.Convert(GridData) with no unit, and the routine and its FX throw (block
    // ausweichen: NullReferenceException in SKILL_ElderGlothCharge and SKILL_MetalCharge SkillRoutine and BuildSkillFX, the charge
    // never ran). So for these the test aims at the unit on the given cell.
    private static readonly HashSet<string> unitAimed = new(StringComparer.Ordinal)
    {
        "SKILL_ElderGlothCharge", "SKILL_MetalCharge", "SKILL_StoneCharge",
    };

    internal static bool AimsAtUnit(string skill) => skill != null && unitAimed.Contains(skill);

    // Range in cells, a diagonal counts as one (as the game's skill range, GroupAiRules.Distance), without the game's one extra
    // cell (RangeCondition, CORRECTION_RANGE = 1).
    internal static bool InRange(int fromX, int fromY, int toX, int toY, int range)
        => Math.Max(Math.Abs(fromX - toX), Math.Abs(fromY - toY)) <= range;

    internal static string Line(string unit, string skill, string target, string result)
        => "CORE_TEST_ENEMY_SKILL unit=" + (string.IsNullOrEmpty(unit) ? "-" : unit) + " skill=" + (string.IsNullOrEmpty(skill) ? "-" : skill)
            + " target=" + (string.IsNullOrWhiteSpace(target) ? "-" : target.Trim()) + " result=" + result;
}
