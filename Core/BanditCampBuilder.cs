using HarmonyLib;
using UnityEngine;
#if BEPINEX
using DungeonGenerationAttemptResult = global::Refactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
#else
using DungeonGenerationAttemptResult = Il2CppRefactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
#endif

namespace DungeonSettlersDelvers.Core;

// The forest bandits' camp built into a generated floor (the camp hangs on the floor through a real
// corridor of the game, like any room). Before the game builds the map from its generator result
// (DungeonGeneratorStrategy.GeneratePrepared, the place the Modding Toolkit's dev room uses), Core decides on the floor
// (BanditCampChoice), places camp and corridor beside an anchor room (BanditCampPlacement), writes their cells into the floor's
// raw grids and adds both as rooms of the game: the camp, a corridor record of type 99 between camp and anchor, and the
// corridor on the anchor's list of connected rooms. The game then builds grid, picture, paths and room areas from that, and saves
// them like any floor. Everything is worked out first and written only at the end; when anything does not fit, the floor stays
// as the game made it, without a camp and without bandits, with a log line.
internal static class BanditCampBuilder
{
    // force: the normal camp also on a floor with the Alpha (BanditCampChoice.Decide, ignoreAlpha), for tests that need a camp.
    internal const string ModeAuto = "auto", ModeNormal = "normal", ModeMirrored = "mirrored", ModeOff = "off", ModeForce = "force";

    // Test hook (DelversCoreRuntime.BanditCampForTest): the variant of the next camps, or none; auto is the seed's pick.
    private static string testMode = ModeAuto;

    // The camp of the floor built last, for its furnishing (BanditCampFurnish); null when that floor has none.
    internal static BanditCampBuilt Last { get; private set; }

    internal static void ResetLifecycle()
    {
        testMode = ModeAuto;
        Last = null;
    }

    internal static bool SetTestMode(string mode)
    {
        if (mode != ModeAuto && mode != ModeNormal && mode != ModeMirrored && mode != ModeOff && mode != ModeForce) return false;
        testMode = mode;
        return true;
    }

    internal static void OnGeneratePrepared(MapGenerateInfo info, DungeonPreMapData pre)
    {
        Last = null;
        if (pre == null || testMode == ModeOff || !ApocalypseRules.Bandits) return;
        int region = (int)info.DungeonRegion, floor = info.Floor, seed = pre.Seed;
        if (!BanditCampChoice.InGate(region, floor)) return;
        var rule = BanditCampBuildRules.InUse;
        var graph = BanditCampProbe.Graph(pre);
        var decision = BanditCampChoice.Decide(graph.Nodes, BanditCampProbe.QuestRoomKeys(), BanditCampProbe.LongCorridor(), seed, region, floor,
            BanditCampRoomFiles.MirroredAvailable, rule.AlphaFloors || testMode == ModeForce, rule.MainPath);
        if (testMode == ModeNormal || testMode == ModeForce) decision.Variant = BanditCampChoice.Normal;
        else if (testMode == ModeMirrored) decision.Variant = BanditCampChoice.Mirrored;
        DelversHost.Info(BanditCampChoice.ChoiceLine(decision));
        if (!decision.CampComes) return;
        if (BanditCampRoomFiles.Room(decision.Variant, out var problem) == null)
        {
            DelversHost.Warning(BanditCampBuildRules.FailedLine(seed, region, floor, BanditCampBuildRules.FileReason, problem));
            return;
        }
        // A test that asks for one variant gets only that one.
        var otherSide = rule.OtherSide && (testMode == ModeAuto || testMode == ModeForce);
        var site = new Site(pre);
        var tried = new List<string>();
        var found = FirstPlace(site, graph, decision, rule, otherSide, tried);
        if (found == null)
        {
            DelversHost.Warning(BanditCampBuildRules.FailedLine(seed, region, floor, BanditCampPlacement.NoSpace, string.Join("+", tried)));
            return;
        }
        var (anchor, room, variant, plan) = found.Value;
        Last = site.Build(room, anchor, plan, graph.Ids(), seed, region, floor, variant);
        DelversHost.Info(BanditCampBuildRules.PlacedLine(seed, region, floor, variant, Last.CampId, Last.CorridorId, anchor.ID, plan, room.Width, room.Height));
    }

