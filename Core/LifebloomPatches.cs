using HarmonyLib;
using UnityEngine;
#if BEPINEX
using CooldownCondition = global::Refactor.Combat.CooldownCondition;
using CooldownSuccessRoutine = global::Refactor.Combat.CooldownCondition._OnSuccessCondition_d__2;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using HealingTouchRoutine = global::Refactor.Combat.SkillRelated.SKILL_HealingTouchAttack._SkillRoutine_d__0;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
#else
using CooldownCondition = Il2CppRefactor.Combat.CooldownCondition;
using CooldownSuccessRoutine = Il2CppRefactor.Combat.CooldownCondition._OnSuccessCondition_d__2;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using HealingTouchRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_HealingTouchAttack._SkillRoutine_d__0;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's hooks of the Lifebloom skill (Lifebloom). The hooks around a cast catch their own
// failures, so the game's code runs on if Core fails (stage lifebloom-*). Arguments bound by name.

// The skill row, once the game has parsed its skill sheet.
[HarmonyPatch(typeof(SkillSheet), "Parse")]
internal static class LifebloomSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { Lifebloom.RegisterSkill(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_LIFEBLOOM_SKILL_FAILED: " + ex); }
    }
}

// The skill object for our key: the healing touch's class with our key and row.
[HarmonyPatch(typeof(Skill_Base), "Create")]
internal static class LifebloomCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (key != LifebloomRules.SkillKey) return true;
        try { __result = Lifebloom.CreateSkill(); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_LIFEBLOOM_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

// The healing touch's routine: for our key Core casts the buff and the routine ends at once.
[HarmonyPatch(typeof(HealingTouchRoutine), "MoveNext")]
internal static class LifebloomRoutinePatch
{
    private static bool Prefix(HealingTouchRoutine __instance, ref bool __result)
    {
        if (!Lifebloom.Active) return true;
        try
        {
            if (!Lifebloom.TakeOver(__instance)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("lifebloom-cast", ex);
            return true;
        }
    }
}

// The game sets the skill's cooldown after a cast: a charge is spent instead, and the game's
// cooldown runs only once no charge is left (as long as the next charge needs).
[HarmonyPatch(typeof(SkillComponent), "AddCooldown")]
internal static class LifebloomCooldownPatch
{
    private static bool Prefix(SkillComponent __instance, string key, ref float value)
    {
        if (!Lifebloom.Active || key != LifebloomRules.SkillKey) return true;
        try { return Lifebloom.SpendCharge(__instance, ref value); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("lifebloom-charges", ex);
            return true;
        }
    }
}

// Off the global cooldown (this patch and LifebloomGlobalSetPatch): our skill passes the cooldown
// check while only the global cooldown runs. The decision is LifebloomRules.SkipsGlobalCooldown and
// PassesCooldownCheck; the patches only read the game's values.
[HarmonyPatch(typeof(CooldownCondition), "CheckCondition")]
internal static class LifebloomGlobalCheckPatch
{
    private static void Postfix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if (!Lifebloom.Active || __result != ExecutionCheckResult.Failed_CoolTimeNotMet) return;
        try
        {
            var key = Lifebloom.SkillKeyOf(context);
            // Other skills end here without a look at the caster's skills.
            if (!LifebloomRules.SkipsGlobalCooldown(key)) return;
            if (LifebloomRules.PassesCooldownCheck(key, Lifebloom.OwnCooldownRunning(context))) __result = ExecutionCheckResult.Successed;
        }
        catch (Exception ex) { ApocalypseHooks.Report("lifebloom-global-check", ex); }
    }
}

// After a cast the cooldown condition's success routine sets the global cooldown in its second step
// (state 1); for our skill the routine ends there instead, so the other skills stay free.
[HarmonyPatch(typeof(CooldownSuccessRoutine), "MoveNext")]
internal static class LifebloomGlobalSetPatch
{
    private static bool Prefix(CooldownSuccessRoutine __instance, ref bool __result)
    {
        if (!Lifebloom.Active || __instance.__1__state != 1) return true;
        try
        {
            if (!LifebloomRules.SkipsGlobalCooldown(Lifebloom.SkillKeyOf(__instance.context))) return true;
            __instance.__1__state = -1;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("lifebloom-global-set", ex);
            return true;
        }
    }
}

// After the game processed an execution: Lifebloom's heals are counted with what really arrived.
[HarmonyPatch(typeof(TickEvent), "CallOnAfterProcess")]
internal static class LifebloomHealPatch
{
    private static void Postfix(IExecutionData data)
    {
        try { Lifebloom.OnAfterProcess(data); }
        catch (Exception ex) { ApocalypseHooks.Report("lifebloom-heal", ex); }
        // The seed probe's count: its first statement asks the group Seeds, so a group that is off costs a field read.
        try { GroupAiHealerSeedProbe.OnAfterProcess(data); }
        catch (Exception ex) { ApocalypseHooks.Report("seed-heal-probe", ex); }
    }
}

// Core's own icon (CoreAssets); the healing touch's while it is missing.
[HarmonyPatch(typeof(ResourceLoader), "LoadSkillIcon")]
internal static class LifebloomIconPatch
{
    private static bool Prefix(ref string key, ref Sprite __result)
    {
        if (key != LifebloomRules.SkillKey) return true;
        var icon = CoreAssets.LifebloomSkill;
        if (icon)
        {
            __result = icon;
            return false;
        }
        key = LifebloomRules.SkillTemplate;
        return true;
    }
}

// The skill slot shows no global cooldown on our skill.
[HarmonyPatch(typeof(SkillSlotUI), "SetGlobalCooldown")]
internal static class LifebloomSlotGlobalPatch
{
    private static void Prefix(SkillSlotUI __instance, ref float cooldown)
    {
        if (!Lifebloom.Active || __instance._key != LifebloomRules.SkillKey) return;
        cooldown = 0f;
        LifebloomSlotIcon.Refresh(__instance);
    }

    // The countdown to the next charge again right after the game's refresh; on other skills' slots a capped probe of
    // which image the game fills.
    private static void Postfix(SkillSlotUI __instance, float cooldown, float duration)
    {
        LifebloomSlotCooldown.Probe(__instance, cooldown, duration);
        LifebloomSlotCooldown.AfterGameRefresh(__instance);
    }
}

// The game's own refresh of one bar slot writes the cooldown fill straight into the image, the global cooldown
// included; on Lifebloom's slot Core paints right after it (LifebloomSlotCooldown).
[HarmonyPatch(typeof(QuickSlotUI), "RefreshSkillSlotUI")]
internal static class LifebloomSlotRefreshPatch
{
    private static void Postfix(QuickSlotUI __instance, int index, string skillKey)
    {
        if (!Lifebloom.Active || skillKey != LifebloomRules.SkillKey) return;
        try { LifebloomSlotCooldown.AfterBarRefresh(__instance, index); }
        catch (Exception ex) { ApocalypseHooks.Report("lifebloom-slot-refresh", ex); }
    }
}

// Same for the slot's cooldown refresh.
[HarmonyPatch(typeof(SkillSlotUI), "UpdateCooldownState")]
internal static class LifebloomSlotStatePatch
{
    private static void Prefix(SkillSlotUI __instance, ref float globalCooldown)
    {
        if (!Lifebloom.Active || __instance._key != LifebloomRules.SkillKey) return;
        globalCooldown = 0f;
        LifebloomSlotIcon.Refresh(__instance);
    }

    private static void Postfix(SkillSlotUI __instance) => LifebloomSlotCooldown.AfterGameRefresh(__instance);
}

// At 14:55 and 16:07
// logged 23 and 18 select corrections: on a selection the game sets the slot again (SetUI through LoadSkillIcon, which
// knows no owner, so the full picture) and the picture stayed full until the next select check (up to SelectCheckTicks).
// Right after the game sets the slot's skill or owner, the picture follows the owner's charges at once. A slot that now shows
// another skill gets the game's cooldown direction back (LifebloomSlotCooldown).
[HarmonyPatch(typeof(SkillSlotUI), "SetUI")]
internal static class LifebloomSlotSetUiPatch
{
    private static void Postfix(SkillSlotUI __instance)
    {
        if (Lifebloom.Active && __instance != null && __instance._key == LifebloomRules.SkillKey) LifebloomSlotIcon.Refresh(__instance);
        LifebloomSlotCooldown.AfterSlotSet(__instance);
    }
}

[HarmonyPatch(typeof(SkillSlotUI), "SetOwner")]
internal static class LifebloomSlotSetOwnerPatch
{
    private static void Postfix(SkillSlotUI __instance)
    {
        if (Lifebloom.Active && __instance != null && __instance._key == LifebloomRules.SkillKey) LifebloomSlotIcon.Refresh(__instance);
    }
}

// The slot's picture follows the owner's charges (3, 2, 1, 0); the game itself shows no charges.
// Set from the slot's own cooldown refreshes above, no patch of its own; an icon that is already right is left alone.
// The author's test (no change in game): the game calls those refreshes for our slot only while its own cooldown
// runs, which Lifebloom keeps off (gameCooldown=no), so a charge change pushes the picture itself: spending one
// (Lifebloom.SpendCharge, called from SkillComponent.AddCooldown, which the game runs only after a cast that went
// through; a cancelled cast spends nothing and changes no leaf) and getting one back (OnGameTick, charges come back
// lazily) look up the caster's
// Lifebloom slots (FindObjectsOfType, only on a change) and log CORE_LIFEBLOOM_ICON. slots=0 is quiet: no bar shown.
internal static class LifebloomSlotIcon
{
    private static int failures;
    // Casters below full charges and the count their slots show.
    private static readonly Dictionary<Guid, (UnitEntity Unit, int Shown)> watched = new();

    internal static void Reset() => watched.Clear();

    internal static void OnCharges(UnitEntity caster, Guid guid, int charges, string source)
    {
        if (caster == null) return;
        if (charges < LifebloomRules.MaxCharges) watched[guid] = (caster, charges);
        else watched.Remove(guid);
        RefreshAll(caster, guid, charges, source);
    }

    // Selecting a unit lets the game set the full picture again (SkillSlotUI.SetUI through LoadSkillIcon, which knows no
    // owner); every SelectCheckTicks the slots of casters below full charges are checked and set once more.
    private const int SelectCheckTicks = 30;
    private static int ticks;

    // Every game tick (GroupAiBanditTickPatch): a charge that came back, and a slot the game set back to full.
    internal static void OnGameTick()
    {
        if (watched.Count == 0 || !Lifebloom.Active) return;
        if (++ticks >= SelectCheckTicks)
        {
            ticks = 0;
            foreach (var pair in watched) RefreshAll(pair.Value.Unit, pair.Key, pair.Value.Shown, "select");
        }
        List<(Guid Guid, UnitEntity Unit, int Charges)> changed = null;
        foreach (var pair in watched)
        {
            var now = Lifebloom.ChargesOf(pair.Key);
            if (now != pair.Value.Shown) (changed ??= new List<(Guid, UnitEntity, int)>()).Add((pair.Key, pair.Value.Unit, now));
        }
        if (changed == null) return;
        foreach (var (guid, unit, charges) in changed) OnCharges(unit, guid, charges, "recharge");
    }

    private static void RefreshAll(UnitEntity caster, Guid guid, int charges, string source)
    {
        try
        {
            var sprite = CoreAssets.LifebloomSkillFor(charges);
            if (!sprite) return;
            var slots = 0;
            var set = 0;
            foreach (var slot in UnityEngine.Object.FindObjectsOfType<SkillSlotUI>())
            {
                if (slot == null || slot._key != LifebloomRules.SkillKey) continue;
                var owner = slot._owner?.TryCast<UnitEntity>();
                if (owner == null || owner.Pointer != caster.Pointer) continue;
                slots++;
                var image = slot._imgSkill;
                if (image == null || image.sprite == sprite) continue;
                image.sprite = sprite;
                set++;
            }
            // The select check logs only when it had to set a picture again.
            if (slots > 0 && (source != "select" || set > 0)) DelversHost.Info(LifebloomRules.IconLine(guid, charges, slots, set, source, sprite.name));
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_ICON_FAILED: " + ex.Message);
        }
    }

    internal static void Refresh(SkillSlotUI slot)
    {
        try
        {
            var image = slot._imgSkill;
            var unit = slot._owner?.TryCast<UnitEntity>();
            if (image == null || unit == null) return;
            var sprite = CoreAssets.LifebloomSkillFor(Lifebloom.ChargesOf(CoreGame.GuidOf(unit)));
            if (sprite && image.sprite != sprite) image.sprite = sprite;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_ICON_FAILED: " + ex.Message);
        }
    }
}
