using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
using GridSelector = global::GridTargetSelector;
using TaskBase = global::Refactor.Task.Task_Base;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ActCombatBehaviourTask = Il2CppRefactor.Task.ActCombatBehaviourTask;
using GridSelector = Il2Cpp.GridTargetSelector;
using TaskBase = Il2CppRefactor.Task.Task_Base;
#endif

namespace DungeonSettlersDelvers.Core;

// A party water mage's summon on a free cell beside it (GroupAiPolicyWater): the game's skill choice gives a summon no cell, so
// the first bubbleling came and every later one stood on the same spot and was never cast. The summon goes the
// way a bandage does (GroupAiBandageUse): an ActCombatBehaviourTask with the cell (GridTargetSelector.SpecificGrid), enqueued as
// DirectOrderRequested plus ForceTickCombatBehaviour. Core's own task is known by its pointer, so GroupAiDirectOrderPatch does not
// count it as a player's order. The damage mage's Glacier Drop and Water Dive go the same way on its focus' tile (CastAt). Out of a
// fight (CastIdle) the game's own use check decides first (cooldown, energy), and the energy keeps the damage reserve. At most one
// per mage every RetrySeconds.
internal static class GroupAiSummonCast
{
    private const float RetrySeconds = 1.5f;
    private static readonly OwnTasks own = new();
    private static readonly Dictionary<string, float> lastCast = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        own.Clear();
        lastCast.Clear();
    }

    // Whether a task passing the game's DirectOrder is Core's own; it is taken out (OwnTasks).
    internal static bool TakeOwnTask(IntPtr task) => own.Take(task, GameClock.Now);

    // The summon on the free cell beside the mage nearest its closest foe; false when none is free, the skill is missing or the last
    // one went out within RetrySeconds.
    internal static bool Cast(GroupAiSituation s, string skillKey)
    {
        if (s?.Owner == null || s.InMap == null) return false;
        var now = GameClock.Now;
        if (lastCast.TryGetValue(s.Guid, out var at) && now - at < RetrySeconds) return false;
        if (!CoreGame.TryMapOfUnit(s.Owner, out var map) || CoreGame.TickHandler(map) is not { } handler) return false;
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        if (skill == null) return false;
        var foe = s.ClosestEnemy;
        var toward = foe != null ? (foe.Position.x, foe.Position.y) : (s.Self.Position.x, s.Self.Position.y);
        var taken = new HashSet<(int, int)>();
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead) { var p = CoreGame.Position(unit); taken.Add((p.x, p.y)); }
        var free = FreeCellBeside(s.InMap, (s.Self.Position.x, s.Self.Position.y), toward);
        return free is { } cell && Enqueue(s.Owner, s.Guid, s.InMap, handler, skill, cell, now);
    }

    // Out of a fight: the summon beside the mage, only when the game's use check lets it through and the energy keeps its reserve.
    internal static bool CastIdle(UnitEntity mage, string skillKey)
    {
        var inMap = CoreGame.InMap;
        if (mage == null || inMap == null) return false;
        var guid = mage.Guid.ToString();
        var now = GameClock.Now;
        if (lastCast.TryGetValue(guid, out var at) && now - at < RetrySeconds) return false;
        if (!CoreGame.TryMapOfUnit(mage, out var map) || CoreGame.TickHandler(map) is not { } handler) return false;
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        var data = GroupAiRoles.Sheet(skillKey);
        if (skill == null || data == null) return false;
        if (!GroupAiRules.HasReserve(CoreGame.Stat(mage, StatType.Energy), CoreGame.Stat(mage, StatType.MaxEnergy), data.EnergyCost, GroupAiRules.DamageReserve))
            return false;
        var context = SkillExecutionContext.Convert(skill.Cast<IExecutionPublisher>(), mage.Cast<IEntity>(), (IEntity)null, null);
        if (skill.CheckForUseSkill(inMap, context, false) != ExecutionCheckResult.Successed) return false;
        var self = inMap.GetLogicalPosition(mage.Guid);
        var free = FreeCellBeside(inMap, (self.x, self.y), (self.x, self.y));
        return free is { } cell && Enqueue(mage, guid, inMap, handler, skill, cell, now);
    }

    // The free neighbour of the mage nearest toward (WeakBanditMageRules.BubblingCells), or null.
    private static Vector2Int? FreeCellBeside(IInMapDataSupplier inMap, (int X, int Y) self, (int X, int Y) toward)
    {
        var taken = new HashSet<(int, int)>();
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead) { var p = CoreGame.Position(unit); taken.Add((p.x, p.y)); }
        foreach (var (x, y) in WeakBanditMageRules.BubblingCells(self, toward, taken))
        {
            var cell = new Vector2Int(x, y);
            if (inMap.GetNearMovablePosition(cell) == cell && inMap.GetGrid(cell) != null) return cell;
        }
        return null;
    }

    // A skill on a tile (Glacier Drop, Water Dive): the order on that cell, within RetrySeconds once.
    internal static bool CastAt(GroupAiSituation s, string skillKey, Vector2Int cell)
    {
        if (s?.Owner == null || s.InMap == null) return false;
        var now = GameClock.Now;
        if (lastCast.TryGetValue(s.Guid, out var at) && now - at < RetrySeconds) return false;
        if (!CoreGame.TryMapOfUnit(s.Owner, out var map) || CoreGame.TickHandler(map) is not { } handler) return false;
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        return skill != null && s.InMap.GetGrid(cell) != null && Enqueue(s.Owner, s.Guid, s.InMap, handler, skill, cell, now);
    }

    private static bool Enqueue(UnitEntity owner, string guid, IInMapDataSupplier inMap, TickFlowHandler handler, ActiveSkill_Base skill, Vector2Int cell,
        float now)
    {
        var entity = owner.Cast<IEntity>();
        var task = new ActCombatBehaviourTask(skill, GridSelector.SpecificGrid(inMap.GetGrid(cell)).Cast<ICombatTargetSelector>(), null, entity);
        own.Add(task.Pointer, GameClock.Now);
        handler.EnqueueEvent(new DirectOrderRequested(entity, task.Cast<TaskBase>()).Cast<IEventData>());
        handler.EnqueueEvent(new ForceTickCombatBehaviour(entity).Cast<IEventData>());
        lastCast[guid] = now;
        GroupAiPolicy.Log($"CORE_AI_SUMMON_CAST unit={guid} skill={skill.Key} cell={cell.x},{cell.y} tick={CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0}");
        return true;
    }
}
