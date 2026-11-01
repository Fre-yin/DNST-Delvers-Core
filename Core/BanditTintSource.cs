using HarmonyLib;
#if BEPINEX
using FXCallExecutionData = global::Refactor.Combat.FXCallExecutionData;
using FXRequestData = global::Refactor.FX.FXRequestData;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using SeedAuraFxCall = global::Refactor.FX.AffecterFX_StrangeSeedAura._OnFXCall_d__1;
using SeedHealRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal._HealRoutine_d__9;
using SprayerAttackRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_MucusSprayer.Passive_MucusSprayerAttack._AttackRoutine_d__8;
using SprayerFx = global::Refactor.FX.AffecterFX_MucusSprayer;
using SprayerFxCall = global::Refactor.FX.AffecterFX_MucusSprayer._OnVFX_d__1;
#else
using FXCallExecutionData = Il2CppRefactor.Combat.FXCallExecutionData;
using FXRequestData = Il2CppRefactor.FX.FXRequestData;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using SeedAuraFxCall = Il2CppRefactor.FX.AffecterFX_StrangeSeedAura._OnFXCall_d__1;
using SeedHealRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal._HealRoutine_d__9;
using SprayerAttackRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_MucusSprayer.Passive_MucusSprayerAttack._AttackRoutine_d__8;
using SprayerFx = Il2CppRefactor.FX.AffecterFX_MucusSprayer;
using SprayerFxCall = Il2CppRefactor.FX.AffecterFX_MucusSprayer._OnVFX_d__1;
#endif

namespace DungeonSettlersDelvers.Core;

// Third attempt (probes 9078c30 to the context probe, self-test): the game binds the seed's area and the
// sprayer's spray to a placeholder entity (key NULL; FXCallExecutionData.User and its context's user, SetVFXOwner's owner) and
// leaves them under VFXPoolRoot, so neither the owner nor the hierarchy says whose they are. The exact link is the passive of
// each building (Owner = the building): its routine yields the FXCallExecutionData (Passive_StrangeSeedHeal.<HealRoutine>,
// Passive_MucusSprayerAttack.<AttackRoutine>, Execution.FXCall), the game hands that very object to the FX coroutine
// (AffecterFX_StrangeSeedAura.<OnFXCall>.data, AffecterFX_MucusSprayer.OnVFX(data)), and the coroutine's CreateVFX step names the
// effect's Guid. Execution object -> building -> effect Guid; the effect is tinted when the game creates it (BanditTint.OnVfx),
// only for a confirmed bandit building. No position. CORE_SEED_TINT_SOURCE once per effect kind and building, capped.
internal static class BanditTintSource
{
    private const int Cap = 256;
    private static readonly Dictionary<IntPtr, string> executions = new();
    private static readonly Dictionary<IntPtr, string> sprayCalls = new();
    private static readonly Dictionary<string, string> effects = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        executions.Clear();
        sprayCalls.Clear();
        effects.Clear();
    }

    // A building's passive routine yielded an FX call: remember whose it is.
    internal static void OnYield(IExecutionData current, IEntity owner)
    {
        if (current == null || owner == null || current.TryCast<FXCallExecutionData>() == null) return;
        if (executions.Count >= Cap) executions.Clear();
        executions[current.Pointer] = owner.Guid.ToString();
    }

    // The seed's area coroutine reached its CreateVFX step (state 1) with the effect's Guid.
    internal static void OnAreaStep(IExecutionData data, Il2CppSystem.Guid fx)
    {
        if (data == null || !executions.TryGetValue(data.Pointer, out var building)) return;
        Remember(fx.ToString(), building);
    }

    // The sprayer's spray: the call carries the execution, the coroutine it returns the effect's Guid.
    internal static void OnSprayCall(IExecutionData data, Il2CppSystem.Object coroutine)
    {
        if (data == null || coroutine == null || !executions.TryGetValue(data.Pointer, out var building)) return;
        if (sprayCalls.Count >= Cap) sprayCalls.Clear();
        sprayCalls[coroutine.Pointer] = building;
    }

    internal static void OnSprayStep(IntPtr coroutine, Il2CppSystem.Guid fx)
    {
        if (!sprayCalls.TryGetValue(coroutine, out var building)) return;
        sprayCalls.Remove(coroutine);
        Remember(fx.ToString(), building);
    }

    private static void Remember(string fx, string building)
    {
        if (effects.Count >= Cap) effects.Clear();
        effects[fx] = building;
    }

    // The building an effect belongs to, taken once (BanditTint.OnVfx), or null.
    internal static string Take(string fx)
    {
        if (fx == null || !effects.TryGetValue(fx, out var building)) return null;
        effects.Remove(fx);
        return building;
    }
}

[HarmonyPatch(typeof(SeedHealRoutine), "MoveNext")]
internal static class BanditTintSourceSeedYieldPatch
{
    private static void Postfix(SeedHealRoutine __instance, bool __result)
    {
        if (!__result || __instance == null) return;
        try { BanditTintSource.OnYield(__instance.__2__current, __instance.__4__this?.Owner); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_SOURCE_FAILED seed: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SprayerAttackRoutine), "MoveNext")]
internal static class BanditTintSourceSprayerYieldPatch
{
    private static void Postfix(SprayerAttackRoutine __instance, bool __result)
    {
        if (!__result || __instance == null) return;
        try { BanditTintSource.OnYield(__instance.__2__current, __instance.__4__this?.Owner); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_SOURCE_FAILED sprayer: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SeedAuraFxCall), "MoveNext")]
internal static class BanditTintSourceAreaPatch
{
    private static void Postfix(SeedAuraFxCall __instance, bool __result)
    {
        if (!__result || __instance == null || __instance.__1__state != 1) return;
        try
        {
            BanditTintSource.OnAreaStep(__instance.data, __instance._guid_5__2);
            BanditTintProbe.Source("area", __instance._guid_5__2.ToString(), __instance.data);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_SOURCE_FAILED area: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SprayerFx), "OnVFX")]
internal static class BanditTintSourceSprayCallPatch
{
    private static void Postfix(IExecutionData data, Il2CppSystem.Collections.Generic.IEnumerator<FXRequestData> __result)
    {
        try
        {
            BanditTintSource.OnSprayCall(data, __result?.TryCast<Il2CppSystem.Object>());
            BanditTintProbe.Source("spray-call", null, data);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_SOURCE_FAILED spray-call: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(SprayerFxCall), "MoveNext")]
internal static class BanditTintSourceSprayPatch
{
    private static void Postfix(SprayerFxCall __instance, bool __result)
    {
        if (!__result || __instance == null || __instance.__1__state != 1) return;
        try
        {
            BanditTintSource.OnSprayStep(__instance.Pointer, __instance._guid_5__2);
            BanditTintProbe.Source("spray", __instance._guid_5__2.ToString(), null);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_SOURCE_FAILED spray: " + ex.Message); }
    }
}
