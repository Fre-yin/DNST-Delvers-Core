using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Players share their logs. Exceptions carry full paths through the Windows user folder
// (Users, then the account name, then AppData and LocalLow), so every Core log line passes
// through here and that folder is replaced by a placeholder. Pure function without game
// types, tested offline. No sample path is written out here: the public export rejects them.
public static class DelversLogPrivacy
{
    // Only Core uses the placeholder itself; packs call Mask.
    internal const string Placeholder = "<user>";

    // A drive letter is optional, both slash kinds count, and the folder name may contain spaces.
    // Covers the Windows user folder, Proton's steamuser and home folders, and Linux home folders.
    private static readonly Regex UserFolder = new(
        @"(?:[A-Za-z]:)?[\\/](?:users|home)[\\/][^\\/\r\n\t:*?""<>|]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Mask(string message)
        => string.IsNullOrEmpty(message) ? message : UserFolder.Replace(message, Placeholder);
}
