using HarmonyLib;
#if BEPINEX
using AffecterApplier = global::Refactor.Tick.DataApply.AffecterApplier;
using AmmoCondition = global::Refactor.Combat.AmmoCondition;
using AmmoSpendRoutine = global::Refactor.Combat.AmmoCondition._OnSuccessCondition_d__2;
using BulkShotAmmoRoutine = global::Refactor.Combat.SkillRelated.SKILL_BulkShot.Passive_BulkShot._UseAmmoRoutine_d__10;
using CombatPattern = global::Refactor.Combat.CombatPattern;
using EnergyCondition = global::Refactor.Combat.EnergyCondition;
using EnergySpendRoutine = global::Refactor.Combat.EnergyCondition._OnSuccessCondition_d__2;
using EquipmentCondition = global::Refactor.Combat.EquipmentCondition;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using Pattern_OnlyBasicAttack = global::Refactor.Combat.PatternRelated.Pattern_OnlyBasicAttack;
using SetMoveIfAvailable = global::Refactor.Task.SetMoveIfAvailable;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
#else
using AffecterApplier = Il2CppRefactor.Tick.DataApply.AffecterApplier;
using AmmoCondition = Il2CppRefactor.Combat.AmmoCondition;
using AmmoSpendRoutine = Il2CppRefactor.Combat.AmmoCondition._OnSuccessCondition_d__2;
using BulkShotAmmoRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_BulkShot.Passive_BulkShot._UseAmmoRoutine_d__10;
using CombatPattern = Il2CppRefactor.Combat.CombatPattern;
using EnergyCondition = Il2CppRefactor.Combat.EnergyCondition;
using EnergySpendRoutine = Il2CppRefactor.Combat.EnergyCondition._OnSuccessCondition_d__2;
using EquipmentCondition = Il2CppRefactor.Combat.EquipmentCondition;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using Pattern_OnlyBasicAttack = Il2CppRefactor.Combat.PatternRelated.Pattern_OnlyBasicAttack;
using SetMoveIfAvailable = Il2CppRefactor.Task.SetMoveIfAvailable;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's hooks of the forest bandits (CoreUnits, CoreUnitCombat). Arguments bound by name;
// hooks that call into Core catch their own failures (stage unit-*), so the game runs on.

// Adds the bandit rows before the game parses its unit table.
[HarmonyPatch(typeof(UnitSheet), "Parse")]
internal static class CoreUnitSheetPatch
{
    private static void Prefix(UnitTable table)
    {
        try { CoreUnits.Register(table); }
        catch (Exception ex) { DelversHost.Error("CORE_UNIT_REGISTER_FAILED: " + ex); }
    }
}

// Picks the look of a bandit while the game creates its object; cleared afterwards.
[HarmonyPatch(typeof(EntityObjectsContainer), "SpawnUnitObject")]
internal static class CoreUnitSpawnObjectPatch
{
    private static void Prefix(UnitEntity entity)
    {
        if (!CoreUnits.Active) return;
        try { CoreUnits.BeginSpawn(entity); }
        catch (Exception ex) { ApocalypseHooks.Report("unit-look-begin", ex); }
    }

    private static void Postfix()
    {
        if (CoreUnits.Active) CoreUnits.EndSpawn();
    }
}

// A bandit loads the human player prefab of its visual type instead of its template's object.
[HarmonyPatch(typeof(ResourceLoader), "LoadUnitObject")]
internal static class CoreUnitObjectPatch
{
    private static void Prefix(ref string key, out string __state)
    {
        __state = null;
        if (CoreUnits.Active && CoreUnitRules.IsCoreUnit(key))
        {
            var prefab = CoreUnits.PrefabFor(key);
            if (prefab != null)
            {
                __state = key;
                key = prefab;
            }
        }
    }

    // A form whose player prefab the game does not have falls back to a human one of the same role (CoreUnits.Fallback),
    // so a bandit never spawns without an object.
    private static void Postfix(ResourceLoader __instance, ref UnitObject __result, string __state)
    {
        if (__state == null || __result != null) return;
        try
        {
            var fallback = CoreUnits.Fallback(__state);
            if (fallback != null) __result = __instance.LoadUnitObject(fallback);
        }
        catch (Exception ex) { ApocalypseHooks.Report("unit-look-fallback", ex); }
    }
}

