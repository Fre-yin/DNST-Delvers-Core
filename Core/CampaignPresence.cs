using UnityEngine;

namespace DungeonSettlersDelvers.Core;

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
        try { return Store.HasSeen(guid, stableId, legacyMarkerFileName); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_PRESENCE_READ_FAILED id=" + stableId + ": " + ex);
            return false;
        }
    }

    public static bool Mark(string campaign, string stableId, string legacyMarkerFileName, string content)
    {
        if (!System.Guid.TryParse(campaign, out var guid) || guid == System.Guid.Empty) return false;
        try { return Store.Mark(guid, stableId, content, legacyMarkerFileName); }
        catch (Exception ex)
        {
            DelversHost.Error("CAMPAIGN_PRESENCE_WRITE_FAILED id=" + stableId + ": " + ex);
            return false;
        }
    }
}
