namespace DungeonSettlersDelvers.Core;

// The three raw values the floor's pre-data carries for one cell: grid flag, low and high decoration.
internal readonly struct BanditCampTerrain
{
    internal BanditCampTerrain(int grid, int low, int high)
    {
        Grid = grid;
        Low = low;
        High = high;
    }

    internal int Grid { get; }
    internal int Low { get; }
    internal int High { get; }
}

// A framed grid: the three raw layers [x, y] from the bottom left, and where the unframed cell (0, 0) sits in them.
internal sealed class BanditCampFramed
{
    internal BanditCampFramed(int width, int height, int offsetX, int offsetY)
    {
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Grid = new int[width, height];
        Low = new int[width, height];
        High = new int[width, height];
    }

    internal int Width { get; }
    internal int Height { get; }
    internal int OffsetX { get; }
    internal int OffsetY { get; }
    internal int[,] Grid { get; }
    internal int[,] Low { get; }
    internal int[,] High { get; }
}

internal enum BanditCampSide { Left, Right }

// Where the camp is left: the side of the room's edge and the row of the party start on it.
internal readonly struct BanditCampExit
{
    internal BanditCampExit(BanditCampSide side, int row)
    {
        Side = side;
        Row = row;
    }

    internal BanditCampSide Side { get; }
    internal int Row { get; }
}

// The camp's terrain and walls as pure rules on the plain model (BanditCampModel): the raw values of a cell kind, the frame the
// game's own rooms carry (the rule follows the room editor's frame rule), and the exit at the party start. The corridor, the
// search for a place and the mirrored camp are BanditCampPlacement and BanditCampMirror.
internal static class BanditCampLayout
{
    // GridFlag values: open ground, the void around the rooms, the roof of a wall and a wall's face.
    internal const int GridGround = 0, GridVoid = 19, GridRoof = 35, GridSide = 69;

    // DecorationType values: grass on the low layer, high grass and water on the high layer.
    internal const int LowGrass = 2, HighGrassDecoration = 6, HighWater = 9;

    // Rows of wall face above every open cell, and the width of the roof ring around floor and face.
    internal const int SideRows = 2, RoofRing = 2;

    // One table for the cell kinds, in the order of the enum. Water carries Ground here; the game blocks it when it builds the map.
    // The void and the walls carry no decoration.
    private static readonly BanditCampTerrain[] Terrain =
    {
        new(GridVoid, 0, 0),
        new(GridGround, 0, 0),
        new(GridGround, LowGrass, 0),
        new(GridGround, LowGrass, HighGrassDecoration),
        new(GridGround, 0, HighWater),
    };

    internal static BanditCampTerrain TerrainOf(BanditCampCell cell) => Terrain[(int)cell];

    internal static bool IsOpen(BanditCampCell cell) => cell != BanditCampCell.Void;

    // The cells with their walls: SideRows of wall face above every open cell where the cell above is empty, then a roof ring of
    // RoofRing cells around floor and face; farther away stays void. The result is (Width + 2 * RoofRing) x
    // (Height + 2 * RoofRing + SideRows), the unframed cell (0, 0) at (RoofRing, RoofRing). Any shape of open cells gets its walls.
    internal static BanditCampFramed Frame(BanditCampCell[,] cells)
    {
        int width = cells.GetLength(0), height = cells.GetLength(1);
        var framed = new BanditCampFramed(width + 2 * RoofRing, height + 2 * RoofRing + SideRows, RoofRing, RoofRing);
        var open = new bool[framed.Width, framed.Height];
        FillCells(framed, open, cells);
        var face = FaceMask(open, framed.Width, framed.Height);
        FillWalls(framed, open, face);
        return framed;
    }

    private static void FillCells(BanditCampFramed framed, bool[,] open, BanditCampCell[,] cells)
    {
        for (var x = 0; x < framed.Width; x++)
            for (var y = 0; y < framed.Height; y++)
                framed.Grid[x, y] = GridVoid;
        for (var x = 0; x < cells.GetLength(0); x++)
            for (var y = 0; y < cells.GetLength(1); y++)
            {
                var terrain = TerrainOf(cells[x, y]);
                framed.Grid[x + RoofRing, y + RoofRing] = terrain.Grid;
                framed.Low[x + RoofRing, y + RoofRing] = terrain.Low;
                framed.High[x + RoofRing, y + RoofRing] = terrain.High;
                open[x + RoofRing, y + RoofRing] = IsOpen(cells[x, y]);
            }
    }

    private static bool[,] FaceMask(bool[,] open, int width, int height)
    {
        var face = new bool[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 1; y < height; y++)
                face[x, y] = IsFace(open, x, y);
        return face;
    }

    // A closed cell is wall face when an open cell lies below it with at most SideRows - 1 closed cells in between.
    private static bool IsFace(bool[,] open, int x, int y)
    {
        if (open[x, y]) return false;
        var below = 0;
        while (below < SideRows && y - below - 1 >= 0 && !open[x, y - below - 1]) below++;
        return below < SideRows && y - below - 1 >= 0 && open[x, y - below - 1];
    }

    private static void FillWalls(BanditCampFramed framed, bool[,] open, bool[,] face)
    {
        for (var x = 0; x < framed.Width; x++)
            for (var y = 0; y < framed.Height; y++)
            {
                if (face[x, y]) framed.Grid[x, y] = GridSide;
                else if (!open[x, y] && NearOpenOrFace(open, face, framed.Width, framed.Height, x, y)) framed.Grid[x, y] = GridRoof;
            }
    }

    private static bool NearOpenOrFace(bool[,] open, bool[,] face, int width, int height, int x, int y)
    {
        for (var dx = -RoofRing; dx <= RoofRing; dx++)
            for (var dy = -RoofRing; dy <= RoofRing; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && (open[nx, ny] || face[nx, ny])) return true;
            }
        return false;
    }

    // The camp's exit: the party start when it lies on the left or the right edge of an open cell. The exit is never on the top or
    // the bottom edge (a waterfall hangs there, and an opening would have to cut the wall face and the roof); null without one.
    internal static BanditCampExit? ExitOf(BanditCampRoom room)
    {
        int x = room.PartyStartX, y = room.PartyStartY;
        if (x < 0 || y < 0 || x >= room.Width || y >= room.Height || !IsOpen(room.Cells[x, y])) return null;
        if (x == room.Width - 1) return new BanditCampExit(BanditCampSide.Right, y);
        if (x == 0) return new BanditCampExit(BanditCampSide.Left, y);
        return null;
    }

}
