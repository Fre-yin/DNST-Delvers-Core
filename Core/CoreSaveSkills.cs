using System.Text;
using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own skills in a save (Lifebloom). A skill key the game does not know
// breaks a save loaded without the mod, so the game's part of a save never mentions one. A unit's
// skill can stand in four places: the skill component's data (ActiveSkills, Cooldowns,
// SkillDurations, AutoUse), the skill tree's ExtraSkills and the quick slots (QuickSlots). On writing
// each mention is cut out (a quick slot becomes empty, "") and kept per unit in Core's own data
// (CoreSaveData.OwnedSkills); on reading it goes back where the game left room for it. The unit of a
// skill component is the component list ("<unit guid>": [{"Type" ...) that opens last before it.
// Pure, so the offline tests cover it. No lambdas (see CoreSaveItems).
internal static class CoreSaveSkills
{
    // Where one own skill of one unit stood. Values are kept as the game wrote them (JSON text).
    internal sealed class Entry
    {
        internal Guid Unit;
        internal string Key = "";
        internal bool Active;
        internal bool Extra;
        internal int QuickSlot = -1;
        internal string AutoUse;
        internal string Cooldown;
        internal string Duration;
    }

    // Lifebloom, the bandits' chain pull (BanditChainRules), which a saved guard carries, and the Serrated Blade
    // (BanditSerratedRules).
    internal static readonly string[] Keys = { LifebloomRules.SkillKey, BanditChainRules.SkillKey, BanditSerratedRules.SkillKey };

    private const string GuidText = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

    private static readonly Regex ComponentList = new(
        "\"(" + GuidText + ")\"\\s*:\\s*\\[\\s*\\{\\s*\"Type\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex SkillData = new(
        "\"\\$type\"\\s*:\\s*\"Refactor\\.SkillComponentSaveData[^\"]*\"(?<body>(?:[^{}]|\\{[^{}]*\\})*)\\}",
        RegexOptions.CultureInvariant);

    private static readonly Regex Extras = new(
        "\"(" + GuidText + ")\"\\s*:\\s*\\{\\s*\"SkillTrees\"\\s*:\\s*\\{[^{}]*\\}\\s*,\\s*\"ExtraSkills\"\\s*:\\s*\\[(?<list>[^\\[\\]]*)\\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex Slots = new(
        "\"(" + GuidText + ")\"\\s*:\\s*\\{\\s*\"QuickSlots\"\\s*:\\s*\\[(?<list>[^\\[\\]]*)\\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex ListItem = new(
        "\"(?:[^\"\\\\]|\\\\.)*\"|null|true|false|-?[0-9][0-9.eE+-]*",
        RegexOptions.CultureInvariant);

    private static readonly Regex Pair = new(
        "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|null|true|false|-?[0-9][0-9.eE+-]*)",
        RegexOptions.CultureInvariant);

    internal static bool IsOwn(string key)
    {
        foreach (var own in Keys)
            if (own == key) return true;
        return false;
    }

