using HarmonyLib;
#if BEPINEX
using ExecutionContext = global::Refactor.Combat.ExecutionContext;
using SpearLambdas = global::Refactor.Combat.SkillRelated.SKILL_BasicSpearAttack.__c;
using SpearSkillRoutine = global::Refactor.Combat.SkillRelated.SKILL_BasicSpearAttack._SkillRoutine_d__0;
using TwinLambdas = global::Refactor.Combat.SkillRelated.SKILL_TwinThrustAttack.__c;
using TwinSkillRoutine = global::Refactor.Combat.SkillRelated.SKILL_TwinThrustAttack._SkillRoutine_d__0;
#else
using ExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using SpearLambdas = Il2CppRefactor.Combat.SkillRelated.SKILL_BasicSpearAttack.__c;
using SpearSkillRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_BasicSpearAttack._SkillRoutine_d__0;
using TwinLambdas = Il2CppRefactor.Combat.SkillRelated.SKILL_TwinThrustAttack.__c;
using TwinSkillRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_TwinThrustAttack._SkillRoutine_d__0;
#endif

namespace DungeonSettlersDelvers.Core;

// The spear's basic attack also pierces the cells behind its target (SKILL_BasicSpearAttack.<SkillRoutine>: the units on
// CombatUtility.GetSpearBasicAttackTiles go into <targets>5__2). The game then drops every unit that is not of the Enemy
// faction from that list (RemoveAll with the static lambda <>c.<SkillRoutine>b__0_1, "t.Faction!= Enemy"), so a bandit's
// thrust went through to its comrades and never to the party (Toolkit, the spear hit the swordsman behind the
// party member). The twin thrust's follow-up (SKILL_TwinThrustAttack) pierces the same way with its own copy
// of the lambda. For a bandit's thrust only the lambda's answer changes: it drops every unit that is no player or ally unit
// (GroupAiRules.CoreVinesHits), the way the vines do. The caster is the routine's, set while its MoveNext runs; every other
// thrust keeps the game's answer. CORE_AI_BANDIT_PIERCE_FILTER per thrust that dropped a unit the game kept, capped.
internal static class BanditSpearPierce
{
    private static IEntity caster;
    private static bool changed;
    private static int lines;

    internal static void Reset()
    {
        caster = null;
        lines = 0;
    }

    internal static void Enter(ExecutionContext context)
    {
        var user = context?.User;
        caster = user != null && CoreFaction.IsBandit(user) ? user : null;
        changed = false;
    }

    internal static void Leave()
    {
        if (caster != null && changed && lines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiRules.PierceFilterLine(caster.Guid.ToString(), CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        caster = null;
        changed = false;
    }

    // The lambda's answer (true: the unit leaves the list) for a bandit's thrust.
    internal static void Answer(IEntity target, ref bool removes)
    {
        if (caster == null || target == null) return;
        var drops = !GroupAiRules.CoreVinesHits((int)target.Faction);
        if (drops != removes) changed = true;
        removes = drops;
    }
}

[HarmonyPatch(typeof(SpearSkillRoutine), "MoveNext")]
internal static class BanditSpearPierceRoutinePatch
{
    private static void Prefix(SpearSkillRoutine __instance)
    {
        if (!CoreUnits.Active) return;
        try { BanditSpearPierce.Enter(__instance?.context); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-enter", ex); }
    }

    private static void Finalizer()
    {
        try { BanditSpearPierce.Leave(); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-leave", ex); }
    }
}

// The game's filter of the pierced units (BanditSpearPierce). Arguments by name.
[HarmonyPatch(typeof(SpearLambdas), "_SkillRoutine_b__0_1")]
internal static class BanditSpearPierceFilterPatch
{
    private static void Postfix(IEntity t, ref bool __result)
    {
        try { BanditSpearPierce.Answer(t, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-filter", ex); }
    }
}

// The twin thrust's follow-up, as the spear's basic attack (BanditSpearPierce).
[HarmonyPatch(typeof(TwinSkillRoutine), "MoveNext")]
internal static class BanditTwinPierceRoutinePatch
{
    private static void Prefix(TwinSkillRoutine __instance)
    {
        if (!CoreUnits.Active) return;
        try { BanditSpearPierce.Enter(__instance?.context); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-enter", ex); }
    }

    private static void Finalizer()
    {
        try { BanditSpearPierce.Leave(); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-leave", ex); }
    }
}

[HarmonyPatch(typeof(TwinLambdas), "_SkillRoutine_b__0_1")]
internal static class BanditTwinPierceFilterPatch
{
    private static void Postfix(IEntity t, ref bool __result)
    {
        try { BanditSpearPierce.Answer(t, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-pierce-filter", ex); }
    }
}
