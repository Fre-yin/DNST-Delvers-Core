using System.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using UseSkillMode = global::Refactor.Control.UseSkillMode;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using ItemUseCondition = global::Refactor.Combat.ItemUseCondition;
using ReturnScrollRoutine = global::Refactor.Combat.SkillRelated.SKILL_ReturnScroll._SkillRoutine_d__0;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using UseSkillMode = Il2CppRefactor.Control.UseSkillMode;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using ItemUseCondition = Il2CppRefactor.Combat.ItemUseCondition;
using ReturnScrollRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_ReturnScroll._SkillRoutine_d__0;
#endif

namespace DungeonSettlersDelvers.Core;

// The revive scroll. A living party member uses it on the tile of a dead
// party member's body; after 20 s the scroll is used up and Core's revive runs (ReviveService).
// - Item and look of the native return scroll, with a yellow-white glow around the icon; in the
//   game's part of a save it is a return scroll (CoreSaveItems), so a save stays loadable without
//   Core.
// - The skill row comes from SKILL_GoddessTears (the game's only item aimed at a tile), the class
//   from SKILL_ReturnScroll; its routine is taken over for our key.
// - The body is checked during the whole cast (ItemUseCondition is checked every tick of an item
//   cast and before the scroll is used up); a stun aborts like every native cast, and the scroll
//   stays.
// - Sold by merchants only: the guild merchant always, dungeon merchants rarely and with a wider
//   price swing. The price adds a combat and a management part (ReviveRules.Price), set per
//   campaign.
// No lambdas here: a lambda as a MatchEvaluator and one in ToDictionary broke with CS0656 (NullableAttribute, which the
// IL2CPP reference set lacks); plain loops are safe.
internal static class ReviveScroll
{
    internal const string ItemKey = CoreSaveItems.Prefix + "ReviveScroll";
    internal const string SkillKey = "SKILL_DelversCoreReviveScroll";
    internal const string NativeItemKey = "ITEM_ReturnScroll";
    private const string RowTemplateKey = "SKILL_GoddessTears";
    private const string ClassTemplateKey = "SKILL_ReturnScroll";
    internal const float CastSeconds = 20f;
    private const float GateCacheSeconds = 0.25f;

    // Merchant pools: (pool, appear chance %, min, max, discount from, to). Values are a first step.
    // The guild merchant's pool key is "TRADEPOOL_GuildMerchant" plus the clan rank (rank based refresh) or
    // "TRADEPOOL_GuildMerchantSafe" on safe floors (ItemEventHandler.OnEvent(TradePoolInitializeRequested),
    // AFFECTER_GuildTrade). The tables have ranks 1 to 3 only (same line; also in DS_B.0.4.23), so every pool
    // with that prefix gets the scroll.
    private const string GuildPoolPrefix = "TRADEPOOL_GuildMerchant";
    private static readonly (float Chance, int Min, int Max, float DiscountFrom, float DiscountTo) GuildOffer = (100f, 1, 2, -10f, 10f);
    private static readonly (string Pool, float Chance, int Min, int Max, float DiscountFrom, float DiscountTo)[] DungeonPools =
    {
        ("TRADEPOOL_VeilSilenceMerchant", 30f, 1, 1, -25f, 25f),
        ("TRADEPOOL_FesteringBurrowMerchant", 30f, 1, 1, -25f, 25f)
    };

    private static readonly List<(string Pool, float Chance, int Min, int Max, float DiscountFrom, float DiscountTo)> Pools = new();
    private static readonly Dictionary<string, int> poolSlots = new(StringComparer.Ordinal);
    private static TradePoolSheet pools;
    private static ItemTableData itemRow;
    private static Sprite glowIcon;
    private static Guid gateTarget;
    private static Vector2Int gateCell;
    private static float gateUntil;
    private static bool gateResult;
    // The last logged cast check; a line is written only when it changes, not every tick.
    private static string lastCastCheck;

    internal static void Initialize() => CoreSaveItems.Register(ItemKey, NativeItemKey);

    internal static void ResetLifecycle()
    {
        poolSlots.Clear();
        pools = null;
        itemRow = null;
        glowIcon = null;
        gateUntil = 0f;
        lastCastCheck = null;
    }

