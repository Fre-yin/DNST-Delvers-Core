using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Commit 1 (only report, only log, only equipment): an item that leaves an equipment slot
// of a party member and arrives nowhere is logged. Pure rules and the ledger of open departures; the game's hooks are in
// EquipmentMarker. Values at most N lines, off after M failures, at most K open departures.
internal static class EquipmentMarkerRules
{
    internal const int MaxLines = 20;      // N: CORE_EQUIPMENT_MISSING lines per scope (play per campaign, load per load)
    internal const int MaxFailures = 3;    // M: failures until the marker is off for the session
    internal const int MaxOpen = 64;       // K: open departures; at K the oldest is settled at once
    internal const int AmmoSlot = 6;       // the ammunition slot of the equipment (ConsumeAmmoRequested)
    internal const long NoCredit = -1;     // an arrival that may not leave credit behind

    // What an AddItem on an equipment slot does to the holder that was there (effect 2).
    internal enum OverwriteKind { None, Reduced, Departure }

    internal static OverwriteKind Overwrite(string oldKey, Guid oldGuid, int oldAmount, string newKey, Guid newGuid, int newAmount)
    {
        if (string.IsNullOrEmpty(oldKey) || oldGuid == Guid.Empty || oldAmount <= 0) return OverwriteKind.None;
        if (!SameHolder(oldKey, oldGuid, newKey, newGuid)) return OverwriteKind.Departure;
        return newAmount < oldAmount ? OverwriteKind.Reduced : OverwriteKind.None;
    }

    // The arrival an AddItem makes (effect 3): the new amount minus the old one for the same holder, else the full new amount.
    internal static int AddItemArrival(string oldKey, Guid oldGuid, int oldAmount, string newKey, Guid newGuid, int newAmount)
    {
        if (newAmount <= 0) return 0;
        return SameHolder(oldKey, oldGuid, newKey, newGuid) ? Math.Max(0, newAmount - Math.Max(0, oldAmount)) : newAmount;
    }

    // ItemDataContainer.OnSpawn (effect 3): the new amount minus the amount the item had, only when positive.
    internal static int SpawnArrival(int oldAmount, int newAmount) => Math.Max(0, newAmount - Math.Max(0, oldAmount));

    private static bool SameHolder(string oldKey, Guid oldGuid, string newKey, Guid newGuid)
        => string.Equals(oldKey, newKey, StringComparison.Ordinal) && oldGuid == newGuid;

    // A holder counts as a departure only with a key, a guid and an amount above 0; else it counts under zero (effect 1).
    internal static bool IsZero(string key, Guid guid, int amount) => string.IsNullOrEmpty(key) || guid == Guid.Empty || amount <= 0;

    // A departure is settled once at least one tick postfix ran fully after it (effect 5).
    internal static bool Due(long departedAtPostfix, long postfixesDone) => postfixesDone >= departedAtPostfix + 1;

    // ---- which inventories are watched (effect 1) ----

    internal enum Watch { Other, Ignored, Party }

    // Only an equipment of a unit; a Core unit or a summon counts ignored, a unit of another faction is not watched.
    internal static Watch WatchOf(bool equipment, bool unit, bool coreUnit, bool summoned, bool player)
    {
        if (!equipment || !unit) return Watch.Other;
        if (coreUnit || summoned) return Watch.Ignored;
        return player ? Watch.Party : Watch.Other;
    }

    // ---- frames of the game's own ways (effect 4) ----

    internal enum FrameKind { Sunk, Despawn, Transit, Load }

    internal enum Route { Open, Sunk, Despawn, Transit, Load }

    internal readonly struct Frame
    {
        internal readonly FrameKind Kind;
        internal readonly IntPtr Unit;
        internal readonly int Slot;     // -1: every slot of the unit

        internal Frame(FrameKind kind, IntPtr unit, int slot)
        {
            Kind = kind;
            Unit = unit;
            Slot = slot;
        }
    }

