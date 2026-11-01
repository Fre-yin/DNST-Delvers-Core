using HarmonyLib;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using CombatUtility = global::Refactor.Combat.CombatUtility;
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using TaskBase = global::Refactor.Task.Task_Base;
using TaskSystem = global::Refactor.Tick.TaskSystem;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using CombatUtility = Il2CppRefactor.Combat.CombatUtility;
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using TaskBase = Il2CppRefactor.Task.Task_Base;
using TaskSystem = Il2CppRefactor.Tick.TaskSystem;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's combat decision of a party member, in two steps: the skill, then its target. With the
// group AI on (GroupAiSettings), Core's choice replaces the game's for party members on auto attack
// (GroupAiGate). Each hook logs once that it fired; failures are logged (capped) and the game's
// choice stays. Arguments bound by name.

// The skill: a dodge out of a marked zone comes first. Without an enemy nearby the game decides.
// During the global cooldown only a healer may still place Lifebloom (off the cooldown).
[HarmonyPatch(typeof(DecidePlayerCombatBehaviour), "DecideSkill")]
internal static class GroupAiDecideSkillPatch
{
    private static bool hookSeen;

    private static void Postfix(DecidePlayerCombatBehaviour __instance, ref ActiveSkill_Base __result)
    {
        if (!hookSeen)
        {
            hookSeen = true;
            DelversHost.Info("CORE_AI_HOOK name=DecideSkill fired=1");
        }
        // Without Core's group AI, how the game itself sees Lifebloom on the quick slots (probe only; the group OneShot decides).
        if (!GroupAiSettings.Enabled)
        {
            // Without the full group AI the player's priority casts lead, the game decides the rest.
            try
            {
                var owner = __instance._owner;
                var priority = owner != null && owner.Faction == FactionType.Player ? GroupAiPriority.Pick(__instance, owner) : null;
                if (priority != null)
                {
                    __result = priority;
                    return;
                }
            }
            catch (Exception ex) { GroupAiPolicy.Warn("CORE_AI_PRIORITY_FAILED: " + ex.Message); }
            try { GroupAiPolicy.NoteNativeLifebloom(__instance, __result); }
            catch (Exception ex) { GroupAiPolicy.Warn("CORE_AI_NATIVE_PROBE_FAILED: " + ex.Message); }
            // Lifebloom stays usable during the global cooldown for the game's own decision too.
            try
            {
                var bloom = GroupAiPolicy.NativeLifebloomOffGcd(__instance);
                if (bloom != null) __result = bloom;
            }
            catch (Exception ex) { GroupAiPolicy.Warn("CORE_LIFEBLOOM_OGCD_FAILED: " + ex.Message); }
            return;
        }
        var watched = false;
        try
        {
            var owner = __instance._owner;
            if (owner == null || owner.Faction != FactionType.Player) return;
            var guid = owner.Guid.ToString();
            // A unit excluded for tests (GroupAiExcluded): the game decides for it alone.
            if (GroupAiExcluded.Has(guid)) return;
            watched = true;
            if (GroupAiGate.Open(__instance) && GroupAiPolicy.TryDodge(__instance))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            // While Core's own move order of the unit runs, Core lays no skill on
            // it. A null skill would throw in SetActCombatBehaviourTask; the game keeps a player's move order
            // itself (TaskSystem.IsPlayerMoveDirectOrder).
            if (GroupAiMoveWatch.Holds(owner, count: true))
            {
                GroupAiPolicy.Forget(guid);
                GroupAiBandageUse.NoteGate(owner, "held");
                return;
            }
            var noEnemy = CombatUtility.GetClosestSingleEnemy(owner.Cast<IEntity>(), __instance._inMapData) == null;
            // No enemy left ends the fight for the focus (new seed, no hold next time).
            if (noEnemy)
            {
                GroupAiFocus.EndFight(guid);
                GroupAiStance.EndFight(guid);
            }
            if (!GroupAiGate.Open(__instance) || noEnemy)
            {
                GroupAiPolicy.Forget(guid);
                GroupAiBandageUse.NoteGate(owner, noEnemy ? "noenemy" : "gate");
                return;
            }
            // (reason=gcd 5x at heavy and lethal wounds): a heavy or lethal wound
            // chooses its bandage before the global cooldown stops a non-healer; the bandage keeps its own 10 s lock.
            if (__instance._skillReader.HasGlobalCooldown() && Guid.TryParse(guid, out var woundGuid) && Wounds.Stage(woundGuid) >= WoundRules.Stage.Heavy
                && GroupAiBandageUse.TryUse(GroupAiSituation.Build(__instance, withSlots: true)))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            if (__instance._skillReader.HasGlobalCooldown())
            {
                GroupAiPolicy.Forget(guid);
                if (GroupAiRoles.Known(guid) is GroupAiRole role && role != GroupAiRole.Heal)
                {
                    GroupAiBandageUse.NoteGate(owner, "gcd");
                    return;
                }
                var offGcd = GroupAiPolicy.DecideOffGcd(GroupAiSituation.Build(__instance, withSlots: true));
                if (offGcd != null)
                {
                    var bloom = CombatDataManager.Instance?.GetActiveSkillData(offGcd.Skill);
                    if (bloom != null) __result = bloom;
                }
                return;
            }
            var situation = GroupAiSituation.Build(__instance, withSlots: true);
            // "Im Samen bleiben": low on health, one walk into the aura of a party seed; the game keeps its choice, as with a dodge.
            if (GroupAiSeedStay.TryWalk(situation))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            // The healer's distance and swing; with a move order the game keeps its own choice, as with a dodge.
            if (GroupAiStance.TryMove(situation, GroupAiPolicy.LastRule(guid)))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            // A ranged unit with no foe in its kit's reach walks toward its focus (GroupAiRangedReach).
            if (GroupAiRangedReach.TryMove(situation))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            // C: a bandage when everything else is on cooldown or at a lethal wound (GroupAiBandageUse).
            if (GroupAiBandageUse.TryUse(situation))
            {
                GroupAiPolicy.Forget(guid);
                return;
            }
            // The player's priority casts, after the dodge, the moves and the bandage, before Core's own choice;
            // the healer in her emergency casts the seed or Lifebloom from her places first.
            var priority = GroupAiPriority.Pick(__instance, owner, situation.Role == GroupAiRole.Heal && GroupAiStance.AllyInEmergency(situation));
            if (priority != null)
            {
                GroupAiPolicy.Forget(guid);
                __result = priority;
                return;
            }
            // A held member of the formation: the rules skip a skill on foes that reaches none from its place, as the places did.
            GroupAiPriority.MarkOutOfReach(situation);
            var choice = GroupAiPolicy.Decide(situation);
            var key = choice.Skill.Length > 0 ? choice.Skill : situation.BasicAttack;
            if (key.Length != 0)
            {
                var skill = CombatDataManager.Instance?.GetActiveSkillData(key);
                if (skill != null) __result = skill;
            }
        }
        catch (Exception ex)
        {
            GroupAiPolicy.Warn("CORE_AI_DECIDE_FAILED: " + ex);
        }
        finally
        {
            // Also a shield wall the game kept on one of the paths above counts.
            if (watched)
            {
                try
                {
                    GroupAiPolicy.NoteShieldUp(__instance, __result);
                }
                catch (Exception ex)
                {
                    GroupAiPolicy.Warn("CORE_AI_SHIELDUP_NOTE_FAILED: " + ex.Message);
                }
            }
        }
    }
}

