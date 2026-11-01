using HarmonyLib;
using UnityEngine;
#if BEPINEX
using DungeonGenerationAttemptResult = global::Refactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
using RuntimeDungeonData = global::Refactor.Runtime.RuntimeDungeonData;
#else
using DungeonGenerationAttemptResult = Il2CppRefactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
using RuntimeDungeonData = Il2CppRefactor.Runtime.RuntimeDungeonData;
#endif

namespace DungeonSettlersDelvers.Core;

// The room graph probe for the bandits' camp (rules: BanditCampRules), only with bandits.camp.probe in
// Core's settings file or SetCampProbeForTest; off by default. Its three patch classes carry the group OneShot and are patched
// only while that group is on (CoreProbes), and the key stays on top of the group: a run with the group on does not log a floor's
// graph, about 40 lines each, unless this key is on as well. It only logs and changes nothing:
// - RawDungeonDataContainer.SetDungeon (the overload with six arguments) remembers region, floor,
//   phase and seed of the floor being generated;
// - DungeonGeneratorStrategy.GeneratePrepared gets the generator's result (DungeonPreMapData);
// - RuntimeDungeonData.OnAddMonsterGroup, which the generation calls right after SetDungeon with that
//   result's rooms (RawRooms) only, logs the graph of every forest floor 4 (region 0, phase 4) once. The
//   corridors (RawCorridors) come from the kept result: a room's ConnectedRooms names corridors only.
// - A graph that cannot place a camp (no corridor links, no way to the portal) logs
//   CORE_BANDITS_GRAPH_FAILED reason=empty|no-path; a missing corridor list stage=corridors.
// Recon (CampProbeRecon) only looks for the generator and its level inputs, it generates nothing.
internal static class BanditCampProbe
{
    internal const string EnabledKey = "bandits.camp.probe";
    internal const string LongCorridorKey = "bandits.camp.long-corridor";
    internal const int ForestRegion = BanditCampChoice.ForestRegion;
    internal const int CampPhase = BanditCampChoice.CampPhase;

    // At most this many floors per session are logged (each floor writes about 40 lines).
    internal const int FloorLogCap = 200;

    private static bool? testOverride;
    private static int region = -1;
    private static int floor = -1;
    private static int phase = -1;
    private static int seed;
    private static int floors;
    // Set by SetDungeon, used by the first OnAddMonsterGroup after it: one log per generated floor.
    private static bool armed;
    // The generator's result of the floor being prepared (only while the probe is on), taken by the
    // next OnAddMonsterGroup.
    private static DungeonPreMapData prepared;

    internal static bool Enabled => testOverride ?? CoreSettings.CampProbeEnabled;

    internal static void SetTest(bool on)
    {
        testOverride = on;
        DelversHost.Info("CORE_TEST_CAMP_PROBE on=" + (on ? "true" : "false"));
    }

    internal static void ResetLifecycle()
    {
        testOverride = null;
        region = floor = phase = -1;
        seed = 0;
        floors = 0;
        armed = false;
        prepared = null;
    }

    internal static void OnGeneratePrepared(DungeonGenerationAttemptResult result)
    {
        prepared = Enabled ? result?.PreData : null;
    }

    internal static void OnSetDungeon(RawDungeonDataContainer container, DungeonRegion region, int floor, int currentSeed)
    {
        BanditCampProbe.region = (int)region;
        BanditCampProbe.floor = floor;
        BanditCampProbe.seed = currentSeed;
        phase = container != null ? container._currentPhase : -1;
        armed = true;
    }

