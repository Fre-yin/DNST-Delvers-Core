using System.Collections;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
#if BEPINEX
using BindingRoomFilterMode = global::Refactor.Map.DungeonGenerator.BindingRoomFilterMode;
using DoorsGrid2D = global::Edgar.Unity.DoorsGrid2D;
using DSDungeonPreDataGenerator = global::Refactor.Map.DungeonGenerator.DSDungeonPreDataGenerator;
using DSLevelInput = global::Refactor.Map.DungeonGenerator.DSLevelInput;
using DSRoomInfo = global::Refactor.Map.DungeonGenerator.DSRoomInfo;
using DungeonGenerationAttemptResult = global::Refactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
using DungeonRoomDataManager = global::Refactor.Map.DungeonGenerator.DungeonRoomDataManager;
using ManualDoorModeGrid2D = global::Edgar.GraphBasedGenerator.Grid2D.ManualDoorModeGrid2D;
using RoomTemplateLoaderGrid2D = global::Edgar.Unity.RoomTemplateLoaderGrid2D;
using RoomTemplateSettingsGrid2D = global::Edgar.Unity.RoomTemplateSettingsGrid2D;
using SimpleDoorModeGrid2D = global::Edgar.GraphBasedGenerator.Grid2D.SimpleDoorModeGrid2D;
#else
using BindingRoomFilterMode = Il2CppRefactor.Map.DungeonGenerator.BindingRoomFilterMode;
using DoorsGrid2D = Il2CppEdgar.Unity.DoorsGrid2D;
using DSDungeonPreDataGenerator = Il2CppRefactor.Map.DungeonGenerator.DSDungeonPreDataGenerator;
using DSLevelInput = Il2CppRefactor.Map.DungeonGenerator.DSLevelInput;
using DSRoomInfo = Il2CppRefactor.Map.DungeonGenerator.DSRoomInfo;
using DungeonGenerationAttemptResult = Il2CppRefactor.Map.DungeonGenerator.DungeonGenerationAttemptResult;
using DungeonRoomDataManager = Il2CppRefactor.Map.DungeonGenerator.DungeonRoomDataManager;
using ManualDoorModeGrid2D = Il2CppEdgar.GraphBasedGenerator.Grid2D.ManualDoorModeGrid2D;
using RoomTemplateLoaderGrid2D = Il2CppEdgar.Unity.RoomTemplateLoaderGrid2D;
using RoomTemplateSettingsGrid2D = Il2CppEdgar.Unity.RoomTemplateSettingsGrid2D;
using SimpleDoorModeGrid2D = Il2CppEdgar.GraphBasedGenerator.Grid2D.SimpleDoorModeGrid2D;
#endif

namespace DungeonSettlersDelvers.Core;

// Step 1 of the camp with its own long corridor (way c), reading only:
// - Probe (CampTemplatesForTest): every template of the forest floor-4 level input with type, weight,
//   size of its ground, repeat mode, door mode and the door lines Edgar sees (door start places), plus
//   the level graphs with their rooms and connections (CORE_BANDITS_TEMPLATE, CORE_BANDITS_LEVELGRAPH).
// - Seed scan (CampSeedScanForTest, test campaigns at the base only): the game's own
//   GenerateForSeedScan for count seeds, each result with its graph as the camp rules see it
//   (CORE_BANDITS_SEEDSCAN*), the generated level cleared afterwards (ResetGenerateInfo). It needs the
//   floor's map size, taken from the last real generation of a forest floor 4 (Generate).
internal static class BanditCampTemplates
{
    private const int ScanCap = 200;
    private const float ScanTimeoutSeconds = 60f;

    // Map size per (region, phase) from the game's own generation: Generate gives region, floor and
    // size, SetDungeon right after it the phase.
    private static readonly Dictionary<(int Region, int Phase), Vector2Int> maxSizes = new();
    private static (int Region, int Floor, Vector2Int Size)? lastGenerate;

    private static bool scanning;
    private static BanditCampRules.SeedScanSummary summary = new();

    internal static void ResetLifecycle()
    {
        maxSizes.Clear();
        lastGenerate = null;
        scanning = false;
        summary = new BanditCampRules.SeedScanSummary();
    }

    internal static void OnGenerate(MapGenerateInfo info) => lastGenerate = ((int)info.DungeonRegion, info.Floor, info.MaxSize);

    internal static void OnSetDungeon(RawDungeonDataContainer container, DungeonRegion region, int floor)
    {
        if (container == null || lastGenerate is not { } last || last.Region != (int)region || last.Floor != floor) return;
        maxSizes[((int)region, container._currentPhase)] = last.Size;
    }

    // ---- probe ----

