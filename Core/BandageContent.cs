using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using static DungeonSettlersDelvers.Core.Bandages;

namespace DungeonSettlersDelvers.Core;

// BandageContent, what the bandages put into the game's tables: the item and skill rows copied from the ointment's
// templates, the merchant offers and the placeholder icons, with the five patch classes that apply them. The keys, the
// use check and the ointment's routine stay in Bandages.cs.
internal static class BandageContent
{
    private const string ItemTemplateKey = "ITEM_Ointment";
    private const string SkillTemplateKey = "SKILL_Ointment";
    private const float PutOnSeconds = 2f;

    // The salve's own FloatValues for the ointment's routine, in the template's order: seconds,
    // health. The routine reads only the seconds (the duration of AFFECTER_Ointment); the heal rate
    // comes from the effect, which is bound to SKILL_Ointment (10 s, 300 health: 30 a second). So the
    // salve heals 800 in 80/3 s (800 over 27 to 30 s, no effect of its own); the
    // 800 only states the sum (SkillTable/AffecterTable; Passive_Ointment.HealRoutine: values[1] / values[0] x LogicalRefresh x
    // Tick per step, 30.06 for SKILL_Ointment, not rounded).
    private static readonly float[] SalveHeal = { 80f / 3f, 800f };

    // Starting values, the author calibrates: guild merchant always light, often heavy; dungeon
    // merchants sometimes.
    private static readonly (string Item, float Chance, int Min, int Max)[] GuildOffers =
    {
        (LightKey, 100f, 2, 4),
        (HeavyKey, 60f, 1, 2)
    };

    private static readonly (string Pool, string Item, float Chance, int Min, int Max)[] DungeonOffers =
    {
        ("TRADEPOOL_VeilSilenceMerchant", LightKey, 40f, 1, 2),
        ("TRADEPOOL_VeilSilenceMerchant", HeavyKey, 20f, 1, 1),
        ("TRADEPOOL_FesteringBurrowMerchant", LightKey, 40f, 1, 2),
        ("TRADEPOOL_FesteringBurrowMerchant", HeavyKey, 20f, 1, 1)
    };

    private static readonly List<(CoreMerchantPools.Offer Offer, string Item, int Slot)> placed = new();
    private static TradePoolSheet pools;

    internal static void ResetLifecycle()
    {
        placed.Clear();
        pools = null;
    }

    private static bool iconProbed;

    // Once per session, when the game first asks for one of our icons: the native base icon's size and
    // atlas (BandageIconRules, CORE_BANDAGE_ICON_PROBE). Reads only. The icon patch below is not a probe class: it has the
    // bandages' icons as its work and calls this, so the probe asks the group OneShot first (CoreProbes).
    internal static void ProbeIcon(ResourceLoader loader)
    {
        if (!CoreProbes.OneShot) return;
        if (iconProbed || loader == null) return;
        iconProbed = true;
        try
        {
            var sprite = loader.LoadItemIcon(BandageIconRules.BaseIcon);
            if (sprite == null)
            {
                DelversHost.Info(BandageIconRules.ProbeLine(BandageIconRules.BaseIcon, false, 0, 0, 0, 0, 0f, false, false, "-"));
                return;
            }
            var texture = sprite.texture;
            DelversHost.Info(BandageIconRules.ProbeLine(BandageIconRules.BaseIcon, true, (int)sprite.rect.width, (int)sprite.rect.height,
                texture != null ? texture.width : 0, texture != null ? texture.height : 0, sprite.pixelsPerUnit,
                texture != null && texture.isReadable, sprite.packed, texture != null ? texture.filterMode.ToString() : "-"));
        }
        catch (Exception ex) { DelversHost.Warning("CORE_BANDAGE_ICON_PROBE_FAILED: " + ex.Message); }
    }

    // Placeholder icons from the game's goods: light like cloth, heavy like thick cloth,
    // herbal like wild herbs, salve like the ointment.
    internal static string IconFor(string itemKey) => itemKey switch
    {
        SalveKey => "ITEM_Ointment",
        HerbalKey => "ITEM_WildHerb",
        HeavyKey => "ITEM_ThickTextile",
        LightKey => "ITEM_Textile",
        _ => null
    };

    // ---- rows ----

