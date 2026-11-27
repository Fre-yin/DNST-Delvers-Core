using System.Text.Json;

namespace DungeonSettlersDelvers.Core;

// Amfi's mod loader "Dungeon Settlers Modded": its starter sets DSML_ROOT (its folder, with the file RELEASE
// holding its version), DSL_PROFILE (the active mod profile, Mods\active.json: {"enabled": [ids], "order": [ids]}) and DSL_STEAM
// ("1" with Steam, "0" without). The start line CORE_MOD_LOADER names the version and the enabled mods, for bug reports. Rules
// without the game; ModLoader reads the variables and files.
internal static class ModLoaderRules
{
    internal const string RootVariable = "DSML_ROOT";
    internal const string ProfileVariable = "DSL_PROFILE";
    internal const string SteamVariable = "DSL_STEAM";
    internal const string ReleaseFile = "RELEASE";
    // Amfi's Hell difficulty, the game's own step 5 above Downfall (ApocalypseDifficultyRules.Hell).
    internal const string HellId = "hell-difficulty";
    private const int MaxIds = 64;

    // The version in RELEASE: its first line, trimmed; null for an empty one.
    internal static string Release(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var line = text.Split('\n')[0].Trim();
        return line.Length == 0 ? null : line;
    }

    // The enabled mod ids of active.json, in their order; empty for a file that is no such object.
    internal static List<string> Enabled(string json)
    {
        var ids = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return ids;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.Array) return ids;
            foreach (var item in enabled.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && IsId(item.GetString()) && ids.Count < MaxIds) ids.Add(item.GetString());
        }
        catch (JsonException) { }
        return ids;
    }

    // A mod id as the loader writes them: letters, digits, '-', '_' and '.', at most 64 characters, so no path or name reaches the log.
    internal static bool IsId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
        foreach (var c in id)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')) return false;
        return true;
    }

    internal static string Line(string release, IReadOnlyList<string> enabled, string steam)
        => "CORE_MOD_LOADER name=dsmodded release=" + (release ?? "-") + " steam=" + (string.IsNullOrEmpty(steam) ? "-" : steam)
            + " enabled=" + (enabled == null || enabled.Count == 0 ? "-" : string.Join(",", enabled));
}

// The Steam guard. Amfi's mod loader sets DSL_STEAM to "0" when it started the game without
// Steam; only its old versions 0.1.10 to 0.1.15 do that, as they switch the game's ownership check off (from 0.1.16 on its starter
// does not start without Steam, a test build aside). Then Core starts none of its game functions: no game patch, no other text, item or
// effect, Core stays not ready, and Frieren, which needs a ready Core, stays off too. Only the Delvers menu's tab is patched in, its
// Core page shows the short line, and the game's dialog says it once in the title. Rules without the game.
internal static class SteamGuardRules
{
    // Only exactly "0" (spaces aside) is a start without Steam; a missing variable or any other value changes nothing.
    internal static bool IsOff(string dslSteam) => dslSteam != null && dslSteam.Trim() == "0";

    internal static string Line(string release)
        => "CORE_STEAM_GUARD steam=0 loader=" + (string.IsNullOrWhiteSpace(release) ? "-" : release) + " inactive";

    internal static string NoticeLine(bool shown, string reason)
        => "CORE_STEAM_GUARD_NOTICE shown=" + (shown ? 1 : 0) + " reason=" + reason;
}

// The game side: reads the loader's variables and files once at Core's start.
internal static class ModLoader
{
    internal static bool Present => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ModLoaderRules.RootVariable));

    // The loader's version from DSML_ROOT\RELEASE, null without the loader or the file.
    internal static string Release()
    {
        try
        {
            var root = Environment.GetEnvironmentVariable(ModLoaderRules.RootVariable);
            if (string.IsNullOrWhiteSpace(root)) return null;
            var releasePath = Path.Combine(root.Trim(), ModLoaderRules.ReleaseFile);
            return File.Exists(releasePath) ? ModLoaderRules.Release(File.ReadAllText(releasePath)) : null;
        }
        catch (Exception) { return null; }
    }

    // The enabled mods of the loader's active profile, read once; empty without the loader or the file.
    private static List<string> enabled;

    internal static IReadOnlyList<string> Enabled()
    {
        if (enabled != null) return enabled;
        try
        {
            var profile = Present ? Environment.GetEnvironmentVariable(ModLoaderRules.ProfileVariable) : null;
            enabled = !string.IsNullOrWhiteSpace(profile) && File.Exists(profile.Trim())
                ? ModLoaderRules.Enabled(File.ReadAllText(profile.Trim())) : new List<string>();
        }
        catch (Exception) { enabled = new List<string>(); }
        return enabled;
    }

    // Amfi's Hell is installed: the campaign screen goes Downfall, Apocalypse, Hell (ApocalypseDifficulty).
    internal static bool HellInstalled => Enabled().Contains(ModLoaderRules.HellId);

    internal static void LogStart()
    {
        try
        {
            if (!Present) return;
            DelversHost.Info(ModLoaderRules.Line(Release(), Enabled(), Environment.GetEnvironmentVariable(ModLoaderRules.SteamVariable)?.Trim()));
        }
        catch (Exception ex) { DelversHost.Warning("CORE_MOD_LOADER_FAILED: " + ex.Message); }
    }
}
