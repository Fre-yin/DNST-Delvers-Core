
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
internal sealed class PackLoadIntegrationRegistry : ICoreRegistry
{
    private readonly LeaseRegistry<string, Registration> registrations = new(DuplicateRegistration.ShareIfEqual,
        (existing, added) => Equals(existing.BeforeComponents, added.BeforeComponents)
            && Equals(existing.BeforeProfile, added.BeforeProfile)
            && Equals(existing.AfterProfile, added.AfterProfile)
            && Equals(existing.AfterCampaign, added.AfterCampaign),
        StringComparer.Ordinal);

    internal IDisposable Register(string packId,
        BeforeComponentsDeserializeHandler beforeComponents,
        BeforeUnitProfileDeserializeHandler beforeProfile,
        AfterUnitProfileDeserializeHandler afterProfile,
        AfterCampaignLoadedHandler afterCampaign)
    {
        PackIds.Require(packId, nameof(packId));
        if (beforeComponents == null && beforeProfile == null && afterProfile == null && afterCampaign == null)
            throw new ArgumentException("At least one load callback is required.", nameof(beforeComponents));
        return registrations.Register(packId,
            new Registration(packId, beforeComponents, beforeProfile, afterProfile, afterCampaign),
            _ => new InvalidOperationException("A different load integration already uses pack ID " + packId + "."));
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

    internal int Count => registrations.Count;

    public void Clear() => registrations.Clear();

    private void Notify(string stage, Action<Registration> callback,
        Action<string, string, Exception> onFailure)
    {
        foreach (var registration in registrations.Snapshot())
        {
            try { callback(registration); }
            catch (Exception ex) { onFailure?.Invoke(registration.PackId, stage, ex); }
        }
    }

    private sealed class Registration
    {
        internal readonly string PackId;
        internal readonly BeforeComponentsDeserializeHandler BeforeComponents;
        internal readonly BeforeUnitProfileDeserializeHandler BeforeProfile;
        internal readonly AfterUnitProfileDeserializeHandler AfterProfile;
        internal readonly AfterCampaignLoadedHandler AfterCampaign;

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
}
