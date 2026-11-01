using System.Text;
using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own effects never reach the game's part of a save. A save loaded
// without the mod that owns an effect loses the unit's whole effect list, traits included
// (Frieren). So every affecter holder whose key starts with Prefix is taken out of the
// text the game writes; Core keeps what it needs in its own data (CoreSaveData) and puts the
// effects back after loading. The game rebuilds effect values on load, so nothing else changes.
// Holders look like {"Key": "...", "Stack": 1, "Duration": 0.0, "InitialDuration": 0.0}.
internal static class CoreSaveEffects
{
    // One underscore only: the game picks a tooltip type from the second-to-last part of a key
    // split at "_", so AFFECTER_DelversCore_X showed a plain text tooltip.
    internal const string Prefix = "AFFECTER_DelversCore";

    private static readonly Regex Holder = new(
        "\\{\\s*\"Key\"\\s*:\\s*\"" + Regex.Escape(Prefix) + "[^\"]*\"[^{}\\[\\]]*\\}",
        RegexOptions.CultureInvariant);

    // The text without Core's holders; the same instance when there are none.
    internal static string Strip(string json, out int removed)
    {
        removed = 0;
        if (string.IsNullOrEmpty(json) || json.IndexOf(Prefix, StringComparison.Ordinal) < 0) return json;
        var matches = Holder.Matches(json);
        if (matches.Count == 0) return json;
        var text = new StringBuilder(json.Length);
        var copied = 0;
        foreach (Match match in matches)
        {
            var start = match.Index;
            var end = match.Index + match.Length;
            // Take one neighbouring comma with it: the one before, or else the one after.
            var before = start - 1;
            while (before >= copied && char.IsWhiteSpace(json[before])) before--;
            if (before >= copied && json[before] == ',') start = before;
            else
            {
                var after = end;
                while (after < json.Length && char.IsWhiteSpace(json[after])) after++;
                if (after < json.Length && json[after] == ',') end = after + 1;
            }
            text.Append(json, copied, start - copied);
            copied = end;
            removed++;
        }
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }
}
