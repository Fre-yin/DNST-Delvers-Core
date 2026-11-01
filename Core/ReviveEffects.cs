using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
#if BEPINEX
using GameResourceLoader = global::Refactor.Addressable.GameResourceLoader;
#else
using GameResourceLoader = Il2CppRefactor.Addressable.GameResourceLoader;
#endif

namespace DungeonSettlersDelvers.Core;

// Core's two revive effects as rows in the game's effect table, after the toolkit's
// "Dungeonpanik". Both show the skull of "Fluch der Toten" (AFFECTER_CurseOfDead),
// the icon the author chose for the panic. They never reach the game's part of a save
// (CoreSaveEffects); Core keeps the sickness in its own data and puts it back after loading.
// - Revival sickness: no values of its own, it carries the duration; ReviveSickness caps health
//   and energy while it runs.
// - Dungeon panic: mood -25 in the dungeon after a revive.
internal static class ReviveEffects
{
    internal const string SicknessKey = ReviveText.SicknessKey;
    internal const string PanicKey = ReviveText.PanicKey;
    internal const float PanicMood = -25f;
    internal const string IconKey = "AFFECTER_CurseOfDead";
    private const string SicknessTemplate = "AFFECTER_CurseOfDead";
    private const string PanicTemplate = "AFFECTER_MajorDissatisfaction";

    private static bool reportedFailure;

    // True when both rows exist; registers them when the game's table was parsed before Core.
    internal static bool Ensure()
    {
        try
        {
            var manager = DataSheetManager.Instance;
            if (manager == null || !manager.IsReady) return false;
            return Register(manager._affecter);
        }
        catch (Exception ex)
        {
            Report(ex);
            return false;
        }
    }

    internal static bool Register(AffecterSheet sheet)
    {
        if (sheet?._affecterTable == null) return false;
        // The sickness carries the values of "Fluch der Toten" as in the toolkit's version (hit -20,
        // dodge -10).
        var sickness = AddRow(sheet, SicknessKey, SicknessTemplate, "Revival sickness: the values of AFFECTER_CurseOfDead; Core caps health and energy.", null, true);
        var panic = AddRow(sheet, PanicKey, PanicTemplate, "Dungeon panic after a revive, mood -25.", PanicMood, false);
        // The bleeding wound in four stages (Wounds), the stacks as the count; no values, no duration.
        var wounds = true;
        foreach (var key in new[] { WoundText.LightKey, WoundText.DeepKey, WoundText.HeavyKey, WoundText.FatalKey })
            wounds &= AddRow(sheet, key, Wounds.BleedingKey, "Bleeding wound of combat Apocalypse; Core drains the body.", null, false, 999, false);
        // The enemy abilities of Apocalypse (EnemyAbilities): silence and sleep, each with its own
        // categories so the game's cleanses and immunities see them as what they are; no values,
        // Core does the work.
        var enemy = AddRow(sheet, EnemyAbilityRules.SilenceKey, "AFFECTER_Stiffness", "Enemy silence: skills blocked, basic attack only.", null, false, -1, true, new[] { "Debuff", "Silence" })
            & AddRow(sheet, EnemyAbilityRules.SleepKey, "AFFECTER_Stun", "Enemy sleep: cannot act, Core wakes on damage.", null, false, -1, true, new[] { "Debuff", "Stun" });
        // Lifebloom, the healer's heal over time (Lifebloom): positive, up to three stacks.
        var lifebloom = AddRow(sheet, LifebloomRules.EffectKey, LifebloomRules.EffectTemplate, "Lifebloom: Core heals over time and blooms at the end.", null, false, 3, true, null, positive: true);
        return sickness && panic && wounds && enemy && lifebloom;
    }

    private static bool AddRow(AffecterSheet sheet, string key, string templateKey, string comment, float? mood, bool templateValues,
        int maxStack = 1, bool hasDuration = true, string[] categories = null, bool positive = false)
    {
        var (ok, row) = CoreAffecterRows.Add(sheet, key, templateKey, comment, mood, templateValues, maxStack, hasDuration, categories, positive);
        if (row != null) DelversHost.Info($"CORE_REVIVE_EFFECT_REGISTERED key={key} values={row.AdjustStats.Count}");
        return ok;
    }

