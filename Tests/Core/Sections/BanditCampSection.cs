using DungeonSettlersDelvers.Core;
using static TestKit;

// The bandits' camp room (BanditCampRules): hand-made floors. Rooms link to corridors, corridors to their two rooms;
// a corridor's cells give its length and width.
internal static class BanditCampSection
{
    const int E = BanditCampRules.EntryType, P = BanditCampRules.PortalType, C = BanditCampRules.CombatType;

    internal static void Run()
    {
        MandatoryPath();
        Cycle();
        OnlyCombatRoomOnPath();
        Tie();
        FixedExclusions();
        UnusableGraphs();
        IdsWithBlanks();
        SeedScanLines();
        CorridorSize();
    }

    static BanditCampRules.Node Room(string id, int type, params string[] links)
        => new() { Id = id, Key = "K_" + id, Type = type, Links = links.ToList(),
            Monsters = type == BanditCampRules.CombatType ? new[] { BanditCampRules.RandomSpawn } : Array.Empty<string>(),
            EnemyCells = type == BanditCampRules.CombatType ? 5 : 0 };

    static BanditCampRules.Node Corridor(string id, string a, string b, int length, int width)
    {
        var node = new BanditCampRules.Node { Id = id, Key = "Corridor_" + length + "x" + width, Type = BanditCampRules.CorridorType, Links = new List<string> { a, b } };
        for (var x = 0; x < length; x++)
            for (var y = 0; y < width; y++) node.Cells.Add((x, y));
        return node;
    }

    static BanditCampRules.Room Of(BanditCampRules.Result r, string id) => r.Rooms.First(room => room.Node.Id == id);

    static BanditCampRules.Result EvaluateWithoutQuestRooms(List<BanditCampRules.Node> graph, int minRank, int seed)
        => BanditCampRules.Evaluate(graph, new HashSet<string>(), minRank, seed);

    // E-c1-A-c2-P is the mandatory path; D and T are dead ends, S a side room.
    static List<BanditCampRules.Node> DeadEndGraph() => new()
    {
        Room("E", E, "c1"), Room("A", C, "c1", "c2", "c3", "c4"), Room("P", P, "c2"),
        Room("D", C, "c3"), Room("S", C, "c4", "c5"), Room("T", C, "c5"),
        Corridor("c1", "E", "A", 3, 2), Corridor("c2", "A", "P", 3, 2), Corridor("c3", "A", "D", 8, 2),
        Corridor("c4", "A", "S", 3, 2), Corridor("c5", "S", "T", 5, 3)
    };

    // Two shortest paths E-A-P and E-B-P (both on the path), a longer loop E-X-Y-P (off it).
    static List<BanditCampRules.Node> CycleGraph() => new()
    {
        Room("E", E, "c1", "c3", "c6"), Room("A", C, "c1", "c2"), Room("B", C, "c3", "c4", "c5"), Room("P", P, "c2", "c4", "c8"),
        Room("Cd", C, "c5"), Room("X", C, "c6", "c7"), Room("Y", C, "c7", "c8"),
        Corridor("c1", "E", "A", 3, 2), Corridor("c2", "A", "P", 3, 2), Corridor("c3", "E", "B", 3, 2), Corridor("c4", "B", "P", 3, 2),
        Corridor("c5", "B", "Cd", 4, 2), Corridor("c6", "E", "X", 3, 2), Corridor("c7", "X", "Y", 3, 2), Corridor("c8", "Y", "P", 3, 2)
    };

    // The only combat room lies on the mandatory path.
    static List<BanditCampRules.Node> OnlyPathGraph() => new()
    {
        Room("E", E, "c1"), Room("A", C, "c1", "c2"), Room("P", P, "c2"), Corridor("c1", "E", "A", 3, 2), Corridor("c2", "A", "P", 3, 2)
    };

    // Two dead ends of the same rank and corridor length.
    static List<BanditCampRules.Node> TieGraph() => new()
    {
        Room("E", E, "c1"), Room("H", C, "c1", "c2", "c3", "c4"), Room("P", P, "c2"), Room("D1", C, "c3"), Room("D2", C, "c4"),
        Corridor("c1", "E", "H", 3, 2), Corridor("c2", "H", "P", 3, 2), Corridor("c3", "H", "D1", 4, 2), Corridor("c4", "H", "D2", 4, 2)
    };

