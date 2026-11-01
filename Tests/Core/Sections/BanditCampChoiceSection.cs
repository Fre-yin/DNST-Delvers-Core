using DungeonSettlersDelvers.Core;
using static TestKit;

// Whether the bandits' camp comes, its variant and the order of its anchors (BanditCampChoice): hand-made floors.
internal static class BanditCampChoiceSection
{
    const int E = BanditCampRules.EntryType, P = BanditCampRules.PortalType, C = BanditCampRules.CombatType;
    const int Region = BanditCampChoice.ForestRegion, Floor = BanditCampChoice.CampFloor;

    internal static void Run()
    {
        Gate();
        AlphaFloors();
        VariantIsDeterministic();
        VariantSplitsEvenly();
        MirroredNotAllowed();
        VariantIsItsOwnPick();
        NeverAnchors();
        AnchorOrder();
        TiesFollowTheHash();
        NoCampOnUnusableFloors();
        SameFloorSameDecision();
        ChoiceLines();
    }

    static BanditCampRules.Node Room(string id, int type, params string[] links)
        => new() { Id = id, Key = "K_" + id, Type = type, Links = links.ToList(),
            Monsters = type == C ? new[] { BanditCampRules.RandomSpawn } : Array.Empty<string>(),
            EnemyCells = type == C ? 5 : 0 };

    static BanditCampRules.Node Corridor(string id, string a, string b, int length, int width)
    {
        var node = new BanditCampRules.Node { Id = id, Key = "Corridor_" + length + "x" + width, Type = BanditCampRules.CorridorType, Links = new List<string> { a, b } };
        for (var x = 0; x < length; x++)
            for (var y = 0; y < width; y++) node.Cells.Add((x, y));
        return node;
    }

    // E-c1-A-c2-P is the mandatory path; D and T are dead ends, S a side room.
    static List<BanditCampRules.Node> DeadEndGraph() => new()
    {
        Room("E", E, "c1"), Room("A", C, "c1", "c2", "c3", "c4"), Room("P", P, "c2"),
        Room("D", C, "c3"), Room("S", C, "c4", "c5"), Room("T", C, "c5"),
        Corridor("c1", "E", "A", 3, 2), Corridor("c2", "A", "P", 3, 2), Corridor("c3", "A", "D", 8, 2),
        Corridor("c4", "A", "S", 3, 2), Corridor("c5", "S", "T", 5, 3)
    };

    // The same floor with the Alpha's elite room off the hub.
    static List<BanditCampRules.Node> EliteGraph()
    {
        var graph = DeadEndGraph();
        graph.First(n => n.Id == "A").Links.Add("c6");
        graph.Add(Room("Al", BanditCampRules.EliteType, "c6"));
        graph.Add(Corridor("c6", "A", "Al", 4, 2));
        return graph;
    }

    // A combat room whose table row names the Alpha, but is not of the elite type.
    static List<BanditCampRules.Node> AlphaKeyGraph()
    {
        var graph = DeadEndGraph();
        graph.First(n => n.Id == "T").Monsters = new[] { "UNIT_AcidSlogel", BanditCampRules.AlphaKey };
        return graph;
    }

    // A hub with the entry, the portal, a quest room, a fixed list and three combat rooms off the path.
    static List<BanditCampRules.Node> MixedGraph()
    {
        var fixedRoom = Room("F", C, "c5");
        fixedRoom.Monsters = new[] { "UNIT_RedRat", "UNIT_RedRat" };
        return new()
        {
            Room("E", E, "c1"), Room("H", C, "c1", "c2", "c3", "c4", "c5", "c6", "c7"), Room("P", P, "c2"),
            Room("Q", C, "c3"), Room("X", C, "c4"), fixedRoom, Room("Y", C, "c6"), Room("Z", C, "c7"),
            Corridor("c1", "E", "H", 3, 2), Corridor("c2", "H", "P", 3, 2), Corridor("c3", "H", "Q", 6, 2),
            Corridor("c4", "H", "X", 4, 2), Corridor("c5", "H", "F", 3, 2), Corridor("c6", "H", "Y", 6, 2), Corridor("c7", "H", "Z", 5, 2)
        };
    }

    // Two dead ends of the same rank and corridor length.
    static List<BanditCampRules.Node> TieGraph() => new()
    {
        Room("E", E, "c1"), Room("H", C, "c1", "c2", "c3", "c4"), Room("P", P, "c2"), Room("D1", C, "c3"), Room("D2", C, "c4"),
        Corridor("c1", "E", "H", 3, 2), Corridor("c2", "H", "P", 3, 2), Corridor("c3", "H", "D1", 4, 2), Corridor("c4", "H", "D2", 4, 2)
    };