    internal static string Probe()
    {
        var phase = BanditCampProbe.CampPhase;
        try
        {
            var input = LevelInput(phase);
            if (input == null)
            {
                DelversHost.Warning("CORE_BANDITS_TEMPLATE_FAILED reason=no-level-input phase=" + phase);
                return "error=no-level-input";
            }
            var graphs = input.LevelGraphs;
            for (var i = 0; graphs != null && i < graphs.Count; i++)
            {
                var graph = graphs[i];
                if (graph == null) continue;
                var types = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var rooms = graph.Rooms;
                for (var r = 0; rooms != null && r < rooms.Count; r++)
                {
                    var name = rooms[r]?.TryCast<DSRoom>()?.Type.ToString() ?? "?";
                    types[name] = types.TryGetValue(name, out var n) ? n + 1 : 1;
                }
                DelversHost.Info(BanditCampRules.LevelGraphLine(phase, graph.name, graph.Weight, rooms?.Count ?? 0, graph.Connections?.Count ?? 0, types));
            }
            var dict = input.Templates?._templates;
            var count = 0;
            var failed = 0;
            foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
            {
                if (dict == null || !dict.ContainsKey(type)) continue;
                var list = dict[type]?.List;
                for (var i = 0; list != null && i < list.Count; i++)
                {
                    if (Template(phase, type, list[i])) count++;
                    else failed++;
                }
            }
            var line = string.Format(CultureInfo.InvariantCulture, "templates={0} failed={1} graphs={2} maxSize={3}",
                count, failed, graphs?.Count ?? 0, maxSizes.TryGetValue((BanditCampProbe.ForestRegion, phase), out var size) ? size.x + "x" + size.y : "-");
            DelversHost.Info("CORE_BANDITS_TEMPLATE_DONE phase=" + phase + " " + line);
            return line;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDITS_TEMPLATE_FAILED " + ex.GetType().Name + ": " + ex.Message);
            return "error=" + ex.GetType().Name;
        }
    }