    // The first anchor, in the decision's order, and the first variant with a place there under the rule; null when none fits.
    private static (DungeonPreRoomData Anchor, BanditCampRoom Room, int Variant, BanditCampPlan Plan)? FirstPlace(Site site,
        BanditCampProbe.FloorGraph graph, BanditCampChoice.Decision decision, BanditCampBuildRules.CampRule rule, bool otherSide, List<string> tried)
    {
        var variants = BanditCampBuildRules.Variants(decision.Variant, otherSide, BanditCampRoomFiles.MirroredAvailable);
        foreach (var anchorId in decision.Anchors)
        {
            var anchor = graph.Row(anchorId);
            if (anchor == null)
            {
                tried.Add(BanditCampRules.LogId(anchorId) + ":" + BanditCampBuildRules.NoCellsReason);
                continue;
            }
            foreach (var variant in variants)
            {
                var room = BanditCampRoomFiles.Room(variant, out _);
                if (room == null) continue;
                var reason = site.Try(room, anchor, out var plan, rule.MaxLength);
                if (reason == null) return (anchor, room, variant, plan);
                tried.Add(BanditCampRules.LogId(anchorId) + ":" + BanditCampChoice.VariantName(variant) + ":" + reason);
            }
        }
        return null;
    }

    // The seed scan's check (BanditCampTemplates): why the camp would not come on this generator result of the forest's fourth floor
    // under the rule in use (BanditCampBuildRules.InUse), or null when it would be placed; nothing is written. The variant is the
    // floor's own pick, as in the game.
    internal static string Measure(DungeonPreMapData pre, int seed)
    {
        if (pre == null) return BanditCampBuildRules.NoCellsReason;
        var rule = BanditCampBuildRules.InUse;
        var graph = BanditCampProbe.Graph(pre);
        var decision = BanditCampChoice.Decide(graph.Nodes, BanditCampProbe.QuestRoomKeys(), BanditCampProbe.LongCorridor(), seed,
            BanditCampChoice.ForestRegion, BanditCampChoice.CampFloor, BanditCampRoomFiles.MirroredAvailable, rule.AlphaFloors, rule.MainPath);
        if (!decision.CampComes) return decision.Reason;
        if (BanditCampRoomFiles.Room(decision.Variant, out _) == null) return BanditCampBuildRules.FileReason;
        return FirstPlace(new Site(pre), graph, decision, rule, rule.OtherSide, new List<string>()) == null ? BanditCampPlacement.NoSpace : null;
    }

    // The floor's raw data: its three grids and what a place needs from them.
    private sealed class Site
    {
        private readonly DungeonPreMapData pre;
        private readonly BanditCampGrid grids, low, high;
        private readonly Vector2Int half;
        private readonly bool[,] isVoid;

        internal Site(DungeonPreMapData pre)
        {
            this.pre = pre;
            grids = new BanditCampGrid(pre.RawGrids);
            low = new BanditCampGrid(pre.LowDecorations);
            high = new BanditCampGrid(pre.HighDecorations);
            half = new Vector2Int(grids.Width / 2, grids.Height / 2);
            isVoid = new bool[grids.Width, grids.Height];
            for (var x = 0; x < grids.Width; x++)
                for (var y = 0; y < grids.Height; y++)
                    isVoid[x, y] = grids[x, y] == BanditCampLayout.GridVoid;
        }

