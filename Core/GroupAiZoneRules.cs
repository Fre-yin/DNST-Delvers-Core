namespace DungeonSettlersDelvers.Core;

// GroupAiZones and the zone lock as rules without the game: the book of marked zones (which zone a unit stands in, when an area's
// execution joins the caster's zone, which zones drop), the cells an enemy area covers, the party unit nearest in reach for a swapped
// target and when a hold applies. Cells are (X, Y); the game's types stay in GroupAiZones, GroupAiZoneLock and GroupAiAreaDodge.
internal static class GroupAiZoneRules
{
    // The range of a charge's lane when the skill has no row.
    internal const int DefaultLaneRange = 12;

    internal sealed class Zone
    {
        internal int Id;
        internal string Side = "";
        internal string Caster = "";
        internal string Skill = "";
        internal string Flag = "";
        internal readonly HashSet<long> Cells = new();
        internal float Until;
        // An enemy's area (GroupAiAreaDodge): its rules, the game's execution that shows and deals it, the charge's direction.
        internal GroupAiAreaRules.Area Area;
        // The game's executions that show and deal it: one zone per caster and area, also for the arrow rain's waves and shots
        // (EAD67420: 16 to 22 executions a round, each its own zone before).
        internal readonly HashSet<IntPtr> Execs = new();
        internal (int X, int Y)? Lane;
        internal float StartedAt;
        internal float LastHit;
        internal string ExecId = "-";

        internal bool Holds(int x, int y) => Cells.Contains(GroupAiRules.CellKey(x, y));
    }

    // The zones marked so far in the order they were marked, and how many of them are enemy areas.
    internal sealed class Book
    {
        private readonly List<Zone> zones = new();
        private int nextId;

        internal IReadOnlyList<Zone> Zones => zones;

        // Enemy areas held now; the ProcessFX hook leaves at once while this is 0.
        internal int AreaZones { get; private set; }

        // The zone ids keep counting across a reset.
        internal void Reset()
        {
            zones.Clear();
            AreaZones = 0;
        }

        // A zone of Core's own areas; the caster's earlier zone of the same skill goes.
        internal Zone Mark(string side, string caster, string skill, string flag, IEnumerable<(int X, int Y)> cells, float until)
        {
            Clear(caster, skill);
            var zone = new Zone { Id = ++nextId, Side = side, Caster = caster, Skill = skill, Flag = flag, Until = until };
            AddCells(zone, cells);
            zones.Add(zone);
            return zone;
        }

        // An enemy's area for the party, held until the game's execution ends (EndExecution) or Until at the latest.
        // True for a new zone, false when the execution joined the caster's zone of that area still held.
        internal bool MarkArea(string side, IntPtr exec, string caster, GroupAiAreaRules.Area area, IEnumerable<(int X, int Y)> cells, float startedAt,
            (int X, int Y)? lane, float? lastHit, string execId)
        {
            var last = lastHit ?? area.LastHit;
            var held = HeldAreaZone(caster, area, startedAt);
            if (held != null)
            {
                held.Execs.Add(exec);
                AddCells(held, cells);
                held.Until = Math.Max(held.Until, GroupAiAreaRules.HoldUntil(startedAt, last));
                return false;
            }
            var zone = new Zone
            {
                Id = ++nextId, Side = side, Caster = caster, Skill = area.Source, Flag = area.Flag, Until = GroupAiAreaRules.HoldUntil(startedAt, last), Area = area,
                Lane = lane, StartedAt = startedAt, LastHit = last, ExecId = execId ?? "-"
            };
            zone.Execs.Add(exec);
            AddCells(zone, cells);
            zones.Add(zone);
            AreaZones++;
            return true;
        }

        // The caster's first zone of this area that is still held when the execution starts.
        private Zone HeldAreaZone(string caster, GroupAiAreaRules.Area area, float startedAt)
        {
            foreach (var zone in zones)
                if (zone.Area == area && zone.Caster == caster && zone.Until > startedAt) return zone;
            return null;
        }

        private static void AddCells(Zone zone, IEnumerable<(int X, int Y)> cells)
        {
            foreach (var cell in cells) zone.Cells.Add(GroupAiRules.CellKey(cell.X, cell.Y));
        }

        // The zones of a caster and skill go. An enemy area among them is not taken off the count of enemy areas.
        internal void Clear(string caster, string skill)
        {
            for (var i = zones.Count - 1; i >= 0; i--)
                if (zones[i].Caster == caster && zones[i].Skill == skill) zones.RemoveAt(i);
        }

