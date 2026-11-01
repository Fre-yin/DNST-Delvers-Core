using DungeonSettlersDelvers.Core;
using static TestKit;

// The bandit camp's terrain, frame, exit, corridor, place on a map and mirror (BanditCampLayout, BanditCampPlacement,
// BanditCampMirror): rooms are built in code, a small stand in for the real camp has its brook, its bridge and its exit.
internal static class BanditCampLayoutSection
{
    const int W = 44, H = 20, Side = BanditCampLayout.GridSide, Roof = BanditCampLayout.GridRoof, Void = BanditCampLayout.GridVoid;

    internal static void Run()
    {
        TerrainTable();
        FrameOverOpenCells();
        FrameShapes();
        Exit();
        MirrorTwice();
        MirrorValues();
        MirrorProbe();
        FeaturesFitAfterMirror();
        FramedCampSize();
        RowOrder();
        PlacementNormal();
        PlacementMirrored();
        PlacementWrites();
        PlacementRejects();
        PlacementFallsBackToOtherRows();
        PlacementAsksOnlyWrittenCells();
    }

    // ---- fixtures ----

    // The camp as the room editor draws it: grass, a brook from the top at x 36..38, a bridge place at x 31..34 in row 10, a
    // tunnel in row 11 from x 40 to the edge at x 43 (the exit, the party's start), a few buildings, four spawns and the two parts.
    static BanditCampRoom Camp()
    {
        var cells = new BanditCampCell[W, H];
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                cells[x, y] = BanditCampCell.Grass;
        for (var x = 36; x <= 38; x++)
            for (var y = 15; y < H; y++)
                cells[x, y] = BanditCampCell.Water;
        cells[32, 10] = BanditCampCell.Water;
        cells[33, 10] = BanditCampCell.Water;
        for (var x = 40; x < W; x++)
            for (var y = 0; y < H; y++)
                if (y != 11) cells[x, y] = BanditCampCell.Void;
        cells[0, 0] = BanditCampCell.Dirt;
        cells[5, 5] = BanditCampCell.HighGrass;
        var buildings = new[]
        {
            new BanditCampBuilding("BED", 10, 5, 90, "Ally"), new BanditCampBuilding("BED", 12, 5, 270, "Ally"),
            new BanditCampBuilding("TREE", 3, 3, 0, "Neutral"), new BanditCampBuilding("CHAIR", 20, 8, 180, "Ally"),
            new BanditCampBuilding("TENT", 25, 14, 0, "Neutral"), new BanditCampBuilding("TORCH", 43, 11, 270, "Ally"),
        };
        var items = new[] { new BanditCampItem("COIN", 7, 7, 3) };
        var spawns = new[]
        {
            new BanditCampSpawn("Blade", "UNIT_A", 5, 16), new BanditCampSpawn("Bow", "UNIT_B", 3, 13),
            new BanditCampSpawn("Guard", "UNIT_C", 7, 12), new BanditCampSpawn("Healer", "UNIT_D", 4, 9),
        };
        var features = new[] { new BanditCampFeature(BanditRoomFeatureRules.WaterfallKind, 36, 19), new BanditCampFeature(BanditRoomFeatureRules.BridgeKind, 31, 10) };
        return new BanditCampRoom("test camp", cells, buildings, items, spawns, features, 43, 11);
    }

    // Footprints relative to the anchor: BED two cells, TENT three, lying at 0 and 180, standing at 90 and 270; others unknown.
    static IReadOnlyList<(int X, int Y)> Footprints(string key, int rotation)
    {
        var length = key switch { "BED" => 2, "TENT" => 3, _ => 0 };
        if (length == 0) return null;
        var standing = rotation == 90 || rotation == 270;
        return Enumerable.Range(0, length).Select(i => standing ? (0, i) : (i, 0)).ToList();
    }

