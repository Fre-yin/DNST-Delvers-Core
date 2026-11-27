using DungeonSettlersDelvers.Core;
using static TestKit;

// Core's settings per game copy.
internal static class SettingsCopySection
{
    internal static void Run()
    {
        // Core's settings per game copy, the shared file's values taken once and never touched.
        var copyRoot = Path.Combine(Path.GetTempPath(), "delvers-settings-copy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gameA = Path.Combine(copyRoot, "Games", "Dungeon Settlers");
            var gameB = Path.Combine(copyRoot, "Sandbox", "Game");
            var idA = CoreSettingsCopyRules.CopyId(gameA);
            Check(idA != null && idA.Length == 8 && idA.All(c => Uri.IsHexDigit(c) && !char.IsLower(c))
                    && CoreSettingsCopyRules.CopyId(gameA.ToUpperInvariant()) == idA
                    && CoreSettingsCopyRules.CopyId(gameA + "\\") == idA
                    && CoreSettingsCopyRules.CopyId(gameA.Replace('\\', '/')) == idA
                    && CoreSettingsCopyRules.CopyId(gameB) != idA
                    && CoreSettingsCopyRules.CopyId(null) == null && CoreSettingsCopyRules.CopyId(" ") == null
                    && CoreSettingsCopyRules.CopyId(@"C:\Games\Dungeon Settlers")
                        == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(@"c:\games\dungeon settlers")), 0, 4)
                    && CoreSettingsCopyRules.ApiVersion == "1.10.0" && new Version(CoreSettingsCopyRules.ApiVersion) > new Version(CampaignGuidRules.ApiVersion),
                "settings copy id: the game folder, full path, backslashes, no trailing one, lower case, SHA256, 8 hex digits upper case");
            var dataRoot = Path.Combine(copyRoot, "LocalLow");
            var sharedSettings = CoreSettingsCopyRules.SharedPath(dataRoot);
            Check(CoreSettingsCopyRules.CopyPath(dataRoot, gameA) == Path.Combine(dataRoot, "DungeonSettlersDelvers", "Core", "copies", idA, "settings.json")
                    && CoreSettingsCopyRules.CopyPath(dataRoot, null) == sharedSettings
                    && sharedSettings == Path.Combine(dataRoot, "DungeonSettlersDelvers", "Core", "settings.json"),
                "settings copy path: copies\\<id>\\settings.json next to the shared file, the shared file without a game folder");
            var fresh = CoreSettingsCopyRules.Prepare(dataRoot, gameA);
            Check(!fresh.Migrated && fresh.Id == idA && !File.Exists(fresh.Path) && !File.Exists(sharedSettings)
                    && File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(fresh.Path), "copy.txt"))
                        .SequenceEqual(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(gameA) + "\r\n")),
                "settings copy without a shared file: defaults, no file made, copy.txt is the game folder in one line, UTF-8 without BOM");
            Directory.CreateDirectory(Path.GetDirectoryName(sharedSettings));
            File.WriteAllText(sharedSettings, "{\"founders.liana.enabled\": false, \"future.setting\": 3}");
            var sharedBefore = File.ReadAllText(sharedSettings);
            var first = CoreSettingsCopyRules.Prepare(dataRoot, gameB);
            new CoreSettingsFile(first.Path).Set("founders.lowell.enabled", false);
            var again = CoreSettingsCopyRules.Prepare(dataRoot, gameB);
            Check(first.Migrated && !again.Migrated
                    && !new CoreSettingsFile(first.Path).Get("founders.liana.enabled", true)
                    && !new CoreSettingsFile(first.Path).Get("founders.lowell.enabled", true)
                    && File.ReadAllText(first.Path).Contains("future.setting", StringComparison.Ordinal)
                    && File.ReadAllText(sharedSettings) == sharedBefore,
                "settings copy: the shared file's values once, later writes stay in the copy, the shared file unchanged");
            var other = CoreSettingsCopyRules.Prepare(dataRoot, gameA);
            Check(other.Migrated && new CoreSettingsFile(other.Path).Get("founders.lowell.enabled", true)
                    && !new CoreSettingsFile(other.Path).Get("founders.liana.enabled", true),
                "settings copy: a switch set in one copy does not reach another; each starts from the shared values");
            // Amfi's mod loader: its own data folder without a file takes the normal game's values once, read only.
            var gameRoot = Path.Combine(copyRoot, "NormalGame");
            var loaderRoot = Path.Combine(copyRoot, "LoaderData");
            var gameSaves = Path.Combine(gameRoot, "Saves");
            Directory.CreateDirectory(gameSaves);
            Directory.CreateDirectory(CoreSettingsCopyRules.CoreFolder(gameRoot));
            File.WriteAllText(CoreSettingsCopyRules.SharedPath(gameRoot), "{\"apocalypse.offered\": true}");
            var outside = CoreSettingsCopyRules.OutsideRoot(gameSaves + "\\", loaderRoot);
            var gameFiles = Directory.GetFiles(gameRoot, "*", SearchOption.AllDirectories).Select(file => file + "|" + File.ReadAllText(file)).OrderBy(x => x).ToList();
            var loaderFirst = CoreSettingsCopyRules.Prepare(loaderRoot, gameA, outside);
            var loaderAgain = CoreSettingsCopyRules.Prepare(loaderRoot, gameA, outside);
            var gameFilesAfter = Directory.GetFiles(gameRoot, "*", SearchOption.AllDirectories).Select(file => file + "|" + File.ReadAllText(file)).OrderBy(x => x).ToList();
            Check(outside == gameRoot && loaderFirst.Source == "game-shared" && loaderAgain.Source == null
                    && new CoreSettingsFile(loaderFirst.Path).Get("apocalypse.offered", false)
                    && loaderFirst.Path.StartsWith(loaderRoot, StringComparison.Ordinal) && gameFiles.SequenceEqual(gameFilesAfter)
                    && CoreSettingsCopyRules.OutsideRoot(null, loaderRoot) == null
                    && CoreSettingsCopyRules.OutsideRoot(Path.Combine(loaderRoot, "Saves"), loaderRoot) == null,
                "settings copy in a mod loader: the normal game's shared values once, read only, nothing written there; none without the variable");
            File.Delete(CoreSettingsCopyRules.SharedPath(gameRoot));
            var copyOutside = CoreSettingsCopyRules.CopyPath(gameRoot, gameB);
            Directory.CreateDirectory(Path.GetDirectoryName(copyOutside));
            File.WriteAllText(copyOutside, "{\"hints.enabled\": false}");
            var fromCopy = CoreSettingsCopyRules.Prepare(Path.Combine(copyRoot, "LoaderData2"), gameB, outside);
            Check(fromCopy.Source == "game-copy" && !new CoreSettingsFile(fromCopy.Path).Get("hints.enabled", true)
                    && CoreSettingsCopyRules.Line("AB12CD34", "G", true, "game-shared") == "CORE_SETTINGS_COPY id=AB12CD34 game=G migrated=1 from=game-shared",
                "settings copy in a mod loader: without the normal game's shared file its copy for the same game folder, the line names the origin");
            Check(CoreSettingsCopyRules.Line("AB12CD34", "G", true) == "CORE_SETTINGS_COPY id=AB12CD34 game=G migrated=1"
                    && CoreSettingsCopyRules.Line(null, null, false) == "CORE_SETTINGS_COPY id=- game=- migrated=0",
                "settings copy log line");
        }
        finally
        {
            if (Directory.Exists(copyRoot)) Directory.Delete(copyRoot, true);
        }
    }
}
