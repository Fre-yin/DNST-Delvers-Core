using HarmonyLib;
#if BEPINEX
using AffecterExecutionData = global::Refactor.Combat.AffecterExecutionData;
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using HealExecutionLog = global::Refactor.Combat.HealExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using AffecterExecutionData = Il2CppRefactor.Combat.AffecterExecutionData;
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using HealExecutionLog = Il2CppRefactor.Combat.HealExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// CORE_BANDIT_EFFECT (BanditEffectRules): after the game processed an execution, its effect when the user is a bandit or a
// bandit's summon. Off until the test hook turns it on in the loaded test campaign (BanditEffectsForTest); the hook costs one
// field read while it is off.
internal static class BanditEffectProbe
{
    internal static bool On;
    private static int lines;

    internal static void Reset()
    {
        On = false;
        lines = 0;
    }

    internal static void Turn(bool on)
    {
        On = on;
        if (on) lines = 0;
    }

    internal static void OnProcessed(IExecutionData data)
    {
        if (lines >= BanditEffectRules.LogCap || data == null) return;
        var damage = data.TryCast<DamageExecutionData>();
        if (damage != null)
        {
            Note(damage.User, damage.Target, "damage", damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f);
            return;
        }
        var heal = data.TryCast<HealExecutionData>();
        if (heal != null)
        {
            Note(heal.User, heal.Target, "heal", heal.GetLog()?.TryCast<HealExecutionLog>()?.TotalHeal ?? 0f);
            return;
        }
        var affecter = data.TryCast<AffecterExecutionData>();
        // An affecter's stack is a DataValue the game resolves later; the line names the key only.
        if (affecter != null) Note(affecter.User, affecter.Target, "affecter:" + (affecter.Key ?? "-"), 0f);
    }

    private static void Note(IEntity user, IEntity target, string kind, float amount)
    {
        if (user == null || target == null || !CoreFaction.IsBandit(user)) return;
        var unit = target.TryCast<UnitEntity>();
        var party = unit != null && (unit.Faction == FactionType.Player || IsPartySummon(target));
        var side = BanditEffectRules.SideOf(party, CoreFaction.IsBandit(target));
        lines++;
        DelversHost.Info(BanditEffectRules.Line(user.Guid.ToString(), user.Key, target.Guid.ToString(), target.Key, side, kind, amount, unit != null && unit.IsDead));
    }

    private static bool IsPartySummon(IEntity target)
    {
        var owner = DelversSummons.OwnerUnitOf(target);
        return owner != null && owner.Faction == FactionType.Player;
    }
}

// After the game processed an execution: the effects of bandits, while the probe runs (BanditEffectProbe). Arguments by name.
// Patched only while the group Effects is on (CoreProbes).
[ProbeGroup(CoreProbeRules.Effects)]
[HarmonyPatch(typeof(TickEvent), "CallOnAfterProcess")]
internal static class BanditEffectProbePatch
{
    private static void Postfix(IExecutionData data)
    {
        if (!BanditEffectProbe.On) return;
        try { BanditEffectProbe.OnProcessed(data); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-effect-probe", ex); }
    }
}
