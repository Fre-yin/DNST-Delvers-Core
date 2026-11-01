namespace DungeonSettlersDelvers.Core;

// Pack predicates are keyed by package ID and leased so duplicate runtime
// initialization cannot replace or prematurely remove another registration.
internal sealed class UniqueCandidatePredicateRegistry<TCandidate> : ICoreRegistry
{
    private readonly object gate = new();
    private readonly LeaseRegistry<string, Registration> registrations = new(DuplicateRegistration.ShareIfEqual,
        (existing, added) => Equals(existing.Predicate, added.Predicate), StringComparer.Ordinal);

    internal int Count => registrations.Count;

    internal IDisposable Register(string packId, Func<TCandidate, bool> predicate)
    {
        PackIds.Require(packId, nameof(packId));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return registrations.Register(packId, new Registration(packId, predicate),
            _ => new InvalidOperationException("A different unique-candidate predicate already uses pack ID " + packId + "."));
    }

    internal bool IsUnique(TCandidate candidate, Action<string, Exception> onFailure)
    {
        if (candidate is null) return false;
        foreach (var registration in Snapshot())
        {
            try
            {
                if (registration.Predicate(candidate)) return true;
            }
            catch (Exception ex)
            {
                if (ShouldReportFailure(registration))
                {
                    try { onFailure?.Invoke(registration.PackId, ex); }
                    catch { /* Logging failures must not break candidate UI. */ }
                }
            }
        }
        return false;
    }

    public void Clear() => registrations.Clear();

    // Sorted by pack ID, so the first matching pack does not depend on load order.
    private Registration[] Snapshot()
        => registrations.Snapshot().OrderBy(registration => registration.PackId, StringComparer.Ordinal).ToArray();

    private bool ShouldReportFailure(Registration registration)
    {
        lock (gate)
        {
            if (registration.FailureLogged) return false;
            registration.FailureLogged = true;
            return true;
        }
    }

    private sealed class Registration
    {
        internal readonly string PackId;
        internal readonly Func<TCandidate, bool> Predicate;
        internal bool FailureLogged;

        internal Registration(string packId, Func<TCandidate, bool> predicate)
        { PackId = packId; Predicate = predicate; }
    }
}