    internal static void OnAddMonsterGroup(Il2CppSystem.Collections.Generic.List<DungeonPreRoomData> rows)
    {
        // The kept result belongs to this call only; a later floor never pairs with it.
        var data = prepared;
        prepared = null;
        // One log per generated floor: other callers of OnAddMonsterGroup without a SetDungeon before are skipped.
        if (!armed) return;
        armed = false;
        if (!Enabled || rows == null || region != ForestRegion || phase != CampPhase) return;
        if (floors >= FloorLogCap)
        {
            if (floors++ == FloorLogCap) DelversHost.Info("CORE_BANDITS_GRAPH_CAP floors=" + FloorLogCap);
            return;
        }
        floors++;
        var pairs = new List<(DungeonPreRoomData Row, BanditCampRules.Node Node)>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row != null) pairs.Add((row, NodeOf(row)));
        }
        // The corridors of the same result: only when its rooms are the very list the game passed here.
        var corridorRows = data?.RawRooms != null && data.RawRooms.Pointer == rows.Pointer ? data.RawCorridors : null;
        if (corridorRows == null) DelversHost.Warning("CORE_BANDITS_GRAPH_FAILED stage=corridors reason=missing seed=" + seed);
        for (var i = 0; corridorRows != null && i < corridorRows.Count; i++)
        {
            var row = corridorRows[i];
            if (row != null) pairs.Add((row, NodeOf(row)));
        }
        var nodes = new List<BanditCampRules.Node>(pairs.Count);
        foreach (var pair in pairs) nodes.Add(pair.Node);
        Link(pairs, nodes);
        var longCorridor = LongCorridor();
        var result = BanditCampRules.Evaluate(nodes, QuestRoomKeys(), longCorridor, seed);
        DelversHost.Info(BanditCampRules.HeaderLine(result, region, floor, phase, seed, longCorridor));
        if (result.Problem != null) DelversHost.Warning(BanditCampRules.FailedLine(result, seed));
        foreach (var room in result.Rooms) DelversHost.Info(BanditCampRules.RoomLine(seed, room));
        var corridors = new SortedDictionary<string, (int Length, int Width, int Count)>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node.Type != BanditCampRules.CorridorType) continue;
            var key = string.IsNullOrEmpty(node.Key) ? node.Id : node.Key;
            var (length, width) = BanditCampRules.Size(node.Cells);
            corridors[key] = corridors.TryGetValue(key, out var seen) ? (seen.Length, seen.Width, seen.Count + 1) : (length, width, 1);
        }
        foreach (var entry in corridors)
            DelversHost.Info(BanditCampRules.CorridorLine(seed, phase, entry.Key, entry.Value.Length, entry.Value.Width, entry.Value.Count));
    }

    // Links: ConnectedRooms is checked pair by pair, without walking an IL2CPP set.
    private static void Link(List<(DungeonPreRoomData Row, BanditCampRules.Node Node)> pairs, List<BanditCampRules.Node> nodes)
    {
        foreach (var pair in pairs)
        {
            var connected = pair.Row.ConnectedRooms;
            if (connected == null) continue;
            foreach (var other in nodes)
                if (other.Id != pair.Node.Id && connected.Contains(other.Id)) pair.Node.Links.Add(other.Id);
        }
    }

    internal static int LongCorridor() => (int)Math.Max(0, CoreSettings.Number(LongCorridorKey) ?? 0);

    // The graph of a whole generator result, rooms and corridors (the seed scan, BanditCampTemplates).
    internal static BanditCampRules.Result Evaluate(DungeonPreMapData data, int seed)
        => BanditCampRules.Evaluate(Graph(data).Nodes, QuestRoomKeys(), LongCorridor(), seed);

    // A generator result's rooms and corridors as nodes with their links, each with its row (BanditCampBuilder builds on them).
    internal sealed class FloorGraph
    {
        internal readonly List<(DungeonPreRoomData Row, BanditCampRules.Node Node)> Pairs = new();
        internal readonly List<BanditCampRules.Node> Nodes = new();

        internal DungeonPreRoomData Row(string id)
        {
            foreach (var pair in Pairs)
                if (pair.Node.Id == id) return pair.Row;
            return null;
        }

        internal List<string> Ids()
        {
            var ids = new List<string>(Nodes.Count);
            foreach (var node in Nodes) ids.Add(node.Id);
            return ids;
        }
    }

    internal static FloorGraph Graph(DungeonPreMapData data)
    {
        var graph = new FloorGraph();
        foreach (var rows in new[] { data?.RawRooms, data?.RawCorridors })
            for (var i = 0; rows != null && i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null) graph.Pairs.Add((row, NodeOf(row)));
            }
        foreach (var pair in graph.Pairs) graph.Nodes.Add(pair.Node);
        Link(graph.Pairs, graph.Nodes);
        return graph;
    }

    private static BanditCampRules.Node NodeOf(DungeonPreRoomData row)
    {
        var node = new BanditCampRules.Node
        {
            Id = row.ID ?? "",
            Key = row.TableData?.Key ?? "",
            Type = (int)row.RoomType
        };
        var monsters = row.TableData?.SpawnMonster;
        if (monsters != null)
        {
            var list = new string[monsters.Length];
            for (var i = 0; i < monsters.Length; i++) list[i] = monsters[i] ?? "";
            node.Monsters = list;
        }
        var cells = row.RoomOccupiedPos;
        if (cells != null)
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                node.Cells.Add((cell.x, cell.y));
            }
        // Monster cells; -1 when the IL2CPP dictionary cannot be walked (the floor is still logged).
        var spawns = row.SpawnCategoryDic;
        if (spawns != null)
        {
            try
            {
                foreach (var entry in spawns)
                    if (entry.Value == SpawnCategory.MonsterSpawn) node.EnemyCells++;
            }
            catch (Exception)
            {
                node.EnemyCells = -1;
            }
        }
        return node;
    }

    // Room keys of the game's quest NPCs (NPCSpawnConditionTable.SpawnDungeonRoomKey).
    internal static HashSet<string> QuestRoomKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var rows = DataSheetManager.Instance?._npcSpawnConditionTable?.tableDatas;
        if (rows == null) return keys;
        for (var i = 0; i < rows.Count; i++)
        {
            var key = rows[i]?.SpawnDungeonRoomKey;
            if (!string.IsNullOrEmpty(key) && key != "NULL") keys.Add(key);
        }
        return keys;
    }

    // The generator and its level inputs derive from Edgar, which Core does not reference: found by the
    // game's own type names.
    private const string GeneratorType = "Refactor.Map.DungeonGenerator.DSDungeonPreDataGenerator, Assembly-CSharp";
    private const string LevelInputType = "Refactor.Map.DungeonGenerator.DSLevelInput, Assembly-CSharp";

    // Recon for a later seed scan, generating nothing: are the dungeon generator and the level inputs
    // reachable here, and is a dungeon floor loaded right now?
    internal static string Recon()
    {
        var generator = Il2CppSystem.Type.GetType(GeneratorType);
        var input = Il2CppSystem.Type.GetType(LevelInputType);
        var active = generator == null ? null : UnityEngine.Object.FindObjectsOfType(generator);
        var all = generator == null ? null : Resources.FindObjectsOfTypeAll(generator);
        var inputs = input == null ? null : Resources.FindObjectsOfTypeAll(input);
        var names = new List<string>();
        if (inputs != null)
            foreach (var asset in inputs)
                if (asset != null) names.Add(asset.name);
        var line = "CORE_BANDITS_GRAPH_RECON map=" + CoreGame.CurrentMap
            + " generatorType=" + (generator != null ? "found" : "missing") + " generators=" + (active?.Length ?? 0) + "/" + (all?.Length ?? 0)
            + " levelInputs=" + names.Count + " inputs=" + (names.Count == 0 ? "-" : string.Join("+", names))
            + " lastRegion=" + region + " lastFloor=" + floor + " lastPhase=" + phase + " lastSeed=" + seed;
        DelversHost.Info(line);
        return line;
    }
}

