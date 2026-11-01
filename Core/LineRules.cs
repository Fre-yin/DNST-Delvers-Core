namespace DungeonSettlersDelvers.Core;

// Lines on the grid, pure and tested offline:
// - the game's own line of SKILL_ClingingVines: CombatUtility.SnapTo8Direction
//   (atan2 in degrees, divided by 45, rounded half to even, modulo 8; table counter-clockwise from (1,0)) and
//   CombatUtility.GetTilesInDirection (the caster's own cell, then up to range steps, ending before the first
//   blocked cell; units do not stop it);
// - the real-angle line of Frieren's Zoltraak, a copy of ZoltraakLineRules (Tiles, Trace). Core may not depend on
//   Frieren; once Core offers this block publicly, Frieren can switch to it.
internal static class CoreLineRules
{
    private static readonly (int X, int Y)[] Eight = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

    // The game's 8 directions in the order of SnapTo8Direction's table.
    internal static IReadOnlyList<(int X, int Y)> Directions => Eight;

    // SnapTo8Direction for an offset; without one (0, 0) the game takes (1, 0), and so does the vines'
    // routine for a target on the caster's own cell. Core gave (0, 0) and no line.
    internal static (int X, int Y) Snap8(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return Eight[0];
        var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;
        return Eight[(int)Math.Round(angle / 45.0, MidpointRounding.ToEven) & 7];
    }

    // GetTilesInDirection from a cell in a direction already snapped: the cell itself, then up to range steps;
    // the line ends before the first blocked cell.
    internal static List<(int X, int Y)> DirectionTiles(int fromX, int fromY, int dirX, int dirY, int range, Func<int, int, bool> isBlocked)
    {
        var tiles = new List<(int X, int Y)>();
        if ((dirX == 0 && dirY == 0) || range < 0) return tiles;
        var x = fromX;
        var y = fromY;
        for (var step = 0; step <= range; step++)
        {
            tiles.Add((x, y));
            x += dirX;
            y += dirY;
            if (isBlocked != null && isBlocked(x, y)) break;
        }
        return tiles;
    }

    // The game's whole vines line towards a target cell (snapped, then GetTilesInDirection).
    internal static List<(int X, int Y)> GameLine(int fromX, int fromY, int toX, int toY, int range, Func<int, int, bool> isBlocked)
    {
        var (dx, dy) = Snap8(toX - fromX, toY - fromY);
        return DirectionTiles(fromX, fromY, dx, dy, range, isBlocked);
    }

    // ---- the real-angle line (copy of Frieren's ZoltraakLineRules) ----

    internal static IReadOnlyList<(int X, int Y)> RealAngleTiles(int fromX, int fromY, int toX, int toY, int radius)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var major = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (major == 0 || radius <= 0) return null;
        var tiles = new (int X, int Y)[radius];
        for (var step = 1; step <= radius; step++)
            tiles[step - 1] = (
                fromX + (int)Math.Round((double)dx * step / major, MidpointRounding.AwayFromZero),
                fromY + (int)Math.Round((double)dy * step / major, MidpointRounding.AwayFromZero));
        return tiles;
    }

    // The real-angle line up to the first blocked cell (Blocked) or the first target (Target); a blocked cell that
    // passes counts as open and is listed in Passed. Null without a direction or range.
    internal static LineTrace Trace(int fromX, int fromY, int toX, int toY, int radius, Func<int, int, bool> isBlocked,
        Func<int, int, bool> holdsTarget = null, Func<int, int, bool> passes = null)
    {
        var tiles = RealAngleTiles(fromX, fromY, toX, toY, radius);
        if (tiles == null) return null;
        var open = new List<(int X, int Y)>(tiles.Count);
        var passed = new List<(int X, int Y)>();
        foreach (var tile in tiles)
        {
            if (isBlocked(tile.X, tile.Y))
            {
                if (passes == null || !passes(tile.X, tile.Y)) return new LineTrace(open, tile, null, passed);
                passed.Add(tile);
            }
            open.Add(tile);
            if (holdsTarget != null && holdsTarget(tile.X, tile.Y)) return new LineTrace(open, null, tile, passed);
        }
        return new LineTrace(open, null, null, passed);
    }
}

internal sealed class LineTrace
{
    internal LineTrace(IReadOnlyList<(int X, int Y)> open, (int X, int Y)? blocked, (int X, int Y)? target,
        IReadOnlyList<(int X, int Y)> passed = null)
    {
        Open = open;
        Blocked = blocked;
        Target = target;
        Passed = passed ?? Array.Empty<(int X, int Y)>();
    }

    internal IReadOnlyList<(int X, int Y)> Open { get; }
    internal (int X, int Y)? Blocked { get; }
    internal (int X, int Y)? Target { get; }
    internal IReadOnlyList<(int X, int Y)> Passed { get; }
}
