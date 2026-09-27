namespace DungeonSettlersDelvers.Core;

internal static class LoadIntegrityDiagnostics
{
    internal const string FailureNotice = "Delvers Core detected mod data or an incompatible mod version during this load. "
        + "Saving is blocked to protect this save. "
        + "This is not an error in the native Dungeon Settlers patch. "
        + "Report the involved mod to its author, not to the game developers.";

    internal static string BuildFailureMessage(string source)
        => FailureNotice + " Detection source: " + Normalize(source) + ".";

    internal static string BuildSaveBlockedMessage(string source)
        => FailureNotice + " Restart the game before saving again. Detection source: "
            + Normalize(source) + ".";

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "unknown load stage"
            : string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
}
