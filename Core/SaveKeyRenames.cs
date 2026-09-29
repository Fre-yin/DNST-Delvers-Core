namespace DungeonSettlersDelvers.Core;

// IDs a pack has renamed although players' saves still contain the old ones. Core rewrites
// them in the raw save text before the game parses it (see SaveKeyRenamePatch), so every
// field and every unit is covered in one place. Leased per pack like FixedTraitRegistry.
// Messages never name a key: old IDs may carry names that must not end up in shared logs.
internal sealed class SaveKeyRenameRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, Rename> byOldKey = new(StringComparer.Ordinal);

    internal int Count
    {
        get { lock (gate) return byOldKey.Count; }
    }

    internal IDisposable Register(string packId, IReadOnlyDictionary<string, string> renames)
    {
        if (!IsValidPackId(packId))
            throw new ArgumentException("Pack IDs may use lowercase letters, digits, and hyphens.", nameof(packId));
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

        lock (gate)
        {
            var newKeys = new HashSet<string>(byOldKey.Values.Select(entry => entry.NewKey), StringComparer.Ordinal);
            newKeys.UnionWith(entries.Select(entry => entry.NewKey));
            for (var i = 0; i < entries.Length; i++)
            {
                if (byOldKey.TryGetValue(entries[i].OldKey, out var existing))
                    throw new InvalidOperationException($"Rename {i + 1}: the old key is already renamed by pack {existing.PackId}.");
                // A chain (A to B, B to C) would depend on replacement order.
                if (newKeys.Contains(entries[i].OldKey))
                    throw new InvalidOperationException($"Rename {i + 1}: the old key is also a rename target.");
            }
            if (entries.Any(entry => byOldKey.ContainsKey(entry.NewKey)))
                throw new InvalidOperationException("A new key is already renamed by another registration.");
            foreach (var entry in entries) byOldKey.Add(entry.OldKey, entry);
            return new Lease(this, entries);
        }
    }

    // Replaces every complete JSON string "old" with "new". A key that is only part of a
    // longer string never matches. Returns the input instance when nothing was renamed.
    internal string Apply(string json, out int tokens, out IReadOnlyCollection<string> packs)
    {
        tokens = 0;
        packs = Array.Empty<string>();
        if (string.IsNullOrEmpty(json)) return json;
        Rename[] current;
        lock (gate) current = byOldKey.Values.ToArray();
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

    internal void Clear()
    {
        lock (gate) byOldKey.Clear();
    }

    private void Release(Rename[] entries)
    {
        lock (gate)
            foreach (var entry in entries)
                if (byOldKey.TryGetValue(entry.OldKey, out var current) && ReferenceEquals(current, entry))
                    byOldKey.Remove(entry.OldKey);
    }

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

    private static bool IsValidPackId(string packId)
    {
        if (string.IsNullOrEmpty(packId)) return false;
        foreach (var character in packId)
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return false;
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

    private sealed class Lease : IDisposable
    {
        private readonly SaveKeyRenameRegistry owner;
        private readonly Rename[] entries;
        private int disposed;

        internal Lease(SaveKeyRenameRegistry owner, Rename[] entries)
        { this.owner = owner; this.entries = entries; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            owner.Release(entries);
        }
    }
}
