namespace DungeonSettlersDelvers.Core;

// A save loaded while a character pack is inactive (for example after a game
// update, before the mod update) loses that unit's whole affecter list on the
// next save; only its profile survives. Traits the pack registered as fixed can
// therefore be restored from the profile alone once the pack is active again.
internal static class FixedTraitRules
{
    // Returns the keys with each missing fixed trait inserted right after the
    // fixed trait that precedes it in the registered order, or first when none
    // precedes it. Existing keys keep their relative order. Null when nothing
    // is missing, so an intact save is never rebuilt.
    internal static List<string> WithMissing(IReadOnlyList<string> keys, IReadOnlyList<string> orderedFixedTraits)
    {
        var present = new HashSet<string>(keys.Where(key => key != null), StringComparer.Ordinal);
        if (orderedFixedTraits.All(present.Contains)) return null;

        var result = new List<string>(keys.Count + orderedFixedTraits.Count);
        void AppendMissingFrom(int start)
        {
            for (var i = start; i < orderedFixedTraits.Count && !present.Contains(orderedFixedTraits[i]); i++)
                result.Add(orderedFixedTraits[i]);
        }

        AppendMissingFrom(0);
        var anchored = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            result.Add(key);
            if (key == null || !anchored.Add(key)) continue;
            for (var i = 0; i < orderedFixedTraits.Count; i++)
                if (orderedFixedTraits[i] == key) { AppendMissingFrom(i + 1); break; }
        }
        return result;
    }
}
