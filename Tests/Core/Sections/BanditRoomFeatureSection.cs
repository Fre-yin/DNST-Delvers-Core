using DungeonSettlersDelvers.Core;
using static TestKit;

// The bandit room's waterfall and wooden bridge: where they fit, the waterfall's frames and how the parts sort.
internal static class BanditRoomFeatureSection
{
    private const BanditRoomFeatureRules.Cell O = BanditRoomFeatureRules.Cell.Other;
    private const BanditRoomFeatureRules.Cell G = BanditRoomFeatureRules.Cell.Ground;
    private const BanditRoomFeatureRules.Cell W = BanditRoomFeatureRules.Cell.Water;
    private const BanditRoomFeatureRules.Cell X = BanditRoomFeatureRules.Cell.Wall;

    // Rows from y = 0 upwards; outside the rows is Other.
    private static Func<int, int, BanditRoomFeatureRules.Cell> Grid(params BanditRoomFeatureRules.Cell[][] rows)
        => (x, y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length ? rows[y][x] : O;

    internal static void Run()
    {
        int count = 0, scope = 0;
        var firstFloor = Enumerable.Range(0, 10).Count(_ => BanditRoomFeatureRules.LogLineAllowed(ref count, ref scope, 11, 8));
        var nextFloor = Enumerable.Range(0, 3).Count(_ => BanditRoomFeatureRules.LogLineAllowed(ref count, ref scope, 22, 8));
        Check(firstFloor == 8 && nextFloor == 3 && scope == 22,
            "bandit room parts: the view's log lines are capped per floor, a new camp's floor logs its parts again");
        var room = Grid(
            new[] { G, W, W, G, G, G },
            new[] { G, W, W, W, G, G },
            new[] { X, X, X, X, X, X });
        Check(BanditRoomFeatureRules.WaterfallFits(room, 1, 1, +1) && !BanditRoomFeatureRules.WaterfallFits(room, 2, 1, +1)
                && !BanditRoomFeatureRules.WaterfallFits(room, 1, 0, +1),
            "bandit room: a waterfall fits on three water cells right under the wall, not on two or away from it");
        Check(BanditRoomFeatureRules.BridgeFits(room, 0, 0) && !BanditRoomFeatureRules.BridgeFits(room, 0, 1),
            "bandit room: a bridge fits on bank, water, water, bank and nowhere else");
        Check(BanditRoomFeatureRules.WaterfallFrameAt(0) == 0 && BanditRoomFeatureRules.WaterfallFrameAt(99) == 0
                && BanditRoomFeatureRules.WaterfallFrameAt(100) == 1 && BanditRoomFeatureRules.WaterfallFrameAt(799) == 7
                && BanditRoomFeatureRules.WaterfallFrameAt(800) == 0 && BanditRoomFeatureRules.WaterfallFrameAt(-5) == 0
                && BanditRoomFeatureRules.WaterfallFrameX(3) == 144 && BanditRoomFeatureRules.WaterfallFrameX(8) == 0,
            "bandit room: the waterfall shows 8 frames of 100 ms in a loop, 48 px apart in the sheet");
        Check(BanditRoomFeatureRules.WaterfallFrames * BanditRoomFeatureRules.WaterfallFrameWidth == 384
                && BanditRoomFeatureRules.WaterfallFrameHeight == 5 * 16 && BanditRoomFeatureRules.WaterfallFrameWidth == 3 * 16
                && BanditRoomFeatureRules.BridgeWidth == 4 * 16 && BanditRoomFeatureRules.BridgeHeight == 2 * 16,
            "bandit room: the image sizes match the cells they cover (16 px a cell)");
        Check(BanditRoomFeatureRules.CellOf(1, 9) == W && BanditRoomFeatureRules.CellOf(1, 10) == O && BanditRoomFeatureRules.CellOf(0, 6) == G
                && BanditRoomFeatureRules.CellOf(69, 0) == X && BanditRoomFeatureRules.CellOf(0, 0) == G && BanditRoomFeatureRules.CellOf(1, 0) == O,
            "bandit room: a cell is water by high decoration 9, wall by the wall side bits, ground without a flag; poison water is not water");
        Check(BanditRoomFeatureRules.BridgeWaterCells(4, 7).SequenceEqual(new[] { (5, 7), (6, 7) }),
            "bandit room: the bridge opens exactly its two water cells");
        Check(BanditRoomFeatureRules.LowestRow(4, 5) == 0f && BanditRoomFeatureRules.LowestRow(0, 2) == -0.5f
                && BanditRoomFeatureRules.FallbackOrder(BanditRoomFeatureRules.Layer.AboveUnits) > BanditRoomFeatureRules.FallbackOrder(BanditRoomFeatureRules.Layer.BelowUnits),
            "bandit room: the waterfall's lowest row is its water row, the bridge sits centred on its path row");
        var tiles = new[]
        {
            new BanditRoomFeatureRules.TileLayer("Default", 0, 10, 0), new BanditRoomFeatureRules.TileLayer("Decoration", 2, 11, 11),
            new BanditRoomFeatureRules.TileLayer("OuterWall", 4, 12, 0), new BanditRoomFeatureRules.TileLayer("OuterWallRoof", 5, 13, 0),
            new BanditRoomFeatureRules.TileLayer("OverDecoration", 6, 14, 0), new BanditRoomFeatureRules.TileLayer("OverDecoration", 6, 14, 1),
            new BanditRoomFeatureRules.TileLayer("UI", 9, 15, 0)
        };
        Check(BanditRoomFeatureRules.AboveAllTiles(tiles) == (14, 2) && BanditRoomFeatureRules.AboveAllTiles(Array.Empty<BanditRoomFeatureRules.TileLayer>()) == null,
            "bandit room: the waterfall lies above the topmost tile layer (wall side, roof, over decoration), below the UI");
        Check(BanditRoomFeatureRules.Over((10, 20, 30, 255), (200, 200, 200, 255)) == (10, 20, 30, 255)
                && BanditRoomFeatureRules.Over((10, 20, 30, 0), (200, 100, 50, 255)) == (200, 100, 50, 255)
                && BanditRoomFeatureRules.Over((0, 0, 0, 0), (0, 0, 0, 0)) == (0, 0, 0, 0)
                && BanditRoomFeatureRules.Over((255, 0, 0, 128), (0, 0, 255, 255)) == (128, 0, 127, 255),
            "bandit room: the bridge icon lays the rail over the deck with straight alpha; clear pixels show what lies below");
        // Under a camp's bridge the floor has dirt, walkable for the game; only the two water cells, only water.
        var cells = new BanditCampCell[8, 3];
        for (var x = 0; x < 8; x++) cells[x, 1] = BanditCampCell.Water;
        cells[2, 1] = BanditCampCell.Grass;
        BanditRoomFeatureRules.GroundUnderBridges(cells, new[] { new BanditCampFeature("bridge", 1, 1), new BanditCampFeature("waterfall", 4, 1) }, 0);
        var shifted = new BanditCampCell[8, 3];
        for (var x = 0; x < 8; x++) shifted[x, 1] = BanditCampCell.Water;
        BanditRoomFeatureRules.GroundUnderBridges(shifted, new[] { new BanditCampFeature("bridge", 4, 1) }, 2);
        Check(cells[2, 1] == BanditCampCell.Grass && cells[3, 1] == BanditCampCell.Dirt && cells[1, 1] == BanditCampCell.Water
                && cells[5, 1] == BanditCampCell.Water && cells[6, 1] == BanditCampCell.Water
                && shifted[7, 1] == BanditCampCell.Dirt && shifted[6, 1] == BanditCampCell.Water,
            "bandit room: a camp's bridge stands on dirt in the floor (its two water cells), nothing else changes, off the room's edge nothing");
        Check(BanditRoomFeatureRules.PaintsWater("GroundTilemap") && BanditRoomFeatureRules.PaintsWater("HighDecorationTilemap")
                && BanditRoomFeatureRules.PaintsWater("LowSpecialDecorationTilemap") && !BanditRoomFeatureRules.PaintsWater("OuterWallRoofTilemap")
                && !BanditRoomFeatureRules.PaintsWater("CreepTilemap") && !BanditRoomFeatureRules.PaintsWater("MarkerTilemap") && !BanditRoomFeatureRules.PaintsWater(null),
            "bandit room: the water painted under the bridge comes from the ground and decoration tilemaps only");

        var parts = new List<BanditRoomFeatureRules.Part>();
        var first = BanditRoomFeatureRules.Place(parts, true, "bridge", 3, 10, 5, true);
        var off = BanditRoomFeatureRules.Place(parts, true, "waterfall", 3, 20, 5, false);
        var overlap = BanditRoomFeatureRules.Place(parts, true, "waterfall", 3, 12, 5, true);
        var otherMap = BanditRoomFeatureRules.Place(parts, true, "waterfall", 4, 12, 5, true);
        Check(first == "placed" && off == "placed:terrain" && overlap == "refused:occupied" && otherMap == "placed" && parts.Count == 3
                && BanditRoomFeatureRules.Place(parts, true, "tree", 3, 0, 0, true) == "refused:kind"
                && BanditRoomFeatureRules.Place(parts, false, "bridge", 3, 40, 5, true) == "refused:campaign",
            "bandit room: parts are placed like items, off terrain with a warning, never overlapping on the same map");
        Check(BanditRoomFeatureRules.Remove(parts, 3, 12, 5) && !BanditRoomFeatureRules.Remove(parts, 3, 12, 5) && parts.Count == 2
                && parts[0].Line == "waterfall 20 5 3 5",
            "bandit room: removing takes the part that covers the cell, once; the list names anchor, width and height");
        var tall = new List<BanditRoomFeatureRules.Part>();
        BanditRoomFeatureRules.Place(tall, true, "waterfall", 1, 0, 0, true);
        Check(BanditRoomFeatureRules.Place(tall, true, "bridge", 1, 1, 4, true) == "refused:occupied"
                && BanditRoomFeatureRules.Remove(tall, 1, 2, 4) && tall.Count == 0,
            "bandit room: a click on any cell of a waterfall, also high on the wall, takes it; nothing may overlap it");

        var save = CoreSaveData.Create(Guid.NewGuid());
        save.SetRoomFeatures(parts);
        var back = CoreSaveData.Parse(save.Serialize(), save.Campaign).RoomFeatures();
        save.SetRoomFeatures(new List<BanditRoomFeatureRules.Part>());
        Check(back.Count == 2 && back[0].Kind == "waterfall" && back[0].Map == 3 && back[0].X == 20 && back[0].Y == 5 && back[1].Map == 4
                && !save.Serialize().Contains("roomFeatures"),
            "bandit room: the placed parts survive Core's save block and an empty list leaves no field");
        FloorSeeds();
        CampWagon();
    }

    // Every dungeon floor is the same map: a part carries its floor's seed and shows only there; one without a seed shows as before.
    static void FloorSeeds()
    {
        var parts = new List<BanditRoomFeatureRules.Part>();
        var a = BanditRoomFeatureRules.Place(parts, true, "bridge", 3, 10, 5, true, 111);
        var b = BanditRoomFeatureRules.Place(parts, true, "bridge", 3, 10, 5, true, 222);
        var legacy = new BanditRoomFeatureRules.Part("waterfall", 3, 40, 5);
        parts.Add(legacy);
        Check(a == "placed" && b == "placed" && parts.Count(part => BanditRoomFeatureRules.Shown(part, 3, 111)) == 2
                && parts.Count(part => BanditRoomFeatureRules.Shown(part, 3, 222)) == 2 && !BanditRoomFeatureRules.Shown(parts[0], 4, 111),
            "bandit room: parts of two floors at the same cells do not clash, each floor shows its own and the parts without a seed");
        Check(BanditRoomFeatureRules.Place(parts, true, "waterfall", 3, 40, 5, true, 333) == "refused:occupied"
                && !BanditRoomFeatureRules.Remove(parts, 3, 10, 5, 333) && BanditRoomFeatureRules.Remove(parts, 3, 10, 5, 222)
                && parts.Count == 2 && parts[0].Seed == 111,
            "bandit room: a part without a seed still blocks its cells; removing takes only the shown floor's part");
        var save = CoreSaveData.Create(Guid.NewGuid());
        save.SetRoomFeatures(parts);
        var text = save.Serialize();
        var back = CoreSaveData.Parse(text, save.Campaign).RoomFeatures();
        Check(back.Count == 2 && back[0].Seed == 111 && back[1].Seed == 0 && text.Contains("\"s\":111") && text.Split("\"s\":").Length == 2,
            "bandit room: the floor's seed survives the block as s; a part without one keeps its old entry");
        Check(BanditRoomFeatureRules.Prune(parts, 3, 222) == 1 && parts.Count == 1 && parts[0].Seed == 0 && BanditRoomFeatureRules.Prune(parts, 3, 222) == 0,
            "bandit room: furnishing a floor's camp clears the parts of other floors, never the ones without a seed");
    }

    static void CampWagon()
    {
        var save = CoreSaveData.Create(Guid.NewGuid());
        var wagon = Guid.NewGuid();
        Check(!save.CampWagon(out _, out _), "bandit camp: a block without a camp has no wagon");
        var flips = new[] { Guid.NewGuid(), Guid.NewGuid() };
        save.SetCamp(4711, wagon, flips);
        var back = CoreSaveData.Parse(save.Serialize(), save.Campaign);
        Check(back.CampWagon(out var seed, out var guid) && seed == 4711 && guid == wagon && back.CampFlips(out var flipSeed).SetEquals(flips) && flipSeed == 4711,
            "bandit camp: the wagon, the mirrored pictures and their floor's seed survive the block");
        back.ClearCampWagon();
        Check(!back.CampWagon(out _, out _) && back.CampFlips(out _).Count == 2, "bandit camp: a gathered wagon leaves the mirrored pictures");
        back.SetCamp(4711, Guid.Empty, Array.Empty<Guid>());
        Check(!back.Serialize().Contains("\"camp\"") && back.CampFlips(out _).Count == 0, "bandit camp: a camp without wagon and mirrored pictures leaves no entry");
        var boxed = CoreSaveData.Create(Guid.NewGuid());
        boxed.SetCamp(9, Guid.Empty, null, (510, 94, 553, 113));
        var boxedBack = CoreSaveData.Parse(boxed.Serialize(), boxed.Campaign);
        Check(boxedBack.CampBox(out var boxSeed) == (510, 94, 553, 113) && boxSeed == 9 && CoreSaveData.Create(Guid.NewGuid()).CampBox(out _) == null,
            "bandit camp: the camp's box on the map survives the block with its floor's seed");
        Check(!boxedBack.CampWakes(), "bandit camp: a block without the wake entry does not wake, as every block written before");
        var waking = CoreSaveData.Create(Guid.NewGuid());
        waking.SetCamp(9, Guid.Empty, null, (510, 94, 553, 113), true);
        var quiet = CoreSaveData.Create(Guid.NewGuid());
        quiet.SetCamp(9, Guid.Empty, null, (510, 94, 553, 113), false);
        Check(CoreSaveData.Parse(waking.Serialize(), waking.Campaign).CampWakes() && !CoreSaveData.Parse(quiet.Serialize(), quiet.Campaign).CampWakes()
                && CoreSaveData.Parse(quiet.Serialize(), quiet.Campaign).CampBox(out _) == (510, 94, 553, 113),
            "bandit camp: whether the camp wakes on entering survives the block; a quiet camp keeps its box");
        Check(!BanditCampWakeRules.WakesOnEnter(BanditCampRoomFiles.NormalFile) && !BanditCampWakeRules.WakesOnEnter(BanditCampRoomFiles.MirroredFile)
                && !BanditCampWakeRules.WakesOnEnter("Other.json") && !BanditCampWakeRules.WakesOnEnter(null),
            "bandit camp: today's camp, normal and mirrored, does not wake on entering; an unknown file neither");
        Check(BanditCampWakeRules.OffLine(BanditCampRoomFiles.NormalFile) == "CORE_BANDITS_CAMP_WAKE off camp=Bandit-Forestcamp.json"
                && BanditCampWakeRules.WakeLine(4, 0f, "u", 429, 124, 39) == "CORE_BANDITS_CAMP_WAKE woken=4 lead=0.0 by=u at=429,124 nearest=39"
                && BanditCampWakeRules.Chebyshev(429, 124, 390, 122) == 39,
            "bandit camp: the wake's log lines name the trigger, its cell and the nearest bandit");
        Check(CampGuardianStoneRules.InReach(10, 10, 10, 10) && CampGuardianStoneRules.InReach(16, 10, 10, 10)
                && !CampGuardianStoneRules.InReach(17, 10, 10, 10) && CampGuardianStoneRules.InReach(14, 14, 10, 10)
                && !CampGuardianStoneRules.InReach(15, 15, 10, 10),
            "camp guardian stones: the aura reaches closer than 7 cells, measured straight as the game does");
        Check(CampGuardianStoneRules.Due(-1f, 0f) && !CampGuardianStoneRules.Due(100f, 109.9f) && CampGuardianStoneRules.Due(100f, 110f)
                && CampGuardianStoneRules.Due(100f, 50f),
            "camp guardian stones: the first pulse at once, then every 10 s of game time, anew after a load");
        Check(CampGuardianStoneRules.FoundLine(6, 779470258) == "CORE_CAMP_GUARDIAN_STONES found=6 seed=779470258"
                && CampGuardianStoneRules.AuraLine("u", "s", 429, 124) == "CORE_CAMP_GUARDIAN_AURA unit=u stone=s at=429,124 seconds=10"
                && CampGuardianStoneRules.AuraKey == "AFFECTER_LesserGuardianAura" && CampGuardianStoneRules.AuraSeconds == 10f,
            "camp guardian stones: the game's aura for 10 s, and the log lines");
        var onlyWagon = CoreSaveData.Create(Guid.NewGuid());
        onlyWagon.SetCamp(1, wagon, null);
        onlyWagon.ClearCampWagon();
        Check(!onlyWagon.Serialize().Contains("\"camp\""), "bandit camp: the normal camp's entry goes with its gathered wagon");
    }
}
