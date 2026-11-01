using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Which unique units a campaign has had (CampaignPresence), kept in Core's block of the save (the save
// markers move into the save file, small). A fixed table of stable ids gives each one a bit; the block holds the bits as hex
// ("pr"), a version ("pv") and the ids outside the table in a short list ("px"). New ids only ever go at the end of the table,
// so a bit keeps its meaning in every save.
internal static class CampaignPresenceRules
{
    internal const int Version = 1;

    internal static readonly string[] Bits = { "frieren", "lowell", "liana", "kragas" };

    internal static int BitOf(string id) => id == null ? -1 : Array.IndexOf(Bits, id);

    internal static string MaskText(int mask) => mask.ToString("x2", CultureInfo.InvariantCulture);

    internal static int ParseMask(string text)
        => int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var mask) && mask >= 0 ? mask : 0;

    // The name an older Core gave the id's marker directly under FrierenPortrait\recruited (CampaignPresence.LegacyMarkerName):
    // Frieren without a suffix, the founders with their name.
    internal static string LegacySuffix(string id) => id switch
    {
        "lowell" => "-Lowell",
        "liana" => "-Liana",
        "kragas" => "-Kragas",
        _ => null
    };

    // A marker file of an older Core counts only when the loaded save was written after it: then the save has seen the unit
    // join, even if it died since. A marker newer than the save was written before a save that never came (a crash between
    // recruiting and saving) and does not count, so the unit may appear again.
    internal static bool Adopt(DateTime saveWrittenUtc, DateTime markerWrittenUtc) => saveWrittenUtc >= markerWrittenUtc;
}