    // ---- rows ----

    internal static void RegisterItem(ItemSheet sheet)
    {
        if (sheet?._itemTable == null || sheet._itemTable.ContainsKey(ItemKey)) return;
        if (!sheet._itemTable.TryGetValue(NativeItemKey, out var t) || t == null)
        {
            DelversHost.Warning("CORE_REVIVE_SCROLL_TEMPLATE_MISSING key=" + NativeItemKey);
            return;
        }
        // Blue in the tooltip; the game colours each rarity.
        const Rarity rarity = Rarity.Rare;
        var row = new ItemTableData
        {
            Key = ItemKey,
            DevComment1 = "Revive scroll",
            DevComment2 = "Delvers Core: revives a dead party member; written as " + NativeItemKey + " in the game's save.",
            DevIndex = t.DevIndex,
            Rarity = rarity,
            ItemType = t.ItemType,
            ItemSubType = t.ItemSubType,
            StandardPrice = ReviveRules.Price(ReviveRules.Level.Normal, ReviveRules.Level.Normal),
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
            UseSkill = SkillKey,
            Equipment_OnEquipAffecters = t.Equipment_OnEquipAffecters,
            Equipment_ItemDefaultSkills = t.Equipment_ItemDefaultSkills,
            Equipment_MaxHealthTotal = t.Equipment_MaxHealthTotal,
            Equipment_PhysicalAttackPower = t.Equipment_PhysicalAttackPower,
            Equipment_MagicAttackPower = t.Equipment_MagicAttackPower,
            Equipment_AdditionalStatuses = t.Equipment_AdditionalStatuses,
            RequiredAmmoType = t.RequiredAmmoType,
            RequiredItemsRepair = t.RequiredItemsRepair
        };
        // Like the game's own Parse loop: the row, its type, and the list per type.
        sheet._itemTable.Add(ItemKey, row);
        sheet._itemTypes.Add(ItemKey, row.ItemType);
        if (!sheet._itemsByType.ContainsKey(row.ItemType))
            sheet._itemsByType.Add(row.ItemType, new Il2CppSystem.Collections.Generic.List<ItemTableData>());
        sheet._itemsByType[row.ItemType].Add(row);
        itemRow = row;
        DelversHost.Info($"CORE_REVIVE_SCROLL_ITEM_REGISTERED key={ItemKey} template={NativeItemKey} rarity={rarity}");
    }

