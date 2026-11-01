using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: move orders, lines on the grid, vines, kiting and the escape cell.
internal static class GroupAiMovementSection
{
    internal static void Run()
    {
        MoveOrders();
        LinesOnTheGrid();
        VinesAsKite();
        EscapeCell();
    }

    static void MoveOrders()
    {
        // Move orders followed to their end: arrival, deadline and the lines the Toolkit reads.
        Check(GroupAiRules.MoveArrived(0, 1) && !GroupAiRules.MoveArrived(1, 1) && GroupAiRules.MoveArrived(1, 2) && !GroupAiRules.MoveArrived(2, 4)
                && !GroupAiRules.MoveArrived(1, 4, true) && GroupAiRules.MoveArrived(0, 4, true) && GroupAiRules.MoveResult(1, 4, 10, 240, 3, false, false, true) == null
                && GroupAiRules.MoveDeadlineTicks(0) == 60 && GroupAiRules.MoveDeadlineTicks(4) == 300 && GroupAiRules.MoveDeadlineTicks(50) == 1200
                && GroupAiRules.MoveDeadlineTicks(-3) == 60
                && GroupAiRules.MoveResult(0, 3, 10, 240, 0, false, false) == "arrived" && GroupAiRules.MoveResult(2, 3, 10, 240, 1, false, false) == null
                && GroupAiRules.MoveResult(2, 3, 240, 240, 1, false, false) == "timeout" && GroupAiRules.MoveResult(2, 3, 10, 240, 1, true, false) == "held"
                && GroupAiRules.MoveResult(3, 3, 90, 240, 0, false, false) == "blocked" && GroupAiRules.MoveResult(3, 3, 89, 240, 0, false, false) == null
                && GroupAiRules.MoveResult(1, 1, 300, 120, 1, false, false) == "timeout" && GroupAiRules.MoveResult(1, 4, 300, 120, 0, true, true) == "arrived"
                && GroupAiRules.MoveResult(2, 3, 10, 240, 1, false, true) == "interrupted" && GroupAiRules.MoveResult(2, 3, 10, 240, 1, true, true) == "held"
                && Math.Abs(GroupAiRules.MoveSpeedFactor(-40) - 0.6) < 1e-9 && Math.Abs(GroupAiRules.MoveSpeedFactor(-0.3) - 0.7) < 1e-9 && GroupAiRules.MoveSpeedFactor(0) == 1.0
                && GroupAiRules.MoveSpeedFactor(double.NaN) == 1.0 && GroupAiRules.MoveSpeedFactor(-95) == 0.2 && GroupAiRules.MoveSpeedFactor(150) == 2.0
                && GroupAiRules.MoveBlockTicks == 90
                && GroupAiRules.MoveHolds(true, 0, 240) && GroupAiRules.MoveHolds(true, 239, 240) && !GroupAiRules.MoveHolds(true, 240, 240)
                && !GroupAiRules.MoveHolds(false, 10, 240) && !GroupAiRules.MoveHolds(true, -1, 240)
                && GroupAiRules.MoveOrderFields(3, -2, "npc", 300, 9000) == " from=3,-2 path=npc deadline=300 tick=9000"
                && GroupAiRules.MoveDoneLine(false, "u1", 1, 2, 4, 2, 1, 2, "interrupted", "kite", "npc", 0, 240, "MoveTask", true, "ActCombatBehaviourTask", 3, 7, 0.6, 9240)
                    == "CORE_AI_BANDIT_MOVE_DONE unit=u1 from=1,2 to=4,2 at=1,2 result=interrupted arrived=False reason=kite path=npc moved=0 ticks=240 task=MoveTask own=1 by=ActCombatBehaviourTask waits=3 stops=7 slowed=1 speed=0.60 tick=9240"
                && GroupAiRules.MoveDoneLine(true, "p1", 449, 153, 448, 154, 448, 154, "arrived", "dodge", "direct", 1, 40, "MoveTask", false, "-", 0, 0, 1.0, 5)
                    == "CORE_AI_PLAYER_MOVE_DONE unit=p1 from=449,153 to=448,154 at=448,154 result=arrived arrived=True reason=dodge path=direct moved=1 ticks=40 task=MoveTask own=0 by=- waits=0 stops=0 slowed=0 speed=1.00 tick=5"
                && GroupAiRules.MoveProbeTicks == 30,
            "group ai: a move order ends arrived (its cell, a neighbour from 2+ cells), held, blocked or timeout and holds the unit's decisions until then");
    }

