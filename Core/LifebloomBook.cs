using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
#if BEPINEX
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using OintmentRoutine = global::Refactor.Combat.SkillRelated.SKILL_Ointment._SkillRoutine_d__0;
#else
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using OintmentRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_Ointment._SkillRoutine_d__0;
#endif

namespace DungeonSettlersDelvers.Core;

// The author's skill book for Lifebloom (LifebloomBookRules). The item and its skill are rows like the bandages': the item
// from the ointment's row, the skill the ointment's class aimed at the user (so Q finds a skill key), whose routine Core
// takes over for the book: Lifebloom.Learn, and the book is used up. The use is checked before the book leaves the bag
// (ActiveSkill_Base.CheckForUseItem): party member, the bandits' option, the skill row, not known yet. Its own icon
// (CoreAssets.LifebloomBookIcon), the scroll's icon without the file. No lambdas (see ReviveScroll).
internal static class LifebloomBook
{
    private const string ItemTemplateKey = "ITEM_Ointment";
    private const string SkillTemplateKey = "SKILL_Ointment";
    private const string IconFallback = "ITEM_ReturnScroll";
    private const float ReadSeconds = 2f;

    internal static void Initialize() => CoreSaveItems.Register(LifebloomBookRules.ItemKey, LifebloomBookRules.StandIn);

    internal static bool IsOurSkill(string skillKey) => skillKey == LifebloomBookRules.SkillKey;

    internal static void RegisterItem(ItemSheet sheet)
    {
        if (sheet?._itemTable == null || sheet._itemTable.ContainsKey(LifebloomBookRules.ItemKey)) return;
        if (!sheet._itemTable.TryGetValue(ItemTemplateKey, out var t) || t == null)
        {
            DelversHost.Warning("CORE_LIFEBLOOM_BOOK_TEMPLATE_MISSING key=" + ItemTemplateKey);
            return;
        }
        var row = new ItemTableData
        {
            Key = LifebloomBookRules.ItemKey,
            DevComment1 = LifebloomBookRules.ItemKey,
            DevComment2 = "Delvers Core: skill book, teaches Lifebloom; written as " + LifebloomBookRules.StandIn + " in the game's save.",
            DevIndex = t.DevIndex,
            Rarity = (Rarity)LifebloomBookRules.RarityValue,
            ItemType = t.ItemType,
            ItemSubType = t.ItemSubType,
            StandardPrice = LifebloomBookRules.Price,
            RandomTags = t.RandomTags,
            RandomTagAmount = t.RandomTagAmount,
            MaxStack = t.MaxStack,
            Durability = t.Durability,
            ExpireDuration = t.ExpireDuration,
            Weight = t.Weight,
            HungerRestore = t.HungerRestore,
            EquipmentType = t.EquipmentType,
            IsEdible = false,
            IsUsable = true,
            IsWeapon = false,
            UseSkill = LifebloomBookRules.SkillKey,
            Equipment_OnEquipAffecters = t.Equipment_OnEquipAffecters,
            Equipment_ItemDefaultSkills = t.Equipment_ItemDefaultSkills,
            Equipment_MaxHealthTotal = t.Equipment_MaxHealthTotal,
            Equipment_PhysicalAttackPower = t.Equipment_PhysicalAttackPower,
            Equipment_MagicAttackPower = t.Equipment_MagicAttackPower,
            Equipment_AdditionalStatuses = t.Equipment_AdditionalStatuses,
            RequiredAmmoType = t.RequiredAmmoType,
            RequiredItemsRepair = t.RequiredItemsRepair
        };
        sheet._itemTable.Add(row.Key, row);
        sheet._itemTypes.Add(row.Key, row.ItemType);
        if (!sheet._itemsByType.ContainsKey(row.ItemType))
            sheet._itemsByType.Add(row.ItemType, new Il2CppSystem.Collections.Generic.List<ItemTableData>());
        sheet._itemsByType[row.ItemType].Add(row);
        DelversHost.Info($"CORE_LIFEBLOOM_BOOK_ITEM_REGISTERED key={row.Key} rarity={row.Rarity} price={row.StandardPrice} standIn={LifebloomBookRules.StandIn}");
    }