// Dresses the bandit's new object in its profile and kit.
[HarmonyPatch(typeof(EntityObjectFactory), "SpawnUnitObject")]
internal static class CoreUnitFactoryPatch
{
    private static void Postfix(UnitObject __result)
    {
        if (!CoreUnits.Active) return;
        try { CoreUnits.Dress(__result); }
        catch (Exception ex) { ApocalypseHooks.Report("unit-look-dress", ex); }
    }
}

// The game's material flash does not run on a bandit (logged once).
[HarmonyPatch(typeof(EntityObjectView), "PlayMaterial")]
internal static class CoreUnitFlashPatch
{
    private static bool flashSkipped;

    private static bool Prefix(IEntity target)
    {
        if (!CoreUnits.Active) return true;
        var unit = target?.TryCast<UnitEntity>();
        if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) return true;
        if (!flashSkipped)
        {
            flashSkipped = true;
            DelversHost.Info("CORE_UNIT_FLASH_SKIPPED key=" + unit.Key);
        }
        return false;
    }
}

// The bandit's skill and target come from the group AI.
[HarmonyPatch(typeof(Pattern_OnlyBasicAttack), "Pattern")]
internal static class CoreUnitPatternPatch
{
    private static void Postfix(Pattern_OnlyBasicAttack __instance, UnitEntity unit, IInMapDataSupplier inMapData, ref CombatPattern.CombatBehaviour __result)
    {
        if (!CoreUnits.Active) return;
        try { CoreUnitCombat.Decide(__instance, unit, inMapData, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("unit-ai", ex); }
    }
}

// The enemy behaviour tree's move check reads the combat behaviour's TargetEntity without a null
// check (SetMoveIfAvailable.CanMoveToHostile: GetCombatBehviour). A
// bandit that aims at a cell has none, so the check threw every tick and the bandit stood still
// (Melon, 501 times in 8 s). For a Core unit without a target entity the check answers
// "no move" and the tree goes on; every other unit runs the game's check. Private, patched by name.
// (the formation walked to the party instead): the check sets a
// MoveTask of its own. (not one bandit order ever
// arrived, moved=0): the tree walks a plain MoveTask only behind this check, and the check says yes only when
// it set a new one (return 1, the same LastTarget return 0; which also finds no range check in it). While Core's own move order of the unit
// runs (GroupAiMoveWatch.Stops) it answers "yes" without a task of its own, so the tree walks Core's MoveTask.
[HarmonyPatch(typeof(SetMoveIfAvailable), "CanMoveToHostile")]
internal static class CoreUnitMovePatch
{
    private static bool Prefix(SetMoveIfAvailable __instance, ref bool __result)
    {
        if (!CoreUnits.Active) return true;
        try
        {
            if (GroupAiMoveWatch.Stops(__instance._owner?.TryCast<UnitEntity>()))
            {
                __result = true;
                return false;
            }
            // (after healer-hold the tree walked her eight cells into the party with
            // her last behaviour): the bandits' healer never steps toward a player unit on her own.
            if (GroupAiBandits.HealerKeepsAway(__instance._owner))
            {
                __result = false;
                return false;
            }
            if (!CoreUnitCombat.LacksTargetEntity(__instance._owner)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("unit-move", ex);
            return true;
        }
    }
}

// A bandit's skills need no ammunition.
[HarmonyPatch(typeof(AmmoCondition), "CheckCondition")]
internal static class CoreUnitAmmoPatch
{
    private static bool Prefix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if (!CoreUnitCombat.IsCoreUser(context)) return true;
        __result = ExecutionCheckResult.Successed;
        return false;
    }
}

// ... and spend none: the condition's success routine ends before its first step.
[HarmonyPatch(typeof(AmmoSpendRoutine), "MoveNext")]
internal static class CoreUnitAmmoSpendPatch
{
    private static bool Prefix(AmmoSpendRoutine __instance, ref bool __result)
    {
        if (!CoreUnits.Active || __instance.__1__state != 0) return true;
        try
        {
            if (!CoreUnitCombat.IsCoreUser(__instance.context)) return true;
            __instance.__1__state = -1;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("unit-ammo", ex);
            return true;
        }
    }
}

// nor does the passive Bulk Shot of a bandit archer: its own routine (Passive_BulkShot.<UseAmmoRoutine>) asks the game to spend
// the arrows of its extra shots (ConsumeAmmoApplyData), past the ammunition condition above, and the game's item handler threw for a
// bandit without an inventory (ItemEventHandler.OnEvent(ConsumeAmmoRequested), NullReferenceException).
// For a Core owner the routine ends before its first step; the extra shots stay.
[HarmonyPatch(typeof(BulkShotAmmoRoutine), "MoveNext")]
internal static class CoreUnitBulkShotAmmoPatch
{
    private static bool Prefix(BulkShotAmmoRoutine __instance, ref bool __result)
    {
        if (!CoreUnits.Active || __instance.__1__state != 0) return true;
        try
        {
            var owner = __instance.__4__this?.Owner?.TryCast<UnitEntity>();
            if (owner == null || !CoreUnitRules.IsCoreUnit(owner.Key)) return true;
            __instance.__1__state = -1;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("unit-bulkshot-ammo", ex);
            return true;
        }
    }
}

// A bandit's skills need no energy.
[HarmonyPatch(typeof(EnergyCondition), "CheckCondition")]
internal static class CoreUnitEnergyPatch
{
    private static bool Prefix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if (!CoreUnitCombat.IsCoreUser(context)) return true;
        __result = ExecutionCheckResult.Successed;
        return false;
    }
}

