namespace DungeonSettlersDelvers.Core;

// The bandit room's own features, drawn by Core over the game's tilemaps: a waterfall where a stream
// leaves the wall, and a wooden bridge where a path crosses a stream. Both are Delvers' own images (Core/Assets), not game
// tiles; Core draws them at run time and writes nothing into the game's part of a save. Pure rules without the game: where a
// feature fits, which frame shows, how the parts sort. The cells come from the room's terrain; tested offline.
internal static class BanditRoomFeatureRules
{
    // The terrain of one cell as the rules see it.
    internal enum Cell { Other, Ground, Water, Wall }

    // GridFlag bits: Ground 0, OuterWallSide 69 (its bits all set on a wall side cell).
    internal const int FlagOuterWallSide = 69;

    // DecorationType of the map's high decoration (RawMapData.GetHighDecoration, TileKey 1009 =
    // DecorationType + 1000): water 9; poison water 10 and high grass 6 are no stream.
    internal const int HighDecorationWater = 9;

    // A cell from its grid flag and its high decoration: water by the decoration, wall by the wall side bits,
    // ground when no flag is set.
    internal static Cell CellOf(int flag, int highDecoration)
    {
        if (highDecoration == HighDecorationWater) return Cell.Water;
        if ((flag & FlagOuterWallSide) == FlagOuterWallSide) return Cell.Wall;
        return flag == 0 ? Cell.Ground : Cell.Other;
    }

    internal const string WaterfallSheet = "Wasserfall_Sheet_384x80.png";
    internal const string BridgeUnderFile = "Bruecke_unter_Figuren_64x32.png";
    internal const string BridgeOverFile = "Bruecke_ueber_Figuren_64x32.png";

    // The waterfall: 8 frames of 48x80 side by side, 100 ms each, in a loop; one frame covers 3 x 5 cells of 16 px.
    internal const int WaterfallFrames = 8;
    internal const int WaterfallFrameWidth = 48;
    internal const int WaterfallFrameHeight = 80;
    internal const int WaterfallFrameMs = 100;
    internal const int WaterfallCells = 3;
    // Of the waterfall's 5 rows, 4 lie above its water row: 2 of wall side, 2 of roof.
    internal const int WaterfallRowsAbove = 4;

    // The view's log lines are capped per shown floor (its seed; 0 off the dungeon), so every camp logs its parts again (one cap
    // for the whole session left the third camp without a bridge line). count and scope carry over
    // between calls; true when the line may go out.
    internal static bool LogLineAllowed(ref int count, ref int scope, int seed, int max)
    {
        if (seed != scope)
        {
            scope = seed;
            count = 0;
        }
        return count++ < max;
    }

    // The bridge: 64x32 over a row of four cells, bank, water, water, bank.
    internal const int BridgeWidth = 64;
    internal const int BridgeHeight = 32;
    internal const int BridgeCells = 4;

    // The two water cells under a bridge whose bank cell is at (x, y).
    internal static (int X, int Y)[] BridgeWaterCells(int x, int y) => new[] { (x + 1, y), (x + 2, y) };

    // The cells under a camp's bridge are written as dirt when the floor is built, so the game itself counts
    // them walkable, after every load and in the save; Core only paints the water back on them (BanditRoomFeatureView). The
    // camp's cells with that ground, in place; offsetX is where the room's cell (0, 0) lies in the array.
    internal static void GroundUnderBridges(BanditCampCell[,] cells, IEnumerable<BanditCampFeature> features, int offsetX)
    {
        foreach (var feature in features)
        {
            if (feature.Kind != BridgeKind) continue;
            foreach (var (x, y) in BridgeWaterCells(feature.X, feature.Y))
            {
                var cx = offsetX + x;
                if (cx < 0 || y < 0 || cx >= cells.GetLength(0) || y >= cells.GetLength(1)) continue;
                if (cells[cx, y] == BanditCampCell.Water) cells[cx, y] = BanditCampCell.Dirt;
            }
        }
    }

    // The row (relative to the anchor cell's row) where the image's lowest row lies: the waterfall's image ends one row below
    // its rows above, the bridge's 2-row image is centred on its path row (rows 8 to 23 of 32 px lie on it).
    internal static float LowestRow(int rowsAbove, int imageRows) => rowsAbove > 0 ? rowsAbove + 1 - imageRows : (1 - imageRows) / 2f;