    internal static void RegisterItems(ItemSheet sheet)
    {
        if (sheet?._itemTable == null) return;
        if (!sheet._itemTable.TryGetValue(ItemTemplateKey, out var t) || t == null)
        {
            DelversHost.Warning("CORE_BANDAGE_TEMPLATE_MISSING key=" + ItemTemplateKey);
            return;
        }
        AddItem(sheet, t, LightKey, LightSkillKey, WoundRules.LightBandagePrice, Rarity.Common);
        AddItem(sheet, t, HeavyKey, HeavySkillKey, WoundRules.HeavyBandagePrice, Rarity.Uncommon);
        AddItem(sheet, t, HerbalKey, HerbalSkillKey, WoundRules.HerbalBandagePrice, Rarity.Uncommon);
        AddItem(sheet, t, SalveKey, SalveSkillKey, WoundRules.SalveBandagePrice, Rarity.Rare);
    }

    private static void AddItem(ItemSheet sheet, ItemTableData t, string key, string skill, int price, Rarity rarity)
    {
        if (sheet._itemTable.ContainsKey(key)) return;
        var row = new ItemTableData
        {
            Key = key,
            DevComment1 = key,
            DevComment2 = "Delvers Core: bandage for the bleeding wound; written as " + StandIn(key) + " in the game's save.",
            DevIndex = t.DevIndex,
            Rarity = rarity,
            ItemType = t.ItemType,
            ItemSubType = t.ItemSubType,
            StandardPrice = price,
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
            UseSkill = skill,
            Equipment_OnEquipAffecters = t.Equipment_OnEquipAffecters,
            Equipment_ItemDefaultSkills = t.Equipment_ItemDefaultSkills,
            Equipment_MaxHealthTotal = t.Equipment_MaxHealthTotal,
            Equipment_PhysicalAttackPower = t.Equipment_PhysicalAttackPower,
            Equipment_MagicAttackPower = t.Equipment_MagicAttackPower,
            Equipment_AdditionalStatuses = t.Equipment_AdditionalStatuses,
            RequiredAmmoType = t.RequiredAmmoType,
            RequiredItemsRepair = t.RequiredItemsRepair
        };
        CoreItemRows.Add(sheet, row);
        DelversHost.Info($"CORE_BANDAGE_ITEM_REGISTERED key={key} template={ItemTemplateKey} price={price}");
    }

    internal static void RegisterSkills(SkillSheet sheet)
    {
        if (sheet?._skillTable == null) return;
        var t = sheet.GetData(SkillTemplateKey);
        if (t == null)
        {
            DelversHost.Warning("CORE_BANDAGE_TEMPLATE_MISSING key=" + SkillTemplateKey);
            return;
        }
        AddSkill(sheet, t, LightSkillKey, null);
        AddSkill(sheet, t, HeavySkillKey, null);
        AddSkill(sheet, t, HerbalSkillKey, null);
        AddSkill(sheet, t, SalveSkillKey, new Il2CppStructArray<float>(SalveHeal));
        // On another unit: the aid kit's target, reach and filter (an entity target, reach 1).
        var other = sheet.GetData(GroupAiBandageRules.AidKitSkillKey);
        if (other == null)
        {
            DelversHost.Warning("CORE_BANDAGE_TEMPLATE_MISSING key=" + GroupAiBandageRules.AidKitSkillKey);
            return;
        }
        AddSkill(sheet, t, LightOtherSkillKey, null, other);
        AddSkill(sheet, t, HeavyOtherSkillKey, null, other);
        AddSkill(sheet, t, HerbalOtherSkillKey, null, other);
        AddSkill(sheet, t, SalveOtherSkillKey, new Il2CppStructArray<float>(SalveHeal), other);
        // The game's aid kit, ointment and raw herbs on another unit (UseOnOther): Core dresses the wound (Bandages.TakeOver).
        AddSkill(sheet, t, AidKitOtherSkillKey, null, other);
        AddSkill(sheet, t, OintmentOtherSkillKey, null, other);
        AddSkill(sheet, t, WildHerbOtherSkillKey, null, other);
    }

