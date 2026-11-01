using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Small helpers for the running campaign, after the toolkit's GameAccess (Modding Toolkit,
// tested). Main thread only. Data changes go through the game's own tick queue
// (DataApplyRequested), like native skills do; that also works while the game is paused.
internal static class CoreGame
{
    private static MainFlow flow;

    internal static void ResetLifecycle() => flow = null;

    // An entity's object, or null. The game throws for one it has not activated yet ("CheckActivateOrFlag"; self-test
    // that aborted the whole frame). Every lookup in Core goes through here (Core's trap list, Tests/Core).
    internal static IEntityObject EntityObjectOf(IEntityObjectSupplier container, Il2CppSystem.Guid guid)
    {
        if (container == null) return null;
        try { return container.GetEntityObject(guid, false); }
        catch (Exception) { return null; }
    }

    internal static MainFlow Flow
    {
        get
        {
            if (flow == null || flow.Pointer == IntPtr.Zero || !flow) flow = UnityEngine.Object.FindObjectOfType<MainFlow>();
            return flow;
        }
    }

    // A campaign runs and no map transition is under way.
    internal static bool CampaignReady
    {
        get
        {
            var current = Flow;
            return current != null && current.IsCampaignStarted && current._mapTransitionState != null
                && !current._mapTransitionState.IsTransitionRunning;
        }
    }

    internal static MapType CurrentMap => Flow.CurrentMap;

    internal static IInMapDataSupplier InMap
    {
        get
        {
            var current = Flow;
            var maps = current?._inMapDatas;
            return maps != null && maps.ContainsKey(current.CurrentMap) ? maps[current.CurrentMap] : null;
        }
    }

    internal static TickFlowHandler TickHandler(MapType map)
    {
        var handlers = Flow?._tickFlowHandlers;
        return handlers != null && handlers.ContainsKey(map) ? handlers[map]?.TryCast<TickFlowHandler>() : null;
    }

    internal static T Component<T>(IEntity entity) where T : Il2CppSystem.Object
        => entity == null ? null : EntityComponent._instance?.GetComponentOf<T>(entity);

    internal static AffecterComponent Affecters(UnitEntity unit) => Component<AffecterComponent>(unit?.Cast<IEntity>());

    internal static float Stat(UnitEntity unit, StatType type)
    {
        var status = Component<StatusComponent>(unit?.Cast<IEntity>());
        return status == null ? float.NaN : new StatusReader(status).GetValue(type);
    }

    // Living or dead party members of the shown map.
    internal static List<UnitEntity> PlayerUnits(bool includeDead)
    {
        var result = new List<UnitEntity>();
        var units = InMap?.GetAllPlayerUnits(includeDead);
        for (var i = 0; units != null && i < units.Count; i++)
            if (units[i] != null) result.Add(units[i]);
        return result;
    }

    // Every unit that still occupies a cell of the shown map; only the game's death handling
    // clears a dead unit from it.
    internal static List<UnitEntity> MapUnits()
    {
        var result = new List<UnitEntity>();
        var units = InMap?.GetAliveUnitsOnMap();
        for (var i = 0; units != null && i < units.Count; i++)
            if (units[i] != null) result.Add(units[i]);
        return result;
    }

    // The map that holds a party member, living or dead, among all loaded maps (not only the shown
    // one); false when none does.
    internal static bool TryMapOfPartyMember(UnitEntity unit, out MapType map)
    {
        map = MapType.Base;
        var maps = Flow?._inMapDatas;
        if (maps == null || unit == null) return false;
        foreach (var entry in maps)
        {
            var units = entry.Value?.GetAllPlayerUnits(true);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                if (units[i] == null || units[i].Pointer != unit.Pointer) continue;
                map = entry.Key;
                return true;
            }
        }
        return false;
    }

    // The map that holds any unit still on a cell (enemies too), among all loaded maps.
    internal static bool TryMapOfUnit(UnitEntity unit, out MapType map)
    {
        map = MapType.Base;
        var maps = Flow?._inMapDatas;
        if (maps == null || unit == null) return false;
        foreach (var entry in maps)
        {
            var units = entry.Value?.GetAliveUnitsOnMap();
            for (var i = 0; units != null && i < units.Count; i++)
            {
                if (units[i] == null || units[i].Pointer != unit.Pointer) continue;
                map = entry.Key;
                return true;
            }
        }
        return false;
    }

    internal static UnitEntity FindUnit(Guid guid)
    {
        if (guid == Guid.Empty) return null;
        var text = guid.ToString("D");
        foreach (var unit in MapUnits()) if (unit.Guid.ToString() == text) return unit;
        foreach (var unit in PlayerUnits(true)) if (unit.Guid.ToString() == text) return unit;
        return null;
    }

    internal static Guid GuidOf(UnitEntity unit)
        => unit != null && Guid.TryParse(unit.Guid.ToString(), out var guid) ? guid : Guid.Empty;

    // The summoner's GUID read from the backing field, "" for none (DelversSummons, public for the packs).
    internal static string SummonerGuid(IEntity entity) => DelversSummons.SummonerGuid(entity);

    internal static Vector2Int Position(UnitEntity unit) => InMap.GetLogicalPosition(unit.Guid);

    internal static void Apply(MapType map, IApplyData data) => Apply(map, data, true);

    // force: the game's tick handler keeps two queues, one for forced events and one for the others (TickFlowHandler.EnqueueEvent);
    // an event that must follow one of the game's own lands in the queue the game used for it.
    internal static void Apply(MapType map, IApplyData data, bool force)
    {
        var handler = TickHandler(map) ?? throw new InvalidOperationException("the game's tick queue of " + map + " is not ready");
        handler.EnqueueEvent(new DataApplyRequested(data).Cast<IEventData>(), force);
    }

    internal static void Apply(IApplyData data) => Apply(CurrentMap, data);

    internal static void Enqueue(IEventData data)
    {
        var queue = Flow?._eventQueue ?? throw new InvalidOperationException("the game's event queue is not ready");
        queue.AddEvent(data);
    }

    // The campaign's combat and management difficulty as the revive rules read them, Apocalypse
    // included.
    internal static ReviveRules.Level CombatLevel()
    {
        var setting = Flow?._campaignSetting?.CampaignStartSetting;
        var native = setting != null ? (int)setting.GetCombatDifficulty() : 1;
        return ReviveRules.FromGame(native, ApocalypseDifficulty.ActiveCombat);
    }

    internal static ReviveRules.Level ManagementLevel()
    {
        var setting = Flow?._campaignSetting?.CampaignStartSetting;
        var native = setting != null ? (int)setting.GetManagementDifficulty() : 1;
        return ReviveRules.FromGame(native, ApocalypseDifficulty.ActiveManagement);
    }
}
