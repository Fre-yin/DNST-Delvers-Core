using HarmonyLib;
using UnityEngine;
#if BEPINEX
using BuildingRotation = global::Refactor.Building.BuildingRotation;
using ConstructionMode = global::Refactor.Control.ConstructionMode;
#else
using BuildingRotation = Il2CppRefactor.Building.BuildingRotation;
using ConstructionMode = Il2CppRefactor.Control.ConstructionMode;
#endif

namespace DungeonSettlersDelvers.Core;

// The camp built into the floor (BanditCampBuilder) furnished once, on the first game tick after the arrival on that floor, the
// way the Modding Toolkit furnishes its custom rooms (in the game): buildings through the construction mode's
// spawn helper, neutral (BanditCampFurnishRules), on the file's floor tiles laid first (the helper's SpawnFloor and
// SpawnFloorDeco, as the Modding Toolkit lays them), every one of the file's (the room is drawn as it is, also
// where the game's own build check would refuse a cell; the helper checks nothing itself, the check only reports), items exactly
// on their cells, the waterfall and the bridge
// (BanditRoomFeatureView.Place), the four bandits through the game's SpawnUnitRequested as enemies. Then nothing more runs per
// tick than one check; a floor loaded from a save brings its camp's things itself and is never furnished again.
//
// The wagon: gathered, the game drops a building's loot (DropHelper.DropFromBuilding) and leaves its remnant. For the camp's wagon,
// noted with the floor's seed in Core's block (CoreSaveData.CampWagon), the camp's own loot drops instead
// (BanditCampWagonDropPatch); without Core it drops the wagon's native loot. Room parts of earlier floors leave the block when a
// camp is furnished (BanditRoomFeatureRules.Prune).
internal static class BanditCampFurnish
{
    private static BanditCampBuilt done;

    internal static void ResetLifecycle() => done = null;

    internal static void OnGameTick()
    {
        var camp = BanditCampBuilder.Last;
        if (camp == null || ReferenceEquals(camp, done)) return;
        if (!CoreGame.CampaignReady || CoreGame.CurrentMap != MapType.Dungeon) return;
        var dungeon = CoreGame.Flow?._campaignData?.DungeonDataContainer;
        if (dungeon == null) return;
        // The floor's rooms come with the arrival; until then the next tick asks again. On the way down the floor before is still
        // shown for a while after the camp's floor was built, so only the camp's own room ends the wait (one lookup per tick).
        var rooms = dungeon.GetRooms();
        if (rooms == null || rooms.Count == 0) return;
        if ((int)dungeon.GetCurrentSelectedRegion() != camp.Region || dungeon.GetCurrentFloor() != camp.Floor || !rooms.ContainsKey(camp.CampId)) return;
        done = camp;
        var area = AreaBox(rooms[camp.CampId]);
        if (area == null)
        {
            DelversHost.Warning(BanditCampBuildRules.FailedLine(camp.Seed, camp.Region, camp.Floor, "no-area", BanditCampRules.LogId(camp.CampId)));
            return;
        }
        Furnish(camp, area.Value, dungeon.GetCurrentSeed());
    }

    private static (int X, int Y)? AreaBox(DungeonRoomData room)
    {
        var cells = room?.GetArea();
        if (cells == null || cells.Count == 0) return null;
        int minX = int.MaxValue, minY = int.MaxValue;
        for (var i = 0; i < cells.Count; i++)
        {
            minX = Math.Min(minX, cells[i].x);
            minY = Math.Min(minY, cells[i].y);
        }
        return (minX, minY);
    }

