namespace DungeonSettlersDelvers.Core;

// The cells of a group's move order in formation (TacticalFormationMove). Pure, so the offline tests cover it.
//   - The leader, when selected, goes to the clicked cell, its formation on or off, and the members with
//     their formation on stand around it.
//   - Every selected member with its formation on and a place in the set goes to its place around the clicked cell, turned the
//     way to the click, with the same fallback as the formation (TacticalFormationRules.Place). With the leader not selected the
//     members stand as if the leader stood at the click.
//   - free is the group AI's cell check with the order's units' own cells counted as free (GroupAiFormation.Walkable).
//   - Selected units with the formation off, summons and units outside the set keep the game's cell; one whose cell falls on a
//     cell of the formation takes the nearest free cell beside it (AsideFor, when the game's cells are known).
//   - Without the leader the way is read from the middle of the selected members to the click (Core's plan, always toward the click).
// INTERPRETATION (not the author's words): a part selected with the leader (case b) arranges only the selected, the others follow the
// formation as before.
internal static class TacticalFormationMoveRules
{
    private const int AsideRadius = 3;
    // The tac window's grid reaches this far around its middle (9 x 9): a click without the leader farther off than this sets an
    // anchor, and the anchor holds while the leader stays farther off.
    internal const int GridReach = 4;

    // A member placed by hand in a fight (the formation breaks only where the player moves one member away
    // alone in a fight) is let go when the fight ends; in a fight only the player's own move lets it go.
    internal static bool HandEnds(bool fightBefore, bool fightNow) => fightBefore && !fightNow;

    // How far the clicked cell may lie from the cell the game gave the leader and still be the anchor (Chebyshev).
    internal const int ClickReach = 1;

    // The anchor of a group's order the game spread (TacticalFormationMove.Rewrite): with the leader among the units the clicked cell
    // the game resolved when the leader can stand there (walkable, no figure on it) and the game gave the leader a cell at most
    // ClickReach from it, else the cell the game gave the leader; without the leader the resolved click. The game's spread put the
    // leader one cell beside a free click, though the leader goes to the clicked cell. A click the leader cannot reach (behind a door,
    // across a gap) the game resolves to a reachable cell further off, which stays the anchor; walkable is no way there, and the game's
    // path search is not asked. Known gap: a free cell right behind a wall one cell thick, next to the cell the game gave the leader,
    // still counts as the click.
    internal static (int X, int Y) Anchor((int X, int Y)? leaderCell, (int X, int Y) resolved, Func<int, int, bool> free, Func<int, int, bool> figure)
        => leaderCell is not { } cell
           || (free(resolved.X, resolved.Y) && (figure == null || !figure(resolved.X, resolved.Y))
               && GroupAiRules.Distance(cell.X, cell.Y, resolved.X, resolved.Y) <= ClickReach)
            ? resolved : cell;

    // Whether a cell lies outside the grid around a middle (Max(|dx|, |dy|) > GridReach).
    internal static bool OutsideGrid((int X, int Y) middle, (int X, int Y) cell)
        => Math.Max(Math.Abs(cell.X - middle.X), Math.Abs(cell.Y - middle.Y)) > GridReach;

    internal readonly struct Unit
    {
        internal Unit(Guid id, (int X, int Y) at, bool on)
        {
            Id = id;
            At = at;
            On = on;
        }

        internal Guid Id { get; }
        internal (int X, int Y) At { get; }
        internal bool On { get; }
    }

    internal sealed class Result
    {
        internal Dictionary<Guid, (int X, int Y)> Cells { get; } = new();
        // The selected members with the formation on and a place (not the leader).
        internal HashSet<Guid> Placed { get; } = new();
        internal List<string> Lines { get; } = new();
        internal TacticalFormationRules.Facing Facing { get; init; }
        internal (int X, int Y) Click { get; init; }
        internal bool WithLeader { get; init; }
        internal Func<int, int, bool> Free { get; init; }
    }

