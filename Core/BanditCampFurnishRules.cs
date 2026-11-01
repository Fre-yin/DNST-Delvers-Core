using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The pure part of furnishing the camp after the arrival (BanditCampFurnish), so the offline tests cover it: where the room
// file's cell (0, 0) lies on the map, what is spawned as what, and the wagon's loot.
// - Origin: the game keeps the camp's area (its open cells, BanditCampBuilder) in the floor's logical cells; the area's box
//   begins at the room's first open cell, so the file's (0, 0) lies that far before it (as the Modding Toolkit's custom rooms).
// - Buildings spawn neutral, whatever the file says (nothing of the camp interacts but the wagon).
// - The wagon (the game's support wagon of the hard setting) keeps its native way: gathered, it drops its loot and leaves its
//   remnant. Its loot is the camp's own (without the cogwheel): cloth, heavy cloth, leather, red crystal,
//   a few coins and the Lifebloom book.
internal static class BanditCampFurnishRules
{
    internal const string WagonKey = "BUILDING_SupportWagonHard";

    internal static readonly (string Key, int Amount)[] WagonLoot =
    {
        ("ITEM_Textile", 6),
        ("ITEM_ThickTextile", 3),
        ("ITEM_Leather", 4),
        ("ITEM_RedCrystalOre", 3),
        ("ITEM_ImperialCoin", 3),
        (LifebloomBookRules.ItemKey, 1),
    };

    internal static bool IsWagon(string key) => key == WagonKey;

    // The room's first open cell: the smallest x and the smallest y of its open cells, each on its own; (0, 0) without any.
    internal static (int X, int Y) FirstOpenCell(BanditCampRoom room)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        for (var x = 0; x < room.Width; x++)
            for (var y = 0; y < room.Height; y++)
                if (BanditCampLayout.IsOpen(room.Cells[x, y]))
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                }
        return minX == int.MaxValue ? (0, 0) : (minX, minY);
    }

    // The map cell of the file's (0, 0) from the box of the camp's area on the map.
    internal static (int X, int Y) Origin(BanditCampRoom room, int areaMinX, int areaMinY)
    {
        var (firstX, firstY) = FirstOpenCell(room);
        return (areaMinX - firstX, areaMinY - firstY);
    }

    // CORE_BANDITS_CAMP_SPAWNED: once per furnished camp. forced: buildings the game's build check refused, placed as drawn;
    // doubled: buildings placed on a cell where another already stands; skipped: only an unknown key or a failure.
    internal static string SpawnedLine(string campId, int units, int buildings, int items, int features, int skipped, bool wagon, int forced = 0,
        int doubled = 0)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_CAMP_SPAWNED camp={0} units={1} buildings={2} items={3} features={4} skipped={5} wagon={6} forced={7} doubled={8}",
            BanditCampRules.LogId(campId), units, buildings, items, features, skipped, wagon ? "yes" : "no", forced, doubled);

    // CORE_BANDITS_CAMP_FLOORS: once per furnished camp, the floor tiles and floor decorations laid; skipped: a failure.
    internal static string FloorsLine(string campId, int floors, int decos, int skipped)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_FLOORS camp={0} floors={1} decos={2} skipped={3}",
            BanditCampRules.LogId(campId), floors, decos, skipped);

    // CORE_BANDITS_CAMP_WAGON: the wagon gathered, its loot dropped.
    internal static string WagonLine(string wagon, int kinds, int dropped)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_WAGON wagon={0} kinds={1} dropped={2}", wagon, kinds, dropped);
}
