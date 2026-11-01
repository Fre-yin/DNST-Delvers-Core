namespace DungeonSettlersDelvers.Core;

// The group AI is rather an accessibility feature and locks itself from Very Hard up. The levels are
// ReviveRules.Level's: the game's CombatDifficulty (Easy 0, Normal 1, Hard 2, VeryHard 3, Downfall 4) plus Core's
// Apocalypse 5. The game sets the difficulty only when the campaign is created (CampaignStartSetting.SetCampaign).
internal static class GroupAiLockRules
{
    internal const int FirstLockedLevel = 3;

    internal static bool Locked(int combatLevel) => combatLevel >= FirstLockedLevel;

    internal static string Line(bool locked, int combatLevel)
        => "CORE_AI_GROUP_LOCK locked=" + (locked ? 1 : 0) + " difficulty=" + Name(combatLevel);

    internal static string Name(int combatLevel) => combatLevel switch
    {
        0 => "Easy",
        1 => "Normal",
        2 => "Hard",
        3 => "VeryHard",
        4 => "Downfall",
        5 => "Apocalypse",
        _ => combatLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };
}
