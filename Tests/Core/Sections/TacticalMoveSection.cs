using DungeonSettlersDelvers.Core;
using static TestKit;
using Facing = DungeonSettlersDelvers.Core.TacticalFormationRules.Facing;
using Slot = DungeonSettlersDelvers.Core.TacticalFormationRules.Slot;
using Unit = DungeonSettlersDelvers.Core.TacticalFormationMoveRules.Unit;

// The group's move order in formation (TacticalFormationMoveRules): the leader to the click, the members with
// their formation on to their places around it, turned the way to the click; the others keep the game's cells.
internal static class TacticalMoveSection
{
    static readonly Guid K = Guid.NewGuid(), F = Guid.NewGuid(), Y = Guid.NewGuid(), R = Guid.NewGuid(), S = Guid.NewGuid();

    // Fre right of the crown, Yin and Frieren two rows behind (the author's formation in the video).
    static readonly Slot[] Slots = { new(F, 1, 0), new(Y, 0, -2), new(R, 1, -2) };

    static bool Open(int x, int y) => true;

    internal static void Run()
    {
        var all = new[]
        {
            new Unit(K, (0, 0), true), new Unit(F, (1, 0), true), new Unit(Y, (0, -2), true), new Unit(R, (1, -2), true)
        };
        var north = TacticalFormationMoveRules.Plan(K, (0, 0), (0, 6), Slots, all, Open);
        Check(north != null && north.Facing == Facing.North && north.Cells[K] == (0, 6) && north.Cells[F] == (1, 6)
                && north.Cells[Y] == (0, 4) && north.Cells[R] == (1, 4),
            "tactical move: all four selected, a click north: the crown on the click, the others on their places");
        var south = TacticalFormationMoveRules.Plan(K, (0, 0), (0, -6), Slots, all, Open);
        Check(south != null && south.Facing == Facing.South && south.Cells[F] == (-1, -6) && south.Cells[Y] == (0, -4),
            "tactical move: a click south turns the places (the right side is west, behind is north)");
        var diagonal = TacticalFormationMoveRules.Plan(K, (0, 0), (3, -3), Slots, all, Open);
        Check(diagonal != null && diagonal.Facing == Facing.SouthEast, "tactical move: a click 3 southeast faces southeast");

        var withoutCrown = new[] { all[1], all[2] };
        var c = TacticalFormationMoveRules.Plan(K, (0, 0), (0, 8), Slots, withoutCrown, Open);
        Check(c != null && !c.Cells.ContainsKey(K) && c.Cells[F] == (1, 8) && c.Cells[Y] == (0, 6),
            "tactical move: without the crown the selected stand as if the crown stood at the click");

        var off = new[] { all[0], all[1], new Unit(Y, (0, -2), false), all[3] };
        var withOff = TacticalFormationMoveRules.Plan(K, (0, 0), (0, 6), Slots, off, Open);
        Check(withOff != null && !withOff.Cells.ContainsKey(Y) && withOff.Cells[R] == (1, 4),
            "tactical move: a member with its formation off keeps the game's cell");

        var summon = new[] { all[0], all[1], new Unit(S, (2, 0), false) };
        var aside = TacticalFormationMoveRules.Plan(K, (0, 0), (0, 6), Slots, summon, Open);
        var sCell = aside == null ? null : TacticalFormationMoveRules.AsideFor(aside, S, (1, 6), null);
        Check(aside != null && !aside.Cells.ContainsKey(S) && sCell is { } moved && moved != (1, 6) && moved != (0, 6)
                && Math.Max(Math.Abs(moved.X - 1), Math.Abs(moved.Y - 6)) == 1
                && TacticalFormationMoveRules.AsideFor(aside, S, (5, 9), null) == null,
            "tactical move: a unit outside the set keeps the game's cell, and takes the free cell beside it only from a place");

        Check(TacticalFormationMoveRules.Plan(K, (0, 0), (0, 6), Slots, new[] { all[0] }, Open) == null
                && TacticalFormationMoveRules.Plan(K, (0, 0), (0, 0), Slots, all, Open) == null,
            "tactical move: nothing to arrange for the crown alone or a click on its own cell");
        Check(TacticalFormationMoveRules.HandEnds(true, false) && !TacticalFormationMoveRules.HandEnds(true, true) && !TacticalFormationMoveRules.HandEnds(false, false),
            "tactical move: a member placed by hand is let go when the fight ends, not before");
        Check(c.WithLeader == false && c.Placed.SetEquals(new[] { F, Y }) && north.WithLeader && north.Placed.Count == 3,
            "tactical move: the plan names whether the crown moves and which members it placed (for an anchor)");
        Check(!TacticalFormationMoveRules.OutsideGrid((0, 0), (4, -4)) && TacticalFormationMoveRules.OutsideGrid((0, 0), (5, 0))
                && TacticalFormationMoveRules.OutsideGrid((0, 0), (-1, 5)),
            "tactical move: the grid reaches 4 cells around its middle");
        Check(north.Lines.Count == 4 && north.Lines[0] == $"CORE_TACTICAL_MOVE unit={K:D} to=0,6 kind=leader facing=north click=0,6",
            "tactical move: a line per rewritten cell");
        // The game's spread gave the leader a cell beside the free click.
        static bool Wall(int x, int y) => !(x == 9 && y == 9);
        static bool Figure(int x, int y) => x == 8 && y == 8;
        Check(TacticalFormationMoveRules.Anchor((4, 5), (5, 6), Open, null) == (5, 6) && TacticalFormationMoveRules.Anchor((4, 5), (5, 6), Open, Figure) == (5, 6),
            "tactical move: with the leader the anchor is the clicked cell where the leader can stand");
        Check(TacticalFormationMoveRules.Anchor((8, 7), (9, 9), Wall, null) == (8, 7) && TacticalFormationMoveRules.Anchor((7, 7), (8, 8), Open, Figure) == (7, 7),
            "tactical move: a clicked cell the leader cannot take (terrain or a figure) keeps the game's cell for the leader");
        Check(TacticalFormationMoveRules.Anchor(null, (9, 9), Wall, Figure) == (9, 9), "tactical move: without the leader the anchor is the resolved click");
        // A free cell behind a closed door or across a gap: the game gives the leader a reachable cell further off, which stays the anchor.
        Check(TacticalFormationMoveRules.Anchor((10, 4), (10, 7), Open, null) == (10, 4) && TacticalFormationMoveRules.Anchor((3, 3), (6, 6), Open, Figure) == (3, 3)
                && TacticalFormationMoveRules.Anchor((4, 5), (5, 4), Open, null) == (5, 4),
            "tactical move: a click more than a cell from the leader's resolved cell (door, gap) keeps the game's cell; one cell beside is the click");
    }
}
