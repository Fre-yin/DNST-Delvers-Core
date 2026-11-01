using System.Text;
using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own units in a save (the forest bandits), like Core's items
// (CoreSaveItems): a unit key the game does not know breaks a save loaded without the mod, so the
// game's part of a save only holds the native stand-in (CoreUnitRules.StandIn, the goblin brawler).
// Core remembers by GUID which unit entities are really its own
// (CoreSaveData.OwnedUnits) and turns exactly those back before the game reads the save. Only keys
// Core knows (CoreUnitRules.TryGet) are changed in either direction. No lambdas (see CoreSaveItems).
internal static class CoreSaveUnits
{
    private static readonly Regex Own = new(
        "\"Guid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"(\\s*,\\s*\"Key\"\\s*:\\s*\")(" + Regex.Escape(CoreUnitRules.Prefix) + "[A-Za-z0-9_]+)\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex Native = new(
        "\"Guid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"(\\s*,\\s*\"Key\"\\s*:\\s*\")(" + Regex.Escape(CoreUnitRules.StandIn) + ")\"",
        RegexOptions.CultureInvariant);

    // Writing: own unit entities become the stand-in; owned receives them (guid -> own key). The same
    // instance when there are none.
    internal static string ToNative(string json, Dictionary<Guid, string> owned)
    {
        if (string.IsNullOrEmpty(json) || json.IndexOf(CoreUnitRules.Prefix, StringComparison.Ordinal) < 0) return json;
        var text = new StringBuilder(json.Length);
        var copied = 0;
        for (var match = Own.Match(json); match.Success; match = match.NextMatch())
        {
            var key = match.Groups[3].Value;
            if (!CoreUnitRules.TryGet(key, out _) || !Guid.TryParse(match.Groups[1].Value, out var guid)) continue;
            owned[guid] = key;
            text.Append(json, copied, match.Groups[3].Index - copied).Append(CoreUnitRules.StandIn);
            copied = match.Groups[3].Index + match.Groups[3].Length;
        }
        if (copied == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // Reading: the recorded entities get their own key back, in one pass; every other stand-in stays.
    internal static string FromNative(string json, IReadOnlyDictionary<Guid, string> owned, out int restored)
    {
        restored = 0;
        if (string.IsNullOrEmpty(json) || owned == null || owned.Count == 0) return json;
        var text = new StringBuilder(json.Length + 64);
        var copied = 0;
        for (var match = Native.Match(json); match.Success; match = match.NextMatch())
        {
            if (!Guid.TryParse(match.Groups[1].Value, out var guid) || !owned.TryGetValue(guid, out var own)
                || !CoreUnitRules.TryGet(own, out _)) continue;
            text.Append(json, copied, match.Groups[3].Index - copied).Append(own);
            copied = match.Groups[3].Index + match.Groups[3].Length;
            restored++;
        }
        if (restored == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }
}