        // Null with the plan when the camp fits beside this anchor, else the reason.
        internal string Try(BanditCampRoom room, DungeonPreRoomData anchor, out BanditCampPlan plan, int maxLength = BanditCampPlacement.MaxLength)
        {
            plan = null;
            var cells = FloorCells(anchor);
            if (cells == null) return BanditCampBuildRules.OriginReason;
            var box = BanditCampBuildRules.AnchorBox(cells);
            if (box == null) return BanditCampBuildRules.NoCellsReason;
            plan = BanditCampPlacement.Place(room, box.Value, isVoid, maxLength: maxLength, accept: found => BanditCampBuildRules.JointOpensOnFloor(found, IsFloor));
            return plan.IsPlaced ? null : plan.Reason;
        }

        // The anchor's floor cells on the grid. The game's room cells count from the room's pivot, the pivot from the middle of
        // the floor (the Toolkit's dev room, first test); a room whose cells do not land on floor that way is skipped.
        private List<(int X, int Y)> FloorCells(DungeonPreRoomData anchor)
        {
            var occupied = anchor?.RoomOccupiedPos;
            if (occupied == null || occupied.Count == 0) return null;
            var shift = anchor.RoomPivot + half;
            var cells = new List<(int X, int Y)>();
            for (var i = 0; i < occupied.Count; i++)
            {
                var cell = occupied[i] + shift;
                if (IsFloor(cell.x, cell.y)) cells.Add((cell.x, cell.y));
            }
            return cells.Count * 2 >= occupied.Count ? cells : null;
        }

        private bool IsFloor(int x, int y)
        {
            if (!grids.Contains(x, y)) return false;
            var grid = grids[x, y];
            return grid != BanditCampLayout.GridVoid && grid != BanditCampLayout.GridRoof && grid != BanditCampLayout.GridSide;
        }

        // Writes the cells and adds camp and corridor to the floor's rooms; the plan was checked by Try.
        internal BanditCampBuilt Build(BanditCampRoom room, DungeonPreRoomData anchor, BanditCampPlan plan, List<string> ids, int seed, int region,
            int floor, int variant)
        {
            var campId = BanditCampBuildRules.NextId(BanditCampBuildRules.CampTypeName, BanditCampBuildRules.CampKey, ids);
            var corridorId = BanditCampBuildRules.NextId(BanditCampBuildRules.CorridorTypeName, BanditCampBuildRules.CorridorKey, ids);
            var campCells = new List<(int X, int Y)>();
            for (var x = 0; x < room.Width; x++)
                for (var y = 0; y < room.Height; y++)
                    if (BanditCampLayout.IsOpen(room.Cells[x, y])) campCells.Add((plan.CampOriginX + x, plan.CampOriginY + y));
            var corridorCells = new List<(int X, int Y)>(plan.Corridor);
            corridorCells.AddRange(plan.Joint);
            var camp = Row(BanditCampBuildRules.CampKey, campId, RoomType.Combat, anchor, campCells, corridorId);
            var corridor = Row(BanditCampBuildRules.CorridorKey, corridorId, RoomType.Corridor, anchor, corridorCells, campId, anchor.ID);
            foreach (var write in plan.Writes)
            {
                grids[write.X, write.Y] = write.Terrain.Grid;
                low[write.X, write.Y] = write.Terrain.Low;
                high[write.X, write.Y] = write.Terrain.High;
            }
            foreach (var (x, y) in BanditCampBuildRules.JointFaces(plan))
                if (grids[x, y] == BanditCampLayout.GridRoof) grids[x, y] = BanditCampLayout.GridSide;
            pre.RawRooms.Add(camp);
            pre.RawCorridors.Add(corridor);
            anchor.ConnectedRooms ??= new Il2CppSystem.Collections.Generic.HashSet<string>();
            anchor.ConnectedRooms.Add(corridorId);
            return new BanditCampBuilt(seed, region, floor, variant, campId, corridorId, anchor.ID, room, plan);
        }

