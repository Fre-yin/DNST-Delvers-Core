namespace DungeonSettlersDelvers.Core;

// When the party's fight starts for all of it (GroupAiFightStart). A foe in sight is one the player can see within SightCells of a
// member. A member that entered combat counts only when a foe has just hit it: aggro alone, a bow's long look, starts nothing for the
// others, so the group does not pull into a room of its own accord.
internal static class GroupAiFightStartRules
{
    internal const int SightCells = 8;
    internal const float HitSeconds = 3f;

    internal static bool InSight(int distance, bool visible) => visible && distance <= SightCells;

    internal static bool Hit(bool inCombat, float now, float lastHit) => inCombat && now - lastHit <= HitSeconds;

    // The log line of a start: why, the foe in sight if there is one, how many members came into the fight.
    internal static string Line(string cause, string foe, int members)
        => "CORE_AI_FIGHT_START cause=" + cause + " foe=" + (foe ?? "-") + " members=" + members;
}
