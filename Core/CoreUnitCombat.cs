using System.Globalization;
using UnityEngine;
#if BEPINEX
using CombatPattern = global::Refactor.Combat.CombatPattern;
using GridData = global::Refactor.Runtime.Map.GridData;
using GridSelector = global::GridTargetSelector;
using Pattern_OnlyBasicAttack = global::Refactor.Combat.PatternRelated.Pattern_OnlyBasicAttack;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using TargetType = global::Refactor.Combat.TargetType;
#else
using CombatPattern = Il2CppRefactor.Combat.CombatPattern;
using GridData = Il2CppRefactor.Runtime.Map.GridData;
using GridSelector = Il2Cpp.GridTargetSelector;
using Pattern_OnlyBasicAttack = Il2CppRefactor.Combat.PatternRelated.Pattern_OnlyBasicAttack;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// The forest bandits in combat. Their row uses the game's pattern OnlyBasicAttack; Core replaces its
// decision with the group AI's skill and target (GroupAiBandits). Skills on a cell aim at the cell of
// the target, or fall back to a basic attack at the target:
// - "basic": the cell is unknown;
// - "forced": the bandit has a forced target (provoked). The game's DecideNonPlayerCombatBehaviour
//   then swaps the selector for the forced target, the cell is lost
//   and the skill's routine throws (ArrowRain DamageRoutine, ClingingVines SkillRoutine read
//   context.TargetGrid);
// - "range": the cell lies beyond the skill's range (cells, diagonals count as one). The game's
//   behaviour tree would then move the bandit through SetMoveIfAvailable, which reads the combat
//   behaviour's TargetEntity without a check; a cell aim has none (see CoreUnitMovePatch).
// Bandits pay no energy or ammunition and need no equipment for their skills (CoreUnit*Patch), and
// the extra handling of effects that lay a unit down to be carried is skipped for them.
internal static class CoreUnitCombat
{
    private const int AimLogCap = 500;
    private const int MoveGuardLogCap = 200;
    private static bool hookReported;
    private static int aimLines;
    private static readonly HashSet<string> moveGuarded = new(StringComparer.Ordinal);
    private static readonly HashSet<string> skippedUnconscious = new(StringComparer.Ordinal);

    internal static bool IsCoreUser(SkillExecutionContext context)
        => CoreUnits.Active && CoreUnitRules.IsCoreUnit(context?.User?.TryCast<UnitEntity>()?.Key);

