using DungeonSettlersDelvers.Core;
using static TestKit;

// Delvers menu pages and rows.
internal static class DelversMenuSection
{
    internal static void Run()
    {
        MenuPages();
        MenuRows();
        MenuEntry();
    }

    // The way in: the last entry of the game's options window, usable on the title screen, locked with a hint in a campaign.
    static void MenuEntry()
    {
        Check(DelversMenuEntryRules.Usable(false) && !DelversMenuEntryRules.Usable(true),
            "menu entry: usable on the title screen, locked while a campaign runs");
        Check(DelversMenuEntryRules.Label(true, "Title menu only") == "Delvers"
                && DelversMenuEntryRules.Label(false, "Title menu only") == "Delvers\n<size=65%>Title menu only</size>"
                && DelversMenuEntryRules.Label(false, "") == "Delvers" && DelversMenuEntryRules.TabName == "Btn_Delvers",
            "menu entry: the locked entry shows the hint under its name, the usable one only its name");
        Check(DelversMenuText.OnlyInTitleMenu.AllLanguages.All(text => text.Length > 0 && !text.Contains('-') && !text.Contains('\u2013') && !text.Contains('\u2014')),
            "menu entry: the hint stands in all ten languages, without hyphens or dashes");
    }

    static void MenuPages()
    {
        // Delvers menu pages (API 1.5.0): one page per pack, in registration order, released by lease.
        var menu = new DelversMenuRegistry();
        IEnumerable<IReadOnlyDictionary<string, object>> NoRows() => Array.Empty<IReadOnlyDictionary<string, object>>();
        var revisionBefore = menu.Revision;
        var corePageLease = menu.Register("delvers-core", "Delvers Core", NoRows);
        var toolkitPageLease = menu.Register("modding-toolkit", "  Modding Toolkit ", NoRows);
        var menuPages = menu.Snapshot();
        Check(menuPages.Count == 2 && menuPages[0].PackId == "delvers-core" && menuPages[1].PackId == "modding-toolkit"
            && menuPages[1].Title == "Modding Toolkit" && menu.Revision == revisionBefore + 2,
            "menu pages keep their registration order, the title is trimmed and each registration raises the revision");
        string MenuFailure(Func<IDisposable> register)
        {
            try { register().Dispose(); return null; }
            catch (Exception ex) { return ex.GetType().Name; }
        }
        Check(MenuFailure(() => menu.Register("modding-toolkit", "Again", NoRows)) == nameof(InvalidOperationException),
            "a pack cannot register a second menu page");
        Check(MenuFailure(() => menu.Register("Modding Toolkit", "Title", NoRows)) == nameof(ArgumentException)
            && MenuFailure(() => menu.Register("pack", " ", NoRows)) == nameof(ArgumentException)
            && MenuFailure(() => menu.Register("pack", "Title", null)) == nameof(ArgumentNullException),
            "a menu page needs a valid pack ID, a title and rows");
        toolkitPageLease.Dispose();
        toolkitPageLease.Dispose();
        Check(menu.Snapshot().Count == 1 && menu.Snapshot()[0].PackId == "delvers-core",
            "releasing a menu page lease removes only that page, and a second release changes nothing");
        var reRegistered = menu.Register("modding-toolkit", "Modding Toolkit", NoRows);
        Check(menu.Snapshot().Count == 2 && menu.Snapshot()[1].PackId == "modding-toolkit",
            "a released pack can register its page again");
        reRegistered.Dispose();
        corePageLease.Dispose();
        Check(menu.Snapshot().Count == 0, "all menu pages are released");
    }

