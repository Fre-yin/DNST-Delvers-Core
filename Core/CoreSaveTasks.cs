using System.Text.RegularExpressions;

namespace DungeonSettlersDelvers.Core;

// Core's own crafting tasks in a save (First Aid). A task with a recipe the game
// does not know breaks a save loaded without the mod, so every task object whose RecipeKey starts with
// Prefix is cut out of its AllTasks list on writing and kept whole in Core's own data
// (CoreSaveData.Tasks), with the number of its list and its place there. On reading it goes back to
// the same place. Pure, so the offline tests cover it. No lambdas (see CoreSaveItems).
internal static class CoreSaveTasks
{
    // One task as it was cut out: which AllTasks list (in text order), its place there, its text.
    internal readonly struct Entry
    {
        internal int Container { get; }
        internal int Index { get; }
        internal string Text { get; }

        internal Entry(int container, int index, string text)
        {
            Container = container;
            Index = index;
            Text = text;
        }
    }

    // One underscore only, like every game key (see CoreSaveEffects.Prefix).
    internal const string Prefix = "RECIPE_DelversCore";

    private static readonly Regex Recipe = new("\"RecipeKey\"\\s*:\\s*\"" + Regex.Escape(Prefix), RegexOptions.CultureInvariant);

    private static readonly Regex AllTasks = new("\"AllTasks\"\\s*:\\s*\\[", RegexOptions.CultureInvariant);

    // Writing: the text without Core's tasks; what was cut out goes into kept. A task counts only as
    // a direct element of an AllTasks list.
    internal static string ToNative(string json, List<Entry> kept)
    {
        if (string.IsNullOrEmpty(json) || json.IndexOf(Prefix, StringComparison.Ordinal) < 0) return json;
        var lists = Lists(json);
        var spans = new List<(int Start, int End)>();
        var previous = -1;
        foreach (Match recipe in Recipe.Matches(json))
        {
            var open = CoreSaveText.OpenBefore(json, recipe.Index);
            if (open < 0 || open < previous) continue;
            var close = CoreSaveText.Close(json, open);
            if (close < 0) continue;
            // The list is the last AllTasks before the task.
            var container = -1;
            for (var i = 0; i < lists.Count; i++)
                if (lists[i] < open) container = i;
            if (container < 0) continue;
            var elements = CoreSaveText.Elements(json, lists[container]);
            var index = -1;
            for (var i = 0; i < elements.Count; i++)
                if (elements[i].Start == open) index = i;
            if (index < 0) continue;
            kept.Add(new Entry(container, index, json.Substring(open, close - open)));
            spans.Add((open, close));
            previous = close;
        }
        return CoreSaveText.Remove(json, spans);
    }

    // Reading: Core's tasks back into their lists, lowest place first; a task whose text is already
    // there is not put back again.
    internal static string FromNative(string json, IReadOnlyList<Entry> kept, out int restored)
    {
        restored = 0;
        if (string.IsNullOrEmpty(json) || kept == null || kept.Count == 0) return json;
        var ordered = new List<Entry>(kept);
        ordered.Sort(CompareEntries);
        foreach (var entry in ordered)
        {
            if (string.IsNullOrEmpty(entry.Text) || json.IndexOf(entry.Text, StringComparison.Ordinal) >= 0) continue;
            var lists = Lists(json);
            if (entry.Container < 0 || entry.Container >= lists.Count) continue;
            json = CoreSaveText.InsertAt(json, lists[entry.Container], entry.Index, entry.Text);
            restored++;
        }
        return json;
    }

    // The '[' of every AllTasks list, in text order.
    private static List<int> Lists(string json)
    {
        var lists = new List<int>();
        foreach (Match list in AllTasks.Matches(json)) lists.Add(list.Index + list.Length - 1);
        return lists;
    }

    private static int CompareEntries(Entry a, Entry b)
    {
        var order = a.Container.CompareTo(b.Container);
        return order != 0 ? order : a.Index.CompareTo(b.Index);
    }
}
