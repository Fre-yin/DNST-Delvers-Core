namespace DungeonSettlersDelvers.Core;

// The order of the summons in DelversSummons.SummonsOf: each summon gets a running number the first time Core sees
// it, and the list is sorted by it, so the summons of one owner stand in the order they appeared, the same for the formation
// window and the bandits' AI. The game's own lists keep no such order. No game types here.
internal sealed class SummonOwnerRules
{
    // Above this many remembered summons, those not seen in the latest scan are let go.
    internal const int Cap = 512;

    private readonly Dictionary<string, long> seen = new(StringComparer.Ordinal);
    private long next;

    internal int Count => seen.Count;

    // The running number of a summon, given the first time it is noted.
    internal long Note(string id)
    {
        if (string.IsNullOrEmpty(id)) return long.MaxValue;
        if (!seen.TryGetValue(id, out var number)) seen[id] = number = next++;
        return number;
    }

    // The ids sorted by their running number, oldest first; an id never noted is noted now, so it goes last.
    internal List<string> Sort(IEnumerable<string> ids)
    {
        var result = new List<string>();
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
        foreach (var id in result) Note(id);
        result.Sort((a, b) => seen[a].CompareTo(seen[b]));
        return result;
    }

    // After a scan of the map: past the cap, only the summons seen in it are kept.
    internal void Keep(ICollection<string> alive)
    {
        if (seen.Count <= Cap || alive == null) return;
        foreach (var id in new List<string>(seen.Keys))
            if (!alive.Contains(id)) seen.Remove(id);
    }

    internal void Clear()
    {
        seen.Clear();
        next = 0;
    }

    // The owner of a summon as DelversSummons reads it: "" for none (SummonerRules.GuidText already drops Guid.Empty).
    internal static bool IsOwnedBy(string summoner, string owner)
        => !string.IsNullOrEmpty(summoner) && !string.IsNullOrEmpty(owner) && string.Equals(summoner, owner, StringComparison.OrdinalIgnoreCase);
}
