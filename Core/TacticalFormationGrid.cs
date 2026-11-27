namespace DungeonSettlersDelvers.Core;

// The formation window's model (TacticalFormationWindow): the group's list, the 9 by 9 grid and each member's priority casts.
// The list shows VisibleRows rows and scrolls past them; its top member wears the crown (the highest place is the crown, the
// order changes by drag and drop). In the grid column 0 is the left, row 0 the front. The crown stands in the middle cell
// (9 by 9, the crown exactly in the middle), so every place is at most TacticalFormationRules.MaxOffset
// from it; a drop on an occupied cell swaps the two, the crown's cell takes no drop and its member is not dragged. Each member has CastSlots places for learned active skills, the first one first. The
// group's summon places (TacticalFormationSummons.Place.Id) stand in the grid like members, below them in the list: they never
// wear the crown and have no casts. Members and summon places fill the rows (a fixed selection, added with "+"
// and removed with a right click; no cap of eight, since packs allow bigger groups). The grid itself is the bound: one cell each,
// MaxRows in all. Pure, so the offline tests cover it.
internal sealed class TacticalFormationGrid
{
    internal const int Size = TacticalFormationRules.GridSize;
    // The list's rows in view (eight); more scroll.
    internal const int VisibleRows = 8;
    // Every row needs a cell of the 9 by 9 grid: the crown and 80 more.
    internal const int MaxRows = Size * Size;
    // Five places fit beside the name (first three).
    internal const int CastSlots = 5;
    internal const int Center = Size / 2;

    private readonly Guid[,] cells = new Guid[Size, Size];
    private readonly List<Guid> members;
    private readonly List<Guid> summons = new();
    private readonly Dictionary<Guid, string[]> casts = new();
    private readonly Dictionary<Guid, TacticalRole> roles = new();

    private TacticalFormationGrid(List<Guid> members) => this.members = members;

    // The top of the list; Guid.Empty for an empty group.
    internal Guid Leader => members.Count > 0 ? members[0] : Guid.Empty;

    // The list from the top: the leader, then the others in the saved order.
    internal IReadOnlyList<Guid> Members => members;

    // The summon places in the grid, in the list's order below the members.
    internal IReadOnlyList<Guid> Summons => summons;

    internal bool IsSummon(Guid unit) => summons.Contains(unit);

    // The list's rows in use: members and summon places.
    internal int Count => members.Count + summons.Count;

    // How many members of the group did not fit the list.
    internal int Left { get; private set; }

    // The grid of a group: the saved leader on top, then the members in the order of the saved places, then the rest in the
    // group's order, at most MaxRows; the saved places where they fit, every other member on the next default place. A saved
    // leader outside the group or Guid.Empty leaves the group's order. casts: the saved priority casts per member. summonPlaces:
    // the summon places to show, in the rows left after the members; saved holds their cells under the place's id, the others
    // take the next default places after the members.
    internal static TacticalFormationGrid Create(IReadOnlyList<Guid> group, Guid leader, IReadOnlyList<TacticalFormationRules.Slot> saved,
        IReadOnlyDictionary<Guid, string[]> casts = null, IReadOnlyList<Guid> summonPlaces = null, IReadOnlyDictionary<Guid, TacticalRole> roles = null)
    {
        var known = new List<Guid>();
        foreach (var unit in group)
            if (unit != Guid.Empty && !known.Contains(unit)) known.Add(unit);
        var members = new List<Guid>();
        if (known.Contains(leader)) members.Add(leader);
        foreach (var slot in saved ?? Array.Empty<TacticalFormationRules.Slot>())
            if (known.Contains(slot.Unit) && !members.Contains(slot.Unit) && members.Count < MaxRows) members.Add(slot.Unit);
        foreach (var unit in known)
            if (!members.Contains(unit) && members.Count < MaxRows) members.Add(unit);
        var grid = new TacticalFormationGrid(members) { Left = known.Count - members.Count };
        foreach (var place in summonPlaces ?? Array.Empty<Guid>())
            if (members.Count > 0 && grid.Count < MaxRows && place != Guid.Empty && !known.Contains(place) && !grid.summons.Contains(place))
                grid.summons.Add(place);
        foreach (var unit in members)
            if (casts != null && casts.TryGetValue(unit, out var keys))
                for (var i = 0; i < CastSlots && i < keys.Length; i++) grid.SetCast(unit, i, keys[i]);
        foreach (var unit in members)
            if (roles != null && roles.TryGetValue(unit, out var role)) grid.SetRole(unit, role);
        if (members.Count > 0) grid.Lay(members.Contains(leader) ? saved : null);
        return grid;
    }

    internal Guid At(int column, int row) => Inside(column, row) ? cells[column, row] : Guid.Empty;

    internal (int Column, int Row)? CellOf(Guid unit)
    {
        if (unit == Guid.Empty) return null;
        for (var column = 0; column < Size; column++)
            for (var row = 0; row < Size; row++)
                if (cells[column, row] == unit) return (column, row);
        return null;
    }