    static void LinesOnTheGrid()
    {
        // Lines on the grid: the game's vines line, and Frieren's real-angle line.
        Check(CoreLineRules.Snap8(1, 0) == (1, 0) && CoreLineRules.Snap8(3, 3) == (1, 1) && CoreLineRules.Snap8(0, 5) == (0, 1)
                && CoreLineRules.Snap8(-2, 0) == (-1, 0) && CoreLineRules.Snap8(-1, -1) == (-1, -1) && CoreLineRules.Snap8(0, -4) == (0, -1)
                && CoreLineRules.Snap8(2, -2) == (1, -1) && CoreLineRules.Snap8(-1, 2) == (-1, 1)
                && CoreLineRules.Snap8(-1, -3) == (0, -1) && CoreLineRules.Snap8(2, 1) == (1, 1) && CoreLineRules.Snap8(3, 1) == (1, 0)
                && CoreLineRules.Snap8(0, 0) == (1, 0),
            "lines: the game snaps to 8 directions (atan2 in 45 degree steps), no offset to (1,0); the T3 aim -1,-3 goes straight up");
        Check(CoreLineRules.DirectionTiles(0, 0, 1, 0, 3, (x, y) => false).SequenceEqual(new[] { (0, 0), (1, 0), (2, 0), (3, 0) })
                && CoreLineRules.DirectionTiles(0, 0, 1, 1, 7, (x, y) => x == 3).SequenceEqual(new[] { (0, 0), (1, 1), (2, 2) })
                && CoreLineRules.DirectionTiles(5, 5, 0, -1, 7, (x, y) => false).Count == 8
                && CoreLineRules.DirectionTiles(0, 0, 0, 0, 7, (x, y) => false).Count == 0
                && CoreLineRules.GameLine(4, 4, 4, 4, 2, (x, y) => false).SequenceEqual(new[] { (4, 4), (5, 4), (6, 4) })
                && CoreLineRules.GameLine(446, 152, 445, 149, 7, (x, y) => false) is { } lineMiss && !lineMiss.Contains((445, 149)) && lineMiss.Contains((446, 149)),
            "lines: the game's vines line holds the caster's cell and 7 steps and ends before a wall; the T3 aim at 445,149 passes beside it");
        Check(CoreLineRules.RealAngleTiles(0, 0, 1, 3, 3) is { } lineSteep && lineSteep.SequenceEqual(new[] { (0, 1), (1, 2), (1, 3) })
                && CoreLineRules.RealAngleTiles(0, 0, 2, 1, 2) is { } lineRight && lineRight.SequenceEqual(new[] { (1, 1), (2, 1) })
                && CoreLineRules.RealAngleTiles(0, 0, -2, -1, 2) is { } lineLeft && lineLeft.SequenceEqual(new[] { (-1, -1), (-2, -1) })
                && CoreLineRules.RealAngleTiles(10, -4, 12, -4, 2) is { } lineShifted && lineShifted.SequenceEqual(new[] { (11, -4), (12, -4) })
                && CoreLineRules.RealAngleTiles(5, 5, 5, 5, 3) == null && CoreLineRules.RealAngleTiles(0, 0, 1, 0, 0) == null
                && CoreLineRules.Trace(0, 0, 1, 0, 5, (x, y) => x == 3) is { } lineWalled && lineWalled.Open.SequenceEqual(new[] { (1, 0), (2, 0) }) && lineWalled.Blocked == (3, 0)
                && CoreLineRules.Trace(0, 0, 1, 1, 4, (x, y) => x == 1 && y == 1) is { } lineAdjacent && lineAdjacent.Open.Count == 0 && lineAdjacent.Blocked == (1, 1)
                && CoreLineRules.Trace(0, 0, 1, 0, 6, (x, y) => false, (x, y) => x == 3 || x == 5) is { } lineAtEnemy && lineAtEnemy.Target == (3, 0) && lineAtEnemy.Open.Count == 3
                && CoreLineRules.Trace(0, 0, 1, 0, 6, (x, y) => x == 2, (x, y) => x == 4) is { } lineWallFirst && lineWallFirst.Blocked == (2, 0) && lineWallFirst.Target == null
                && CoreLineRules.Trace(0, 0, 1, 0, 6, (x, y) => x == 2, (x, y) => x == 4, (x, y) => x == 2) is { } lineThrough
                    && lineThrough.Target == (4, 0) && lineThrough.Passed.SequenceEqual(new[] { (2, 0) })
                && CoreLineRules.Trace(2, 2, 2, 2, 3, (x, y) => true) == null,
            "lines: the real-angle line and its trace give Frieren's Zoltraak results (a copy; Core does not depend on Frieren)");
    }