    internal static void Decide(Pattern_OnlyBasicAttack pattern, UnitEntity unit, IInMapDataSupplier inMap, ref CombatPattern.CombatBehaviour result)
    {
        if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) return;
        // A skill the test tool queued for this unit takes this decision (EnemySkillTest, its own postfix after this one): no
        // decision and no move order of Core's, which would take the unit away from it (the herbalist's
        // test seed went into her formation step and never grew).
        if (EnemySkillTest.Pending(unit.Guid.ToString())) return;
        if (!hookReported)
        {
            hookReported = true;
            DelversHost.Info("CORE_AI_BANDIT_HOOK loader=" + DelversCoreRuntime.LoaderProfile + " unit=" + unit.Key);
        }
        var (skill, target, rule, aimCell) = GroupAiBandits.Decide(unit, inMap);
        // (every move order lost against this decision's basic attack): while
        // Core's own move order of the bandit runs (GroupAiMoveWatch.Holds), also in the decision that gave
        // it, the pattern gives nothing. CombatPattern.Decide then has no value and the game's
        // DecideNonPlayerCombatBehaviour returns without SetTask.
        if (GroupAiMoveWatch.Holds(unit, count: true))
        {
            result = null;
            return;
        }
        // A healer that would only walk into melee stands and decides again soon.
        if (GroupAiRules.HoldsStill(rule))
        {
            result = null;
            return;
        }
        var targetType = TargetTypeOf(skill);
        var self = CoreUnitRules.AimsAtSelf(targetType) || (target != null && target.Pointer == unit.Pointer);
        // A unit excluded for tests is never the game's pick for a bandit; without
        // another player unit the bandit holds (GroupAiBandits.ExcludedPick).
        if (!self)
        {
            // A fallen combat target is turned away from before the attack, so the game does not keep it.
            GroupAiBandits.SkipFallenOnDecide(unit);
            var excluded = GroupAiBandits.ExcludedPick(unit, inMap, target == null, out var replacement);
            if (excluded == "drop")
            {
                result = null;
                return;
            }
            if (excluded == "replace") target = replacement;
        }
        var selector = (self ? EntityTargetSelector.Self() : target == null ? EntityTargetSelector.ClosestHostile() : EntityTargetSelector.SpecificTarget(target)).Cast<ICombatTargetSelector>();
        if (CoreUnitRules.AimsAtCell(targetType))
        {
            var aimed = self ? unit.Cast<IEntity>() : target;
            var cell = "-";
            // A cell the rule chose (a summon beside the caster) stands for the target's cell.
            var grid = aimCell is Vector2Int at ? CellAt(inMap, at, out cell) : aimed == null ? null : CellOf(inMap, aimed, out cell);
            // The checks run only as far as the rule needs them: without a cell there is no aimed unit to measure.
            var hasCell = grid != null;
            var forced = hasCell && IsForced(unit);
            var inRange = !hasCell || forced || InRange(inMap, unit, aimed, skill);
            var fallback = CoreUnitRules.AimFallback(hasCell, forced, inRange);
            if (aimLines < AimLogCap)
            {
                aimLines++;
                CellOf(inMap, unit.Cast<IEntity>(), out var own);
                DelversHost.Info($"CORE_AI_BANDIT_AIM unit={unit.Guid} skill={GroupAiRules.Short(skill)} target={(aimed == null ? "-" : aimed.Guid.ToString())} cell={cell} from={own}" + (fallback == null ? "" : " fallback=" + fallback)
                    + " tick=" + (CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0).ToString(CultureInfo.InvariantCulture));
            }
            if (fallback != null)
            {
                result = pattern.BasicAttackTo(self || target == null ? EntityTargetSelector.ClosestHostile().Cast<ICombatTargetSelector>() : selector);
                return;
            }
            selector = GridSelector.SpecificGrid(grid).Cast<ICombatTargetSelector>();
        }
        result = string.IsNullOrEmpty(skill) ? pattern.BasicAttackTo(selector) : pattern.UseSkillTo(skill, selector);
    }

    // Provoked: the game replaces the target of every skill that is not for allies.
    internal static bool IsForced(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetForceAttackTarget() != null;

    private static bool InRange(IInMapDataSupplier inMap, UnitEntity unit, IEntity aimed, string skill)
    {
        var from = inMap.GetLogicalPosition(unit.Guid);
        var to = inMap.GetLogicalPosition(aimed.Guid);
        var range = DataSheetManager.Instance?.GetSkillData(skill)?.Range ?? 0;
        return GroupAiRules.Distance(from.x, from.y, to.x, to.y) <= range;
    }

    // For CoreUnitMovePatch: a Core unit whose combat behaviour has no target entity (a cell aim).
    internal static bool LacksTargetEntity(IEntity owner)
    {
        var unit = owner?.TryCast<UnitEntity>();
        if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) return false;
        var behaviour = CoreGame.Component<CombatInfoComponent>(owner)?.GetCombatBehaviour();
        if (behaviour == null || behaviour.TargetEntity != null) return false;
        if (moveGuarded.Count < MoveGuardLogCap && moveGuarded.Add(unit.Guid.ToString()))
            DelversHost.Info($"CORE_AI_BANDIT_MOVE_GUARD unit={unit.Guid} key={unit.Key} grid={(behaviour.TargetGrid != null ? "yes" : "no")}");
        return true;
    }

    private static GridData CellOf(IInMapDataSupplier inMap, IEntity entity, out string cell) => CellAt(inMap, inMap.GetLogicalPosition(entity.Guid), out cell);

    private static GridData CellAt(IInMapDataSupplier inMap, Vector2Int position, out string cell)
    {
        cell = $"{position.x},{position.y}";
        return inMap.GetGrid(position);
    }

    // A bandit has no carry component, so it cannot be laid down for carrying; the effect's extra
    // handling is skipped (logged once per effect).
    internal static bool SkipsAdditionalEffect(IEntity target, Affecter affecter)
    {
        var unit = target?.TryCast<UnitEntity>();
        if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key) || !CoreUnitRules.IsUnconsciousEffect(affecter?.Key)) return false;
        if (CoreGame.Component<CarryComponent>(target) != null) return false;
        if (skippedUnconscious.Add(affecter.Key))
            DelversHost.Info($"CORE_UNIT_UNCONSCIOUS_SKIPPED key={affecter.Key} unit={unit.Key} reason=no-carry");
        return true;
    }

    private static int TargetTypeOf(string skill)
    {
        if (string.IsNullOrEmpty(skill)) return 0;
        return (int)((DataSheetManager.Instance?.GetSkillData(skill))?.TargetType ?? TargetType.None);
    }
}
