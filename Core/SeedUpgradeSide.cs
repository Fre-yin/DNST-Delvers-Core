using HarmonyLib;
#if BEPINEX
using ExecutionContext = global::Refactor.Combat.ExecutionContext;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using BuildingTransformTargetCondition = global::Refactor.Combat.BuildingTransformTargetCondition;
#else
using ExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using BuildingTransformTargetCondition = Il2CppRefactor.Combat.BuildingTransformTargetCondition;
#endif

namespace DungeonSettlersDelvers.Core;

// The seed upgrades keep to their side (SeedUpgradeRules): after the game's own check passed, a seed of the other side is the
// game's own refusal for a wrong target, Failed_UnvalidTarget, which the check returns for any other building too. The priority
// casts and the game's choice of the skill read the same check, so neither side picks the other's seed. CORE_SEED_UPGRADE_SIDE
// once per refusal, capped.
[HarmonyPatch(typeof(BuildingTransformTargetCondition), "CheckCondition")]
internal static class SeedUpgradeSidePatch
{
    private static int lines;

    private static void Postfix(ExecutionContext context, ref ExecutionCheckResult __result)
    {
        if ((int)__result < 0 || !CoreUnits.Active || context == null) return;
        try
        {
            var caster = context.User?.TryCast<UnitEntity>();
            var seed = context.TargetEntity;
            if (caster == null || seed == null) return;
            var casterIsCore = CoreUnitRules.IsCoreUnit(caster.Key);
            // A seed the tint's scan confirmed as a bandit's stays one when its summoner has left the map already; a fresh seed
            // the scan has not seen yet is told by its summoner.
            var seedIsCore = BanditTint.IsBanditBuildingSeen(seed.Guid.ToString()) || DelversSummons.IsCoreOwned(seed);
            if (SeedUpgradeRules.Allows(casterIsCore, seedIsCore)) return;
            __result = ExecutionCheckResult.Failed_UnvalidTarget;
            if (lines++ < SeedUpgradeRules.LogCap)
                DelversHost.Info(SeedUpgradeRules.RefusedLine(caster.Guid.ToString(), seed.Guid.ToString(), seed.TryCast<BuildingEntity>()?.Key,
                    casterIsCore, seedIsCore));
        }
        catch (Exception ex) { ApocalypseHooks.Report("seed-upgrade-side", ex); }
    }
}
