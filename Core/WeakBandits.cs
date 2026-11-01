using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#if BEPINEX
using DungeonGenerationAttemptResult = global::Refactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
#else
using DungeonGenerationAttemptResult = Il2CppRefactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
#endif

namespace DungeonSettlersDelvers.Core;

// The weak forest bandits in a generated floor (rules: WeakBanditSpawnRules, WeakBanditRules). The game rolls one monster group
// per combat room (DungeonGeneratorStrategy.HandleRandomMonsterSpawn, which asks DataSheetManager.GetRandomSpawnGroupData). While
// their rule is in effect (ApocalypseRules.WeakBandits: combat Apocalypse or its option, and the forest bandits active):
// - GeneratePrepared notes the floor's seed, region and floor (WeakBanditsFloorPatch);
// - around HandleRandomMonsterSpawn, the room's id waits for its roll (WeakBanditsRoomPatch);
// - GetRandomSpawnGroupData gets the room's type from the game's room data: for a plain combat room the hash picks, it hands the
//   game a group of the weak keys instead of its own roll, once (WeakBanditsGroupPatch). HardCombat and the others keep theirs.
// The game shuffles the positions, spawns and saves the units as it does with its own; Core's rows draw and steer them. A room the
// game refills later asks without a pending room and gets the game's monsters. CORE_WEAKBANDIT_ROOM per replaced room,
// CORE_WEAKBANDIT_FLOOR when the next floor begins.
internal static class WeakBandits
{
    private const int LogCap = 60;
    private static int lines;
    private static bool floorOn;
    private static int seed, region, floor, level, rooms, replaced;
    private static double percent;
    private static string pending; // the room whose roll comes next

    internal static void Reset()
    {
        floorOn = false;
        pending = null;
        spawnCells.Clear();
        lines = 0;
        groups.Clear();
    }

    // A floor is about to be built.
    internal static void Begin(MapGenerateInfo info, int floorSeed)
    {
        EndFloor();
        pending = null;
        floorOn = false;
        if (!ApocalypseRules.WeakBandits) return;
        region = (int)info.DungeonRegion;
        floor = info.Floor;
        level = WeakBanditSpawnRules.LevelOf(region, floor);
        if (level == 0) return;
        seed = floorSeed;
        percent = WeakBanditSpawnRules.Percent(CoreSettings.Number(WeakBanditSpawnRules.PercentKey));
        rooms = replaced = 0;
        floorOn = true;
    }

    // The game is about to roll the monster group of one room; whether it is a plain combat room shows only in the roll (GroupFor).
    internal static void BeforeRoom(string roomId) => pending = floorOn ? roomId : null;

    private static readonly List<string> groups = new();

    // Where each weak bandit stood at its spawn (BanditDifficultyPatch, the game's own spawn step): a room's group spawns close
    // together, and a fight spreads it out (guard and healer of one group died 15 cells apart and each left
    // the group's coins). Not saved; after a load a bandit without its cell counts where it stands.
    private static readonly Dictionary<string, (int X, int Y)> spawnCells = new(StringComparer.Ordinal);
    private const int SpawnCellCap = 512;

    internal static void NoteSpawn(UnitEntity unit)
    {
        if (unit == null || !CoreUnitRules.TryGet(unit.Key, out var definition) || !WeakBanditRules.IsWeak(definition)) return;
        var inMap = CoreGame.InMap;
        if (inMap == null) return;
        // Called before the difficulty swap in the same postfix: a unit the map cannot place yet only goes without its cell.
        try
        {
            var at = inMap.GetLogicalPosition(unit.Guid);
            if (spawnCells.Count >= SpawnCellCap) spawnCells.Clear();
            spawnCells[unit.Guid.ToString()] = (at.x, at.y);
        }
        catch (Exception) { /* no cell: counted where it stands at its death */ }
    }

    private static (int X, int Y) CellOf(IInMapDataSupplier inMap, UnitEntity unit)
    {
        if (spawnCells.TryGetValue(unit.Guid.ToString(), out var cell)) return cell;
        var at = inMap.GetLogicalPosition(unit.Guid);
        return (at.x, at.y);
    }

    // A Core bandit died (CorpseKeep.NoteBanditCorpse, once per death): the last weak bandit of its group (by the cells they spawned
    // on, NoteSpawn) leaves the group's coins
    // (WeakBanditRules.DropsCoin, one stack of CoinCount) on its cell, through the game's own spawn request. CORE_WEAKBANDIT_COIN
    // per stack.
    internal static void OnDied(MapType map, UnitEntity unit, CoreUnitRules.Definition definition)
    {
        if (unit == null || definition == null || !WeakBanditRules.IsWeak(definition)) return;
        var inMap = CoreGame.InMap;
        if (inMap == null) return;
        var at = inMap.GetLogicalPosition(unit.Guid);
        var home = CellOf(inMap, unit);
        var living = 0;
        foreach (var other in CoreGame.MapUnits())
        {
            if (other == null || other.IsDead || other.Guid == unit.Guid || !CoreUnitRules.TryGet(other.Key, out var od) || !WeakBanditRules.IsWeak(od)) continue;
            var there = CellOf(inMap, other);
            if (GroupAiRules.IsGroupAlly(Math.Max(Math.Abs(there.X - home.X), Math.Abs(there.Y - home.Y)))) living++;
        }
        if (!WeakBanditRules.DropsCoin(true, living)) return;
        CoreGame.Enqueue(new SpawnItemRequested(map, WeakBanditRules.CoinKey, at, WeakBanditRules.CoinCount).Cast<IEventData>());
        DelversHost.Info($"CORE_WEAKBANDIT_COIN unit={unit.Guid} key={unit.Key} count={WeakBanditRules.CoinCount} at={at.x},{at.y}");
    }

