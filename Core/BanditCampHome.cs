using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The game side of BanditCampHomeRules: once per LookSeconds of game time, only while the shown floor has a camp with its cells, the
// boss party's bandits that are out of their fight walk home once (one order per fight, GroupAiBanditMoves); their idle
// (GroupAiFormationIdlePatch) stays inside the camp's box. The normal bandits are not asked: they have no cell in the block.
internal static class BanditCampHome
{
    private static CoreSaveData readFrom;
    private static Dictionary<string, (int X, int Y)> homes = new(StringComparer.Ordinal);
    private static (int MinX, int MinY, int MaxX, int MaxY)? box;
    private static int seed;
    // The last look in game time; a look waits LookSeconds after it, and a clock that jumped back (a load, a new campaign) does
    // not hold it (GameClockRules.Elapsed).
    private static float lastLook = float.NegativeInfinity;
    private static int lines;
    private static readonly HashSet<string> fought = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ordered = new(StringComparer.Ordinal);

    internal static void ResetLifecycle()
    {
        readFrom = null;
        homes = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
        box = null;
        lastLook = float.NegativeInfinity;
        lines = 0;
        fought.Clear();
        ordered.Clear();
    }

    // A camp was furnished: its block changed.
    internal static void Reload() => readFrom = null;

    private static void Read()
    {
        var data = CoreSaveStore.Current;
        if (ReferenceEquals(readFrom, data)) return;
        readFrom = data;
        box = data?.CampBox(out seed);
        homes = data?.CampHomes() ?? new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
        fought.Clear();
        ordered.Clear();
    }

    private static bool OnCampFloor => box != null && homes.Count > 0 && CoreGame.CurrentMap == MapType.Dungeon && seed == BanditRoomFeatureView.FloorSeed();

    internal static void OnGameTick()
    {
        Read();
        if (!OnCampFloor) return;
        var now = GameClock.Now;
        if (!GameClockRules.Elapsed(now, lastLook, BanditCampHomeRules.LookSeconds)) return;
        lastLook = now;
        var inMap = CoreGame.InMap;
        if (inMap == null) return;
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit == null || unit.IsDead || !homes.TryGetValue(unit.Key ?? "", out var home)) continue;
            var guid = unit.Guid.ToString();
            var inCombat = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;
            if (inCombat)
            {
                fought.Add(guid);
                ordered.Remove(guid);
                continue;
            }
            var cell = inMap.GetLogicalPosition(unit.Guid);
            var at = (cell.x, cell.y);
            if (!BanditCampHomeRules.WalksHome(false, fought.Contains(guid), ordered.Contains(guid), at, home, box.Value)) continue;
            ordered.Add(guid);
            fought.Remove(guid);
            if (!GroupAiBanditMoves.TryOrder(unit, new Vector2Int(home.X, home.Y), "camp-home")) continue;
            if (lines++ < BanditCampHomeRules.LogCap)
                DelversHost.Info(BanditCampHomeRules.Line(guid, at, home, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        }
    }

    // The idle cell of a boss party's bandit on its camp's floor; null for every other unit.
    internal static (int X, int Y)? IdleCell(UnitEntity unit, IInMapDataSupplier inMap)
    {
        Read();
        if (!OnCampFloor || unit == null || inMap == null || !homes.TryGetValue(unit.Key ?? "", out var home)) return null;
        var cell = inMap.GetLogicalPosition(unit.Guid);
        return BanditCampHomeRules.IdleCell((cell.x, cell.y), home, box.Value);
    }
}

// The probe of the gap after waking (BanditTakeoverRules), group OneShot: noted when the game changes a Core bandit to its fight
// (ApocalypseAggro.Sharpen), logged at Core's first decision for it (CoreUnitCombat.Decide). CORE_BANDIT_TAKEOVER.
internal static class BanditTakeoverProbe
{
    private static readonly Dictionary<string, (long Tick, string Task)> woken = new(StringComparer.Ordinal);
    private static int lines;

    internal static void ResetLifecycle()
    {
        woken.Clear();
        lines = 0;
    }

    internal static void OnWake(UnitEntity unit)
    {
        if (!CoreProbes.OneShot || unit == null || lines >= BanditTakeoverRules.LogCap) return;
        var guid = unit.Guid.ToString();
        if (woken.ContainsKey(guid)) return;
        string task;
        try { task = CoreGame.TickHandler(CoreGame.CurrentMap)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.GetTask()?.GetIl2CppType().Name ?? "-"; }
        catch (Exception ex) { task = "error:" + ex.GetType().Name; }
        woken[guid] = (CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, task);
    }

    internal static void OnDecide(UnitEntity unit)
    {
        if (!CoreProbes.OneShot || unit == null) return;
        var guid = unit.Guid.ToString();
        if (!woken.TryGetValue(guid, out var wake) || wake.Tick < 0) return;
        woken[guid] = (-1, wake.Task);
        if (lines++ < BanditTakeoverRules.LogCap)
            DelversHost.Info(BanditTakeoverRules.Line(guid, wake.Tick, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, wake.Task));
    }
}
