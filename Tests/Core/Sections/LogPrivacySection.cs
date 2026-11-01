using DungeonSettlersDelvers.Core;
using static TestKit;

// Log privacy: no user folder reaches a log line.
internal static class LogPrivacySection
{
    internal static void Run()
    {
        // Log privacy: players share logs, so no user folder may reach one (review, F6).
        // Sample paths are joined at run time: the public export rejects any literal local path.
        string WinPath(params string[] parts) => string.Join('\\', parts);
        var maskedWindows = DelversLogPrivacy.Mask("CAMPAIGN_PRESENCE_READ_FAILED: "
            + WinPath("C:", "Users", "Max Mustermann", "AppData", "LocalLow", "CanOpener", "Dungeon Settlers", "x.json"));
        Check(!maskedWindows.Contains("Mustermann", StringComparison.OrdinalIgnoreCase)
                && maskedWindows.Contains(WinPath("<user>", "AppData", "LocalLow"), StringComparison.Ordinal),
            "a Windows user folder with spaces is masked and the rest of the path stays readable");
        Check(!DelversLogPrivacy.Mask("at C:/Users/Ben/AppData/LocalLow").Contains("Ben", StringComparison.Ordinal)
                && !DelversLogPrivacy.Mask("open /home/tim/.local/share/x").Contains("tim", StringComparison.Ordinal)
                && !DelversLogPrivacy.Mask(WinPath("Z:", "home", "deck", ".steam", "x")).Contains("deck", StringComparison.Ordinal)
                && !DelversLogPrivacy.Mask(WinPath("C:", "users", "steamuser", "AppData")).Contains("steamuser", StringComparison.Ordinal),
            "forward slashes, Linux and Proton user folders are masked");
        Check(DelversLogPrivacy.Mask("Benchmark time for Tim's build") == "Benchmark time for Tim's build"
                && DelversLogPrivacy.Mask(@"H:\Steam\steamapps\common\Dungeon Settlers\Mods") == @"H:\Steam\steamapps\common\Dungeon Settlers\Mods"
                && DelversLogPrivacy.Mask(null) == null && DelversLogPrivacy.Mask("") == "",
            "text without a user folder stays untouched, short names inside words are not cut");
        var realProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var realName = Environment.UserName;
        Check(string.IsNullOrEmpty(realProfile) || string.IsNullOrEmpty(realName)
                || !DelversLogPrivacy.Mask("failed at " + Path.Combine(realProfile, "AppData", "LocalLow", "x.json"))
                    .Contains(realName, StringComparison.OrdinalIgnoreCase),
            "the user name of this machine does not slip through the mask");
        var privacyHost = new TestDelversHost("privacy-core-package");
        DelversHost.Bind(privacyHost);
        try
        {
            var secretPath = WinPath("C:", "Users", "Secret Person", "AppData", "x");
            DelversHost.Error("E: " + secretPath);
            DelversHost.Warning("W: " + secretPath);
            DelversHost.Info("I: " + secretPath);
            Check(privacyHost.AllMessages.Count == 3
                    && privacyHost.AllMessages.All(line => !line.Contains("Secret", StringComparison.Ordinal)),
                "DelversHost masks errors, warnings and info centrally");
        }
        finally
        {
            DelversHost.Unbind(privacyHost);
        }
    }
}
