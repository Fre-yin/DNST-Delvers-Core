namespace DungeonSettlersDelvers.Core;

// The floor box of the anchor room the camp hangs on (open cells only, its walls lie outside), in map cells.
internal readonly struct BanditCampAnchor
{
    internal BanditCampAnchor(int minX, int minY, int maxX, int maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    internal int MinX { get; }
    internal int MinY { get; }
    internal int MaxX { get; }
    internal int MaxY { get; }
}

// One map cell to write: absolute x and y and the three raw values.
internal readonly struct BanditCampWrite
{
    internal BanditCampWrite(int x, int y, BanditCampTerrain terrain)
    {
        X = x;
        Y = y;
        Terrain = terrain;
    }

    internal int X { get; }
    internal int Y { get; }
    internal BanditCampTerrain Terrain { get; }
}

// The result of the search: where the camp and its corridor go, in absolute map cells, or the reason there is no place.
internal sealed class BanditCampPlan
{
    private BanditCampPlan(string reason) => Reason = reason;

    private BanditCampPlan(BanditCampSide side, int length, int campOriginX, int campOriginY, int exitX, int exitY,
        IReadOnlyList<(int X, int Y)> corridor, IReadOnlyList<(int X, int Y)> joint, IReadOnlyList<BanditCampWrite> writes)
    {
        Side = side;
        Length = length;
        CampOriginX = campOriginX;
        CampOriginY = campOriginY;
        ExitX = exitX;
        ExitY = exitY;
        Corridor = corridor;
        Joint = joint;
        Writes = writes;
    }

    internal static BanditCampPlan Failed(string reason) => new(reason);

    internal static BanditCampPlan Found(BanditCampSide side, int length, int campOriginX, int campOriginY, int exitX, int exitY,
        IReadOnlyList<(int X, int Y)> corridor, IReadOnlyList<(int X, int Y)> joint, IReadOnlyList<BanditCampWrite> writes)
        => new(side, length, campOriginX, campOriginY, exitX, exitY, corridor, joint, writes);

    // Null when the camp has a place; else BanditCampPlacement.NoSpace or NoExit.
    internal string Reason { get; }
    internal bool IsPlaced => Reason == null;
    // The camp's exit side: Right means the camp lies west of its anchor, Left east of it.
    internal BanditCampSide Side { get; }
    // The corridor's strip in columns, without the joint.
    internal int Length { get; }
    // The map cell of the camp's own cell (0, 0), its bottom left.
    internal int CampOriginX { get; }
    internal int CampOriginY { get; }
    // The map cell of the camp's exit, where the corridor begins.
    internal int ExitX { get; }
    internal int ExitY { get; }
    // The strip of ground cells between the camp and the anchor's wall, two rows high.
    internal IReadOnlyList<(int X, int Y)> Corridor { get; }
    // The anchor's wall cells the corridor cuts through (RoofRing columns, two rows).
    internal IReadOnlyList<(int X, int Y)> Joint { get; }
    // Every cell to write, ready to copy: the framed camp and corridor, then the joint cells as ground.
    internal IReadOnlyList<BanditCampWrite> Writes { get; }
}

// The corridor from the camp to its anchor room and the search for a place on a map of void and not void cells, as pure rules.
// The corridor is a straight strip of ground, CorridorHeight rows high, from the camp's edge to the wall of the anchor room at the
// exit row, MinLength to MaxLength columns long; the camp and the strip are framed together (BanditCampLayout.Frame) and the
// anchor's wall cells at the joint become ground. Normal camp: exit right, the camp lies west of the anchor. A mirrored camp
// (BanditCampMirror) has its exit left and lies east of it.
//
// The exit is one cell high in the room file, the corridor two. The corridor begins two rows high one cell outside the camp,
// on the exit row and the row above it, and the camp's own wall stays as drawn: the wall face beside the corridor's upper row
// is the camp's wall over its exit row (when the camp's edge cell above the exit is void).
internal static class BanditCampPlacement
{
    // The longest corridor 40 columns, the camp's rule in use (BanditCampBuildRules.InUse; no place
    // with at most 12, the mirrored camp's test corridor was 18).
    internal const int CorridorHeight = 2, MinLength = 4, MaxLength = 40;

    internal const string NoSpace = "no-space";
    internal const string NoExit = "no-exit";

    // The cells of one corridor length, relative to the bottom left cell of camp and strip together (x to the right, y up from the
    // camp's bottom row): the footprint to keep void (From..To, without the joint columns) and what is written.
    private sealed class Shape
    {
        internal int Length;
        internal int FromX, ToX, FromY, ToY;
        // The camp's cell (0, 0) within the union.
        internal int CampX;
        internal readonly List<BanditCampWrite> Writes = new();
        internal readonly List<BanditCampWrite> JointWrites = new();
        internal readonly List<(int X, int Y)> Strip = new();
        internal readonly List<(int X, int Y)> Joint = new();
    }

    // Camp and corridor beside an anchor room. isVoid[x, y] is true for a map cell that is empty; anything outside the map is
    // taken. Tries the rows of the anchor from its middle outwards and the lengths from the shortest; the first place wins.
    // Only void cells are written, plus the joint cells of the anchor's wall.
    // wholeRect: the stricter first rule, the whole framed rectangle void, kept for the seed scan's comparison (no place for either
    // anchor). The rule in use asks only the cells that are written: camp, corridor and their frame.
    // accept: a further check of a found place (BanditCampBuildRules.JointOpensOnFloor); a place it refuses is passed over and the
    // search goes on with the next row and length (one refused place lost the whole anchor).
    internal static BanditCampPlan Place(BanditCampRoom room, BanditCampAnchor anchor, bool[,] isVoid, bool wholeRect = false, int maxLength = MaxLength,
        Func<BanditCampPlan, bool> accept = null)
    {
        var exit = BanditCampLayout.ExitOf(room);
        if (exit == null || exit.Value.Row + CorridorHeight > room.Height) return BanditCampPlan.Failed(NoExit);
        var shapes = ShapesOf(room, exit.Value, maxLength);
        foreach (var bottom in RowsFromMiddle(anchor))
            for (var length = MinLength; length <= maxLength; length++)
            {
                var plan = TryPlace(room, exit.Value, shapes[length], anchor, bottom, isVoid, wholeRect);
                if (plan != null && (accept == null || accept(plan))) return plan;
            }
        return BanditCampPlan.Failed(NoSpace);
    }

    // The bottom rows of a corridor inside the anchor's floor box, the middle ones first, the lower of two equal ones first.
    internal static List<int> RowsFromMiddle(BanditCampAnchor anchor)
    {
        var rows = new List<int>();
        for (var y = anchor.MinY; y + CorridorHeight - 1 <= anchor.MaxY; y++) rows.Add(y);
        var middle = anchor.MinY + anchor.MaxY;
        rows.Sort((a, b) => DistanceKey(a, middle).CompareTo(DistanceKey(b, middle)));
        return rows;
    }

    // Twice the distance of the corridor's middle (2 * bottom + CorridorHeight - 1) from the anchor's, a bit more above it.
    private static int DistanceKey(int bottom, int anchorMiddle)
    {
        var offset = 2 * bottom + CorridorHeight - 1 - anchorMiddle;
        return Math.Abs(offset) * 2 + (offset > 0 ? 1 : 0);
    }

    private static Shape[] ShapesOf(BanditCampRoom room, BanditCampExit exit, int maxLength)
    {
        var shapes = new Shape[maxLength + 1];
        for (var length = MinLength; length <= maxLength; length++) shapes[length] = ShapeOf(room, exit, length);
        return shapes;
    }

    private static Shape ShapeOf(BanditCampRoom room, BanditCampExit exit, int length)
    {
        var right = exit.Side == BanditCampSide.Right;
        var unionWidth = room.Width + length;
        var shape = new Shape
        {
            Length = length,
            CampX = right ? 0 : length,
            FromX = right ? -BanditCampLayout.RoofRing : 0,
            ToX = right ? unionWidth - 1 : unionWidth + BanditCampLayout.RoofRing - 1,
            FromY = -BanditCampLayout.RoofRing,
            ToY = room.Height + BanditCampLayout.SideRows + BanditCampLayout.RoofRing - 1,
        };
        AddFramedWrites(shape, BanditCampLayout.Frame(UnionWithCorridor(room, exit, length, shape.CampX)));
        AddStripAndJoint(shape, exit, right ? room.Width : 0, right ? unionWidth : -BanditCampLayout.RoofRing, length);
        return shape;
    }

    // The camp's cells with the corridor's ground (grass, like the camp) beside its exit.
    private static BanditCampCell[,] UnionWithCorridor(BanditCampRoom room, BanditCampExit exit, int length, int campX)
    {
        var union = new BanditCampCell[room.Width + length, room.Height];
        for (var x = 0; x < room.Width; x++)
            for (var y = 0; y < room.Height; y++)
                union[campX + x, y] = room.Cells[x, y];
        BanditRoomFeatureRules.GroundUnderBridges(union, room.Features, campX);
        var stripX = exit.Side == BanditCampSide.Right ? room.Width : 0;
        for (var i = 0; i < length; i++)
            for (var row = 0; row < CorridorHeight; row++)
                union[stripX + i, exit.Row + row] = BanditCampCell.Grass;
        return union;
    }

    private static void AddFramedWrites(Shape shape, BanditCampFramed framed)
    {
        for (var x = shape.FromX; x <= shape.ToX; x++)
            for (var y = shape.FromY; y <= shape.ToY; y++)
            {
                var fx = x + framed.OffsetX;
                var fy = y + framed.OffsetY;
                if (framed.Grid[fx, fy] == BanditCampLayout.GridVoid) continue;
                shape.Writes.Add(new BanditCampWrite(x, y, new BanditCampTerrain(framed.Grid[fx, fy], framed.Low[fx, fy], framed.High[fx, fy])));
            }
    }

    private static void AddStripAndJoint(Shape shape, BanditCampExit exit, int stripX, int jointX, int length)
    {
        var ground = BanditCampLayout.TerrainOf(BanditCampCell.Grass);
        for (var row = 0; row < CorridorHeight; row++)
        {
            for (var i = 0; i < length; i++) shape.Strip.Add((stripX + i, exit.Row + row));
            for (var i = 0; i < BanditCampLayout.RoofRing; i++)
            {
                shape.Joint.Add((jointX + i, exit.Row + row));
                shape.JointWrites.Add(new BanditCampWrite(jointX + i, exit.Row + row, ground));
            }
        }
    }

    // The place for one length and one corridor row, or null. The cells written for camp, corridor and frame (or, wholeRect, the
    // camp's whole rectangle with its frame), without the joint columns, must be void; the joint cells only have to lie on the map.
    private static BanditCampPlan TryPlace(BanditCampRoom room, BanditCampExit exit, Shape shape, BanditCampAnchor anchor, int bottom, bool[,] isVoid,
        bool wholeRect)
    {
        var length = shape.Length;
        var right = exit.Side == BanditCampSide.Right;
        var campX = right ? anchor.MinX - BanditCampLayout.RoofRing - length - room.Width : anchor.MaxX + BanditCampLayout.RoofRing + 1 + length;
        var originX = campX - shape.CampX;
        var originY = bottom - exit.Row;
        var free = wholeRect
            ? RectIsVoid(isVoid, originX + shape.FromX, originY + shape.FromY, originX + shape.ToX, originY + shape.ToY)
            : WritesAreVoid(isVoid, shape.Writes, originX, originY);
        if (!free) return null;
        var joint = Moved(shape.Joint, originX, originY);
        if (!OnMap(isVoid, joint)) return null;
        var exitX = campX + (right ? room.Width - 1 : 0);
        return BanditCampPlan.Found(exit.Side, length, campX, originY, exitX, originY + exit.Row,
            Moved(shape.Strip, originX, originY), joint, MovedWrites(shape, originX, originY));
    }

    private static bool RectIsVoid(bool[,] isVoid, int fromX, int fromY, int toX, int toY)
    {
        if (fromX < 0 || fromY < 0 || toX >= isVoid.GetLength(0) || toY >= isVoid.GetLength(1)) return false;
        for (var x = fromX; x <= toX; x++)
            for (var y = fromY; y <= toY; y++)
                if (!isVoid[x, y]) return false;
        return true;
    }

    private static bool WritesAreVoid(bool[,] isVoid, List<BanditCampWrite> writes, int dx, int dy)
    {
        int width = isVoid.GetLength(0), height = isVoid.GetLength(1);
        foreach (var write in writes)
        {
            int x = write.X + dx, y = write.Y + dy;
            if (x < 0 || y < 0 || x >= width || y >= height || !isVoid[x, y]) return false;
        }
        return true;
    }

    private static bool OnMap(bool[,] isVoid, List<(int X, int Y)> cells)
    {
        foreach (var (x, y) in cells)
            if (x < 0 || y < 0 || x >= isVoid.GetLength(0) || y >= isVoid.GetLength(1)) return false;
        return true;
    }

    private static List<(int X, int Y)> Moved(List<(int X, int Y)> cells, int dx, int dy)
    {
        var moved = new List<(int X, int Y)>(cells.Count);
        foreach (var (x, y) in cells) moved.Add((x + dx, y + dy));
        return moved;
    }

    private static List<BanditCampWrite> MovedWrites(Shape shape, int dx, int dy)
    {
        var writes = new List<BanditCampWrite>(shape.Writes.Count + shape.JointWrites.Count);
        foreach (var write in shape.Writes) writes.Add(new BanditCampWrite(write.X + dx, write.Y + dy, write.Terrain));
        foreach (var write in shape.JointWrites) writes.Add(new BanditCampWrite(write.X + dx, write.Y + dy, write.Terrain));
        return writes;
    }
}