    // The Alpha's elite room, a room one corridor away from it, a quest room, a fixed spawn list, too few monster cells.
    static List<BanditCampRules.Node> ExclusionsGraph()
    {
    var alpha = Room("Al", BanditCampRules.EliteType, "c3", "c4");
    alpha.Monsters = new[] { "UNIT_AcidSlogel", BanditCampRules.AlphaKey };
    var fixedRoom = Room("F", C, "c6");
    fixedRoom.Monsters = new[] { "UNIT_RedRat", "UNIT_RedRat" };
    var small = Room("Sm", C, "c7");
    small.EnemyCells = 3;
    var unknown = Room("U", C, "c8");
    unknown.EnemyCells = -1;
    var exclusions = new List<BanditCampRules.Node>
    {
        Room("E", E, "c1"), Room("H", C, "c1", "c2", "c3", "c5", "c6", "c7", "c8"), Room("P", P, "c2"), alpha, Room("N", C, "c4"),
        Room("Q", C, "c5"), fixedRoom, small, unknown,
        Corridor("c1", "E", "H", 3, 2), Corridor("c2", "H", "P", 3, 2), Corridor("c3", "H", "Al", 3, 2), Corridor("c4", "Al", "N", 3, 2),
        Corridor("c5", "H", "Q", 3, 2), Corridor("c6", "H", "F", 3, 2), Corridor("c7", "H", "Sm", 3, 2), Corridor("c8", "H", "U", 3, 2)
    };
        return exclusions;
    }

    static BanditCampRules.Result EvaluateExclusions()
        => BanditCampRules.Evaluate(ExclusionsGraph(), new HashSet<string> { "K_Q" }, 0, 1);

    // The rooms without their corridors: every room has degree 0.
    static List<BanditCampRules.Node> RoomsOnlyGraph() => new()
    {
        Room("E", E, "c1"), Room("A", C, "c1", "c2", "c3"), Room("P", P, "c2"), Room("D", C, "c3")
    };

    // Corridors, but the portal cannot be reached from the entry.
    static List<BanditCampRules.Node> PortalCutOffGraph() => new()
    {
        Room("E", E, "c1"), Room("A", C, "c1", "c3"), Room("P", P), Room("D", C, "c3"),
        Corridor("c1", "E", "A", 3, 2), Corridor("c3", "A", "D", 8, 2)
    };

    static void MandatoryPath()
    {
    // Dead ends and a side room: E-c1-A-c2-P is the mandatory path; D and T are dead ends, S a side room.
        var r1 = EvaluateWithoutQuestRooms(DeadEndGraph(), 0, 7);
        var r2 = EvaluateWithoutQuestRooms(DeadEndGraph(), 6, 7);
        var r3 = EvaluateWithoutQuestRooms(DeadEndGraph(), 10, 7);
    Check(r1.Entry == "E" && r1.Portal == "P" && r1.PathLength == 4 && r1.Corridors == 5 && r1.Rooms.Count == 6
            && Of(r1, "A").Reason == "mainpath" && Of(r1, "E").Reason == "mainpath" && Of(r1, "P").Reason == "mainpath"
            && Of(r1, "D").Degree == 1 && Of(r1, "S").Degree == 2 && Of(r1, "A").Degree == 4
            && Of(r1, "D").CorridorLength == 8 && Of(r1, "D").CorridorWidth == 2 && Of(r1, "S").CorridorLength == 5
            && Of(r1, "D").Rank == 1 && Of(r1, "T").Rank == 1 && Of(r1, "S").Rank == 3 && r1.Candidates == 3 && r1.Picked?.Node.Id == "D"
            && Of(r2, "D").Rank == 1 && Of(r2, "T").Rank == 2 && r2.Picked?.Node.Id == "D"
            && Of(r3, "D").Rank == 2 && Of(r3, "T").Rank == 2 && r3.Picked?.Node.Id == "D",
        "camp: never on the mandatory path; dead ends first, the long corridor wins, side rooms last");
    }

    static void Cycle()
    {
    // A cycle: two shortest paths E-A-P and E-B-P (both on the path), a longer loop E-X-Y-P (off it).
        var rc = EvaluateWithoutQuestRooms(CycleGraph(), 0, 1);
    Check(rc.PathLength == 4 && Of(rc, "A").OnPath && Of(rc, "B").OnPath && !Of(rc, "X").OnPath && !Of(rc, "Y").OnPath
            && Of(rc, "X").Rank == 3 && Of(rc, "Cd").Rank == 1 && rc.Picked?.Node.Id == "Cd",
        "camp: in a cycle every shortest path counts as mandatory; a longer loop stays a side way");
    }

    static void OnlyCombatRoomOnPath()
    {
    // The only combat room lies on the mandatory path: no camp on this floor.
        var ro = EvaluateWithoutQuestRooms(OnlyPathGraph(), 0, 1);
    Check(ro.Candidates == 0 && ro.Picked == null && Of(ro, "A").Reason == "mainpath"
            && BanditCampRules.HeaderLine(ro, 0, 3, 4, 1, 0).Contains("picked=none rank=0 corridor=-"),
        "camp: when every combat room lies on the mandatory path there is no camp (picked=none)");
    // The seed scan's widest measure lets a room on the mandatory path anchor the camp; entry and portal stay out by their type.
    var wide = BanditCampRules.Evaluate(OnlyPathGraph(), new HashSet<string>(), 0, 1, allowMainPath: true);
    Check(wide.Candidates == 1 && Of(wide, "A").Reason == null && Of(wide, "E").Reason == "type" && Of(wide, "P").Reason == "type",
        "camp: with anchors on the mandatory path allowed, the path's combat room is a candidate, entry and portal are not");
    }

