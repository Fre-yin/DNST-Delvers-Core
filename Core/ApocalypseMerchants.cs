using HarmonyLib;
#if BEPINEX
using ItemControlProcessor = global::Refactor.Main.Processor.ItemControlProcessor;
#else
using ItemControlProcessor = Il2CppRefactor.Main.Processor.ItemControlProcessor;
#endif

namespace DungeonSettlersDelvers.Core;

// Merchants in Apocalypse management offer about 40 % fewer goods. Quantities only, prices stay. Rule:
// ApocalypseMerchantRules.
// Where: the game fills a merchant's stock in OnEvent(TradePoolInitializeRequested)
// and OnEvent(TradePoolSetRequested). Each clears the stock, then adds item by item with TryAddToInventory and right after
// calls ApplyDiscountForNewlyAddedIndices with the slots filled before that item. That call is the one place both share, so
// the reduction runs there, on the item's new slots only. A third way, ApplyTradePool → PopulateTradeInventoryFromPool, has
// no caller in this build (same line), so Core does not patch it (the patch never ran). It takes the
// excess through the game's own PickUpFromInventory, which keeps weight and records right. That
// method needs the map, which the shared call lacks; the ways hand it over (context hooks).
// The stock is saved as it is, so a load never reduces twice; the next refresh reduces again.
internal static class ApocalypseMerchants
{
    private static bool inStock;
    private static MapType stockMap;
    private static string stockPool;
    private static int stockItems, stockBefore, stockAfter;

    internal static void Enter(MapType map, string pool)
    {
        inStock = true;
        stockMap = map;
        stockPool = pool;
        stockItems = stockBefore = stockAfter = 0;
    }

    internal static void Leave()
    {
        if (inStock && stockItems > 0)
            DelversHost.Info("CORE_APOCALYPSE_MERCHANT_STOCK pool=" + (stockPool ?? "unknown") + " map=" + stockMap
                + " items=" + stockItems + " before=" + stockBefore + " after=" + stockAfter);
        inStock = false;
        stockPool = null;
    }

    internal static void Reduce(ItemEventHandler handler, InventoryReader inventoryReader,
        TradeDataComponent tradeDataComponent, Il2CppSystem.Collections.Generic.List<int> startNotEmpty)
    {
        if (!ApocalypseDifficulty.ActiveManagement || handler == null || inventoryReader == null || tradeDataComponent == null) return;
        if (!inStock)
        {
            ApocalypseHooks.Report("merchant-no-map", new InvalidOperationException("merchant stock outside the three known ways"));
            return;
        }
        var trader = tradeDataComponent._owner;
        var processor = handler._itemControlProcessor;
        if (trader == null || processor == null) return;
        if (!inventoryReader.TryGetFilledIndices(out var filled) || filled == null) return;

        var before = new HashSet<int>();
        if (startNotEmpty != null)
            for (var i = 0; i < startNotEmpty.Count; i++) before.Add(startNotEmpty[i]);
        var slots = new List<int>();
        var amounts = new List<int>();
        string key = null;
        for (var i = 0; i < filled.Count; i++)
        {
            var index = filled[i];
            if (before.Contains(index)) continue;
            var holder = inventoryReader.GetItemInfo(index);
            if (holder == null) continue;
            slots.Add(index);
            amounts.Add(holder.Amount);
            key ??= holder.Key;
        }
        if (slots.Count == 0) return;   // nothing new (inventory full, or added onto an old stack)

        // A removal of the whole amount empties the slot the game's own way (PickUpFromItemContainer:
        // DisposeItem, RemoveItem, weight change).
        var removals = ApocalypseMerchantRules.Removals(amounts);
        var emptied = 0;
        for (var i = 0; i < slots.Count; i++)
        {
            if (removals[i] <= 0) continue;
            if (removals[i] >= amounts[i]) emptied++;
            processor.PickUpFromInventory(stockMap, trader, slots[i], new Il2CppSystem.Nullable<int>(removals[i]));
        }

        var total = amounts.Sum();
        var after = 0;
        foreach (var index in slots)
        {
            var holder = inventoryReader.GetItemInfo(index);
            if (holder != null) after += holder.Amount;
        }
        stockItems++;
        stockBefore += total;
        stockAfter += after;
        DelversHost.Info("CORE_APOCALYPSE_MERCHANT pool=" + (stockPool ?? "unknown") + " map=" + stockMap
            + " item=" + (key ?? "unknown") + " before=" + total + " after=" + after
            + " expected=" + ApocalypseMerchantRules.Reduced(total) + " slots=" + slots.Count + " emptied=" + emptied);
    }
}

// The two ways the game fills a merchant's stock hand over the map (context). Names, not
// positions, bind the arguments.
[HarmonyPatch(typeof(ItemEventHandler), nameof(ItemEventHandler.OnEvent), new[] { typeof(TradePoolInitializeRequested) })]
internal static class ApocalypseMerchantInitializePatch
{
    private static void Prefix(TradePoolInitializeRequested msg)
        => ApocalypseHooks.Run("merchant-enter", () => { if (msg != null) ApocalypseMerchants.Enter(msg.Map, msg.TradePool); });

    private static void Postfix() => ApocalypseHooks.Run("merchant-leave", ApocalypseMerchants.Leave);
}

[HarmonyPatch(typeof(ItemEventHandler), nameof(ItemEventHandler.OnEvent), new[] { typeof(TradePoolSetRequested) })]
internal static class ApocalypseMerchantSetPatch
{
    private static void Prefix(TradePoolSetRequested msg)
        => ApocalypseHooks.Run("merchant-enter", () => { if (msg != null) ApocalypseMerchants.Enter(msg.Map, msg.TradePool); });

    private static void Postfix() => ApocalypseHooks.Run("merchant-leave", ApocalypseMerchants.Leave);
}

// The one call both ways share, once per item right after it was added.
[HarmonyPatch(typeof(ItemEventHandler), "ApplyDiscountForNewlyAddedIndices")]
internal static class ApocalypseMerchantStockPatch
{
    private static void Postfix(ItemEventHandler __instance, InventoryReader inventoryReader,
        TradeDataComponent tradeDataComponent, Il2CppSystem.Collections.Generic.List<int> startNotEmpty)
        => ApocalypseHooks.Run("merchant-stock",
            () => ApocalypseMerchants.Reduce(__instance, inventoryReader, tradeDataComponent, startNotEmpty));
}
