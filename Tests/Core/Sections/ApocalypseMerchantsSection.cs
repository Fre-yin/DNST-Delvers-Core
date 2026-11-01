using DungeonSettlersDelvers.Core;
using static TestKit;

// Merchants in Apocalypse management.
internal static class ApocalypseMerchantsSection
{
    internal static void Run()
    {
        // Merchants in Apocalypse management: about 40 % fewer goods, 60 % rounded
        // down, never below one; taken from the last new slot first, and a slot may empty, so every item
        // ends at exactly Reduced(total).
        Check(ApocalypseMerchantRules.Reduced(45) == 27 && ApocalypseMerchantRules.Reduced(120) == 72
                && ApocalypseMerchantRules.Reduced(10) == 6 && ApocalypseMerchantRules.Reduced(4) == 2
                && ApocalypseMerchantRules.Reduced(3) == 1 && ApocalypseMerchantRules.Reduced(2) == 1
                && ApocalypseMerchantRules.Reduced(1) == 1 && ApocalypseMerchantRules.Reduced(0) == 0,
            "merchant stock keeps 60 % rounded down and never drops below one");
        Check(string.Join(",", ApocalypseMerchantRules.Removals(new[] { 30 })) == "12"
                && string.Join(",", ApocalypseMerchantRules.Removals(new[] { 20, 11 })) == "2,11"
                && string.Join(",", ApocalypseMerchantRules.Removals(new[] { 2 })) == "1"
                && string.Join(",", ApocalypseMerchantRules.Removals(new[] { 1 })) == "0",
            "stacked goods end at exactly the reduced total, the last slot empties first (LumaLog 20+11 to 18, scroll 2 to 1)");
        Check(string.Join(",", ApocalypseMerchantRules.Removals(new[] { 1, 1, 1 })) == "0,1,1"
                && string.Join(",", ApocalypseMerchantRules.Removals(new[] { 1, 1, 1, 1 })) == "0,0,1,1",
            "goods that do not stack lose whole slots and keep at least one (torch 3 to 1, torch 4 to 2, a weapon stays)");
    }
}
