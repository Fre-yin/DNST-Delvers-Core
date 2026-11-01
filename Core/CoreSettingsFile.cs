using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonSettlersDelvers.Core;

// Core's own settings, one JSON object of switches. Pure file logic without game types, tested
// offline. Unknown keys are kept, so an older Core never drops a newer Core's setting. A file
// that cannot be read yields the defaults and is set aside before the next write, never lost.
internal sealed class CoreSettingsFile
{
    private readonly object gate = new();
    private readonly string path;
    private JsonObject values;
    private bool brokenOnDisk;

    internal CoreSettingsFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", nameof(path));
        this.path = Path.GetFullPath(path);
    }

    internal string FilePath => path;

    // Set when the file existed but could not be read; the caller reports it once.
    internal string LoadError { get; private set; }

    // (corpse.burial.warning=0 left the warning on, the number fell back silently): a switch
    // takes true/false, the numbers 0 and 1, and the texts "true", "false", "0" and "1" in any case. Any other value keeps the
    // fallback and is named once per key (TakeTypeWarning).
    internal bool Get(string key, bool fallback)
    {
        lock (gate)
        {
            EnsureLoaded();
            JsonNode node;
            try { node = values[key]; }
            catch { return fallback; }
            if (node == null) return fallback;
            if (ParseBool(node) is bool result) return result;
            if (typeWarned.Add(key)) pendingTypeWarnings.Enqueue(TypeLine(key, node.ToJsonString(), fallback));
            return fallback;
        }
    }

    private readonly HashSet<string> typeWarned = new(StringComparer.Ordinal);
    private readonly Queue<string> pendingTypeWarnings = new();

    // The next CORE_SETTINGS_TYPE line not reported yet, or null.
    internal string TakeTypeWarning()
    {
        lock (gate) return pendingTypeWarnings.Count > 0 ? pendingTypeWarnings.Dequeue() : null;
    }

    internal static bool? ParseBool(JsonNode node)
    {
        if (node is not JsonValue value) return null;
        try
        {
            if (value.TryGetValue<bool>(out var flag)) return flag;
            if (value.TryGetValue<double>(out var number)) return number == 0 ? false : number == 1 ? true : null;
            if (value.TryGetValue<string>(out var text))
            {
                switch (text?.Trim().ToLowerInvariant())
                {
                    case "true": case "1": return true;
                    case "false": case "0": return false;
                }
            }
        }
        catch { /* not a value of a known kind */ }
        return null;
    }

    internal static string TypeLine(string key, string value, bool fallback)
        => "CORE_SETTINGS_TYPE key=" + key + " value=" + (value ?? "-") + " fallback=" + (fallback ? 1 : 0);

    // A number from the file, null when it is missing, not a number or not finite; the caller
    // keeps its own default and range.
    internal double? GetNumber(string key)
    {
        lock (gate)
        {
            EnsureLoaded();
            try { return values[key] is JsonValue value && value.TryGetValue<double>(out var result) && double.IsFinite(result) ? result : null; }
            catch { return null; }
        }
    }

    // Throws on an IO failure; the value is then still active in memory for this session.
    internal void Set(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A setting key is required.", nameof(key));
        lock (gate)
        {
            EnsureLoaded();
            values[key] = value;
            Write();
        }
    }

    private void EnsureLoaded()
    {
        if (values != null) return;
        values = new JsonObject();
        if (!File.Exists(path)) return;
        try
        {
            values = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject
                ?? throw new InvalidDataException("The settings file is not a JSON object.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            values = new JsonObject();
            brokenOnDisk = true;
            LoadError = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void Write()
    {
        var directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        if (brokenOnDisk && File.Exists(path))
        {
            // Keep the unreadable file for inspection instead of overwriting it.
            File.Move(path, path + ".broken-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
            brokenOnDisk = false;
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, values.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { /* The original file is untouched either way. */ }
        }
    }
}
