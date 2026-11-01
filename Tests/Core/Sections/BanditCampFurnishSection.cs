using DungeonSettlersDelvers.Core;
using static TestKit;

// Furnishing the camp after the arrival, the pure part (BanditCampFurnishRules): the file's origin on the map, the wagon and
// its loot, the log lines.
internal static class BanditCampFurnishSection
{
    internal static void Run()
    {
        var cells = new BanditCampCell[6, 4];
        for (var x = 0; x < 6; x++)
            for (var y = 0; y < 4; y++)
                cells[x, y] = x >= 2 && y >= 1 ? BanditCampCell.Grass : BanditCampCell.Void;
        var room = new BanditCampRoom("r", cells, Array.Empty<BanditCampBuilding>(), Array.Empty<BanditCampItem>(), Array.Empty<BanditCampSpawn>(),
            Array.Empty<BanditCampFeature>(), 5, 2);
        Check(BanditCampFurnishRules.FirstOpenCell(room) == (2, 1) && BanditCampFurnishRules.Origin(room, 140, 90) == (138, 89),
            "camp furnish: the file's (0, 0) lies before the area's box by the room's first open cell");
        var empty = new BanditCampRoom("e", new BanditCampCell[2, 2], Array.Empty<BanditCampBuilding>(), Array.Empty<BanditCampItem>(),
            Array.Empty<BanditCampSpawn>(), Array.Empty<BanditCampFeature>(), 0, 0);
        Check(BanditCampFurnishRules.FirstOpenCell(empty) == (0, 0), "camp furnish: a room without open cells keeps its origin");

        var loot = BanditCampFurnishRules.WagonLoot;
        Check(loot.Length == 6 && loot.Select(entry => entry.Key).Distinct().Count() == 6 && loot.All(entry => entry.Amount >= 1)
                && loot.Any(entry => entry.Key == LifebloomBookRules.ItemKey && entry.Amount == 1)
                && loot.All(entry => entry.Key != "ITEM_CogWheel" && entry.Key != "ITEM_TechBook"),
            "camp furnish: the wagon drops Fre-yin's loot, one Lifebloom book, no cogwheel and none of the wagon's own loot");
        Check(loot.Sum(entry => entry.Amount) == 20, "camp furnish: the wagon's loot is 6 cloth, 3 heavy cloth, 4 leather, 3 crystal, 3 coins and the book");
        Check(BanditCampFurnishRules.IsWagon("BUILDING_SupportWagonHard") && !BanditCampFurnishRules.IsWagon("BUILDING_WagonScraps1")
                && !BanditCampFurnishRules.IsWagon(null),
            "camp furnish: the camp's wagon is the game's support wagon of the hard setting, the wagon scraps are not");

        Check(BanditCampFurnishRules.SpawnedLine("Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4", 4, 137, 0, 2, 0, true)
                == "CORE_BANDITS_CAMP_SPAWNED camp=Combat:DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4 units=4 buildings=137 items=0 features=2 skipped=0 wagon=yes forced=0 doubled=0"
                && BanditCampFurnishRules.SpawnedLine("c", 4, 137, 0, 2, 0, false, 3, 1).EndsWith(" skipped=0 wagon=no forced=3 doubled=1", StringComparison.Ordinal)
                && BanditCampFurnishRules.FloorsLine("c", 31, 0, 1).EndsWith(" floors=31 decos=0 skipped=1", StringComparison.Ordinal)
                && BanditCampFurnishRules.FloorsLine("c", 31, 0, 1).StartsWith("CORE_BANDITS_CAMP_FLOORS camp=", StringComparison.Ordinal)
                && BanditCampFurnishRules.WagonLine("abc", 6, 20) == "CORE_BANDITS_CAMP_WAGON wagon=abc kinds=6 dropped=20",
            "camp furnish: the spawned and the wagon line");
    }
}
