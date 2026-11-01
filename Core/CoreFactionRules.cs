namespace DungeonSettlersDelvers.Core;

// Core's side "bandits", pure: a unit with a bandit key, or a
// summon (unit or building) whose owner is one. The game's faction is never written for it (the lesson of fix A); the tag is only
// Core's view, which every Core question of side asks (CoreFaction).
internal static class CoreFactionRules
{
    internal static bool IsBandit(bool banditKey, bool ownerIsBandit) => banditKey || ownerIsBandit;

    // A bandit's solar bloom (BanditSolarBloom): what its attack routine's first step left, from the step it came in at, the
    // routine's cooldown then, MoveNext's answer and the step after. "found": the game picked a target (one of the bandits, as
    // its IsHostile counts only Enemy units for the Ally building), Core puts a party unit in its place; "none": the game found
    // no target, Core may still find a party unit; "keep": any other step (the cooldown running down, the bloom's end, the
    // shot itself).
    internal static string BloomStep(int stateBefore, int cooldownBefore, bool moved, int stateAfter)
        => stateBefore != 0 ? "keep"
            : moved && stateAfter == 2 ? "found"
            : !moved && cooldownBefore <= 0 && stateAfter == -1 ? "none"
            : "keep";

    internal static string BloomLine(string bloom, string step, string target, long tick)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "CORE_BANDIT_BLOOM_TARGET bloom={0} step={1} target={2} tick={3}",
            bloom ?? "-", step, target ?? "-", tick);

    // The side a log line names: bandit, else player.
    internal static string SideText(bool bandit) => bandit ? "bandit" : "player";
}
