namespace DungeonSettlersDelvers.Core;

// The tactical formation: every member of a group has a place relative to the leader, the unit with the
// crown. A place is an offset in the formation's own space: x to the leader's right, y to the front (up in the window's grid).
// The formation turns with the leader's walk in eight directions; on the map an offset is turned by whole steps of 45 degrees
// around the leader. Pure, so the offline tests cover it; the game side reads cells and walks (TacticalFormation*).
internal static class TacticalFormationRules
{
    // The window's grid is 9 by 9 cells with the crown in the middle, so a place is at most 4 cells from
    // the leader in any direction; a farther one (an older 8 by 8 formation) is drawn to the edge.
    internal const int GridSize = 9;
    internal const int MaxOffset = GridSize / 2;

    // A new direction only after the leader walked that way for half a second; a change under 45 degrees
    // does not count, and a standing leader keeps the last direction.
    internal const float TurnSeconds = 0.5f;
    internal const double IgnoredTurnDegrees = 45.0;

    // A blocked place falls back to a free cell within this many cells of it (step 1 of Place).
    internal const int FallbackRadius = 2;
    // The last fallback looks this far around the leader (step 3 of Place).
    internal const int LeaderSearchRadius = 4;

    // The eight directions on the map, clockwise from north. The game's logical cells grow to the north and the east.
    internal enum Facing
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }

    internal readonly struct Slot
    {
        internal Slot(Guid unit, int x, int y)
        {
            Unit = unit;
            X = Math.Clamp(x, -MaxOffset, MaxOffset);
            Y = Math.Clamp(y, -MaxOffset, MaxOffset);
        }

        internal Guid Unit { get; }
        internal int X { get; }
        internal int Y { get; }
    }

    // ---- directions ----

    internal static (int X, int Y) Step(Facing facing) => facing switch
    {
        Facing.North => (0, 1),
        Facing.NorthEast => (1, 1),
        Facing.East => (1, 0),
        Facing.SouthEast => (1, -1),
        Facing.South => (0, -1),
        Facing.SouthWest => (-1, -1),
        Facing.West => (-1, 0),
        _ => (-1, 1)
    };

    // The angle of a walk in degrees, clockwise from north (0 to 360).
    internal static double AngleOf(int dx, int dy)
    {
        var degrees = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    // The nearest of the eight directions to a walk; null for no walk.
    internal static Facing? FacingOf(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return null;
        return (Facing)((int)Math.Floor(AngleOf(dx, dy) / 45.0 + 0.5) % 8);
    }

    // The smaller angle between a walk and a direction, 0 to 180 degrees.
    internal static double TurnDegrees(Facing facing, int dx, int dy)
    {
        var difference = Math.Abs(AngleOf(dx, dy) - (int)facing * 45.0) % 360.0;
        return difference > 180.0 ? 360.0 - difference : difference;
    }

    // The formation's direction with the turn it may be about to make: Candidate is the new direction the leader walks,
    // Since when it was first seen.
    internal readonly struct Turn
    {
        internal Turn(Facing facing, Facing? candidate = null, float since = 0f)
        {
            Facing = facing;
            Candidate = candidate;
            Since = since;
        }

        internal Facing Facing { get; }
        internal Facing? Candidate { get; }
        internal float Since { get; }
    }

    // One look at the leader's walk (dx, dy in cells since the last look; 0, 0 while standing) at the time now in seconds.
    internal static Turn Next(Turn turn, int dx, int dy, float now)
    {
        if ((dx == 0 && dy == 0) || TurnDegrees(turn.Facing, dx, dy) < IgnoredTurnDegrees) return new Turn(turn.Facing);
        var candidate = FacingOf(dx, dy).Value;
        if (turn.Candidate != candidate) return new Turn(turn.Facing, candidate, now);
        return now - turn.Since >= TurnSeconds ? new Turn(candidate) : turn;
    }

    // ---- offsets on the map ----

    // An offset turned clockwise by whole steps of 45 degrees, kept on its square ring around the leader: every cell of a ring
    // moves by the ring's radius per step. The turn never joins two places and never moves a place nearer or farther; for the
    // four main directions it is the exact turn by 90 degrees.
    internal static (int X, int Y) Rotate(int x, int y, Facing facing)
    {
        var radius = Math.Max(Math.Abs(x), Math.Abs(y));
        if (radius == 0) return (0, 0);
        var length = 8 * radius;
        var index = (RingIndex(x, y, radius) + (int)facing * radius) % length;
        return RingCell(index, radius);
    }

    // The map cell of a place: the leader's cell plus the offset turned to the formation's direction.
    internal static (int X, int Y) CellOf(int leaderX, int leaderY, int offsetX, int offsetY, Facing facing)
    {
        var (x, y) = Rotate(offsetX, offsetY, facing);
        return (leaderX + x, leaderY + y);
    }

    // Clockwise along the ring from its top left corner: top edge to the east, right edge to the south, bottom edge to the
    // west, left edge to the north.
    private static int RingIndex(int x, int y, int r)
    {
        if (y == r && x < r) return x + r;
        if (x == r && y > -r) return 2 * r + (r - y);
        if (y == -r && x > -r) return 4 * r + (r - x);
        return 6 * r + (y + r);
    }

    private static (int X, int Y) RingCell(int index, int r)
    {
        if (index < 2 * r) return (index - r, r);
        if (index < 4 * r) return (r, r - (index - 2 * r));
        if (index < 6 * r) return (r - (index - 4 * r), -r);
        return (-r, (index - 6 * r) - r);
    }

    // ---- placing the group ----

    // Where each member goes, in the order of the slots; null for a member without any free cell. free says whether a cell can
    // be walked on and reached. A blocked place falls back, in this order (Core's plan):
    //   1. the nearest free cell within FallbackRadius of the place, on the leader's same side first;
    //   2. else the mirrored place on the other side of the leader's axis;
    //   3. else the nearest free cell around the leader.
    // Never two members on one cell, never one on the leader's cell. Members whose own place is free are placed first, so a
    // fallback never takes another member's place.
    internal static (int X, int Y)?[] Place(int leaderX, int leaderY, Facing facing, IReadOnlyList<Slot> slots, Func<int, int, bool> free)
    {
        var result = new (int X, int Y)?[slots.Count];
        var taken = new HashSet<(int, int)> { (leaderX, leaderY) };
        bool Open((int X, int Y) cell) => !taken.Contains(cell) && free(cell.X, cell.Y);
        for (var i = 0; i < slots.Count; i++)
        {
            var cell = CellOf(leaderX, leaderY, slots[i].X, slots[i].Y, facing);
            if (!Open(cell)) continue;
            result[i] = cell;
            taken.Add(cell);
        }
        for (var i = 0; i < slots.Count; i++)
        {
            if (result[i] != null) continue;
            var cell = Fallback(leaderX, leaderY, facing, slots[i], Open);
            if (cell == null) continue;
            result[i] = cell;
            taken.Add(cell.Value);
        }
        return result;
    }

    private static (int X, int Y)? Fallback(int leaderX, int leaderY, Facing facing, Slot slot, Func<(int X, int Y), bool> open)
    {
        var place = CellOf(leaderX, leaderY, slot.X, slot.Y, facing);
        var side = Side(facing, place.X - leaderX, place.Y - leaderY);
        var near = Nearest(place, FallbackRadius, open, cell => side == 0 || Side(facing, cell.X - leaderX, cell.Y - leaderY) == side);
        if (near != null) return near;
        var mirrored = CellOf(leaderX, leaderY, -slot.X, slot.Y, facing);
        if (open(mirrored)) return mirrored;
        return Nearest((leaderX, leaderY), LeaderSearchRadius, open, null, place);
    }

    // The nearest open cell within radius of the centre (not the centre), the preferred ones first, then by distance to
    // the centre and to towards, then along the ring; null for none.
    private static (int X, int Y)? Nearest((int X, int Y) centre, int radius, Func<(int X, int Y), bool> open,
        Func<(int X, int Y), bool> preferred, (int X, int Y)? towards = null)
    {
        (int X, int Y)? best = null;
        var bestKey = (Preferred: false, Distance: 0, Towards: 0, Order: 0);
        var order = 0;
        for (var r = 1; r <= radius; r++)
            for (var index = 0; index < 8 * r; index++)
            {
                var (dx, dy) = RingCell(index, r);
                var cell = (X: centre.X + dx, Y: centre.Y + dy);
                order++;
                if (!open(cell)) continue;
                var key = (Preferred: preferred == null || preferred(cell), Distance: dx * dx + dy * dy,
                    Towards: towards is { } t ? Square(cell.X - t.X) + Square(cell.Y - t.Y) : 0, Order: order);
                if (best == null || Better(key, bestKey))
                {
                    best = cell;
                    bestKey = key;
                }
            }
        return best;
    }

    private static bool Better((bool Preferred, int Distance, int Towards, int Order) a, (bool Preferred, int Distance, int Towards, int Order) b)
    {
        if (a.Preferred != b.Preferred) return a.Preferred;
        if (a.Distance != b.Distance) return a.Distance < b.Distance;
        if (a.Towards != b.Towards) return a.Towards < b.Towards;
        return a.Order < b.Order;
    }

    private static int Square(int value) => value * value;

    // The leader's side of a cell offset: 1 left of the walk, -1 right, 0 on the axis.
    internal static int Side(Facing facing, int dx, int dy)
    {
        var (fx, fy) = Step(facing);
        return Math.Sign(fx * dy - fy * dx);
    }

    // ---- the default places ----

    // The default places of count members: rows behind the leader, the nearest first, the middle before the sides and the
    // left before the right. With the leader in its default cell every place fits the grid. Places in front of or beside the
    // leader are the player's choice (a member may also walk ahead, a tank as the point).
    internal static List<(int X, int Y)> DefaultOffsets(int count)
    {
        var offsets = new List<(int X, int Y)>(count);
        var right = GridSize - 1 - DefaultLeaderColumn;
        for (var row = 1; row <= GridSize - 1 - DefaultLeaderRow && offsets.Count < count; row++)
            for (var spread = 0; spread <= Math.Max(DefaultLeaderColumn, right) && offsets.Count < count; spread++)
            {
                if (spread <= DefaultLeaderColumn) offsets.Add((-spread, -row));
                if (spread > 0 && spread <= right && offsets.Count < count) offsets.Add((spread, -row));
            }
        return offsets;
    }

    // The leader's cell in the window: the middle of the grid, so places fit in front, beside and behind.
    internal const int DefaultLeaderColumn = GridSize / 2;
    internal const int DefaultLeaderRow = GridSize / 2;
}