    // The game's tilemaps by a part of their object name: the ground for positions, the decorations for the sorting.
    internal const string GroundTilemapName = "Ground";

    // The tilemaps whose tile on a water cell makes its look (ground and decorations); walls, roof, creep and markers stay.
    internal static bool PaintsWater(string tilemapName)
        => tilemapName != null && (tilemapName.IndexOf(GroundTilemapName, StringComparison.OrdinalIgnoreCase) >= 0
            || tilemapName.IndexOf("Decoration", StringComparison.OrdinalIgnoreCase) >= 0);

    internal static string SortingTilemapName(Layer layer) => layer == Layer.BelowUnits ? "HighDecoration" : "OverDecoration";

    // One tilemap renderer of the game as the sorting sees it (the run log CORE_BANDIT_ROOM_TILEMAPS shows the
    // layers: OuterWall for the wall side, OuterWallRoof, OverDecoration, Decoration, Default, UI and UI_UnderObject).
    internal readonly struct TileLayer
    {
        internal readonly string LayerName;
        internal readonly int LayerValue;
        internal readonly int LayerId;
        internal readonly int Order;

        internal TileLayer(string layerName, int layerValue, int layerId, int order)
        {
            LayerName = layerName;
            LayerValue = layerValue;
            LayerId = layerId;
            Order = order;
        }
    }

    // The waterfall lies over every tile layer of the map, wall side, roof and over decoration included: one order above
    // the topmost renderer by layer value, then order. The game's UI layers (markers, indicators) stay above it.
    internal static (int LayerId, int Order)? AboveAllTiles(IEnumerable<TileLayer> layers)
    {
        TileLayer? top = null;
        foreach (var layer in layers)
        {
            if (layer.LayerName != null && layer.LayerName.StartsWith("UI", StringComparison.Ordinal)) continue;
            if (top == null || layer.LayerValue > top.Value.LayerValue
                || (layer.LayerValue == top.Value.LayerValue && layer.Order > top.Value.Order)) top = layer;
        }
        return top == null ? null : (top.Value.LayerId, top.Value.Order + 1);
    }

    // One pixel of the bridge's front rail laid over its deck for the tool's list icon: the usual "over" compositing with
    // straight alpha, channels 0 to 255.
    internal static (byte R, byte G, byte B, byte A) Over((byte R, byte G, byte B, byte A) top, (byte R, byte G, byte B, byte A) below)
    {
        var ta = top.A / 255f;
        var ba = below.A / 255f;
        var a = ta + ba * (1f - ta);
        if (a <= 0f) return (0, 0, 0, 0);
        byte Mix(byte t, byte b) => (byte)Math.Round((t * ta + b * ba * (1f - ta)) / a);
        return (Mix(top.R, below.R), Mix(top.G, below.G), Mix(top.B, below.B), (byte)Math.Round(a * 255f));
    }

    // A sorting order when the game's tilemap renderer cannot be read; above everything the game draws at 0.
    internal static int FallbackOrder(Layer layer) => layer switch { Layer.BelowUnits => 1, Layer.AboveUnits => 1000, _ => 1001 };

    // The frame shown after the given milliseconds of play, in a loop.
    internal static int WaterfallFrameAt(long milliseconds)
        => (int)(Math.Max(0, milliseconds) / WaterfallFrameMs % WaterfallFrames);

    // The pixel column of a frame in the sheet.
    internal static int WaterfallFrameX(int frame) => ((frame % WaterfallFrames) + WaterfallFrames) % WaterfallFrames * WaterfallFrameWidth;

    // A waterfall fits at (x, y) when x..x+2 of row y are water and the row above them is wall (the stream leaves the wall).
    // up: +1 or -1, the direction of "above" in the room's rows.
    internal static bool WaterfallFits(Func<int, int, Cell> cell, int x, int y, int up)
    {
        for (var i = 0; i < WaterfallCells; i++)
            if (cell(x + i, y) != Cell.Water || cell(x + i, y + up) != Cell.Wall) return false;
        return true;
    }

    // A bridge fits at (x, y) when the row reads bank, water, water, bank from x on.
    internal static bool BridgeFits(Func<int, int, Cell> cell, int x, int y)
        => cell(x, y) == Cell.Ground && cell(x + 1, y) == Cell.Water && cell(x + 2, y) == Cell.Water && cell(x + 3, y) == Cell.Ground;

    // ---- placed parts (set like any item; the terrain rules only check and suggest) ----

