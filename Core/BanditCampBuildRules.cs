using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The pure part of building the camp into a generated floor (BanditCampBuilder), so the offline tests cover it: the ids of the
// camp and its corridor, the anchor's floor box, whether the corridor opens onto the anchor's floor, and the log lines.
//
// Ids: the game loads a floor's monster groups by room id (RuntimeDungeonData.OnLoadMonsterGroup): it cuts the id before
// "DUNGEONROOM", drops the number after the last underscore and asks the room table for that key; without a row the load
// breaks, with Core and without. So camp and corridor carry ids of the game's form "<type> - <table key>_<number>" with keys the
// room table knows (the author's plan, a middle combat room of the forest and its wide horizontal corridor), numbered
// past every id the floor already has for that key.
internal static class BanditCampBuildRules
{
    internal const string CampKey = "DUNGEONROOM_VeilSilence_Common_Combat_Middle14";
    internal const string CorridorKey = "DUNGEONROOM_VeilSilence_Common_HorizontalCorridor_2";
    internal const string CampTypeName = "Combat";
    internal const string CorridorTypeName = "Corridor";
    private const string IdSeparator = " - ";

    // Reasons why an anchor or the floor gets no camp, besides BanditCampChoice's and BanditCampPlacement's.
    internal const string NoCellsReason = "no-cells";
    internal const string OriginReason = "origin";
    internal const string FileReason = "file";

    // The camp's rule (the smallest that brings the camp onto about 30 % of the forest's fourth floors, chosen
    // on the seed scan's numbers; the camp never lies at the Alpha's room or its neighbour). The game and the seed scan use InUse.
    internal readonly struct CampRule
    {
        internal CampRule(int maxLength, bool otherSide, bool alphaFloors, bool mainPath)
        {
            MaxLength = maxLength;
            OtherSide = otherSide;
            AlphaFloors = alphaFloors;
            MainPath = mainPath;
        }

        // The longest corridor in columns.
        internal int MaxLength { get; }
        // The other variant on the anchor's other side when the chosen one finds no place.
        internal bool OtherSide { get; }
        // Floors with the Alpha get a camp too.
        internal bool AlphaFloors { get; }
        // Rooms on the mandatory path may anchor the camp (it stays a leaf).
        internal bool MainPath { get; }
    }

    // Corridors up to MaxLength and the other variant on the anchor's other side; neither the Alpha's floors nor the mandatory path
    // (the seed scan over 200 floors: 28 % of them get the camp).
    internal static readonly CampRule InUse = new(BanditCampPlacement.MaxLength, true, false, false);

    // The variants to try, the chosen one first, then the other one when that is allowed and its file is there.
    internal static int[] Variants(int chosen, bool otherSide, bool otherAvailable)
    {
        var other = chosen == BanditCampChoice.Mirrored ? BanditCampChoice.Normal : BanditCampChoice.Mirrored;
        var otherThere = other == BanditCampChoice.Normal || otherAvailable;
        return otherSide && otherThere ? new[] { chosen, other } : new[] { chosen };
    }

    // "<type> - <key>_<n>", n one more than the highest number of any id of the floor for that key, at least 1.
    internal static string NextId(string typeName, string key, IEnumerable<string> ids)
    {
        var highest = 0;
        foreach (var id in ids ?? Enumerable.Empty<string>()) highest = Math.Max(highest, NumberOf(id, key));
        return typeName + IdSeparator + key + "_" + (highest + 1).ToString(CultureInfo.InvariantCulture);
    }

