using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
#if BEPINEX
using BladeGrindingPassive = global::Refactor.Combat.AffecterRelated.AFFECTER_BladeGrinding.Passive_BladeGrinding;
using CombatFX_Base = global::Refactor.FX.CombatFX_Base;
using SkillEndApplyData = global::Refactor.SkillEndApplyData;
using SkillStartApplyData = global::Refactor.SkillStartApplyData;
using StatUtility = global::Refactor.Component.Status.StatUtility;
#else
using BladeGrindingPassive = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_BladeGrinding.Passive_BladeGrinding;
using CombatFX_Base = Il2CppRefactor.FX.CombatFX_Base;
using SkillEndApplyData = Il2CppRefactor.SkillEndApplyData;
using SkillStartApplyData = Il2CppRefactor.SkillStartApplyData;
using StatUtility = Il2CppRefactor.Component.Status.StatUtility;
#endif

namespace DungeonSettlersDelvers.Core;

// The blade's skill "Serrated Blade" (BanditSerratedRules; it takes Blade Grinding's place with its
// effect): a copy of SKILL_BladeGrinding's row with the bandits' key, the skill the game's Blade Grinding class with that key
// and row, as the chain does (BanditChain). Its effect (the next melee skill +{0} % damage, +{1} % accuracy, the game's
// AFFECTER_BladeGrinding) is the game's; the FX keys are handed on as Blade Grinding's. The author's icon. The extra bleeding
// of every blade hit ({3} % for {2} stacks, EnemyAbilities) stays as it was; the skill shows it in its tooltip.
// CORE_BANDIT_BLADE_PROBE once a session: the game's data of the blade's greatsword and of AFFECTER_ExcessiveBleedingInscription, and once whether a blade carries the inscription.
internal static class BanditSerrated
{
    internal const string TemplateKey = "SKILL_BladeGrinding";
    private const string BladeWeaponKey = "ITEM_BoneGreatSword";
    private static bool staticProbed;
    private static bool unitProbed;

    internal static (double Chance, int Stacks)? Extra
        => BanditSerratedRules.Of(CoreSettings.Number(BanditSerratedRules.ChanceKey), CoreSettings.Number(BanditSerratedRules.StacksKey));

