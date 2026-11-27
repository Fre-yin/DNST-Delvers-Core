using HarmonyLib;
#if BEPINEX
using CombatStateApplier = global::Refactor.Tick.DataApply.CombatStateApplier;
using CombatStateApplyData = global::Refactor.CombatStateApplyData;
#else
using CombatStateApplier = Il2CppRefactor.Tick.DataApply.CombatStateApplier;
using CombatStateApplyData = Il2CppRefactor.CombatStateApplyData;
#endif

namespace DungeonSettlersDelvers.Core;

// (bound aggro, one bandit is attacked and the whole camp wakes; the first hit received is the
// trigger, and once all are awake nobody calls again). Catalog the game's CombatStateApplier.Apply calls
// DungeonBehaviorSimulator.OnCombatStart for a unit entering combat, which looks up its dungeon room
// (RuntimeDungeonData.GetUnitRoomInfos) and puts the units of that room into combat (on dungeon maps
// only, every unit not neutral and not yet in combat); inside a room the game already
// wakes the camp. Core adds what it leaves out (no room, comrades outside the room's area):
// - triggers: the first landed hit of a player or ally unit on a Core bandit (GroupAiBandits.OnDamage), and a player's
//   taunt (a bandit entering combat with a forced target), which is an attack too;
// - the wake is queued and done on the next game tick (TickFlowHandler hook), never inside the game's applier;
// - it uses the game's own way, a CombatStateApplyData through the same applier, never a direct state change;
// - only living, not fallen Core bandits within GroupAiRules.GroupWakeCells that are not fighting yet; once everybody is
//   awake nothing is logged again. Switch bandits.groupWake (on by default).
// - each comrade enters combat after its own short delay (GroupAiRules.WakeDelay), the camp wake's bandits
//   too (WakeLater); the delays run on game time and are settled on the tick (FlushDelayed).
// - the game's own wake of a whole room (DungeonBehaviorSimulator.OnCombatStart) stays at once for every unit in it (the skills
// act staggered anyway). Should players report a drop at the start of a fight, this is the first place
//   to look: holding Core's bandits back there would mean a prefix on CombatStateApplier.Apply.
internal static class BanditGroupWake
{
    private static readonly List<(UnitEntity Trigger, string Cause)> queued = new();
    private static readonly HashSet<string> triggered = new(StringComparer.Ordinal);
    private static readonly List<(Guid Unit, MapType Map, float Due)> delayed = new();
    private static bool waking;
    private static int lines;

    internal static void Reset()
    {
        queued.Clear();
        triggered.Clear();
        delayed.Clear();
        lines = 0;
    }

    // A bandit Core has woken and that waits for its delay: it belongs to the fight already (BanditBossBar).
    internal static bool Waking(UnitEntity unit)
    {
        if (unit == null || delayed.Count == 0 || !Guid.TryParse(unit.Guid.ToString(), out var guid)) return false;
        foreach (var entry in delayed) if (entry.Unit == guid) return true;
        return false;
    }

    // A bandit Core wakes enters combat after its own short delay of game time.
    internal static void WakeLater(UnitEntity unit, MapType map)
    {
        if (unit == null || !Guid.TryParse(unit.Guid.ToString(), out var guid)) return;
        delayed.Add((guid, map, GameClock.Now + GroupAiRules.WakeDelay(CoreRandom.Next01(CoreRandomRules.BanditWake, guid))));
    }

    // On the game tick: the delayed bandits whose time has come enter combat through the game's applier; one that died, fell or
    // is fighting already in the meantime is left as it is.
    internal static void FlushDelayed()
    {
        if (delayed.Count == 0) return;
        var now = GameClock.Now;
        waking = true;
        try
        {
            for (var i = delayed.Count - 1; i >= 0; i--)
            {
                var (guid, map, due) = delayed[i];
                if (!GroupAiRules.WakeDue(due, now)) continue;
                delayed.RemoveAt(i);
                var unit = CoreGame.FindUnit(map, guid);
                if (unit == null || unit.IsDead || GroupAiBandits.IsDowned(unit)) continue;
                var state = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() ?? CombatState.Unawared;
                if (state == CombatState.InCombat) continue;
                CoreGame.Apply(map, new CombatStateApplyData(unit.Cast<IEntity>(), CombatState.InCombat, false).Cast<IApplyData>());
            }
        }
        finally
        {
            waking = false;
        }
    }