    // A member dragged from one cell onto another: an occupied target swaps the two. False when nothing moved, also for the
    // crown's cell.
    internal bool Drop(int fromColumn, int fromRow, int toColumn, int toRow)
    {
        if (!Inside(fromColumn, fromRow) || !Inside(toColumn, toRow) || (fromColumn == toColumn && fromRow == toRow)
            || IsCenter(fromColumn, fromRow) || IsCenter(toColumn, toRow)) return false;
        var moved = cells[fromColumn, fromRow];
        if (moved == Guid.Empty) return false;
        cells[fromColumn, fromRow] = cells[toColumn, toRow];
        cells[toColumn, toRow] = moved;
        return true;
    }

    // A member dragged to another place of the list; the others close up. A new member on top wears the crown and takes the
    // middle cell; the former leader takes the new one's cell, everybody else keeps theirs.
    internal bool MoveInList(Guid unit, int index)
    {
        var from = members.IndexOf(unit);
        if (from < 0 || index < 0 || index >= members.Count || index == from) return false;
        var former = Leader;
        members.RemoveAt(from);
        members.Insert(index, unit);
        if (Leader != former && CellOf(Leader) is { } cell)
        {
            cells[cell.Column, cell.Row] = former;
            cells[Center, Center] = Leader;
        }
        return true;
    }

    internal static bool IsCenter(int column, int row) => column == Center && row == Center;

    // A drag is on while a member is taken from a grid cell or from a list row; the drag picture follows the pointer for both.
    internal static bool Dragging(int cellFrom, int rowFrom) => cellFrom >= 0 || rowFrom >= 0;

    internal bool SetCrown(Guid unit) => MoveInList(unit, 0);

    // ---- the fixed selection ----

    // A member added on the next free row and the next free default place; the first one wears the crown in the middle. False when
    // the rows are full or it is there already.
    internal bool Add(Guid unit)
    {
        if (unit == Guid.Empty || Count >= MaxRows || members.Contains(unit) || summons.Contains(unit)) return false;
        members.Add(unit);
        if (members.Count == 1) cells[Center, Center] = unit;
        else PutDefault(unit);
        return true;
    }

    // A summon place added below the members, on the next free default place; not without a member.
    internal bool AddSummon(Guid place)
    {
        if (place == Guid.Empty || members.Count == 0 || Count >= MaxRows || members.Contains(place) || summons.Contains(place)) return false;
        summons.Add(place);
        PutDefault(place);
        return true;
    }

    // A row taken out (a right click): its cell becomes free. Without the crown's member the next member wears it and takes the
    // middle cell; without any member the summon places go too.
    internal bool Remove(Guid unit)
    {
        if (summons.Remove(unit))
        {
            Clear(unit);
            return true;
        }
        var index = members.IndexOf(unit);
        if (index < 0) return false;
        members.RemoveAt(index);
        casts.Remove(unit);
        roles.Remove(unit);
        Clear(unit);
        if (members.Count == 0)
        {
            foreach (var place in summons) Clear(place);
            summons.Clear();
            return true;
        }
        if (index == 0 && CellOf(Leader) is { } cell)
        {
            cells[cell.Column, cell.Row] = Guid.Empty;
            cells[Center, Center] = Leader;
        }
        return true;
    }

    private void Clear(Guid unit)
    {
        if (CellOf(unit) is { } cell) cells[cell.Column, cell.Row] = Guid.Empty;
    }

    // The next default place that is free, else any free cell.
    private void PutDefault(Guid unit)
    {
        foreach (var (x, y) in TacticalFormationRules.DefaultOffsets(Size * Size))
        {
            var (c, r) = (Center + x, Center - y);
            if (!Inside(c, r) || cells[c, r] != Guid.Empty) continue;
            cells[c, r] = unit;
            return;
        }
        PutAnywhere(unit);
    }

    // ---- roles (each formation its own) ----

    internal TacticalRole RoleOf(Guid unit) => roles.TryGetValue(unit, out var role) ? role : TacticalRole.Neutral;

    // A member's role; Neutral clears it. False for a stranger or no change.
    internal bool SetRole(Guid unit, TacticalRole role)
    {
        if (!members.Contains(unit) || RoleOf(unit) == role) return false;
        if (role == TacticalRole.Neutral) roles.Remove(unit);
        else roles[unit] = role;
        return true;
    }

    // The roles of every member that is not Neutral, for the book.
    internal Dictionary<Guid, TacticalRole> AllRoles()
    {
        var result = new Dictionary<Guid, TacticalRole>();
        foreach (var unit in members)
            if (roles.TryGetValue(unit, out var role)) result[unit] = role;
        return result;
    }

    // ---- priority casts ----

    // The member's casts by place, "" for an empty one.
    internal IReadOnlyList<string> Casts(Guid unit) => casts.TryGetValue(unit, out var keys) ? keys : Empty;

    private static readonly string[] Empty = NoCasts();

    private static string[] NoCasts()
    {
        var keys = new string[CastSlots];
        Array.Fill(keys, "");
        return keys;
    }