    static void Tie()
    {
    // A tie (same rank, same corridor): the seed decides, and the same seed always picks the same room.
        var t1 = EvaluateWithoutQuestRooms(TieGraph(), 0, 42);
        var t2 = EvaluateWithoutQuestRooms(TieGraph(), 0, 42);
    var expected = BanditCampRules.TieHash(42, "D1") < BanditCampRules.TieHash(42, "D2") ? "D1" : "D2";
    Check(t1.Picked?.Node.Id == expected && t2.Picked?.Node.Id == expected,
        "camp: a tie is broken by a hash of seed and room id, the same for the same floor");
    }

    static void FixedExclusions()
    {
        var re = EvaluateExclusions();
    Check(re.Alpha == "Al" && Of(re, "Al").Reason == "elite" && Of(re, "N").Reason == "neighbour" && Of(re, "Q").Reason == "quest"
            && Of(re, "F").Reason == "fixed" && Of(re, "Sm").Reason == "small" && Of(re, "U").Reason == null && re.Picked?.Node.Id == "U"
            && BanditCampRules.RoomLine(1, Of(re, "N")).Contains("reason=neighbour")
            && BanditCampRules.RoomLine(1, Of(re, "U")).Contains("enemyCells=-1 reason=candidate rank=1 corridor=Corridor_3x2:3x2"),
        "camp: never the Alpha's room, its neighbours (one corridor away), quest rooms, fixed lists or rooms too small");
    }

    static void UnusableGraphs()
    {
    // An unusable graph is no result (the first probe in the game passed the rooms without
    // their corridors, every room had degree 0): nothing is picked, the probe logs FAILED.
        var rEmpty = EvaluateWithoutQuestRooms(RoomsOnlyGraph(), 0, 1);
        var rNone = EvaluateWithoutQuestRooms(new List<BanditCampRules.Node>(), 0, 1);
        var rCut = EvaluateWithoutQuestRooms(PortalCutOffGraph(), 0, 1);
        var r1 = EvaluateWithoutQuestRooms(DeadEndGraph(), 0, 7);
        var rc = EvaluateWithoutQuestRooms(CycleGraph(), 0, 1);
        var ro = EvaluateWithoutQuestRooms(OnlyPathGraph(), 0, 1);
        var re = EvaluateExclusions();
    Check(rEmpty.Problem == "empty" && rEmpty.Picked == null && rEmpty.Candidates == 0 && rEmpty.Links == 0 && rEmpty.PathLength == -1
            && Of(rEmpty, "D").Reason == "graph" && Of(rEmpty, "D").Degree == 0 && Of(rEmpty, "A").Reason == "graph"
            && rNone.Problem == "empty" && rNone.Picked == null
            && rCut.Problem == "no-path" && rCut.Picked == null && rCut.Candidates == 0 && rCut.Links == 4 && Of(rCut, "D").Reason == "graph"
            && r1.Problem == null && r1.Links == 10 && rc.Problem == null && ro.Problem == null && re.Problem == null
            && BanditCampRules.FailedLine(rEmpty, 1) == "CORE_BANDITS_GRAPH_FAILED reason=empty seed=1 rooms=4 corridors=0 links=0 entry=E portal=P mainpath=-1"
            && BanditCampRules.HeaderLine(rEmpty, 0, 4, 4, 1, 0).Contains("mainpath=-1 candidates=0 picked=none rank=0 corridor=-"),
        "camp: a graph without corridor links or without a way to the portal is no result (FAILED, nothing picked)");
    Check(!BanditCampRules.ScanResultFinal(true, false, 0, 0) && !BanditCampRules.ScanResultFinal(false, true, 3, 0)
        && BanditCampRules.ScanResultFinal(true, true, 1, 0) && BanditCampRules.ScanResultFinal(true, false, 2, 9)
        && BanditCampRules.ScanResultFinal(true, false, 0, 7),
    "camp: the seed scan waits past the game's empty placeholder result for the final one");
    }