    // A landed hit on a Core bandit by a player or ally unit: the first one per bandit triggers.
    internal static void OnHit(UnitEntity target, UnitEntity attacker)
    {
        if (target == null || attacker == null || attacker.Faction == FactionType.Enemy || !CoreUnitRules.IsCoreUnit(target.Key)) return;
        Queue(target, "hit");
    }

    // After the game's applier: a Core bandit entering combat under a forced target (a player's taunt). A Core bandit leaving its
    // fight (the game's combat state tick builds a CombatStateApplyData for that too, catalog 01-kampf, CombatState) ends what
    // Core noted for that fight (GroupAiBandits.FightOver), so its next fight starts fresh.
    internal static void After(CombatStateApplyData data)
    {
        if (data != null && data.State != CombatState.InCombat)
        {
            var left = data.Target?.TryCast<UnitEntity>();
            if (left != null && CoreUnitRules.IsCoreUnit(left.Key)) GroupAiBandits.FightOver(left.Guid.ToString());
            return;
        }
        if (waking || data == null) return;
        var trigger = data.Target?.TryCast<UnitEntity>();
        if (trigger == null || !CoreUnitRules.IsCoreUnit(trigger.Key)) return;
        if (CoreGame.Component<CombatInfoComponent>(trigger.Cast<IEntity>())?.GetForceAttackTarget() != null) Queue(trigger, "taunt");
    }

    // A bandit's fight ended: its first hit or taunt in the next fight wakes its comrades again.
    internal static void Forget(string guid) => triggered.Remove(guid);

    private static void Queue(UnitEntity trigger, string cause)
    {
        if (!CoreSettings.GroupWake || trigger.IsDead || !triggered.Add(trigger.Guid.ToString())) return;
        queued.Add((trigger, cause));
    }

    // On the next game tick (GroupAiBandits' tick hook): wake the queued triggers' comrades.
    internal static void Flush()
    {
        if (queued.Count == 0) return;
        var batch = new List<(UnitEntity Trigger, string Cause)>(queued);
        queued.Clear();
        foreach (var (trigger, cause) in batch)
        {
            try { Wake(trigger, cause); }
            catch (Exception ex) { ApocalypseHooks.Report("bandit-group-wake", ex); }
        }
    }

    private static void Wake(UnitEntity trigger, string cause)
    {
        if (trigger == null || trigger.IsDead || !CoreGame.TryMapOfUnit(trigger, out var map)) return;
        var inMap = CoreGame.InMapOf(map);
        var alive = inMap?.GetAliveUnitsOnMap();
        if (alive == null) return;
        var from = inMap.GetLogicalPosition(trigger.Guid);
        var candidates = new List<(string Guid, int Distance, bool Awake, bool Fallen)>();
        var units = new Dictionary<string, UnitEntity>(StringComparer.Ordinal);
        for (var i = 0; i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || unit.Pointer == trigger.Pointer || !CoreUnitRules.IsCoreUnit(unit.Key)
                || unit.Faction != FactionType.Enemy || GroupAiExcluded.Has(unit.Guid.ToString())) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            var state = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() ?? CombatState.Unawared;
            var guid = unit.Guid.ToString();
            units[guid] = unit;
            candidates.Add((guid, GroupAiRules.Distance(from.x, from.y, at.x, at.y), state == CombatState.InCombat, GroupAiBandits.IsDowned(unit)));
        }
        var woken = GroupAiRules.WakeGroup(candidates);
        if (woken.Count == 0) return;
        foreach (var guid in woken) WakeLater(units[guid], map);
        if (lines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiRules.GroupWakeLine(trigger.Guid.ToString(), woken.Count, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0) + " cause=" + cause);
    }
}

[HarmonyPatch(typeof(CombatStateApplier), "Apply", new[] { typeof(CombatStateApplyData), typeof(Il2CppSystem.Collections.Generic.List<IApplyData>) })]
internal static class BanditGroupWakePatch
{
    private static void Postfix(CombatStateApplyData data)
    {
        if (!CoreUnits.Active) return;
        try
        {
            BanditGroupWake.After(data);
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("bandit-group-wake", ex);
        }
    }
}
