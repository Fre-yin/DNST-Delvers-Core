namespace DungeonSettlersDelvers.Core;

// Rows of a page in the Delvers menu (DelversCoreRuntime.RegisterMenuPage). A row is a
// dictionary, so mods without a reference to Core can build rows too; these helpers build them
// for packs that reference Core. Texts are fixed strings or functions; the menu asks functions
// again four times a second while the page is shown, for a countdown or a changing state.
//   kind      section | text | toggle | choice | button
//   label     string or Func<string>: the section title, or the label left of a control
//   text      string or Func<string>: a text row
//   caption   string or Func<string>: the button's own text (default: its label)
//   get, set  Func<bool> and Action<bool> for a toggle; Func<int> and Action<int> for a choice
//   options   IReadOnlyList<string> or Func<IReadOnlyList<string>>: the entries of a choice
//   click     Action: the button
//   enabled   Func<bool>; visible Func<bool>; danger bool (button); muted or warning bool (text)
// Core's own pages also know "toggles": items, a list of toggle rows side by side (Toggles).
public static class DelversMenuRows
{
    public static IReadOnlyDictionary<string, object> Section(string title)
        => Row("section", ("label", title));

    public static IReadOnlyDictionary<string, object> Text(Func<string> text, bool muted = false, Func<bool> visible = null,
        bool warning = false)
        => Row("text", ("text", text), ("muted", muted), ("visible", visible), ("warning", warning));

    public static IReadOnlyDictionary<string, object> Toggle(string label, Func<bool> get, Action<bool> set,
        Func<bool> enabled = null, Func<bool> visible = null)
        => Row("toggle", ("label", label), ("get", get), ("set", set), ("enabled", enabled), ("visible", visible));

    // Label asked again while the page is shown, for a text in the game's current language.
    public static IReadOnlyDictionary<string, object> Toggle(Func<string> label, Func<bool> get, Action<bool> set,
        Func<bool> enabled = null, Func<bool> visible = null)
        => Row("toggle", ("label", label), ("get", get), ("set", set), ("enabled", enabled), ("visible", visible));

    public static IReadOnlyDictionary<string, object> Choice(string label, IReadOnlyList<string> options,
        Func<int> get, Action<int> set, Func<bool> enabled = null, Func<bool> visible = null)
        => Row("choice", ("label", label), ("options", options), ("get", get), ("set", set),
            ("enabled", enabled), ("visible", visible));

    public static IReadOnlyDictionary<string, object> Button(string label, Func<string> caption, Action click,
        Func<bool> enabled = null, bool danger = false, Func<bool> visible = null)
        => Row("button", ("label", label), ("caption", caption), ("click", click), ("enabled", enabled),
            ("danger", danger), ("visible", visible));

    // Toggles side by side in one row, for short labels such as names (the founders, to spare scrolling).
    // Core's own for now, not part of the API: each item is a Toggle row, two to DelversMenuRow.MostToggles of them.
    internal static IReadOnlyDictionary<string, object> Toggles(IReadOnlyList<IReadOnlyDictionary<string, object>> toggles,
        Func<bool> visible = null)
        => Row("toggles", ("items", toggles), ("visible", visible));

    private static IReadOnlyDictionary<string, object> Row(string kind, params (string Key, object Value)[] values)
    {
        var row = new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = kind };
        foreach (var (key, value) in values)
            if (value != null) row[key] = value;
        return row;
    }
}

// A row as the menu draws it. Parse reads the dictionary once when the page is opened and
// names what is missing, so a faulty row of one mod is skipped and reported, not drawn wrong.
internal sealed class DelversMenuRow
{
    internal string Kind;
    internal Func<string> Label;
    internal Func<string> Text;
    internal Func<string> Caption;
    internal Func<bool> GetBool;
    internal Action<bool> SetBool;
    internal Func<int> GetIndex;
    internal Action<int> SetIndex;
    internal Func<IReadOnlyList<string>> Options;
    internal Action Click;
    internal Func<bool> Enabled;
    internal Func<bool> Visible;
    internal bool Danger;
    internal bool Muted;
    internal bool Warning;
    internal IReadOnlyList<DelversMenuRow> Items;

