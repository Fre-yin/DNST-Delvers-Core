using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageSimulator = global::Refactor.Combat.ExecutionProcess.DamageSimulator;
using CombatUtility = global::Refactor.Combat.CombatUtility;
using UseSkillMode = global::Refactor.Control.UseSkillMode;
using FormationLineDecider = global::Refactor.Control.FormationLineDecider;
using CombatInfoViewPresenter = global::Refactor.CombatInfoViewPresenter;
using SkillUseFilter = global::Refactor.EntityTargetSelector.SkillUseFilter;
using UseSkillTask = global::Refactor.Task.UseSkill;
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using SetSkillMoveTarget = global::Refactor.Task.SetSkillMoveTarget;
using SetMoveIfAvailable = global::Refactor.Task.SetMoveIfAvailable;
using MoveTaskHelper = global::Refactor.Task.MoveTaskHelper;
using CheckCombatState = global::Refactor.Task.CheckCombatState;
using CheckInputPath = global::Refactor.Task.CheckInputPath;
using CheckItemSkillAccessible = global::Refactor.Task.CheckItemSkillAccessible;
using IsMeleeSkill = global::Refactor.Task.IsMeleeSkill;
using SharedEntityBrain = global::Refactor.Task.SharedEntityBrain;
using RangeCondition = global::Refactor.Combat.RangeCondition;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageSimulator = Il2CppRefactor.Combat.ExecutionProcess.DamageSimulator;
using CombatUtility = Il2CppRefactor.Combat.CombatUtility;
using UseSkillMode = Il2CppRefactor.Control.UseSkillMode;
using FormationLineDecider = Il2CppRefactor.Control.FormationLineDecider;
using CombatInfoViewPresenter = Il2CppRefactor.CombatInfoViewPresenter;
using SkillUseFilter = Il2CppRefactor.EntityTargetSelector.SkillUseFilter;
using UseSkillTask = Il2CppRefactor.Task.UseSkill;
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using SetSkillMoveTarget = Il2CppRefactor.Task.SetSkillMoveTarget;
using SetMoveIfAvailable = Il2CppRefactor.Task.SetMoveIfAvailable;
using MoveTaskHelper = Il2CppRefactor.Task.MoveTaskHelper;
using CheckCombatState = Il2CppRefactor.Task.CheckCombatState;
using CheckInputPath = Il2CppRefactor.Task.CheckInputPath;
using CheckItemSkillAccessible = Il2CppRefactor.Task.CheckItemSkillAccessible;
using IsMeleeSkill = Il2CppRefactor.Task.IsMeleeSkill;
using SharedEntityBrain = Il2CppRefactor.Task.SharedEntityBrain;
using RangeCondition = Il2CppRefactor.Combat.RangeCondition;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
#endif

namespace DungeonSettlersDelvers.Core;

// The bow's Falcon Eyes beyond sight (BossEffectRules): +2 range on the bearer's attack skills, more crit at a distance.
// Range: the game keeps one skill object per key for every unit (CombatDataManager.GetActiveSkillData), so get_Range does
// not know whose skill it is. Each method of the game that reads a range for a unit pushes that unit as owner before it runs
// and a finalizer pops it, also on an exception: the player's target mode (its user), the party member's combat decision
// and its behaviour tree nodes for using a skill, moving to a target and checking range (their brain's owner), the move
// helper (its brain), the formation line (its unit), the skill target filter (the user of its arguments) and the range
// indicator (the one selected unit, else none); get_Range adds the bonus only for the innermost owner, when it wears the bow and holds that skill. Outside
// these methods, and for every other unit, the range stays the game's. The enemies' AI (DecideNonPlayerCombatBehaviour) is
// left out: Core's bandits only show their kit and carry no item effects (BossEffects.Wears reads the effect's affecter), so
// the boss party's archer has no Falcon Eyes, nor the others their parts' effects (the boss party does not
// use its items' effects for now, the weapons are strong already; maybe later after player feedback).
// Crit: around DamageSimulator.CalculateResult the bearer's crit chance goes up by the distance bonus and down again in a
// finalizer (StatusComponent.AddValue; the roll reads the live status, StatusReader.GetValue -> GetFinalValue).
// That patch is put on by hand (FalconCritInstall), never by attribute: DamageSimulator's type initializer reads
// SystemVar.Combat.DamagePartsRatio, which is null until MainFlow.InitCore has run SystemVar.Init. Patching it at mod start ran the
// initializer too early, the type stayed broken for the session and no campaign loaded (run log; trap list, row 10).
internal static class FalconEyes
{
    private static readonly Stack<IEntity> owners = new();

