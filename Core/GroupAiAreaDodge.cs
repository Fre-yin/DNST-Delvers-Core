using HarmonyLib;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using Execution = global::Refactor.Combat.Execution;
using ExecutionDataType = global::Refactor.Combat.ExecutionDataType;
using ExecutionEnd = global::Refactor.Combat.ExecutionProcess.ExecutionEnd;
using ExecutionFXHandler = global::Refactor.FX.System.ExecutionFXHandler;
using IExecutionState = global::Refactor.Combat.ExecutionProcess.IExecutionState;
using ExecutionNodeState = global::Refactor.Combat.ExecutionProcess.ExecutionNodeState;
using ExecutionNodeStateType = global::Refactor.Combat.ExecutionProcess.ExecutionNodeStateType;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using Execution = Il2CppRefactor.Combat.Execution;
using ExecutionDataType = Il2CppRefactor.Combat.ExecutionDataType;
using ExecutionEnd = Il2CppRefactor.Combat.ExecutionProcess.ExecutionEnd;
using ExecutionFXHandler = Il2CppRefactor.FX.System.ExecutionFXHandler;
using IExecutionState = Il2CppRefactor.Combat.ExecutionProcess.IExecutionState;
using ExecutionNodeState = Il2CppRefactor.Combat.ExecutionProcess.ExecutionNodeState;
using ExecutionNodeStateType = Il2CppRefactor.Combat.ExecutionProcess.ExecutionNodeStateType;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiAreaRules in the game, bound to the way the game itself shows an area. The area is part of the skill's presentation:
// ExecutionFXHandler.StartFX(Execution) fetches the
// skill's FX class by execution.Context.Publisher.Key, and that class draws
// the area at a node of the skill's routine. So Core reads, from the same execution:
// - owner and target: Context.User, TargetEntity, TargetGrid;
// - cells: the skill row's area through ExecutionArea (GroupAiZones.AreaCells), the function family the damage routine's
//   ActiveSkill_Base.GetAffectTargets uses (for the arrow rain); the first runs log both side by side;
// - time: the zone lasts until the execution's ExecutionEnd in ExecutionFXHandler.ProcessFX, the rule's hold time at most.
// The VFX probe (GroupAiAreaProbe) stays as the fallback view. Only enemies' areas, only for the party.
internal static class GroupAiAreaDodge
{
    private const int LogCap = 600;
    private static int lines;
    private static readonly HashSet<IntPtr> damageLogged = new();

    internal static void Reset()
    {
        lines = 0;
        ticks = 0;
        damageLogged.Clear();
        GroupAiZoneLock.Reset();
    }

    // Every game tick (GroupAiBanditTickPatch); a look at the party every WatchEveryTicks while an enemy area holds.
    private const int WatchEveryTicks = 10;
    private static int ticks;

    internal static void OnGameTick()
    {
        if (GroupAiZones.AreaZones == 0 || ++ticks % WatchEveryTicks != 0) return;
        GroupAiZones.WatchReturns(CoreGame.InMap);
    }

