using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Which unique units a campaign has had. Kept in Core's block of the save (CampaignPresenceBook), written only
// with the game's save; the marker files of older versions are read once per old save (LegacyMarkers) and never written,
// moved or removed. The public calls and their arguments stay as they were (the legacy name and the content are no longer used).
public static class CampaignPresence
{
    private static CampaignPresenceStore store;

    private static CampaignPresenceStore Store
    {
        get
        {
            var root = Application.persistentDataPath;
            if (store == null || !string.Equals(store.RootDirectory, Path.GetFullPath(root),
                StringComparison.OrdinalIgnoreCase))
                store = new CampaignPresenceStore(root);
            return store;
        }
    }

    public static bool IsValidCampaign(string value)
        => System.Guid.TryParse(value, out var guid) && guid != System.Guid.Empty;

    public static string LegacyMarkerName(string campaign, string suffix = null)
    {
        if (!System.Guid.TryParse(campaign, out var guid) || guid == System.Guid.Empty) return null;
        return guid.ToString("D") + suffix + ".marker";
    }

    public static bool Seen(string campaign, string stableId, string legacyMarkerFileName = null)
    {
        if (!System.Guid.TryParse(campaign, out var guid) || guid == System.Guid.Empty) return false;
        try { return CampaignPresenceBook.Seen(CoreSaveStore.Current, guid, stableId); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_PRESENCE_READ_FAILED id=" + stableId + ": " + ex);
            return false;
        }
    }

    // For CoreSaveStore.LegacyPresence: the old marker files of a campaign with their write times.
    internal static IEnumerable<(string Id, DateTime WrittenUtc)> LegacyMarkers(Guid campaign)
    {
        try { return Store.LegacyMarkers(campaign); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_PRESENCE_LEGACY_READ_FAILED: " + ex);
            return null;
        }
    }

    internal static bool IsTestCampaign(Guid campaign)
    {
        if (campaign == Guid.Empty) return false;
        try { return Store.IsTestCampaign(campaign); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_TEST_MARK_READ_FAILED: " + ex);
            return false;
        }
    }

    internal static bool MarkTestCampaign(Guid campaign)
    {
        if (campaign == Guid.Empty) return false;
        try
        {
            Store.MarkTestCampaign(campaign, "Created by a Delvers test. Uniqueness may be loosened here.\n");
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_TEST_MARK_WRITE_FAILED: " + ex);
            return false;
        }
    }

    internal static int Forget(Guid campaign)
    {
        if (campaign == Guid.Empty) return -1;
        try { return Store.Forget(campaign); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_FORGET_FAILED: " + ex);
            return -1;
        }
    }

    public static bool Mark(string campaign, string stableId, string legacyMarkerFileName, string content)
    {
        if (!System.Guid.TryParse(campaign, out var guid) || guid == System.Guid.Empty) return false;
        try
        {
            if (CampaignPresenceBook.Mark(CoreSaveStore.Current, guid, stableId))
                DelversHost.Info($"CAMPAIGN_PRESENCE_MARKED id={stableId} campaign={guid:D} bound={(CoreSaveStore.Current?.Campaign == guid ? 1 : 0)}");
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_PRESENCE_WRITE_FAILED id=" + stableId + ": " + ex);
            return false;
        }
    }
}
