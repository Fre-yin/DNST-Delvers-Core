using DungeonSettlersDelvers.Core;
using static TestKit;
using static DungeonSettlersDelvers.Core.TacticalFormationRules;

// The tactical formation's rules (TacticalFormationRules): the turn of the places with the leader's walk, the filter against
// jumping between directions, the fallback of a blocked place and the default places.
internal static class TacticalFormationSection
{
    internal static void Run()
    {
        Directions();
        Rotation();
        Turning();
        Placing();
        Defaults();
    }

    static void Directions()
    {
        Check(FacingOf(0, 5) == Facing.North && FacingOf(3, 3) == Facing.NorthEast && FacingOf(4, 0) == Facing.East
                && FacingOf(1, -1) == Facing.SouthEast && FacingOf(0, -2) == Facing.South && FacingOf(-2, -2) == Facing.SouthWest
                && FacingOf(-1, 0) == Facing.West && FacingOf(-5, 5) == Facing.NorthWest && FacingOf(0, 0) == null,
            "tactical: a walk gives the nearest of the eight directions, none while standing");
        Check(FacingOf(1, 3) == Facing.North && FacingOf(2, 3) == Facing.NorthEast,
            "tactical: a walk between two directions goes to the nearer one");
        Check(Math.Abs(TurnDegrees(Facing.North, 0, -1) - 180.0) < 1e-9 && Math.Abs(TurnDegrees(Facing.West, 1, 1) - 135.0) < 1e-9
                && Math.Abs(TurnDegrees(Facing.NorthWest, 1, 1) - 90.0) < 1e-9,
            "tactical: the turn between a walk and a direction is the smaller angle");
    }

    static void Rotation()
    {
        Check(Rotate(0, 2, Facing.North) == (0, 2) && Rotate(0, 2, Facing.East) == (2, 0) && Rotate(0, 2, Facing.South) == (0, -2)
                && Rotate(0, 2, Facing.West) == (-2, 0),
            "tactical: the front turns with the walk, north east south west");
        Check(Rotate(1, 0, Facing.North) == (1, 0) && Rotate(1, 0, Facing.East) == (0, -1) && Rotate(-1, -1, Facing.East) == (-1, 1),
            "tactical: the leader's right stays right of the walk");
        Check(Rotate(1, 2, Facing.East) == (2, -1) && Rotate(-3, 1, Facing.South) == (3, -1),
            "tactical: the four main directions turn a place exactly by 90 degrees");
        Check(Rotate(0, 1, Facing.NorthEast) == (1, 1) && Rotate(0, 2, Facing.NorthEast) == (2, 2) && Rotate(1, 0, Facing.NorthEast) == (1, -1),
            "tactical: a diagonal walk turns a place along its ring by 45 degrees");
        var unique = true;
        var sameRing = true;
        foreach (Facing facing in Enum.GetValues(typeof(Facing)))
        {
            var seen = new HashSet<(int, int)>();
            for (var x = -MaxOffset; x <= MaxOffset; x++)
                for (var y = -MaxOffset; y <= MaxOffset; y++)
                {
                    var turned = Rotate(x, y, facing);
                    unique &= seen.Add(turned);
                    sameRing &= Math.Max(Math.Abs(turned.X), Math.Abs(turned.Y)) == Math.Max(Math.Abs(x), Math.Abs(y));
                }
        }
        Check(unique && sameRing, "tactical: a turn never puts two places on one cell and keeps each place as far from the leader");
        var full = true;
        for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
            {
                var p = (x, y);
                for (var i = 0; i < 8; i++) p = Rotate(p.Item1, p.Item2, Facing.NorthEast);
                full &= p == (x, y);
            }
        Check(full, "tactical: eight turns by 45 degrees bring every place back");
        Check(CellOf(10, 20, -1, -2, Facing.North) == (9, 18) && CellOf(10, 20, -1, -2, Facing.South) == (11, 22),
            "tactical: the map cell is the leader's cell plus the turned place");
    }

