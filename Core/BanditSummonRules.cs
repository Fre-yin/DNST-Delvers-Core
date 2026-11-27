namespace DungeonSettlersDelvers.Core;

// The bandit water mage's summons out of a fight (the water mage's plan), pure so the offline tests cover it. In a fight
// they stay the game's and attack the mage's target (SummonFollowTarget). Out of it the game's idle is locked (SummonControl) and each
// stands on a cell beside its mage, and keeps that cell while the mage stays within StayCells.
internal static class BanditSummonRules
{
    internal const float LookSeconds = 1f;
    internal const int StayCells = 2;

    internal static bool IsMage(string key) => (key ?? "").EndsWith("WaterMage", StringComparison.Ordinal);

    internal static bool IsSummon(string key) => key == WeakBanditKitKeys.Bubbling || key == WeakBanditKitKeys.BubbleToad;

    // Either in a fight, the summon is the game's: a foe only it noticed must not wait for the mage.
    internal static bool Holds(bool mageInCombat, bool summonInCombat) => !mageInCombat && !summonInCombat;

    internal static bool KeepsCell((int X, int Y) cell, (int X, int Y) mage)
        => Math.Max(Math.Abs(cell.X - mage.X), Math.Abs(cell.Y - mage.Y)) <= StayCells;
}
