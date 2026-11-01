using DungeonSettlersDelvers.Core;
using static TestKit;

// The party dodges enemy areas: area table, shapes, cells, probe and log lines.
internal static class GroupAiAreasSection
{
    internal static void Run()
    {
        DodgedAreas();
        ZoneLock();
        AreaProbe();
    }

    // The end of the zone a fresh book gives an area marked at startedAt: the way Core really ends an enemy area's zone.
    static float ZoneEnd(GroupAiAreaRules.Area area, float startedAt, float? lastHit)
    {
        var book = new GroupAiZoneRules.Book();
        book.MarkArea("players", new IntPtr(1), "boss", area, GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, 1, 0, 0), startedAt, null, lastHit, "e");
        return book.Zones[0].Until;
    }

    static void DodgedAreas()
    {
        // The party dodges enemy areas: F1, F4, F7, F9 always, F8 below 50 % health; F2, F3, F5, F6 never (no warning).
        var f = GroupAiAreaRules.Areas.GroupBy(a => a.Flag).ToDictionary(g => g.Key, g => g.First());
        Check(GroupAiAreaRules.Areas.Length == 10 && GroupAiAreaRules.Areas.Select(a => a.Vfx).Distinct().Count() == 9
                && GroupAiAreaRules.BySource("AFFECTER_SporeExplosive") is { Flag: "F9", Vfx: 185, Radius: 1, Dodge: true } spores && spores.FirstHit == 1.5f
                && new[] { "F1", "F4", "F7", "F9" }.All(k => GroupAiAreaRules.Dodges(f[k], 1.0))
                && new[] { "F2", "F3" }.All(k => !GroupAiAreaRules.Dodges(f[k], 0.1) && GroupAiAreaRules.SkipReason(f[k], 0.1, 0.1f) == "harmless")
                && GroupAiAreaRules.SkipReason(f["F7"], 1.0, 4f) == null
                && !GroupAiAreaRules.Dodges(f["F5"], 0.1) && !GroupAiAreaRules.Dodges(f["F6"], 0.1)
                && !GroupAiAreaRules.Dodges(f["F8"], 0.5) && GroupAiAreaRules.Dodges(f["F8"], 0.49) && !GroupAiAreaRules.Dodges(f["F8"], double.NaN)
                && GroupAiAreaRules.BySource("SKILL_ArrowRain") == f["F8"]
                && GroupAiAreaRules.SkipReason(f["F8"], 0.8, 3f) == "hp" && GroupAiAreaRules.SkipReason(f["F1"], 1.0, 3.5f) == "over"
                && GroupAiAreaRules.SkipReason(f["F5"], 0.1, 3f) == "harmless" && GroupAiAreaRules.SkipReason(f["F4"], 1.0, 2f) == null
                && GroupAiAreaRules.SkipReason(null, 1.0, 2f) == "harmless"
                // Where a unit stands when the skill asks counts: a single query is late
                // 0.4 s before it, waves and the chain's list still reward a later step out, nothing after the last query.
                && GroupAiAreaRules.SkipReason(f["F9"], 1.0, 1.2f) == "late" && GroupAiAreaRules.SkipReason(f["F9"], 1.0, 0.5f) == null
                && GroupAiAreaRules.SkipReason(f["F4"], 1.0, 4f) == null && GroupAiAreaRules.SkipReason(f["F8"], 0.3, 3.5f) == null
                && GroupAiAreaRules.SkipReason(f["F8"], 0.3, 4.1f) == "over" && GroupAiAreaRules.SkipReason(f["F1"], 1.0, 2.9f) == null
                && f["F2"].CenterOnUser && f["F3"].CenterOnUser && !f["F8"].CenterOnUser
                && Math.Abs(f["F8"].HoldSeconds - 4.22f) < 0.001f && Math.Abs(f["F7"].HoldSeconds - 6.1f) < 0.001f
                && GroupAiAreaRules.Areas.Where(a => a.Dodge).All(a => a.FirstHit > 0f && a.LastHit >= a.FirstHit),
            "areas: F1, F4, F7, F9 dodged, F8 only below 50 % health, F2, F3, F5, F6 never; late before a single query, waves keep it open");
        // The partner of the hold seconds above on the way the zones are really ended: a zone book marked at second 0 ends at the same time.
        Check(Math.Abs(ZoneEnd(f["F8"], 0f, null) - 4.22f) < 0.001f && Math.Abs(ZoneEnd(f["F7"], 0f, null) - 6.1f) < 0.001f
                && ZoneEnd(f["F8"], 0f, null) == GroupAiAreaRules.HoldUntil(0f, f["F8"].LastHit) && ZoneEnd(f["F7"], 0f, null) == GroupAiAreaRules.HoldUntil(0f, f["F7"].LastHit),
            "areas: a zone marked at second 0 ends at the area's last hit plus the hold margin (arrow rain 4.22 s, phantom chain 6.1 s)");

        {
        var rain = GroupAiAreaRules.BySource("SKILL_ArrowRain");
        var stomp = GroupAiAreaRules.BySource("SKILL_StoneStomp");
        Check(Math.Abs(GroupAiAreaRules.LastHitFor(rain, new[] { 40f, 6f }) - 3.92f) < 0.001f
                && Math.Abs(GroupAiAreaRules.LastHitFor(rain, new[] { 40f, 8f }) - 4.92f) < 0.001f
                && Math.Abs(GroupAiAreaRules.LastHitFor(rain, new[] { 40f, 1f }) - 1.42f) < 0.001f
                && Math.Abs(GroupAiAreaRules.LastHitFor(rain, new[] { 40f }) - rain.LastHit) < 0.001f
                && Math.Abs(GroupAiAreaRules.LastHitFor(rain, null) - rain.LastHit) < 0.001f
                && Math.Abs(GroupAiAreaRules.HoldFor(rain, new[] { 40f, 8f }) - 5.22f) < 0.001f
                && Math.Abs(GroupAiAreaRules.LastHitFor(stomp, new[] { 1f, 9f }) - stomp.LastHit) < 0.001f
                && GroupAiAreaRules.SkipReason(rain, 0.3, 4.5f, 4.92f) == null && GroupAiAreaRules.SkipReason(rain, 0.3, 4.5f) == "over",
            "areas: the arrow rain's waves come from its row (FloatValues[1], 0.5 s apart); other areas keep the rule's last hit");
        var eightWaves = GroupAiAreaRules.LastHitFor(rain, new[] { 40f, 8f });
        Check(Math.Abs(ZoneEnd(rain, 0f, eightWaves) - 5.22f) < 0.001f && ZoneEnd(rain, 0f, eightWaves) == GroupAiAreaRules.HoldFor(rain, new[] { 40f, 8f })
                && ZoneEnd(rain, 0f, eightWaves) == GroupAiAreaRules.HoldUntil(0f, eightWaves),
            "areas: a zone of the arrow rain with eight waves, marked at second 0, ends at 5.22 s");
        }
        Check(GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, 1, 5, 5).Count == 9
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Manhattan, 3, 0, 0).Count == 25
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Manhattan, 2, 0, 0).Count == 13
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Circle, 5, 0, 0).Count == 81
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Count == 81
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Contains((-2, 4)) && !GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Contains((6, 0))
                && !GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Contains((0, 6)) && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Contains((5, 1))
                && !GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 0, 0).Contains((-2, 5))
                && GroupAiAreaRules.BySource("SKILL_PhantomChain") is { CenterOnUser: true, ShiftY: 1, Shape: GroupAiAreaRules.Shape.Chain }
                && GroupAiAreaRules.BySource("SKILL_EarthCrash").ShiftY == 1 && GroupAiAreaRules.BySource("SKILL_StoneStomp").ShiftY == 0
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Cell, 0, 3, 4).SequenceEqual(new[] { (3, 4) })
                && GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, 1, 449, 138).Contains((450, 139)) && !GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, 1, 449, 138).Contains((451, 138))
                && GroupAiAreaRules.PathCells(0, 0, 3, 1).SequenceEqual(new[] { (1, 1), (2, 1) }) && GroupAiAreaRules.PathCells(0, 0, 1, 1).Count == 0,
            "area shapes: square 3x3, Manhattan 3 is 25 cells, circle 5 is 81, a single cell; a straight walk's cells on the way");
        var zone = new HashSet<(int, int)> { (0, 0), (1, 0), (2, 0), (-1, 0) };
        bool Free(int x, int y) => !zone.Contains((x, y)) && !(x == 0 && y == 1);
        bool InZone(int x, int y) => zone.Contains((x, y));
        bool NoEnemy(int x, int y) => false;
        bool EnemyAbove(int x, int y) => y >= 1 && Math.Abs(x) <= 1;
        Check(GroupAiAreaRules.PickCell(0, 0, false, false, 0, 0, (1, 0), Free, InZone, NoEnemy, 3, out var ax, out var ay) && ay != 0 && Math.Max(Math.Abs(ax), Math.Abs(ay)) == 1
                && GroupAiAreaRules.PickCell(0, 0, false, false, 0, 0, (1, 0), Free, InZone, EnemyAbove, 3, out _, out var by) && by < 0
                && GroupAiAreaRules.PickCell(0, 0, true, true, 1, -2, null, Free, InZone, EnemyAbove, 3, out var cx, out var cy) && GroupAiRules.Distance(cx, cy, 1, -2) == 1
                && !GroupAiAreaRules.PickCell(0, 0, false, false, 0, 0, null, (x, y) => false, InZone, NoEnemy, 3, out _, out _)
                && GroupAiAreaRules.BandageAllowed(false) && !GroupAiAreaRules.BandageAllowed(true),
            "dodge cell: out of the lane across it, not next to an enemy for ranged units, a melee unit stays next to its target, none when no cell is free");
        // No way out of a 3 wide lane (every path crossed the unit's own zone), the metal charge's
        // lane missed its target, the golem hit outside its one cell, the chain's circle 5 is wider than three rings.
        var lane = new HashSet<(int, int)>();
        for (var x = -12; x <= 12; x++) for (var y = -1; y <= 1; y++) lane.Add((x, y));
        bool LaneFree(int x, int y) => !lane.Contains((x, y));
        bool InOtherZone(int x, int y) => false;
        Check(GroupAiAreaRules.PickCell(0, 0, false, false, 0, 0, (1, 0), LaneFree, InOtherZone, NoEnemy, 3, out var lx, out var ly) && Math.Abs(ly) == 2 && lx == 0
                && GroupAiAreaRules.PickCell(0, 0, false, false, 0, 0, (1, 0), LaneFree, InOtherZone, (x, y) => true, 3, out _, out var ey) && Math.Abs(ey) == 2
                && GroupAiAreaRules.LaneCells(0, 0, 3, 1, 6).SequenceEqual(new[] { (1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2) })
                && GroupAiAreaRules.LaneCells(5, 5, 5, 2, 12).Count == 12 && GroupAiAreaRules.LaneCells(5, 5, 5, 2, 12)[2] == (5, 2)
                && GroupAiAreaRules.LaneCells(1, 1, 1, 1, 12).Count == 0
                && GroupAiAreaRules.Footprint(10, 10, 2, 2).Count == 9 && GroupAiAreaRules.Footprint(10, 10, 1, 1).SequenceEqual(new[] { (10, 10) })
                && GroupAiAreaRules.SearchRing(f["F7"], 3) == 7 && GroupAiAreaRules.SearchRing(f["F3"], 3) == 3 && GroupAiAreaRules.SearchRing(null, 3) == 3,
            "dodge after EAD67420: out of a 3 wide lane in two steps, next to an enemy before staying, the lane through the target, a 2x2 unit's footprint, wider search for wide areas");
        Check(GroupAiAreaRules.ZoneLine("F8", "u", "SKILL_ArrowRain", "cast", 13, 12.34f, "e") == "CORE_AI_ZONE side=players area=F8 unit=u source=SKILL_ArrowRain via=cast cells=13 until=12.3 exec=e"
                && GroupAiAreaRules.SkipLine("u", "F8", "hp", 0.734) == "CORE_AI_AUSWEICHEN_SKIP unit=u area=F8 reason=hp hp=0.73",
            "area log lines");
    }

    static void ZoneLock()
    {
        Check(GroupAiAreaRules.DodgeAllowed(true, 0, false) && GroupAiAreaRules.DodgeAllowed(false, 0, false)
                && !GroupAiAreaRules.DodgeAllowed(true, 1, false) && GroupAiAreaRules.DodgeAllowed(true, 1, true)
                && GroupAiAreaRules.DodgeAllowed(true, 3, true) && !GroupAiAreaRules.DodgeAllowed(true, 4, true)
                && !GroupAiAreaRules.DodgeAllowed(false, 1, true) && GroupAiAreaRules.MaxDodgeAgain == 3
                && Math.Abs(GroupAiAreaRules.BySource("SKILL_PhantomChain").HoldSeconds - 6.1f) < 0.001f,
        "areas: a unit that walked back into an enemy area goes out again only after it was outside, at most 3 times; Core's own areas once; the chain's zone holds to 6.1 s");
        Check(Math.Abs(ZoneEnd(GroupAiAreaRules.BySource("SKILL_PhantomChain"), 0f, null) - 6.1f) < 0.001f,
            "areas: the phantom chain's zone, marked at second 0, holds to 6.1 s");
        {
            // A knight's chain area around 449..450,154..156 (cells marked by the test), a melee unit at 445,150, a ranged one at 442,154.
            var zone = new HashSet<(int, int)>(GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Chain, 5, 449, 155));
            Func<int, int, bool> inZone = (x, y) => zone.Contains((x, y));
            Check(!zone.Contains((445, 150)) && GroupAiAreaRules.ApproachEntersZone(445, 150, 449, 154, 1, inZone)
                    && !GroupAiAreaRules.ApproachEntersZone(442, 154, 449, 154, 7, inZone)
                    && GroupAiAreaRules.Approach(442, 154, 449, 154, 7).Count == 0 && GroupAiAreaRules.Approach(445, 150, 449, 154, 0).Count == 0
                    && GroupAiAreaRules.Approach(0, 0, 5, 0, 1).SequenceEqual(new[] { (1, 0), (2, 0), (3, 0) })
                    && !GroupAiAreaRules.ApproachEntersZone(0, 0, 9, 0, 1, (x, y) => x == 9)
                    && GroupAiAreaRules.LockLine("u", "Hammering", "t", "-", "skip reason=zone") == "CORE_AI_ZONE_LOCK unit=u skill=Hammering target=t to=- result=skip reason=zone",
                "zone lock: a melee step to the knight crosses its chain area, a ranged unit in reach (range + 1) shoots from where it stands");
            Check(GroupAiAreaRules.HoldApplies(true, 1f, 6.1f, true) && !GroupAiAreaRules.HoldApplies(false, 1f, 6.1f, true)
                    && !GroupAiAreaRules.HoldApplies(true, 6.1f, 6.1f, true) && !GroupAiAreaRules.HoldApplies(true, 1f, 6.1f, false)
                    && !GroupAiAreaRules.HoldApplies(true, 1f, 0f, true)
                    && GroupAiAreaRules.HoldLine("u", "BasicClubAttack", "t", 6.1f) == "CORE_AI_ZONE_HOLD unit=u skill=BasicClubAttack target=t until=6.1",
                "zone hold: only after a skipped choice, only until the crossed zone ends, only while the way still crosses it");
        }
    }

    static void AreaProbe()
    {
        // 1a: the red areas are the VFX ids ending in "Area"; cell from a known unit, inside within half a cell.
        Check(GroupAiAreaProbeRules.IsArea("Explosion_Area") && GroupAiAreaProbeRules.IsArea("StoneStompArea") && !GroupAiAreaProbeRules.IsArea("Bleeding")
                && !GroupAiAreaProbeRules.IsArea("AreaHit") && !GroupAiAreaProbeRules.IsArea(null)
                && GroupAiAreaProbeRules.CellOf(12.0f, 7.0f, 450, 150, 10.0f, 9.0f) == (452, 148)
                && GroupAiAreaProbeRules.CellOf(10.4f, 9.6f, 450, 150, 10.0f, 9.0f) == (450, 151)
                && GroupAiAreaProbeRules.Inside(3.2f, 4.1f, 3.0f, 4.0f) && !GroupAiAreaProbeRules.Inside(3.6f, 4.0f, 3.0f, 4.0f)
                && GroupAiAreaProbeRules.Short("0123456789abcdef") == "01234567" && GroupAiAreaProbeRules.Short(null) == "-"
                && GroupAiAreaProbeRules.CreateLine("01234567", "Explosion_Area", 104, 3f, 4.5f, "451,152", "bd0cac24", 9)
                    == "CORE_AI_AREA_FX_PROBE kind=create fx=01234567 vfx=Explosion_Area id=104 at=3.00,4.50 cell=451,152 inside=bd0cac24 tick=9"
                && GroupAiAreaProbeRules.CreateLine("a", "X_Area", 1, 0f, 0f, null, "", 1).Contains(" cell=- inside=- ")
                && GroupAiAreaProbeRules.DurationLine("a", "X_Area", 1.5f, 2) == "CORE_AI_AREA_FX_PROBE kind=duration fx=a vfx=X_Area duration=1.50 tick=2"
                && GroupAiAreaProbeRules.SeenLine("Bleeding", 4, 1f, 2f, 3) == "CORE_AI_VFX_SEEN vfx=Bleeding id=4 at=1.00,2.00 tick=3"
                && GroupAiAreaProbeRules.Repeat(100, 279) && !GroupAiAreaProbeRules.Repeat(100, 280) && !GroupAiAreaProbeRules.Repeat(-1, 5) && !GroupAiAreaProbeRules.Repeat(300, 100)
                && GroupAiAreaProbeRules.CreateLine("a", "X_Area", 1, 0f, 0f, "1,2", "", 9, 3).EndsWith(" tick=9 skipped=3"),
            "1a: the red area probe's lines, a repeat on the same cell within 3 s only counted");
    }
}
