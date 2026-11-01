using DungeonSettlersDelvers.Core;
using static TestKit;

// Campaign presence markers and the campaign GUID for addons.
internal static class CampaignPresenceSection
{
    internal static void Run()
    {
        PresenceMarkers();
        CampaignGuidText();
    }

    static void PresenceMarkers()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DelversPresenceTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var campaign = Guid.Parse("a3d91e23-e351-4b2d-b09a-331260fbda9e");
            var store = new CampaignPresenceStore(tempRoot);
            var stableMarker = store.GetMarkerPath(campaign, "example-hero");
            Check(!store.HasSeen(campaign, "example-hero"), "new campaign starts unmarked");
            Check(store.Mark(campaign, "example-hero", "recruited"), "stable marker is written");
            Check(File.Exists(stableMarker) && store.HasSeen(campaign, "example-hero"),
                "written marker is visible immediately");
            Check(new CampaignPresenceStore(tempRoot).HasSeen(campaign, "example-hero"),
                "marker survives a fresh store instance");
            Check(!Directory.EnumerateFiles(Path.GetDirectoryName(stableMarker)!, "*.tmp").Any(),
                "atomic write leaves no temporary marker");
            Check(!store.HasSeen(Guid.NewGuid(), "example-hero"), "marker is scoped to its campaign");
            Check(!store.HasSeen(campaign, "lowell"), "marker is scoped to its character");
            Check(!store.HasSeen(Guid.Empty, "example-hero"), "empty campaign identity is rejected");
            Check(stableMarker.Contains(Path.Combine("DungeonSettlersDelvers", "Core", "recruited")),
                "new core markers use a neutral Delvers path");

            var hierarchicalLegacyCampaign = Guid.NewGuid();
            var hierarchicalLegacyDirectory = Path.Combine(tempRoot, "FrierenPortrait", "recruited",
                hierarchicalLegacyCampaign.ToString("D"));
            Directory.CreateDirectory(hierarchicalLegacyDirectory);
            File.WriteAllText(Path.Combine(hierarchicalLegacyDirectory, "liana.marker"), "legacy");
            Check(store.HasSeen(hierarchicalLegacyCampaign, "liana"),
                "legacy campaign/stableId markers remain readable");

            var legacyDirectory = Path.Combine(tempRoot, "FrierenPortrait", "recruited");
            Directory.CreateDirectory(legacyDirectory);
            var secondCampaign = Guid.NewGuid();
            var legacyName = secondCampaign.ToString("D") + "-Lowell.marker";
            File.WriteAllText(Path.Combine(legacyDirectory, legacyName), "legacy");
            Check(store.HasSeen(secondCampaign, "lowell", legacyName), "legacy marker is recognized");
            Check(store.Mark(secondCampaign, "lowell", "new", legacyName),
                "marking an existing legacy presence stays idempotent");
            var blockedRoot = Path.Combine(tempRoot, "root-is-file");
            File.WriteAllText(blockedRoot, "not a directory");
            var failingStore = new CampaignPresenceStore(blockedRoot);
            var failedCampaign = Guid.NewGuid();
            var writeFailed = false;
            try { failingStore.Mark(failedCampaign, "example-hero", "unwritable"); }
            catch (IOException) { writeFailed = true; }
            catch (UnauthorizedAccessException) { writeFailed = true; }
            Check(writeFailed, "marker write failure is reported");
            Check(!failingStore.HasSeen(failedCampaign, "example-hero"),
                "failed marker write does not mark the in-memory cache");
            var rejectedUnsafeId = false;
            try { store.GetMarkerPath(secondCampaign, "../outside"); }
            catch (ArgumentException) { rejectedUnsafeId = true; }
            Check(rejectedUnsafeId, "invalid marker IDs cannot resolve to a path");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    static void CampaignGuidText()
    {
        // (field test Antrag 4): the loaded campaign's GUID for addons, null outside a campaign.
        var campaign = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        Check(CampaignGuidRules.Text(Guid.Empty) == null && CampaignGuidRules.Text(campaign) == "0f8fad5b-d9cb-469f-a165-70867728950e"
                && CampaignGuidRules.ApiVersion == "1.9.0" && new Version(CampaignGuidRules.ApiVersion) > new Version("1.8.0"),
            "campaign GUID: the bound campaign as \"D\" text, none outside a campaign, from API 1.9.0");
    }
}
