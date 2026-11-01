using System.Globalization;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiZones, the dodge: a unit of a side standing in another caster's zone steps out once per zone (TryDodge) to the
// nearest free cell (CellProbe), and a party unit seen back in an enemy area is sent out again (WatchReturns). The zones, the
// area log and the move order are in GroupAiZones.cs; the counters of the log lines are shared with it.
internal static partial class GroupAiZones
{
    private static readonly HashSet<string> dodged = new(StringComparer.Ordinal);
    // Per unit and zone: dodges so far and whether the unit was seen outside since the last one (WatchReturns).
    private static readonly Dictionary<string, (int Done, bool Left)> dodges = new(StringComparer.Ordinal);

    // Cells a dodging unit may step on: walkable, not blocked, no living unit on it and outside
    // every zone of its side.
    private sealed class CellProbe
    {
        private readonly IInMapDataSupplier inMap;
        private readonly string side;
        private readonly HashSet<long> occupied = new();

        internal CellProbe(IInMapDataSupplier inMap, string side)
        {
            this.inMap = inMap;
            this.side = side;
            var alive = inMap.GetAliveUnitsOnMap();
            for (var i = 0; alive != null && i < alive.Count; i++)
            {
                if (alive[i] == null || alive[i].IsDead) continue;
                var cell = inMap.GetLogicalPosition(alive[i].Guid);
                occupied.Add(GroupAiRules.CellKey(cell.x, cell.y));
            }
        }

        internal bool Usable(int x, int y)
        {
            var key = GroupAiRules.CellKey(x, y);
            if (occupied.Contains(key) || book.InAnyZone(side, x, y)) return false;
            var position = new Vector2Int(x, y);
            try
            {
                return inMap.IsMovable(position) && !inMap.IsBlocked(position);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    // A few times a second while enemy areas hold (GroupAiAreaDodge.OnGameTick): a party unit that dodged a zone and stands
    // outside it is marked as out; one that stands in it again after that is sent out again (TryDodge, DodgeAllowed).
    internal static void WatchReturns(IInMapDataSupplier inMap)
    {
        if (AreaZones == 0 || inMap == null || dodges.Count == 0) return;
        var now = GameClock.Now;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead) continue;
            var guid = unit.Guid.ToString();
            var at = inMap.GetLogicalPosition(unit.Guid);
            var key = GroupAiRules.CellKey(at.x, at.y);
            var back = false;
            foreach (var zone in book.Zones)
            {
                if (zone.Area == null || zone.Until <= now) continue;
                var dodgeKey = guid + "|" + zone.Id.ToString(CultureInfo.InvariantCulture);
                if (!dodges.TryGetValue(dodgeKey, out var record)) continue;
                if (!zone.Cells.Contains(key)) { if (!record.Left) dodges[dodgeKey] = (record.Done, true); }
                else if (record.Left) back = true;
            }
            if (!back || GroupAiExcluded.Has(guid) || !GroupAiGate.OpenFor(unit)) continue;
            var target = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatTarget();
            TryDodge(Players, unit, inMap, target, GroupAiPolicy.IsMelee(guid));
        }
    }