// The floor being generated: region, floor, phase and seed (generation calls it before the rooms).
[ProbeGroup(CoreProbeRules.OneShot)]
[HarmonyPatch(typeof(RawDungeonDataContainer), nameof(RawDungeonDataContainer.SetDungeon),
    new[] { typeof(DungeonRegion), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int) })]
internal static class BanditCampProbeSetDungeonPatch
{
    private static void Postfix(RawDungeonDataContainer __instance, DungeonRegion region, int floor, int currentSeed)
    {
        try { BanditCampProbe.OnSetDungeon(__instance, region, floor, currentSeed); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_GRAPH_FAILED stage=set-dungeon: " + ex.Message); }
    }
}

// The generator's result of the floor (rooms and corridors), before the strategy runs it: GeneratePrepared
// only builds its routine (a coroutine, own method, one caller), whose preData is generationResult.PreData.
[ProbeGroup(CoreProbeRules.OneShot)]
[HarmonyPatch(typeof(DungeonGeneratorStrategy), "GeneratePrepared")]
internal static class BanditCampProbePreparedPatch
{
    private static void Postfix(DungeonGenerationAttemptResult generationResult)
    {
        try { BanditCampProbe.OnGeneratePrepared(generationResult); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_GRAPH_FAILED stage=prepared: " + ex.Message); }
    }
}

// The floor's rooms (RawRooms only), right after SetDungeon (DungeonGeneratorStrategy.GeneratePrepared).
[ProbeGroup(CoreProbeRules.OneShot)]
[HarmonyPatch(typeof(RuntimeDungeonData), nameof(RuntimeDungeonData.OnAddMonsterGroup))]
internal static class BanditCampProbeRoomsPatch
{
    private static void Postfix(Il2CppSystem.Collections.Generic.List<DungeonPreRoomData> rowRooms)
    {
        try { BanditCampProbe.OnAddMonsterGroup(rowRooms); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_GRAPH_FAILED stage=rooms: " + ex.Message); }
    }
}
