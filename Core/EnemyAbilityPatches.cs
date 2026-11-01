using HarmonyLib;
#if BEPINEX
using IExecutionData = global::Refactor.Combat.IExecutionData;
using StatusApplier = global::Refactor.Tick.DataApply.StatusApplier;
#else
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using StatusApplier = Il2CppRefactor.Tick.DataApply.StatusApplier;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's hooks of the enemy abilities (EnemyAbilities). Arguments bound by name; each hook
// catches its own failures (stage enemy-*), so the game runs on.

// After the game processed an execution: a landed hit of a listed enemy rolls its ability.
[HarmonyPatch(typeof(TickEvent), "CallOnAfterProcess")]
internal static class EnemyAbilityHitPatch
{
    private static void Postfix(IExecutionData data)
    {
        try { EnemyAbilities.OnAfterProcess(data); }
        catch (Exception ex) { ApocalypseHooks.Report("enemy-ability", ex); }
        // The greatsword's Bloodletting on the same landed hits (BossEffects).
        try { BossEffects.OnHit(data); }
        catch (Exception ex) { ApocalypseHooks.Report("boss-effect-hit", ex); }
    }
}

// After the game applied a status change: damage wakes a party member an enemy put to sleep.
[HarmonyPatch(typeof(StatusApplier), "Apply")]
internal static class EnemySleepWakePatch
{
    private static void Postfix(StatusApplyData data)
    {
        try { EnemyAbilities.OnStatusApplied(data); }
        catch (Exception ex) { ApocalypseHooks.Report("enemy-sleep-wake", ex); }
    }
}
