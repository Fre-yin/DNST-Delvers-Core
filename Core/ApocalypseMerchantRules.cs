namespace DungeonSettlersDelvers.Core;

// Merchant stock in Apocalypse management: merchants offer about 40 % fewer
// goods, quantities only, never prices. Pure, so the offline tests cover it; ApocalypseMerchants
// applies it where the game fills a merchant's stock.
internal static class ApocalypseMerchantRules
{
    // 0.6 as 3/5, so the rounding down stays exact in integers.
    internal const int KeepNumerator = 3;
    internal const int KeepDenominator = 5;

    // The amount an item keeps: 60 % rounded down, never below one. Nothing grows, nothing
    // already at one or below changes.
    internal static int Reduced(int amount)
        => amount <= 1 ? amount : Math.Max(1, amount * KeepNumerator / KeepDenominator);

    // What to take from each new slot of one item so that its total becomes exactly
    // Reduced(total), taken from the last slot first. A slot may empty: items that do not stack lie one per slot, and a floor of one per slot
    // left them untouched. Reduced never drops below one, so the item keeps at least one slot.
    internal static int[] Removals(IReadOnlyList<int> slotAmounts)
    {
        var removals = new int[slotAmounts.Count];
        var total = 0;
        foreach (var amount in slotAmounts) total += Math.Max(0, amount);
        var excess = total - Reduced(total);
        for (var i = slotAmounts.Count - 1; i >= 0 && excess > 0; i--)
        {
            var take = Math.Min(excess, Math.Max(0, slotAmounts[i]));
            removals[i] = take;
            excess -= take;
        }
        return removals;
    }
}
