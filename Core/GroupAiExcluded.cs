namespace DungeonSettlersDelvers.Core;

// Units the group AI leaves out entirely (the Toolkit's dummy, a stunned and
// immortal fifth party member): no hostile, no ally, no capable unit, and Core decides nothing for them.
// Set by ExcludeFromGroupAiForTest; holds for the session.
internal static class GroupAiExcluded
{
    private static readonly HashSet<string> guids = new(StringComparer.OrdinalIgnoreCase);

    internal static int Count => guids.Count;

    internal static bool Set(string guid, bool on)
    {
        if (string.IsNullOrEmpty(guid)) return false;
        if (on) guids.Add(guid);
        else guids.Remove(guid);
        return true;
    }

    internal static bool Has(string guid) => guids.Count > 0 && !string.IsNullOrEmpty(guid) && guids.Contains(guid);

    internal static void Clear() => guids.Clear();
}