    // One template's line; false when it could not be read (logged with the reason).
    private static bool Template(int phase, RoomType type, DSRoomInfo info)
    {
        if (info == null) return false;
        var key = info.TableData?.Key ?? info.name;
        try
        {
            var cells = new List<(int X, int Y)>();
            var ground = info.GroundArea;
            for (var i = 0; ground != null && i < ground.Count; i++) cells.Add((ground[i].x, ground[i].y));
            var (length, width) = BanditCampRules.Size(cells);
            var settings = info.GetComponent<RoomTemplateSettingsGrid2D>();
            var repeat = settings != null ? (int)settings.RepeatMode : -1;
            var doors = info.GetComponent<DoorsGrid2D>();
            int mode = -1, lines = -1, starts = -1, doorLength = -1;
            if (doors != null)
            {
                mode = (int)doors.SelectedMode;
                doorLength = mode == 0 ? doors.SimpleDoorModeData?.DoorLength ?? -1 : mode == 2 ? doors.HybridDoorModeData?.DefaultLength ?? -1 : doors.DoorLength;
                try
                {
                    // Through the concrete door modes: the interop does not carry the generic base interface.
                    var doorMode = doors.GetDoorMode();
                    var shape = RoomTemplateLoaderGrid2D.GetPolygonFromRoomTemplate(info.gameObject);
                    var simple = doorMode?.TryCast<SimpleDoorModeGrid2D>();
                    var manual = simple == null ? doorMode?.TryCast<ManualDoorModeGrid2D>() : null;
                    var found = simple != null ? simple.GetDoors(shape) : manual?.GetDoors(shape);
                    if (found != null)
                    {
                        lines = found.Count;
                        starts = 0;
                        for (var i = 0; i < found.Count; i++) starts += found[i].Line.Length + 1;
                    }
                }
                catch (Exception ex)
                {
                    DelversHost.Warning("CORE_BANDITS_TEMPLATE_DOORS_FAILED key=" + BanditCampRules.LogId(key) + ": " + ex.GetType().Name);
                }
            }
            DelversHost.Info(BanditCampRules.TemplateLine(phase, (int)type, key, info.TableData?.Weight ?? 0, length, width, repeat, mode, lines, starts, doorLength));
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDITS_TEMPLATE_FAILED key=" + BanditCampRules.LogId(key) + ": " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static DSLevelInput LevelInput(int phase)
    {
        var manager = UnityEngine.Object.FindObjectOfType<DungeonRoomDataManager>();
        return manager != null ? manager.GetLevelInput((DungeonRegion)BanditCampProbe.ForestRegion, phase) : null;
    }

    // ---- seed scan ----

    internal static string StartScan(int firstSeed, int count)
    {
        if (scanning) return "error=busy";
        if (CoreGame.CurrentMap != MapType.Base) return "error=not-at-base";
        var phase = BanditCampProbe.CampPhase;
        if (!maxSizes.TryGetValue((BanditCampProbe.ForestRegion, phase), out var maxSize)) return "error=no-max-size (enter a forest floor 4 once)";
        var input = LevelInput(phase);
        if (input == null) return "error=no-level-input";
        var generator = UnityEngine.Object.FindObjectOfType<DSDungeonPreDataGenerator>();
        if (generator == null) return "error=no-generator";
        count = Math.Clamp(count, 1, ScanCap);
        scanning = true;
        summary = new BanditCampRules.SeedScanSummary();
        try { DelversHost.StartCoroutine(Scan(generator, input, maxSize, firstSeed, count)); }
        catch (Exception ex)
        {
            scanning = false;
            return "error=" + ex.GetType().Name;
        }
        var line = string.Format(CultureInfo.InvariantCulture, "started first={0} count={1} maxSize={2}x{3}", firstSeed, count, maxSize.x, maxSize.y);
        DelversHost.Info("CORE_BANDITS_SEEDSCAN_START " + line);
        return line;
    }

    internal static string ScanStatus() => (scanning ? "running " : "idle ") + BanditCampRules.SeedScanDoneText(summary);

    private static IEnumerator Scan(DSDungeonPreDataGenerator generator, DSLevelInput input, Vector2Int maxSize, int firstSeed, int count)
    {
        try
        {
            for (var i = 0; i < count; i++)
            {
                var seed = firstSeed + i;
                var before = generator.LastGenerationResult;
                var started = Time.realtimeSinceStartup;
                string error = null;
                try
                {
                    generator.ResetGenerateInfo();
                    var routine = generator.GenerateForSeedScan(maxSize, input, new Il2CppSystem.Collections.Generic.List<int>(), seed,
                        false, true, BindingRoomFilterMode.ExcludeAllBindingRooms);
                    generator.StartCoroutine(routine);
                }
                catch (Exception ex) { error = ex.GetType().Name; }
                while (error == null)
                {
                    yield return null;
                    var now = generator.LastGenerationResult;
                    var changed = now != null && (before == null || now.Pointer != before.Pointer);
                    if (changed && BanditCampRules.ScanResultFinal(true, now.Succeeded, now.AttemptCount, (int)now.FailReason)) break;
                    if (Time.realtimeSinceStartup - started > ScanTimeoutSeconds) error = "timeout";
                }
                Record(seed, error, error == null ? generator.LastGenerationResult : null, Time.realtimeSinceStartup - started);
                try { generator.ResetGenerateInfo(); }
                catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_SEEDSCAN_FAILED stage=reset: " + ex.Message); }
                yield return null;
            }
        }
        finally
        {
            scanning = false;
            DelversHost.Info("CORE_BANDITS_SEEDSCAN_DONE " + BanditCampRules.SeedScanDoneText(summary));
        }
    }

    // Whether the camp would come on the scanned floor under the rule in use (CORE_BANDITS_SEEDSCAN_CAMP).
    private static void Camp(DungeonPreMapData pre, int seed)
    {
        string reason;
        try { reason = BanditCampBuilder.Measure(pre, seed); }
        catch (Exception ex) { reason = "error-" + ex.GetType().Name; }
        summary.AddCamp(reason);
        DelversHost.Info("CORE_BANDITS_SEEDSCAN_CAMP seed=" + seed + " camp=" + (reason ?? BanditCampRules.CampPlaced));
    }

    private static void Record(int seed, string error, DungeonGenerationAttemptResult result, float seconds)
    {
        try
        {
            var ok = error == null && result != null && result.Succeeded;
            var reason = error ?? (result != null ? result.FailReason.ToString() : "no-result");
            var graph = ok && result.PreData != null ? BanditCampProbe.Evaluate(result.PreData, seed) : null;
            summary.Add(ok, reason, graph);
            if (graph != null) Camp(result.PreData, seed);
            DelversHost.Info(BanditCampRules.SeedScanLine(seed, ok, reason, result?.UsedSeed ?? 0, result?.AttemptCount ?? 0, graph, seconds));
        }
        catch (Exception ex)
        {
            summary.Add(false, "record-" + ex.GetType().Name, null);
            DelversHost.Warning("CORE_BANDITS_SEEDSCAN_FAILED stage=record seed=" + seed + ": " + ex.Message);
        }
    }
}

// The size of the floor being generated (the scan generates the same floor at the same size).
[HarmonyPatch(typeof(DSDungeonPreDataGenerator), "Generate", new[] { typeof(MapGenerateInfo) })]
internal static class BanditCampTemplatesGeneratePatch
{
    private static void Prefix(MapGenerateInfo info)
    {
        try { BanditCampTemplates.OnGenerate(info); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_SEEDSCAN_FAILED stage=generate: " + ex.Message); }
    }
}

// The phase of that floor, right after Generate (the six-argument SetDungeon, as BanditCampProbe).
[HarmonyPatch(typeof(RawDungeonDataContainer), nameof(RawDungeonDataContainer.SetDungeon),
    new[] { typeof(DungeonRegion), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int) })]
internal static class BanditCampTemplatesSetDungeonPatch
{
    private static void Postfix(RawDungeonDataContainer __instance, DungeonRegion region, int floor)
    {
        try { BanditCampTemplates.OnSetDungeon(__instance, region, floor); }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDITS_SEEDSCAN_FAILED stage=set-dungeon: " + ex.Message); }
    }
}
