using System.Globalization;
using DungeonSettlersDelvers.Core;
using static TestKit;

// Building the camp into a floor, the pure part (BanditCampBuildRules): ids the game's load can trace back to its room table,
// the anchor's floor box, the joint onto the anchor's floor and the log lines.
internal static class BanditCampBuildSection
{
    internal static void Run()
    {
        Ids();
        AnchorBox();
        Joint();
        Lines();
        Levels();
    }

    // The rule in use: the smallest that brings the camp onto about 30 % of the floors.
    static void Levels()
    {
        Check(BanditCampBuildRules.InUse.MaxLength == BanditCampPlacement.MaxLength && BanditCampBuildRules.InUse.OtherSide
                && !BanditCampBuildRules.InUse.AlphaFloors && !BanditCampBuildRules.InUse.MainPath,
            "camp build: corridors up to 40 columns and the other side; the Alpha's floors and the mandatory path stay without");
        Check(BanditCampBuildRules.Variants(BanditCampChoice.Normal, true, true).SequenceEqual(new[] { BanditCampChoice.Normal, BanditCampChoice.Mirrored })
                && BanditCampBuildRules.Variants(BanditCampChoice.Mirrored, true, true).SequenceEqual(new[] { BanditCampChoice.Mirrored, BanditCampChoice.Normal })
                && BanditCampBuildRules.Variants(BanditCampChoice.Normal, true, false).SequenceEqual(new[] { BanditCampChoice.Normal })
                && BanditCampBuildRules.Variants(BanditCampChoice.Mirrored, true, false).SequenceEqual(new[] { BanditCampChoice.Mirrored, BanditCampChoice.Normal })
                && BanditCampBuildRules.Variants(BanditCampChoice.Mirrored, false, true).SequenceEqual(new[] { BanditCampChoice.Mirrored }),
            "camp build: the floor's variant comes first, the other side only when the level allows it and its file is there");
    }

    static void Ids()
    {
        var ids = new[]
        {
            "Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_3", "Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle1_9",
            "Corridor - DUNGEONROOM_VeilSilence_Common_HorizontalCorridor_2_12", "Entry - DUNGEONROOM_VeilSilence_Common_Entry_1"
        };
        var camp = BanditCampBuildRules.NextId(BanditCampBuildRules.CampTypeName, BanditCampBuildRules.CampKey, ids);
        var corridor = BanditCampBuildRules.NextId(BanditCampBuildRules.CorridorTypeName, BanditCampBuildRules.CorridorKey, ids);
        Check(camp == "Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4" && corridor == "Corridor - DUNGEONROOM_VeilSilence_Common_HorizontalCorridor_2_13",
            "camp build: the ids take the game's form and the next free number of their key");
        Check(BanditCampBuildRules.NextId("Combat", BanditCampBuildRules.CampKey, Array.Empty<string>()).EndsWith("_1", StringComparison.Ordinal)
                && BanditCampBuildRules.NumberOf("Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_x", BanditCampBuildRules.CampKey) == 0
                && BanditCampBuildRules.NumberOf(ids[1], BanditCampBuildRules.CampKey) == 0,
            "camp build: a floor without that key starts at 1; another key or no number does not count");
        Check(TableKeyOf(camp) == BanditCampBuildRules.CampKey && TableKeyOf(corridor) == BanditCampBuildRules.CorridorKey
                && TableKeyOf("Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle3_3f_7") == "DUNGEONROOM_VeilSilence_Common_Combat_Middle3_3f"
                && TableKeyOf("no key") == null,
            "camp build: the game's load finds the table key again in both ids");
    }

