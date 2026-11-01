using DungeonSettlersDelvers.Core;
using static TestKit;

// The equipment marker's rules and ledger, one check per case of the contract's counter-cases.
internal static class EquipmentMarkerSection
{
    private static readonly Guid G1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid G2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly IntPtr UnitX = new(1);
    private static readonly IntPtr UnitY = new(2);

    private static EquipmentMarkerRules.Departure Dep(string key, int amount, long at = 0, string why = "unexplained", long frame = 1)
        => new() { Unit = "u", Map = 0, Slot = 2, Key = key, ItemGuid = G1, Amount = amount, Tick = 5, AtPostfix = at, Frame = frame, Why = why };

    private static List<string> Lines(IEnumerable<EquipmentMarkerRules.Report> reports) => reports.Select(r => r.Line).ToList();

    internal static void Run()
    {
        Arrivals();
        Settling();
        Credit();
        Caps();
        Frames();
        Filter();
        FailuresAndDrop();
        Formats();
    }

    private static void Arrivals()
    {
        var drop = new EquipmentMarkerRules.Ledger();
        Lines(drop.Depart(Dep("ITEM_Sword", 1)));
        Check(Lines(drop.Arrive("ITEM_Sword", 1)).Count == 0 && drop.Open == 0 && drop.Counters.Kept == 1 && drop.Counters.Departures == 1,
            "equipment marker: dropping with a new entity at the target, the arrival pays the departure (kept)");

        var arrows = new EquipmentMarkerRules.Ledger();
        Lines(arrows.Depart(Dep("ITEM_Arrow", 84)));
        Lines(arrows.Arrive("ITEM_Arrow", EquipmentMarkerRules.AddItemArrival("ITEM_Arrow", G1, 50, "ITEM_Arrow", G1, 134)));
        Check(arrows.Open == 0 && arrows.Counters.Kept == 1,
            "equipment marker: 84 arrows dropped onto 50 in the bag arrive as 134 - 50 = 84 and are kept");

        var part = new EquipmentMarkerRules.Ledger();
        Lines(part.Depart(Dep("ITEM_Arrow", 10)));
        Lines(part.Arrive("ITEM_Arrow", 4));
        Check(Lines(part.SettleDue(1)).Count == 0 && part.Counters.Partial == 1 && part.Counters.Missing == 0,
            "equipment marker: a partly paid departure counts partial and writes no line");

        Check(EquipmentMarkerRules.Overwrite("ITEM_A", G1, 5, "ITEM_A", G1, 3) == EquipmentMarkerRules.OverwriteKind.Reduced
                && EquipmentMarkerRules.Overwrite("ITEM_A", G1, 5, "ITEM_B", G2, 1) == EquipmentMarkerRules.OverwriteKind.Departure
                && EquipmentMarkerRules.Overwrite("ITEM_A", G1, 5, "ITEM_A", G1, 7) == EquipmentMarkerRules.OverwriteKind.None
                && EquipmentMarkerRules.Overwrite(null, Guid.Empty, 0, "ITEM_A", G1, 1) == EquipmentMarkerRules.OverwriteKind.None,
            "equipment marker: overwrite kinds, another holder departs, the same with less is reduced, empty or more is nothing");

        Check(EquipmentMarkerRules.IsZero("ITEM_A", G1, 0) && EquipmentMarkerRules.IsZero("ITEM_A", Guid.Empty, 3)
                && EquipmentMarkerRules.IsZero(null, G1, 1) && !EquipmentMarkerRules.IsZero("ITEM_A", G1, 1),
            "equipment marker: an empty holder or amount 0 counts zero, not as a departure");

        Check(EquipmentMarkerRules.SpawnArrival(50, 134) == 84 && EquipmentMarkerRules.SpawnArrival(10, 4) == 0,
            "equipment marker: a spawn arrives with the new amount minus the old, only when positive");

        // Drop to the bag, then dispose of it from the bag: the drop is kept; the bag is not watched, so no second departure.
        var dispose = new EquipmentMarkerRules.Ledger();
        Lines(dispose.Depart(Dep("ITEM_Sword", 1)));
        Lines(dispose.Arrive("ITEM_Sword", 1));
        Check(Lines(dispose.SettleDue(1)).Count == 0 && dispose.Counters.Kept == 1 && dispose.Counters.Missing == 0
                && EquipmentMarkerRules.WatchOf(false, true, false, false, true) == EquipmentMarkerRules.Watch.Other,
            "equipment marker: drop, then dispose from the inventory, the drop stays kept and the bag writes nothing");

        var summon = new EquipmentMarkerRules.Ledger();
        Lines(summon.Depart(Dep("ITEM_Bow", 1)));
        Lines(summon.Arrive("ITEM_Bow", 1));
        Check(summon.Open == 0 && summon.Counters.Kept == 1,
            "equipment marker: an arrival at a summon pays a party departure (arrivals have no filter)");

        var handover = new EquipmentMarkerRules.Ledger();
        Lines(handover.Depart(Dep("ITEM_Ring", 1)));
        Lines(handover.Arrive("ITEM_Ring", EquipmentMarkerRules.AddItemArrival(null, Guid.Empty, 0, "ITEM_Ring", G1, 1)));
        Check(handover.Open == 0 && handover.Counters.Kept == 1 && Lines(handover.SettleDue(1)).Count == 0,
            "equipment marker: a handover from one party member to another onto an empty slot is kept");
    }