    // DestroyItem and the last arrow sink one slot; a dead unit's despawn sinks all, a living one's is reported; a transfer is
    // in transit; LoadEquipmentSlots only takes notes.
    internal static Frame FrameOf(string hook, IntPtr unit, int slot, bool unitDead) => hook switch
    {
        "destroy" or "ammo" => new Frame(FrameKind.Sunk, unit, slot),
        "despawn" => new Frame(unitDead ? FrameKind.Sunk : FrameKind.Despawn, unit, -1),
        "transfer" => new Frame(FrameKind.Transit, unit, -1),
        _ => new Frame(FrameKind.Load, unit, -1)
    };

    // The innermost open frame of the unit that covers the slot.
    internal static Frame? FrameFor(IReadOnlyList<Frame> frames, IntPtr unit, int slot)
    {
        for (var i = frames.Count - 1; i >= 0; i--)
            if (frames[i].Unit == unit && (frames[i].Slot < 0 || frames[i].Slot == slot)) return frames[i];
        return null;
    }

    internal static Route RouteOf(Frame? frame) => frame?.Kind switch
    {
        FrameKind.Sunk => Route.Sunk,
        FrameKind.Despawn => Route.Despawn,
        FrameKind.Transit => Route.Transit,
        FrameKind.Load => Route.Load,
        _ => Route.Open
    };

    // A living party member's despawn is reported as retire inside RetireUnitRequested, else as despawn.
    internal static string DespawnWhy(bool retiring) => retiring ? "retire" : "despawn";

    // ---- failures (effect 8) ----

    // M failures switch the marker off for the session; Record is true exactly once, at the failure that switches it off.
    internal sealed class Failures
    {
        private int count;

        internal bool Off { get; private set; }

        internal bool Record()
        {
            count++;
            if (Off || count < MaxFailures) return false;
            Off = true;
            return true;
        }
    }

    // ---- log lines (effect 6; word for word) ----

