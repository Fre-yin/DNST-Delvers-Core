namespace DungeonSettlersDelvers.Core;

// Fixed traits are keyed by the character's stable profile ID and leased per
// pack, so duplicate runtime initialization cannot replace or prematurely
// remove another pack's registration. See FixedTraitRules.
internal sealed class FixedTraitRegistry : ICoreRegistry
{
    private readonly object gate = new();
    private readonly LeaseRegistry<string, Registration> registrations = new(DuplicateRegistration.ShareIfEqual,
        (existing, added) => existing.PackId == added.PackId
            && existing.Traits.SequenceEqual(added.Traits, StringComparer.Ordinal),
        StringComparer.Ordinal);

    internal int Count => registrations.Count;

    internal IDisposable Register(string packId, string profileKey, IEnumerable<string> orderedTraitKeys)
    {
        PackIds.Require(packId, nameof(packId));
        if (string.IsNullOrWhiteSpace(profileKey))
            throw new ArgumentException("A stable profile ID is required.", nameof(profileKey));
        if (orderedTraitKeys == null) throw new ArgumentNullException(nameof(orderedTraitKeys));
        var traits = orderedTraitKeys.ToArray();
        if (traits.Length == 0 || traits.Any(string.IsNullOrWhiteSpace)
            || traits.Distinct(StringComparer.Ordinal).Count() != traits.Length)
            throw new ArgumentException("Fixed traits must be a non-empty list of distinct keys.", nameof(orderedTraitKeys));

        return registrations.Register(profileKey, new Registration(packId, profileKey, traits),
            existing => new InvalidOperationException("Profile " + profileKey
                + " already has different fixed traits from pack " + existing.PackId + "."));
    }

    internal bool TryGet(string profileKey, out string packId, out IReadOnlyList<string> traits)
    {
        if (registrations.TryGet(profileKey, out var registration))
        {
            packId = registration.PackId;
            traits = registration.Traits;
            return true;
        }
        packId = null;
        traits = null;
        return false;
    }

    // Profiles with fixed traits are unique characters of a pack.
    internal IReadOnlyList<string> ProfileKeys()
    {
        var registrations = this.registrations.Snapshot();
        var keys = new string[registrations.Length];
        for (var i = 0; i < registrations.Length; i++) keys[i] = registrations[i].ProfileKey;
        return keys;
    }

    // Report a failing restore once per registration; a broken save must not
    // flood the log on every unit load.
    internal bool ShouldReportFailure(string profileKey)
    {
        if (!registrations.TryGet(profileKey, out var registration)) return false;
        lock (gate)
        {
            if (registration.FailureLogged) return false;
            registration.FailureLogged = true;
            return true;
        }
    }

    public void Clear() => registrations.Clear();

    private sealed class Registration
    {
        internal readonly string PackId;
        internal readonly string ProfileKey;
        internal readonly IReadOnlyList<string> Traits;
        internal bool FailureLogged;

        internal Registration(string packId, string profileKey, string[] traits)
        { PackId = packId; ProfileKey = profileKey; Traits = Array.AsReadOnly(traits); }
    }
}