    private static void Settling()
    {
        var lost = new EquipmentMarkerRules.Ledger();
        Lines(lost.Depart(Dep("ITEM_Arrow", 84)));
        var lostLines = Lines(lost.SettleDue(1));
        Check(lostLines.Count == 1 && lostLines[0] == "CORE_EQUIPMENT_MISSING unit=u map=0 slot=2 key=ITEM_Arrow amount=84 arrived=0 tick=5 during=play why=unexplained save_load=0",
            "equipment marker: an unpaid departure after a full tick postfix writes one MISSING line, word for word");

        var sameTick = new EquipmentMarkerRules.Ledger();
        Lines(sameTick.Depart(Dep("ITEM_Ring", 1, at: 3)));
        var notYet = Lines(sameTick.SettleDue(3)).Count == 0 && sameTick.Open == 1;
        Lines(sameTick.Arrive("ITEM_Ring", 1));
        Check(notYet && sameTick.Counters.Kept == 1,
            "equipment marker: a departure in a tick's event part is not settled by that tick's postfix; a later arrival keeps it");

        Check(!EquipmentMarkerRules.Due(3, 3) && EquipmentMarkerRules.Due(3, 4),
            "equipment marker: a departure is due once one tick postfix ran fully after it");

        var pause = new EquipmentMarkerRules.Ledger();
        Lines(pause.Depart(Dep("ITEM_Helm", 1)));
        var pauseLines = Lines(pause.SettleAll());
        Check(pauseLines.Count == 1 && pauseLines[0].Contains(" why=unsettled ") && pause.Counters.Unsettled == 1,
            "equipment marker: a loss in the pause and then a new bind writes one unsettled line");

        var full = new EquipmentMarkerRules.Ledger();
        var fullLines = new List<string>();
        for (var i = 0; i < 65; i++) fullLines.AddRange(Lines(full.Depart(Dep("ITEM_K" + i, 1))));
        Check(full.Open == 64 && fullLines.Count == 1 && fullLines[0].Contains("key=ITEM_K0 ") && fullLines[0].Contains(" why=unsettled "),
            "equipment marker: at K = 64 open departures the 65th settles the oldest at once as unsettled");

        var pauseArrival = new EquipmentMarkerRules.Ledger();
        Lines(pauseArrival.Depart(Dep("ITEM_Helm", 1)));
        Lines(pauseArrival.Arrive("ITEM_Helm", 1));
        Check(pauseArrival.Counters.Kept == 1 && Lines(pauseArrival.SettleAll()).Count == 0,
            "equipment marker: a loss in the pause paid by an arrival in the same pause is kept, no line at the next bind");

        var reported = new EquipmentMarkerRules.Ledger();
        var first = Lines(reported.ReportMissing("L", true, false, "u", 1, "ITEM_Helm", 1, "load_repair"));
        Check(first.Count == 1 && reported.Open == 0 && Lines(reported.SettleDue(5)).Count == 0 && Lines(reported.SettleAll()).Count == 0,
            "equipment marker: a load repair is reported once, no second line after the load");

        var over = new EquipmentMarkerRules.Ledger();
        Lines(over.Depart(Dep("ITEM_A", 1, why: "overwrite")));
        var overLines = Lines(over.SettleDue(1));
        Check(overLines.Count == 1 && overLines[0].Contains(" why=overwrite "),
            "equipment marker: an unpaid overwritten holder is reported with why=overwrite");
    }

