namespace DungeonSettlersDelvers.Core;

// The camp mirrored left to right, as pure rules on the plain model, the same rule as the Modding Toolkit's room editor
// (the mirrored camp is a second room file she approves; these rules check that file). The cells of every
// row are reversed, so the walls that BanditCampLayout.Frame builds from the shape come out right by themselves, and no row
// moves: the waterfall stays at the wall.
//
// Positions: a one cell thing at x moves to Width - 1 - x (spawns, the party start, items). A part that is w cells wide and
// anchored on its left cell moves to Width - x - w (BanditRoomFeatureRules.WidthOf: waterfall 3, bridge 4).
//
// Buildings: 90 and 270 swap, 0 and 180 stay. The cells the building covers (its footprint at its rotation, from the caller,
// relative to its anchor) are mirrored, and the new anchor is the one whose footprint at the new rotation covers exactly those
// cells. Without a footprint the building counts as one cell; without a fitting anchor it stays where it is. Both are reported.
// Mirroring a camp twice gives the camp back, its name too.
internal static class BanditCampMirror
{
    private const int RotationQuarter = 90, RotationThreeQuarters = 270;
    internal const string NameSuffix = " gespiegelt";

    // The cells a building covers at a rotation, relative to its anchor; null when the building is unknown.
    internal delegate IReadOnlyList<(int X, int Y)> Footprint(string key, int rotation);

    // What the mirror could not do as the rule says, by building key and place in the source room.
    internal sealed class Report
    {
        internal readonly List<(string Key, int X, int Y)> Unknown = new();
        internal readonly List<(string Key, int X, int Y)> NoAnchor = new();
        // Buildings of more than one cell moved by their footprint.
        internal int MultiCell;
    }

    internal static BanditCampRoom Mirror(BanditCampRoom room, Footprint footprint = null, Report report = null)
    {
        var width = room.Width;
        report ??= new Report();
        return new BanditCampRoom(MirrorName(room.Name), MirrorCells(room.Cells), MirrorBuildings(room.Buildings, width, footprint, report),
            MirrorItems(room.Items, width), MirrorSpawns(room.Spawns, width), MirrorFeatures(room.Features, width),
            OneCell(room.PartyStartX, width), room.PartyStartY, MirrorFloors(room.Floors, width), MirrorFloors(room.FloorDecos, width));
    }

    // "<name> gespiegelt", and back.
    internal static string MirrorName(string name)
    {
        name ??= "";
        return name.EndsWith(NameSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - NameSuffix.Length) : name + NameSuffix;
    }

    // The mirrored x of a thing of the given width in cells, anchored on its left cell.
    internal static int MirrorX(int x, int cellWidth, int roomWidth) => roomWidth - x - cellWidth;

    private static int OneCell(int x, int roomWidth) => MirrorX(x, 1, roomWidth);

    // 90 and 270 swap, 0 and 180 stay.
    internal static int MirrorRotation(int rotation)
    {
        if (rotation == RotationQuarter) return RotationThreeQuarters;
        return rotation == RotationThreeQuarters ? RotationQuarter : rotation;
    }

    private static BanditCampCell[,] MirrorCells(BanditCampCell[,] cells)
    {
        int width = cells.GetLength(0), height = cells.GetLength(1);
        var mirrored = new BanditCampCell[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                mirrored[width - 1 - x, y] = cells[x, y];
        return mirrored;
    }

    private static List<BanditCampBuilding> MirrorBuildings(IReadOnlyList<BanditCampBuilding> buildings, int roomWidth, Footprint footprint,
        Report report)
    {
        var mirrored = new List<BanditCampBuilding>(buildings.Count);
        foreach (var building in buildings) mirrored.Add(MirrorBuilding(building, roomWidth, footprint, report));
        return mirrored;
    }

    private static BanditCampBuilding MirrorBuilding(BanditCampBuilding building, int roomWidth, Footprint footprint, Report report)
    {
        var rotation = MirrorRotation(building.Rotation);
        var before = footprint?.Invoke(building.Key, building.Rotation);
        var after = footprint?.Invoke(building.Key, rotation);
        if (before == null || before.Count == 0 || after == null || after.Count == 0)
        {
            report.Unknown.Add((building.Key, building.X, building.Y));
            return new BanditCampBuilding(building.Key, OneCell(building.X, roomWidth), building.Y, rotation, building.Faction, building.FlipX);
        }
        var cells = new HashSet<(int X, int Y)>();
        foreach (var (dx, dy) in before) cells.Add((roomWidth - 1 - (building.X + dx), building.Y + dy));
        if (!TryAnchor(cells, after, out var anchorX, out var anchorY))
        {
            report.NoAnchor.Add((building.Key, building.X, building.Y));
            return building;
        }
        if (cells.Count > 1) report.MultiCell++;
        return new BanditCampBuilding(building.Key, anchorX, anchorY, rotation, building.Faction, building.FlipX);
    }

    // The anchor whose footprint covers exactly the cells, tried from each footprint cell onto the first mirrored cell.
    private static bool TryAnchor(HashSet<(int X, int Y)> cells, IReadOnlyList<(int X, int Y)> footprint, out int anchorX, out int anchorY)
    {
        var target = cells.First();
        foreach (var (fx, fy) in footprint)
        {
            anchorX = target.X - fx;
            anchorY = target.Y - fy;
            if (Covers(cells, footprint, anchorX, anchorY)) return true;
        }
        anchorX = anchorY = 0;
        return false;
    }

    private static bool Covers(HashSet<(int X, int Y)> cells, IReadOnlyList<(int X, int Y)> footprint, int anchorX, int anchorY)
    {
        var covered = new HashSet<(int X, int Y)>();
        foreach (var (fx, fy) in footprint) covered.Add((anchorX + fx, anchorY + fy));
        return covered.SetEquals(cells);
    }

    private static List<BanditCampItem> MirrorItems(IReadOnlyList<BanditCampItem> items, int roomWidth)
    {
        var mirrored = new List<BanditCampItem>(items.Count);
        foreach (var item in items) mirrored.Add(new BanditCampItem(item.Key, OneCell(item.X, roomWidth), item.Y, item.Count));
        return mirrored;
    }

    private static List<BanditCampFloor> MirrorFloors(IReadOnlyList<BanditCampFloor> floors, int roomWidth)
    {
        var mirrored = new List<BanditCampFloor>(floors.Count);
        foreach (var floor in floors) mirrored.Add(new BanditCampFloor(floor.Key, OneCell(floor.X, roomWidth), floor.Y));
        return mirrored;
    }

    private static List<BanditCampSpawn> MirrorSpawns(IReadOnlyList<BanditCampSpawn> spawns, int roomWidth)
    {
        var mirrored = new List<BanditCampSpawn>(spawns.Count);
        foreach (var spawn in spawns) mirrored.Add(new BanditCampSpawn(spawn.Name, spawn.Kind, OneCell(spawn.X, roomWidth), spawn.Y));
        return mirrored;
    }

    private static List<BanditCampFeature> MirrorFeatures(IReadOnlyList<BanditCampFeature> features, int roomWidth)
    {
        var mirrored = new List<BanditCampFeature>(features.Count);
        foreach (var feature in features)
            mirrored.Add(new BanditCampFeature(feature.Kind, MirrorX(feature.X, BanditRoomFeatureRules.WidthOf(feature.Kind), roomWidth), feature.Y));
        return mirrored;
    }
}