    // A skill on one of the member's places; null or "" empties it. The same skill on another place of the member leaves it.
    internal bool SetCast(Guid unit, int place, string skill)
    {
        if (!members.Contains(unit) || place < 0 || place >= CastSlots) return false;
        skill = string.IsNullOrWhiteSpace(skill) ? "" : skill;
        if (!casts.TryGetValue(unit, out var keys)) casts[unit] = keys = NoCasts();
        if (keys[place] == skill) return false;
        for (var i = 0; i < CastSlots; i++)
            if (skill.Length > 0 && keys[i] == skill) keys[i] = "";
        keys[place] = skill;
        return true;
    }

    // The casts of every member with at least one, for the save.
    internal Dictionary<Guid, string[]> AllCasts()
    {
        var result = new Dictionary<Guid, string[]>();
        foreach (var unit in members)
            if (casts.TryGetValue(unit, out var keys) && Array.Exists(keys, key => key.Length > 0)) result[unit] = (string[])keys.Clone();
        return result;
    }

    // The default places around the crown (TacticalFormationRules.DefaultOffsets).
    internal void Reset() => Lay(null);

    // The places of every member but the leader, relative to the leader: x to the right, y to the front.
    internal List<TacticalFormationRules.Slot> Slots()
    {
        var slots = new List<TacticalFormationRules.Slot>();
        var leaderCell = CellOf(Leader);
        if (leaderCell == null) return slots;
        foreach (var unit in members)
        {
            if (unit == Leader || CellOf(unit) is not { } cell) continue;
            slots.Add(new TacticalFormationRules.Slot(unit, cell.Column - leaderCell.Value.Column, leaderCell.Value.Row - cell.Row));
        }
        return slots;
    }

    // The summon places with their cells relative to the leader, as Slots gives the members'.
    internal List<TacticalFormationRules.Slot> SummonSlots()
    {
        var slots = new List<TacticalFormationRules.Slot>();
        var leaderCell = CellOf(Leader);
        if (leaderCell == null) return slots;
        foreach (var place in summons)
            if (CellOf(place) is { } cell)
                slots.Add(new TacticalFormationRules.Slot(place, cell.Column - leaderCell.Value.Column, leaderCell.Value.Row - cell.Row));
        return slots;
    }

    // The leader in the middle, then the saved places, then the defaults for the rest. A saved place farther than the grid
    // (an older 8 by 8 formation) was already drawn to the edge (Slot); when two land on one cell, the later one takes the
    // nearest free cell.
    private void Lay(IReadOnlyList<TacticalFormationRules.Slot> saved)
    {
        Array.Clear(cells, 0, cells.Length);
        const int column = Center, row = Center;
        cells[column, row] = Leader;
        foreach (var slot in saved ?? Array.Empty<TacticalFormationRules.Slot>())
        {
            if (slot.Unit == Leader || !(members.Contains(slot.Unit) || summons.Contains(slot.Unit)) || CellOf(slot.Unit) != null) continue;
            var (c, r) = (column + slot.X, row - slot.Y);
            if (!Inside(c, r)) continue;
            if (cells[c, r] == Guid.Empty) cells[c, r] = slot.Unit;
            else if (NearestFree(c, r) is { } free) cells[free.Column, free.Row] = slot.Unit;
        }
        var defaults = TacticalFormationRules.DefaultOffsets(Size * Size);
        var next = 0;
        var everyone = new List<Guid>(members);
        everyone.AddRange(summons);
        foreach (var unit in everyone)
        {
            if (CellOf(unit) != null) continue;
            while (next < defaults.Count)
            {
                var (c, r) = (column + defaults[next].X, row - defaults[next].Y);
                next++;
                if (!Inside(c, r) || cells[c, r] != Guid.Empty) continue;
                cells[c, r] = unit;
                break;
            }
            if (CellOf(unit) == null) PutAnywhere(unit);
        }
    }

    // The free cell nearest to a cell, ring by ring; null when the grid is full.
    private (int Column, int Row)? NearestFree(int column, int row)
    {
        for (var ring = 1; ring < Size; ring++)
        {
            (int Column, int Row)? best = null;
            var bestDistance = int.MaxValue;
            for (var dc = -ring; dc <= ring; dc++)
                for (var dr = -ring; dr <= ring; dr++)
                {
                    if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != ring) continue;
                    var (c, r) = (column + dc, row + dr);
                    var distance = dc * dc + dr * dr;
                    if (!Inside(c, r) || cells[c, r] != Guid.Empty || distance >= bestDistance) continue;
                    best = (c, r);
                    bestDistance = distance;
                }
            if (best != null) return best;
        }
        return null;
    }

    private void PutAnywhere(Guid unit)
    {
        for (var row = 0; row < Size; row++)
            for (var column = 0; column < Size; column++)
                if (cells[column, row] == Guid.Empty)
                {
                    cells[column, row] = unit;
                    return;
                }
    }

    private static bool Inside(int column, int row) => column >= 0 && column < Size && row >= 0 && row < Size;
}