    private static void Credit()
    {
        // Slot swap through two overwrites in one frame: the second holder's arrival comes before its departure.
        var swap = new EquipmentMarkerRules.Ledger();
        Lines(swap.Depart(Dep("ITEM_A", 1, why: "overwrite", frame: 7)));
        Lines(swap.Arrive("ITEM_B", EquipmentMarkerRules.AddItemArrival("ITEM_A", G1, 1, "ITEM_B", G2, 1), 7));
        Lines(swap.Depart(Dep("ITEM_B", 1, why: "overwrite", frame: 7)));
        Lines(swap.Arrive("ITEM_A", EquipmentMarkerRules.AddItemArrival("ITEM_B", G2, 1, "ITEM_A", G1, 1), 7));
        Check(swap.Open == 0 && swap.Counters.Kept == 2 && swap.Counters.Departures == 2,
            "equipment marker: a slot swap through two overwrites in one frame keeps both holders");

        // In the pause Y equips ITEM_X from the bag (no overwrite), then X loses ITEM_X: a departure, not kept.
        var pauseEquip = new EquipmentMarkerRules.Ledger();
        Lines(pauseEquip.Arrive("ITEM_X", 1));
        Lines(pauseEquip.Depart(Dep("ITEM_X", 1, frame: 9)));
        Check(pauseEquip.Open == 1 && pauseEquip.Counters.Kept == 0 && Lines(pauseEquip.SettleAll()).Count == 1,
            "equipment marker: an arrival that overwrote nothing leaves no credit, a later loss stays a departure");

        // Credit of an overwrite holds only for its frame.
        var otherFrame = new EquipmentMarkerRules.Ledger();
        Lines(otherFrame.Arrive("ITEM_B", 1, 7));
        Lines(otherFrame.Depart(Dep("ITEM_B", 1, frame: 8)));
        var otherFrameLines = Lines(otherFrame.SettleDue(1));
        Check(otherFrame.Counters.Kept == 0 && otherFrameLines.Count == 1 && otherFrameLines[0].Contains(" key=ITEM_B "),
            "equipment marker: credit of an overwrite in another frame pays nothing, the departure is reported");

        // Arrival, postfix, departure: the postfix ends the credit, the departure is reported.
        var afterPostfix = new EquipmentMarkerRules.Ledger();
        Lines(afterPostfix.Arrive("ITEM_B", 1, 7));
        Lines(afterPostfix.SettleDue(0));
        Lines(afterPostfix.Depart(Dep("ITEM_B", 1, at: 1, frame: 7)));
        Check(Lines(afterPostfix.SettleDue(2)).Count == 1,
            "equipment marker: arrival, tick postfix, departure gives one MISSING line");

        // A departure fully paid from credit takes no place among the K open ones and settles nothing early.
        var fullK = new EquipmentMarkerRules.Ledger();
        for (var i = 0; i < 64; i++) Lines(fullK.Depart(Dep("ITEM_K" + i, 1)));
        Lines(fullK.Arrive("ITEM_P", 1, 7));
        var paidLines = Lines(fullK.Depart(Dep("ITEM_P", 1, frame: 7)));
        Check(paidLines.Count == 0 && fullK.Open == 64 && fullK.Counters.Unsettled == 0 && fullK.Counters.Kept == 1,
            "equipment marker: a departure paid from credit at K open ones pushes no oldest out");
    }

