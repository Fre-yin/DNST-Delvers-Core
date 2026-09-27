#if BEPINEX
using global::Refactor;
using global::Refactor.Component;
using global::Refactor.Main;
#else
using Il2CppRefactor;
using Il2CppRefactor.Component;
using Il2CppRefactor.Main;
#endif
#if BEPINEX
using ComponentSaveList = Il2CppSystem.Collections.Generic.List<global::Refactor.ComponentSaveData>;
#else
using ComponentSaveList = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.ComponentSaveData>;
#endif

namespace DungeonSettlersDelvers.Core;

public delegate void BeforeComponentsDeserializeHandler(ComponentSaveList saved, IEntity entity);
public delegate void BeforeUnitProfileDeserializeHandler(ComponentSaveBaseData saved, IEntity entity);
public delegate void AfterUnitProfileDeserializeHandler(UnitProfileComponent profile);
public delegate void AfterCampaignLoadedHandler(CampaignDataContainer campaign);

// Optional packs register data migrations here instead of patching native load
// methods themselves. Core owns the audited Harmony boundaries and reports a
// failed callback to the load-integrity guard before native save code can run.
// Throwing is therefore fatal: saving stays blocked until the game restarts.
// Throw only when saved data may be lost or half-migrated; runtime fix-ups that
// can be retried must handle their own failures.
internal sealed class PackLoadIntegrationRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, Registration> registrations = new(StringComparer.Ordinal);

    internal IDisposable Register(string packId,
        BeforeComponentsDeserializeHandler beforeComponents,
        BeforeUnitProfileDeserializeHandler beforeProfile,
        AfterUnitProfileDeserializeHandler afterProfile,
        AfterCampaignLoadedHandler afterCampaign)
    {
        if (!IsValidPackId(packId))
            throw new ArgumentException("Pack IDs may use lowercase letters, digits, and hyphens.", nameof(packId));
        if (beforeComponents == null && beforeProfile == null && afterProfile == null && afterCampaign == null)
            throw new ArgumentException("At least one load callback is required.", nameof(beforeComponents));

        lock (gate)
        {
            if (registrations.TryGetValue(packId, out var existing))
            {
                if (!Equals(existing.BeforeComponents, beforeComponents)
                    || !Equals(existing.BeforeProfile, beforeProfile)
                    || !Equals(existing.AfterProfile, afterProfile)
                    || !Equals(existing.AfterCampaign, afterCampaign))
                    throw new InvalidOperationException("A different load integration already uses pack ID " + packId + ".");
                existing.LeaseCount++;
                return new Lease(this, existing);
            }

            var registration = new Registration(packId, beforeComponents, beforeProfile,
                afterProfile, afterCampaign);
            registrations.Add(packId, registration);
            return new Lease(this, registration);
        }
    }

    internal void NotifyBeforeComponents(ComponentSaveList saved, IEntity entity,
        Action<string, string, Exception> onFailure)
        => Notify("before-components", registration => registration.BeforeComponents?.Invoke(saved, entity), onFailure);

    internal void NotifyBeforeProfile(ComponentSaveBaseData saved, IEntity entity,
        Action<string, string, Exception> onFailure)
        => Notify("before-unit-profile", registration => registration.BeforeProfile?.Invoke(saved, entity), onFailure);

    internal void NotifyAfterProfile(UnitProfileComponent profile,
        Action<string, string, Exception> onFailure)
        => Notify("after-unit-profile", registration => registration.AfterProfile?.Invoke(profile), onFailure);

    internal void NotifyAfterCampaign(CampaignDataContainer campaign,
        Action<string, string, Exception> onFailure)
        => Notify("after-campaign", registration => registration.AfterCampaign?.Invoke(campaign), onFailure);

    internal int Count
    {
        get { lock (gate) return registrations.Count; }
    }

    internal void Clear()
    {
        lock (gate) registrations.Clear();
    }

    private void Notify(string stage, Action<Registration> callback,
        Action<string, string, Exception> onFailure)
    {
        foreach (var registration in Snapshot())
        {
            try { callback(registration); }
            catch (Exception ex) { onFailure?.Invoke(registration.PackId, stage, ex); }
        }
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
        internal readonly BeforeComponentsDeserializeHandler BeforeComponents;
        internal readonly BeforeUnitProfileDeserializeHandler BeforeProfile;
        internal readonly AfterUnitProfileDeserializeHandler AfterProfile;
        internal readonly AfterCampaignLoadedHandler AfterCampaign;
        internal int LeaseCount = 1;

        internal Registration(string packId,
            BeforeComponentsDeserializeHandler beforeComponents,
            BeforeUnitProfileDeserializeHandler beforeProfile,
            AfterUnitProfileDeserializeHandler afterProfile,
            AfterCampaignLoadedHandler afterCampaign)
        {
            PackId = packId;
            BeforeComponents = beforeComponents;
            BeforeProfile = beforeProfile;
            AfterProfile = afterProfile;
            AfterCampaign = afterCampaign;
        }
    }

    private sealed class Lease : IDisposable
    {
        private readonly PackLoadIntegrationRegistry owner;
        private readonly Registration registration;
        private bool disposed;

        internal Lease(PackLoadIntegrationRegistry owner, Registration registration)
        {
            this.owner = owner;
            this.registration = registration;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.Release(registration);
        }
    }
}