    internal static void RegisterSkill(SkillSheet sheet)
    {
        if (sheet?._skillTable == null || sheet._skillTable.ContainsKey(SkillKey)) return;
        var t = sheet.GetData(RowTemplateKey);
        var shape = sheet.GetData(ClassTemplateKey);
        if (t == null || shape == null)
        {
            DelversHost.Warning("CORE_REVIVE_SCROLL_TEMPLATE_MISSING key=" + RowTemplateKey + "/" + ClassTemplateKey);
            return;
        }
        var row = new SkillTableData
        {
            Key = SkillKey,
            DevComment_SkillName = "Revive scroll",
            DevComment_SkillFloatValues = t.DevComment_SkillFloatValues,
            SkillType = t.SkillType,
            IsBasicAttackSkill = false,
            FloatValues = t.FloatValues,
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = t.TargetType,          // GridTarget: aimed at the body's tile
            AffectTarget = t.AffectTarget,
            Range = 1,                          // next to the body, like the aid kit
            AreaType = shape.AreaType,
            AreaRadius = shape.AreaRadius,
            AllowTargetOutside = t.AllowTargetOutside,
            AllowOvercomeWall = t.AllowOvercomeWall,
            IsDelayAdjustedByAttackSpeed = false,
            StartDelay = CastSeconds,
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
        sheet._skillTable.Add(SkillKey, row);
        DelversHost.Info($"CORE_REVIVE_SCROLL_SKILL_REGISTERED key={SkillKey} class={ClassTemplateKey} range={row.Range} delay={row.StartDelay}");
    }

    // The class comes from the return scroll, key and data are ours.
    internal static Skill_Base CreateSkill()
    {
        var skill = Skill_Base.Create(ClassTemplateKey);
        var data = DataSheetManager.Instance?.GetSkillData(SkillKey);
        if (skill == null || data == null) return null;
        skill._Key_k__BackingField = SkillKey;
        skill._Data_k__BackingField = data;
        return skill;
    }

    // ---- merchants and price ----

    internal static void AddToMerchants(TradePoolSheet sheet)
    {
        var table = sheet?._farmingTable;
        if (table == null) return;
        pools = sheet;
        CollectPools(table);
        foreach (var entry in Pools)
        {
            if (!table.TryGetValue(entry.Pool, out var pool) || pool?.AvailableItems == null) continue;
            var items = pool.AvailableItems;
            var count = items.Length;
            var existing = -1;
            for (var i = 0; i < count; i++) if (items[i] == ItemKey) existing = i;
            if (existing >= 0) { poolSlots[entry.Pool] = existing; continue; }
            // All six arrays must keep the same length: the restock loop runs over the chances.
            if (pool.AppearChancePers.Length != count || pool.MinAmounts.Length != count || pool.MaxAmounts.Length != count
                || pool.MinDiscountPers.Length != count || pool.MaxDiscountPers.Length != count)
            {
                DelversHost.Warning("CORE_REVIVE_SCROLL_POOL_SKIPPED pool=" + entry.Pool + " reason=uneven-arrays");
                continue;
            }
            var newItems = new Il2CppStringArray(count + 1);
            var chances = new Il2CppStructArray<float>(count + 1);
            var mins = new Il2CppStructArray<int>(count + 1);
            var maxs = new Il2CppStructArray<int>(count + 1);
            var discountFrom = new Il2CppStructArray<float>(count + 1);
            var discountTo = new Il2CppStructArray<float>(count + 1);
            for (var i = 0; i < count; i++)
            {
                newItems[i] = items[i];
                chances[i] = pool.AppearChancePers[i];
                mins[i] = pool.MinAmounts[i];
                maxs[i] = pool.MaxAmounts[i];
                discountFrom[i] = pool.MinDiscountPers[i];
                discountTo[i] = pool.MaxDiscountPers[i];
            }
            newItems[count] = ItemKey;
            chances[count] = CoreSettings.ReviveScrollOffered ? entry.Chance : 0f;
            mins[count] = entry.Min;
            maxs[count] = entry.Max;
            discountFrom[count] = entry.DiscountFrom;
            discountTo[count] = entry.DiscountTo;
            pool.AvailableItems = newItems;
            pool.AppearChancePers = chances;
            pool.MinAmounts = mins;
            pool.MaxAmounts = maxs;
            pool.MinDiscountPers = discountFrom;
            pool.MaxDiscountPers = discountTo;
            poolSlots[entry.Pool] = count;
        }
        var names = new List<string>(poolSlots.Keys);
        names.Sort(StringComparer.Ordinal);
        DelversHost.Info("CORE_REVIVE_SCROLL_MERCHANTS pools=" + poolSlots.Count + " names=" + string.Join(",", names));
    }

    // Every guild pool of the loaded table (all ranks and the safe floor), then the dungeon merchants.
    private static void CollectPools(Il2CppSystem.Collections.Generic.Dictionary<string, TradePoolTableData> table)
    {
        var guild = new List<string>();
        foreach (var key in table.Keys)
            if (key != null && key.StartsWith(GuildPoolPrefix, StringComparison.Ordinal)) guild.Add(key);
        guild.Sort(StringComparer.Ordinal);
        Pools.Clear();
        foreach (var key in guild)
            Pools.Add((key, GuildOffer.Chance, GuildOffer.Min, GuildOffer.Max, GuildOffer.DiscountFrom, GuildOffer.DiscountTo));
        Pools.AddRange(DungeonPools);
    }

    // The Delvers menu switch: the pools are read at every restock, so the chance changes at once.
    internal static void ApplyMerchantSetting()
    {
        var table = pools?._farmingTable;
        if (table == null) return;
        var offered = CoreSettings.ReviveScrollOffered;
        foreach (var entry in Pools)
            if (poolSlots.TryGetValue(entry.Pool, out var slot) && table.TryGetValue(entry.Pool, out var pool) && pool != null
                && slot < pool.AppearChancePers.Length)
                pool.AppearChancePers[slot] = offered ? entry.Chance : 0f;
    }

    // Per campaign: combat part plus management part. Read live at every trade and tooltip.
    internal static void ApplyPrice()
    {
        var row = itemRow ?? DataSheetManager.Instance?.GetItemData(ItemKey);
        if (row == null) return;
        var price = ReviveRules.Price(CoreGame.CombatLevel(), CoreGame.ManagementLevel());
        if (row.StandardPrice == price) return;
        row.StandardPrice = price;
        DelversHost.Info("CORE_REVIVE_SCROLL_PRICE price=" + price);
    }

    // ---- the cast ----

    // The click on a tile: only a tile with a body that can be revived counts. Otherwise no order
    // exists, so no unit takes the scroll into its hand and stands there with it (SC3). Each click logs its outcome, a refusal with the reason
    // (diagnosis on DS_B.0.4.29).
    internal static bool AllowClick(UnitEntity user, ActiveSkill_Base skill, Vector2Int cell)
    {
        if (skill == null || skill.Key != SkillKey) return true;
        var target = BodyAt(CoreGame.InMap, cell);
        if (target == Guid.Empty)
        {
            DelversHost.Info($"CORE_REVIVE_SCROLL_TILE_REFUSED cell={cell} body=no");
            return false;
        }
        var blocker = ReviveService.Blocker(target);
        if (blocker != null)
        {
            DelversHost.Info($"CORE_REVIVE_SCROLL_TILE_REFUSED cell={cell} body=yes reason={blocker}"
                + ReviveService.DescribeBlocker(blocker, target, user));
            return false;
        }
        var caster = CellOf(CoreGame.InMap, user);
        DelversHost.Info($"CORE_REVIVE_SCROLL_TILE_ACCEPTED unit={target:D} cell={cell} caster={Describe(caster)} distance={Distance(caster, cell)}");
        return true;
    }

    // Checked every tick of an item cast and right before the scroll is used up. False refuses.
    // A line is logged only when target or outcome change, never every tick.
    internal static bool AllowCast(IInMapDataSupplier inMap, SkillExecutionContext context)
    {
        var item = context?.UseItem;
        if (item == null || item.Key != ItemKey) return true;
        var target = CastTarget(inMap, context, out var cell, out var source);
        if (target == Guid.Empty)
        {
            LogCastCheck(inMap, context, source, cell, target, "no-body");
            return false;
        }
        if (target == gateTarget && cell == gateCell && Time.unscaledTime < gateUntil) return gateResult;
        gateTarget = target;
        gateCell = cell;
        gateUntil = Time.unscaledTime + GateCacheSeconds;
        var blocker = ReviveService.Blocker(target);
        gateResult = blocker == null;
        LogCastCheck(inMap, context, source, cell, target, blocker);
        return gateResult;
    }

    // DS_B.0.4.29 can aim an item skill at the body as an entity (ExecutionContext.TargetEntity)
    // instead of a cell; before, the cast carried TargetGrid (Delvers' own experience). The cell comes from TargetGrid,
    // else from the entity. With neither, no body is found and the cast is refused as before.
    private static Guid CastTarget(IInMapDataSupplier inMap, SkillExecutionContext context, out Vector2Int cell, out string source)
    {
        cell = default;
        source = "none";
        if (context.TargetGrid != null)
        {
            cell = context.TargetGrid.Position;
            source = "grid";
            var onGrid = BodyAt(inMap, cell);
            if (onGrid != Guid.Empty) return onGrid;
        }
        var entityCell = CellOf(inMap, context.TargetEntity?.TryCast<UnitEntity>());
        if (!entityCell.HasValue) return Guid.Empty;
        cell = entityCell.Value;
        source = "entity";
        return BodyAt(inMap, cell);
    }

    private static void LogCastCheck(IInMapDataSupplier inMap, SkillExecutionContext context, string source,
        Vector2Int cell, Guid target, string reason)
    {
        var key = source + "|" + cell + "|" + target + "|" + reason;
        if (key == lastCastCheck) return;
        lastCastCheck = key;
        try
        {
            var grid = context.TargetGrid != null ? context.TargetGrid.Position.ToString() : "null";
            var entity = context.TargetEntity?.TryCast<UnitEntity>();
            var casterUnit = context.User?.TryCast<UnitEntity>();
            var detail = reason != null && target != Guid.Empty ? ReviveService.DescribeBlocker(reason, target, casterUnit) : string.Empty;
            DelversHost.Info($"CORE_REVIVE_SCROLL_CAST_CHECK grid={grid} entity={(entity != null ? CoreGame.GuidOf(entity).ToString("D") : "null")}"
                + $" from={source} cell={cell} unit={target:D} caster={Describe(CellOf(inMap, casterUnit))} result={reason ?? "ok"}{detail}");
        }
        catch (Exception ex) { DelversHost.Warning("CORE_REVIVE_SCROLL_CAST_CHECK_LOG_FAILED " + ex.GetType().Name); }
    }

    private static Vector2Int? CellOf(IInMapDataSupplier inMap, UnitEntity unit)
    {
        if (inMap == null || unit == null) return null;
        try { return inMap.GetLogicalPosition(unit.Guid); }
        catch (Exception) { return null; }
    }

    private static string Describe(Vector2Int? cell) => cell.HasValue ? cell.Value.ToString() : "unknown";

    private static string Distance(Vector2Int? from, Vector2Int to)
        => from.HasValue ? Math.Max(Math.Abs(from.Value.x - to.x), Math.Abs(from.Value.y - to.y)).ToString() : "unknown";

    // The return scroll's routine, at its start, for our key: the scroll is already used up, Core
    // revives instead of teleporting. True when the routine was taken over.
    internal static bool TakeOver(ReturnScrollRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        var context = routine.context;
        var item = context?.UseItem;
        if (item == null || item.Key != ItemKey) return false;
        routine.__1__state = -1;
        var target = CastTarget(routine.inMapData, context, out var cell, out var source);
        var caster = CellOf(routine.inMapData, context.User?.TryCast<UnitEntity>());
        DelversHost.Info($"CORE_REVIVE_SCROLL_USED unit={target:D} cell={cell} from={source} caster={Describe(caster)}"
            + ReviveService.DescribeReviveCell(target));
        // Outside the game's tick: the revive starts in the next frame.
        DelversHost.StartCoroutine(StartNextFrame(target));
        return true;
    }

    private static IEnumerator StartNextFrame(Guid target)
    {
        yield return null;
        string result;
        try { result = target == Guid.Empty ? ReviveService.NotFound : ReviveService.Start(target); }
        catch (Exception ex) { result = ReviveService.Error + " " + ex.Message; }
        if (result != null) DelversHost.Error($"CORE_REVIVE_SCROLL_FAILED unit={target:D} reason={result}");
    }

    private static Guid BodyAt(IInMapDataSupplier inMap, Vector2Int cell)
    {
        var units = inMap?.GetAllPlayerUnits(true);
        for (var i = 0; units != null && i < units.Count; i++)
        {
            var unit = units[i];
            if (unit == null || !unit.IsDead) continue;
            Vector2Int at;
            try { at = inMap.GetLogicalPosition(unit.Guid); }
            catch (Exception) { continue; }
            if (at == cell) return CoreGame.GuidOf(unit);
        }
        return Guid.Empty;
    }

    // ---- icon ----

    // The return scroll's icon with a yellow-white glow on the transparent pixels around it.
    internal static Sprite GlowIcon(ResourceLoader loader)
    {
        if (glowIcon != null) return glowIcon;
        var source = loader.LoadItemIcon(NativeItemKey);
        if (source == null) return null;
        glowIcon = ScrollGlow.Create(source) ?? source;
        return glowIcon;
    }
}

[HarmonyPatch(typeof(ItemSheet), nameof(ItemSheet.Parse))]
internal static class ReviveScrollItemSheetPatch
{
    private static void Postfix(ItemSheet __instance)
    {
        try { ReviveScroll.RegisterItem(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_REVIVE_SCROLL_ITEM_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(SkillSheet), nameof(SkillSheet.Parse))]
internal static class ReviveScrollSkillSheetPatch
{
    private static void Postfix(SkillSheet __instance)
    {
        try { ReviveScroll.RegisterSkill(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_REVIVE_SCROLL_SKILL_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(TradePoolSheet), nameof(TradePoolSheet.ParseTable))]
internal static class ReviveScrollTradePoolPatch
{
    private static void Postfix(TradePoolSheet __instance)
    {
        try { ReviveScroll.AddToMerchants(__instance); }
        catch (Exception ex) { DelversHost.Error("CORE_REVIVE_SCROLL_MERCHANTS_FAILED: " + ex); }
    }
}

[HarmonyPatch(typeof(Skill_Base), nameof(Skill_Base.Create))]
internal static class ReviveScrollCreatePatch
{
    private static bool Prefix(string key, ref Skill_Base __result)
    {
        if (key != ReviveScroll.SkillKey) return true;
        try { __result = ReviveScroll.CreateSkill(); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_REVIVE_SCROLL_CREATE_FAILED: " + ex);
            __result = null;
        }
        return false;
    }
}

// Once per click in the game's skill target mode. One caller, not shared with other classes; its
// only struct parameter is Unity's Vector2Int, by value. Arguments are bound by name: `ItemEntity useItem` stands at index 2,
// before `position`, and older dumps have three parameters (from the game's own data; the parameter came
// between DS_B.0.4.23 and 0.4.29). The old index binding (__2) then handed an object to a Vector2Int (IL compile error under
// Melon, a wrong position under BepInEx; Delvers' own experience).
[HarmonyPatch(typeof(UseSkillMode), "GetPossibleTarget")]
internal static class ReviveScrollClickGatePatch
{
    private static void Postfix(UnitEntity user, ActiveSkill_Base skill, Vector2Int position, ref ICombatTargetSelector __result)
    {
        if (__result == null) return;
        try
        {
            if (!ReviveScroll.AllowClick(user, skill, position)) __result = null;
        }
        catch (Exception) { /* The native target stands; the cast check still guards the scroll. */ }
    }
}

// Every tick of an item cast; only our key does more than a string compare.
[HarmonyPatch(typeof(ItemUseCondition), "CheckCondition")]
internal static class ReviveScrollCastGatePatch
{
    private static void Postfix(IInMapDataSupplier inMapData, SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if ((int)__result < 0) return;
        try
        {
            if (!ReviveScroll.AllowCast(inMapData, context)) __result = ExecutionCheckResult.Failed_UnvalidTarget;
        }
        catch (Exception) { /* The native result stands. */ }
    }
}

// Runs only when a return scroll or a revive scroll actually fires.
[HarmonyPatch(typeof(ReturnScrollRoutine), "MoveNext")]
internal static class ReviveScrollRoutinePatch
{
    private static bool Prefix(ReturnScrollRoutine __instance, ref bool __result)
    {
        try
        {
            if (!ReviveScroll.TakeOver(__instance)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_REVIVE_SCROLL_ROUTINE_FAILED: " + ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.LoadItemIcon))]
internal static class ReviveScrollIconPatch
{
    private static bool Prefix(ResourceLoader __instance, string key, ref Sprite __result)
    {
        if (key != ReviveScroll.ItemKey) return true;
        try
        {
            __result = ReviveScroll.GlowIcon(__instance);
            return __result == null;
        }
        catch (Exception) { return true; }
    }
}

// The scroll takes the return scroll's row with its durability 600; the tooltip line "600/600"
// means nothing for a scroll that is used up at once (leave it out for now).
// The durability itself stays: every usable item of the game carries one, so 0 is untested ground.
[HarmonyPatch(typeof(Tooltip_Item), nameof(Tooltip_Item.AddDurabilityStatRow))]
internal static class ReviveScrollDurabilityRowPatch
{
    private static bool Prefix(ItemTableData data)
        => data == null || (data.Key != ReviveScroll.ItemKey && !Bandages.IsOurs(data.Key));
}