    private static void Furnish(BanditCampBuilt camp, (int X, int Y) area, int floorSeed)
    {
        var room = camp.Room;
        var (ox, oy) = BanditCampFurnishRules.Origin(room, area.X, area.Y);
        DelversHost.Info(BanditCampBuildRules.MapLine(camp.CampId, camp.Plan, room.Width, room.Height, ox - camp.Plan.CampOriginX, oy - camp.Plan.CampOriginY));
        int buildings = 0, items = 0, features = 0, units = 0, skipped = 0, forced = 0, doubled = 0;
        var wagon = Guid.Empty;
        var flips = new List<Guid>();
        var data = CoreSaveStore.Current;
        if (data != null)
        {
            var parts = data.RoomFeatures();
            if (BanditRoomFeatureRules.Prune(parts, (int)MapType.Dungeon, floorSeed) > 0) data.SetRoomFeatures(parts);
        }
        var mode = CoreGame.Flow?._eventProcessor?.ControlModeProcessor?.GetProcessor(ControlMode.Construction)?.TryCast<ConstructionMode>();
        LayFloors(camp.CampId, room, mode, ox, oy);
        foreach (var building in room.Buildings)
        {
            var cell = new Vector2Int(ox + building.X, oy + building.Y);
            var rotation = (BuildingRotation)building.Rotation;
            try
            {
                if (mode == null || !ConstructionCheckTool.IsBuildingDataExist(building.Key)) { skipped++; continue; }
                if (!ConstructionCheckTool.IsBuildingSpaceAvailable(building.Key, cell, rotation, CoreGame.InMap)) forced++;
                if (OnBuilding(building.Key, cell, rotation)) doubled++;
                // A building marked FlipX gets its footprint mirrored while it is spawned (BanditCampFootprint); only with Core's
                // block, which keeps it marked for its removal and a load (else the cells the game frees would not be the blocked ones).
                if (building.FlipX && data != null) BanditCampFootprint.BeginMarkedSpawn();
                BuildingEntity spawned;
                try { spawned = mode._buildingSpawnHelper.SpawnBuilding(building.Key, cell, rotation, FactionType.Neutral); }
                finally { BanditCampFootprint.EndMarkedSpawn(); }
                buildings++;
                if (spawned != null && Guid.TryParse(spawned.Guid.ToString(), out var guid))
                {
                    if (BanditCampFurnishRules.IsWagon(building.Key)) wagon = guid;
                    if (building.FlipX) flips.Add(guid);
                }
            }
            catch (Exception) { skipped++; }
        }
        // The buildings stand with their mirrored footprints: the camp's entry is written whatever follows them throws.
        try
        {
            var helper = CoreGame.Flow?._campaignHelperSet?.EntityLifecycleHelper;
            foreach (var item in room.Items)
            {
                try
                {
                    helper.SpawnItem(MapType.Dungeon, item.Key, null, item.Count, new Il2CppSystem.Nullable<float>(), new Vector2Int(ox + item.X, oy + item.Y));
                    items++;
                }
                catch (Exception) { skipped++; }
            }
            foreach (var feature in room.Features)
            {
                var result = BanditRoomFeatureView.Place(feature.Kind, ox + feature.X, oy + feature.Y, checkTerrain: false);
                if (result.StartsWith(BanditRoomFeatureRules.Placed, StringComparison.Ordinal)) features++;
                else skipped++;
            }
            foreach (var spawn in room.Spawns)
            {
                try
                {
                    CoreGame.Enqueue(new SpawnUnitRequested(MapType.Dungeon, spawn.Kind, new Vector2Int(ox + spawn.X, oy + spawn.Y), FactionType.Enemy,
                        new Il2CppSystem.Nullable<Il2CppSystem.Guid>(), new Il2CppSystem.Collections.Generic.List<string>(),
                        new Il2CppSystem.Nullable<AnimationType>()).Cast<IEventData>());
                    units++;
                }
                catch (Exception) { skipped++; }
            }
        }
        finally
        {
            var campFile = BanditCampRoomFiles.FileOf(camp.Variant);
            var wakes = BanditCampWakeRules.WakesOnEnter(campFile);
            data?.SetCamp(floorSeed, wagon, flips, (ox, oy, ox + room.Width - 1, oy + room.Height - 1), wakes);
            if (!wakes) DelversHost.Info(BanditCampWakeRules.OffLine(campFile));
            BanditCampWake.Reload();
            CampGuardianStones.Reload();
            BanditCampFlip.Reload();
            BanditCampFootprint.Reload();
        }
        DelversHost.Info(BanditCampFurnishRules.SpawnedLine(camp.CampId, units, buildings, items, features, skipped, wagon != Guid.Empty, forced, doubled));
    }