    // The number of an id of that key ("…<key>_<n>"), else 0.
    internal static int NumberOf(string id, string key)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)) return 0;
        var at = id.IndexOf(key + "_", StringComparison.Ordinal);
        if (at < 0) return 0;
        var rest = id.Substring(at + key.Length + 1);
        return int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    // The anchor's floor box over its floor cells (absolute), or null without any.
    internal static BanditCampAnchor? AnchorBox(IEnumerable<(int X, int Y)> floorCells)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        var any = false;
        foreach (var (x, y) in floorCells ?? Enumerable.Empty<(int X, int Y)>())
        {
            any = true;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        return any ? new BanditCampAnchor(minX, minY, maxX, maxY) : null;
    }

    // Whether every joint row opens onto the anchor's floor: the cell past the joint, on the anchor's side, is floor. The anchor
    // box of a room that is not a rectangle can hold rows where its wall is deeper.
    internal static bool JointOpensOnFloor(BanditCampPlan plan, Func<int, int, bool> isFloor)
    {
        if (plan?.Joint == null || plan.Joint.Count == 0 || isFloor == null) return false;
        var step = plan.Side == BanditCampSide.Right ? 1 : -1;
        var rows = new Dictionary<int, int>();
        foreach (var (x, y) in plan.Joint)
            rows[y] = rows.TryGetValue(y, out var edge) ? (step > 0 ? Math.Max(edge, x) : Math.Min(edge, x)) : x;
        foreach (var row in rows)
            if (!isFloor(row.Value + step, row.Key)) return false;
        return true;
    }

    // The anchor's wall cells above the joint that become wall face, as BanditCampLayout.Frame puts SideRows of face above every
    // open cell: the joint's columns, the rows right above its top row. Only roof cells there change (BanditCampBuilder).
    internal static List<(int X, int Y)> JointFaces(BanditCampPlan plan)
    {
        var faces = new List<(int X, int Y)>();
        if (plan?.Joint == null || plan.Joint.Count == 0) return faces;
        var top = plan.Joint.Max(cell => cell.Y);
        foreach (var x in plan.Joint.Select(cell => cell.X).Distinct().OrderBy(x => x))
            for (var row = 1; row <= BanditCampLayout.SideRows; row++)
                faces.Add((x, top + row));
        return faces;
    }

    // CORE_BANDIT_CAMP_PLACED: once per floor with a camp, in the form the Toolkit's walk reads.
    internal static string PlacedLine(int seed, int region, int floor, int variant, string campId, string corridorId, string anchorId,
        BanditCampPlan plan, int campWidth, int campHeight)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDIT_CAMP_PLACED seed={0} region={1} floor={2} camp={3} mirrored={4} box={5},{6},{7},{8} entrance={9},{10}"
            + " neighbour={11} corridor={12} cells={13} length={14}",
            seed, region, floor, BanditCampRules.LogId(campId), variant == BanditCampChoice.Mirrored ? "yes" : "no",
            plan.CampOriginX, plan.CampOriginY, plan.CampOriginX + campWidth - 1, plan.CampOriginY + campHeight - 1, plan.ExitX, plan.ExitY,
            BanditCampRules.LogId(anchorId), BanditCampRules.LogId(corridorId), CellsOf(plan.Corridor), plan.Length);

    // CORE_BANDIT_CAMP_MAP: the placed camp in the shown map's cells, once it is furnished. The placed line counts in the generator's
    // raw grid (0 to its size); the map adds an offset (dx, dy), found from the camp's area as the game keeps it.
    internal static string MapLine(string campId, BanditCampPlan plan, int campWidth, int campHeight, int dx, int dy)
    {
        var corridor = plan.Corridor == null ? null : plan.Corridor.Select(cell => (cell.X + dx, cell.Y + dy)).ToList();
        return string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDIT_CAMP_MAP camp={0} offset={1},{2} box={3},{4},{5},{6} entrance={7},{8} cells={9}",
            BanditCampRules.LogId(campId), dx, dy, plan.CampOriginX + dx, plan.CampOriginY + dy, plan.CampOriginX + campWidth - 1 + dx,
            plan.CampOriginY + campHeight - 1 + dy, plan.ExitX + dx, plan.ExitY + dy, CellsOf(corridor));
    }

    // CORE_BANDITS_CAMP_FAILED: the floor of the camp stays as the game made it.
    internal static string FailedLine(int seed, int region, int floor, string reason, string tried)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_FAILED seed={0} region={1} floor={2} reason={3} tried={4}",
            seed, region, floor, reason ?? "unknown", string.IsNullOrEmpty(tried) ? "-" : tried);

    // The corridor's cells as the box "x0,y0..x1,y1".
    private static string CellsOf(IReadOnlyList<(int X, int Y)> cells)
    {
        if (cells == null || cells.Count == 0) return "-";
        var box = AnchorBox(cells).Value;
        return string.Format(CultureInfo.InvariantCulture, "{0},{1}..{2},{3}", box.MinX, box.MinY, box.MaxX, box.MaxY);
    }
}
