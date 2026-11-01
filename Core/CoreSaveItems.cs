using System.Text;
using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own items in a save. An item the game does not know makes a save
// unsafe to load without the mod (GetItemData returns null), so the game's part of a save only
// ever holds a native stand-in: the revive scroll is written as the native return scroll, whose
// item and look it uses anyway. Core remembers in its own data which item entities are really its
// own, and turns exactly those back before the game reads the save. Loaded without Core, they are
// plain return scrolls. Items are stored as {"Guid": "...", "Key": "ITEM_..."} in ItemSaveData.
//
// An own key can also stand outside an item record: as a unit's item quick slot
// (ItemQuickSlotKey, the bandage key of a unit) and as an entry of a storage rule's item list
// (AvailableItems). A stand-in there would turn the slot into cloth and add a second entry of the same key to the
// list, so these two places are not renamed: the slot is written empty (null) and the list entry is cut out. Core
// notes both in its own data (Places) and puts them back where they stood.
internal static class CoreSaveItems
{
    // One underscore only, like every game key (see CoreSaveEffects.Prefix).
    internal const string Prefix = "ITEM_DelversCore";

    private const string QuickSlotName = "ItemQuickSlotKey";
    private const string AvailableItemsName = "AvailableItems";
    private const string GuidText = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

    // Where own item keys stood outside the item records: the item quick slot per unit, and the cut entries per storage rule.
    internal sealed class Places
    {
        internal readonly Dictionary<Guid, string> QuickSlots = new();
        internal readonly Dictionary<Guid, List<StorageEntry>> Storage = new();

        internal bool Any => QuickSlots.Count > 0 || Storage.Count > 0;

        // Own keys in a quick slot or a storage rule's list that the shape rules above did not catch (a form of the save they
        // do not know); they fall back to their stand-in like any other mention. CoreSaveStore warns when this is not 0.
        internal int Missed;
    }

    // One own entry of a storage rule's item list: its key, its switch and its place among the entries (counted from 0).
    internal sealed class StorageEntry
    {
        internal readonly string Key;
        internal readonly bool On;
        internal readonly int Index;

        internal StorageEntry(string key, bool on, int index)
        {
            Key = key;
            On = on;
            Index = index;
        }
    }

    // An entry of the item list as the text has it: the white space before it, the entry itself, its key and its value.
    private sealed class ListEntry
    {
        internal readonly string Lead;
        internal readonly string Text;
        internal readonly string Key;
        internal readonly string Value;

        internal ListEntry(string lead, string text, string key, string value)
        {
            Lead = lead;
            Text = text;
            Key = key;
            Value = value;
        }
    }

    private sealed class PairList
    {
        internal readonly List<ListEntry> Entries = new();
        internal string Trailing = "";
    }

    private static readonly Regex Entity = new(
        "\"Guid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"(\\s*,\\s*\"Key\"\\s*:\\s*\")(" + Regex.Escape(Prefix) + "[A-Za-z0-9_]+)\"",
        RegexOptions.CultureInvariant);

    // A unit's entry of the quick slots ("<unit>": { ... "ItemQuickSlotKey": value }): with an own key, and empty.
    private static readonly Regex QuickSlot = new(
        "\"(?<unit>" + GuidText + ")\"\\s*:\\s*\\{[^{}]*?\"" + QuickSlotName + "\"\\s*:\\s*(?<value>\"(?<key>" + Regex.Escape(Prefix) + "[A-Za-z0-9_]+)\")",
        RegexOptions.CultureInvariant);

    private static readonly Regex EmptyQuickSlot = new(
        "\"(?<unit>" + GuidText + ")\"\\s*:\\s*\\{[^{}]*?\"" + QuickSlotName + "\"\\s*:\\s*(?<value>null|\"\")",
        RegexOptions.CultureInvariant);

    // A storage rule ("<rule>": { ... "AvailableItems": { entries } }).
    private static readonly Regex Policy = new(
        "\"(?<policy>" + GuidText + ")\"\\s*:\\s*\\{[^{}]*?\"" + AvailableItemsName + "\"\\s*:\\s*\\{(?<pairs>[^{}]*)\\}",
        RegexOptions.CultureInvariant);

    // An own key still standing as a quick slot's value or as a key inside a storage rule's item list after the cuts.
    private static readonly Regex LeftQuickSlot = new(
        "\"" + QuickSlotName + "\"\\s*:\\s*\"" + Regex.Escape(Prefix), RegexOptions.CultureInvariant);

    private static readonly Regex LeftStorage = new(
        "\"" + AvailableItemsName + "\"\\s*:\\s*\\{[^{}]*\"" + Regex.Escape(Prefix), RegexOptions.CultureInvariant);

    private static readonly Regex Pair = new(
        "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|null|true|false|-?[0-9][0-9.eE+-]*)",
        RegexOptions.CultureInvariant);

    // Own item key -> the native item written in its place.
    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal);