    // The floor tiles first: the buildings stand on them. The game keeps them in its floor layer and saves them itself.
    private static void LayFloors(string campId, BanditCampRoom room, ConstructionMode mode, int ox, int oy)
    {
        if (room.Floors.Count == 0 && room.FloorDecos.Count == 0) return;
        int floors = 0, decos = 0, skipped = 0;
        foreach (var (list, deco) in new[] { (room.Floors, false), (room.FloorDecos, true) })
            foreach (var floor in list)
            {
                try
                {
                    if (mode == null) { skipped++; continue; }
                    var cell = new Vector2Int(ox + floor.X, oy + floor.Y);
                    if (deco)
                    {
                        mode._buildingSpawnHelper.SpawnFloorDeco(floor.Key, cell);
                        decos++;
                    }
                    else
                    {
                        mode._buildingSpawnHelper.SpawnFloor(floor.Key, cell);
                        floors++;
                    }
                }
                catch (Exception) { skipped++; }
            }
        DelversHost.Info(BanditCampFurnishRules.FloorsLine(campId, floors, decos, skipped));
    }

    // Whether a building already stands on a cell of the footprint (the game keeps a set of buildings per cell, so both stand; the
    // file decides, the line reports it).
    private static bool OnBuilding(string key, Vector2Int cell, BuildingRotation rotation)
    {
        var area = ConstructionCheckTool.GetBuildingAreaPositions(key, cell, rotation);
        var inMap = CoreGame.InMap;
        if (area == null || inMap == null) return false;
        foreach (var cells in new[] { area.Blocked, area.Occupied })
            for (var i = 0; cells != null && i < cells.Count; i++)
            {
                var entities = inMap.GetAllEntitiesOnPosition(cells[i]);
                for (var j = 0; entities != null && j < entities.Count; j++)
                    if (entities[j]?.TryCast<BuildingEntity>() != null) return true;
            }
        return false;
    }

    // The camp's own loot on the wagon's cell, through the game's item spawn (the game lays a dropped stack there or beside it).
    internal static bool DropWagonLoot(MapType map, BuildingEntity building, Il2CppSystem.Collections.Generic.List<ItemEntity> dropped)
    {
        var data = CoreSaveStore.Current;
        if (data == null || building == null || !BanditCampFurnishRules.IsWagon(building.Key) || !data.CampWagon(out var seed, out var wagon)) return false;
        if (!Guid.TryParse(building.Guid.ToString(), out var guid) || guid != wagon || seed != BanditRoomFeatureView.FloorSeed()) return false;
        // Through the game's own spawn request, as a bandit's kit drops (CoreUnits.DropKit): the game lays each stack on the
        // wagon's cell or a free one beside it. Spawned straight onto the wagon's own, taken cell, only the book stayed in the
        // .
        var at = CoreGame.InMap.GetLogicalPosition(building.Guid);
        var count = 0;
        foreach (var (key, amount) in BanditCampFurnishRules.WagonLoot)
        {
            CoreGame.Enqueue(new SpawnItemRequested(map, key, at, amount).Cast<IEventData>());
            count += amount;
        }
        DelversHost.Info(BanditCampFurnishRules.WagonLine(guid.ToString("D"), BanditCampFurnishRules.WagonLoot.Length, count));
        data.ClearCampWagon();
        return true;
    }
}

[HarmonyPatch(typeof(DropHelper), nameof(DropHelper.DropFromBuilding))]
internal static class BanditCampWagonDropPatch
{
    private static bool Prefix(MapType map, BuildingEntity building, ref Il2CppSystem.Collections.Generic.List<ItemEntity> __result)
    {
        try
        {
            var dropped = new Il2CppSystem.Collections.Generic.List<ItemEntity>();
            if (!BanditCampFurnish.DropWagonLoot(map, building, dropped)) return true;
            __result = dropped;
            return false;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDITS_CAMP_WAGON_FAILED (the wagon drops its own loot): " + ex.Message);
            return true;
        }
    }
}