    static BanditCampChoice.Decision Decide(List<BanditCampRules.Node> graph, int seed, bool mirroredAllowed = true, params string[] questKeys)
        => BanditCampChoice.Decide(graph, new HashSet<string>(questKeys), 0, seed, Region, Floor, mirroredAllowed);

    static bool Excludes(BanditCampChoice.Decision d, string id, string reason)
        => d.Excluded.Any(x => x.Id == id && x.Reason == reason);

    static void Gate()
    {
        var inside = Decide(DeadEndGraph(), 7);
        var otherRegion = BanditCampChoice.Decide(DeadEndGraph(), new HashSet<string>(), 0, 7, 1, Floor, true);
        var otherFloor = BanditCampChoice.Decide(DeadEndGraph(), new HashSet<string>(), 0, 7, Region, Floor - 1, true);
        Check(Region == 0 && Floor == 3 && BanditCampChoice.CampPhase == 4 && BanditCampChoice.InGate(0, 3)
                && !BanditCampChoice.InGate(1, 3) && !BanditCampChoice.InGate(0, 2) && !BanditCampChoice.InGate(0, 4)
                && inside.CampComes && inside.Reason == null
                && !otherRegion.CampComes && otherRegion.Reason == "gate" && otherRegion.Anchors.Count == 0
                && !otherFloor.CampComes && otherFloor.Reason == "gate" && otherFloor.Anchors.Count == 0,
            "camp choice: only the forest's floor index 3 (phase 4) takes a camp");
    }

    static void AlphaFloors()
    {
        var elite = Decide(EliteGraph(), 7);
        var byKey = Decide(AlphaKeyGraph(), 7);
        var without = Decide(DeadEndGraph(), 7);
        Check(BanditCampChoice.HasAlpha(EliteGraph()) && BanditCampChoice.HasAlpha(AlphaKeyGraph()) && !BanditCampChoice.HasAlpha(DeadEndGraph())
                && !BanditCampChoice.HasAlpha(null) && !BanditCampChoice.HasAlpha(new List<BanditCampRules.Node>())
                && elite.Alpha && !elite.CampComes && elite.Reason == "alpha" && elite.Anchors.Count == 0
                && byKey.Alpha && !byKey.CampComes && byKey.Reason == "alpha" && byKey.Anchors.Count == 0
                && !without.Alpha && without.CampComes && without.Anchors.Count > 0,
            "camp choice: a floor with an elite room or a room naming the Alpha gets no camp, a floor without one does");
        // Only for tests: the force mode passes over the Alpha rule; the Alpha's room and its neighbour stay out as anchors.
        var forced = BanditCampChoice.Decide(AlphaKeyGraph(), new HashSet<string>(), 0, 7, Region, Floor, false, ignoreAlpha: true);
        Check(forced.Alpha && forced.Forced && forced.CampComes && forced.Anchors.Contains("D") && !forced.Anchors.Contains("T") && !forced.Anchors.Contains("S")
                && BanditCampChoice.ChoiceLine(forced).EndsWith(" reason=none forced=yes", StringComparison.Ordinal)
                && !Decide(DeadEndGraph(), 7).Forced && !BanditCampChoice.ChoiceLine(Decide(DeadEndGraph(), 7)).Contains("forced", StringComparison.Ordinal),
            "camp choice: the test's force mode gives a camp beside the Alpha, logged as forced, never beside its room or its neighbour");
        // A corridor whose row names the Alpha is no Alpha room.
        var corridorGraph = DeadEndGraph();
        corridorGraph.First(n => n.Id == "c3").Monsters = new[] { BanditCampRules.AlphaKey };
        Check(!BanditCampChoice.HasAlpha(corridorGraph), "camp choice: a corridor is never the Alpha's room");
    }

    static void VariantIsDeterministic()
    {
        var same = true;
        for (var seed = -50; seed < 50; seed++)
        {
            var a = BanditCampChoice.Variant(seed, Region, Floor);
            same &= a == BanditCampChoice.Variant(seed, Region, Floor) && (a == 0 || a == 1);
        }
        var d1 = Decide(DeadEndGraph(), 12345);
        var d2 = Decide(DeadEndGraph(), 12345);
        Check(same && d1.Variant == d2.Variant && d1.Variant == BanditCampChoice.Variant(12345, Region, Floor)
                && BanditCampChoice.VariantName(0) == "normal" && BanditCampChoice.VariantName(1) == "mirrored",
            "camp choice: the variant is 0 or 1 and the same for the same seed, region and floor");
    }

