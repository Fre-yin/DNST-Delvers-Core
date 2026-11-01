using DungeonSettlersDelvers.Core;
using static TestKit;

// Core settings file: founder switches in the Delvers menu.
internal static class CoreSettingsSection
{
    internal static void Run()
    {
        // Core settings file: founder switches in the Delvers menu.
        var settingsRoot = Path.Combine(Path.GetTempPath(), "delvers-settings-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settingsPath = Path.Combine(settingsRoot, "Core", "settings.json");
            var freshSettings = new CoreSettingsFile(settingsPath);
            Check(freshSettings.Get("founders.lowell.enabled", true) && !freshSettings.Get("other.switch", false)
                    && !File.Exists(settingsPath),
                "a missing settings file yields the defaults and reading does not create it");
            freshSettings.Set("founders.liana.enabled", false);
            var rereadSettings = new CoreSettingsFile(settingsPath);
            Check(!rereadSettings.Get("founders.liana.enabled", true) && rereadSettings.Get("founders.lowell.enabled", true),
                "a switched-off founder is stored and read back, the others keep their default");
            File.WriteAllText(settingsPath, "{\"future.setting\": 3, \"founders.kragas.enabled\": false}");
            new CoreSettingsFile(settingsPath).Set("founders.lowell.enabled", false);
            var writtenSettings = File.ReadAllText(settingsPath);
            Check(writtenSettings.Contains("future.setting", StringComparison.Ordinal)
                    && !new CoreSettingsFile(settingsPath).Get("founders.kragas.enabled", true)
                    && !new CoreSettingsFile(settingsPath).Get("founders.lowell.enabled", true),
                "unknown keys of a newer Core survive a write");
            File.WriteAllText(settingsPath, "{\"founders.lowell.enabled\": \"no\"}");
            var typedSettings = new CoreSettingsFile(settingsPath);
            Check(typedSettings.Get("founders.lowell.enabled", true) && typedSettings.Get("founders.lowell.enabled", true)
                    && typedSettings.TakeTypeWarning() == "CORE_SETTINGS_TYPE key=founders.lowell.enabled value=\"no\" fallback=1"
                    && typedSettings.TakeTypeWarning() == null,
                "a value of the wrong type falls back to the default and is named once");
            // corpse.burial.warning=0 left the warning on; switches take 0/1 and their texts too.
            File.WriteAllText(settingsPath, "{\"a\": 0, \"b\": 1, \"c\": \"false\", \"d\": \"TRUE\", \"e\": \"0\", \"f\": 2, \"g\": false}");
            var numericSettings = new CoreSettingsFile(settingsPath);
            Check(!numericSettings.Get("a", true) && numericSettings.Get("b", false) && !numericSettings.Get("c", true) && numericSettings.Get("d", false)
                    && !numericSettings.Get("e", true) && numericSettings.Get("f", true) && !numericSettings.Get("g", true)
                    && numericSettings.TakeTypeWarning() == "CORE_SETTINGS_TYPE key=f value=2 fallback=1" && numericSettings.TakeTypeWarning() == null,
                "a switch takes true/false, 0/1 and their texts; another number falls back with a CORE_SETTINGS_TYPE line");
            File.WriteAllText(settingsPath, "{ broken");
            var brokenSettings = new CoreSettingsFile(settingsPath);
            Check(brokenSettings.Get("founders.lowell.enabled", true) && brokenSettings.LoadError != null,
                "an unreadable file yields the defaults and names the reason");
            brokenSettings.Set("founders.liana.enabled", false);
            var settingsDirectory = Path.GetDirectoryName(settingsPath);
            Check(Directory.GetFiles(settingsDirectory, "settings.json.broken-*").Length == 1
                    && !new CoreSettingsFile(settingsPath).Get("founders.liana.enabled", true)
                    && Directory.GetFiles(settingsDirectory, "*.tmp").Length == 0,
                "an unreadable file is set aside before the next write, and no temporary file remains");
        }
        finally
        {
            if (Directory.Exists(settingsRoot)) Directory.Delete(settingsRoot, true);
        }
    }
}