    internal static void OnStart(ExecutionFXHandler handler, Execution execution)
    {
        var context = execution?.Context;
        var key = context?.Publisher?.Key;
        var area = GroupAiAreaRules.BySource(key);
        if (area == null || !area.Dodge) return;
        var user = context.User?.TryCast<UnitEntity>();
        if (user == null || user.Faction != FactionType.Enemy) return;
        var inMap = handler?._inMapData ?? CoreGame.InMap;
        if (inMap == null) return;
        var from = inMap.GetLogicalPosition(user.Guid);
        var to = context.TargetGrid != null ? context.TargetGrid.Position
            : context.TargetEntity != null ? inMap.GetLogicalPosition(context.TargetEntity.Guid) : from;
        var data = key.StartsWith("SKILL_", StringComparison.Ordinal) ? DataSheetManager.Instance?.GetSkillData(key) : null;
        (int X, int Y)? lane = null;
        HashSet<(int X, int Y)> keys;
        if (area.Lane)
        {
            // A charge runs its whole range along the line through its target (LaneCells), each step with the row's width.
            lane = GroupAiZoneRules.LaneDirection(from.x, from.y, to.x, to.y);
            keys = GroupAiZoneRules.LaneKeys(area, from.x, from.y, to.x, to.y, data != null ? data.Range : GroupAiZoneRules.DefaultLaneRange);
        }
        else if (area.CenterOnUser)
        {
            // The golem's stomp and crash centre on the golem itself, over every cell its
            // size may cover (UnitTableData.Size; EAD67420: Frieren slowed outside a zone around its one cell).
            var size = DataSheetManager.Instance?.GetUnitData(user.Key)?.Size;
            var width = size != null && size.Length > 0 ? size[0] : 1;
            var height = size != null && size.Length > 1 ? size[1] : width;
            keys = GroupAiZoneRules.CenterKeys(area, from.x, from.y, width, height);
        }
        else
        {
            var game = data != null ? GroupAiZones.AreaCells(data, from, to) : null;
            // No skill row (F9, the corpse and the spores) or no area from the game: the rule's shape around the user's cell.
            keys = GroupAiZoneRules.ShapeKeys(area, data != null, from.x, from.y, to.x, to.y, game != null ? GroupAiZones.Points(game) : null);
        }
        if (keys.Count == 0) return;
        var cells = keys.Select(c => new Vector2Int(c.X, c.Y)).ToList();
        var now = GameClock.Now;
        // Further executions of the same area (the arrow rain's waves and shots) join the caster's zone; only a new one is logged
        // and sends the party out.
        // The arrow rain's waves come from its row (GroupAiAreaRules.LastHitFor), not a fixed six.
        var values = data?.FloatValues;
        var row = values == null ? null : Enumerable.Range(0, values.Length).Select(i => values[i]).ToArray();
        var lastHit = GroupAiAreaRules.LastHitFor(area, row);
        var exec = execution.Guid.ToString();
        if (!GroupAiZones.MarkArea(execution.Pointer, user.Guid.ToString(), area, cells, now, lane, lastHit, exec)) return;
        Log(GroupAiAreaRules.ZoneLine(area.Flag, user.Guid.ToString(), key, data != null ? "cast" : "rule", cells.Count, GroupAiAreaRules.HoldUntil(now, lastHit), exec));
        Compare(context, inMap, user, to, cells, area, exec);
        // The party's units in it step out now, not at their next decision (as for Core's own areas, GroupAiPolicy).
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead || GroupAiExcluded.Has(unit.Guid.ToString()) || !GroupAiGate.OpenFor(unit)) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            if (!keys.Contains((at.x, at.y))) continue;
            var target = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatTarget();
            GroupAiZones.TryDodge(GroupAiZones.Players, unit, inMap, target, GroupAiPolicy.IsMelee(unit.Guid.ToString()));
        }
    }
    // First runs: Core's cells against the units the game's own GetAffectTargets finds there now.
    private static void Compare(SkillExecutionContext context, IInMapDataSupplier inMap, UnitEntity user, Vector2Int to, List<Vector2Int> cells, GroupAiAreaRules.Area area, string exec)
    {
        var skill = context.Publisher?.TryCast<ActiveSkill_Base>();
        if (skill == null) return;
        try
        {
            var affected = skill.GetAffectTargets(inMap, user.Cast<IEntity>(), to);
            int total = 0, inside = 0;
            for (var i = 0; affected != null && i < affected.Count; i++)
            {
                if (affected[i] == null) continue;
                total++;
                var at = inMap.GetLogicalPosition(affected[i].Guid);
                if (cells.Any(c => c.x == at.x && c.y == at.y)) inside++;
            }
            Log($"CORE_AI_ZONE_CHECK area={area.Flag} exec={exec} cells={cells.Count} affect={total} affectInCells={inside}");
        }
        catch (Exception ex) { Log($"CORE_AI_ZONE_CHECK area={area.Flag} exec={exec} failed={ex.GetType().Name}"); }
    }

    // Every execution state of every tick passes here; it leaves at once while no enemy area is held.
    internal static void OnProcess(IExecutionState state)
    {
        if (GroupAiZones.AreaZones == 0 || state == null) return;
        var execution = state.BaseExecution;
        if (execution == null) return;
        var pointer = execution.Pointer;
        var own = GroupAiZones.IsAreaExecution(pointer);
        if (state.TryCast<ExecutionEnd>() != null)
        {
            damageLogged.Remove(pointer);
            if (own && GroupAiZones.EndExecution(pointer)) Log("CORE_AI_ZONE_END exec=" + execution.Guid + " end=end");
            return;
        }
        // The game's FX of a node run on its finished state by default (ExecutionFXStep.OnNodeFX state 2, Successed; EAD67420: no
        // hit seen on NodeStart in 41 zones), so the damage is the Damage node's ExecutionNodeState.
        var node = state.TryCast<ExecutionNodeState>();
        if (node == null || node.Type != ExecutionNodeStateType.Successed || node.BaseNode?.GetData()?.Type != ExecutionDataType.Damage) return;
        // Many areas deal their damage in a delegated execution (Execution.Delagate with a new ExecutionContext.Convert: the charges'
        // SkillRoutine, the corpse's ExplodeRoutine) with a Guid of its own (it does pass StartFX, under that Guid);
        //  no hit for F1, F4 and F9.
        // Such a hit counts for the zone of its user or delegator while that caster's zone is held.
        var via = "-";
        string zoneExec = null;
        if (!own)
        {
            var context = execution.Context;
            var user = context?.User?.Guid.ToString();
            var delegator = context?.Delegator?.Guid.ToString();
            if ((zoneExec = GroupAiZones.ZoneExecOf(user)) != null) via = "user";
            else if ((zoneExec = GroupAiZones.ZoneExecOf(delegator)) != null) via = "delegator";
            else return;
        }
        if (!damageLogged.Add(pointer)) return;
        var inMap = CoreGame.InMap;
        var inside = new List<string>();
        foreach (var unit in CoreGame.PlayerUnits(false))
            if (unit != null && GroupAiZones.InEnemyArea(unit, inMap)) inside.Add(unit.Guid.ToString());
        // zone= is the execution of the zone's CORE_AI_ZONE line, so a delegated hit maps to its zone.
        Log("CORE_AI_ZONE_HIT exec=" + execution.Guid + " via=" + (own ? "zone" : via) + (own ? "" : " zone=" + zoneExec)
            + " inside=" + (inside.Count == 0 ? "-" : string.Join("+", inside)));
    }

    private static void Log(string line)
    {
        if (lines++ < LogCap) DelversHost.Info(line);
    }
}

[HarmonyPatch(typeof(ExecutionFXHandler), "StartFX")]
internal static class GroupAiAreaDodgeStartPatch
{
    private static void Postfix(ExecutionFXHandler __instance, Execution execution)
    {
        try { GroupAiAreaDodge.OnStart(__instance, execution); }
        catch (Exception ex) { ApocalypseHooks.Report("area-dodge-start", ex); }
    }
}

[HarmonyPatch(typeof(ExecutionFXHandler), nameof(ExecutionFXHandler.ProcessFX))]
internal static class GroupAiAreaDodgeProcessPatch
{
    private static void Prefix(IExecutionState processState)
    {
        if (GroupAiZones.AreaZones == 0) return;
        try { GroupAiAreaDodge.OnProcess(processState); }
        catch (Exception ex) { ApocalypseHooks.Report("area-dodge-process", ex); }
    }
}