    static void VinesAsKite()
    {
        // The vines are a kite and escape tool, never thrown blind.
        Check(GroupAiRules.VinesPick(new[] { (false, 0), (false, 2), (true, 1), (false, 0), (false, 0), (false, 0), (false, 0), (false, 0) }) == 2
                && GroupAiRules.VinesPick(new[] { (false, 1), (false, 2), (false, 2), (false, 0), (false, 0), (false, 0), (false, 0), (false, 0) }) == 1
                && GroupAiRules.VinesPick(new[] { (false, 0), (false, 0), (false, 0), (false, 0), (false, 0), (false, 0), (false, 0), (false, 0) }) == null
                && GroupAiRules.VinesPick(Array.Empty<(bool, int)>()) == null
                && GroupAiRules.VinesAllowed(true, false) && !GroupAiRules.VinesAllowed(true, true) && !GroupAiRules.VinesAllowed(false, false)
                && GroupAiRules.VinesRange == 7 && GroupAiRules.VinesNearCells == 3
                && GroupAiRules.VinesLine(false, "h", "cast", 0, -1, "t", true, 2, 99) == "CORE_AI_BANDIT_VINES unit=h result=cast dir=0,-1 target=t threat=1 near=2 tick=99"
                && GroupAiRules.VinesLine(true, "p", "none", 0, 0, "-", false, 1, 5).StartsWith("CORE_AI_PLAYER_VINES unit=p result=none", StringComparison.Ordinal),
            "vines: only a line that roots a near hostile counts, the threat first; none while provoked");
        // A slowed healer kites shorter: one cell less while slowed, one at half speed or below.
        Check(GroupAiRules.KiteStepFor(1.0, 3) == 3 && GroupAiRules.KiteStepFor(1.2, 3) == 3 && GroupAiRules.KiteStepFor(0.7, 3) == 2
                && GroupAiRules.KiteStepFor(0.6, 3) == 2 && GroupAiRules.KiteStepFor(0.5, 3) == 1 && GroupAiRules.KiteStepFor(0.3, 3) == 1
                && GroupAiRules.KiteStepFor(0.7, 1) == 1 && GroupAiRules.Slowed(0.6) && !GroupAiRules.Slowed(1.0) && !GroupAiRules.Slowed(1.2),
            "bandits: a slowed healer kites one cell shorter, at half speed or below one cell");
    }

    static void EscapeCell()
    {
        // No flee skill; the shared escape cell (EscapeRules) decides every step.
        // Open field 9x9 around (0,0): walkable everywhere except the foes' cells.
        bool Open(int x, int y) => Math.Abs(x) <= 4 && Math.Abs(y) <= 4;
        var oneFoe = new[] { new EscapeRules.Foe(1, 0, false) };
        var away = EscapeRules.Choose(0, 0, oneFoe, null, 5, 3, (x, y) => Open(x, y) && (x, y) != (1, 0), 7);
        // A neighbour foe and no safe cell next to her: still an escape cell, never standing still.
        var walled = EscapeRules.Choose(0, 0, oneFoe, null, 5, 3, (x, y) => (x, y) == (-1, 0) || (x, y) == (-2, 0), 7);
        // A dead end (x = -1 only) against a corridor with ways out: the ways out win at the same distance.
        bool Pocket(int x, int y) => (x == -1 && y == 0) || (x == 0 && y >= 1 && y <= 4);
        var noPocket = EscapeRules.Choose(0, 0, new[] { new EscapeRules.Foe(2, 0, false) }, null, 5, 1, Pocket, 7);
        // The leash weighs, it does not forbid: the friend far away still lets her flee.
        var leashed = EscapeRules.Choose(0, 0, oneFoe, new[] { (8, 8) }, 5, 3, (x, y) => Open(x, y) && (x, y) != (1, 0), 7);
        // A second foe beside the way: that path is left out while another is safe.
        var twoFoes = new[] { new EscapeRules.Foe(1, 0, false), new EscapeRules.Foe(-2, 2, false) };
        var clear = EscapeRules.Choose(0, 0, twoFoes, null, 5, 3, (x, y) => Open(x, y) && (x, y) != (1, 0) && (x, y) != (-2, 2), 7);
        // Surrounded: every way passes a second foe; the one past the stunned foe wins.
        var ring = new[] { new EscapeRules.Foe(1, 0, false), new EscapeRules.Foe(-1, 1, true), new EscapeRules.Foe(-1, -1, false) };
        bool Gaps(int x, int y) => (x, y) == (-1, 0) || (x, y) == (-2, 0) || (x, y) == (0, 1) || (x, y) == (0, 2) || (x, y) == (0, -1) || (x, y) == (0, -2);
        var surrounded = EscapeRules.Choose(0, 0, ring, null, 5, 2, Gaps, 7);
        var tieA = EscapeRules.Choose(0, 0, new EscapeRules.Foe[0], null, 5, 1, Open, 1);
        var tieB = EscapeRules.Choose(0, 0, new EscapeRules.Foe[0], null, 5, 1, Open, 1);
        Check(!CoreUnitRules.Units.First(unit => unit.Role == "healer").Skills.Contains("SKILL_Run")
                && away != null && away.X <= -2 && away.Mode == "escape"
                && walled != null && walled.X < 0
                && noPocket != null && noPocket.X == 0 && noPocket.Y == 1
                && leashed != null && leashed.X <= -2 && leashed.Leash > 0
                && clear != null && Math.Max(Math.Abs(clear.X + 2), Math.Abs(clear.Y - 2)) > 1 && clear.Mode == "escape"
                && surrounded != null && surrounded.Mode == "surrounded" && surrounded.Y >= 1
                && tieA.X == tieB.X && tieA.Y == tieB.Y && EscapeRules.SeedOf("a", 1) == EscapeRules.SeedOf("a", 1),
            "escape: away from the foe, never standing still, ways out before a dead end, leash as a weight, not past a second foe, surrounded past the stunned one, ties by the fight's seed");
    }
}
