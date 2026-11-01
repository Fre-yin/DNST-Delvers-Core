namespace DungeonSettlersDelvers.Core;

// The footprint of a mirrored camp building (the cells mirrored with the picture, so the game blocks what
// it draws), as the room editor's mirror computes it: every cell of the building's lists mirrored left to right inside the
// box around all of them together, x' = minX + maxX - x, y unchanged. One box over all lists keeps blocked, occupied, hidden
// and interaction cells in line with each other. Pure, so the offline tests cover it; BanditCampFootprint applies it in the game.
//
// Only a building of the bandit camp, never any other: the footprint is mirrored only while the game
// computes it for one building on a dungeon map, and that building is one of the camp's marked buildings by its GUID (or the one
// Core's furnishing is spawning as such). Another building with the same key, in the settlement, on another floor or built by
// the player, has another GUID and keeps its footprint; previews and placement checks compute without a building and keep theirs.
internal static class BanditCampFootprintRules
{
    internal static bool Applies(Guid building, bool onDungeon, bool spawningMarked, ICollection<Guid> marked)
        => building != Guid.Empty && onDungeon && (spawningMarked || (marked != null && marked.Contains(building)));

    // The box's left and right edge over every list; null when all are empty.
    internal static (int MinX, int MaxX)? Box(IEnumerable<IReadOnlyList<(int X, int Y)>> lists)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (var list in lists)
            foreach (var (x, _) in list ?? Array.Empty<(int X, int Y)>())
            {
                min = Math.Min(min, x);
                max = Math.Max(max, x);
            }
        return min == int.MaxValue ? null : (min, max);
    }

    internal static int MirrorX(int x, int minX, int maxX) => minX + maxX - x;

    // The lists mirrored in their common box, each in its own order.
    internal static List<List<(int X, int Y)>> Mirror(IReadOnlyList<IReadOnlyList<(int X, int Y)>> lists)
    {
        var result = new List<List<(int X, int Y)>>();
        var box = Box(lists);
        foreach (var list in lists)
        {
            var mirrored = new List<(int X, int Y)>();
            foreach (var (x, y) in list ?? Array.Empty<(int X, int Y)>())
                mirrored.Add(box == null ? (x, y) : (MirrorX(x, box.Value.MinX, box.Value.MaxX), y));
            result.Add(mirrored);
        }
        return result;
    }
}