    internal static string MissingLine(string unit, int map, string slot, string key, int amount, int arrived, long tick, string during, string why, bool saveLoad)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_EQUIPMENT_MISSING unit={0} map={1} slot={2} key={3} amount={4} arrived={5} tick={6} during={7} why={8} save_load={9}",
            unit, map, slot, string.IsNullOrEmpty(key) ? "-" : key, amount, arrived, tick, during, why, saveLoad ? 1 : 0);

    internal static string CappedLine(string during)
        => "CORE_EQUIPMENT_MISSING_CAPPED after=" + MaxLines.ToString(CultureInfo.InvariantCulture) + " during=" + during;

    internal static string DepartureLine(string unit, int slot, string key, int amount, string outcome)
        => string.Format(CultureInfo.InvariantCulture, "CORE_EQUIPMENT_DEPARTURE unit={0} slot={1} key={2} amount={3} outcome={4}",
            unit, slot, string.IsNullOrEmpty(key) ? "-" : key, amount, outcome);

    internal static string OffLine(string stage) => "CORE_EQUIPMENT_MARKER_OFF reason=errors stage=" + stage;

    internal static string SummaryLine(Counters c)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_EQUIPMENT_MARKER departures={0} kept={1} sunk={2} transit={3} zero={4} reduced={5} partial={6} missing={7} unsettled={8} ignored={9} dropped={10} errors={11} capped={12}",
            c.Departures, c.Kept, c.Sunk, c.Transit, c.Zero, c.Reduced, c.Partial, c.Missing, c.Unsettled, c.Ignored, c.Dropped, c.Errors, c.Capped);

    // The summary when the marker switches off: the counters so far, marked so it is never read as the summary of a bind.
    internal static string OffSummaryLine(Counters c) => SummaryLine(c) + " at=off";

    // The counters of one bound campaign (effect 6, the summary line).
    internal sealed class Counters
    {
        internal int Departures, Kept, Sunk, Transit, Zero, Reduced, Partial, Missing, Unsettled, Ignored, Dropped, Errors, Capped;
    }

    // One departure (effect 1).
    internal sealed class Departure
    {
        internal string Unit;
        internal int Map;
        internal int Slot;
        internal string Key;
        internal Guid ItemGuid;
        internal int Amount;
        internal int Arrived;
        internal long Tick;
        internal long AtPostfix;
        internal long Frame;
        internal bool SaveLoad;
        // Why an unpaid departure is reported: unexplained, or overwrite for a holder an AddItem replaced.
        internal string Why;
    }

    // A line the ledger wants written, with its scope for the caps.
    internal readonly struct Report
    {
        internal readonly string Line;
        internal readonly bool Load;

        internal Report(string line, bool load)
        {
            Line = line;
            Load = load;
        }
    }

    // The open departures and the counters of one bound campaign; pure, the runtime feeds it (effects 3, 5, 6, 7).
    internal sealed class Ledger
    {
        private readonly List<Departure> open = new();
        // A slot swap through two overwrites brings the second holder's arrival before its departure (both kept). Only an
        // arrival from an AddItem that overwrote another holder on a party member's equipment slot leaves credit, and only for
        // the same frame; any other arrival before a departure does not count (effect 3).
        private readonly Dictionary<string, int> credit = new(StringComparer.Ordinal);
        private long creditFrame = NoCredit;
        private int playLines;
        private int loadLines;
        private bool playCapped;
        private bool loadCapped;

        internal Counters Counters { get; private set; } = new();
        internal int Open => open.Count;
        internal bool DepartureLines { get; set; }

        // Counts the runtime books without a ledger decision: an empty holder, a reduced stack, an ignored owner, an own
        // failure and a note dropped after one.
        internal void CountZero() => Counters.Zero++;
        internal void CountReduced() => Counters.Reduced++;
        internal void CountIgnored() => Counters.Ignored++;
        internal void CountError() => Counters.Errors++;
        internal void CountDropped() => Counters.Dropped++;

        // A holder left a slot. Credit of the same frame pays it first; only one that stays open takes a place, and at K
        // open departures the oldest is settled first (as unsettled).
        internal IEnumerable<Report> Depart(Departure departure)
        {
            Counters.Departures++;
            var reports = new List<Report>();
            PayFromCredit(departure);
            if (departure.Arrived >= departure.Amount)
            {
                Counters.Kept++;
                reports.AddRange(DepartureReport(departure, "kept"));
                return reports;
            }
            if (open.Count >= MaxOpen)
            {
                var oldest = open[0];
                open.RemoveAt(0);
                reports.AddRange(Settle(oldest, "unsettled"));
            }
            open.Add(departure);
            return reports;
        }

        private void PayFromCredit(Departure d)
        {
            if (d.Frame != creditFrame || string.IsNullOrEmpty(d.Key) || !credit.TryGetValue(d.Key, out var left) || left <= 0) return;
            var pay = Math.Min(left, d.Amount - d.Arrived);
            d.Arrived += pay;
            credit[d.Key] = left - pay;
        }

        // A holder that left inside a frame that sinks it (destroyed, last arrow, a dead unit's despawn): settled at once.
        internal IEnumerable<Report> Sink(Departure departure)
        {
            Counters.Departures++;
            Counters.Sunk++;
            return DepartureReport(departure, "sunk");
        }

        // An arrival of key: it pays open departures of that key, the oldest first; a fully paid one is kept. What is left
        // becomes credit only with a frame (an overwriting AddItem on a party member's equipment, NoCredit otherwise).
        internal IEnumerable<Report> Arrive(string key, int amount, long creditFrameOfArrival = NoCredit)
        {
            var reports = new List<Report>();
            if (string.IsNullOrEmpty(key) || amount <= 0) return reports;
            for (var i = 0; i < open.Count && amount > 0; i++)
            {
                var d = open[i];
                if (!string.Equals(d.Key, key, StringComparison.Ordinal)) continue;
                var pay = Math.Min(amount, d.Amount - d.Arrived);
                d.Arrived += pay;
                amount -= pay;
                if (d.Arrived < d.Amount) continue;
                open.RemoveAt(i--);
                Counters.Kept++;
                reports.AddRange(DepartureReport(d, "kept"));
            }
            if (amount > 0 && creditFrameOfArrival != NoCredit) AddCredit(key, amount, creditFrameOfArrival);
            return reports;
        }

        private void AddCredit(string key, int amount, long frame)
        {
            if (frame != creditFrame)
            {
                credit.Clear();
                creditFrame = frame;
            }
            credit[key] = (credit.TryGetValue(key, out var had) ? had : 0) + amount;
        }

        // The tick postfix: every departure that is due is settled (partial or unexplained); unused credit ends.
        internal IEnumerable<Report> SettleDue(long postfixesDone)
        {
            ClearCredit();
            var reports = new List<Report>();
            for (var i = 0; i < open.Count; i++)
            {
                if (!Due(open[i].AtPostfix, postfixesDone)) continue;
                var d = open[i];
                open.RemoveAt(i--);
                reports.AddRange(Settle(d, "unexplained"));
            }
            return reports;
        }

        // Before the campaign is bound anew: every open departure is settled (unsettled), then the summary.
        internal IEnumerable<Report> SettleAll()
        {
            var reports = new List<Report>();
            while (open.Count > 0)
            {
                var d = open[0];
                open.RemoveAt(0);
                reports.AddRange(Settle(d, "unsettled"));
            }
            return reports;
        }

        // A departure reported without the arrival check (a living unit's despawn, retire, transfer, load repair), through the
        // caps. Each counts as a departure; a transfer also under transit.
        internal IEnumerable<Report> ReportMissing(string line, bool load, bool transit, string unit, int slot, string key, int amount, string why)
        {
            Counters.Departures++;
            Counters.Missing++;
            if (transit) Counters.Transit++;
            var reports = new List<Report>(Capped(line, load));
            reports.AddRange(DepartureReport(new Departure { Unit = unit, Slot = slot, Key = key, Amount = amount }, why));
            return reports;
        }

        // Every new campaign: counters, open departures and the play cap start over; the load cap is per load.
        internal void Reset()
        {
            open.Clear();
            ClearCredit();
            Counters = new Counters();
            playLines = 0;
            playCapped = false;
        }

        internal void ResetLoadCap()
        {
            loadLines = 0;
            loadCapped = false;
        }

        // After the marker went off: what was open, and the load notes not reported, are dropped without a line.
        internal void Drop(int notes)
        {
            Counters.Dropped += open.Count + Math.Max(0, notes);
            open.Clear();
            ClearCredit();
        }

        private void ClearCredit()
        {
            credit.Clear();
            creditFrame = NoCredit;
        }

        private IEnumerable<Report> Settle(Departure d, string why)
        {
            if (d.Arrived > 0)
            {
                Counters.Partial++;
                return DepartureReport(d, "partial");
            }
            if (why != "unsettled") why = d.Why ?? why;
            if (why == "unsettled") Counters.Unsettled++;
            else Counters.Missing++;
            var line = MissingLine(d.Unit, d.Map, d.Slot.ToString(CultureInfo.InvariantCulture), d.Key, d.Amount, d.Arrived, d.Tick, "play", why, d.SaveLoad);
            var reports = new List<Report>(Capped(line, false));
            reports.AddRange(DepartureReport(d, why));
            return reports;
        }

        // N lines per scope, then one CAPPED line; counting goes on (effect 7).
        private IEnumerable<Report> Capped(string line, bool load)
        {
            ref var lines = ref load ? ref loadLines : ref playLines;
            ref var capped = ref load ? ref loadCapped : ref playCapped;
            if (lines < MaxLines)
            {
                lines++;
                return new[] { new Report(line, load) };
            }
            Counters.Capped++;
            if (capped) return Array.Empty<Report>();
            capped = true;
            return new[] { new Report(CappedLine(load ? "load" : "play"), load) };
        }

        private IEnumerable<Report> DepartureReport(Departure d, string outcome)
            => DepartureLines ? new[] { new Report(DepartureLine(d.Unit, d.Slot, d.Key, d.Amount, outcome), false) } : Array.Empty<Report>();
    }
}
