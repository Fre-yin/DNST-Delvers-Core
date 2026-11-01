using DungeonSettlersDelvers.Core;
using static TestKit;

// Pack load integrations: callbacks around a campaign load.
internal static class PackLoadIntegrationSection
{
    internal static void Run()
    {
        var loadIntegrations = new PackLoadIntegrationRegistry();
        var loadCallbackTrace = new List<string>();
        var componentSaveList = new ComponentSaveList();
        var profileSaveData = new ComponentSaveBaseData();
        var loadEntity = new TestEntity();
        var loadedProfile = new UnitProfileComponent();
        var loadedCampaign = new CampaignDataContainer();
        var beforeComponents = new BeforeComponentsDeserializeHandler((saved, entity) =>
        {
            Check(ReferenceEquals(saved, componentSaveList) && ReferenceEquals(entity, loadEntity),
                "before-components load callback receives the original deserialize context");
            loadCallbackTrace.Add("before-components");
        });
        var beforeProfile = new BeforeUnitProfileDeserializeHandler((saved, entity) =>
        {
            Check(ReferenceEquals(saved, profileSaveData) && ReferenceEquals(entity, loadEntity),
                "before-profile load callback receives the original deserialize context");
            loadCallbackTrace.Add("before-profile");
        });
        var afterProfile = new AfterUnitProfileDeserializeHandler(profile =>
        {
            Check(ReferenceEquals(profile, loadedProfile),
                "after-profile load callback receives the loaded profile");
            loadCallbackTrace.Add("after-profile");
        });
        var afterCampaign = new AfterCampaignLoadedHandler(campaign =>
        {
            Check(ReferenceEquals(campaign, loadedCampaign),
                "after-campaign load callback receives the loaded campaign");
            loadCallbackTrace.Add("after-campaign");
        });
        var loadLease = loadIntegrations.Register("example-pack", beforeComponents, beforeProfile,
            afterProfile, afterCampaign);
        var duplicateLoadLease = loadIntegrations.Register("example-pack", beforeComponents, beforeProfile,
            afterProfile, afterCampaign);
        Check(loadIntegrations.Count == 1,
            "an identical load integration reuses one registration with independent leases");
        var conflictingLoadIntegrationRejected = false;
        try
        {
            loadIntegrations.Register("example-pack", (_, _) => { }, beforeProfile, afterProfile,
                afterCampaign).Dispose();
        }
        catch (InvalidOperationException)
        {
            conflictingLoadIntegrationRejected = true;
        }
        Check(conflictingLoadIntegrationRejected && loadIntegrations.Count == 1,
            "a pack cannot replace an existing load integration with different callbacks");
        var unexpectedLoadFailureCount = 0;
        void UnexpectedLoadFailure(string _, string __, Exception ___) => unexpectedLoadFailureCount++;
        loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity, UnexpectedLoadFailure);
        loadIntegrations.NotifyBeforeProfile(profileSaveData, loadEntity, UnexpectedLoadFailure);
        loadIntegrations.NotifyAfterProfile(loadedProfile, UnexpectedLoadFailure);
        loadIntegrations.NotifyAfterCampaign(loadedCampaign, UnexpectedLoadFailure);
        Check(unexpectedLoadFailureCount == 0
                && loadCallbackTrace.SequenceEqual(new[]
                {
                    "before-components", "before-profile", "after-profile", "after-campaign"
                }),
            "load integration dispatches every callback stage once despite duplicate leases");
        var expectedLoadException = new InvalidOperationException("expected load callback failure");
        var failureReports = new List<(string PackId, string Stage, Exception Error)>();
        var callbacksAfterFailure = 0;
        var failingLoadLease = loadIntegrations.Register("broken-pack",
            (_, _) => throw expectedLoadException, null, null, null);
        var healthyLoadLease = loadIntegrations.Register("healthy-pack",
            (_, _) => callbacksAfterFailure++, null, null, null);
        loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity,
            (packId, stage, error) => failureReports.Add((packId, stage, error)));
        Check(failureReports.Count == 1
                && failureReports[0].PackId == "broken-pack"
                && failureReports[0].Stage == "before-components"
                && ReferenceEquals(failureReports[0].Error, expectedLoadException),
            "a failing load callback reports its pack, stage and original exception");
        Check(callbacksAfterFailure == 1,
            "one failing load callback does not prevent another pack from receiving the stage");
        failingLoadLease.Dispose();
        healthyLoadLease.Dispose();
        loadLease.Dispose();
        loadLease.Dispose();
        Check(loadIntegrations.Count == 1,
            "disposing one reused load-integration lease keeps the registration active");
        duplicateLoadLease.Dispose();
        Check(loadIntegrations.Count == 0,
            "disposing the last load-integration lease removes the registration");
        var callbacksAfterClear = 0;
        var clearedLoadLease = loadIntegrations.Register("cleanup-pack",
            (_, _) => callbacksAfterClear++, null, null, null);
        loadIntegrations.Clear();
        loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity, UnexpectedLoadFailure);
        clearedLoadLease.Dispose();
        Check(loadIntegrations.Count == 0 && callbacksAfterClear == 0,
            "Core cleanup clears load integrations and later lease disposal remains harmless");
    }
}