    private static bool Mentions(string json)
    {
        foreach (var key in Keys)
            if (json.IndexOf(key, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    // Writing: the text without Core's skills; what was cut out goes into kept, one entry per unit and
    // skill. The same instance when there is nothing to cut.
    internal static string ToNative(string json, List<Entry> kept)
    {
        if (string.IsNullOrEmpty(json) || !Mentions(json)) return json;
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var edits = new List<(int Start, int End, string Text)>();
        var lists = ComponentList.Matches(json);
        foreach (Match data in SkillData.Matches(json))
        {
            var body = data.Groups["body"];
            if (!Mentions(body.Value) || !TryOwner(lists, data.Index, out var unit)) continue;
            var stripped = StripBody(body.Value, unit, entries);
            edits.Add((body.Index, body.Index + body.Length, stripped));
        }
        foreach (Match extras in Extras.Matches(json))
        {
            var list = extras.Groups["list"];
            if (!Mentions(list.Value) || !Guid.TryParse(extras.Groups[1].Value, out var unit)) continue;
            var items = Items(list.Value);
            var rest = new List<string>();
            foreach (var item in items)
            {
                var key = Unquote(item);
                if (key != null && IsOwn(key)) Get(entries, unit, key).Extra = true;
                else rest.Add(item);
            }
            edits.Add((list.Index, list.Index + list.Length, ListText(rest)));
        }
        foreach (Match slots in Slots.Matches(json))
        {
            var list = slots.Groups["list"];
            if (!Mentions(list.Value) || !Guid.TryParse(slots.Groups[1].Value, out var unit)) continue;
            var items = Items(list.Value);
            for (var i = 0; i < items.Count; i++)
            {
                var key = Unquote(items[i]);
                if (key == null || !IsOwn(key)) continue;
                Get(entries, unit, key).QuickSlot = i;
                items[i] = "\"\"";
            }
            edits.Add((list.Index, list.Index + list.Length, ListText(items)));
        }
        if (edits.Count == 0) return json;
        foreach (var entry in entries.Values) kept.Add(entry);
        return Apply(json, edits);
    }

    private static string StripBody(string body, Guid unit, Dictionary<string, Entry> entries)
        => EditObject(
            EditObject(
                EditObject(
                    EditArray(body, "ActiveSkills", unit, entries),
                    "Cooldowns", unit, entries),
                "SkillDurations", unit, entries),
            "AutoUse", unit, entries);

    private static string EditArray(string body, string name, Guid unit, Dictionary<string, Entry> entries)
    {
        var match = new Regex("\"" + name + "\"\\s*:\\s*\\[(?<list>[^\\[\\]]*)\\]", RegexOptions.CultureInvariant).Match(body);
        if (!match.Success || !Mentions(match.Groups["list"].Value)) return body;
        var rest = new List<string>();
        foreach (var item in Items(match.Groups["list"].Value))
        {
            var key = Unquote(item);
            if (key != null && IsOwn(key)) Get(entries, unit, key).Active = true;
            else rest.Add(item);
        }
        var list = match.Groups["list"];
        return body.Substring(0, list.Index) + ListText(rest) + body.Substring(list.Index + list.Length);
    }

    private static string EditObject(string body, string name, Guid unit, Dictionary<string, Entry> entries)
    {
        var match = new Regex("\"" + name + "\"\\s*:\\s*\\{(?<pairs>[^{}]*)\\}", RegexOptions.CultureInvariant).Match(body);
        if (!match.Success || !Mentions(match.Groups["pairs"].Value)) return body;
        var rest = new List<string>();
        foreach (Match pair in Pair.Matches(match.Groups["pairs"].Value))
        {
            var key = pair.Groups[1].Value;
            if (!IsOwn(key))
            {
                rest.Add(pair.Value);
                continue;
            }
            var entry = Get(entries, unit, key);
            var value = pair.Groups[2].Value;
            if (name == "Cooldowns") entry.Cooldown = value;
            else if (name == "SkillDurations") entry.Duration = value;
            else entry.AutoUse = value;
        }
        var pairs = match.Groups["pairs"];
        return body.Substring(0, pairs.Index) + ObjectText(rest) + body.Substring(pairs.Index + pairs.Length);
    }

    // Reading: Core's skills back into the text; restored counts the units that got something back.
    // Nothing is put back twice, and a quick slot the player filled meanwhile stays as it is.
    internal static string FromNative(string json, IReadOnlyList<Entry> kept, out int restored)
    {
        restored = 0;
        if (string.IsNullOrEmpty(json) || kept == null || kept.Count == 0) return json;
        var byUnit = new Dictionary<Guid, List<Entry>>();
        foreach (var entry in kept)
        {
            if (entry == null || !IsOwn(entry.Key)) continue;
            if (!byUnit.TryGetValue(entry.Unit, out var own)) byUnit[entry.Unit] = own = new List<Entry>();
            own.Add(entry);
        }
        if (byUnit.Count == 0) return json;
        var units = new HashSet<Guid>();
        var edits = new List<(int Start, int End, string Text)>();
        var lists = ComponentList.Matches(json);
        foreach (Match data in SkillData.Matches(json))
        {
            if (!TryOwner(lists, data.Index, out var unit) || !byUnit.TryGetValue(unit, out var entries)) continue;
            var body = data.Groups["body"];
            var text = body.Value;
            foreach (var entry in entries)
            {
                if (entry.Active) text = AddToArray(text, "ActiveSkills", entry.Key);
                if (entry.Cooldown != null) text = AddToObject(text, "Cooldowns", entry.Key, entry.Cooldown);
                if (entry.Duration != null) text = AddToObject(text, "SkillDurations", entry.Key, entry.Duration);
                if (entry.AutoUse != null) text = AddToObject(text, "AutoUse", entry.Key, entry.AutoUse);
            }
            if (text == body.Value) continue;
            edits.Add((body.Index, body.Index + body.Length, text));
            units.Add(unit);
        }
        foreach (Match extras in Extras.Matches(json))
        {
            if (!Guid.TryParse(extras.Groups[1].Value, out var unit) || !byUnit.TryGetValue(unit, out var entries)) continue;
            var list = extras.Groups["list"];
            var items = Items(list.Value);
            var changed = false;
            foreach (var entry in entries)
            {
                if (!entry.Extra || Contains(items, entry.Key)) continue;
                items.Add(Quote(entry.Key));
                changed = true;
            }
            if (!changed) continue;
            edits.Add((list.Index, list.Index + list.Length, ListText(items)));
            units.Add(unit);
        }
        foreach (Match slots in Slots.Matches(json))
        {
            if (!Guid.TryParse(slots.Groups[1].Value, out var unit) || !byUnit.TryGetValue(unit, out var entries)) continue;
            var list = slots.Groups["list"];
            var items = Items(list.Value);
            var changed = false;
            foreach (var entry in entries)
            {
                if (entry.QuickSlot < 0 || entry.QuickSlot >= items.Count || !IsEmptySlot(items[entry.QuickSlot])) continue;
                items[entry.QuickSlot] = Quote(entry.Key);
                changed = true;
            }
            if (!changed) continue;
            edits.Add((list.Index, list.Index + list.Length, ListText(items)));
            units.Add(unit);
        }
        restored = units.Count;
        return edits.Count == 0 ? json : Apply(json, edits);
    }

    private static string AddToArray(string body, string name, string key)
    {
        var match = new Regex("\"" + name + "\"\\s*:\\s*\\[(?<list>[^\\[\\]]*)\\]", RegexOptions.CultureInvariant).Match(body);
        if (!match.Success) return body;
        var items = Items(match.Groups["list"].Value);
        if (Contains(items, key)) return body;
        items.Add(Quote(key));
        var list = match.Groups["list"];
        return body.Substring(0, list.Index) + ListText(items) + body.Substring(list.Index + list.Length);
    }

    private static string AddToObject(string body, string name, string key, string value)
    {
        var match = new Regex("\"" + name + "\"\\s*:\\s*\\{(?<pairs>[^{}]*)\\}", RegexOptions.CultureInvariant).Match(body);
        if (!match.Success) return body;
        var all = new List<string>();
        foreach (Match pair in Pair.Matches(match.Groups["pairs"].Value))
        {
            if (pair.Groups[1].Value == key) return body;
            all.Add(pair.Value);
        }
        all.Add(Quote(key) + ": " + value);
        var pairs = match.Groups["pairs"];
        return body.Substring(0, pairs.Index) + ObjectText(all) + body.Substring(pairs.Index + pairs.Length);
    }

    private static Entry Get(Dictionary<string, Entry> entries, Guid unit, string key)
    {
        var id = unit.ToString("D") + "|" + key;
        if (!entries.TryGetValue(id, out var entry)) entries[id] = entry = new Entry { Unit = unit, Key = key };
        return entry;
    }

    // The unit of the component list that opens last before position.
    private static bool TryOwner(MatchCollection lists, int position, out Guid unit)
    {
        unit = Guid.Empty;
        Match owner = null;
        foreach (Match list in lists)
        {
            if (list.Index >= position) break;
            owner = list;
        }
        return owner != null && Guid.TryParse(owner.Groups[1].Value, out unit);
    }

    private static List<string> Items(string list)
    {
        var items = new List<string>();
        foreach (Match item in ListItem.Matches(list)) items.Add(item.Value);
        return items;
    }

    private static bool Contains(List<string> items, string key)
    {
        foreach (var item in items)
            if (Unquote(item) == key) return true;
        return false;
    }

    private static bool IsEmptySlot(string item) => item == "null" || item == "\"\"";

    // The text of a JSON string without its quotes (escapes stay); null for any other value.
    private static string Unquote(string item)
    {
        if (item.Length < 2 || item[0] != '"' || item[item.Length - 1] != '"') return null;
        return item.Substring(1, item.Length - 2);
    }

    private static string Quote(string key) => "\"" + key + "\"";

    private static string ListText(List<string> items) => items.Count == 0 ? "" : " " + string.Join(", ", items) + " ";

    private static string ObjectText(List<string> pairs) => pairs.Count == 0 ? "" : " " + string.Join(", ", pairs) + " ";

    // The text with the edits applied, by start; an edit inside an earlier one is skipped.
    private static string Apply(string json, List<(int Start, int End, string Text)> edits)
    {
        edits.Sort(CompareStart);
        var text = new StringBuilder(json.Length + 256);
        var copied = 0;
        foreach (var edit in edits)
        {
            if (edit.Start < copied) continue;
            text.Append(json, copied, edit.Start - copied).Append(edit.Text);
            copied = edit.End;
        }
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    private static int CompareStart((int Start, int End, string Text) a, (int Start, int End, string Text) b) => a.Start.CompareTo(b.Start);
}