    internal static void RegisterSkill(SkillSheet sheet)
    {
        if (sheet?._skillTable == null || sheet._skillTable.ContainsKey(BanditSerratedRules.SkillKey)) return;
        var t = sheet.GetData(TemplateKey);
        if (t == null)
        {
            DelversHost.Warning("CORE_BANDIT_SERRATED_TEMPLATE_MISSING key=" + TemplateKey);
            return;
        }
        var row = new SkillTableData
        {
            Key = BanditSerratedRules.SkillKey,
            DevComment_SkillName = BanditSerratedRules.SkillKey,
            DevComment_SkillFloatValues = "{0} % damage, {1} % accuracy of the next melee skill; {2} bleeding stacks, {3} % chance per hit (Delvers Core)",
            SkillType = t.SkillType,
            IsBasicAttackSkill = false,
            FloatValues = new Il2CppStructArray<float>(BanditSerratedRules.TooltipValues(t.FloatValues != null && t.FloatValues.Length > 0 ? t.FloatValues[0] : 0f,
                t.FloatValues != null && t.FloatValues.Length > 1 ? t.FloatValues[1] : 0f, Extra)),
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = t.TargetType,
            AffectTarget = t.AffectTarget,
            Range = t.Range,
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
        DelversHost.Info($"CORE_BANDIT_SERRATED_SKILL_REGISTERED key={row.Key} type={row.SkillType} target={row.TargetType} templateTarget={t.TargetType} values={row.FloatValues[0]}/{row.FloatValues[1]}/{row.FloatValues[2]}/{row.FloatValues[3]} cooldown={row.Cooldown}");
    }

    internal static Skill_Base CreateSkill()
    {
        var skill = Skill_Base.Create(TemplateKey);
        var data = DataSheetManager.Instance?.GetSkillData(BanditSerratedRules.SkillKey);
        if (skill == null || data == null) return null;
        skill._Key_k__BackingField = BanditSerratedRules.SkillKey;
        skill._Data_k__BackingField = data;
        return skill;
    }

    // After a save is read: a blade with the old kit (Blade Grinding, Sweeping Cleave) gets the new one (BanditSerratedRules.
    // Migration), in the kit's order. The old passive Serrated Blade keeps its key; it is the active copy now.
    internal static void Migrate(SkillComponent skills, IEntity entity)
    {
        if (skills == null || entity?.Key != BanditSerratedRules.BladeUnitKey) return;
        var changes = new List<string>();
        foreach (var (from, to) in BanditSerratedRules.Migration)
        {
            if (!skills.HasSkill(from)) continue;
            skills.RemoveSkill(from);
            if (!skills.HasSkill(to)) skills.AddSkill(to);
            changes.Add(from + ">" + to);
        }
        // The old passive Serrated Blade sits in the save's passive list; it is an active skill now.
        var passive = skills._passiveSkills?.Contains(BanditSerratedRules.SkillKey) == true;
        if (changes.Count == 0 && !passive) return;
        if (passive) changes.Add("passive>active");
        // The kit's order (the author's slots: Serrated Blade 3, Deadly Fear 4): each kit skill is taken out and added again in
        // that order; the game adds a skill to the end of its list (SkillComponent.AddOrMoveToLast), by its row's type.
        var kit = Array.Find(CoreUnitRules.Units, u => u.Key == BanditSerratedRules.BladeUnitKey)?.Skills ?? Array.Empty<string>();
        foreach (var key in kit)
        {
            if (key == skills.GetBasicAttackSkill() || !skills.HasSkill(key)) continue;
            skills.RemoveSkill(key);
            skills.AddSkill(key);
        }
        DelversHost.Info(BanditSerratedRules.MigrateLine(entity.Guid.ToString("D"), string.Join(",", changes)));
    }

    private static int buffLines;

    // Before the game's Blade Grinding buff reacts to a skill start or end: false (skip the game) for our copy's own key.
    // A Core blade's calls are logged, capped (CORE_BANDIT_SERRATED_BUFF).
    internal static bool OnBuffCall(string at, IEntity owner, string skill)
    {
        var skip = BanditSerratedRules.BuffSkips(skill);
        if (owner != null && owner.Key == BanditSerratedRules.BladeUnitKey && buffLines < BanditSerratedRules.BuffLogCap)
        {
            buffLines++;
            DelversHost.Info(BanditSerratedRules.BuffLine(at, owner.Guid.ToString("D"), skill, skip));
        }
        return !skip;
    }

    private static readonly HashSet<string> penaltyChecked = new(StringComparer.Ordinal);

    // Once per blade and session, at its first decision: an old penalty from before the buff fix (BanditSerratedRules.OldPenalty)
    // is given back through the unit's status (StatusComponent.AddValue).
    internal static void ResetOldPenalty(UnitEntity blade)
    {
        if (blade == null || blade.Key != BanditSerratedRules.BladeUnitKey) return;
        var guid = blade.Guid.ToString("D");
        if (penaltyChecked.Count >= 256) penaltyChecked.Clear();
        if (!penaltyChecked.Add(guid)) return;
        var status = CoreGame.Component<StatusComponent>(blade.Cast<IEntity>());
        var row = DataSheetManager.Instance?.GetSkillData(TemplateKey)?.FloatValues;
        if (status == null || row == null || row.Length < 2) return;
        float defaultHit = 0f, defaultDealt = 0f;
        var defaults = StatUtility.GetDefaultStats(blade.Key);
        for (var i = 0; defaults != null && i < defaults.Count; i++)
        {
            if (defaults[i].Item1 == StatType.HitChance) defaultHit = defaults[i].Item2;
            else if (defaults[i].Item1 == StatType.DamageDealtBonus) defaultDealt = defaults[i].Item2;
        }
        var reader = new StatusReader(status);
        var hit = reader.GetValue(StatType.HitChance);
        var dealt = reader.GetValue(StatType.DamageDealtBonus);
        var steps = BanditSerratedRules.OldPenalty(hit, defaultHit, dealt, defaultDealt, row[1], row[0]);
        if (steps == 0) return;
        status.AddValue(StatType.HitChance, steps * row[1]);
        status.AddValue(StatType.DamageDealtBonus, steps * row[0]);
        DelversHost.Info(BanditSerratedRules.PenaltyLine(guid, steps, hit, dealt));
    }

    // Once a session: the blade's weapon and the inscription as the game's data has them. A probe line of the group OneShot.
    internal static void ProbeStatic()
    {
        if (!CoreProbes.OneShot) return;
        if (staticProbed) return;
        var sheet = DataSheetManager.Instance;
        if (sheet == null) return;
        staticProbed = true;
        var weapon = sheet.GetItemData(BladeWeaponKey);
        DelversHost.Info(BanditSerratedRules.ProbeLine("weapon", weapon == null ? null
            : "key=" + BladeWeaponKey + " subtype=" + weapon.ItemSubType + " onEquip=" + Join(weapon.Equipment_OnEquipAffecters)
              + " defaultSkills=" + Join(weapon.Equipment_ItemDefaultSkills)));
        var inscription = sheet.GetAffecterData(BanditSerratedRules.InscriptionKey);
        DelversHost.Info(BanditSerratedRules.ProbeLine("inscription", inscription == null ? null
            : "key=" + inscription.Key + " type=" + inscription.Type + " maxStack=" + inscription.MaxStack + " values=" + Join(inscription.FloatValues)
              + " comment=\"" + (inscription.DevComment1 ?? "-") + "|" + (inscription.DevComment2 ?? "-") + "|" + (inscription.DevComment3 ?? "-") + "\""));
    }

    // Once: whether the first blade that lands a hit carries the inscription (EnemyAbilities). A probe line of the group OneShot.
    internal static void ProbeUnit(UnitEntity blade)
    {
        if (!CoreProbes.OneShot) return;
        if (unitProbed || blade == null) return;
        unitProbed = true;
        var affecters = CoreGame.Affecters(blade);
        DelversHost.Info(BanditSerratedRules.ProbeLine("blade", "unit=" + CoreGame.GuidOf(blade).ToString("D") + " inscription="
            + (affecters != null && affecters.Has(BanditSerratedRules.InscriptionKey) ? "yes" : "no")
            + " serratedSkill=" + (CoreGame.Component<SkillComponent>(blade.Cast<IEntity>())?.HasSkill(BanditSerratedRules.SkillKey) == true ? "yes" : "no")));
    }

    private static string Join(Il2CppStringArray values)
    {
        if (values == null || values.Length == 0) return "-";
        var parts = new List<string>();
        for (var i = 0; i < values.Length; i++) parts.Add(values[i]);
        return string.Join(",", parts);
    }

    private static string Join(Il2CppStructArray<float> values)
    {
        if (values == null || values.Length == 0) return "-";
        var parts = new List<string>();
        for (var i = 0; i < values.Length; i++) parts.Add(values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join("/", parts);
    }
}

[HarmonyPatch(typeof(SkillSheet), nameof(SkillSheet.Parse))]
internal static class BanditSerratedSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { BanditSerrated.RegisterSkill(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDIT_SERRATED_SKILL_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(Skill_Base), nameof(Skill_Base.Create))]
internal static class BanditSerratedCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (key != BanditSerratedRules.SkillKey) return true;
        try { __result = BanditSerrated.CreateSkill(); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_BANDIT_SERRATED_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

// The author's icon; the game's SKILL_BladeGrinding icon while the file is missing. Every other key passes at once.
[HarmonyPatch(typeof(ResourceLoader), "LoadSkillIcon")]
internal static class BanditSerratedIconPatch
{
    private static bool Prefix(ref string key, ref Sprite __result)
    {
        if (key != BanditSerratedRules.SkillKey) return true;
        var icon = CoreAssets.SerratedBladeIcon();
        if (icon)
        {
            __result = icon;
            return false;
        }
        key = "SKILL_BladeGrinding";
        return true;
    }
}

// Blade Grinding's effect for the copy: the game builds the effect's class from the key (as BanditChainFxPatch).
[HarmonyPatch(typeof(CombatFX_Base), nameof(CombatFX_Base.Create))]
internal static class BanditSerratedFxPatch
{
    private static void Prefix(ref string key)
    {
        if (key == BanditSerratedRules.SkillKey) key = BanditSerrated.TemplateKey;
    }
}

[HarmonyPatch(typeof(CombatFX_Base), nameof(CombatFX_Base.CreateSkillFX))]
internal static class BanditSerratedSkillFxPatch
{
    private static void Prefix(ref string key)
    {
        if (key == BanditSerratedRules.SkillKey) key = BanditSerrated.TemplateKey;
    }
}

[HarmonyPatch(typeof(SkillComponent), nameof(SkillComponent.Deserialize))]
internal static class BanditSerratedMigratePatch
{
    private static void Postfix(SkillComponent __instance, IEntity entity)
    {
        try { BanditSerrated.Migrate(__instance, entity); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-serrated-migrate", ex); }
    }
}

// The game's Blade Grinding buff skips only its own key; our copy is skipped the same way (BanditSerratedRules.BuffSkips).
[HarmonyPatch(typeof(BladeGrindingPassive), "OnDamage")]
internal static class BanditSerratedBuffStartPatch
{
    private static bool Prefix(SkillStartApplyData data)
    {
        try { return data == null || BanditSerrated.OnBuffCall("start", data.Owner, data.Key); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDIT_SERRATED_BUFF_FAILED start: " + ex.Message);
            return true;
        }
    }
}

[HarmonyPatch(typeof(BladeGrindingPassive), "AfterDamage")]
internal static class BanditSerratedBuffEndPatch
{
    private static bool Prefix(SkillEndApplyData data)
    {
        try { return data == null || BanditSerrated.OnBuffCall("end", data.Owner, data.Key); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_BANDIT_SERRATED_BUFF_FAILED end: " + ex.Message);
            return true;
        }
    }
}
