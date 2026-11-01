using DungeonSettlersDelvers.Core;
using static TestKit;

// Bandits: how a skill on a cell is aimed, and when it falls back to a basic attack (CoreUnitRules.AimFallback).
internal static class BanditAimSection
{
    internal static void Run()
    {
        TargetKinds();
        FallbackOrder();
    }

    static void TargetKinds()
    {
        // The game's TargetType.GridTarget (2) and SelfTarget (3).
        Check(CoreUnitRules.AimsAtCell(2) && !CoreUnitRules.AimsAtCell(3) && !CoreUnitRules.AimsAtCell(0) && !CoreUnitRules.AimsAtCell(1),
            "bandit aim: only a grid target skill aims at a cell");
        Check(CoreUnitRules.AimsAtSelf(3) && !CoreUnitRules.AimsAtSelf(2) && !CoreUnitRules.AimsAtSelf(0),
            "bandit aim: only a self target skill aims at the unit itself");
    }

    static void FallbackOrder()
    {
        // No fallback when the cell is known, the target is not provoked and the cell is in range.
        Check(CoreUnitRules.AimFallback(true, false, true) == null, "aim fallback: a known cell in range, not provoked: no fallback");
        // The order: a missing cell, then a provoked target, then the range.
        Check(CoreUnitRules.AimFallback(false, false, true) == "basic" && CoreUnitRules.AimFallback(false, false, false) == "basic",
            "aim fallback: without a cell the answer is basic");
        Check(CoreUnitRules.AimFallback(false, true, true) == "basic" && CoreUnitRules.AimFallback(false, true, false) == "basic" && CoreUnitRules.AimFallback(false, true, true) != "forced",
            "aim fallback: without a cell and under provocation the answer is basic, not forced");
        Check(CoreUnitRules.AimFallback(true, true, true) == "forced" && CoreUnitRules.AimFallback(true, true, false) == "forced",
            "aim fallback: with a cell under provocation the answer is forced, in range or not");
        Check(CoreUnitRules.AimFallback(true, false, false) == "range", "aim fallback: a known cell beyond the range, not provoked: range");
        // The three words are what the CORE_AI_BANDIT_AIM line carries after fallback=.
        Check(CoreUnitRules.FallbackNoCell == "basic" && CoreUnitRules.FallbackForced == "forced" && CoreUnitRules.FallbackRange == "range",
            "aim fallback: the three reasons are basic, forced and range");
    }
}