        // One of an area's executions ended; the zone goes with its last one. True when the zone went.
        internal bool EndExecution(IntPtr exec)
        {
            var ended = false;
            for (var i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i].Area == null || !zones[i].Execs.Remove(exec) || zones[i].Execs.Count > 0) continue;
                zones.RemoveAt(i);
                AreaZones--;
                ended = true;
            }
            return ended;
        }

        internal bool IsAreaExecution(IntPtr exec)
        {
            foreach (var zone in zones) if (zone.Area != null && zone.Execs.Contains(exec)) return true;
            return false;
        }

        // The execution that opened the held zone of this caster, or null when none is held.
        internal string ZoneExecOf(string caster, float now)
        {
            if (AreaZones == 0 || string.IsNullOrEmpty(caster)) return null;
            foreach (var zone in zones) if (zone.Area != null && zone.Until > now && zone.Caster == caster) return zone.ExecId;
            return null;
        }

        // The latest end of the enemy areas that hold now and contain one of these cells, 0 when none does.
        internal float UntilCrossed(List<(int X, int Y)> cells, float now)
        {
            if (AreaZones == 0 || cells == null || cells.Count == 0) return 0f;
            var until = 0f;
            foreach (var zone in zones)
            {
                if (zone.Area == null || zone.Until <= now) continue;
                foreach (var c in cells)
                    if (zone.Holds(c.X, c.Y)) { until = Math.Max(until, zone.Until); break; }
            }
            return until;
        }

        // Whether a cell lies in an enemy area that holds now.
        internal bool InEnemyAreaCell(int x, int y, float now)
        {
            if (AreaZones == 0) return false;
            foreach (var zone in zones) if (zone.Area != null && zone.Until > now && zone.Holds(x, y)) return true;
            return false;
        }

        // Drops expired zones; the first zone of the side that holds the cell, not marked by the unit itself.
        internal Zone ZoneAt(string side, string self, int x, int y, float now)
        {
            DropExpired(now);
            foreach (var zone in zones)
                if (zone.Side == side && zone.Caster != self && zone.Holds(x, y)) return zone;
            return null;
        }

        private void DropExpired(float now)
        {
            for (var i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i].Until > now) continue;
                if (zones[i].Area != null) AreaZones--;
                zones.RemoveAt(i);
            }
        }

        // A zone on the way out, other than the one the unit leaves.
        internal bool InAnyZoneBut(string side, int x, int y, Zone own)
        {
            foreach (var zone in zones)
                if (zone != own && zone.Side == side && zone.Holds(x, y)) return true;
            return false;
        }

        internal bool InAnyZone(string side, int x, int y)
        {
            foreach (var zone in zones)
                if (zone.Side == side && zone.Holds(x, y)) return true;
            return false;
        }
    }

    // The direction of a charge: the signs of the way from the user to the target.
    internal static (int X, int Y) LaneDirection(int fromX, int fromY, int toX, int toY) => (Math.Sign(toX - fromX), Math.Sign(toY - fromY));

    // A charge runs its whole range along the line through its target (LaneCells), each step with the row's width.
    internal static HashSet<(int X, int Y)> LaneKeys(GroupAiAreaRules.Area area, int fromX, int fromY, int toX, int toY, int range)
    {
        var keys = new HashSet<(int X, int Y)>();
        foreach (var step in GroupAiAreaRules.LaneCells(fromX, fromY, toX, toY, range))
            foreach (var c in GroupAiAreaRules.Cells(GroupAiAreaRules.Shape.Square, area.Radius, step.X, step.Y)) keys.Add(c);
        return keys;
    }

    // An area that centres on the user, over every cell the user's size may cover.
    internal static HashSet<(int X, int Y)> CenterKeys(GroupAiAreaRules.Area area, int fromX, int fromY, int width, int height)
    {
        var keys = new HashSet<(int X, int Y)>();
        foreach (var cell in GroupAiAreaRules.Footprint(fromX, fromY, width, height))
            foreach (var c in GroupAiAreaRules.Cells(area.Shape, area.Radius, cell.X, cell.Y + area.ShiftY)) keys.Add(c);
        return keys;
    }

    // The game's cells of the skill when it names some; else the rule's shape around the target, or around the user's cell when the
    // skill has no row (hasRow false).
    internal static HashSet<(int X, int Y)> ShapeKeys(GroupAiAreaRules.Area area, bool hasRow, int fromX, int fromY, int toX, int toY,
        IEnumerable<(int X, int Y)> gameCells)
    {
        var keys = new HashSet<(int X, int Y)>();
        if (gameCells != null) foreach (var c in gameCells) keys.Add(c);
        else foreach (var c in GroupAiAreaRules.Cells(area.Shape, area.Radius, hasRow ? toX : fromX, hasRow ? toY : fromY)) keys.Add(c);
        return keys;
    }

    // The index of the cell nearest to the given one that is within reach for a skill of this range (GroupAiRules.InRange), the first
    // of equals; -1 when none is.
    internal static int NearestInReach(int fromX, int fromY, int range, IReadOnlyList<(int X, int Y)> spots)
    {
        var best = -1;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < spots.Count; i++)
        {
            var distance = GroupAiRules.Distance(fromX, fromY, spots[i].X, spots[i].Y);
            if (!GroupAiRules.InRange(distance, range) || distance >= bestDistance) continue;
            best = i;
            bestDistance = distance;
        }
        return best;
    }

    // The zone a held choice crossed has ended.
    internal static bool HoldExpired(float now, float until) => now >= until;

    // Only a party unit that takes part in Core's AI, whose behaviour is open and whose player gave no order within the quiet time.
    internal static bool MayHold(bool party, bool excluded, bool gateOpen, bool playerQuiet) => party && !excluded && gateOpen && !playerQuiet;
}
