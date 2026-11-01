using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own research in a save (First Aid). A research key the game does not
// know breaks a save loaded without the mod, so the game's part of a save never holds one: its entry
// in ResearchDatas ("RESEARCH_DelversCore...": {...}) and its place in ResearchKeyQueue are cut out
// on writing and kept in Core's own data (CoreSaveData.Research). On reading they go back: the data
// first into ResearchDatas, the key at its old place in the queue. Pure, so the offline tests cover
// it. No lambdas (see CoreSaveItems).
internal static class CoreSaveResearch
{
    // One own research as it was cut out: its data object (null when it had none) and its place in
    // the queue (-1 when it was not queued).
    internal readonly struct Entry
    {
        internal string Key { get; }
        internal string Body { get; }
        internal int QueueIndex { get; }

        internal Entry(string key, string body, int queueIndex)
        {
            Key = key;
            Body = body;
            QueueIndex = queueIndex;
        }
    }

    // A key in ResearchKeyQueue: its place and its span in the text (with quotes).
    private readonly struct QueueEntry
    {
        internal string Key { get; }
        internal int Index { get; }
        internal int Start { get; }
        internal int End { get; }

        internal QueueEntry(string key, int index, int start, int end)
        {
            Key = key;
            Index = index;
            Start = start;
            End = end;
        }
    }

    // One underscore only, like every game key (see CoreSaveEffects.Prefix).
    internal const string Prefix = "RESEARCH_DelversCore";

    private static readonly Regex Data = new(
        "\"(" + Regex.Escape(Prefix) + "[A-Za-z0-9_]*)\"\\s*:\\s*(\\{[^{}\\[\\]]*\\})",
        RegexOptions.CultureInvariant);

    private static readonly Regex Datas = new("\"ResearchDatas\"\\s*:\\s*\\{", RegexOptions.CultureInvariant);

    private static readonly Regex Queue = new("\"ResearchKeyQueue\"\\s*:\\s*\\[", RegexOptions.CultureInvariant);

    private static readonly Regex QueueItem = new("\"([^\"]*)\"", RegexOptions.CultureInvariant);

    // Writing: the text without Core's research; what was cut out goes into kept, in the order found.
    internal static string ToNative(string json, List<Entry> kept)
    {
        if (string.IsNullOrEmpty(json) || json.IndexOf(Prefix, StringComparison.Ordinal) < 0) return json;
        var spans = new List<(int Start, int End)>();
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (Match data in Data.Matches(json))
        {
            var key = data.Groups[1].Value;
            if (bodies.ContainsKey(key)) continue;
            bodies[key] = data.Groups[2].Value;
            order.Add(key);
            spans.Add((data.Index, data.Index + data.Length));
        }
        var queued = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in QueueItems(json))
        {
            if (!item.Key.StartsWith(Prefix, StringComparison.Ordinal) || queued.ContainsKey(item.Key)) continue;
            queued[item.Key] = item.Index;
            if (!bodies.ContainsKey(item.Key)) order.Add(item.Key);
            spans.Add((item.Start, item.End));
        }
        if (spans.Count == 0) return json;
        spans.Sort(CompareSpans);
        foreach (var key in order)
        {
            bodies.TryGetValue(key, out var body);
            kept.Add(new Entry(key, body, queued.TryGetValue(key, out var index) ? index : -1));
        }
        return CoreSaveText.Remove(json, spans);
    }

    // Reading: Core's research back into the text; restored counts the data objects put back. Nothing
    // is put back twice: a key the text already holds stays as it is.
    internal static string FromNative(string json, IReadOnlyList<Entry> kept, out int restored)
    {
        restored = 0;
        if (string.IsNullOrEmpty(json) || kept == null || kept.Count == 0) return json;
        foreach (var entry in kept)
        {
            if (string.IsNullOrEmpty(entry.Body)
                || Regex.IsMatch(json, "\"" + Regex.Escape(entry.Key) + "\"\\s*:", RegexOptions.CultureInvariant)) continue;
            var datas = Datas.Match(json);
            if (!datas.Success) continue;
            json = CoreSaveText.InsertFirst(json, datas.Index + datas.Length - 1, "\"" + entry.Key + "\": " + entry.Body);
            restored++;
        }
        // Lowest place first, so each key finds the places before it filled again.
        var queue = new List<Entry>();
        foreach (var entry in kept)
            if (entry.QueueIndex >= 0) queue.Add(entry);
        queue.Sort(CompareQueue);
        foreach (var entry in queue) json = Enqueue(json, entry.Key, entry.QueueIndex);
        return json;
    }

    // The key at place index of ResearchKeyQueue, or at its end when the queue is shorter.
    private static string Enqueue(string json, string key, int index)
    {
        var queue = Queue.Match(json);
        if (!queue.Success) return json;
        var items = QueueItems(json);
        foreach (var item in items)
            if (item.Key == key) return json;
        var text = "\"" + key + "\"";
        if (items.Count == 0) return CoreSaveText.InsertFirst(json, queue.Index + queue.Length - 1, text);
        if (index < items.Count)
        {
            var start = items[index].Start;
            return json.Substring(0, start) + text + ", " + json.Substring(start);
        }
        var end = items[items.Count - 1].End;
        return json.Substring(0, end) + ", " + text + json.Substring(end);
    }

    private static List<QueueEntry> QueueItems(string json)
    {
        var items = new List<QueueEntry>();
        var queue = Queue.Match(json);
        if (!queue.Success) return items;
        var open = queue.Index + queue.Length - 1;
        var close = CoreSaveText.Close(json, open);
        if (close < 0) return items;
        var inner = json.Substring(open + 1, close - open - 2);
        var index = 0;
        foreach (Match item in QueueItem.Matches(inner))
        {
            var start = open + 1 + item.Index;
            items.Add(new QueueEntry(item.Groups[1].Value, index++, start, start + item.Length));
        }
        return items;
    }

    private static int CompareSpans((int Start, int End) a, (int Start, int End) b) => a.Start.CompareTo(b.Start);

    private static int CompareQueue(Entry a, Entry b) => a.QueueIndex.CompareTo(b.QueueIndex);
}