    internal static void RegisterSkill(SkillSheet sheet)
    {
        if (sheet?._skillTable == null || sheet._skillTable.ContainsKey(LifebloomBookRules.SkillKey)) return;
        var t = sheet.GetData(SkillTemplateKey);
        if (t == null)
        {
            DelversHost.Warning("CORE_LIFEBLOOM_BOOK_TEMPLATE_MISSING key=" + SkillTemplateKey);
            return;
        }
        var row = new SkillTableData
        {
            Key = LifebloomBookRules.SkillKey,
            DevComment_SkillName = LifebloomBookRules.SkillKey,
            DevComment_SkillFloatValues = t.DevComment_SkillFloatValues,
            SkillType = t.SkillType,
            IsBasicAttackSkill = false,
            FloatValues = t.FloatValues,
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = t.TargetType,          // SelfTarget: the unit reads its own book
            AffectTarget = t.AffectTarget,
            Range = t.Range,
            AreaType = t.AreaType,
            AreaRadius = t.AreaRadius,
            AllowTargetOutside = t.AllowTargetOutside,
            AllowOvercomeWall = t.AllowOvercomeWall,
            IsDelayAdjustedByAttackSpeed = false,
            StartDelay = ReadSeconds,
            EndDelay = t.EndDelay,
            Cooldown = 0f,
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
        DelversHost.Info($"CORE_LIFEBLOOM_BOOK_SKILL_REGISTERED key={row.Key} class={SkillTemplateKey} delay={row.StartDelay}");
    }

    // Checked when the player uses the book, before it leaves the bag; false refuses it (the book stays).
    internal static bool AllowUse(SkillExecutionContext context)
    {
        var item = context?.UseItem;
        if (item == null || item.Key != LifebloomBookRules.ItemKey) return true;
        var user = context.User?.TryCast<UnitEntity>();
        var reason = LifebloomBookRules.RefuseReason(user != null && !user.IsDead && CoreGame.TryMapOfPartyMember(user, out _),
            ApocalypseRules.Bandits, Lifebloom.Active, user != null && Lifebloom.Knows(user));
        if (reason == null) return true;
        DelversHost.Info(LifebloomBookRules.RefusedLine(CoreGame.GuidOf(user).ToString("D"), reason));
        return false;
    }

    // The ointment's routine, at its start, for the book: Core teaches Lifebloom instead. True when taken over.
    internal static bool TakeOver(OintmentRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        var context = routine.context;
        if (context?.UseItem == null || context.UseItem.Key != LifebloomBookRules.ItemKey) return false;
        routine.__1__state = -1;
        var user = context.User?.TryCast<UnitEntity>();
        var refreshed = false;
        var learned = user != null && Lifebloom.Learn(user, out refreshed);
        if (learned) Notice(user);
        DelversHost.Info(LifebloomBookRules.UsedLine(CoreGame.GuidOf(user).ToString("D"), learned, refreshed));
        return true;
    }

    // As the game's help notice, the way the soul
    // stones announce their trait (TraitAcquisitionItemSkillBase.CreateTraitAcquiredNotice: the display name as argument).
    private static void Notice(UnitEntity unit)
    {
        if (!CoreGame.TryMapOfPartyMember(unit, out var map)) return;
        UniqueCandidateLocalization.EnsureCurrent();
        var args = new Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object>();
        args.Add(new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(EntityDisplayNameExtensions.GetDisplayName(unit) ?? "")));
        CoreGame.Apply(map, new HelpNoticeApplyData(LifebloomBookRules.LearnedNoticeKey, args).Cast<IApplyData>());
    }

    internal static Sprite Icon(ResourceLoader loader)
    {
        var own = CoreAssets.LifebloomBookIcon();
        if (own) return own;
        try { return loader?.LoadItemIcon(IconFallback); }
        catch (Exception) { return null; }
    }
}

[HarmonyPatch(typeof(ItemSheet), nameof(ItemSheet.Parse))]
internal static class LifebloomBookItemSheetPatch
{
    private static void Postfix(ItemSheet __instance)
    {
        try { LifebloomBook.RegisterItem(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_LIFEBLOOM_BOOK_ITEM_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(SkillSheet), nameof(SkillSheet.Parse))]
internal static class LifebloomBookSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { LifebloomBook.RegisterSkill(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_LIFEBLOOM_BOOK_SKILL_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(Skill_Base), nameof(Skill_Base.Create))]
internal static class LifebloomBookCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (!LifebloomBook.IsOurSkill(key)) return true;
        try { __result = BandageContent.CreateSkill(key); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_LIFEBLOOM_BOOK_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

[HarmonyPatch(typeof(ActiveSkill_Base), nameof(ActiveSkill_Base.CheckForUseItem))]
internal static class LifebloomBookUseGatePatch
{
    private static void Postfix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if ((int)__result < 0) return;
        try
        {
            if (!LifebloomBook.AllowUse(context)) __result = ExecutionCheckResult.Failed_UnvalidTarget;
        }
        catch (Exception ex) { ApocalypseHooks.Report("lifebloom-book-gate", ex); }
    }
}

[HarmonyPatch(typeof(OintmentRoutine), "MoveNext")]
internal static class LifebloomBookRoutinePatch
{
    private static bool Prefix(OintmentRoutine __instance, ref bool __result)
    {
        try
        {
            if (!LifebloomBook.TakeOver(__instance)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("lifebloom-book", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.LoadItemIcon))]
internal static class LifebloomBookIconPatch
{
    private static bool Prefix(ResourceLoader __instance, string key, ref Sprite __result)
    {
        if (key != LifebloomBookRules.ItemKey) return true;
        var icon = LifebloomBook.Icon(__instance);
        if (icon == null) return true;
        __result = icon;
        return false;
    }
}
