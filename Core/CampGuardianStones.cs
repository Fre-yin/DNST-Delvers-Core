using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The camp's guardian stones give their aura (CampGuardianStoneRules). On the camp's floor, every 10 s of game time: the stones
// are found once per floor visit, in the camp's box from Core's block (CoreSaveData.CampBox, so also after a load), and each
// pulse looks at them again at their cells, so a destroyed stone gives nothing. Each living party member closer than 7 to a
// stone gets AFFECTER_LesserGuardianAura for 10 s through the game's own apply. CORE_CAMP_GUARDIAN_STONES once per floor,
// CORE_CAMP_GUARDIAN_AURA per aura given, capped.
internal static class CampGuardianStones
{
    private static CoreSaveData readFrom;
    private static int seed;
    private static (int MinX, int MinY, int MaxX, int MaxY)? box;
    private static List<(Il2CppSystem.Guid Guid, Vector2Int Cell)> stones;
    private static float lastPulse = -1f;
    private static int lines;

    internal static void ResetLifecycle()
    {
        readFrom = null;
        box = null;
        stones = null;
        lastPulse = -1f;
    }

    internal static void OnGameTick()
    {
        var data = CoreSaveStore.Current;
        if (!ReferenceEquals(readFrom, data))
        {
            readFrom = data;
            seed = 0;
            box = data?.CampBox(out seed);
            stones = null;
            lastPulse = -1f;
        }
        if (box == null || CoreGame.CurrentMap != MapType.Dungeon || seed != BanditRoomFeatureView.FloorSeed())
        {
            stones = null;
            return;
        }
        var now = GameClock.Now;
        if (!CampGuardianStoneRules.Due(lastPulse, now)) return;
        lastPulse = now;
        var inMap = CoreGame.InMap;
        if (inMap == null) return;
        if (stones == null)
        {
            stones = Find(inMap, box.Value);
            DelversHost.Info(CampGuardianStoneRules.FoundLine(stones.Count, seed));
        }
        if (stones.Count > 0) Pulse(inMap);
    }

    // Reloads the camp's box after the camp was furnished (its block changed).
    internal static void Reload() => readFrom = null;

    private static List<(Il2CppSystem.Guid, Vector2Int)> Find(IInMapDataSupplier inMap, (int MinX, int MinY, int MaxX, int MaxY) camp)
    {
        var found = new List<(Il2CppSystem.Guid, Vector2Int)>();
        for (var x = camp.MinX; x <= camp.MaxX; x++)
            for (var y = camp.MinY; y <= camp.MaxY; y++)
            {
                var cell = new Vector2Int(x, y);
                var stone = StoneAt(inMap, cell);
                if (stone != null) found.Add((stone.Guid, cell));
            }
        return found;
    }

    // A living neutral guardian stone standing on the cell, else null; a stone of the party's own stays out.
    private static BuildingEntity StoneAt(IInMapDataSupplier inMap, Vector2Int cell)
    {
        var entities = inMap.GetAllEntitiesOnPosition(cell);
        for (var i = 0; entities != null && i < entities.Count; i++)
        {
            var building = entities[i]?.TryCast<BuildingEntity>();
            if (building == null || building.IsDead || building.Key != CampGuardianStoneRules.StoneKey || building.Faction != FactionType.Neutral) continue;
            return building;
        }
        return null;
    }

    private static void Pulse(IInMapDataSupplier inMap)
    {
        var standing = new List<(BuildingEntity Stone, Vector2Int Cell)>();
        foreach (var (guid, cell) in stones)
        {
            var stone = StoneAt(inMap, cell);
            if (stone != null && stone.Guid == guid) standing.Add((stone, cell));
        }
        if (standing.Count == 0) return;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead) continue;
            var at = CoreGame.Position(unit);
            foreach (var (stone, cell) in standing)
            {
                if (!CampGuardianStoneRules.InReach(at.x, at.y, cell.x, cell.y)) continue;
                CoreGame.Apply(MapType.Dungeon, new AffecterApplyData(unit.Cast<IEntity>(), CampGuardianStoneRules.AuraKey, 1,
                    CampGuardianStoneRules.AuraSeconds, true, stone.Cast<IEntity>()).Cast<IApplyData>());
                if (lines++ < CampGuardianStoneRules.LogCap)
                    DelversHost.Info(CampGuardianStoneRules.AuraLine(unit.Guid.ToString(), stone.Guid.ToString(), at.x, at.y));
                break;
            }
        }
    }
}