    internal static void ResetLifecycle()
    {
        owners.Clear();
        FalconCritInstall.ResetLifecycle();
    }

    internal static void Push(IEntity owner) => owners.Push(owner);

    internal static void Pop()
    {
        if (owners.Count > 0) owners.Pop();
    }

    internal static IEntity OwnerOf(SharedEntityBrain brain) => brain?.Value?.Owner;

    internal static int Range(ActiveSkill_Base skill, int range)
    {
        if (owners.Count == 0 || skill == null) return range;
        return ExtendedRange(owners.Peek(), skill.Data, range);
    }

    // The range the owner's skill has with Falcon Eyes: the given range plus the bonus, or the given range as it is.
    private static int ExtendedRange(IEntity owner, SkillTableData data, int range)
    {
        if (owner == null || data == null || !BossEffects.Wears(owner, BossItemRules.BowKey)) return range;
        var holds = CoreGame.Component<SkillComponent>(owner)?.HasSkill(data.Key) == true;
        return BossEffectRules.RangeWithBonus(range, holds, data.Key);
    }

    // The game's checks of a cast read the table range past the getter (BossEffectRules.CastRangeCorrection). When one of
    // them says out of range, the answer is asked again with Falcon Eyes' range. Only the answer "out" is asked again, and
    // only when the bearer's skill has a bonus at all. The distance is the one the game measures: to the target entity
    // (GetDistanceWithEnemy), else to the target cell.
    internal static bool InExtendedCastRange(SkillExecutionContext context, IInMapDataSupplier inMap, int correction)
    {
        var user = context?.User;
        var data = context?.Publisher?.TryCast<ActiveSkill_Base>()?.Data;
        if (user == null || data == null || inMap == null) return false;
        var range = ExtendedRange(user, data, data.Range);
        if (range <= data.Range) return false;
        var target = context.TargetEntity;
        int distance;
        if (target != null) distance = CombatUtility.GetDistanceWithEnemy(user, target, inMap);
        else if (context.TargetGrid != null) distance = CombatUtility.GetGridDistance(user, context.TargetGrid.Position, inMap);
        else return false;
        return BossEffectRules.InCastRange(distance, range, correction);
    }

    // The crit bonus put on the attacker for this one roll, or 0.
    internal static float CritBefore(DamageExecutionData data)
    {
        var user = data?.User;
        var target = data?.Target;
        if (user == null || target == null || !BossEffects.Wears(user, BossItemRules.BowKey)) return 0f;
        var inMap = CoreGame.InMap;
        if (inMap == null) return 0f;
        var distance = CombatUtility.GetGridDistance(user, inMap.GetLogicalPosition(target.Guid), inMap);
        var bonus = (float)BossEffectRules.FalconCritBonus(distance);
        if (bonus <= 0f) return 0f;
        var status = CoreGame.Component<StatusComponent>(user);
        if (status == null) return 0f;
        status.AddValue(StatType.CriticalChance, bonus);
        BossEffects.Line("falcon-crit", user, FormattableString.Invariant($"target={target.Guid:D} distance={distance} bonus={bonus:0.#}"));
        return bonus;
    }

    internal static void CritAfter(DamageExecutionData data, float bonus)
    {
        if (bonus <= 0f || data?.User == null) return;
        CoreGame.Component<StatusComponent>(data.User)?.AddValue(StatType.CriticalChance, -bonus);
    }
}

