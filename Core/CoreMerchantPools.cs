using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// Core's own items in the game's merchant pools (TradePoolTableData): one more entry in each of the
// six parallel arrays. Used by the bandages; the revive scroll still carries its own copy of this
// (ReviveScroll.AddToMerchants), to be moved here when it is touched next.
internal static class CoreMerchantPools
{
    // The guild merchant's pool key is this prefix plus the clan rank, or "…Safe" on safe floors.
    internal const string GuildPoolPrefix = "TRADEPOOL_GuildMerchant";

    internal readonly struct Offer
    {
        internal Offer(string pool, float chance, int min, int max, float discountFrom, float discountTo)
        {
            Pool = pool;
            Chance = chance;
            Min = min;
            Max = max;
            DiscountFrom = discountFrom;
            DiscountTo = discountTo;
        }

        internal string Pool { get; }
        internal float Chance { get; }
        internal int Min { get; }
        internal int Max { get; }
        internal float DiscountFrom { get; }
        internal float DiscountTo { get; }
    }

    // Every guild pool of the loaded table, sorted.
    internal static List<string> GuildPools(Il2CppSystem.Collections.Generic.Dictionary<string, TradePoolTableData> table)
    {
        var guild = new List<string>();
        foreach (var key in table.Keys)
            if (key != null && key.StartsWith(GuildPoolPrefix, StringComparison.Ordinal)) guild.Add(key);
        guild.Sort(StringComparer.Ordinal);
        return guild;
    }

    // The item's slot in the pool: the existing one, or a new last one. -1 when the pool is missing
    // or its arrays differ in length (the restock loop runs over the chances).
    internal static int Insert(Il2CppSystem.Collections.Generic.Dictionary<string, TradePoolTableData> table,
        string itemKey, Offer offer, float chance)
    {
        if (!table.TryGetValue(offer.Pool, out var pool) || pool?.AvailableItems == null) return -1;
        var items = pool.AvailableItems;
        var count = items.Length;
        for (var i = 0; i < count; i++) if (items[i] == itemKey) return i;
        if (pool.AppearChancePers.Length != count || pool.MinAmounts.Length != count || pool.MaxAmounts.Length != count
            || pool.MinDiscountPers.Length != count || pool.MaxDiscountPers.Length != count)
        {
            DelversHost.Warning("CORE_MERCHANT_POOL_SKIPPED pool=" + offer.Pool + " item=" + itemKey + " reason=uneven-arrays");
            return -1;
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
        newItems[count] = itemKey;
        chances[count] = chance;
        mins[count] = offer.Min;
        maxs[count] = offer.Max;
        discountFrom[count] = offer.DiscountFrom;
        discountTo[count] = offer.DiscountTo;
        pool.AvailableItems = newItems;
        pool.AppearChancePers = chances;
        pool.MinAmounts = mins;
        pool.MaxAmounts = maxs;
        pool.MinDiscountPers = discountFrom;
        pool.MaxDiscountPers = discountTo;
        return count;
    }

    // The chance of an item already in the pool; the pools are read at every restock.
    internal static void SetChance(Il2CppSystem.Collections.Generic.Dictionary<string, TradePoolTableData> table,
        string pool, int slot, float chance)
    {
        if (slot >= 0 && table.TryGetValue(pool, out var data) && data != null && slot < data.AppearChancePers.Length)
            data.AppearChancePers[slot] = chance;
    }
}
