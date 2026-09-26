namespace DungeonSettlersDelvers.Core;

// Loader-independent dispatcher for optional character packs.
internal sealed class PackIntegrationRegistry<TContext>
{
    private readonly object gate = new();
    private readonly Dictionary<string, Registration> registrations = new(StringComparer.Ordinal);

    internal IDisposable Register(string packId, Action<TContext> observer, Func<bool> suppressPolicy)
    {
        if (!IsValidPackId(packId))
            throw new ArgumentException("Pack IDs may use lowercase letters, digits, and hyphens.", nameof(packId));
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (suppressPolicy == null) throw new ArgumentNullException(nameof(suppressPolicy));

        lock (gate)
        {
            if (registrations.TryGetValue(packId, out var existing))
            {
                if (!Equals(existing.Observer, observer) || !Equals(existing.SuppressPolicy, suppressPolicy))
                    throw new InvalidOperationException("A different integration already uses pack ID " + packId + ".");
                existing.LeaseCount++;
                return new Lease(this, existing);
            }
            var registration = new Registration(packId, observer, suppressPolicy);
            registrations.Add(packId, registration);
            return new Lease(this, registration);
        }
    }

    internal bool AnyPolicy(Action<string, Exception> onFailure)
    {
        foreach (var registration in Snapshot())
        {
            try { if (registration.SuppressPolicy()) return true; }
            catch (Exception ex)
            {
                if (!registration.PolicyFailureLogged)
                {
                    registration.PolicyFailureLogged = true;
                    onFailure?.Invoke(registration.PackId, ex);
                }
            }
        }
        return false;
    }

    internal void Notify(TContext context, Action<string, Exception> onFailure)
    {
        foreach (var registration in Snapshot())
        {
            try { registration.Observer(context); }
            catch (Exception ex) { onFailure?.Invoke(registration.PackId, ex); }
        }
    }

    internal int Count
    {
        get { lock (gate) return registrations.Count; }
    }

    internal void Clear()
    {
        lock (gate) registrations.Clear();
    }

    private Registration[] Snapshot()
    {
        lock (gate) return registrations.Values.ToArray();
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
        internal readonly Action<TContext> Observer;
        internal readonly Func<bool> SuppressPolicy;
        internal int LeaseCount = 1;
        internal bool PolicyFailureLogged;

        internal Registration(string packId, Action<TContext> observer, Func<bool> suppressPolicy)
        { PackId = packId; Observer = observer; SuppressPolicy = suppressPolicy; }
    }

    private sealed class Lease : IDisposable
    {
        private readonly PackIntegrationRegistry<TContext> owner;
        private readonly Registration registration;
        private bool disposed;

        internal Lease(PackIntegrationRegistry<TContext> owner, Registration registration)
        { this.owner = owner; this.registration = registration; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.Release(registration);
        }
    }
}
