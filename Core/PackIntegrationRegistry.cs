namespace DungeonSettlersDelvers.Core;

// Loader-independent dispatcher for optional character packs. Keyed by pack ID; the same
// integration again shares its lease, a different one under the same ID is rejected.
internal sealed class PackIntegrationRegistry<TContext> : ICoreRegistry
{
    private readonly LeaseRegistry<string, Registration> registrations = new(DuplicateRegistration.ShareIfEqual,
        (existing, added) => Equals(existing.Observer, added.Observer) && Equals(existing.SuppressPolicy, added.SuppressPolicy),
        StringComparer.Ordinal);

    internal IDisposable Register(string packId, Action<TContext> observer, Func<bool> suppressPolicy)
    {
        PackIds.Require(packId, nameof(packId));
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (suppressPolicy == null) throw new ArgumentNullException(nameof(suppressPolicy));
        return registrations.Register(packId, new Registration(packId, observer, suppressPolicy),
            _ => new InvalidOperationException("A different integration already uses pack ID " + packId + "."));
    }

    internal bool AnyPolicy(Action<string, Exception> onFailure)
    {
        foreach (var registration in registrations.Snapshot())
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
        foreach (var registration in registrations.Snapshot())
        {
            try { registration.Observer(context); }
            catch (Exception ex) { onFailure?.Invoke(registration.PackId, ex); }
        }
    }

    internal int Count => registrations.Count;

    public void Clear() => registrations.Clear();

    private sealed class Registration
    {
        internal readonly string PackId;
        internal readonly Action<TContext> Observer;
        internal readonly Func<bool> SuppressPolicy;
        internal bool PolicyFailureLogged;

        internal Registration(string packId, Action<TContext> observer, Func<bool> suppressPolicy)
        { PackId = packId; Observer = observer; SuppressPolicy = suppressPolicy; }
    }
}
