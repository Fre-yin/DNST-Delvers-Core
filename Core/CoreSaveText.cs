using System.Text;

namespace DungeonSettlersDelvers.Core;

// Small text tools for the game's save JSON, shared by CoreSaveResearch and CoreSaveTasks. Core changes the text the
// game writes and reads instead of parsing it, so everything
// else in a save stays byte for byte as the game wrote it. Pure, so the offline tests cover it. No
// lambdas (see CoreSaveItems).
internal static class CoreSaveText
{
    // The text without the given spans (start inclusive, end exclusive; sorted, not overlapping),
    // each with one neighbouring comma: the one before, or else the one after.
    internal static string Remove(string json, List<(int Start, int End)> spans)
    {
        if (spans == null || spans.Count == 0) return json;
        var text = new StringBuilder(json.Length);
        var copied = 0;
        foreach (var span in spans)
        {
            var start = span.Start;
            var end = span.End;
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
        }
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // The end (exclusive) of the object or array that opens at index open, strings respected; -1
    // when it does not close.
    internal static int Close(string json, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < json.Length; i++)
        {
            var c = json[i];
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
                if (depth == 0) return i + 1;
            }
        }
        return -1;
    }

    // The '{' of the object that contains index at; -1 when there is none. Meant for flat game data
    // whose strings hold no braces (keys, GUIDs, numbers).
    internal static int OpenBefore(string json, int at)
    {
        var depth = 0;
        for (var i = at - 1; i >= 0; i--)
        {
            var c = json[i];
            if (c == '}' || c == ']') depth++;
            else if (c == '{' || c == '[')
            {
                if (depth == 0) return c == '{' ? i : -1;
                depth--;
            }
        }
        return -1;
    }

    // The elements of the object or array that opens at index open, as spans (start inclusive, end
    // exclusive): nested objects and arrays, strings and plain values. Empty when it does not close.
    internal static List<(int Start, int End)> Elements(string json, int open)
    {
        var elements = new List<(int Start, int End)>();
        var close = Close(json, open);
        if (close < 0) return elements;
        var last = close - 1;
        var i = open + 1;
        while (i < last)
        {
            while (i < last && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
            if (i >= last) break;
            int end;
            switch (json[i])
            {
                case '{':
                case '[':
                    end = Close(json, i);
                    break;
                case '"':
                    end = StringEnd(json, i);
                    break;
                default:
                    end = i;
                    while (end < last && json[end] != ',' && !char.IsWhiteSpace(json[end])) end++;
                    break;
            }
            if (end < 0) break;
            elements.Add((i, end));
            i = end;
        }
        return elements;
    }

    // The text with item as element number index of the container that opens at index open; after
    // the last element when index is out of range.
    internal static string InsertAt(string json, int open, int index, string item)
    {
        var elements = Elements(json, open);
        if (elements.Count == 0) return InsertFirst(json, open, item);
        if (index >= 0 && index < elements.Count)
        {
            var start = elements[index].Start;
            return json.Substring(0, start) + item + ", " + json.Substring(start);
        }
        var end = elements[elements.Count - 1].End;
        return json.Substring(0, end) + ", " + item + json.Substring(end);
    }

    // The end (exclusive) of the string that starts with the quote at index quote; -1 when it does
    // not end.
    private static int StringEnd(string json, int quote)
    {
        for (var i = quote + 1; i < json.Length; i++)
        {
            if (json[i] == '\\') i++;
            else if (json[i] == '"') return i + 1;
        }
        return -1;
    }

    // The text with item put first into the object or array that opens at index open, with a comma
    // when the container already holds something.
    internal static string InsertFirst(string json, int open, string item)
    {
        var next = open + 1;
        while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
        var empty = next < json.Length && (json[next] == '}' || json[next] == ']');
        return json.Substring(0, open + 1) + item + (empty ? "" : ", ") + json.Substring(open + 1);
    }
}