    static void Turning()
    {
        var turn = new Turn(Facing.North);
        Check(Next(turn, 0, 0, 1f).Facing == Facing.North && Next(turn, 0, 0, 1f).Candidate == null,
            "tactical: a standing leader keeps the last direction");
        Check(Next(turn, 1, 3, 1f).Candidate == null, "tactical: a walk less than 45 degrees off does not count");
        var first = Next(turn, 1, 0, 1f);
        Check(first.Facing == Facing.North && first.Candidate == Facing.East && first.Since == 1f,
            "tactical: a new walk direction waits as a candidate");
        Check(Next(first, 1, 0, 1.4f).Facing == Facing.North, "tactical: the formation does not turn before half a second");
        Check(Next(first, 1, 0, 1.5f).Facing == Facing.East && Next(first, 1, 0, 1.5f).Candidate == null,
            "tactical: the formation turns after half a second of the same walk direction");
        var other = Next(first, 0, -1, 1.3f);
        Check(other.Candidate == Facing.South && other.Since == 1.3f && Next(other, 0, -1, 1.6f).Facing == Facing.North,
            "tactical: a changed walk starts the half second again");
        Check(Next(first, 0, 0, 2f).Candidate == null && Next(Next(first, 0, 0, 2f), 1, 0, 2.1f).Since == 2.1f,
            "tactical: standing still drops a waiting turn");
        Check(Next(new Turn(Facing.North), 1, 1, 0f).Candidate == Facing.NorthEast,
            "tactical: a turn of exactly 45 degrees counts");
    }

    static void Placing()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var slots = new[] { new Slot(a, -1, -1), new Slot(b, 1, -1), new Slot(c, 0, -2) };
        var open = Place(10, 10, Facing.North, slots, (x, y) => true);
        Check(open[0] == (9, 9) && open[1] == (11, 9) && open[2] == (10, 8), "tactical: free places are taken as planned");
        var east = Place(10, 10, Facing.East, slots, (x, y) => true);
        Check(east[0] == (9, 11) && east[1] == (9, 9) && east[2] == (8, 10), "tactical: walking east the group stands west of the leader");

        // a's place (9, 9) is blocked: the nearest free cell on the leader's left side within two cells.
        var blocked = Place(10, 10, Facing.North, slots, (x, y) => (x, y) != (9, 9));
        Check(blocked[0] == (9, 10) && blocked[1] == (11, 9) && blocked[2] == (10, 8),
            "tactical: a blocked place falls back to the nearest free cell on the same side");
        var leftWall = Place(10, 10, Facing.North, slots, (x, y) => x >= 10);
        Check(leftWall[0] == (11, 9) || leftWall[0] == (11, 10) || leftWall[0] == (11, 8) || leftWall[0] == (10, 9),
            "tactical: without a free cell on the same side another free cell near the place is taken");
        Check(leftWall[1] == (11, 9) != (leftWall[0] == (11, 9)), "tactical: never two members on one cell");

        // Only the mirrored place is free near the leader.
        var far = new[] { new Slot(a, -4, 0) };
        var onlyMirror = Place(20, 20, Facing.North, far, (x, y) => (x, y) == (24, 20));
        Check(onlyMirror[0] == (24, 20), "tactical: a blocked place without a free cell near it takes the mirrored place");
        var nearLeader = Place(20, 20, Facing.North, far, (x, y) => (x, y) == (21, 22));
        Check(nearLeader[0] == (21, 22), "tactical: else the nearest free cell around the leader");
        var none = Place(20, 20, Facing.North, far, (x, y) => false);
        Check(none[0] == null, "tactical: no free cell leaves the member without a place");
        var notLeader = Place(20, 20, Facing.North, new[] { new Slot(a, 0, -1) }, (x, y) => (x, y) == (20, 20));
        Check(notLeader[0] == null, "tactical: nobody is sent onto the leader's cell");

        // b's own place is free; a's fallback must not take it even though a comes first.
        var order = Place(10, 10, Facing.North, new[] { new Slot(a, 0, -1), new Slot(b, 1, -1) }, (x, y) => (x, y) != (10, 9));
        Check(order[1] == (11, 9) && order[0] != (11, 9) && order[0] != null,
            "tactical: a member whose place is free keeps it against another member's fallback");
    }

    static void Defaults()
    {
        var offsets = DefaultOffsets(5);
        Check(offsets.Count == 5 && offsets[0] == (0, -1) && offsets[1] == (-1, -1) && offsets[2] == (1, -1),
            "tactical: the default places start right behind the leader, middle first, then left and right");
        var all = DefaultOffsets(100);
        var fit = all.Count == (GridSize - 1 - DefaultLeaderRow) * GridSize;
        foreach (var (x, y) in all)
            fit &= DefaultLeaderColumn + x >= 0 && DefaultLeaderColumn + x < GridSize && DefaultLeaderRow - y >= 0 && DefaultLeaderRow - y < GridSize;
        Check(fit && all.Distinct().Count() == all.Count, "tactical: every default place fits the grid behind the leader, each once");
        var slot = new Slot(Guid.NewGuid(), 12, -9);
        Check(slot.X == MaxOffset && slot.Y == -MaxOffset, "tactical: a place is at most four cells from the leader, a farther one is drawn to the edge");
    }
}
