using DungeonSettlersDelvers.Core;
using static TestKit;

// The Toolkit's enemy skill test.
internal static class EnemySkillTestSection
{
    internal static void Run()
    {
        ParseAndOutcome();
        TargetChoice();
    }

    static void ParseAndOutcome()
    {
        // Toolkit's area block: an enemy's next decision set to one skill at a unit or a cell.
        Check(EnemySkillTestRules.TryParseTarget("3,4", out var noUnit, out var cellA) && noUnit == Guid.Empty && cellA == (3, 4)
                && EnemySkillTestRules.TryParseTarget(" -2 , 7 ", out _, out var cellB) && cellB == (-2, 7)
                && EnemySkillTestRules.TryParseTarget("6862ec60-a569-4547-a561-5bd6158d1cd4", out var unitA, out var cellC) && unitA != Guid.Empty && cellC == null
                && EnemySkillTestRules.TryParseTarget(null, out _, out var cellD) && cellD == null
                && !EnemySkillTestRules.TryParseTarget("3;4", out _, out _) && !EnemySkillTestRules.TryParseTarget(Guid.Empty.ToString(), out _, out _)
                && EnemySkillTestRules.Outcome(false, true, true, true) == "noskill" && EnemySkillTestRules.Outcome(true, true, false, false) == "cooldown"
                && EnemySkillTestRules.Outcome(true, false, false, false) == "notarget" && EnemySkillTestRules.Outcome(true, false, true, false) == "range"
                && EnemySkillTestRules.Outcome(true, false, true, true) == "cast" && EnemySkillTestRules.Outcome(true, false, true, true, true) == "forced"
                && EnemySkillTestRules.Outcome(true, false, true, false, true) == "range"
                && EnemySkillTestRules.Outcome(true, false, true, false, false, true) == "approach" && EnemySkillTestRules.Casts("approach")
                && EnemySkillTestRules.Casts("cast") && !EnemySkillTestRules.Casts("range") && !EnemySkillTestRules.Casts("forced")
                && EnemySkillTestRules.InRange(0, 0, 3, 3, 3) && !EnemySkillTestRules.InRange(0, 0, 4, 1, 3)
                && EnemySkillTestRules.Line("u", "SKILL_X", "3,4", "queued") == "CORE_TEST_ENEMY_SKILL unit=u skill=SKILL_X target=3,4 result=queued"
                && EnemySkillTestRules.Line(null, null, " ", "nounit") == "CORE_TEST_ENEMY_SKILL unit=- skill=- target=- result=nounit"
                && EnemySkillTestRules.AimsAtUnit("SKILL_ElderGlothCharge") && EnemySkillTestRules.AimsAtUnit("SKILL_MetalCharge")
                && EnemySkillTestRules.AimsAtUnit("SKILL_StoneCharge") && !EnemySkillTestRules.AimsAtUnit("SKILL_ArrowRain")
                && !EnemySkillTestRules.AimsAtUnit("SKILL_GlothCharge") && !EnemySkillTestRules.AimsAtUnit(null),
            "enemy skill test: a unit GUID or a cell x,y; skill, cooldown, target, range checked in that order; diagonals count as one; the three charges that read context.TargetEntity aim at the unit on the cell");
    }

    static void TargetChoice()
    {
        var unit = Guid.NewGuid();
        var cell = ((int X, int Y)?)(3, 4);
        const string arrowRain = "SKILL_ArrowRain";
        const string charge = "SKILL_MetalCharge";
        // The aim: a skill on the user wins over everything, then the unit of the request, then its cell.
        Check(EnemySkillTestRules.AimOf(true, unit, cell) == EnemySkillTestRules.Aim.Self
                && EnemySkillTestRules.AimOf(true, Guid.Empty, null) == EnemySkillTestRules.Aim.Self
                && EnemySkillTestRules.AimOf(false, unit, cell) == EnemySkillTestRules.Aim.Unit
                && EnemySkillTestRules.AimOf(false, Guid.Empty, cell) == EnemySkillTestRules.Aim.Cell
                && EnemySkillTestRules.AimOf(false, Guid.Empty, null) == EnemySkillTestRules.Aim.None,
            "enemy skill test: the aim is the user for a skill on the user, else the unit of the request, else its cell, else none");
        // A cell serves a skill on cells only; a charge needs the unit standing on the cell; a skill on the user always has its target.
        Check(EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Self, false, arrowRain, false, false)
                && EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Unit, false, arrowRain, false, true)
                && !EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Unit, true, arrowRain, true, false)
                && EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, arrowRain, true, false)
                && !EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, arrowRain, false, false)
                && EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, false, arrowRain, true, true)
                && !EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, false, arrowRain, true, false)
                && !EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, charge, true, false)
                && EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, charge, true, true)
                && !EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.None, true, arrowRain, true, true),
            "enemy skill test: a cell needs a skill on cells and its grid, a charge also the unit on the cell, a skill on units the unit or building on the cell, a unit must exist, a skill on the user needs nothing");
        // The selector: a skill on a cell gets the cell, never a unit; only a charge on the unit of its cell, or a skill on units, gets the unit.
        Check(EnemySkillTestRules.SelectorFor(EnemySkillTestRules.Aim.Self, true, false) == EnemySkillTestRules.SelectorKind.Self
                && EnemySkillTestRules.SelectorFor(EnemySkillTestRules.Aim.Cell, true, false) == EnemySkillTestRules.SelectorKind.Cell
                && EnemySkillTestRules.SelectorFor(EnemySkillTestRules.Aim.Unit, true, false) == EnemySkillTestRules.SelectorKind.Cell
                && EnemySkillTestRules.SelectorFor(EnemySkillTestRules.Aim.Cell, true, true) == EnemySkillTestRules.SelectorKind.Unit
                && EnemySkillTestRules.SelectorFor(EnemySkillTestRules.Aim.Unit, false, false) == EnemySkillTestRules.SelectorKind.Unit,
            "enemy skill test: a skill on cells is cast at the cell, a unit selector only for the charges on a unit or for skills on units");
        // The forced target: only a cell skill that is not aimed at a unit loses its cell to a taunt.
        Check(EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Cell, true, false, true)
                && !EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Cell, true, false, false)
                && !EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Cell, true, true, true)
                && !EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Self, true, false, true)
                && !EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Unit, false, false, true),
            "enemy skill test: a forced unit is refused only for a cell skill that is not aimed at a unit, never for a skill on itself");
        // With the outcome: the forced refusal comes after the target and the range, before the cast.
        var forced = EnemySkillTestRules.ForcedOnCell(EnemySkillTestRules.Aim.Cell, true, false, true);
        Check(EnemySkillTestRules.Outcome(true, false, EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, arrowRain, true, false), true, forced) == "forced"
                && EnemySkillTestRules.Outcome(true, false, EnemySkillTestRules.HasTarget(EnemySkillTestRules.Aim.Cell, true, arrowRain, false, false), true, forced) == "notarget",
            "enemy skill test: a cell without a grid is no target before it can be forced");
    }
}