    static void VariantSplitsEvenly()
    {
        var mirrored = 0;
        for (var seed = 1; seed <= 1000; seed++) mirrored += BanditCampChoice.Variant(seed, Region, Floor);
        Console.WriteLine("INFO: camp variant over seeds 1 to 1000: mirrored " + mirrored + ", normal " + (1000 - mirrored));
        Check(mirrored >= 450 && mirrored <= 550, "camp choice: 1000 seeds split about half and half between normal and mirrored (" + mirrored + " mirrored)");
    }

    static void MirroredNotAllowed()
    {
        var allZero = true;
        var anyMirroredWhenAllowed = false;
        for (var seed = 1; seed <= 1000; seed++)
        {
            allZero &= BanditCampChoice.Variant(seed, Region, Floor, false) == BanditCampChoice.Normal
                && Decide(DeadEndGraph(), seed, false).Variant == BanditCampChoice.Normal;
            anyMirroredWhenAllowed |= BanditCampChoice.Variant(seed, Region, Floor, true) == BanditCampChoice.Mirrored;
        }
        Check(allZero && anyMirroredWhenAllowed && BanditCampChoice.Variant(5, Region, Floor, true) == BanditCampChoice.Variant(5, Region, Floor),
            "camp choice: while the mirrored form is not allowed the variant is always normal");
    }

    static void VariantIsItsOwnPick()
    {
        // The boss item pick (1 of 2 here) uses the same seed, region and floor; the camp's variant must not follow it.
        var agree = 0;
        for (var seed = 1; seed <= 1000; seed++)
            if (BanditCampChoice.Variant(seed, Region, Floor) == BossItemRules.ChosenIndex(seed, Region, Floor, 2)) agree++;
        Console.WriteLine("INFO: camp variant equals the boss item pick of 2 for " + agree + " of 1000 seeds");
        Check(agree >= 400 && agree <= 600, "camp choice: the variant does not follow the boss item pick (" + agree + " of 1000 agree)");
    }

    static void NeverAnchors()
    {
        var d = Decide(MixedGraph(), 3, true, "K_Q");
        Check(d.CampComes && d.Anchors.Count == 3 && d.Anchors.Contains("X") && d.Anchors.Contains("Y") && d.Anchors.Contains("Z")
                && !d.Anchors.Contains("E") && !d.Anchors.Contains("P") && !d.Anchors.Contains("Q") && !d.Anchors.Contains("H") && !d.Anchors.Contains("F")
                && Excludes(d, "E", "mainpath") && Excludes(d, "P", "mainpath") && Excludes(d, "H", "mainpath")
                && Excludes(d, "Q", "quest") && Excludes(d, "F", "fixed") && d.Excluded.Count == 5,
            "camp choice: never the entry, the portal, the mandatory path, a quest room or a fixed list; every exclusion has its reason");
        // The elite room never is an anchor: with it the floor has no anchors at all.
        var elite = Decide(EliteGraph(), 3);
        Check(elite.Anchors.Count == 0 && !elite.Anchors.Contains("Al"), "camp choice: the elite room is no anchor");
    }

    static void AnchorOrder()
    {
        // Dead ends first (D with the long corridor, then T), the side room S last.
        var d = Decide(DeadEndGraph(), 7);
        var result = BanditCampRules.Evaluate(DeadEndGraph(), new HashSet<string>(), 0, 7);
        Check(d.Anchors.SequenceEqual(new[] { "D", "T", "S" }) && d.Anchors[0] == result.Picked.Node.Id
                && Excludes(d, "E", "mainpath") && Excludes(d, "A", "mainpath") && Excludes(d, "P", "mainpath") && d.Excluded.Count == 3,
            "camp choice: the anchors follow Evaluate's rank, the first is the room Evaluate picks");
        // Mixed floor: Y (6 long) before Z (5) before X (4), all dead ends.
        var m = Decide(MixedGraph(), 3, true, "K_Q");
        Check(m.Anchors.SequenceEqual(new[] { "Y", "Z", "X" }), "camp choice: of equal rank the longer access corridor comes first");
        // A long-corridor demand moves the dead end with the short corridor behind the long one, never out.
        var longOnly = BanditCampChoice.Decide(DeadEndGraph(), new HashSet<string>(), 8, 7, Region, Floor, true);
        Check(longOnly.Anchors.SequenceEqual(new[] { "D", "T", "S" }), "camp choice: rank 2 rooms stay in the list, behind rank 1");
    }

