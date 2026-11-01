namespace DungeonSettlersDelvers.Core;

// The pages of the Delvers menu in the title screen. Core shows the menu; every mod of ours
// offers its settings there. One page per pack, in the order of
// registration, Core's own page first. A page is a function returning its rows: the menu asks
// it again whenever the page is opened and refreshes the rows' values while it is shown.
internal sealed class DelversMenuRegistry : ICoreRegistry
{
    private readonly LeaseRegistry<string, Page> pages = new(DuplicateRegistration.Reject, keys: StringComparer.Ordinal);

    // Rises with every registration and release; an open menu then rebuilds its tabs.
    internal int Revision => pages.Revision;

    internal IDisposable Register(string packId, string title,
        Func<IEnumerable<IReadOnlyDictionary<string, object>>> rows)
    {
        PackIds.Require(packId, nameof(packId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A menu page needs a title.", nameof(title));
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        return pages.Register(packId, new Page(packId, title.Trim(), rows),
            _ => new InvalidOperationException("Pack " + packId + " already has a page in the Delvers menu."));
    }

    internal IReadOnlyList<Page> Snapshot() => pages.Snapshot();

    public void Clear() => pages.Clear();

    internal sealed class Page
    {
        internal readonly string PackId;
        internal readonly string Title;
        internal readonly Func<IEnumerable<IReadOnlyDictionary<string, object>>> Rows;

        internal Page(string packId, string title, Func<IEnumerable<IReadOnlyDictionary<string, object>>> rows)
        { PackId = packId; Title = title; Rows = rows; }
    }
}
