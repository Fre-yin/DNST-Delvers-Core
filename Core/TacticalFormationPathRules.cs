namespace DungeonSettlersDelvers.Core;

// Which look a figure's path line gets: with the formation on, the leader's line is drawn stronger and the
// lines of the members following it faint and dashed, so the one way the group takes stands out. Everything else keeps the game's
// own line. Only the look; the paths themselves stay the game's (TacticalFormationPathLook). Pure, so the offline tests cover it.
internal static class TacticalFormationPathRules
{
    internal enum Line { Game, Leader, Follower }

    // The leader's line wider and fully opaque; a follower's faint.
    internal const float LeaderWidth = 1.6f, FollowerAlpha = 0.35f;
    // A dash and a gap per world unit along a follower's line.
    internal const int DashPixels = 4;

    // The members that follow the active set's middle: those with a place of the set there now (TacticalFormationBook.Apply) and
    // their formation switched on. The middle itself is no follower; it stays the middle even when switched off.
    internal static HashSet<Guid> Followers(Guid leader, IEnumerable<Guid> placed, Func<Guid, bool> on)
    {
        var result = new HashSet<Guid>();
        if (leader == Guid.Empty || placed == null) return result;
        foreach (var unit in placed)
            if (unit != leader && unit != Guid.Empty && on(unit)) result.Add(unit);
        return result;
    }

    // The active set read for the members there now, only read (TacticalFormationBook.Apply without summons, so no summon number is
    // written): its middle and Followers of the placed members. Guid.Empty and none without a set or member.
    internal static (Guid Leader, HashSet<Guid> Followers) Read(TacticalFormationBook.Set set, IReadOnlyCollection<Guid> present, Func<Guid, bool> on)
    {
        if (!TacticalFormationBook.Apply(set, present, out var leader, out var slots)) return (Guid.Empty, new HashSet<Guid>());
        return (leader, Followers(leader, slots.Select(slot => slot.Unit), on));
    }
    // enabled: the comfort option; inDungeon: where the formation applies; leader: the active set's middle there now, Guid.Empty
    // without one; followers: Followers. The middle's line is the strong one while anybody follows it.
    internal static Line LineOf(bool enabled, bool inDungeon, Guid unit, Guid leader, IReadOnlyCollection<Guid> followers)
    {
        if (!enabled || !inDungeon || unit == Guid.Empty || leader == Guid.Empty || followers == null || followers.Count == 0) return Line.Game;
        if (unit == leader) return Line.Leader;
        return followers.Contains(unit) ? Line.Follower : Line.Game;
    }
}