    internal const string WaterfallKind = "waterfall";
    internal const string BridgeKind = "bridge";
    internal static readonly string[] Kinds = { WaterfallKind, BridgeKind };

    internal const string Placed = "placed";
    internal const string PlacedOffTerrain = "placed:terrain";
    internal const string RefusedKind = "refused:kind";
    internal const string RefusedCampaign = "refused:campaign";
    internal const string RefusedOccupied = "refused:occupied";

    // One placed part: its kind, the map and its anchor cell (waterfall: left water cell under the wall; bridge: left bank), and
    // the seed of the dungeon floor it belongs to (0: no floor, a part of a map that stays, or one placed before the seed).
    internal readonly struct Part
    {
        internal readonly string Kind;
        internal readonly int Map;
        internal readonly int X;
        internal readonly int Y;
        internal readonly int Seed;

        internal Part(string kind, int map, int x, int y, int seed = 0)
        {
            Kind = kind;
            Map = map;
            X = x;
            Y = y;
            Seed = seed;
        }

        // "kind x y w h": the anchor and the cells the part covers, for the tool's brush.
        internal string Line => Kind + " " + X + " " + Y + " " + WidthOf(Kind) + " " + HeightOf(Kind);
    }

    // The cells a part covers from its anchor: the waterfall 3 wide over its water row and the 4 rows above, the bridge 4 wide
    // on its path row.
    internal static int WidthOf(string kind) => kind == BridgeKind ? BridgeCells : WaterfallCells;

    internal static int HeightOf(string kind) => kind == BridgeKind ? 1 : WaterfallRowsAbove + 1;

    // A part belongs to the shown map and floor: same map, and its seed the floor's or none (the author's camp, every
    // floor of the dungeon is the same map, so a part of an earlier floor must not show on the next one).
    internal static bool Shown(Part part, int map, int seed) => part.Map == map && (part.Seed == 0 || part.Seed == seed);

    internal static bool Covers(Part part, int map, int x, int y, int seed = 0)
        => Shown(part, map, seed) && x >= part.X && x < part.X + WidthOf(part.Kind) && y >= part.Y && y < part.Y + HeightOf(part.Kind);

    internal static bool IsKind(string kind) => Array.IndexOf(Kinds, kind) >= 0;

    // Two parts on the same map overlap when their covered cells share one.
    internal static bool Overlaps(Part a, Part b)
        => a.Map == b.Map && (a.Seed == 0 || b.Seed == 0 || a.Seed == b.Seed) && a.X < b.X + WidthOf(b.Kind) && b.X < a.X + WidthOf(a.Kind)
            && a.Y < b.Y + HeightOf(b.Kind) && b.Y < a.Y + HeightOf(a.Kind);

    // Whether the terrain at the anchor fits the kind (only a check: a part is placed either way, with a warning).
    internal static bool TerrainFits(string kind, Func<int, int, Cell> cell, int x, int y, int up)
        => kind == BridgeKind ? BridgeFits(cell, x, y) : WaterfallFits(cell, x, y, up);

    // Adds a part to the list: refused for an unknown kind, without a campaign or where it overlaps a placed part.
    internal static string Place(List<Part> parts, bool campaign, string kind, int map, int x, int y, bool terrainFits, int seed = 0)
    {
        if (!IsKind(kind)) return RefusedKind;
        if (!campaign) return RefusedCampaign;
        var part = new Part(kind, map, x, y, seed);
        foreach (var other in parts)
            if (Overlaps(part, other)) return RefusedOccupied;
        parts.Add(part);
        return terrainFits ? Placed : PlacedOffTerrain;
    }

    // Removes the part that covers the cell on that map, whichever of its cells it is; false when none does.
    internal static bool Remove(List<Part> parts, int map, int x, int y, int seed = 0)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (!Covers(parts[i], map, x, y, seed)) continue;
            parts.RemoveAt(i);
            return true;
        }
        return false;
    }

    // The parts of that map that belong to another floor, removed when a floor's camp is furnished; how many went.
    internal static int Prune(List<Part> parts, int map, int seed)
        => parts.RemoveAll(part => part.Map == map && part.Seed != 0 && part.Seed != seed);


    // Sorting against the units on the map: the bridge's deck below them, its front rail above them; the waterfall above
    // every tile layer, wall and roof included (their grass edge reaches 4 px up).
    internal enum Layer { BelowUnits, AboveUnits, AboveTiles }
}