    static void MenuRows()
    {
        // Rows: the helpers build what the menu parses; a faulty row names what it lacks.
        var toggled = false;
        var chosen = 0;
        var clicked = 0;
        var toggleRow = DelversMenuRow.Parse(DelversMenuRows.Toggle("Option", () => toggled, value => toggled = value), out var toggleError);
        toggleRow?.SetBool(true);
        Check(toggleRow != null && toggleError == null && toggleRow.Kind == "toggle" && toggleRow.Label() == "Option"
            && toggleRow.GetBool() && toggled, "a toggle row reads and writes its value");
        var choiceRow = DelversMenuRow.Parse(DelversMenuRows.Choice("Stufe", new[] { "Light", "Cheating", "Dev" },
            () => chosen, value => chosen = value), out var choiceError);
        choiceRow?.SetIndex(2);
        Check(choiceRow != null && choiceError == null && choiceRow.Options().Count == 3 && choiceRow.GetIndex() == 2,
            "a choice row offers its options and sets the index");
        var buttonRow = DelversMenuRow.Parse(DelversMenuRows.Button("Selbsttest", null, () => clicked++, danger: true), out var buttonError);
        buttonRow?.Click();
        Check(buttonRow != null && buttonError == null && buttonRow.Caption() == "Selbsttest" && buttonRow.Danger && clicked == 1,
            "a button without a caption shows its label and runs its click");
        var countdown = 3;
        var textRow = DelversMenuRow.Parse(DelversMenuRows.Text(() => "noch " + countdown + " s", muted: true), out _);
        countdown = 2;
        Check(textRow != null && textRow.Muted && !textRow.Warning && textRow.Text() == "noch 2 s", "a text row asks its function again");
        var warningRow = DelversMenuRow.Parse(DelversMenuRows.Text(() => "Achtung", warning: true), out _);
        Check(warningRow != null && warningRow.Warning && !warningRow.Muted, "a text row can be a warning");
        var looseRow = DelversMenuRow.Parse(new Dictionary<string, object>
        {
            ["kind"] = "section", ["label"] = "Built without a reference to Core"
        }, out var looseError);
        Check(looseRow != null && looseError == null && looseRow.Label() == "Built without a reference to Core",
            "a row built by hand as a dictionary with a fixed string parses");
        Check(DelversMenuRow.Parse(new Dictionary<string, object> { ["kind"] = "toggle", ["label"] = "No value" }, out var missingError) == null
            && missingError.Contains("get", StringComparison.Ordinal)
            && DelversMenuRow.Parse(new Dictionary<string, object> { ["kind"] = "slider" }, out var unknownError) == null
            && unknownError.Contains("slider", StringComparison.Ordinal)
            && DelversMenuRow.Parse(null, out var nullError) == null && nullError != null,
            "faulty rows are rejected with the reason");
        Check(DelversMenuRow.Parse(new Dictionary<string, object>
        {
            ["kind"] = "toggle", ["label"] = "Wrong types", ["get"] = (Func<int>)(() => 1), ["set"] = (Action<bool>)(_ => { })
        }, out var typeError) == null && typeError != null, "a toggle with a value of the wrong type is rejected");
        // Toggles side by side (the founders): two to MostToggles toggle rows, each switching its own value.
        var switched = new bool[3];
        var togglesRow = DelversMenuRow.Parse(DelversMenuRows.Toggles(Enumerable.Range(0, 3)
            .Select(index => DelversMenuRows.Toggle("T" + index, () => switched[index], value => switched[index] = value)).ToList()), out var togglesError);
        togglesRow?.Items[1].SetBool(true);
        Check(togglesRow != null && togglesError == null && togglesRow.Kind == "toggles" && togglesRow.Items.Count == 3
            && togglesRow.Items.Select(item => item.Label()).SequenceEqual(new[] { "T0", "T1", "T2" }) && !switched[0] && switched[1] && !switched[2],
            "a toggles row holds its toggles in order and each switches its own value");
        var oneToggle = new[] { DelversMenuRows.Toggle("Alone", () => false, _ => { }) };
        var tooMany = Enumerable.Range(0, DelversMenuRow.MostToggles + 1).Select(index => DelversMenuRows.Toggle("T" + index, () => false, _ => { })).ToList();
        var notToggle = new[] { DelversMenuRows.Toggle("T", () => false, _ => { }), DelversMenuRows.Text(() => "text") };
        Check(DelversMenuRow.Parse(DelversMenuRows.Toggles(oneToggle), out var oneError) == null && oneError.Contains("toggles", StringComparison.Ordinal)
            && DelversMenuRow.Parse(DelversMenuRows.Toggles(tooMany), out var manyError) == null && manyError != null
            && DelversMenuRow.Parse(DelversMenuRows.Toggles(notToggle), out var itemError) == null && itemError.Contains("not a toggle", StringComparison.Ordinal)
            && DelversMenuRow.Parse(new Dictionary<string, object> { ["kind"] = "toggles" }, out var emptyError) == null && emptyError != null,
            "a toggles row with one, too many or a non-toggle item is rejected");
        var menuTexts = DelversMenuText.All.ToList();
        Check(menuTexts.Count > 0 && menuTexts.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('\u2013') < 0 && text.IndexOf('\u2014') < 0)),
            "the Delvers menu has its words in all ten languages, without long dashes");
    }
}