[HarmonyPatch(typeof(ActiveSkill_Base), nameof(ActiveSkill_Base.Range), MethodType.Getter)]
internal static class FalconRangePatch
{
    private static void Postfix(ActiveSkill_Base __instance, ref int __result)
    {
        try { __result = FalconEyes.Range(__instance, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("falcon-range", ex); }
    }
}

// The cast's own range checks read the table range directly, so the getter patch above does not reach them:
// RangeCondition.CheckCondition lets a cast through up to the range plus its constant 1, and the context form of
// CombatUtility.IsInRange up to the range plus its addRange. Both are asked again for a bearer when they say out of range.
[HarmonyPatch(typeof(RangeCondition), "CheckCondition")]
internal static class FalconCastRangeConditionPatch
{
    private static void Postfix(IInMapDataSupplier inMapData, SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if (__result != ExecutionCheckResult.Failed_NotNeedToNotice) return;
        try
        {
            if (FalconEyes.InExtendedCastRange(context, inMapData, BossEffectRules.CastRangeCorrection)) __result = ExecutionCheckResult.Successed;
        }
        catch (Exception ex) { ApocalypseHooks.Report("falcon-cast-range", ex); }
    }
}

[HarmonyPatch(typeof(CombatUtility), nameof(CombatUtility.IsInRange), typeof(SkillExecutionContext), typeof(IInMapDataSupplier), typeof(int))]
internal static class FalconCastInRangePatch
{
    private static void Postfix(SkillExecutionContext context, IInMapDataSupplier inMapData, int addRange, ref bool __result)
    {
        if (__result) return;
        try { __result = FalconEyes.InExtendedCastRange(context, inMapData, addRange); }
        catch (Exception ex) { ApocalypseHooks.Report("falcon-cast-range", ex); }
    }
}

// No [HarmonyPatch]: FalconCritInstall puts it on once the game's combat data is loaded.
internal static class FalconCritPatch
{
    private static void Prefix(DamageExecutionData data, out float __state)
    {
        __state = 0f;
        try { __state = FalconEyes.CritBefore(data); }
        catch (Exception ex) { ApocalypseHooks.Report("falcon-crit", ex); }
    }

    private static Exception Finalizer(DamageExecutionData data, float __state, Exception __exception)
    {
        try { FalconEyes.CritAfter(data, __state); }
        catch (Exception ex) { ApocalypseHooks.Report("falcon-crit-after", ex); }
        return __exception;
    }
}

// Puts FalconCritPatch on at the first hook (LatePatches) that finds the combat data loaded: the end of the boot, else the start
// of a load or of a campaign. All three come before the first hit. At most once per session (LatePatchGate); a failure costs only the crit bonus.
internal static class FalconCritInstall
{
    private const string Name = "falcon-crit";
    private static readonly LatePatchGate gate = new();
    private static bool toldWaiting;

    internal static void ResetLifecycle()
    {
        gate.Reset();
        toldWaiting = false;
    }

    internal static void TryInstall(string at)
    {
        if (gate.State != LatePatchState.Waiting) return;
        if (!gate.ShouldTry(CombatDataReady()))
        {
            if (!toldWaiting) DelversHost.Info(LatePatchGate.Line(Name, at, gate.State));
            toldWaiting = true;
            return;
        }
        gate.Record(Install());
        DelversHost.Info(LatePatchGate.Line(Name, at, gate.State));
    }

    // The value DamageSimulator's type initializer reads; SystemVar.Combat has no initializer of its own.
    private static bool CombatDataReady()
    {
        try
        {
            var ratios = SystemVar.Combat.DamagePartsRatio;
            return ratios != null && ratios.Length > 0;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_PATCH_LATE_FAILED name=" + Name + " stage=data error=" + ex.Message);
            return false;
        }
    }

    private static bool Install()
    {
        try
        {
            var owner = DelversCoreRuntime.HarmonyOwner;
            if (owner == null) return false;
            var original = AccessTools.Method(typeof(DamageSimulator), nameof(DamageSimulator.CalculateResult));
            new Harmony(owner).Patch(original,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FalconCritPatch), "Prefix")),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(FalconCritPatch), "Finalizer")));
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_PATCH_LATE_FAILED name=" + Name + " error=" + ex);
            return false;
        }
    }
}

// ---- the readers: each pushes the unit the range is read for (reader list, numbers as there) ----

