using HarmonyLib;
using UnityEngine;
#if BEPINEX
using CombatFX_Base = global::Refactor.FX.CombatFX_Base;
#else
using CombatFX_Base = Il2CppRefactor.FX.CombatFX_Base;
#endif

namespace DungeonSettlersDelvers.Core;

// The bandits' chain pull (BanditChainRules): the row is a copy of SKILL_ChainGrap with the bandits' key, the skill the
// chain's class with that key and row (Skill_Base.Create). The game finds a skill's effect by its key
// (CombatFX_Base.Create and CreateSkillFX build "Refactor.FX.SkillFX_" from the key), so the copy's key is handed on as the
// game's one there. Name and description are the game's texts copied to the bandits' key in the active language. The icon
// is the author's (CoreAssets.BanditChainIcon), the game's while the file is missing. Every prefix compares the key first and
// lets every other key through at once, without allocation (like LifebloomIconPatch).
internal static class BanditChain
{
    internal static void RegisterSkill(SkillSheet sheet)
    {
        if (sheet?._skillTable == null || sheet._skillTable.ContainsKey(BanditChainRules.SkillKey)) return;
        var t = sheet.GetData(BanditChainRules.GameKey);
        if (t == null)
        {
            DelversHost.Warning("CORE_BANDIT_CHAIN_TEMPLATE_MISSING key=" + BanditChainRules.GameKey);
            return;
        }
        var row = new SkillTableData
        {
            Key = BanditChainRules.SkillKey,
            DevComment_SkillName = BanditChainRules.SkillKey,
            DevComment_SkillFloatValues = t.DevComment_SkillFloatValues,
            SkillType = t.SkillType,
            IsBasicAttackSkill = t.IsBasicAttackSkill,
            FloatValues = t.FloatValues,
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = t.TargetType,
            AffectTarget = t.AffectTarget,
            Range = BanditChainRules.Range(CoreSettings.Number(BanditChainRules.RangeKey)),
            AreaType = t.AreaType,
            AreaRadius = t.AreaRadius,
            AllowTargetOutside = t.AllowTargetOutside,
            AllowOvercomeWall = t.AllowOvercomeWall,
            IsDelayAdjustedByAttackSpeed = t.IsDelayAdjustedByAttackSpeed,
            StartDelay = t.StartDelay,
            EndDelay = t.EndDelay,
            Cooldown = t.Cooldown,
            Duration = t.Duration,
            EnergyCost = t.EnergyCost,
            DurabilityCost = t.DurabilityCost,
            ProperDurabilityEquip = t.ProperDurabilityEquip,
            RequiredEquipment = t.RequiredEquipment,
            AmmoType = t.AmmoType,
            AmmoAmount = t.AmmoAmount,
            CreatureKey = t.CreatureKey,
            CreatureAmount = t.CreatureAmount,
            RequiredSkillPoint = t.RequiredSkillPoint,
            RequiredLevel = t.RequiredLevel,
            PrerequisiteSkills = t.PrerequisiteSkills,
            Categories = t.Categories
        };
        sheet._skillTable.Add(row.Key, row);
        DelversHost.Info($"CORE_BANDIT_CHAIN_SKILL_REGISTERED key={row.Key} class={BanditChainRules.GameKey} range={row.Range} cooldown={row.Cooldown}");
    }

    internal static Skill_Base CreateSkill()
    {
        var skill = Skill_Base.Create(BanditChainRules.GameKey);
        var data = DataSheetManager.Instance?.GetSkillData(BanditChainRules.SkillKey);
        if (skill == null || data == null) return null;
        skill._Key_k__BackingField = BanditChainRules.SkillKey;
        skill._Data_k__BackingField = data;
        return skill;
    }

    // The game's name and description of the chain under the bandits' key, in the language just parsed.
    internal static void CopyTexts(TextSheet sheet)
    {
        var table = sheet?._textTable;
        if (table == null) return;
        Copy(table, BanditChainRules.NameKey(BanditChainRules.GameKey), BanditChainRules.NameKey(BanditChainRules.SkillKey));
        Copy(table, BanditChainRules.DescKey(BanditChainRules.GameKey), BanditChainRules.DescKey(BanditChainRules.SkillKey));
    }

    private static void Copy(Il2CppSystem.Collections.Generic.Dictionary<string, string> table, string from, string to)
    {
        if (table.TryGetValue(from, out var text) && text != null) table[to] = text;
    }

    // After a save is read: a guard that still carries the game's chain gets the bandits' (BanditChainRules.Migrates).
    internal static void Migrate(SkillComponent skills, IEntity entity)
    {
        if (skills == null || !skills.HasSkill(BanditChainRules.GameKey)) return;
        var key = entity?.Key;
        if (key == null || !key.StartsWith(CoreUnitRules.Prefix, StringComparison.Ordinal)) return;
        var kitHasCopy = false;
        foreach (var unit in CoreUnitRules.Units)
            if (unit.Key == key && Array.IndexOf(unit.Skills, BanditChainRules.SkillKey) >= 0) kitHasCopy = true;
        if (!BanditChainRules.Migrates(true, kitHasCopy, true, skills.HasSkill(BanditChainRules.SkillKey))) return;
        skills.RemoveSkill(BanditChainRules.GameKey);
        skills.AddSkill(BanditChainRules.SkillKey);
        DelversHost.Info(BanditChainRules.MigrateLine(entity.Guid.ToString("D"), key));
    }
}

[HarmonyPatch(typeof(SkillSheet), nameof(SkillSheet.Parse))]
internal static class BanditChainSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { BanditChain.RegisterSkill(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDIT_CHAIN_SKILL_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(Skill_Base), nameof(Skill_Base.Create))]
internal static class BanditChainCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (key != BanditChainRules.SkillKey) return true;
        try { __result = BanditChain.CreateSkill(); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_BANDIT_CHAIN_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

// The chain's effect for the copy: the game builds the effect's class from the key.
[HarmonyPatch(typeof(CombatFX_Base), nameof(CombatFX_Base.Create))]
internal static class BanditChainFxPatch
{
    private static void Prefix(ref string key)
    {
        if (key == BanditChainRules.SkillKey) key = BanditChainRules.GameKey;
    }
}

[HarmonyPatch(typeof(CombatFX_Base), nameof(CombatFX_Base.CreateSkillFX))]
internal static class BanditChainSkillFxPatch
{
    private static void Prefix(ref string key)
    {
        if (key == BanditChainRules.SkillKey) key = BanditChainRules.GameKey;
    }
}

[HarmonyPatch(typeof(ResourceLoader), "LoadSkillIcon")]
internal static class BanditChainIconPatch
{
    private static bool Prefix(ref string key, ref Sprite __result)
    {
        if (key != BanditChainRules.SkillKey) return true;
        var icon = CoreAssets.BanditChainIcon();
        if (icon)
        {
            __result = icon;
            return false;
        }
        key = BanditChainRules.GameKey;
        return true;
    }
}

[HarmonyPatch(typeof(TextSheet), nameof(TextSheet.ParseMatchingLanguage))]
internal static class BanditChainTextPatch
{
    private static void Postfix(TextSheet __instance)
    {
        try { BanditChain.CopyTexts(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDIT_CHAIN_TEXT_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(SkillComponent), nameof(SkillComponent.Deserialize))]
internal static class BanditChainMigratePatch
{
    private static void Postfix(SkillComponent __instance, IEntity entity)
    {
        try { BanditChain.Migrate(__instance, entity); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-chain-migrate", ex); }
    }
}