    // A melee unit keeps next to its target (anchor) when it can.
    internal static bool TryDodge(string side, UnitEntity unit, IInMapDataSupplier inMap, IEntity anchor, bool melee)
    {
        if (unit == null || inMap == null || book.Zones.Count == 0) return false;
        var guid = unit.Guid.ToString();
        var position = inMap.GetLogicalPosition(unit.Guid);
        var zone = book.ZoneAt(side, guid, position.x, position.y, GameClock.Now);
        if (zone == null) return false;
        var dodgeKey = guid + "|" + zone.Id.ToString(CultureInfo.InvariantCulture);
        // A held unit (down, stunned, bound) cannot walk (chain 1: the order for a lying unit ended "held" at
        // once, moved 0, and it was hit four times where it lay). Noted once per zone; a later call may still send it out when free.
        if (GroupAiMoveWatch.IsHeld(unit))
        {
            if (dodged.Add(dodgeKey + "|held"))
                Log($"CORE_AI_AUSWEICHEN unit={guid} from={position.x},{position.y} to=none zone={zone.Caster}|{GroupAiRules.Short(zone.Skill)} {zone.Flag} held=1 why={GroupAiMoveWatch.HeldWhy(unit)}");
            return false;
        }
        var record = dodges.TryGetValue(dodgeKey, out var r) ? r : (Done: 0, Left: false);
        if (!GroupAiAreaRules.DodgeAllowed(zone.Area != null, record.Done, record.Left)) return false;
        dodges[dodgeKey] = (record.Done + 1, false);
        var again = record.Done;
        var probe = new CellProbe(inMap, side);
        var anchored = melee && anchor != null;
        var anchorAt = anchored ? inMap.GetLogicalPosition(anchor.Guid) : position;
        var source = zone.Caster + "|" + GroupAiRules.Short(zone.Skill);
        int tx, ty;
        bool picked;
        if (zone.Area != null)
        {
            // An enemy's area: the arrow rain only below half health, nobody walks off too late for a skill
            // that asks once; the way out avoids other zones and, for ranged units, the cells next to an enemy.
            var share = GroupAiRules.Share(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.MaxHealthTotal));
            var skip = GroupAiAreaRules.SkipReason(zone.Area, share, GameClock.Now - zone.StartedAt, zone.LastHit);
            if (skip != null)
            {
                Log(GroupAiAreaRules.SkipLine(guid, zone.Flag, skip, share));
                return false;
            }
            var enemies = EnemyCells(inMap);
            picked = GroupAiAreaRules.PickCell(position.x, position.y, melee, anchored, anchorAt.x, anchorAt.y, zone.Lane, probe.Usable,
                (x, y) => book.InAnyZoneBut(side, x, y, zone),
                (x, y) => enemies.Any(e => GroupAiRules.Distance(x, y, e.X, e.Y) <= 1), GroupAiAreaRules.SearchRing(zone.Area, GroupAiRules.MaxDodgeRing), out tx, out ty);
        }
        else picked = GroupAiRules.PickDodgeCell(position.x, position.y, anchored, anchorAt.x, anchorAt.y, probe.Usable, GroupAiRules.MaxDodgeRing, out tx, out ty);
        if (!picked)
        {
            Log($"CORE_AI_AUSWEICHEN unit={guid} from={position.x},{position.y} to=none zone={source} {zone.Flag}");
            return false;
        }
        if (TryMapOf(inMap, out var map))
        {
            var info = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>());
            var state = info != null ? info.GetCombatState().ToString() : "-";
            var target = info?.GetCombatTarget();
            if (OrderMove(unit, map, new Vector2Int(tx, ty), "dodge"))
            {
                Log($"CORE_AI_AUSWEICHEN unit={guid} from={position.x},{position.y} to={tx},{ty} zone={source} {zone.Flag} state={state} target={(target != null ? target.Guid.ToString() : "-")}"
                    + (again > 0 ? " again=" + again.ToString(CultureInfo.InvariantCulture) : ""));
                return true;
            }
        }
        Warn("CORE_AI_AUSWEICHEN_FAILED unit=" + guid + ": no tick handler for the unit's map");
        return false;
    }

    // The cells of the living enemies on the map (the party's dodge keeps ranged units off their neighbour cells).
    private static List<(int X, int Y)> EnemyCells(IInMapDataSupplier inMap)
    {
        var cells = new List<(int X, int Y)>();
        var alive = inMap.GetAliveUnitsOnMap();
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            if (alive[i] == null || alive[i].IsDead || alive[i].Faction != FactionType.Enemy) continue;
            var at = inMap.GetLogicalPosition(alive[i].Guid);
            cells.Add((at.x, at.y));
        }
        return cells;
    }

    private static bool TryMapOf(IInMapDataSupplier inMap, out MapType map)
    {
        map = MapType.Base;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return false;
        foreach (var pair in maps)
        {
            if (pair.Value != null && pair.Value.Pointer == inMap.Pointer)
            {
                map = pair.Key;
                return true;
            }
        }
        return false;
    }
}