// 1. The player's target mode: the user is a parameter.
[HarmonyPatch(typeof(UseSkillMode), nameof(UseSkillMode.GetPossibleTarget))]
internal static class FalconOwnerUseSkillMode
{
    private static void Prefix(UnitEntity user) => FalconEyes.Push(user?.Cast<IEntity>());

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 2. The party member's combat decision: its brain's owner.
[HarmonyPatch]
internal static class FalconOwnerDecidePlayer
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DecidePlayerCombatBehaviour), "SetActCombatBehaviourTask");
        yield return AccessTools.Method(typeof(DecidePlayerCombatBehaviour), "GetSpecificTarget");
    }

    private static void Prefix(DecidePlayerCombatBehaviour __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 4. to 6., 8. to 11.: behaviour tree nodes of a unit, their brain's owner.
[HarmonyPatch(typeof(UseSkillTask), "OnStart")]
internal static class FalconOwnerUseSkill
{
    private static void Prefix(UseSkillTask __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch]
internal static class FalconOwnerSetSkillMoveTarget
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SetSkillMoveTarget), "SetSkillTarget");
        yield return AccessTools.Method(typeof(SetSkillMoveTarget), "ShouldCancelSkill");
    }

    private static void Prefix(SetSkillMoveTarget __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(SetMoveIfAvailable), "CanMoveToHostile")]
internal static class FalconOwnerSetMoveIfAvailable
{
    private static void Prefix(SetMoveIfAvailable __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(CheckCombatState), "OnUpdate")]
internal static class FalconOwnerCheckCombatState
{
    private static void Prefix(CheckCombatState __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(CheckInputPath), "OnStart")]
internal static class FalconOwnerCheckInputPath
{
    private static void Prefix(CheckInputPath __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(CheckItemSkillAccessible), "OnUpdate")]
internal static class FalconOwnerCheckItemSkillAccessible
{
    private static void Prefix(CheckItemSkillAccessible __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(IsMeleeSkill), "OnUpdate")]
internal static class FalconOwnerIsMeleeSkill
{
    private static void Prefix(IsMeleeSkill __instance) => FalconEyes.Push(FalconEyes.OwnerOf(__instance.EntityBrain));

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 7. The move helper of a unit's brain.
[HarmonyPatch]
internal static class FalconOwnerMoveTaskHelper
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MoveTaskHelper), nameof(MoveTaskHelper.HasAlternative));
        yield return AccessTools.Method(typeof(MoveTaskHelper), nameof(MoveTaskHelper.ShouldReject));
    }

    private static void Prefix(MoveTaskHelper __instance) => FalconEyes.Push(__instance._brain?.Owner);

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 12. The formation line of a unit: the unit is a parameter.
[HarmonyPatch(typeof(FormationLineDecider), nameof(FormationLineDecider.Decide))]
internal static class FalconOwnerFormationLine
{
    private static void Prefix(UnitEntity unit) => FalconEyes.Push(unit?.Cast<IEntity>());

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 13. The range indicator: the owner only when exactly one unit is selected; otherwise none (no bonus shown).
[HarmonyPatch(typeof(CombatInfoViewPresenter), nameof(CombatInfoViewPresenter.ShowIndicator))]
internal static class FalconOwnerIndicator
{
    private static void Prefix(CombatInfoViewPresenter __instance)
    {
        IEntity owner = null;
        var selected = __instance._selectedReader;
        if (selected != null && selected.GetSelectedCount() == 1)
        {
            var all = selected.GetAllSelected();
            if (all != null && all.Count == 1) owner = all[0];
        }
        FalconEyes.Push(owner);
    }

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}

// 14. The skill target filter: the user of its own arguments.
[HarmonyPatch(typeof(SkillUseFilter), nameof(SkillUseFilter.CreateMove))]
internal static class FalconOwnerSkillUseFilter
{
    private static void Prefix(SkillUseFilter __instance) => FalconEyes.Push(__instance._args?.User);

    private static Exception Finalizer(Exception __exception)
    {
        FalconEyes.Pop();
        return __exception;
    }
}
