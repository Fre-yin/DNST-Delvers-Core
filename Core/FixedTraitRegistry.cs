namespace DungeonSettlersDelvers.Core;

// Fixed traits are keyed by the character's stable profile ID and leased per
// pack, so duplicate runtime initialization cannot replace or prematurely
// remove another pack's registration. See FixedTraitRules.
internal sealed class FixedTraitRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, Registration> registrations = new(StringComparer.Ordinal);

    internal int Count
    {
        get { lock (gate) return registrations.Count; }
    }

    internal IDisposable Register(string packId, string profileKey, IEnumerable<string> orderedTraitKeys)
    {
        if (!IsValidPackId(packId))
            throw new ArgumentException("Pack IDs may use lowercase letters, digits, and hyphens.", nameof(packId));
        if (string.IsNullOrWhiteSpace(profileKey))
            throw new ArgumentException("A stable profile ID is required.", nameof(profileKey));
        if (orderedTraitKeys == null) throw new ArgumentNullException(nameof(orderedTraitKeys));
        var traits = orderedTraitKeys.ToArray();
        if (traits.Length == 0 || traits.Any(string.IsNullOrWhiteSpace)
            || traits.Distinct(StringComparer.Ordinal).Count() != traits.Length)
            throw new ArgumentException("Fixed traits must be a non-empty list of distinct keys.", nameof(orderedTraitKeys));

        lock (gate)
        {
            if (registrations.TryGetValue(profileKey, out var existing))
            {
                if (existing.PackId != packId || !existing.Traits.SequenceEqual(traits, StringComparer.Ordinal))
                    throw new InvalidOperationException("Profile " + profileKey
                        + " already has different fixed traits from pack " + existing.PackId + ".");
                existing.LeaseCount++;
                return new Lease(this, existing);
            }

            var registration = new Registration(packId, profileKey, traits);
            registrations.Add(profileKey, registration);
            return new Lease(this, registration);
        }
    }

    internal bool TryGet(string profileKey, out string packId, out IReadOnlyList<string> traits)
    {
        lock (gate)
        {
            if (profileKey != null && registrations.TryGetValue(profileKey, out var registration))
            {
                packId = registration.PackId;
                traits = registration.Traits;
                return true;
            }
        }
        packId = null;
        traits = null;
        return false;
    }

    // Report a failing restore once per registration; a broken save must not
    // flood the log on every unit load.
    internal bool ShouldReportFailure(string profileKey)
    {
        lock (gate)
        {
            if (profileKey == null || !registrations.TryGetValue(profileKey, out var registration)
                || registration.FailureLogged) return false;
            registration.FailureLogged = true;
            return true;
        }
    }

    internal void Clear()
    {
        lock (gate) registrations.Clear();
    }

    private void Release(Registration registration)
    {
        lock (gate)
        {
            if (registrations.TryGetValue(registration.ProfileKey, out var current)
                && ReferenceEquals(current, registration) && --registration.LeaseCount <= 0)
                registrations.Remove(registration.ProfileKey);
        }
    }

    private static bool IsValidPackId(string packId)
    {
        if (string.IsNullOrEmpty(packId)) return false;
        foreach (var character in packId)
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return false;
        return true;
    }

    private sealed class Registration
    {
        internal readonly string PackId;
        internal readonly string ProfileKey;
        internal readonly IReadOnlyList<string> Traits;
        internal int LeaseCount = 1;
        internal bool FailureLogged;

        internal Registration(string packId, string profileKey, string[] traits)
        { PackId = packId; ProfileKey = profileKey; Traits = Array.AsReadOnly(traits); }
    }

    private sealed class Lease : IDisposable
    {
        private readonly FixedTraitRegistry owner;
        private readonly Registration registration;
        private int disposed;

        internal Lease(FixedTraitRegistry owner, Registration registration)
        { this.owner = owner; this.registration = registration; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            owner.Release(registration);
        }
    }
}