    internal static void Report(Exception ex)
    {
        if (reportedFailure) return;
        reportedFailure = true;
        DelversHost.Warning("CORE_REVIVE_EFFECT_REGISTER_FAILED " + ex.Message);
    }
}

[HarmonyPatch(typeof(AffecterSheet), nameof(AffecterSheet.Parse))]
internal static class ReviveEffectsSheetPatch
{
    private static void Postfix(AffecterSheet __instance)
    {
        try { ReviveEffects.Register(__instance); }
        catch (Exception ex) { ReviveEffects.Report(ex); }
        try { BossItems.RegisterEffects(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BOSS_ITEM_EFFECT_FAILED: " + ex); }
        try { BanditDifficulty.Register(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDIT_DIFFICULTY_REGISTER_FAILED (bandits keep the game's rows): " + ex); }
    }
}

// Icons, one level below ResourceLoader like Frieren's: the effect bar asks
// LoadAffecterIconWithSkill, which never passes ResourceLoader.LoadAffecterIcon (research). Called on every icon
// refresh: a string compare first, nothing else.
internal static class ReviveEffectsIcon
{
    // The icon to show for one of Core's effects, null for every other key: the game's icon of a
    // close effect (Lifebloom like the health boost, sleep like rest, silence like panic).
    internal static string IconFor(string key)
        => key == ReviveEffects.SicknessKey || key == ReviveEffects.PanicKey ? ReviveEffects.IconKey
            : Wounds.IsWoundEffect(key) ? Wounds.BleedingKey
            : key == EnemyAbilityRules.SilenceKey ? EnemyAbilities.SilenceIconKey
            : key == EnemyAbilityRules.SleepKey ? EnemyAbilities.SleepIconKey
            : key == LifebloomRules.EffectKey ? "AFFECTER_HealthBoost"
            : BossItemRules.EffectIcon(key);

    // Core's own picture for an effect, when it has one (Lifebloom, CoreAssets; the boss effects, BossItemLook.EffectIcon);
    // null otherwise.
    internal static Sprite OwnSprite(string key, Func<string, Sprite> gameIcon)
        => key == LifebloomRules.EffectKey ? CoreAssets.LifebloomEffect
            : BossItemRules.IsEffect(key) || BossItemRules.RedIcon(key) ? BossItemLook.EffectIcon(key, gameIcon) : null;
}

[HarmonyPatch(typeof(GameResourceLoader), nameof(GameResourceLoader.LoadAffecterIcon))]
internal static class ReviveEffectsIconPatch
{
    private static bool Prefix(GameResourceLoader __instance, string key, ref Sprite __result)
    {
        var icon = ReviveEffectsIcon.IconFor(key);
        if (icon == null && !BossItemRules.IsEffect(key)) return true;
        try
        {
            var loader = __instance;
            var sprite = ReviveEffectsIcon.OwnSprite(key, loader.LoadAffecterIcon);
            if (sprite)
            {
                __result = sprite;
                return false;
            }
            if (icon == null) return true;
            __result = __instance.LoadAffecterIcon(icon);
            return __result == null;
        }
        catch (Exception) { return true; }
    }
}

[HarmonyPatch(typeof(GameResourceLoader), nameof(GameResourceLoader.LoadAffecterIconWithSkill))]
internal static class ReviveEffectsIconWithSkillPatch
{
    private static bool Prefix(GameResourceLoader __instance, string key, ref Sprite __result)
    {
        var icon = ReviveEffectsIcon.IconFor(key);
        if (icon == null && !BossItemRules.IsEffect(key)) return true;
        try
        {
            var loader = __instance;
            var sprite = ReviveEffectsIcon.OwnSprite(key, loader.LoadAffecterIconWithSkill);
            if (sprite)
            {
                __result = sprite;
                return false;
            }
            if (icon == null) return true;
            __result = __instance.LoadAffecterIconWithSkill(icon);
            return __result == null;
        }
        catch (Exception) { return true; }
    }
}