    internal const int MostToggles = 4;

    internal static DelversMenuRow Parse(IReadOnlyDictionary<string, object> values, out string error)
    {
        error = null;
        if (values == null)
        {
            error = "the row is null";
            return null;
        }
        var row = new DelversMenuRow
        {
            Kind = Value<string>(values, "kind"),
            Label = TextOf(values, "label"),
            Text = TextOf(values, "text"),
            Caption = TextOf(values, "caption"),
            Enabled = Value<Func<bool>>(values, "enabled"),
            Visible = Value<Func<bool>>(values, "visible"),
            Danger = values.TryGetValue("danger", out var danger) && danger is true,
            Muted = values.TryGetValue("muted", out var muted) && muted is true,
            Warning = values.TryGetValue("warning", out var warning) && warning is true
        };
        switch (row.Kind)
        {
            case "section":
                if (row.Label == null) error = "a section needs a label";
                break;
            case "text":
                if (row.Text == null) error = "a text row needs a text";
                break;
            case "toggle":
                row.GetBool = Value<Func<bool>>(values, "get");
                row.SetBool = Value<Action<bool>>(values, "set");
                if (row.Label == null || row.GetBool == null || row.SetBool == null)
                    error = "a toggle needs a label, get (Func<bool>) and set (Action<bool>)";
                break;
            case "toggles":
                row.Items = TogglesOf(values, out error);
                break;
            case "choice":
                row.GetIndex = Value<Func<int>>(values, "get");
                row.SetIndex = Value<Action<int>>(values, "set");
                row.Options = OptionsOf(values);
                if (row.Label == null || row.GetIndex == null || row.SetIndex == null || row.Options == null)
                    error = "a choice needs a label, options, get (Func<int>) and set (Action<int>)";
                break;
            case "button":
                row.Click = Value<Action>(values, "click");
                row.Caption ??= row.Label;
                if (row.Label == null || row.Click == null) error = "a button needs a label and click (Action)";
                break;
            default:
                error = "unknown kind \"" + (row.Kind ?? "") + "\"";
                break;
        }
        return error == null ? row : null;
    }

    // The items of a toggles row: two to MostToggles, each a valid toggle.
    private static IReadOnlyList<DelversMenuRow> TogglesOf(IReadOnlyDictionary<string, object> values, out string error)
    {
        error = null;
        var items = Value<IReadOnlyList<IReadOnlyDictionary<string, object>>>(values, "items");
        if (items == null || items.Count < 2 || items.Count > MostToggles)
        {
            error = "a toggles row needs two to " + MostToggles + " toggles (items)";
            return null;
        }
        var toggles = new List<DelversMenuRow>(items.Count);
        foreach (var item in items)
        {
            var toggle = Parse(item, out var problem);
            if (toggle == null || toggle.Kind != "toggle")
            {
                error = "an item of a toggles row: " + (problem ?? "not a toggle");
                return null;
            }
            toggles.Add(toggle);
        }
        return toggles;
    }

    private static T Value<T>(IReadOnlyDictionary<string, object> values, string key) where T : class
        => values.TryGetValue(key, out var value) ? value as T : null;

    private static Func<string> TextOf(IReadOnlyDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value)) return null;
        return value switch
        {
            string text => () => text,
            Func<string> function => function,
            _ => null
        };
    }

    private static Func<IReadOnlyList<string>> OptionsOf(IReadOnlyDictionary<string, object> values)
    {
        if (!values.TryGetValue("options", out var value)) return null;
        return value switch
        {
            IReadOnlyList<string> list => () => list,
            Func<IReadOnlyList<string>> function => function,
            _ => null
        };
    }
}
