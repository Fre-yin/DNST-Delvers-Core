using DungeonSettlersDelvers.Core;
using static TestKit;

// The zone book of GroupAiZones, the cells of an enemy area and the choices of the zone lock, as rules without the game.
internal static class GroupAiZoneRulesSection
{
    const string Players = "players";
    const string Bandits = "bandits";
    static readonly IntPtr ExecA = new(101);
    static readonly IntPtr ExecB = new(102);
    static readonly IntPtr ExecC = new(103);

    internal static void Run()
    {
        Marking();
        Joining();
        HoldEnd();
        Lookup();
        Counting();
        Ending();
        Queries();
        Cells();
        Lock();
    }

    static GroupAiAreaRules.Area Area(string source) => GroupAiAreaRules.BySource(source);

    static List<(int X, int Y)> Square(int x, int y, int radius) => GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, radius, x, y);

    static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    static void Marking()
    {
        var book = new GroupAiZoneRules.Book();
        var first = book.Mark(Players, "a", "S1", "F0", Square(0, 0, 1), 5f);
        var again = book.Mark(Players, "a", "S1", "F0", Square(10, 10, 1), 6f);
        var other = book.Mark(Players, "a", "S2", "F0", Square(0, 0, 1), 7f);
        Check(first.Id == 1 && again.Id == 2 && other.Id == 3 && book.Zones.Count == 2 && book.Zones[0] == again && book.Zones[1] == other
                && again.Holds(10, 10) && !again.Holds(0, 0) && again.Side == Players && again.Until == 6f && book.AreaZones == 0,
            "zone book: marking replaces the caster's earlier zone of the same skill, the ids count up, Core's own zones are no enemy areas");
        book.Reset();
        var after = book.Mark(Bandits, "b", "S1", "F0", Square(0, 0, 1), 5f);
        Check(book.Zones.Count == 1 && after.Id == 4 && book.AreaZones == 0,
            "zone book: a reset empties the book, the ids keep counting");
    }

    static void Joining()
    {
        var rain = Area("SKILL_ArrowRain");
        var chain = Area("SKILL_PhantomChain");
        var book = new GroupAiZoneRules.Book();
        var created = book.MarkArea(Players, ExecA, "boss", rain, Square(0, 0, 1), 10f, null, null, "e1");
        var zone = book.Zones[0];
        Check(created && book.AreaZones == 1 && zone.Side == Players && zone.Skill == rain.Source && zone.Flag == "F8" && zone.Caster == "boss"
                && zone.ExecId == "e1" && zone.StartedAt == 10f && zone.LastHit == rain.LastHit && zone.Lane == null && zone.Execs.Count == 1
                && Near(zone.Until, GroupAiAreaRules.HoldUntil(10f, rain.LastHit)) && Near(GroupAiAreaRules.HoldUntil(10f, 3f), 13.3f),
            "zone book: an enemy area is a zone of the party's side held to its last query plus 0.3 s, and counts as one enemy area");
        var joined = book.MarkArea(Players, ExecB, "boss", rain, Square(5, 5, 1), 11f, null, 4.9f, "e2");
        var untilAfterJoin = zone.Until;
        var shorter = book.MarkArea(Players, ExecC, "boss", rain, Square(8, 8, 1), 11.5f, null, 0.5f, "e3");
        Check(!joined && !shorter && book.Zones.Count == 1 && book.AreaZones == 1 && zone.Execs.Count == 3 && zone.Holds(0, 0) && zone.Holds(5, 5) && zone.Holds(8, 8)
                && Near(untilAfterJoin, GroupAiAreaRules.HoldUntil(11f, 4.9f)) && zone.Until == untilAfterJoin && zone.ExecId == "e1" && zone.StartedAt == 10f,
            "zone book: a later execution of the caster's area joins its zone, adds its cells and execution, and the zone's end never moves earlier");
        var otherCaster = book.MarkArea(Players, ExecA, "boss2", rain, Square(0, 0, 1), 11f, null, null, "e4");
        var otherArea = book.MarkArea(Players, ExecA, "boss", chain, Square(0, 0, 1), 11f, (1, 0), null, null);
        Check(otherCaster && otherArea && book.Zones.Count == 3 && book.AreaZones == 3 && book.Zones[2].ExecId == "-" && book.Zones[2].Lane == (1, 0)
                && book.Zones[2].LastHit == chain.LastHit,
            "zone book: another caster or another area gets a zone of its own; without an execution id it is \"-\", the last query comes from the rule");
        var late = book.MarkArea(Players, ExecB, "boss", rain, Square(0, 0, 1), untilAfterJoin, null, null, "e5");
        Check(late && book.Zones.Count == 4 && book.AreaZones == 4,
            "zone book: an execution that starts when the caster's zone has ended opens a new zone");
    }

    // The end of a zone is (startedAt + last) + 0.3 as floats. startedAt + (last + 0.3) is another sum: for some pairs it differs in the
    // last bit, and the zone's until= in the log lines must stay the first one, so a HoldUntil built over HoldFor fails here.
    static void HoldEnd()
    {
        var chain = Area("SKILL_PhantomChain");
        float startedAt = 0f, last = 0f, expected = 0f, other = 0f;
        var found = false;
        for (var i = 1; i <= 20000 && !found; i++)
        {
            startedAt = i * 0.37f;
            foreach (var area in GroupAiAreaRules.Areas)
            {
                last = area.LastHit;
                float sum = startedAt + last;
                expected = sum + 0.3f;
                float margin = last + 0.3f;
                other = startedAt + margin;
                found = BitConverter.SingleToInt32Bits(expected) != BitConverter.SingleToInt32Bits(other);
                if (found) break;
            }
        }
        var book = new GroupAiZoneRules.Book();
        book.MarkArea(Players, ExecA, "boss", chain, Square(0, 0, 1), startedAt, null, last, "e1");
        Check(found && BitConverter.SingleToInt32Bits(GroupAiAreaRules.HoldUntil(startedAt, last)) == BitConverter.SingleToInt32Bits(expected)
                && BitConverter.SingleToInt32Bits(GroupAiAreaRules.HoldUntil(startedAt, last)) != BitConverter.SingleToInt32Bits(other)
                && BitConverter.SingleToInt32Bits(book.Zones[0].Until) == BitConverter.SingleToInt32Bits(expected)
                && GroupAiAreaRules.HoldMargin == 0.3f,
            "zone end: HoldUntil adds start, last hit and margin in this order, and a zone's until is exactly that sum, also for a pair where the other order differs in the last bit");
    }

    static void Lookup()
    {
        var book = new GroupAiZoneRules.Book();
        var early = book.Mark(Players, "k", "S1", "F1", Square(0, 0, 1), 5f);
        var late = book.Mark(Players, "m", "S2", "F2", Square(0, 0, 2), 9f);
        Check(book.ZoneAt(Players, "me", 0, 0, 1f) == early && book.ZoneAt(Players, "me", 2, 2, 1f) == late,
            "zone book: ZoneAt returns the first zone marked that holds the cell, not the later one or the one that ends later");
        Check(book.ZoneAt(Players, "k", 0, 0, 1f) == late && book.ZoneAt(Players, "m", 0, 0, 1f) == early && book.ZoneAt(Bandits, "me", 0, 0, 1f) == null
                && book.ZoneAt(Players, "me", 5, 5, 1f) == null,
            "zone book: ZoneAt skips the unit's own zones, other sides and cells outside");
        Check(book.ZoneAt(Players, "me", 0, 0, 5f) == late && book.Zones.Count == 1 && book.ZoneAt(Players, "me", 0, 0, 9f) == null && book.Zones.Count == 0,
            "zone book: ZoneAt drops the zones that have ended, a zone ends at its Until");
        var chain = Area("SKILL_PhantomChain");
        book.MarkArea(Players, ExecA, "boss", chain, Square(0, 0, 1), 10f, null, null, "e");
        Check(book.AreaZones == 1 && book.ZoneAt(Players, "me", 0, 0, 20f) == null && book.AreaZones == 0 && book.Zones.Count == 0,
            "zone book: an enemy area that has ended is dropped by ZoneAt and leaves the count");
    }

    static void Counting()
    {
        var rain = Area("SKILL_ArrowRain");
        var book = new GroupAiZoneRules.Book();
        book.MarkArea(Players, ExecA, "boss", rain, Square(0, 0, 1), 10f, null, null, "e");
        book.Clear("boss", "SKILL_Other");
        book.Clear("other", rain.Source);
        Check(book.Zones.Count == 1 && book.AreaZones == 1,
            "zone book: Clear keeps the zones of another caster or skill");
        book.Clear("boss", rain.Source);
        var endedLater = book.EndExecution(ExecA);
        Check(book.Zones.Count == 0 && book.AreaZones == 1 && !endedLater && book.AreaZones == 1,
            "zone book: Clear removes an enemy area but leaves the count of enemy areas as it was, and no later end lowers it");
        book.Reset();
        Check(book.AreaZones == 0, "zone book: a reset sets the count of enemy areas to 0");
    }

    static void Ending()
    {
        var rain = Area("SKILL_ArrowRain");
        var book = new GroupAiZoneRules.Book();
        book.Mark(Players, "k", "S1", "F1", Square(0, 0, 1), 9f);
        book.MarkArea(Players, ExecA, "boss", rain, Square(0, 0, 1), 10f, null, null, "e");
        book.MarkArea(Players, ExecB, "boss", rain, Square(1, 1, 1), 11f, null, null, "e2");
        Check(book.IsAreaExecution(ExecA) && book.IsAreaExecution(ExecB) && !book.IsAreaExecution(ExecC),
            "zone book: the executions of an enemy area are known, Core's own zones have none");
        var first = book.EndExecution(ExecA);
        var stillHeld = book.Zones.Count == 2 && book.AreaZones == 1 && !book.IsAreaExecution(ExecA) && book.IsAreaExecution(ExecB);
        var unknown = book.EndExecution(ExecC);
        var last = book.EndExecution(ExecB);
        Check(!first && stillHeld && !unknown && last && book.Zones.Count == 1 && book.Zones[0].Caster == "k" && book.AreaZones == 0,
            "zone book: an enemy area's zone goes with its last execution and leaves the count; other zones stay");
    }

    static void Queries()
    {
        var chain = Area("SKILL_PhantomChain");
        var rain = Area("SKILL_ArrowRain");
        var book = new GroupAiZoneRules.Book();
        book.Mark(Players, "k", "S1", "F1", Square(50, 50, 1), 99f);
        Check(!book.InEnemyAreaCell(50, 50, 1f) && book.UntilCrossed(new List<(int X, int Y)> { (50, 50) }, 1f) == 0f && book.ZoneExecOf("k", 1f) == null,
            "zone book: no enemy area held, no cell is in one, nothing is crossed (Core's own zones do not count)");
        book.MarkArea(Players, ExecA, "boss", chain, Square(0, 0, 1), 10f, null, null, "e1");
        book.MarkArea(Players, ExecB, "boss2", rain, Square(5, 5, 1), 10f, null, null, "e2");
        var chainEnd = book.Zones[1].Until;
        var rainEnd = book.Zones[2].Until;
        Check(book.InEnemyAreaCell(0, 0, 11f) && !book.InEnemyAreaCell(50, 50, 11f) && !book.InEnemyAreaCell(20, 20, 11f)
                && book.InEnemyAreaCell(0, 0, chainEnd - 0.01f) && !book.InEnemyAreaCell(0, 0, chainEnd),
            "zone book: a cell is in an enemy area only while that zone holds, not at its end");
        Check(Near(book.UntilCrossed(new List<(int X, int Y)> { (0, 0), (5, 5) }, 11f), Math.Max(chainEnd, rainEnd))
                && Near(book.UntilCrossed(new List<(int X, int Y)> { (20, 20), (5, 5) }, 11f), rainEnd)
                && book.UntilCrossed(new List<(int X, int Y)> { (20, 20) }, 11f) == 0f && book.UntilCrossed(new List<(int X, int Y)>(), 11f) == 0f
                && book.UntilCrossed(null, 11f) == 0f && book.UntilCrossed(new List<(int X, int Y)> { (0, 0) }, chainEnd) == 0f,
            "zone book: the cells' crossing ends at the latest end of the enemy areas they touch, 0 when none holds them");
        Check(book.ZoneExecOf("k", 11f) == null && book.UntilCrossed(new List<(int X, int Y)> { (50, 50) }, 11f) == 0f,
            "zone book: with enemy areas held, Core's own zone of a caster still names no execution and crosses nothing");
        Check(book.ZoneExecOf("boss", 11f) == "e1" && book.ZoneExecOf("boss2", 11f) == "e2" && book.ZoneExecOf("boss", chainEnd) == null
                && book.ZoneExecOf("nobody", 11f) == null && book.ZoneExecOf("", 11f) == null && book.ZoneExecOf(null, 11f) == null,
            "zone book: the execution of a caster's held enemy area is named by the zone, null once it has ended or for no caster");
        Check(book.InAnyZone(Players, 0, 0) && !book.InAnyZone(Bandits, 0, 0) && !book.InAnyZone(Players, 30, 30)
                && !book.InAnyZoneBut(Players, 0, 0, book.Zones[1]) && book.InAnyZoneBut(Players, 0, 0, book.Zones[2]),
            "zone book: a cell is in a zone of a side, or in one other than the zone the unit leaves");
    }

    static void Cells()
    {
        var charge = Area("SKILL_ElderGlothCharge");
        var lane = GroupAiZoneRules.LaneKeys(charge, 0, 0, 3, 0, 6);
        var longLane = GroupAiZoneRules.LaneKeys(charge, 0, 0, 3, 0, GroupAiZoneRules.DefaultLaneRange);
        Check(lane.Count == 24 && lane.Contains((7, 1)) && lane.Contains((0, -1)) && !lane.Contains((8, 0)) && !lane.Contains((-1, 0))
                && longLane.Count == 42 && longLane.Contains((13, 0)) && !longLane.Contains((14, 0)) && GroupAiZoneRules.DefaultLaneRange == 12
                && GroupAiZoneRules.LaneDirection(0, 0, 3, 1) == (1, 1) && GroupAiZoneRules.LaneDirection(5, 5, 2, 5) == (-1, 0)
                && GroupAiZoneRules.LaneDirection(4, 4, 4, 4) == (0, 0),
            "zone cells: a charge covers its range along the line through its target, three wide; the default range is 12, the direction is the sign of the way");
        var stomp = Area("SKILL_StoneStomp");
        var crash = Area("SKILL_EarthCrash");
        var stompCells = GroupAiZoneRules.CenterKeys(stomp, 10, 10, 1, 1);
        var bigStomp = GroupAiZoneRules.CenterKeys(stomp, 10, 10, 2, 2);
        var crashCells = GroupAiZoneRules.CenterKeys(crash, 10, 10, 1, 1);
        Check(stompCells.Count == 9 && stompCells.Contains((11, 11)) && !stompCells.Contains((12, 10))
                && bigStomp.Count == 25 && bigStomp.Contains((8, 8)) && bigStomp.Contains((12, 12)) && !bigStomp.Contains((13, 10))
                && crashCells.Count == 25 && crashCells.Contains((10, 14)) && crashCells.Contains((10, 8)) && !crashCells.Contains((10, 7)) && !crashCells.Contains((10, 15)),
            "zone cells: an area centred on the user covers every cell the user's size may cover; the crash's centre is one row down");
        var corpse = Area("AFFECTER_ExplosiveCorpse");
        var withoutRow = GroupAiZoneRules.ShapeKeys(corpse, false, 3, 3, 8, 8, null);
        var withRow = GroupAiZoneRules.ShapeKeys(corpse, true, 3, 3, 8, 8, null);
        var fromGame = GroupAiZoneRules.ShapeKeys(corpse, true, 3, 3, 8, 8, new List<(int X, int Y)> { (1, 1), (2, 2), (1, 1) });
        Check(withoutRow.Count == 9 && withoutRow.Contains((2, 2)) && !withoutRow.Contains((9, 9))
                && withRow.Count == 9 && withRow.Contains((9, 9)) && !withRow.Contains((3, 3))
                && fromGame.Count == 2 && fromGame.Contains((1, 1)) && fromGame.Contains((2, 2)) && !fromGame.Contains((8, 8)),
            "zone cells: the game's cells win; else the rule's shape around the target, or around the user when the skill has no row");
    }

    static void Lock()
    {
        var spots = new List<(int X, int Y)> { (10, 14), (13, 10), (11, 10), (10, 8) };
        Check(GroupAiZoneRules.NearestInReach(10, 10, 1, spots) == 2 && GroupAiZoneRules.NearestInReach(10, 10, 0, spots) == 2
                && GroupAiZoneRules.NearestInReach(10, 10, 1, new List<(int X, int Y)> { (10, 12), (12, 10), (10, 13) }) == 0
                && GroupAiZoneRules.NearestInReach(10, 10, 1, new List<(int X, int Y)> { (10, 13), (13, 10) }) == -1
                && GroupAiZoneRules.NearestInReach(10, 10, 1, new List<(int X, int Y)>()) == -1
                && GroupAiZoneRules.NearestInReach(10, 10, 0, new List<(int X, int Y)> { (10, 40) }) == 0,
            "zone lock: the nearest unit within the skill's range plus one cell, the first of equals; none when all are out of reach; no range, any");
        // A knight's chain area around 449,155, the party's melee unit at 445,150 on its way to the knight at 449,154.
        var chain = Area("SKILL_PhantomChain");
        var book = new GroupAiZoneRules.Book();
        book.MarkArea(Players, ExecA, "knight", chain, GroupAiZoneRules.CenterKeys(chain, 449, 154, 1, 1), 20f, null, null, "e");
        var until = book.Zones[0].Until;
        var now = 22f;
        Check(!book.InEnemyAreaCell(445, 150, now) && book.InEnemyAreaCell(447, 152, now),
            "zone lock: the unit stands outside the chain area and the way to the knight leads into it");
        bool InArea(int x, int y) => book.InEnemyAreaCell(x, y, now);
        var approach = GroupAiAreaRules.Approach(445, 150, 449, 154, 1);
        var enters = GroupAiAreaRules.ApproachEntersZone(445, 150, 449, 154, 1, InArea);
        var crossedUntil = book.UntilCrossed(approach, now);
        var nobodyInReach = GroupAiZoneRules.NearestInReach(445, 150, 1, new List<(int X, int Y)> { (449, 154), (449, 156) });
        var swap = GroupAiZoneRules.NearestInReach(445, 150, 1, new List<(int X, int Y)> { (449, 154), (446, 150) });
        Check(enters && Near(crossedUntil, until) && nobodyInReach == -1 && swap == 1
                && GroupAiAreaRules.HoldApplies(true, now, crossedUntil, enters) && !GroupAiZoneRules.HoldExpired(now, crossedUntil)
                && GroupAiZoneRules.HoldExpired(crossedUntil, crossedUntil) && !GroupAiAreaRules.HoldApplies(true, crossedUntil, crossedUntil, enters),
            "zone lock: a target across the area is swapped for one in reach, or the choice is skipped and held to the area's end, no longer");
        Check(GroupAiZoneRules.MayHold(true, false, true, false) && !GroupAiZoneRules.MayHold(false, false, true, false)
                && !GroupAiZoneRules.MayHold(true, true, true, false) && !GroupAiZoneRules.MayHold(true, false, false, false)
                && !GroupAiZoneRules.MayHold(true, false, true, true),
            "zone lock: a hold needs a party unit in Core's AI with open behaviour and no player's order within the quiet time");
    }
}
