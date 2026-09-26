namespace DungeonSettlersDelvers.Core;

// Pack predicates are keyed by package ID and leased so duplicate runtime
// initialization cannot replace or prematurely remove another registration.
internal sealed class UniqueCandidatePredicateRegistry<TCandidate>
{
    private readonly object gate = new();
    private readonly Dictionary<string, Registration> registrations = new(StringComparer.Ordinal);

    internal int Count
    {
        get { lock (gate) return registrations.Count; }
    }

    internal IDisposable Register(string packId, Func<TCandidate, bool> predicate)
    {
        if (!IsValidPackId(packId))
            throw new ArgumentException("Pack IDs may use lowercase letters, digits, and hyphens.", nameof(packId));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        lock (gate)
        {
            if (registrations.TryGetValue(packId, out var existing))
            {
                if (!Equals(existing.Predicate, predicate))
                    throw new InvalidOperationException("A different unique-candidate predicate already uses pack ID " + packId + ".");
                existing.LeaseCount++;
                return new Lease(this, existing);
            }

            var registration = new Registration(packId, predicate);
            registrations.Add(packId, registration);
            return new Lease(this, registration);
        }
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

    internal void Clear()
    {
        lock (gate) registrations.Clear();
    }

    private Registration[] Snapshot()
    {
        lock (gate)
            return registrations.Values.OrderBy(registration => registration.PackId, StringComparer.Ordinal).ToArray();
    }

    private bool ShouldReportFailure(Registration registration)
    {
        lock (gate)
        {
            if (registration.FailureLogged) return false;
            registration.FailureLogged = true;
            return true;
        }
    }

    private void Release(Registration registration)
    {
        lock (gate)
        {
            if (registrations.TryGetValue(registration.PackId, out var current)
                && ReferenceEquals(current, registration) && --registration.LeaseCount <= 0)
                registrations.Remove(registration.PackId);
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
        internal readonly Func<TCandidate, bool> Predicate;
        internal int LeaseCount = 1;
        internal bool FailureLogged;

        internal Registration(string packId, Func<TCandidate, bool> predicate)
        { PackId = packId; Predicate = predicate; }
    }

    private sealed class Lease : IDisposable
    {
        private readonly UniqueCandidatePredicateRegistry<TCandidate> owner;
        private readonly Registration registration;
        private int disposed;

        internal Lease(UniqueCandidatePredicateRegistry<TCandidate> owner, Registration registration)
        { this.owner = owner; this.registration = registration; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            owner.Release(registration);
        }
    }
}
