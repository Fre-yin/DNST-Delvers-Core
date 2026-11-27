namespace DungeonSettlersDelvers.Core;

// The order the formation window's list shows its rows in: each member, and right below it the summon places of
// that member, indented under it. A place whose summoner is no member of the set stands at the end; the window no longer shows such
// places (it takes and offers none), the rule keeps the case as a guard. Only the showing
// changes: the grid keeps its members and summon places in their own order (TacticalFormationGrid), and a drop on a row means a
// member's place in that order (DropIndex).
internal static class TacticalFormationListOrder
{
    internal readonly record struct Row(Guid Unit, bool Member, int MemberIndex, bool UnderSummoner, bool LastUnder);

    // members in their order; summons in theirs, each with its summoner (Guid.Empty when unknown).
    internal static List<Row> Rows(IReadOnlyList<Guid> members, IReadOnlyList<Guid> summons, Func<Guid, Guid> summonerOf)
    {
        var result = new List<Row>();
        var placed = new HashSet<Guid>();
        for (var m = 0; m < members.Count; m++)
        {
            result.Add(new Row(members[m], true, m, false, false));
            var under = new List<Guid>();
            foreach (var summon in summons)
                if (!placed.Contains(summon) && summonerOf(summon) == members[m]) under.Add(summon);
            for (var k = 0; k < under.Count; k++)
            {
                placed.Add(under[k]);
                result.Add(new Row(under[k], false, m, true, k == under.Count - 1));
            }
        }
        foreach (var summon in summons)
            if (placed.Add(summon)) result.Add(new Row(summon, false, -1, false, false));
        return result;
    }

    // The member index a drop of the member at from on the list's row means, for TacticalFormationGrid.MoveInList (which takes the
    // member out first): a member's row its own index; a summon place of member k right behind k, that is k when the dragged member
    // stands at k or above it and k + 1 when it comes from below, so the summoner on its own place stays and the crown changes only
    // on the top row; else (a place without its summoner, the add row) the last member, as the list did when
    // the summon places all stood below the members.
    internal static int DropIndex(IReadOnlyList<Row> rows, int row, int memberCount, int from)
    {
        if (memberCount <= 0) return -1;
        if (row < 0 || row >= rows.Count || rows[row].MemberIndex < 0) return memberCount - 1;
        var k = rows[row].MemberIndex;
        var index = rows[row].Member ? k : from <= k ? k : k + 1;
        return Math.Min(index, memberCount - 1);
    }

    // What the window shows while a member is dragged over a row (points 14 and 15): the target is framed only
    // when the drop changes the order, and "Group leader" turns gold only when it passes the crown to the dragged member; the leader
    // dragged away passes it too, without the gold.
    internal readonly record struct Hint(int To, bool Changes, bool Crown);

    internal static Hint DropHint(IReadOnlyList<Row> rows, int row, int memberCount, int from)
    {
        if (from < 0 || row < 0 || memberCount <= 0) return new Hint(-1, false, false);
        var to = DropIndex(rows, row, memberCount, from);
        var changes = to >= 0 && to != from;
        return new Hint(to, changes, changes && to == 0);
    }
}