// ... and spend none: the condition's success routine ends before its first step.
[HarmonyPatch(typeof(EnergySpendRoutine), "MoveNext")]
internal static class CoreUnitEnergySpendPatch
{
    private static bool Prefix(EnergySpendRoutine __instance, ref bool __result)
    {
        if (!CoreUnits.Active || __instance.__1__state != 0) return true;
        try
        {
            if (!CoreUnitCombat.IsCoreUser(__instance.context)) return true;
            __instance.__1__state = -1;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("unit-energy", ex);
            return true;
        }
    }
}

// A bandit's kit is only drawn, so its skills need no equipped item.
[HarmonyPatch(typeof(EquipmentCondition), "CheckCondition")]
internal static class CoreUnitEquipmentPatch
{
    private static bool Prefix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if (!CoreUnitCombat.IsCoreUser(context)) return true;
        __result = ExecutionCheckResult.Successed;
        return false;
    }
}

// When an unconscious effect newly lands on a bandit, its extra handling (laying the unit down to be
// carried) is skipped (CoreUnitCombat.SkipsAdditionalEffect).
[HarmonyPatch(typeof(AffecterApplier), "CalculateAdditionalEffect")]
internal static class CoreUnitUnconsciousPatch
{
    private static bool Prefix(IEntity target, Affecter affecter, int beforeStack, int afterStack)
    {
        if (!CoreUnits.Active || afterStack <= 0 || beforeStack > 0) return true;
        try { return !CoreUnitCombat.SkipsAdditionalEffect(target, affecter); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("unit-unconscious", ex);
            return true;
        }
    }
}

// A dead bandit keeps its kit on the body:
// nothing drops at death, undressing drops the kit (step 2, CoreUnits.DropKit). Core notes the corpse.
[HarmonyPatch(typeof(EntityEventHandler), "DropUnitItem")]
internal static class CoreUnitDropPatch
{
    private static void Postfix(MapType map, UnitEntity unit)
    {
        if (!CoreUnits.Active) return;
        try { CorpseKeep.NoteBanditCorpse(map, unit); }
        catch (Exception ex) { ApocalypseHooks.Report("unit-drop", ex); }
    }
}

// Logs when the game removes a bandit from a map (capped); a noted bandit corpse is forgotten.
[HarmonyPatch(typeof(EntityLifecycleHelper), "DespawnUnit")]
internal static class CoreUnitDespawnPatch
{
    private static void Prefix(MapType map, UnitEntity entity)
    {
        if (!CoreUnits.Active) return;
        try
        {
            CoreUnits.ReportDespawn(map, entity);
            CorpseKeep.Forget(entity);
        }
        catch (Exception ex) { ApocalypseHooks.Report("unit-despawn", ex); }
    }
}
