using System.Text;
using System.Text.Json;

namespace DungeonSettlersDelvers.Core;

// Core's own data inside a campaign save: one top-level property "DelversCore" at the end of the
// save's JSON (research).
// The game reads its 24 named properties and never touches an unknown one; its header
// reader stops at the header, long before the end (TryReadSaveHeader). Without Core the block is ignored and lost on the
// next save. Pure text work, so the offline tests cover it; never re-parses the whole save.
internal static class CoreSaveBlock
{
    internal const string PropertyName = "DelversCore";
    private const string Marker = "\"" + PropertyName + "\":";

    // The block's JSON object, or null when the save has none or it is damaged. A damaged block
    // is ignored: the save itself stays as the game wrote it.
    internal static string Extract(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var at = json.LastIndexOf(Marker, StringComparison.Ordinal);
        if (at < 0) return null;
        var start = SkipWhitespace(json, at + Marker.Length);
        if (start >= json.Length || json[start] != '{') return null;
        var end = ObjectEnd(json, start);
        if (end < 0) return null;
        // Only the save's closing brace may follow: the block is the last property.
        var rest = SkipWhitespace(json, end);
        if (rest >= json.Length || json[rest] != '}' || SkipWhitespace(json, rest + 1) != json.Length) return null;
        return json.Substring(start, end - start);
    }

    // The save text with the block as its last property; an existing block is replaced. Returns
    // the text unchanged when it does not end like a JSON object or the block is not one.
    internal static string Append(string json, string block)
    {
        if (string.IsNullOrEmpty(json) || !IsObject(block)) return json;
        var withoutOld = Remove(json);
        var close = withoutOld.Length - 1;
        while (close >= 0 && char.IsWhiteSpace(withoutOld[close])) close--;
        if (close < 0 || withoutOld[close] != '}') return json;
        var before = close - 1;
        while (before >= 0 && char.IsWhiteSpace(withoutOld[before])) before--;
        if (before < 0) return json;
        var empty = withoutOld[before] == '{';
        var text = new StringBuilder(withoutOld.Length + block.Length + 32);
        text.Append(withoutOld, 0, before + 1);
        text.Append(empty ? "\n  " : ",\n  ").Append(Marker).Append(' ').Append(block).Append("\n}");
        text.Append(withoutOld, close + 1, withoutOld.Length - close - 1);
        return text.ToString();
    }

    // The campaign a save text belongs to (the game's "CampaignGuid", read from the end where the clan data stands), or
    // Guid.Empty when the text names none.
    internal static Guid CampaignOf(string json)
    {
        if (string.IsNullOrEmpty(json)) return Guid.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(json, "\"CampaignGuid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"",
            System.Text.RegularExpressions.RegexOptions.RightToLeft);
        return match.Success && Guid.TryParse(match.Groups[1].Value, out var guid) ? guid : Guid.Empty;
    }

    // (version 4): a block applies its lists to a save text only when both name the same campaign; a text or a
    // block without a campaign keeps the old way (offline texts, blocks older than the campaign field).
    internal static bool BlockApplies(Guid blockCampaign, Guid textCampaign)
        => blockCampaign == Guid.Empty || textCampaign == Guid.Empty || blockCampaign == textCampaign;

    // The save text without Core's block; every Append starts from it.
    internal static string Remove(string json)
    {
        if (Extract(json) == null) return json;
        var at = json.LastIndexOf(Marker, StringComparison.Ordinal);
        var cut = at - 1;
        while (cut >= 0 && char.IsWhiteSpace(json[cut])) cut--;
        if (cut >= 0 && json[cut] == ',') cut--;
        var start = SkipWhitespace(json, at + Marker.Length);
        var end = ObjectEnd(json, start);
        var close = SkipWhitespace(json, end);
        var text = new StringBuilder(json.Length);
        text.Append(json, 0, cut + 1).Append('\n');
        text.Append(json, close, json.Length - close);
        return text.ToString();
    }

    internal static bool IsObject(string block)
    {
        if (string.IsNullOrWhiteSpace(block)) return false;
        try
        {
            using var document = JsonDocument.Parse(block);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    // Index just after the object that starts at 'start', or -1. Strings and escapes count.
    private static int ObjectEnd(string text, int start)
    {
        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0) return c == '}' ? i + 1 : -1;
                if (depth < 0) return -1;
            }
        }
        return -1;
    }
}