    private static void Caps()
    {
        var cap = new EquipmentMarkerRules.Ledger();
        for (var i = 0; i < 22; i++) Lines(cap.Depart(Dep("ITEM_C" + i, 1)));
        var capLines = Lines(cap.SettleDue(1));
        Check(capLines.Count == 21 && capLines[20] == "CORE_EQUIPMENT_MISSING_CAPPED after=20 during=play" && cap.Counters.Capped == 2,
            "equipment marker: 20 play lines, then one CAPPED during=play, then silent while counting goes on");

        var loadLine = Lines(cap.ReportMissing("L", true, false, "u", 0, "ITEM_X", 1, "load_repair"));
        Check(loadLine.Count == 1 && loadLine[0] == "L",
            "equipment marker: load lines have their own cap, a capped play scope still lets a load line through");

        var loadCap = new EquipmentMarkerRules.Ledger();
        var loads = new List<string>();
        for (var i = 0; i < 21; i++) loads.AddRange(Lines(loadCap.ReportMissing("L" + i, true, false, "u", 0, "ITEM_X", 1, "load_repair")));
        loadCap.ResetLoadCap();
        var afterReset = Lines(loadCap.ReportMissing("again", true, false, "u", 0, "ITEM_X", 1, "load_repair"));
        Check(loads.Count == 21 && loads[20] == "CORE_EQUIPMENT_MISSING_CAPPED after=20 during=load" && afterReset.Count == 1 && afterReset[0] == "again",
            "equipment marker: 20 load lines then CAPPED during=load; the next load starts its cap over");

        var bind = new EquipmentMarkerRules.Ledger();
        var bindLoads = new List<string>();
        for (var i = 0; i < 25; i++) bindLoads.AddRange(Lines(bind.ReportMissing("L" + i, true, false, "u", 0, "ITEM_X", 1, "load_repair")));
        for (var i = 0; i < 21; i++) Lines(bind.Depart(Dep("ITEM_P" + i, 1)));
        var playBefore = Lines(bind.SettleDue(1));
        var cappedBefore = bind.Counters.Capped;
        bind.Reset();
        bind.ResetLoadCap();
        var loadAfter = Lines(bind.ReportMissing("L-new", true, false, "u", 0, "ITEM_X", 1, "load_repair"));
        Lines(bind.Depart(Dep("ITEM_Q", 1)));
        var playAfter = Lines(bind.SettleDue(1));
        Check(bindLoads.Count == 21 && cappedBefore == 6 && playBefore.Count == 21 && playBefore[20].EndsWith("during=play", StringComparison.Ordinal)
                && loadAfter.Count == 1 && loadAfter[0] == "L-new" && playAfter.Count == 1 && bind.Counters.Capped == 0,
            "equipment marker: 25 load lines give 20 and CAPPED, the play scope caps apart, and a new bind and load start both over");
    }

