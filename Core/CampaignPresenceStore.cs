using System.Text;

namespace DungeonSettlersDelvers.Core;

// New core markers use a neutral path; both FrierenPortrait legacy formats remain readable.
internal sealed class CampaignPresenceStore
{
    private readonly string root;
    private readonly object gate = new();
    private readonly HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> testCampaigns = new(StringComparer.OrdinalIgnoreCase);

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

    // The marker files an older Core wrote for one campaign, read only (CampaignPresenceBook.Adopt): per id of the table the
    // earliest of its three places, and every other *.marker of the campaign's folder as an id outside the table.
    internal List<(string Id, DateTime WrittenUtc)> LegacyMarkers(Guid campaign)
    {
        ValidateCampaign(campaign);
        var id = campaign.ToString("D");
        var folder = Path.Combine(root, "DungeonSettlersDelvers", "Core", "recruited", id);
        var result = new List<(string Id, DateTime WrittenUtc)>();
        foreach (var stableId in CampaignPresenceRules.Bits)
        {
            var paths = new[]
            {
                Path.Combine(folder, stableId + ".marker"),
                Path.Combine(root, "FrierenPortrait", "recruited", id, stableId + ".marker"),
                Path.Combine(root, "FrierenPortrait", "recruited", id + CampaignPresenceRules.LegacySuffix(stableId) + ".marker")
            };
            DateTime? earliest = null;
            foreach (var path in paths)
                if (File.Exists(path))
                {
                    var written = File.GetLastWriteTimeUtc(path);
                    if (earliest == null || written < earliest) earliest = written;
                }
            if (earliest is DateTime found) result.Add((stableId, found));
        }
        if (Directory.Exists(folder))
            foreach (var path in Directory.GetFiles(folder, "*.marker", SearchOption.TopDirectoryOnly))
            {
                var stableId = Path.GetFileNameWithoutExtension(path);
                if (CampaignPresenceRules.BitOf(stableId) < 0 && IsStableId(stableId)) result.Add((stableId, File.GetLastWriteTimeUtc(path)));
            }
        return result;
    }

    // Test campaigns are named by GUID only, never by clan name: a player cannot loosen a real
    // campaign by choosing a name (review, H8).
    internal string GetTestCampaignMarkerPath(Guid campaign)
    {
        ValidateCampaign(campaign);
        return Path.Combine(root, "DungeonSettlersDelvers", "Core", "testcampaigns", campaign.ToString("D") + ".marker");
    }

    internal bool IsTestCampaign(Guid campaign)
    {
        if (campaign == Guid.Empty) return false;
        var path = GetTestCampaignMarkerPath(campaign);
        lock (gate)
            if (testCampaigns.TryGetValue(path, out var known)) return known;
        var exists = File.Exists(path);
        lock (gate) testCampaigns[path] = exists;
        return exists;
    }

    internal void MarkTestCampaign(Guid campaign, string content)
    {
        var path = GetTestCampaignMarkerPath(campaign);
        WriteAtomic(path, content);
        lock (gate) testCampaigns[path] = true;
    }

    // Removes everything Core remembers about one campaign: its uniqueness markers (current and
    // legacy layout) and its test mark. Returns the number of removed files.
    internal int Forget(Guid campaign)
    {
        ValidateCampaign(campaign);
        var id = campaign.ToString("D");
        var removed = 0;
        foreach (var directory in new[]
                 {
                     Path.Combine(root, "DungeonSettlersDelvers", "Core", "recruited", id),
                     Path.Combine(root, "FrierenPortrait", "recruited", id)
                 })
        {
            if (!Directory.Exists(directory)) continue;
            removed += Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(directory, true);
        }
        var legacyRoot = Path.Combine(root, "FrierenPortrait", "recruited");
        if (Directory.Exists(legacyRoot))
            foreach (var file in Directory.GetFiles(legacyRoot, id + "*.marker", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
                removed++;
            }
        var testMarker = GetTestCampaignMarkerPath(campaign);
        if (File.Exists(testMarker))
        {
            File.Delete(testMarker);
            removed++;
        }
        lock (gate)
        {
            seenPaths.RemoveWhere(path => path.Contains(id, StringComparison.OrdinalIgnoreCase));
            testCampaigns.Remove(testMarker);
        }
        return removed;
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content ?? string.Empty);
            File.Move(temporaryPath, path, true);
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