    // null when the order has nothing to arrange: no selected member with the formation on, or a click on the walkers' own spot.
    internal static Result Plan(Guid leader, (int X, int Y) leaderAt, (int X, int Y) click, IReadOnlyList<TacticalFormationRules.Slot> slots,
        IReadOnlyList<Unit> units, Func<int, int, bool> free, Func<int, int, bool> figure = null)
    {
        if (slots == null || units == null || free == null) return null;
        var placed = new HashSet<Guid>();
        foreach (var slot in slots) placed.Add(slot.Unit);
        var leaderSelected = false;
        var members = new List<Unit>();
        foreach (var unit in units)
        {
            if (unit.Id == leader) leaderSelected = true;
            else if (unit.On && placed.Contains(unit.Id)) members.Add(unit);
        }
        if (members.Count == 0) return null;
        var start = leaderSelected ? leaderAt : Middle(members);
        var facing = TacticalFormationRules.FacingOf(click.X - start.X, click.Y - start.Y);
        if (facing == null) return null;
        var memberSlots = new List<TacticalFormationRules.Slot>();
        foreach (var slot in slots)
            if (members.Exists(member => member.Id == slot.Unit)) memberSlots.Add(slot);
        var cells = TacticalFormationRules.Place(click.X, click.Y, facing.Value, memberSlots, free, figure);
        var result = new Result { Facing = facing.Value, Click = click, WithLeader = leaderSelected, Free = free };
        if (leaderSelected)
        {
            result.Cells[leader] = click;
            result.Lines.Add(Line(leader, click, "leader", facing.Value, click));
        }
        for (var i = 0; i < memberSlots.Count; i++)
        {
            if (cells[i] is not { } cell) continue;
            result.Cells[memberSlots[i].Unit] = cell;
            result.Placed.Add(memberSlots[i].Unit);
            result.Lines.Add(Line(memberSlots[i].Unit, cell, "place", facing.Value, click));
        }
        return result;
    }

    // A unit the plan leaves alone keeps the game's cell, unless that cell is the click or a planned cell: then the nearest free
    // cell beside it that nothing of the plan or of the units done before takes. null keeps the game's cell.
    internal static (int X, int Y)? AsideFor(Result plan, Guid unit, (int X, int Y) gameCell, HashSet<(int, int)> takenBefore)
    {
        if (plan == null || plan.Cells.ContainsKey(unit)) return null;
        var taken = new HashSet<(int, int)>(takenBefore ?? new HashSet<(int, int)>()) { plan.Click };
        foreach (var cell in plan.Cells.Values) taken.Add(cell);
        if (!taken.Contains(gameCell)) return null;
        var aside = Aside(gameCell, taken, plan.Free);
        if (aside != null) plan.Lines.Add(Line(unit, aside.Value, "aside", plan.Facing, plan.Click));
        return aside;
    }

    private static (int X, int Y) Middle(List<Unit> units)
    {
        int x = 0, y = 0;
        foreach (var unit in units)
        {
            x += unit.At.X;
            y += unit.At.Y;
        }
        return ((int)Math.Round(x / (double)units.Count), (int)Math.Round(y / (double)units.Count));
    }

    // The nearest open cell beside the game's cell that no other unit of the order takes; null for none.
    private static (int X, int Y)? Aside((int X, int Y) centre, HashSet<(int, int)> taken, Func<int, int, bool> open)
    {
        for (var r = 1; r <= AsideRadius; r++)
            for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var cell = (centre.X + dx, centre.Y + dy);
                    if (!taken.Contains(cell) && open(cell.Item1, cell.Item2)) return cell;
                }
        return null;
    }

    internal static string Line(Guid unit, (int X, int Y) to, string kind, TacticalFormationRules.Facing facing, (int X, int Y) click)
        => $"CORE_TACTICAL_MOVE unit={unit:D} to={to.X},{to.Y} kind={kind} facing={facing.ToString().ToLowerInvariant()} click={click.X},{click.Y}";
}
