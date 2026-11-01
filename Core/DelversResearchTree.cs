namespace DungeonSettlersDelvers.Core;

// The Delvers research tree (First Aid as a tree of its own at the very bottom). The game has no
// categories or titles for its trees: a tree is a research in column 0 without a prerequisite, and its lines come only from the
// prerequisites (SubUI_ResearchPanel.MakeResearchEdge). Its own two roots stand in rows 6 and 11, its nodes in rows 4 to 19
// (ResearchTable 0.4.29). Core's tree starts one empty row below the game's last, in column 0; later nodes of Core's (sicknesses,
// fractures) go to the right of the root and into the rows below it, so no line of Core's crosses one of the game's. Pure, so the
// offline tests cover it.
internal static class DelversResearchTree
{
    // The game's last row of research in 0.4.29.
    internal const int GameLastRow = 19;
    internal const int Column = 0;
    internal const int Row = GameLastRow + 2;

    // A node of the tree: steps to the right of the root and rows below it.
    internal static (int X, int Y) Place(int right, int down) => (Column + right, Row + down);

    // A root's prerequisites as the game's own roots write them (ResearchTable "NULL"); the game counts a prerequisite that is no
    // research as done (ResearchProgressContainer.IsPrerequisitesComplete), and its edges skip "NULL".
    internal static readonly string[] NoPrerequisites = { "NULL" };

    // How far the research screen's content must reach further down (DelversResearchTreeView): from its bottom edge to the lowest
    // node's bottom less the margin, in the content's own units (y grows upwards); 0 when it reaches already.
    internal static float ContentGrowth(float contentBottom, float lowestNodeBottom, float margin)
        => Math.Max(0f, contentBottom - (lowestNodeBottom - margin));

    // The height to add so the bottom edge moves down by growth while the content keeps its place (and its nodes theirs): a rect
    // grows around its pivot, so the bottom moves by pivot times the added height.
    internal static float SizeFor(float growth, float pivotY) => pivotY > 0.01f ? growth / pivotY : growth;
}