    // The table key the game reads from an id when it loads the floor (RuntimeDungeonData.OnLoadMonsterGroup): cut before
    // "DUNGEONROOM", drop the number after the last underscore; null without "DUNGEONROOM".
    static string TableKeyOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var at = id.IndexOf("DUNGEONROOM", StringComparison.Ordinal);
        if (at < 0) return null;
        var key = id.Substring(at);
        var underscore = key.LastIndexOf('_');
        return underscore > 0 && int.TryParse(key.Substring(underscore + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? key.Substring(0, underscore) : key;
    }

    static void AnchorBox()
    {
        var box = BanditCampBuildRules.AnchorBox(new[] { (10, 5), (14, 9), (12, 3), (11, 7) });
        Check(box != null && box.Value.MinX == 10 && box.Value.MinY == 3 && box.Value.MaxX == 14 && box.Value.MaxY == 9
                && BanditCampBuildRules.AnchorBox(Array.Empty<(int, int)>()) == null,
            "camp build: the anchor's box spans its floor cells; none gives none");
    }

    // A rectangle anchor at x 100..117, y 50..61 with a notch: rows 50..53 start at x 104.
    static bool Floor(int x, int y) => x <= 117 && y >= 50 && y <= 61 && x >= (y <= 53 ? 104 : 100);

    static void Joint()
    {
        var room = Camp();
        var map = new bool[300, 300];
        for (var x = 0; x < 300; x++)
            for (var y = 0; y < 300; y++)
                map[x, y] = !(x >= 98 && x <= 119 && y >= 48 && y <= 65);
        var plan = BanditCampPlacement.Place(room, new BanditCampAnchor(100, 50, 117, 61), map);
        Check(plan.IsPlaced && BanditCampBuildRules.JointOpensOnFloor(plan, Floor), "camp build: a joint in the middle rows opens onto the anchor's floor");
        var notch = BanditCampPlan.Found(BanditCampSide.Right, 4, 0, 0, 0, 50, new List<(int, int)>(), new List<(int, int)> { (98, 50), (99, 50), (98, 51), (99, 51) },
            new List<BanditCampWrite>());
        Check(!BanditCampBuildRules.JointOpensOnFloor(notch, Floor), "camp build: a joint onto the notch's wall does not open");
        var east = BanditCampPlan.Found(BanditCampSide.Left, 4, 0, 0, 0, 56, new List<(int, int)>(), new List<(int, int)> { (118, 56), (119, 56), (118, 57), (119, 57) },
            new List<BanditCampWrite>());
        Check(BanditCampBuildRules.JointOpensOnFloor(east, Floor) && !BanditCampBuildRules.JointOpensOnFloor(null, Floor),
            "camp build: a joint on the anchor's east wall opens onto its floor; no plan never does");
        Check(BanditCampBuildRules.JointFaces(east).SequenceEqual(new[] { (118, 58), (118, 59), (119, 58), (119, 59) })
                && BanditCampBuildRules.JointFaces(null).Count == 0,
            "camp build: above the joint two rows of the anchor's wall become wall face, in the joint's columns");
    }

    static void Lines()
    {
        var room = Camp();
        var map = new bool[300, 300];
        for (var x = 0; x < 300; x++)
            for (var y = 0; y < 300; y++)
                map[x, y] = !(x >= 98 && x <= 119 && y >= 48 && y <= 65);
        var plan = BanditCampPlacement.Place(room, new BanditCampAnchor(100, 50, 117, 61), map);
        var line = BanditCampBuildRules.PlacedLine(77, 0, 3, BanditCampChoice.Normal, "Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4",
            "Corridor - DUNGEONROOM_VeilSilence_Common_HorizontalCorridor_2_13", "Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle1_9", plan, room.Width, room.Height);
        Check(line.StartsWith("CORE_BANDIT_CAMP_PLACED seed=77 region=0 floor=3 camp=Combat:DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4 mirrored=no box=", StringComparison.Ordinal)
                && line.Contains(" entrance=" + plan.ExitX + "," + plan.ExitY + " ", StringComparison.Ordinal)
                && line.Contains(" neighbour=Combat:DUNGEONROOM_VeilSilence_Common_Combat_Middle1_9 corridor=Corridor:", StringComparison.Ordinal)
                && line.EndsWith(" length=" + plan.Length, StringComparison.Ordinal),
            "camp build: the placed line names camp, box, entrance, neighbour, corridor and its cells");
        var mapLine = BanditCampBuildRules.MapLine("Combat - DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4", plan, room.Width, room.Height, 200, -10);
        Check(mapLine.StartsWith("CORE_BANDIT_CAMP_MAP camp=Combat:DUNGEONROOM_VeilSilence_Common_Combat_Middle14_4 offset=200,-10 box="
                    + (plan.CampOriginX + 200) + "," + (plan.CampOriginY - 10) + ",", StringComparison.Ordinal)
                && mapLine.Contains(" entrance=" + (plan.ExitX + 200) + "," + (plan.ExitY - 10) + " ", StringComparison.Ordinal),
            "camp build: the map line gives the camp in the map's cells, the raw grid's moved by the offset");
        Check(BanditCampBuildRules.FailedLine(77, 0, 3, BanditCampPlacement.NoSpace, "a+b") == "CORE_BANDITS_CAMP_FAILED seed=77 region=0 floor=3 reason=no-space tried=a+b"
                && BanditCampBuildRules.FailedLine(1, 0, 3, null, null).EndsWith("reason=unknown tried=-", StringComparison.Ordinal),
            "camp build: the failed line names the reason and the anchors tried");
    }

    // A camp of 10 x 6 grass with its exit at the right edge in row 2.
    static BanditCampRoom Camp()
    {
        var cells = new BanditCampCell[10, 6];
        for (var x = 0; x < 10; x++)
            for (var y = 0; y < 6; y++)
                cells[x, y] = BanditCampCell.Grass;
        return new BanditCampRoom("small camp", cells, Array.Empty<BanditCampBuilding>(), Array.Empty<BanditCampItem>(), Array.Empty<BanditCampSpawn>(),
            Array.Empty<BanditCampFeature>(), 9, 2);
    }
}