    // floatValues: the skill's own values, null for the template's; aim: the row whose target, filter and reach the skill takes,
    // null for the template's (the unit itself).
    private static void AddSkill(SkillSheet sheet, SkillTableData t, string key, Il2CppStructArray<float> floatValues, SkillTableData aim = null)
    {
        aim ??= t;
        if (sheet._skillTable.ContainsKey(key)) return;
        var row = new SkillTableData
        {
            Key = key,
            DevComment_SkillName = key,
            DevComment_SkillFloatValues = t.DevComment_SkillFloatValues,
            SkillType = t.SkillType,
            IsBasicAttackSkill = false,
            FloatValues = floatValues ?? t.FloatValues,
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = aim.TargetType,        // the template's SelfTarget, or the aid kit's entity target
            AffectTarget = aim.AffectTarget,
            Range = aim.Range,
            AreaType = t.AreaType,
            AreaRadius = t.AreaRadius,
            AllowTargetOutside = t.AllowTargetOutside,
            AllowOvercomeWall = t.AllowOvercomeWall,
            IsDelayAdjustedByAttackSpeed = false,
            StartDelay = PutOnSeconds,
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
        sheet._skillTable.Add(key, row);
        DelversHost.Info($"CORE_BANDAGE_SKILL_REGISTERED key={key} class={SkillTemplateKey} delay={row.StartDelay}");
    }

    // The ointment's class with our key and data.
    internal static Skill_Base CreateSkill(string key)
    {
        var skill = Skill_Base.Create(SkillTemplateKey);
        var data = DataSheetManager.Instance?.GetSkillData(key);
        if (skill == null || data == null) return null;
        skill._Key_k__BackingField = key;
        skill._Data_k__BackingField = data;
        return skill;
    }

    // ---- merchants ----

    internal static void AddToMerchants(TradePoolSheet sheet)
    {
        var table = sheet?._farmingTable;
        if (table == null) return;
        pools = sheet;
        placed.Clear();
        foreach (var guild in CoreMerchantPools.GuildPools(table))
            foreach (var offer in GuildOffers)
                Place(table, new CoreMerchantPools.Offer(guild, offer.Chance, offer.Min, offer.Max, -10f, 10f), offer.Item);
        foreach (var offer in DungeonOffers)
            Place(table, new CoreMerchantPools.Offer(offer.Pool, offer.Chance, offer.Min, offer.Max, -25f, 25f), offer.Item);
        DelversHost.Info("CORE_BANDAGE_MERCHANTS entries=" + placed.Count);
        ApplyMerchantChances();
    }

    private static void Place(Il2CppSystem.Collections.Generic.Dictionary<string, TradePoolTableData> table,
        CoreMerchantPools.Offer offer, string item)
    {
        var slot = CoreMerchantPools.Insert(table, item, offer, 0f);
        if (slot >= 0) placed.Add((offer, item, slot));
    }

    // Only where wounds exist (combat Apocalypse or the option, ApocalypseRules). Called when a
    // campaign is bound.
    internal static void ApplyMerchantChances()
    {
        var table = pools?._farmingTable;
        if (table == null) return;
        var offered = ApocalypseRules.Wounds;
        foreach (var entry in placed)
            CoreMerchantPools.SetChance(table, entry.Offer.Pool, entry.Slot, offered ? entry.Offer.Chance : 0f);
        DelversHost.Info("CORE_BANDAGE_MERCHANTS offered=" + (offered ? "true" : "false") + " entries=" + placed.Count);
    }
}

[HarmonyPatch(typeof(ItemSheet), nameof(ItemSheet.Parse))]
internal static class BandageItemSheetPatch
{
    private static void Postfix(ItemSheet __instance)
    {
        try { BandageContent.RegisterItems(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDAGE_ITEM_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(SkillSheet), nameof(SkillSheet.Parse))]
internal static class BandageSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { BandageContent.RegisterSkills(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDAGE_SKILL_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(TradePoolSheet), nameof(TradePoolSheet.ParseTable))]
internal static class BandageTradePoolPatch
{
    private static void Postfix(TradePoolSheet __instance)
    {
        try { BandageContent.AddToMerchants(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_BANDAGE_MERCHANTS_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(Skill_Base), nameof(Skill_Base.Create))]
internal static class BandageCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (!Bandages.IsOurSkill(key)) return true;
        try { __result = BandageContent.CreateSkill(key); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_BANDAGE_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

// The bandages' own icons (CoreAssets.BandageIcon); without the file the placeholder from the game's goods
// (BandageContent.IconFor). The boss bandits' parts take their icon here too (BossItemLook.Icon).
[HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.LoadItemIcon))]
internal static class BandageIconPatch
{
    private static bool Prefix(ResourceLoader __instance, string key, ref Sprite __result)
    {
        if (BossItemRules.IsBossItem(key))
        {
            try { __result = BossItemLook.Icon(__instance, key); }
            catch (Exception) { __result = null; }
            return __result == null;
        }
        var icon = BandageContent.IconFor(key);
        if (icon == null) return true;
        BandageContent.ProbeIcon(__instance);
        var own = CoreAssets.BandageIcon(key);
        if (own)
        {
            __result = own;
            return false;
        }
        try
        {
            __result = __instance.LoadItemIcon(icon);
            return __result == null;
        }
        catch (Exception) { return true; }
    }
}