    static void TiesFollowTheHash()
    {
        var ok = true;
        var sawBoth = new HashSet<string>();
        for (var seed = 1; seed <= 60; seed++)
        {
            var d = Decide(TieGraph(), seed);
            var first = BanditCampRules.TieHash(seed, "D1") < BanditCampRules.TieHash(seed, "D2") ? "D1" : "D2";
            var second = first == "D1" ? "D2" : "D1";
            ok &= d.Anchors.SequenceEqual(new[] { first, second });
            sawBoth.Add(d.Anchors[0]);
        }
        Check(ok && sawBoth.Count == 2, "camp choice: a tie is broken by the hash of seed and room id, and the seed moves it");
    }

    static void NoCampOnUnusableFloors()
    {
        // Rooms without their corridors; the only combat room on the mandatory path; no way to the portal.
        var rooms = new List<BanditCampRules.Node> { Room("E", E, "c1"), Room("A", C, "c1", "c2"), Room("P", P, "c2"), Room("D", C, "c3") };
        var onlyPath = new List<BanditCampRules.Node>
        {
            Room("E", E, "c1"), Room("A", C, "c1", "c2"), Room("P", P, "c2"), Corridor("c1", "E", "A", 3, 2), Corridor("c2", "A", "P", 3, 2)
        };
        var cutOff = new List<BanditCampRules.Node>
        {
            Room("E", E, "c1"), Room("A", C, "c1", "c3"), Room("P", P), Room("D", C, "c3"), Corridor("c1", "E", "A", 3, 2), Corridor("c3", "A", "D", 8, 2)
        };
        var empty = Decide(rooms, 1);
        var path = Decide(onlyPath, 1);
        var cut = Decide(cutOff, 1);
        var none = Decide(new List<BanditCampRules.Node>(), 1);
        Check(empty.Reason == "graph-empty" && empty.Anchors.Count == 0 && empty.Excluded.Count == 4 && Excludes(empty, "D", "graph")
                && path.Reason == "no-anchor" && path.Anchors.Count == 0 && Excludes(path, "A", "mainpath")
                && cut.Reason == "graph-no-path" && cut.Anchors.Count == 0
                && none.Reason == "graph-empty" && !none.CampComes,
            "camp choice: a floor without usable graph or without a fitting room gets no camp, with the reason");
    }

    static void SameFloorSameDecision()
    {
        var a = Decide(DeadEndGraph(), 99, true);
        var b = Decide(DeadEndGraph(), 99, true);
        var c = Decide(TieGraph(), 99, true);
        var d = Decide(TieGraph(), 99, true);
        Check(a.Anchors.SequenceEqual(b.Anchors) && a.Variant == b.Variant && a.Alpha == b.Alpha && a.Reason == b.Reason
                && a.Excluded.SequenceEqual(b.Excluded) && c.Anchors.SequenceEqual(d.Anchors)
                && BanditCampChoice.ChoiceLine(a) == BanditCampChoice.ChoiceLine(b),
            "camp choice: the same seed, region and floor give the same decision");
    }

    static void ChoiceLines()
    {
        var d = Decide(DeadEndGraph(), 7);
        var name = BanditCampChoice.VariantName(BanditCampChoice.Variant(7, Region, Floor));
        var elite = Decide(EliteGraph(), 7, false);
        var spaced = new BanditCampChoice.Decision { Seed = 5, Region = 0, Floor = 3, Anchors = new List<string> { "Combat - X_5" }, Variant = 1 };
        Check(BanditCampChoice.ChoiceLine(d) == "CORE_BANDITS_CAMP_CHOICE seed=7 region=0 floor=3 alpha=no variant=" + name + " candidates=3 first=D reason=none"
                && BanditCampChoice.ChoiceLine(elite) == "CORE_BANDITS_CAMP_CHOICE seed=7 region=0 floor=3 alpha=yes variant=normal candidates=0 first=none reason=alpha"
                && BanditCampChoice.ChoiceLine(spaced) == "CORE_BANDITS_CAMP_CHOICE seed=5 region=0 floor=3 alpha=no variant=mirrored candidates=1 first=Combat:X_5 reason=none",
            "camp choice: the log line names seed, region, floor, alpha, variant, candidates, first anchor and reason, without blanks");
    }
}