    private static void Frames()
    {
        var destroy = EquipmentMarkerRules.FrameOf("destroy", UnitX, 3, false);
        var ammo = EquipmentMarkerRules.FrameOf("ammo", UnitX, EquipmentMarkerRules.AmmoSlot, false);
        Check(EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { destroy }, UnitX, 3)) == EquipmentMarkerRules.Route.Sunk
                && EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { destroy }, UnitX, 4)) == EquipmentMarkerRules.Route.Open,
            "equipment marker: DestroyItem sinks only its target slot (drop and dispose: sunk, other slots open)");

        Check(EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { ammo }, UnitX, 6)) == EquipmentMarkerRules.Route.Sunk
                && EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { ammo }, UnitX, 2)) == EquipmentMarkerRules.Route.Open
                && EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { ammo }, UnitY, 6)) == EquipmentMarkerRules.Route.Open,
            "equipment marker: the ammunition frame sinks only slot 6 of its owner");

        var dead = EquipmentMarkerRules.FrameOf("despawn", UnitX, -1, true);
        var living = EquipmentMarkerRules.FrameOf("despawn", UnitX, -1, false);
        Check(EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { dead }, UnitX, 0)) == EquipmentMarkerRules.Route.Sunk
                && EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { living }, UnitX, 5)) == EquipmentMarkerRules.Route.Despawn
                && EquipmentMarkerRules.DespawnWhy(false) == "despawn" && EquipmentMarkerRules.DespawnWhy(true) == "retire",
            "equipment marker: a dead unit's despawn sinks every slot, a living one's is reported as despawn, in retirement as retire");

        var corpse = new EquipmentMarkerRules.Ledger { DepartureLines = true };
        var sunkLines = new List<string>();
        for (var slot = 0; slot < 3; slot++) sunkLines.AddRange(Lines(corpse.Sink(Dep("ITEM_S" + slot, 1))));
        Check(corpse.Counters.Departures == 3 && corpse.Counters.Sunk == 3 && corpse.Open == 0
                && sunkLines.Count == 3 && sunkLines.All(l => l.EndsWith(" outcome=sunk", StringComparison.Ordinal)),
            "equipment marker: a buried corpse with three holders books three departures, sunk=3 and outcome=sunk lines");

        var transfer = EquipmentMarkerRules.FrameOf("transfer", UnitX, -1, false);
        var moved = new EquipmentMarkerRules.Ledger();
        var movedLines = Lines(moved.ReportMissing(EquipmentMarkerRules.MissingLine("u", 1, "6", "ITEM_Arrow", 3, 0, 9, "transfer", "slot_cleared", false),
            false, true, "u", 6, "ITEM_Arrow", 3, "slot_cleared"));
        Check(EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { transfer }, UnitX, 6)) == EquipmentMarkerRules.Route.Transit
                && EquipmentMarkerRules.IsZero("ITEM_Arrow", G1, 0)
                && movedLines.Count == 1 && movedLines[0].Contains(" amount=3 ") && movedLines[0].Contains(" during=transfer why=slot_cleared ")
                && moved.Counters.Transit == 1 && moved.Counters.Departures == 1,
            "equipment marker: ExtractUnitFrom with amount 0 counts zero, with 3 one transfer line and transit=1");

        var load = EquipmentMarkerRules.FrameOf("load", UnitX, -1, false);
        var repair = new EquipmentMarkerRules.Ledger();
        var repairLines = Lines(repair.ReportMissing(EquipmentMarkerRules.MissingLine("u", 0, "1", "ITEM_Helm", 1, 0, 0, "load", "load_repair", true),
            true, false, "u", 1, "ITEM_Helm", 1, "load_repair"));
        Check(EquipmentMarkerRules.RouteOf(EquipmentMarkerRules.FrameFor(new[] { load }, UnitX, 1)) == EquipmentMarkerRules.Route.Load
                && repairLines.Count == 1 && repairLines[0].Contains(" during=load why=load_repair save_load=1"),
            "equipment marker: LoadEquipmentSlots takes a note that is reported exactly once as load_repair");

        Check(EquipmentMarkerRules.FrameFor(new[] { living, destroy }, UnitX, 3)?.Kind == EquipmentMarkerRules.FrameKind.Sunk
                && EquipmentMarkerRules.FrameFor(new[] { living, destroy }, UnitX, 4)?.Kind == EquipmentMarkerRules.FrameKind.Despawn
                && EquipmentMarkerRules.FrameFor(Array.Empty<EquipmentMarkerRules.Frame>(), UnitX, 0) == null,
            "equipment marker: the innermost frame of the unit that covers the slot decides");
    }

    private static void Filter()
    {
        Check(EquipmentMarkerRules.WatchOf(true, true, false, false, true) == EquipmentMarkerRules.Watch.Party
                && EquipmentMarkerRules.WatchOf(true, true, true, false, false) == EquipmentMarkerRules.Watch.Ignored
                && EquipmentMarkerRules.WatchOf(true, true, false, true, true) == EquipmentMarkerRules.Watch.Ignored
                && EquipmentMarkerRules.WatchOf(true, true, false, false, false) == EquipmentMarkerRules.Watch.Other
                && EquipmentMarkerRules.WatchOf(false, true, false, false, true) == EquipmentMarkerRules.Watch.Other
                && EquipmentMarkerRules.WatchOf(true, false, false, false, false) == EquipmentMarkerRules.Watch.Other,
            "equipment marker: only a party member's equipment is watched; a bandit or a summon counts ignored");
    }

    private static void FailuresAndDrop()
    {
        var failures = new EquipmentMarkerRules.Failures();
        var switches = new[] { failures.Record(), failures.Record(), failures.Record(), failures.Record() };
        Check(switches.Count(s => s) == 1 && switches[2] && failures.Off,
            "equipment marker: three failures switch the marker off exactly once (one OFF line)");

        var dropped = new EquipmentMarkerRules.Ledger();
        Lines(dropped.Depart(Dep("ITEM_D1", 1)));
        Lines(dropped.Depart(Dep("ITEM_D2", 1)));
        dropped.Drop(3);
        Check(dropped.Counters.Dropped == 5 && dropped.Open == 0 && Lines(dropped.SettleAll()).Count == 0,
            "equipment marker: switching off drops open departures and unreported notes without a line, counted as dropped");
    }

    private static void Formats()
    {
        var probe = new EquipmentMarkerRules.Ledger { DepartureLines = true };
        Lines(probe.Depart(Dep("ITEM_P", 1)));
        var probeLines = Lines(probe.Arrive("ITEM_P", 1));
        var quiet = new EquipmentMarkerRules.Ledger();
        Lines(quiet.Depart(Dep("ITEM_P", 1)));
        Check(probeLines.Count == 1 && probeLines[0] == "CORE_EQUIPMENT_DEPARTURE unit=u slot=2 key=ITEM_P amount=1 outcome=kept"
                && Lines(quiet.Arrive("ITEM_P", 1)).Count == 0,
            "equipment marker: DEPARTURE lines only when the runtime asks for them (test campaign)");

        Check(EquipmentMarkerRules.OffSummaryLine(new EquipmentMarkerRules.Counters { Dropped = 2 }).EndsWith(" dropped=2 errors=0 capped=0 at=off", StringComparison.Ordinal),
            "equipment marker: the summary at switch-off is marked at=off, never read as the summary of a bind");

        Check(EquipmentMarkerRules.OffLine("settle") == "CORE_EQUIPMENT_MARKER_OFF reason=errors stage=settle"
                && EquipmentMarkerRules.SummaryLine(new EquipmentMarkerRules.Counters { Departures = 2, Kept = 1, Zero = 3 })
                    == "CORE_EQUIPMENT_MARKER departures=2 kept=1 sunk=0 transit=0 zero=3 reduced=0 partial=0 missing=0 unsettled=0 ignored=0 dropped=0 errors=0 capped=0",
            "equipment marker: OFF and summary lines word for word");

        Check(EquipmentMarkerRules.MaxLines == 20 && EquipmentMarkerRules.MaxFailures == 3 && EquipmentMarkerRules.MaxOpen == 64,
            "equipment marker: N = 20, M = 3, K = 64");

        var duringLoad = new EquipmentMarkerRules.Ledger();
        var loadDeparture = Dep("ITEM_L", 1);
        loadDeparture.SaveLoad = true;
        Lines(duringLoad.Depart(loadDeparture));
        var loadLines = Lines(duringLoad.SettleDue(1));
        Check(loadLines.Count == 1 && loadLines[0].EndsWith(" save_load=1", StringComparison.Ordinal),
            "equipment marker: a departure made while a save loads is reported with save_load=1");

        var counted = new EquipmentMarkerRules.Ledger();
        counted.CountZero();
        counted.CountReduced();
        counted.CountIgnored();
        counted.CountError();
        counted.CountDropped();
        Check(EquipmentMarkerRules.SummaryLine(counted.Counters)
                == "CORE_EQUIPMENT_MARKER departures=0 kept=0 sunk=0 transit=0 zero=1 reduced=1 partial=0 missing=0 unsettled=0 ignored=1 dropped=1 errors=1 capped=0",
            "equipment marker: the runtime's own counts land in their summary fields");
    }
}