    static BanditCampCell[,] Open(int width, int height)
    {
        var cells = new BanditCampCell[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                cells[x, y] = BanditCampCell.Grass;
        return cells;
    }

    static int Count(int[,] layer, int value)
    {
        var count = 0;
        foreach (var cell in layer)
            if (cell == value) count++;
        return count;
    }

    static Func<int, int, BanditRoomFeatureRules.Cell> CellsOf(BanditCampFramed framed)
        => (x, y) => BanditRoomFeatureRules.CellOf(framed.Grid[x + framed.OffsetX, y + framed.OffsetY], framed.High[x + framed.OffsetX, y + framed.OffsetY]);

    static bool SameRoom(BanditCampRoom a, BanditCampRoom b)
    {
        if (a.Name != b.Name || a.Width != b.Width || a.Height != b.Height || a.PartyStartX != b.PartyStartX || a.PartyStartY != b.PartyStartY) return false;
        foreach (var x in Enumerable.Range(0, a.Width))
            foreach (var y in Enumerable.Range(0, a.Height))
                if (a.Cells[x, y] != b.Cells[x, y]) return false;
        return a.Buildings.Select(v => (v.Key, v.X, v.Y, v.Rotation, v.Faction)).SequenceEqual(b.Buildings.Select(v => (v.Key, v.X, v.Y, v.Rotation, v.Faction)))
            && a.Items.Select(v => (v.Key, v.X, v.Y, v.Count)).SequenceEqual(b.Items.Select(v => (v.Key, v.X, v.Y, v.Count)))
            && a.Spawns.Select(v => (v.Name, v.Kind, v.X, v.Y)).SequenceEqual(b.Spawns.Select(v => (v.Name, v.Kind, v.X, v.Y)))
            && a.Features.Select(v => (v.Kind, v.X, v.Y)).SequenceEqual(b.Features.Select(v => (v.Kind, v.X, v.Y)));
    }

    static bool[,] VoidMap(int width, int height)
    {
        var map = new bool[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                map[x, y] = true;
        return map;
    }

    static void Occupy(bool[,] map, int fromX, int fromY, int toX, int toY)
    {
        for (var x = fromX; x <= toX; x++)
            for (var y = fromY; y <= toY; y++)
                map[x, y] = false;
    }

    // An anchor room with floor x 80..100, y 20..40 and its walls (two roof columns and rows, two face rows above) in a 120 x 60 map.
    static (bool[,] Map, BanditCampAnchor Anchor) EastAnchorMap()
    {
        var map = VoidMap(120, 60);
        Occupy(map, 78, 18, 102, 44);
        return (map, new BanditCampAnchor(80, 20, 100, 40));
    }

    // The same room with floor x 20..40, y 20..40: the camp can only lie east of it.
    static (bool[,] Map, BanditCampAnchor Anchor) WestAnchorMap()
    {
        var map = VoidMap(120, 60);
        Occupy(map, 18, 18, 42, 44);
        return (map, new BanditCampAnchor(20, 20, 40, 40));
    }

    static BanditCampWrite? WriteAt(BanditCampPlan plan, int x, int y)
    {
        foreach (var write in plan.Writes)
            if (write.X == x && write.Y == y) return write;
        return null;
    }

    // ---- terrain and frame ----

    static void TerrainTable()
    {
        bool Is(BanditCampCell cell, int grid, int low, int high)
        {
            var terrain = BanditCampLayout.TerrainOf(cell);
            return terrain.Grid == grid && terrain.Low == low && terrain.High == high;
        }
        Check(Is(BanditCampCell.Dirt, 0, 0, 0) && Is(BanditCampCell.Grass, 0, 2, 0) && Is(BanditCampCell.HighGrass, 0, 2, 6)
                && Is(BanditCampCell.Water, 0, 0, 9) && Is(BanditCampCell.Void, 19, 0, 0),
            "bandit camp: the cell kinds carry the grid flag and the decorations of the room editor's table");
        Check(!BanditCampLayout.IsOpen(BanditCampCell.Void) && BanditCampLayout.IsOpen(BanditCampCell.Water) && BanditCampLayout.IsOpen(BanditCampCell.Dirt),
            "bandit camp: everything but the void is open, water too (the game blocks it when it builds the map)");
    }

    static void FrameOverOpenCells()
    {
        var framed = BanditCampLayout.Frame(Open(3, 2));
        Check(framed.Width == 7 && framed.Height == 8 && framed.OffsetX == 2 && framed.OffsetY == 2,
            "bandit camp: the frame adds a roof ring of 2 around and 2 rows of face above, the cell (0, 0) sits at (2, 2)");
        var floor = true;
        var face = true;
        for (var x = 2; x <= 4; x++)
        {
            floor &= framed.Grid[x, 2] == 0 && framed.Grid[x, 3] == 0;
            face &= framed.Grid[x, 4] == Side && framed.Grid[x, 5] == Side;
        }
        Check(floor && face && Count(framed.Grid, 0) == 6 && Count(framed.Grid, Side) == 6,
            "bandit camp: two rows of wall face stand directly above every open cell, and nowhere else");
        Check(Count(framed.Grid, Roof) == 7 * 8 - 12 && Count(framed.Grid, Void) == 0
                && framed.Grid[0, 0] == Roof && framed.Grid[6, 7] == Roof && framed.Grid[1, 2] == Roof && framed.Grid[2, 6] == Roof,
            "bandit camp: the rest of the ring is roof, two cells wide around floor and face");
        Check(framed.Low[3, 2] == 2 && framed.High[3, 2] == 0 && framed.Low[3, 4] == 0 && framed.High[3, 4] == 0 && framed.Low[0, 0] == 0,
            "bandit camp: the floor keeps its decoration, walls carry none");
    }

    static void FrameShapes()
    {
        var cells = new BanditCampCell[1, 5];
        cells[0, 0] = BanditCampCell.Grass;
        cells[0, 2] = BanditCampCell.Grass;
        var framed = BanditCampLayout.Frame(cells);
        Check(framed.Grid[2, 2] == 0 && framed.Grid[2, 3] == Side && framed.Grid[2, 4] == 0 && framed.Grid[2, 5] == Side && framed.Grid[2, 6] == Side
                && framed.Grid[2, 7] == Roof,
            "bandit camp: a one cell gap between two floors gets its wall face");
        var near = new BanditCampCell[6, 1];
        near[0, 0] = BanditCampCell.Grass;
        near[5, 0] = BanditCampCell.Grass;
        var gap = BanditCampLayout.Frame(near);
        Check(gap.Grid[4, 2] == Roof && gap.Grid[5, 2] == Roof && gap.Grid[4, 3] == Roof, "bandit camp: the roof ring also closes the void between two floors");
        var wide = new BanditCampCell[12, 1];
        wide[0, 0] = BanditCampCell.Grass;
        wide[11, 0] = BanditCampCell.Grass;
        var apart = BanditCampLayout.Frame(wide);
        Check(apart.Grid[7, 2] == Void && apart.Grid[7, 0] == Void && apart.Grid[7, 6] == Void && apart.Grid[4, 2] == Roof,
            "bandit camp: void farther than the ring from any floor stays void");
        var water = new BanditCampCell[3, 1];
        for (var x = 0; x < 3; x++) water[x, 0] = BanditCampCell.Water;
        var pool = BanditCampLayout.Frame(water);
        Check(pool.Grid[3, 2] == 0 && pool.High[3, 2] == 9 && pool.Grid[3, 3] == Side && pool.Grid[3, 4] == Side,
            "bandit camp: water is open in the frame, so a stream under the wall has its face above it");
    }

    static void Exit()
    {
        var room = Camp();
        var exit = BanditCampLayout.ExitOf(room);
        Check(exit != null && exit.Value.Side == BanditCampSide.Right && exit.Value.Row == 11,
            "bandit camp: the party start on the right edge makes the exit right, in its row");
        var mirrored = BanditCampLayout.ExitOf(BanditCampMirror.Mirror(room));
        Check(mirrored != null && mirrored.Value.Side == BanditCampSide.Left && mirrored.Value.Row == 11,
            "bandit camp: the mirrored camp's exit changes the side and keeps the row");
        BanditCampRoom WithStart(int x, int y) => new("s", room.Cells, room.Buildings, room.Items, room.Spawns, room.Features, x, y);
        Check(BanditCampLayout.ExitOf(WithStart(20, 11)) == null && BanditCampLayout.ExitOf(WithStart(20, 0)) == null
                && BanditCampLayout.ExitOf(WithStart(43, 12)) == null && BanditCampLayout.ExitOf(WithStart(44, 11)) == null
                && BanditCampLayout.ExitOf(WithStart(0, 5)).Value.Side == BanditCampSide.Left,
            "bandit camp: no exit inside the room, on the top or bottom edge, on a void cell or outside; the left edge is the left exit");
    }

    // ---- mirror ----

    static void MirrorTwice()
    {
        var room = Camp();
        Check(SameRoom(BanditCampMirror.Mirror(BanditCampMirror.Mirror(room)), room),
            "bandit camp: mirroring twice gives the camp back (cells, spawns, parts, buildings, items, start)");
        Check(SameRoom(BanditCampMirror.Mirror(BanditCampMirror.Mirror(room, Footprints), Footprints), room),
            "bandit camp: mirroring twice with footprints gives the camp back too");
        Check(!SameRoom(BanditCampMirror.Mirror(room), room), "bandit camp: the mirrored camp is not the camp");
    }

    static void MirrorValues()
    {
        var room = Camp();
        var report = new BanditCampMirror.Report();
        var mirrored = BanditCampMirror.Mirror(room, Footprints, report);
        Check(mirrored.Width == W && mirrored.Height == H && mirrored.Cells[W - 1, 0] == BanditCampCell.Dirt && mirrored.Cells[0, 11] == BanditCampCell.Grass
                && mirrored.Cells[0, 10] == BanditCampCell.Void && mirrored.Cells[W - 1 - 5, 5] == BanditCampCell.HighGrass,
            "bandit camp: the cells of every row are reversed and no row moves");
        Check(mirrored.PartyStartX == 0 && mirrored.PartyStartY == 11
                && mirrored.Spawns[0].X == 38 && mirrored.Spawns[0].Y == 16 && mirrored.Spawns[3].X == 39 && mirrored.Spawns[0].Name == "Blade" && mirrored.Spawns[0].Kind == "UNIT_A"
                && mirrored.Items[0].X == 36 && mirrored.Items[0].Count == 3,
            "bandit camp: the start, the spawns and the items move to Width - 1 - x and keep their names and kinds");
        Check(mirrored.Buildings[0].X == 33 && mirrored.Buildings[0].Y == 5 && mirrored.Buildings[1].X == 31 && mirrored.Buildings[2].X == 44 - 3 - 1
                && mirrored.Buildings[4].X == 44 - 25 - 3 && mirrored.Buildings[5].X == 0 && mirrored.Buildings[2].Y == 3,
            "bandit camp: a building moves to the anchor that covers its mirrored cells, one without a footprint counts as one cell");
        Check(report.MultiCell == 3 && report.Unknown.Select(entry => entry.Key).SequenceEqual(new[] { "TREE", "CHAIR", "TORCH" }) && report.NoAnchor.Count == 0,
            "bandit camp: the report names the buildings without a footprint and counts the ones of more cells");
        Check(BanditCampMirror.Mirror(room).Buildings[0].X == 44 - 10 - 1, "bandit camp: without footprints every building is one cell");
        Check(mirrored.Name == "test camp gespiegelt" && BanditCampMirror.MirrorName(mirrored.Name) == "test camp",
            "bandit camp: the mirrored camp is named after the camp with the suffix, mirrored again its name comes back");
        Check(mirrored.Buildings[0].Rotation == 270 && mirrored.Buildings[1].Rotation == 90 && mirrored.Buildings[2].Rotation == 0
                && mirrored.Buildings[3].Rotation == 180 && mirrored.Buildings[0].Faction == "Ally" && mirrored.Buildings[2].Faction == "Neutral",
            "bandit camp: the rotations 90 and 270 swap, 0 and 180 stay, the faction stays");
        Check(mirrored.Features[0].X == 44 - 36 - BanditRoomFeatureRules.WaterfallCells && mirrored.Features[0].X == 5
                && mirrored.Features[1].X == 44 - 31 - BanditRoomFeatureRules.BridgeCells && mirrored.Features[1].X == 9
                && mirrored.Features[0].Kind == BanditRoomFeatureRules.WaterfallKind && mirrored.Features[1].Y == 10,
            "bandit camp: a part moves by its width, the waterfall from 36 to 5 and the bridge from 31 to 9");
        Check(BanditCampMirror.MirrorRotation(90) == 270 && BanditCampMirror.MirrorRotation(270) == 90 && BanditCampMirror.MirrorRotation(0) == 0
                && BanditCampMirror.MirrorRotation(180) == 180, "bandit camp: the rotation rule alone");
    }

    // The Modding Toolkit's probe of its room editor's mirror (Tests/Test-RoomMirror.ps1) with its numbers, so both rules give the
    // same: a 6 x 3 room with a wall, a tent lying and standing, an L without an anchor, an unknown key, a waterfall and a bridge.
    static IReadOnlyList<(int X, int Y)> ProbeFootprints(string key, int rotation) => key switch
    {
        "WALL" => new[] { (0, 0) },
        "TENT" => rotation == 0 || rotation == 180 ? new[] { (0, 0), (1, 0) } : new[] { (0, 0), (0, 1) },
        "L" => rotation == 0 ? new[] { (0, 0), (1, 0), (0, 1) } : new[] { (0, 0), (1, 0), (1, 1) },
        _ => null
    };

    static BanditCampRoom Probe(bool withOdd)
    {
        var rows = new[]
        {
            new[] { BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Dirt, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Water },
            Enumerable.Repeat(BanditCampCell.Grass, 6).ToArray(),
            new[] { BanditCampCell.HighGrass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass },
        };
        var cells = new BanditCampCell[6, 3];
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 6; x++)
                cells[x, y] = rows[y][x];
        var buildings = new List<BanditCampBuilding>
        {
            new("WALL", 0, 1, 0, "Ally"), new("TENT", 0, 2, 0, "Neutral"), new("TENT", 1, 0, 90, "Neutral"),
        };
        if (withOdd)
        {
            buildings.Add(new BanditCampBuilding("L", 3, 0, 0, "Neutral"));
            buildings.Add(new BanditCampBuilding("GHOST", 2, 1, 270, "Neutral"));
        }
        return new BanditCampRoom("Probe", cells, buildings, new[] { new BanditCampItem("ITEM_Stone", 1, 1, 3) },
            new[] { new BanditCampSpawn("Schlächter", "UNIT_Bandit", 0, 0) },
            new[] { new BanditCampFeature(BanditRoomFeatureRules.WaterfallKind, 1, 2), new BanditCampFeature(BanditRoomFeatureRules.BridgeKind, 2, 0) }, 5, 1);
    }

    static void MirrorProbe()
    {
        var report = new BanditCampMirror.Report();
        var mirrored = BanditCampMirror.Mirror(Probe(true), ProbeFootprints, report);
        var row0 = Enumerable.Range(0, 6).Select(x => mirrored.Cells[x, 0]);
        var row2 = Enumerable.Range(0, 6).Select(x => mirrored.Cells[x, 2]);
        Check(row0.SequenceEqual(new[] { BanditCampCell.Water, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Dirt, BanditCampCell.Grass, BanditCampCell.Grass })
                && row2.SequenceEqual(new[] { BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.Grass, BanditCampCell.HighGrass }),
            "mirror probe: the cells as the toolkit's probe has them");
        Check(mirrored.Spawns[0].X == 5 && mirrored.Spawns[0].Y == 0 && mirrored.PartyStartX == 0 && mirrored.PartyStartY == 1
                && mirrored.Items[0].X == 4 && mirrored.Items[0].Y == 1 && mirrored.Items[0].Count == 3
                && mirrored.Features[0].X == 2 && mirrored.Features[0].Y == 2 && mirrored.Features[1].X == 0 && mirrored.Features[1].Y == 0,
            "mirror probe: spawn (5,0), start (0,1), item (4,1) x3, waterfall (2,2), bridge (0,0)");
        var b = mirrored.Buildings.Select(v => (v.Key, v.X, v.Y, v.Rotation, v.Faction)).ToList();
        Check(b[0] == ("WALL", 5, 1, 0, "Ally") && b[1] == ("TENT", 4, 2, 0, "Neutral") && b[2] == ("TENT", 4, 0, 270, "Neutral")
                && b[3] == ("L", 3, 0, 0, "Neutral") && b[4] == ("GHOST", 3, 1, 90, "Neutral"),
            "mirror probe: wall (5,1) 0, tents (4,2) 0 and (4,0) 270, the L stays at (3,0) 0, the unknown key at (3,1) 90");
        Check(report.Unknown.Count == 1 && report.Unknown[0].Key == "GHOST" && report.NoAnchor.Count == 1 && report.NoAnchor[0].Key == "L" && report.MultiCell == 2,
            "mirror probe: report unknown 1, without anchor 1, of more cells 2");
        var plain = Probe(false);
        var twice = BanditCampMirror.Mirror(BanditCampMirror.Mirror(plain, ProbeFootprints), ProbeFootprints);
        Check(mirrored.Name == "Probe gespiegelt" && SameRoom(twice, plain), "mirror probe: without the L and the unknown key twice mirrored is the probe, its name too");
    }

    static void FeaturesFitAfterMirror()
    {
        var room = Camp();
        var plain = CellsOf(BanditCampLayout.Frame(room.Cells));
        Check(BanditRoomFeatureRules.WaterfallFits(plain, 36, 19, +1) && BanditRoomFeatureRules.BridgeFits(plain, 31, 10),
            "bandit camp: the test camp's waterfall and bridge fit as drawn");
        var mirrored = BanditCampMirror.Mirror(room);
        var cells = CellsOf(BanditCampLayout.Frame(mirrored.Cells));
        var waterfall = mirrored.Features[0];
        var bridge = mirrored.Features[1];
        Check(BanditRoomFeatureRules.WaterfallFits(cells, waterfall.X, waterfall.Y, +1) && BanditRoomFeatureRules.BridgeFits(cells, bridge.X, bridge.Y),
            "bandit camp: the waterfall and the bridge still fit on the mirrored cells");
        Check(!BanditRoomFeatureRules.WaterfallFits(cells, 36, 19, +1) && !BanditRoomFeatureRules.BridgeFits(cells, 31, 10),
            "bandit camp: and not at their old places");
    }

    static void FramedCampSize()
    {
        var framed = BanditCampLayout.Frame(Camp().Cells);
        Check(framed.Width == 48 && framed.Height == 26, "bandit camp: the 44 x 20 camp with its frame is 48 x 26");
        Check(framed.Grid[2 + 36, 2 + 19 + 1] == Side && framed.Grid[2 + 36, 2 + 19 + 2] == Side && framed.Grid[2 + 36, 2 + 19 + 3] == Roof,
            "bandit camp: the brook leaves two rows of face and then roof at the top");
        Check(framed.Grid[2 + 43, 2 + 12] == Side && framed.Grid[2 + 44, 2 + 11] == Roof && framed.Grid[2 + 43, 2 + 11] == 0,
            "bandit camp: the exit cell is open at the edge, the cells beside it are wall");
    }

    // ---- placement ----

    static void RowOrder()
    {
        Check(BanditCampPlacement.RowsFromMiddle(new BanditCampAnchor(0, 0, 9, 5)).SequenceEqual(new[] { 2, 1, 3, 0, 4 })
                && BanditCampPlacement.RowsFromMiddle(new BanditCampAnchor(0, 0, 9, 3)).SequenceEqual(new[] { 1, 0, 2 })
                && BanditCampPlacement.RowsFromMiddle(new BanditCampAnchor(0, 4, 9, 4)).Count == 0,
            "bandit camp: the corridor rows are tried from the anchor's middle outwards, two rows must fit in the floor box");
    }

    // No place beside either anchor under the first rule. A cell taken in an empty corner of the framed
    // rectangle, far from the camp's open cells, blocks only the whole rectangle, not the cells that are written.
    static void PlacementAsksOnlyWrittenCells()
    {
        var (map, anchor) = EastAnchorMap();
        var plan = BanditCampPlacement.Place(Camp(), anchor, map);
        map[plan.CampOriginX + 43, plan.CampOriginY + 2] = false;
        var loose = BanditCampPlacement.Place(Camp(), anchor, map);
        var strict = BanditCampPlacement.Place(Camp(), anchor, map, wholeRect: true);
        Check(plan.IsPlaced && loose.IsPlaced && loose.CampOriginX == plan.CampOriginX && loose.CampOriginY == plan.CampOriginY && loose.Length == plan.Length
                && !(strict.IsPlaced && strict.CampOriginX == plan.CampOriginX && strict.CampOriginY == plan.CampOriginY && strict.Length == plan.Length),
            "bandit camp: a taken cell in an empty corner of the frame's rectangle blocks only the old whole-rectangle rule");
        var next = BanditCampPlacement.Place(Camp(), anchor, VoidMapLike(map), accept: found => found.ExitY != plan.ExitY || found.Length != plan.Length);
        Check(next.IsPlaced && (next.ExitY != plan.ExitY || next.Length != plan.Length)
                && BanditCampPlacement.Place(Camp(), anchor, map, accept: _ => false).Reason == BanditCampPlacement.NoSpace,
            "bandit camp: a place the further check refuses is passed over, the search goes on; none accepted is no-space");
        map[plan.CampOriginX + 20, plan.CampOriginY + 5] = false;
        var onFloor = BanditCampPlacement.Place(Camp(), anchor, map);
        Check(!(onFloor.IsPlaced && onFloor.CampOriginX == plan.CampOriginX && onFloor.CampOriginY == plan.CampOriginY && onFloor.Length == plan.Length),
            "bandit camp: a taken cell under the camp's floor still blocks that place");
    }

    static bool[,] VoidMapLike(bool[,] map) => (bool[,])map.Clone();

    static void PlacementNormal()
    {
        var (map, anchor) = EastAnchorMap();
        var plan = BanditCampPlacement.Place(Camp(), anchor, map);
        Check(plan.IsPlaced && plan.Reason == null && plan.Side == BanditCampSide.Right && plan.Length == BanditCampPlacement.MinLength
                && plan.CampOriginX == 80 - 2 - 4 - 44 && plan.CampOriginY == 29 - 11 && plan.ExitX == 73 && plan.ExitY == 29,
            "bandit camp: the camp is found west of an anchor on its east side, exit row on the anchor's middle, the shortest corridor");
        Check(plan.Corridor.Count == 8 && plan.Corridor.All(c => c.X >= 74 && c.X <= 77 && c.Y >= 29 && c.Y <= 30)
                && plan.Joint.Count == 4 && plan.Joint.All(c => c.X >= 78 && c.X <= 79 && c.Y >= 29 && c.Y <= 30),
            "bandit camp: the corridor is a strip two rows high from the camp's edge to the anchor's wall, the joint its two wall columns");
        Check(plan.Corridor.All(c => c.Y >= anchor.MinY && c.Y <= anchor.MaxY) && plan.Corridor.Any(c => c.X == plan.ExitX + 1 && c.Y == plan.ExitY),
            "bandit camp: the corridor lies in the anchor's rows and begins right outside the camp's exit cell");
        var again = BanditCampPlacement.Place(Camp(), anchor, map);
        Check(again.CampOriginX == plan.CampOriginX && again.CampOriginY == plan.CampOriginY && again.Length == plan.Length && again.Writes.Count == plan.Writes.Count,
            "bandit camp: the same map and anchor give the same place");
    }

    static void PlacementMirrored()
    {
        var (map, anchor) = WestAnchorMap();
        var mirrored = BanditCampMirror.Mirror(Camp());
        var plan = BanditCampPlacement.Place(mirrored, anchor, map);
        Check(plan.IsPlaced && plan.Side == BanditCampSide.Left && plan.CampOriginX == 40 + 3 + 4 && plan.CampOriginY == 18 && plan.ExitX == 47 && plan.ExitY == 29,
            "bandit camp: the mirrored camp is found east of an anchor on its west side");
        Check(plan.Corridor.All(c => c.X >= 43 && c.X <= 46) && plan.Joint.All(c => c.X >= 41 && c.X <= 42) && plan.Joint.Count == 4,
            "bandit camp: the strip runs west from the camp's edge and the joint is the anchor's east wall");
        var (eastMap, eastAnchor) = EastAnchorMap();
        Check(!BanditCampPlacement.Place(mirrored, eastAnchor, eastMap).IsPlaced && !BanditCampPlacement.Place(Camp(), anchor, map).IsPlaced,
            "bandit camp: the normal camp needs room west of its anchor, the mirrored one east of it");
    }

    static void PlacementWrites()
    {
        var (map, anchor) = EastAnchorMap();
        var plan = BanditCampPlacement.Place(Camp(), anchor, map);
        var joint = new HashSet<(int, int)>(plan.Joint);
        var onlyVoidOrJoint = true;
        var anchorWallOnly = true;
        foreach (var write in plan.Writes)
        {
            if (joint.Contains((write.X, write.Y)))
            {
                anchorWallOnly &= !map[write.X, write.Y] && (write.X == 78 || write.X == 79) && write.Terrain.Grid == 0 && write.Terrain.Low == 2;
                continue;
            }
            onlyVoidOrJoint &= map[write.X, write.Y] && write.Terrain.Grid != Void;
        }
        Check(onlyVoidOrJoint && anchorWallOnly && plan.Writes.Count(w => joint.Contains((w.X, w.Y))) == 4,
            "bandit camp: the corridor and the frame write only void cells, the joint cuts only four wall cells of the anchor and makes them ground");
        Check(plan.Writes.All(w => w.X < 78 || joint.Contains((w.X, w.Y))),
            "bandit camp: nothing of the anchor's wall is written beyond the joint");
        Check(WriteAt(plan, 74, 29).Value.Terrain.Grid == 0 && WriteAt(plan, 74, 29).Value.Terrain.Low == 2 && WriteAt(plan, 77, 30).Value.Terrain.Grid == 0
                && WriteAt(plan, 74, 31).Value.Terrain.Grid == Side && WriteAt(plan, 74, 32).Value.Terrain.Grid == Side
                && WriteAt(plan, 74, 33).Value.Terrain.Grid == Roof && WriteAt(plan, 74, 34).Value.Terrain.Grid == Roof
                && WriteAt(plan, 74, 28).Value.Terrain.Grid == Roof && WriteAt(plan, 74, 26) == null,
            "bandit camp: the corridor is framed with the camp, face above both rows and roof around");
        Check(WriteAt(plan, 73, 29).Value.Terrain.Grid == 0 && WriteAt(plan, 73, 30).Value.Terrain.Grid == Side && WriteAt(plan, 73, 28).Value.Terrain.Grid == Roof,
            "bandit camp: the camp's wall at its exit stays as drawn, the corridor begins two rows high one cell outside");
        Check(WriteAt(plan, 30, 18).Value.Terrain.Grid == 0 && WriteAt(plan, 30, 18).Value.Terrain.High == 0 && WriteAt(plan, 30, 18).Value.Terrain.Low == 0
                && WriteAt(plan, 30 + 36, 18 + 19).Value.Terrain.High == 9 && WriteAt(plan, 30, 18 + 20).Value.Terrain.Grid == Side
                && WriteAt(plan, 28, 16).Value.Terrain.Grid == Roof && WriteAt(plan, 27, 15) == null,
            "bandit camp: the camp's cells land at their origin with their decoration, the frame around them");
    }

    static void PlacementRejects()
    {
        var (map, anchor) = EastAnchorMap();
        var blockedColumn = (bool[,])map.Clone();
        Occupy(blockedColumn, 50, 0, 50, 59);
        var plan = BanditCampPlacement.Place(Camp(), anchor, blockedColumn);
        Check(!plan.IsPlaced && plan.Reason == BanditCampPlacement.NoSpace && plan.Writes == null && plan.Corridor == null,
            "bandit camp: an occupied area gives no-space and no cells");
        var blockedStrip = (bool[,])map.Clone();
        Occupy(blockedStrip, 76, 0, 76, 59);
        Check(BanditCampPlacement.Place(Camp(), anchor, blockedStrip).Reason == BanditCampPlacement.NoSpace,
            "bandit camp: an occupied column across the corridor's strip refuses every length and row");
        var narrow = VoidMap(120, 60);
        Occupy(narrow, 8, 18, 32, 44);
        Check(BanditCampPlacement.Place(Camp(), new BanditCampAnchor(10, 20, 30, 40), narrow).Reason == BanditCampPlacement.NoSpace,
            "bandit camp: a camp that would leave the map is refused");
        Check(BanditCampPlacement.Place(Camp(), new BanditCampAnchor(80, 20, 100, 20), map).Reason == BanditCampPlacement.NoSpace,
            "bandit camp: an anchor floor one row high has no room for the two row corridor");
        var noExit = new BanditCampRoom("s", Camp().Cells, Camp().Buildings, Camp().Items, Camp().Spawns, Camp().Features, 20, 11);
        var tooHigh = new BanditCampRoom("s", Open(10, 5), Array.Empty<BanditCampBuilding>(), Array.Empty<BanditCampItem>(),
            Array.Empty<BanditCampSpawn>(), Array.Empty<BanditCampFeature>(), 9, 4);
        Check(BanditCampPlacement.Place(noExit, anchor, map).Reason == BanditCampPlacement.NoExit && BanditCampPlacement.Place(tooHigh, anchor, map).Reason == BanditCampPlacement.NoExit,
            "bandit camp: a camp without an exit, or an exit in its top row, has no corridor");
        var before = (bool[,])map.Clone();
        BanditCampPlacement.Place(Camp(), anchor, map);
        Check(map.Cast<bool>().SequenceEqual(before.Cast<bool>()), "bandit camp: the search does not change the map");
    }

    static void PlacementFallsBackToOtherRows()
    {
        var (map, anchor) = EastAnchorMap();
        Occupy(map, 50, 41, 50, 41);
        // Corridors up to 20 columns, so the middle rows cannot reach past the blocked cell.
        var plan = BanditCampPlacement.Place(Camp(), anchor, map, maxLength: 20);
        Check(plan.IsPlaced && plan.ExitY == 28 && plan.Corridor.All(c => c.Y == 28 || c.Y == 29) && plan.Length == BanditCampPlacement.MinLength,
            "bandit camp: when the middle rows are blocked the next row that fits is taken");
        var blockedAll = VoidMap(120, 60);
        Occupy(blockedAll, 78, 18, 102, 44);
        Occupy(blockedAll, 50, 0, 50, 59);
        Check(BanditCampPlacement.Place(Camp(), anchor, blockedAll).Reason == BanditCampPlacement.NoSpace, "bandit camp: a wall across every length is no-space");
        Check(BanditCampPlacement.MinLength == 4 && BanditCampPlacement.MaxLength == 40 && BanditCampPlacement.CorridorHeight == 2
                && BanditCampLayout.RoofRing == 2 && BanditCampLayout.SideRows == 2,
            "bandit camp: the corridor is 4 to 40 columns long and 2 rows high, the frame has 2 face rows and a roof ring of 2");
    }
}