    internal static void Register(string ownKey, string nativeKey)
    {
        if (!ownKey.StartsWith(Prefix, StringComparison.Ordinal)) throw new ArgumentException("own items start with " + Prefix, nameof(ownKey));
        StandIns[ownKey] = nativeKey;
    }

    // Stand-ins an own item was written as by an older Core; still read back (the herbal bandage moved from
    // ITEM_ThickTextile to ITEM_Textile, saves from before still hold thick cloth).
    private static readonly Dictionary<string, HashSet<string>> LegacyStandIns = new(StringComparer.Ordinal);

    internal static void RegisterLegacy(string ownKey, string nativeKey)
    {
        if (!LegacyStandIns.TryGetValue(ownKey, out var natives)) LegacyStandIns[ownKey] = natives = new HashSet<string>(StringComparer.Ordinal);
        natives.Add(nativeKey);
    }

    internal static void Clear()
    {
        StandIns.Clear();
        LegacyStandIns.Clear();
    }

    internal static bool TryStandIn(string ownKey, out string nativeKey) => StandIns.TryGetValue(ownKey, out nativeKey);

    // Writing: own item entities become their stand-in; returns the entities that were Core's
    // (guid -> own key). An own key outside an item record is not renamed: a unit's item quick slot and the
    // entries of a storage rule's item list are cut out and noted in places. Any other mention of an own key
    // is replaced by its stand-in without record, so the game's part never holds one.
    // No lambdas in this file: a MatchEvaluator lambda needs NullableAttribute, which the IL2CPP
    // reference set lacks (CS0656), so the replacements are written out as loops.
    internal static string ToNative(string json, Dictionary<Guid, string> owned, Places places)
    {
        if (string.IsNullOrEmpty(json) || json.IndexOf(Prefix, StringComparison.Ordinal) < 0) return json;
        var result = CutQuickSlots(CutStorage(CutRecords(json, owned), places), places);
        places.Missed = Missed(result);
        foreach (var pair in StandIns)
            if (result.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                result = result.Replace("\"" + pair.Key + "\"", "\"" + pair.Value + "\"", StringComparison.Ordinal);
        return result;
    }

    // How many quick slots and storage lists still hold an own key after the cuts.
    internal static int Missed(string json)
    {
        var count = 0;
        for (var match = LeftQuickSlot.Match(json); match.Success; match = match.NextMatch()) count++;
        for (var match = LeftStorage.Match(json); match.Success; match = match.NextMatch()) count++;
        return count;
    }

    private static string CutRecords(string json, Dictionary<Guid, string> owned)
    {
        var text = new StringBuilder(json.Length);
        var copied = 0;
        for (var match = Entity.Match(json); match.Success; match = match.NextMatch())
        {
            var key = match.Groups[3].Value;
            if (!StandIns.TryGetValue(key, out var native) || !Guid.TryParse(match.Groups[1].Value, out var guid)) continue;
            owned[guid] = key;
            text.Append(json, copied, match.Groups[3].Index - copied).Append(native);
            copied = match.Groups[3].Index + match.Groups[3].Length;
        }
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // A unit's item quick slot holding an own item becomes empty (null).
    private static string CutQuickSlots(string json, Places places)
    {
        if (json.IndexOf(QuickSlotName, StringComparison.Ordinal) < 0) return json;
        var text = new StringBuilder(json.Length);
        var copied = 0;
        for (var match = QuickSlot.Match(json); match.Success; match = match.NextMatch())
        {
            var value = match.Groups["value"];
            var key = match.Groups["key"].Value;
            if (!StandIns.ContainsKey(key) || !Guid.TryParse(match.Groups["unit"].Value, out var unit)) continue;
            places.QuickSlots[unit] = key;
            text.Append(json, copied, value.Index - copied).Append("null");
            copied = value.Index + value.Length;
        }
        if (copied == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // The entries with an own key leave a storage rule's item list; each key is noted once with its switch and its place in
    // the list, so the list holds no key twice because of Core.
    private static string CutStorage(string json, Places places)
    {
        if (json.IndexOf(AvailableItemsName, StringComparison.Ordinal) < 0) return json;
        var text = new StringBuilder(json.Length);
        var copied = 0;
        for (var match = Policy.Match(json); match.Success; match = match.NextMatch())
        {
            var pairs = match.Groups["pairs"];
            if (pairs.Value.IndexOf(Prefix, StringComparison.Ordinal) < 0 || !Guid.TryParse(match.Groups["policy"].Value, out var policy)
                || !TryParseList(pairs.Value, out var list)) continue;
            var kept = new PairList { Trailing = list.Trailing };
            var cut = new List<StorageEntry>();
            for (var i = 0; i < list.Entries.Count; i++)
            {
                var entry = list.Entries[i];
                if (!StandIns.ContainsKey(entry.Key) || !IsSwitch(entry.Value)) kept.Entries.Add(entry);
                else if (!Contains(cut, entry.Key)) cut.Add(new StorageEntry(entry.Key, entry.Value == "true", i));
            }
            if (kept.Entries.Count == list.Entries.Count) continue;
            places.Storage[policy] = cut;
            text.Append(json, copied, pairs.Index - copied).Append(ListText(kept));
            copied = pairs.Index + pairs.Length;
        }
        if (copied == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // Reading: the recorded entities get their own key back, in one pass; everything else stays
    // native. The places are put back only where the game's part holds nothing of its own now: an empty
    // item quick slot, a storage rule without that key. restored counts entities and places.
    internal static string FromNative(string json, IReadOnlyDictionary<Guid, string> owned, Places places, out int restored)
    {
        restored = 0;
        if (string.IsNullOrEmpty(json) || StandIns.Count == 0) return json;
        var text = owned == null || owned.Count == 0 ? json : RestoreRecords(json, owned, out restored);
        if (places == null || !places.Any) return text;
        text = RestoreQuickSlots(text, places.QuickSlots, out var slots);
        text = RestoreStorage(text, places.Storage, out var entries);
        restored += slots + entries;
        return text;
    }

    private static string RestoreRecords(string json, IReadOnlyDictionary<Guid, string> owned, out int restored)
    {
        restored = 0;
        var natives = new HashSet<string>(StringComparer.Ordinal);
        foreach (var native in StandIns.Values) natives.Add(Regex.Escape(native));
        foreach (var legacy in LegacyStandIns.Values)
            foreach (var native in legacy) natives.Add(Regex.Escape(native));
        var pattern = new Regex("\"Guid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"(\\s*,\\s*\"Key\"\\s*:\\s*\")(" + string.Join("|", natives) + ")\"",
            RegexOptions.CultureInvariant);
        var text = new StringBuilder(json.Length + 64);
        var copied = 0;
        for (var match = pattern.Match(json); match.Success; match = match.NextMatch())
        {
            if (!Guid.TryParse(match.Groups[1].Value, out var guid) || !owned.TryGetValue(guid, out var own)
                || !StandIns.TryGetValue(own, out var native)
                || (native != match.Groups[3].Value && !(LegacyStandIns.TryGetValue(own, out var legacy) && legacy.Contains(match.Groups[3].Value)))) continue;
            text.Append(json, copied, match.Groups[3].Index - copied).Append(own);
            copied = match.Groups[3].Index + match.Groups[3].Length;
            restored++;
        }
        if (restored == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // An item quick slot that is empty now gets its own key back; one the game filled (a native item) stays.
    private static string RestoreQuickSlots(string json, IReadOnlyDictionary<Guid, string> slots, out int restored)
    {
        restored = 0;
        if (slots.Count == 0 || json.IndexOf(QuickSlotName, StringComparison.Ordinal) < 0) return json;
        var text = new StringBuilder(json.Length + 64);
        var copied = 0;
        for (var match = EmptyQuickSlot.Match(json); match.Success; match = match.NextMatch())
        {
            var value = match.Groups["value"];
            if (!Guid.TryParse(match.Groups["unit"].Value, out var unit) || !slots.TryGetValue(unit, out var key) || !StandIns.ContainsKey(key)) continue;
            text.Append(json, copied, value.Index - copied).Append('"').Append(key).Append('"');
            copied = value.Index + value.Length;
            restored++;
        }
        if (restored == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    private static string RestoreStorage(string json, IReadOnlyDictionary<Guid, List<StorageEntry>> storage, out int restored)
    {
        restored = 0;
        if (storage.Count == 0 || json.IndexOf(AvailableItemsName, StringComparison.Ordinal) < 0) return json;
        var text = new StringBuilder(json.Length + 256);
        var copied = 0;
        for (var match = Policy.Match(json); match.Success; match = match.NextMatch())
        {
            var pairs = match.Groups["pairs"];
            if (!Guid.TryParse(match.Groups["policy"].Value, out var policy) || !storage.TryGetValue(policy, out var cut)
                || !TryParseList(pairs.Value, out var list)) continue;
            var back = InsertEntries(list, cut);
            if (back == 0) continue;
            restored += back;
            text.Append(json, copied, pairs.Index - copied).Append(ListText(list));
            copied = pairs.Index + pairs.Length;
        }
        if (restored == 0) return json;
        text.Append(json, copied, json.Length - copied);
        return text.ToString();
    }

    // The cut entries back into the list, in the order they stood, each at its recorded place (at the end when the list
    // got shorter); a key the list holds already, or one Core no longer knows, is not added. Returns how many went in.
    private static int InsertEntries(PairList list, List<StorageEntry> cut)
    {
        var back = 0;
        var ordered = new List<StorageEntry>(cut);
        ordered.Sort(CompareIndex);
        foreach (var entry in ordered)
        {
            if (!StandIns.ContainsKey(entry.Key) || ContainsKey(list.Entries, entry.Key)) continue;
            var at = Math.Min(entry.Index, list.Entries.Count);
            list.Entries.Insert(at, NewEntry(LeadFor(list, at), entry));
            back++;
        }
        return back;
    }

    private static ListEntry NewEntry(string lead, StorageEntry entry)
    {
        var value = entry.On ? "true" : "false";
        return new ListEntry(lead, "\"" + entry.Key + "\": " + value, entry.Key, value);
    }

    private static int CompareIndex(StorageEntry a, StorageEntry b) => a.Index.CompareTo(b.Index);

    // What stands before a new entry: the neighbour's layout, or one level deeper than the closing brace in an empty list.
    private static string LeadFor(PairList list, int at)
    {
        if (list.Entries.Count > 0) return list.Entries[Math.Min(at, list.Entries.Count - 1)].Lead;
        return list.Trailing.IndexOf('\n') < 0 ? "" : list.Trailing + "  ";
    }

    private static bool IsSwitch(string value) => value == "true" || value == "false";

    private static bool Contains(List<StorageEntry> entries, string key)
    {
        foreach (var entry in entries)
            if (entry.Key == key) return true;
        return false;
    }

    private static bool ContainsKey(List<ListEntry> entries, string key)
    {
        foreach (var entry in entries)
            if (entry.Key == key) return true;
        return false;
    }

    // The entries of a storage rule's item list between its braces, each with the white space before it, and the white space
    // before the closing brace. False when anything else stands between the entries: the list is then left as it is.
    private static bool TryParseList(string pairs, out PairList list)
    {
        list = new PairList();
        var end = 0;
        foreach (Match pair in Pair.Matches(pairs))
        {
            var between = pairs.Substring(end, pair.Index - end);
            var lead = between;
            if (list.Entries.Count > 0)
            {
                var comma = between.IndexOf(',');
                if (comma < 0 || between.Substring(0, comma).Trim().Length > 0) return false;
                lead = between.Substring(comma + 1);
            }
            if (lead.Trim().Length > 0) return false;
            list.Entries.Add(new ListEntry(lead, pair.Value, pair.Groups[1].Value, pair.Groups[2].Value));
            end = pair.Index + pair.Length;
        }
        list.Trailing = pairs.Substring(end);
        return list.Trailing.Trim().Length == 0;
    }

    private static string ListText(PairList list)
    {
        var text = new StringBuilder();
        for (var i = 0; i < list.Entries.Count; i++)
        {
            var entry = list.Entries[i];
            if (i > 0) text.Append(',');
            text.Append(entry.Lead).Append(entry.Text);
        }
        return text.Append(list.Trailing).ToString();
    }
}
