namespace DungeonSettlersDelvers.Core;

// The game's sort of target candidates by downed state (EntityTargetSelector, step ToLowestByDeadly) reads both candidates'
// affecter reader and throws when one is gone: a candidate that died, vanished or was torn down between collecting and sorting.
// A game error since at least, before any boss item (trap list, caught game errors). Core only keeps it from throwing: a
// candidate gone sorts after every whole one, two gone ones are equal, and the game's sort goes on. Should the game fix it, the guard
// can go.
// Not "equal" for a gone one (review): the game's queue breaks a tie by insertion number, so a gone candidate equal to
// both a downed and a standing one made a cycle and the heap's order depended on its shape. After all whole ones it is a total order.
internal static class TargetSortRules
{
    // Skip the game's comparison only where it would throw; every other pair is the game's.
    internal static bool SkipsGame(bool firstHasReader, bool secondHasReader) => !firstHasReader || !secondHasReader;

    // The order of a pair the game skips: the gone one is the greater, so it comes after; two gone ones are equal.
    internal static int GoneOrder(bool firstHasReader, bool secondHasReader)
        => firstHasReader == secondHasReader ? 0 : firstHasReader ? -1 : 1;

    internal static string Line(string side, string guid, string key, long tick, int count)
        => "CORE_TARGET_SORT_NULL side=" + side + " guid=" + guid + " key=" + key + " tick=" + tick + " count=" + count;
}