    static void IdsWithBlanks()
    {
    // The game's ids carry blanks ("Combat - X_5"); the log lines do not.
    var spaced = new BanditCampRules.Room { Node = Room("Combat - X 5", C) };
    Check(BanditCampRules.LogId("Combat - DUNGEONROOM_VeilSilence_Common_Combat_NonTree4_5") == "Combat:DUNGEONROOM_VeilSilence_Common_Combat_NonTree4_5"
            && BanditCampRules.LogId("a b\tc") == "a_b_c" && BanditCampRules.LogId(null) == "-" && BanditCampRules.LogId("") == "-"
            && BanditCampRules.RoomLine(1, spaced).Contains("id=Combat:X_5 key=K_Combat:X_5 type=2 ")
            && BanditCampRules.CorridorLine(1, 4, "Corridor - L 3", 8, 2, 1).Contains("key=Corridor:L_3 size=8x2"),
        "camp: ids and keys in the graph lines carry no blanks (Combat - X_5 becomes Combat:X_5)");
    }

    static void SeedScanLines()
    {
    // Step 1 of the camp with its own corridor: template, graph and seed scan lines.
        var r1 = EvaluateWithoutQuestRooms(DeadEndGraph(), 0, 7);
        var ro = EvaluateWithoutQuestRooms(OnlyPathGraph(), 0, 1);
    var scan = new BanditCampRules.SeedScanSummary();
    scan.Add(true, "None", r1);
    scan.Add(true, "None", ro);
    scan.Add(false, "ExtractFailed", null);
    scan.Add(false, "timeout", null);
    scan.Add(false, "ExtractFailed", null);
    Check(BanditCampRules.TemplateLine(4, 2, "DUNGEONROOM_VeilSilence_Common_Combat_Middle14", 150, 14, 9, 0, 0, 4, 38, 2)
            == "CORE_BANDITS_TEMPLATE phase=4 type=2 key=DUNGEONROOM_VeilSilence_Common_Combat_Middle14 weight=150 size=14x9 repeat=allow doors=simple lines=4 starts=38 doorLength=2"
            && BanditCampRules.TemplateLine(4, 99, "Corridor - X", 100, 4, 2, 2, 1, -1, -1, 2).Contains("key=Corridor:X ")
            && BanditCampRules.TemplateLine(4, 99, "c", 100, 4, 2, 7, 9, -1, -1, 2).Contains("repeat=- doors=- lines=-1 starts=-1")
            && BanditCampRules.LevelGraphLine(4, "Phase4 Graph", 1.5f, 13, 12, new SortedDictionary<string, int>(StringComparer.Ordinal) { ["Combat"] = 8, ["Entry"] = 1 })
                == "CORE_BANDITS_LEVELGRAPH phase=4 name=Phase4_Graph weight=1.5 rooms=13 connections=12 types=Combat8+Entry1"
            && BanditCampRules.SeedScanLine(7, true, "None", 7, 1, r1, 2.4)
                == "CORE_BANDITS_SEEDSCAN seed=7 ok=1 reason=None used=7 attempts=1 rooms=6 corridors=5 mainpath=4 candidates=3 seconds=2.4"
            && BanditCampRules.SeedScanLine(8, false, "timeout", 0, 0, null, 60).Contains("ok=0 reason=timeout used=0 attempts=0 rooms=- corridors=- mainpath=- candidates=-")
            && BanditCampRules.SeedScanDoneText(scan) == "count=5 ok=2 failed=3 reasons=ExtractFailed:2,timeout:1 meanMainpath=4.0 withCandidates=1"
            && BanditCampRules.SeedScanDoneText(new BanditCampRules.SeedScanSummary()) == "count=0 ok=0 failed=0 reasons=- meanMainpath=- withCandidates=0"
            && CampCounted(),
        "camp: templates, level graphs and the seed scan log in one line each, without blanks");
    }

    static void CorridorSize()
    {
    Check(BanditCampRules.Size(new List<(int X, int Y)> { (5, 1), (6, 1), (7, 1), (8, 1), (5, 2), (8, 2) }) == (4, 2)
            && BanditCampRules.Size(new List<(int X, int Y)> { (1, 1), (1, 2), (1, 3), (2, 3) }) == (3, 2)
            && BanditCampRules.Size(new List<(int X, int Y)>()) == (0, 0)
            && BanditCampRules.CorridorLine(5, 4, "Corridor_8x2", 8, 2, 3) == "CORE_BANDITS_GRAPH_CORRIDOR seed=5 phase=4 key=Corridor_8x2 size=8x2 count=3",
        "camp: a corridor's length is the longer, its width the shorter side of its cells");
    }

    // The seed scan counts whether the camp would come under the rule in use.
    static bool CampCounted()
    {
        var scan = new BanditCampRules.SeedScanSummary();
        scan.AddCamp(null);
        scan.AddCamp("no-space");
        scan.AddCamp(null);
        return BanditCampRules.SeedScanDoneText(scan).EndsWith(" camp=no-space:1,placed:2", StringComparison.Ordinal);
    }
}