    // ---- test hook (WeakBanditsTestHooks) ----

    // The gate and hash over the combat rooms of the room table that roll their monsters, without a floor, for a test.
    internal static (int Rooms, int Replaced, string Groups) Scan(int floorSeed, int scanFloor, IReadOnlyList<string> roomIds)
    {
        var level = WeakBanditSpawnRules.LevelOf(WeakBanditSpawnRules.Region, scanFloor);
        var scanPercent = WeakBanditSpawnRules.Percent(CoreSettings.Number(WeakBanditSpawnRules.PercentKey));
        var picked = new List<string>();
        foreach (var room in roomIds)
        {
            if (level == 0 || !WeakBanditSpawnRules.Hit(floorSeed, WeakBanditSpawnRules.Region, scanFloor, room, scanPercent)) continue;
            picked.Add(room + ":" + string.Join("+", WeakBanditSpawnRules.GroupOf(floorSeed, WeakBanditSpawnRules.Region, scanFloor, room, level, WeakBanditSpawnRules.ClassesOf())));
        }
        return (roomIds.Count, picked.Count, string.Join(";", picked));
    }

    internal static void AfterRoom() => pending = null;

    // The game's roll for the room, or a weak group when the room is a plain combat room the hash picks (once per room).
    internal static SpawnGroupTableData GroupFor(int roomType)
    {
        var roomId = pending;
        pending = null;
        if (roomId == null || !WeakBanditSpawnRules.Replaceable(roomType)) return null;
        rooms++;
        if (!WeakBanditSpawnRules.Hit(seed, region, floor, roomId, percent)) return null;
        var classes = WeakBanditSpawnRules.GroupOf(seed, region, floor, roomId, level, WeakBanditSpawnRules.ClassesOf());
        var keys = WeakBanditSpawnRules.KeysOf(level, classes);
        if (keys.Length == 0) return null;
        replaced++;
        groups.Add(roomId + ":" + string.Join("+", classes));
        if (lines++ < LogCap) DelversHost.Info(WeakBanditSpawnRules.RoomLine(seed, region, floor, roomId, keys));
        return new SpawnGroupTableData
        {
            Key = WeakBanditSpawnRules.GroupKey,
            Region = (DungeonRegion)region,
            RoomType = (RoomType)WeakBanditSpawnRules.CombatRoomType,
            Weight = 0,
            AvailablePhase = new Il2CppStructArray<int>(new[] { floor + 1 }),
            SpawnPool = new Il2CppStringArray(keys)
        };
    }

    private static void EndFloor()
    {
        if (floorOn && lines++ < LogCap) DelversHost.Info(WeakBanditSpawnRules.FloorLine(seed, region, floor, rooms, replaced, percent) + " groups=" + string.Join(";", groups));
        groups.Clear();
    }
}

[HarmonyPatch(typeof(DungeonGeneratorStrategy), nameof(DungeonGeneratorStrategy.GeneratePrepared))]
internal static class WeakBanditsFloorPatch
{
    private static void Prefix(MapGenerateInfo info, DungeonGenerationAttemptResult generationResult)
    {
        try { WeakBandits.Begin(info, generationResult?.PreData?.Seed ?? 0); }
        catch (Exception ex) { DelversHost.Warning("CORE_WEAKBANDIT_FAILED stage=floor (the floor keeps the game's monsters): " + ex.Message); }
    }
}

[HarmonyPatch(typeof(DungeonGeneratorStrategy), nameof(DungeonGeneratorStrategy.HandleRandomMonsterSpawn))]
internal static class WeakBanditsRoomPatch
{
    private static void Prefix(string roomID)
    {
        try { WeakBandits.BeforeRoom(roomID); }
        catch (Exception ex) { DelversHost.Warning("CORE_WEAKBANDIT_FAILED stage=room: " + ex.Message); }
    }

    private static Exception Finalizer(Exception __exception)
    {
        WeakBandits.AfterRoom();
        return __exception;
    }
}

[HarmonyPatch(typeof(DataSheetManager), nameof(DataSheetManager.GetRandomSpawnGroupData))]
internal static class WeakBanditsGroupPatch
{
    private static void Postfix(RoomType roomType, ref SpawnGroupTableData __result)
    {
        try
        {
            var weak = WeakBandits.GroupFor((int)roomType);
            if (weak != null) __result = weak;
        }
        catch (Exception ex) { DelversHost.Warning("CORE_WEAKBANDIT_FAILED stage=group (the room keeps the game's roll): " + ex.Message); }
    }
}
