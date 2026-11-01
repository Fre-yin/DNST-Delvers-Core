namespace DungeonSettlersDelvers.Core;

// The one rule for pack IDs, shared by every registration in Core (review, G1).
internal static class PackIds
{
    internal const string Rule = "Pack IDs may use lowercase letters, digits, and hyphens.";

    internal static bool IsValid(string packId)
    {
        if (string.IsNullOrEmpty(packId)) return false;
        foreach (var character in packId)
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return false;
        return true;
    }

    internal static void Require(string packId, string parameterName)
    {
        if (!IsValid(packId)) throw new ArgumentException(Rule, parameterName);
    }
}

// What a second registration of a taken key does.
internal enum DuplicateRegistration
{
    // The same registration again, for example after a duplicate runtime start, shares the
    // entry and counts one more lease; a different one is a conflict.
    ShareIfEqual,
    // Every second registration of a key is a conflict, even from the same pack.
    Reject
}

internal interface ICoreRegistry
{
    void Clear();
}

// Registries announce themselves where they are created, so Core's shutdown clears every one
// of them without a list kept by hand.
internal static class CoreRegistries
{
    private static readonly object Gate = new();
    private static readonly List<ICoreRegistry> Tracked = new();

    internal static T Track<T>(T registry) where T : class, ICoreRegistry
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        lock (Gate) if (!Tracked.Contains(registry)) Tracked.Add(registry);
        return registry;
    }

    internal static int Count
    {
        get { lock (Gate) return Tracked.Count; }
    }

    internal static void ClearAll()
    {
        ICoreRegistry[] registries;
        lock (Gate) registries = Tracked.ToArray();
        foreach (var registry in registries) registry.Clear();
    }
}

// Keyed registrations handed out as leases. The registries in Core keep their own checks and
// messages and store through this class. Entries keep their registration order; Revision rises
// with every change. A lease may be disposed several times, from any thread, and after Clear.
internal sealed class LeaseRegistry<TKey, TEntry> : ICoreRegistry where TEntry : class
{
    private readonly object gate = new();
    private readonly DuplicateRegistration duplicates;
    private readonly Func<TEntry, TEntry, bool> isSameRegistration;
    private readonly Dictionary<TKey, Slot> slots;
    private readonly List<Slot> order = new();
    private int revision;

    internal LeaseRegistry(DuplicateRegistration duplicates, Func<TEntry, TEntry, bool> isSameRegistration = null,
        IEqualityComparer<TKey> keys = null)
    {
        if (duplicates == DuplicateRegistration.ShareIfEqual && isSameRegistration == null)
            throw new ArgumentNullException(nameof(isSameRegistration), "Sharing needs a rule for equal registrations.");
        this.duplicates = duplicates;
        this.isSameRegistration = isSameRegistration;
        slots = new Dictionary<TKey, Slot>(keys ?? EqualityComparer<TKey>.Default);
    }

    internal int Count
    {
        get { lock (gate) return slots.Count; }
    }

    internal int Revision
    {
        get { lock (gate) return revision; }
    }

    // conflict builds the exception for a key that is taken and may not be shared, so every
    // registry keeps its own exception type and message.
    internal IDisposable Register(TKey key, TEntry entry, Func<TEntry, Exception> conflict)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (conflict == null) throw new ArgumentNullException(nameof(conflict));
        lock (gate)
        {
            if (slots.TryGetValue(key, out var existing))
            {
                if (duplicates == DuplicateRegistration.ShareIfEqual && isSameRegistration(existing.Entry, entry))
                {
                    existing.Leases++;
                    return new Lease(this, new[] { existing });
                }
                throw conflict(existing.Entry) ?? new InvalidOperationException("The key is already registered.");
            }
            var slot = Add(key, entry);
            revision++;
            return new Lease(this, new[] { slot });
        }
    }

    // Several keys at once, all or none. validate sees the current entries and throws to refuse.
    internal IDisposable RegisterAll(IReadOnlyList<KeyValuePair<TKey, TEntry>> items,
        Action<IReadOnlyCollection<TEntry>> validate)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) throw new ArgumentException("At least one entry is required.", nameof(items));
        lock (gate)
        {
            validate?.Invoke(order.Select(slot => slot.Entry).ToArray());
            var keys = new HashSet<TKey>(slots.Comparer);
            foreach (var item in items)
                if (item.Value == null || slots.ContainsKey(item.Key) || !keys.Add(item.Key))
                    throw new InvalidOperationException("Entry " + (keys.Count + 1) + " is missing or its key is already registered.");
            var added = items.Select(item => Add(item.Key, item.Value)).ToArray();
            revision++;
            return new Lease(this, added);
        }
    }

    internal bool TryGet(TKey key, out TEntry entry)
    {
        lock (gate)
        {
            if (key != null && slots.TryGetValue(key, out var slot))
            {
                entry = slot.Entry;
                return true;
            }
        }
        entry = null;
        return false;
    }

    internal TEntry[] Snapshot()
    {
        lock (gate) return order.Select(slot => slot.Entry).ToArray();
    }

    public void Clear()
    {
        lock (gate)
        {
            slots.Clear();
            order.Clear();
            revision++;
        }
    }

    private Slot Add(TKey key, TEntry entry)
    {
        var slot = new Slot(key, entry);
        slots.Add(key, slot);
        order.Add(slot);
        return slot;
    }

    private void Release(Slot[] released)
    {
        lock (gate)
        {
            var changed = false;
            foreach (var slot in released)
            {
                if (!slots.TryGetValue(slot.Key, out var current) || !ReferenceEquals(current, slot)
                    || --slot.Leases > 0) continue;
                slots.Remove(slot.Key);
                order.Remove(slot);
                changed = true;
            }
            if (changed) revision++;
        }
    }

    private sealed class Slot
    {
        internal readonly TKey Key;
        internal readonly TEntry Entry;
        internal int Leases = 1;

        internal Slot(TKey key, TEntry entry) { Key = key; Entry = entry; }
    }

    private sealed class Lease : IDisposable
    {
        private readonly LeaseRegistry<TKey, TEntry> owner;
        private readonly Slot[] slots;
        private int disposed;

        internal Lease(LeaseRegistry<TKey, TEntry> owner, Slot[] slots) { this.owner = owner; this.slots = slots; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            owner.Release(slots);
        }
    }
}