// The target of the chosen skill: the one Core planned with it, or the game's choice redirected to
// an add next to the boss (GroupAiPolicy.Target).
[HarmonyPatch(typeof(DecidePlayerCombatBehaviour), "DecideTarget")]
internal static class GroupAiDecideTargetPatch
{
    private static bool hookSeen;

    private static void Postfix(DecidePlayerCombatBehaviour __instance, ActiveSkill_Base skill, ref IEntity __result)
    {
        if (!hookSeen)
        {
            hookSeen = true;
            DelversHost.Info("CORE_AI_HOOK name=DecideTarget fired=1");
        }
        if (!GroupAiSettings.Enabled)
        {
            // The target of Core's off-GCD Lifebloom for the game's own decision.
            try
            {
                var planned = __instance._owner == null ? null : GroupAiPolicy.NativeOffGcdTarget(__instance._owner.Guid.ToString(), skill);
                if (planned != null) __result = planned;
            }
            catch (Exception ex) { GroupAiPolicy.Warn("CORE_LIFEBLOOM_OGCD_TARGET_FAILED: " + ex.Message); }
            return;
        }
        try
        {
            var owner = __instance._owner;
            if (owner != null && owner.Faction == FactionType.Player && !GroupAiExcluded.Has(owner.Guid.ToString()) && GroupAiGate.Open(__instance))
            {
                var target = GroupAiPolicy.Target(GroupAiPolicy.SituationFor(__instance), skill, __result);
                if (target != null) __result = target;
                // No target the unit would have to walk into an enemy area for (GroupAiZoneLock).
                var inReach = GroupAiZoneLock.Retarget(owner.Cast<IEntity>(), skill, __result, __instance._inMapData);
                if (inReach != null) __result = inReach;
                // In formation, a foe in reach of the member's place (GroupAiFormation).
                var fromPlace = GroupAiFormation.Retarget(owner, skill, __result, __instance._inMapData);
                if (fromPlace != null) __result = fromPlace;
            }
        }
        catch (Exception ex)
        {
            GroupAiPolicy.Warn("CORE_AI_TARGET_FAILED: " + ex);
        }
    }
}

// A player's direct order on a party unit. TaskSystem.DirectOrder is called by the
// game's input event (TickFlowHandler.HandleEvent). The prefix only watches: it changes, blocks
// or replaces nothing. Only with the group AI on or a formation in place (GroupAiFormation.Active, which needs it without the AI
// too), only for party units, Core's own move orders excluded (GroupAiMoveWatch.IsOwnTask). GroupAiStance then keeps the AI and
// the formation quiet for 3 s.
[HarmonyPatch(typeof(TaskSystem), nameof(TaskSystem.DirectOrder))]
internal static class GroupAiDirectOrderPatch
{
    private static bool hookSeen;

    private static void Prefix(IEntity target, TaskBase task)
    {
        if ((!GroupAiSettings.Enabled && !GroupAiFormation.Active) || target == null || task == null) return;
        try
        {
            var unit = target.TryCast<UnitEntity>();
            if (unit == null || unit.Faction != FactionType.Player) return;
            if (GroupAiMoveWatch.IsOwnTask(unit, task.Pointer) || GroupAiBandageUse.IsOwnTask(task.Pointer)) return;
            if (!hookSeen)
            {
                hookSeen = true;
                DelversHost.Info("CORE_AI_HOOK name=DirectOrder fired=1");
            }
            // The input path the order came through (GroupAiOrderSource), observed only.
            GroupAiStance.NotePlayerOrder(unit, task.GetIl2CppType().Name, GroupAiOrderSource.Take(task.Pointer));
        }
        catch (Exception ex) { GroupAiPolicy.Warn("CORE_AI_ORDER_FAILED: " + ex.Message); }
    }
}
