using System.Text;

namespace DungeonSettlersDelvers.Core;

// New core markers use a neutral path; both FrierenPortrait legacy formats remain readable.
internal sealed class CampaignPresenceStore
{
    private readonly string root;
    private readonly object gate = new();
    private readonly HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

    internal CampaignPresenceStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A persistent data directory is required.", nameof(rootDirectory));
        root = Path.GetFullPath(rootDirectory);
    }

    internal string RootDirectory => root;

    internal string GetMarkerPath(Guid campaign, string stableId)
    {
        ValidateCampaign(campaign);
        ValidateStableId(stableId);
        return Path.Combine(root, "DungeonSettlersDelvers", "Core", "recruited",
            campaign.ToString("D"), stableId + ".marker");
    }

    internal bool HasSeen(Guid campaign, string stableId, string legacyMarkerFileName = null)
    {
        if (campaign == Guid.Empty || !IsStableId(stableId)) return false;
        var paths = new List<string>
        {
            GetMarkerPath(campaign, stableId),
            Path.Combine(root, "FrierenPortrait", "recruited", campaign.ToString("D"), stableId + ".marker")
        };
        if (IsSafeLegacyFileName(legacyMarkerFileName))
            paths.Add(Path.Combine(root, "FrierenPortrait", "recruited", legacyMarkerFileName));

        foreach (var path in paths)
        {
            lock (gate)
                if (seenPaths.Contains(path)) return true;
            if (!File.Exists(path)) continue;
            lock (gate) seenPaths.Add(path);
            return true;
        }
        return false;
    }

    internal bool Mark(Guid campaign, string stableId, string content,
        string legacyMarkerFileName = null)
    {
        if (campaign == Guid.Empty || !IsStableId(stableId)) return false;
        if (HasSeen(campaign, stableId, legacyMarkerFileName)) return true;

        var markerPath = GetMarkerPath(campaign, stableId);
        var directory = Path.GetDirectoryName(markerPath);
        var temporaryPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content ?? string.Empty);
            File.Move(temporaryPath, markerPath, true);
            lock (gate) seenPaths.Add(markerPath);
            return true;
        }
        catch
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { }
            throw;
        }
    }

    private static void ValidateCampaign(Guid campaign)
    {
        if (campaign == Guid.Empty) throw new ArgumentException("Campaign ID cannot be empty.", nameof(campaign));
    }

    private static void ValidateStableId(string value)
    {
        if (!IsStableId(value)) throw new ArgumentException("Stable campaign IDs may use lowercase letters, digits, and hyphens only.", nameof(value));
    }

    private static bool IsStableId(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (var character in value)
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return false;
        return true;
    }

    private static bool IsSafeLegacyFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.GetFileName(value) != value) return false;
        foreach (var character in value)
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                or >= '0' and <= '9' or '-' or '_' or '.')) return false;
        return true;
    }
}
