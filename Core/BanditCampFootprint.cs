using System.Globalization;
using HarmonyLib;
using UnityEngine;
#if BEPINEX
using BuildingAreaPositions = global::Refactor.Building.BuildingAreaPositions;
using BuildingRotation = global::Refactor.Building.BuildingRotation;
#else
using BuildingAreaPositions = Il2CppRefactor.Building.BuildingAreaPositions;
using BuildingRotation = Il2CppRefactor.Building.BuildingRotation;
#endif

namespace DungeonSettlersDelvers.Core;

// The footprint of the mirrored camp's buildings mirrored with their picture, so the game blocks the cells
// it draws, and only theirs (BanditCampFootprintRules.Applies). The game keeps no footprint and saves none; it computes one for
// a building and hands it on at two places that know the building and its map: EntityDataContainer.OnSpawnBuilding, where its
// spawn (BuildingSpawner.GenerateBuilding) and its load from a save (EntityLoadHelper.LoadBuilding) enter the footprint into the
// map, and BuildingDespawner.GetBuildingAreaPositions, which gives the footprint its removal takes out. There the cells are
// mirrored in place when the building is one of the camp's marked buildings (their GUIDs in Core's block,
// CoreSaveData.CampFlips) or the one Core's furnishing is spawning as such. Without Core a load gives the unmirrored footprint.
// (A postfix on the BuildingAreaPositions constructor did not take effect in the game.)
internal static class BanditCampFootprint
{
    private const int LineCap = 40;

    private static CoreSaveData readFrom;
    private static HashSet<Guid> marked = new();
    private static bool spawningMarked;
    // Capped per place, so the rare removal still logs after the many entries of spawns and loads.
    private static readonly Dictionary<string, int> lines = new();

    internal static void ResetLifecycle()
    {
        readFrom = null;
        marked = new();
        spawningMarked = false;
        lines.Clear();
    }

    // Core's furnishing spawns a marked building: its footprint, entered inside the spawn, is mirrored.
    internal static void BeginMarkedSpawn() => spawningMarked = true;

    internal static void EndMarkedSpawn() => spawningMarked = false;

    // The camp's block changed: its marked buildings are read again.
    internal static void Reload() => readFrom = null;

    internal static void OnFootprint(string where, MapType map, IEntity entity, BuildingAreaPositions positions)
    {
        if (positions == null || entity == null) return;
        if (!spawningMarked)
        {
            var data = CoreSaveStore.Current;
            if (!ReferenceEquals(readFrom, data))
            {
                marked = data?.CampFlips(out _) ?? new HashSet<Guid>();
                readFrom = data;
            }
            if (marked.Count == 0) return;
        }
        if (!Guid.TryParse(entity.Guid.ToString(), out var guid)) return;
        if (!BanditCampFootprintRules.Applies(guid, map == MapType.Dungeon, spawningMarked, marked)) return;
        Mirror(positions);
        lines.TryGetValue(where, out var logged);
        lines[where] = logged + 1;
        if (logged < LineCap)
            DelversHost.Info(string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_FOOTPRINT where={0} building={1:D} cells={2}",
                where, guid, positions.Blocked?.Count ?? 0));
    }

    private static void Mirror(BuildingAreaPositions positions)
    {
        var lists = new[] { positions.Blocked, positions.NotVisible, positions.Occupied, positions.Interactable };
        var mirrored = BanditCampFootprintRules.Mirror(lists.Select(Read).ToList());
        for (var i = 0; i < lists.Length; i++)
            for (var j = 0; lists[i] != null && j < lists[i].Count; j++)
                lists[i][j] = new Vector2Int(mirrored[i][j].X, mirrored[i][j].Y);
    }

    private static IReadOnlyList<(int X, int Y)> Read(Il2CppSystem.Collections.Generic.List<Vector2Int> list)
    {
        var cells = new List<(int X, int Y)>();
        for (var i = 0; list != null && i < list.Count; i++) cells.Add((list[i].x, list[i].y));
        return cells;
    }
}

// Put on by hand at the end of the boot (LatePatches), like the other late patches; both go on together.
internal static class BanditCampFootprintInstall
{
    private const string Name = "camp-footprint";
    private static readonly LatePatchGate gate = new();

    internal static void ResetLifecycle() => gate.Reset();

    internal static void TryInstall(string at)
    {
        if (gate.State != LatePatchState.Waiting || !gate.ShouldTry(true)) return;
        gate.Record(Install());
        DelversHost.Info(LatePatchGate.Line(Name, at, gate.State));
    }

    private static bool Install()
    {
        try
        {
            var owner = DelversCoreRuntime.HarmonyOwner;
            if (owner == null) return false;
            var enter = AccessTools.Method(typeof(EntityDataContainer), nameof(EntityDataContainer.OnSpawnBuilding),
                new[] { typeof(MapType), typeof(BuildingEntity), typeof(Vector2Int), typeof(BuildingAreaPositions), typeof(BuildingRotation) });
            var removal = AccessTools.Method(typeof(BuildingDespawner), "GetBuildingAreaPositions", new[] { typeof(MapType), typeof(IEntity) });
            if (enter == null || removal == null) return false;
            var harmony = new Harmony(owner);
            harmony.Patch(enter, prefix: new HarmonyMethod(AccessTools.Method(typeof(BanditCampFootprintPatch), "Entered")));
            harmony.Patch(removal, postfix: new HarmonyMethod(AccessTools.Method(typeof(BanditCampFootprintPatch), "Removed")));
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_PATCH_LATE_FAILED name=" + Name + " error=" + ex);
            return false;
        }
    }
}

internal static class BanditCampFootprintPatch
{
    private static int failures;

    private static void Entered(MapType map, BuildingEntity entity, BuildingAreaPositions areaPositions)
    {
        if (failures >= 3) return;
        try { BanditCampFootprint.OnFootprint("enter", map, entity?.Cast<IEntity>(), areaPositions); }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Removed(MapType map, IEntity entity, BuildingAreaPositions __result)
    {
        if (failures >= 3) return;
        try { BanditCampFootprint.OnFootprint("removal", map, entity, __result); }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Fail(Exception ex)
    {
        if (++failures <= 3) DelversHost.Warning("CORE_BANDITS_CAMP_FOOTPRINT_FAILED: " + ex.Message);
    }
}
