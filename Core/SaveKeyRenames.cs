namespace DungeonSettlersDelvers.Core;

// IDs a pack has renamed although players' saves still contain the old ones. Core rewrites
// them in the raw save text before the game parses it (see SaveKeyRenamePatch), so every
// field and every unit is covered in one place; only complete JSON strings are replaced.
// Each registration is its own lease. Unlike FixedTraitRegistry there is no reference count:
// a second registration of an old key is rejected, even from the same pack, and disposing a
// lease removes exactly its own entries.
// Messages never name a key: old IDs may carry names that must not end up in shared logs.
internal sealed class SaveKeyRenameRegistry : ICoreRegistry
{
    private readonly LeaseRegistry<string, Rename> byOldKey = new(DuplicateRegistration.Reject, keys: StringComparer.Ordinal);

    internal int Count => byOldKey.Count;

    internal IDisposable Register(string packId, IReadOnlyDictionary<string, string> renames)
    {
        PackIds.Require(packId, nameof(packId));
        if (renames == null) throw new ArgumentNullException(nameof(renames));
        if (renames.Count == 0) throw new ArgumentException("At least one rename is required.", nameof(renames));
        var entries = renames.Select(pair => new Rename(packId, pair.Key, pair.Value)).ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            if (!IsValidKey(entries[i].OldKey) || !IsValidKey(entries[i].NewKey))
                throw new ArgumentException($"Rename {i + 1}: save keys may use letters, digits, and underscores.", nameof(renames));
            if (string.Equals(entries[i].OldKey, entries[i].NewKey, StringComparison.Ordinal))
                throw new ArgumentException($"Rename {i + 1}: the new key equals the old key.", nameof(renames));
        }

        return byOldKey.RegisterAll(
            entries.Select(entry => new KeyValuePair<string, Rename>(entry.OldKey, entry)).ToArray(),
            existingEntries =>
            {
                // Built by hand: ToDictionary with a lambda fails (CS0656) against the IL2CPP references.
                var existing = new Dictionary<string, Rename>(StringComparer.Ordinal);
                foreach (var entry in existingEntries) existing[entry.OldKey] = entry;
                var newKeys = new HashSet<string>(existing.Values.Select(entry => entry.NewKey), StringComparer.Ordinal);
                newKeys.UnionWith(entries.Select(entry => entry.NewKey));
                for (var i = 0; i < entries.Length; i++)
                {
                    if (existing.TryGetValue(entries[i].OldKey, out var taken))
                        throw new InvalidOperationException($"Rename {i + 1}: the old key is already renamed by pack {taken.PackId}.");
                    // A chain (A to B, B to C) would depend on replacement order.
                    if (newKeys.Contains(entries[i].OldKey))
                        throw new InvalidOperationException($"Rename {i + 1}: the old key is also a rename target.");
                }
                if (entries.Any(entry => existing.ContainsKey(entry.NewKey)))
                    throw new InvalidOperationException("A new key is already renamed by another registration.");
            });
    }

    // Replaces every complete JSON string "old" with "new". A key that is only part of a
    // longer string never matches. Returns the input instance when nothing was renamed.
    internal string Apply(string json, out int tokens, out IReadOnlyCollection<string> packs)
    {
        tokens = 0;
        packs = Array.Empty<string>();
        if (string.IsNullOrEmpty(json)) return json;
        var current = byOldKey.Snapshot();
        if (current.Length == 0) return json;

        var result = json;
        var touched = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in current)
        {
            var quotedOld = "\"" + entry.OldKey + "\"";
            var count = Occurrences(result, quotedOld);
            if (count == 0) continue;
            result = result.Replace(quotedOld, "\"" + entry.NewKey + "\"", StringComparison.Ordinal);
            tokens += count;
            touched.Add(entry.PackId);
        }
        if (tokens > 0) packs = touched;
        return tokens > 0 ? result : json;
    }

    public void Clear() => byOldKey.Clear();

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var character in key)
            if (!(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) return false;
        return true;
    }

    private sealed class Rename
    {
        internal readonly string PackId;
        internal readonly string OldKey;
        internal readonly string NewKey;

        internal Rename(string packId, string oldKey, string newKey)
        { PackId = packId; OldKey = oldKey; NewKey = newKey; }
    }
}
