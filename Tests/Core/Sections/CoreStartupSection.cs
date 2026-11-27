using DungeonSettlersDelvers.Core;
using static TestKit;

// The start analysis line.
internal static class CoreStartupSection
{
    internal static void Run()
    {
        // Start analysis: Core only measures the seconds since the process started.
        Check(CoreStartup.Line("ready", 12.345) == "CORE_STARTUP at=ready sinceProcess=12.3s"
                && CoreStartup.Line("init", double.NaN) == "CORE_STARTUP at=init sinceProcess=-"
                && CoreStartup.Line("init", -1) == "CORE_STARTUP at=init sinceProcess=-",
            "startup line: seconds since the process started, one decimal, '-' when unknown");
        // Amfi's mod loader: its version and the enabled mods in one start line, no path or name.
        Check(ModLoaderRules.Release("0.1.31\r\n") == "0.1.31" && ModLoaderRules.Release("  ") == null
                && ModLoaderRules.Enabled("{\"enabled\":[\"delvers-core\",\"extended-hotbar\",\"C:\\\\Users\\\\x\",5],\"order\":[\"a\"]}")
                    .SequenceEqual(new[] { "delvers-core", "extended-hotbar" })
                && ModLoaderRules.Enabled("not json").Count == 0 && ModLoaderRules.Enabled("[]").Count == 0
                && ModLoaderRules.Line("0.1.31", new[] { "delvers-core", "multiplayer" }, "1")
                    == "CORE_MOD_LOADER name=dsmodded release=0.1.31 steam=1 enabled=delvers-core,multiplayer"
                && ModLoaderRules.Line(null, null, null) == "CORE_MOD_LOADER name=dsmodded release=- steam=- enabled=-",
            "mod loader line: the loader's release, Steam and the enabled mod ids only, anything else in the profile left out");
        // The Steam guard (the author's wording): only DSL_STEAM exactly "0" keeps Core off.
        Check(SteamGuardRules.IsOff("0") && SteamGuardRules.IsOff(" 0 ") && !SteamGuardRules.IsOff(null) && !SteamGuardRules.IsOff("1")
                && !SteamGuardRules.IsOff("") && !SteamGuardRules.IsOff("00") && !SteamGuardRules.IsOff("false")
                && SteamGuardRules.Line("0.1.14") == "CORE_STEAM_GUARD steam=0 loader=0.1.14 inactive"
                && SteamGuardRules.Line(null) == "CORE_STEAM_GUARD steam=0 loader=- inactive"
                && SteamGuardRules.NoticeLine(true, "title") == "CORE_STEAM_GUARD_NOTICE shown=1 reason=title",
            "steam guard: only a start without Steam keeps Core off, a missing or other value changes nothing; its lines");
        Check(CoreTextRows.Registered.Count(row => row.Key == SteamGuardText.NoticeKey) == 1
                && CoreTextRows.Sources.Count(row => row.Text == SteamGuardText.MenuLine) == 1
                && SteamGuardText.Notice.English.StartsWith("Delvers is off. This game was started without Steam", StringComparison.Ordinal)
                && SteamGuardText.MenuLine.English == "Delvers is off: the game was started without Steam.",
            "steam guard: the notice is a registered row, the menu line a checked source, both in Fre-yin's wording");
    }
}
