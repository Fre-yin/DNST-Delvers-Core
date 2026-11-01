using HarmonyLib;
#if BEPINEX
using GuardiansTauntRoutine = global::Refactor.Combat.SkillRelated.SKILL_GuardiansTaunt._SkillRoutine_d__0;
#else
using GuardiansTauntRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_GuardiansTaunt._SkillRoutine_d__0;
#endif

namespace DungeonSettlersDelvers.Core;

// The forest bandits' guard casts Guardian's Taunt as a slowing ring (an NPC taunt
// on players only works by forcing their units, which players hate; the game's own design note on
// AFFECTER_Taunt says enemies never taunt). Same skill row, so the same reach (Manhattan 5), look and
// cooldown; for a Core bandit Core takes over the skill's routine and puts the game's slow
// (AFFECTER_Slow, movement -30 %) on the same targets instead of the taunt and its forced target.
// Player units casting Guardian's Taunt are not touched.
internal static class BanditSlowRing
{
    private static double seconds = GroupAiRules.SlowSecondsDefault;
    private static int lines;

    // Read at the campaign's start (GroupAiBandits.Reset).
    internal static void Reset(double slowSeconds)
    {
        seconds = slowSeconds;
        lines = 0;
    }

    // Prefix of the routine's MoveNext: true when the caster is a Core bandit; then the routine ends at
    // once and no taunt is applied, whatever happens below.
    internal static bool TakeOver(GuardiansTauntRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        var user = routine.context?.User?.TryCast<UnitEntity>();
        if (user == null || !CoreUnitRules.IsCoreUnit(user.Key)) return false;
        routine.__1__state = -1;
        try { Ring(routine, user); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_BANDIT_SLOW_RING_FAILED unit=" + user.Guid + ": " + ex.Message); }
        return true;
    }

    // The skill's own targets around the guard (GetAffectTargets at the guard's cell, as the game's
    // routine does), each slowed for the set seconds.
    private static void Ring(GuardiansTauntRoutine routine, UnitEntity user)
    {
        var inMap = routine.inMapData;
        var skill = routine.__4__this;
        if (inMap == null || skill == null || !CoreGame.TryMapOfUnit(user, out var map)) return;
        var self = user.Cast<IEntity>();
        var targets = skill.GetAffectTargets(inMap, self, inMap.GetLogicalPosition(user.Guid));
        var hit = 0;
        var renewed = 0;
        var struck = new List<string>();
        for (var i = 0; targets != null && i < targets.Count; i++)
        {
            var target = targets[i];
            var unit = target?.TryCast<UnitEntity>();
            // A fallen unit is no target of the ring (targets=4 after a wipe, nobody slowed).
            if (unit == null || unit.IsDead || GroupAiExcluded.Has(unit.Guid.ToString()) || GroupAiBandits.IsDowned(unit)) continue;
            if (CoreGame.Affecters(unit)?.Has(GroupAiRules.SlowKey) == true) renewed++;
            CoreGame.Apply(map, new AffecterApplyData(target, GroupAiRules.SlowKey, 1, (float)seconds, true, self).Cast<IApplyData>());
            struck.Add(unit.Guid.ToString());
            hit++;
        }
        if (lines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiRules.SlowRingLine(user.Guid.ToString(), hit, seconds, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, struck, renewed));
    }
}

// Guardian's Taunt's routine: for a Core bandit the slowing ring, the routine ends at once.
[HarmonyPatch(typeof(GuardiansTauntRoutine), "MoveNext")]
internal static class BanditSlowRingPatch
{
    private static bool Prefix(GuardiansTauntRoutine __instance, ref bool __result)
    {
        if (!CoreUnits.Active) return true;
        try
        {
            if (!BanditSlowRing.TakeOver(__instance)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("bandit-slow-ring", ex);
            return true;
        }
    }
}