        // A room of the game: a copy of a table row without monsters and buildings (as the Toolkit's dev room copies its row), its
        // cells from the pivot in the middle of its box, no spawn or decoration cells of the game; the repeat mode is the anchor's
        // (Edgar's type, which Core names only through the anchor).
        private DungeonPreRoomData Row(string tableKey, string id, RoomType type, DungeonPreRoomData anchor, List<(int X, int Y)> cells,
            params string[] connected)
        {
            var box = BanditCampBuildRules.AnchorBox(cells).Value;
            var center = new Vector2Int((box.MinX + box.MaxX) / 2, (box.MinY + box.MaxY) / 2);
            var occupied = new Il2CppSystem.Collections.Generic.List<Vector2Int>();
            foreach (var (x, y) in cells) occupied.Add(new Vector2Int(x - center.x, y - center.y));
            var links = new Il2CppSystem.Collections.Generic.HashSet<string>();
            foreach (var other in connected) links.Add(other);
            return new DungeonPreRoomData
            {
                TableData = TableRow(tableKey),
                ID = id,
                RoomType = type,
                RepeatMode = anchor.RepeatMode,
                RoomPivot = center - half,
                RoomOccupiedPos = occupied,
                SpawnCategoryDic = new Il2CppSystem.Collections.Generic.Dictionary<Vector2Int, SpawnCategory>(),
                DecorationBuildingDic = new Il2CppSystem.Collections.Generic.Dictionary<Vector2Int, string>(),
                ConnectedRooms = links
            };
        }

        private static DungeonRoomTableData TableRow(string key)
        {
            var source = DataSheetManager.Instance?.GetDungeonRoomData(key) ?? throw new InvalidOperationException("the room table has no row " + key);
            return new DungeonRoomTableData
            {
                Key = source.Key, DevComment = "Delvers bandit camp", Region = source.Region, AvailablePhases = source.AvailablePhases,
                RoomType = source.RoomType, RoomDecoStyle = source.RoomDecoStyle, Weight = source.Weight,
                SpawnMonster = Empty(), SpawnBuildingList_1 = Empty(), SpawnBuildingList_2 = Empty(), SpawnBuildingList_3 = Empty(),
                SpawnBuildingList_4 = Empty(), SpawnBuildingList_5 = Empty(), SpawnBuildingList_6 = Empty(),
                SpawnItemCategory_1 = source.SpawnItemCategory_1, SpawnItemCategory_2 = source.SpawnItemCategory_2
            };
        }

        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray Empty() => new(0);
    }
}

// The camp built into a floor: what its furnishing and the save's block need.
internal sealed class BanditCampBuilt
{
    internal BanditCampBuilt(int seed, int region, int floor, int variant, string campId, string corridorId, string anchorId, BanditCampRoom room,
        BanditCampPlan plan)
    {
        Seed = seed;
        Region = region;
        Floor = floor;
        Variant = variant;
        CampId = campId;
        CorridorId = corridorId;
        AnchorId = anchorId;
        Room = room;
        Plan = plan;
    }

    internal int Seed { get; }
    internal int Region { get; }
    internal int Floor { get; }
    internal int Variant { get; }
    internal string CampId { get; }
    internal string CorridorId { get; }
    internal string AnchorId { get; }
    internal BanditCampRoom Room { get; }
    internal BanditCampPlan Plan { get; }
}

// Before the routine that builds the map: the generator result is still raw data (as BanditCampProbePreparedPatch notes).
[HarmonyPatch(typeof(DungeonGeneratorStrategy), nameof(DungeonGeneratorStrategy.GeneratePrepared))]
internal static class BanditCampBuilderPatch
{
    private static void Prefix(MapGenerateInfo info, DungeonGenerationAttemptResult generationResult)
    {
        try { BanditCampBuilder.OnGeneratePrepared(info, generationResult?.PreData); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_CAMP_FAILED stage=build (the floor stays as the game made it): " + ex.Message); }
    }
}
