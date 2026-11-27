using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The tactical formation in the dungeon, the group AI's part: out of combat the members follow their places
// around the leader (TacticalFormationRules); when a fight starts they take their places at once and fight from there, striking only
// what comes into reach; a player's order wins, and a member without one goes back to its place once nothing has attacked it for
// ReturnQuietSeconds ("wenn kein Input, wieder repositionieren, solange nicht angegriffen/gehittet werden, da soll der Spieler
// managen"). Pure, so the offline tests cover it; GroupAiFormation reads the game.
internal static class GroupAiFormationRules
{
    // How often the formation is looked at, in game seconds.
    internal const float LookSeconds = 0.5f;
    // In a fight, this long without an attack on it (hit or not) before a member walks back to its place.
    internal const float ReturnQuietSeconds = 2.5f;
    // A member sent to a place it never reached (pushed off by a dodge, a player's order or a blocked way) is sent again after
    // this long, once no move order of Core's runs for it.
    internal const float ReorderSeconds = 2f;

    internal enum Reason { Follow, Start, Return }

    // The leader's walk turns the formation only from two cells on; one cell is a correcting step.
    internal const int WalkCells = 2;
    // The walk is read from the cell where the leader last stood this many looks in a row (1.5 s), not over a fixed window: a
    // slow leader (one cell a second) never covered two cells in the last second, so the formation never turned (in a
    // corridor the tank walked four cells and the back row kept standing in front of it).
    internal const int StillLooks = 3;

    // The leader's cells kept for the stop's direction: a sliding window, the newest count and the oldest goes when it is full
    // (Core the stop must always be in it, and a long trail cost too many calls at the stop); also the path's last cells.
    internal const int TrailCap = 64;
    // Looks after a group's order in which the leader's path is read before it is given up (CORE_AI_FORMATION_PATH none).
    internal const int PathLooksMax = 4;
    // A plan is taken by its orders within this time; an older one is stale and dropped.
    internal const float PlanSeconds = 1f;
    // A leader that never moved after a group's order drops its goal after this long (a leader that had stood long
    // dropped its goal at once).
    internal const float GoalWaitSeconds = 2f;

    // Whether the leader's goal of a group's order is done: it stands on it, it moved and has stood StillLooks looks since, or it
    // never left the cell where it got the goal for more than GoalWaitSeconds.
    internal static bool GoalDone((int X, int Y) at, (int X, int Y) goal, (int X, int Y) goalFrom, int stillLooks, float sinceSet)
        => at == goal || (at != goalFrom ? stillLooks >= StillLooks : sinceSet > GoalWaitSeconds);

    internal static bool IsWalk(int dx, int dy) => Math.Max(Math.Abs(dx), Math.Abs(dy)) >= WalkCells;

    // A member whose slot is taken keeps the place aside it was sent to while the anchor and its direction stay and that place stays
    // open; null where the slot is free again, the anchor or direction changed or the place is no longer open (then the new place
    // goes). Each look placed the fallback anew from the cells of members on their way, so it jumped
    // between two cells.
    internal static (int X, int Y)? HoldAside((int X, int Y) place, (int X, int Y) slot, (int X, int Y) anchor, TacticalFormationRules.Facing facing,
        ((int X, int Y) Anchor, TacticalFormationRules.Facing Facing, (int X, int Y) Place) sent, Func<int, int, bool> open)
        => place == slot || sent.Anchor != anchor || sent.Facing != facing ? null
            : place == sent.Place || open(sent.Place.X, sent.Place.Y) ? sent.Place : null;

    // Where the leader's walk began and for how many looks it has stood since.
    internal readonly struct Walk
    {
        internal Walk((int X, int Y)? from, int still)
        {
            From = from;
            Still = still;
        }

        internal (int X, int Y)? From { get; }
        internal int Still { get; }
    }

    // One look at the leader: the walk to read (from the walk's start to the leader's cell), or null when it stood or has not
    // yet gone WalkCells. The walk starts anew where the leader stood StillLooks looks, where it turned back or bent at a right
    // angle (its last step against or across the walk so far) and where the formation turned (Restart). The start stays
    // while the walk goes on, so the turn's second look comes with the next cell (in a corridor with the AI on: a
    // short turn back, then three cells, and no turn because each read restarted the walk).
    internal static (Walk Next, (int Dx, int Dy)? Moved) Track(Walk walk, (int X, int Y) at, (int X, int Y)? last)
    {
        if (last == null) return (new Walk(at, 0), null);
        if (at == last.Value)
        {
            var still = walk.Still + 1;
            return (new Walk(still >= StillLooks ? at : walk.From ?? at, still), null);
        }
        var from = walk.From ?? last.Value;
        var before = (last.Value.X - from.X, last.Value.Y - from.Y);
        var step = (at.X - last.Value.X, at.Y - last.Value.Y);
        // A turn back starts a new walk; so does a right angle after a walk of WalkCells (in a corridor: three cells
        // west, then four towards the exit, read as one diagonal). A path wiggling by single cells keeps its start.
        var dot = before.Item1 * step.Item1 + before.Item2 * step.Item2;
        if (dot < 0 || (dot == 0 && IsWalk(before.Item1, before.Item2))) from = last.Value;
        var moved = (at.X - from.X, at.Y - from.Y);
        return (new Walk(from, 0), IsWalk(moved.Item1, moved.Item2) ? moved : null);
    }

    // After the formation turned: the next walk starts where the leader stands, so a bend counts anew.
    internal static Walk Restart((int X, int Y) at) => new(at, 0);

    // An idle member does not wander while it belongs to the formation that applies and has it switched on.
    internal static bool StaysStill(bool member, bool on) => member && on;

    // A member with auto attack off follows the formation too; only "wait for orders" keeps it where it is.
    // Whether it strikes on its own stays the game's auto attack; the group AI's own gate (GroupAiGate.OpenFor) is not asked here.
    internal static bool OpenToFollow(bool waitsForOrder) => !waitsForOrder;

    // Only the follow was freed. Core's own actions for a kept member, roaming to the foes by its role and
    // summoning again out of a fight, keep the group AI's gate (GroupAiGate.OpenFor: auto attack on, not waiting for orders).
    internal static bool CoreActs(bool kept, bool gateOpen) => kept && gateOpen;

    // What a look does with the party's summons (GroupAiSummonWalk): out of a fight they take their places in the formation with the
    // group AI or without it; in a fight only the full group AI steers them (the support's guard), else they
    // are the game's.
    internal enum SummonDo { Game, Place, Guard }

    internal static SummonDo SummonStep(bool fight, bool groupAi)
        => !fight ? SummonDo.Place : groupAi ? SummonDo.Guard : SummonDo.Game;

    // CORE_AI_FORMATION_SKIP: a member of the formation not sent to its place this look, and why: dead, wait (wait for orders), hand
    // (placed by hand or the player's attack order), busy (a task the player ordered runs), quiet (after a hand), walk (the player's own
    // walk runs), stance (a player's order of the last seconds), off (its formation switched off), lying, seed (staying in a seed), role
    // (out by its role), noplace.
    // Written once when the reason changes.
    internal static string SkipLine(string unit, string why)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION_SKIP unit={0} why={1}", unit, why);

    internal static bool LogsSkip(string before, string now) => now != null && now != before;

    // CORE_AI_FORMATION_IDLE: the game's idle walk of a member kept on its cell.
    internal static string IdleLine(string unit, int x, int y)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION_IDLE unit={0} cell={1},{2}", unit, x, y);

    // Orders to a place that ended without a single step from the same cell before a member steps aside first
    // (with the AI on: an archer that stopped on a cell another member was just leaving got three orders, none took a step,
    // until the game's idle walk moved it away).
    internal const int AsideAfter = 2;

    // How many orders in a row ended unmoved from this cell: one more when the last one did, else none.
    internal static int Unmoved(int before, bool lastUnmoved, (int X, int Y) at, (int X, int Y)? orderedFrom)
        => lastUnmoved && orderedFrom == at ? before + 1 : 0;

    // The free neighbour cell to step aside to, the one nearest the place (ties in a fixed order), never the place itself; null
    // for a member already next to its place, which counts as there (the step aside went to the place every time and
    // failed again from the diagonal cell beside it).
    internal static (int X, int Y)? Aside((int X, int Y) at, (int X, int Y) place, Func<int, int, bool> free)
    {
        if (Math.Max(Math.Abs(place.X - at.X), Math.Abs(place.Y - at.Y)) <= 1) return null;
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = (at.X + dx, at.Y + dy);
                if (cell == place || !free(cell.Item1, cell.Item2)) continue;
                var ox = Math.Abs(place.X - cell.Item1);
                var oy = Math.Abs(place.Y - cell.Item2);
                var distance = Math.Max(ox, oy) * 10 + ox + oy;
                if (distance >= bestDistance) continue;
                best = cell;
                bestDistance = distance;
            }
        return best;
    }

    // CORE_AI_FORMATION_ASIDE: a member steps aside before its place is ordered again.
    internal static string AsideLine(string unit, (int X, int Y) from, (int X, int Y) to, (int X, int Y) place, int unmoved)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION_ASIDE unit={0} from={1},{2} to={3},{4} place={5},{6} unmoved={7}",
            unit, from.X, from.Y, to.X, to.Y, place.X, place.Y, unmoved);

    // A member keeps its place when it is free (alive, not waiting for orders, no player's order) and its formation is switched on
    // (TacticalFormation.IsOn, the button switches it per member). Switched off it neither follows, holds nor
    // returns; the others still follow a switched off leader. A member lying at near-death or unconscious cannot walk: it
    // gets no order until it stands again (at a barricade a downed archer got a return order every look).
    internal static bool Keeps(bool free, bool on, bool lying) => free && on && !lying;

    // The player holds a member: placed by hand, or its attack order stands (GroupAiChase; the player orders
    // the advance). Such a member is not free, so the formation neither holds nor fetches it back while the order stands.
    internal static bool HeldByPlayer(bool byHand, bool ordered) => byHand || ordered;

    // A member carrying out a task the player ordered that is no plain walk (feeding, giving or using an item, a
    // bandage, carrying, a rescue, any cast) is not fetched back to its place until that task ends, in a fight and out of it; then it
    // follows as before. The task counts from the order's moment for PlanSeconds, before the game's brain has taken it, and after
    // that while the brain still runs it (CORE_AI_FORMATION_SKIP why=busy).
    internal static bool BusyWithOrder(bool ordered, double sinceOrder, bool stillRunning)
        => ordered && (sinceOrder <= PlanSeconds || stillRunning);

    // Which of the player's orders make a member busy: none that is a plain walk (its own hold, memberOrders) and none that attacks
    // a foe (the attack order holds through GroupAiChase.HasOrder until the target falls, a new order comes or the fight ends).
    internal static bool BusyOrder(bool walk, bool attackOnFoe) => !walk && !attackOnFoe;

    // The ordered task still runs only while the brain runs that very task: the same object, and of the same kind, as an address
    // can be used again. A later task of the same kind (the fight's next attack, the game's own feeding) is another task.
    internal static bool SameTask(bool samePointer, bool sameType) => samePointer && sameType;

    // A member placed by a click on a foe while no fight runs is let go once that foe is gone or dead, at the latest after this many
    // seconds without a fight; never while the player's attack order stands (it ends with the target, a new order or the fight).
    internal const float ClickNoFightSeconds = 5f;

    // Why a hand is let go out of a fight: "foe-gone", "no-fight", or null to keep it.
    internal static string IdleHandRelease(bool ordered, bool foeGone, float secondsWithoutFight)
        => ordered ? null : foeGone ? "foe-gone" : secondsWithoutFight >= ClickNoFightSeconds ? "no-fight" : null;

    // Only the cell of a member that will move counts as free for the places; those of members that stay (switched off among them)
    // stay taken, and the leader's while it does not walk to a group order's goal.
    internal static bool FreesCell(bool leader, bool keeps, bool leaderWalksToGoal) => leader ? leaderWalksToGoal : keeps;

    // Whether a member gets a move order to its place: it is not there, and it was not already sent to that very cell, or that
    // order is over and ReorderSeconds have passed since.
    internal static bool NeedsOrder((int X, int Y) at, (int X, int Y) place, (int X, int Y)? ordered, bool orderRuns, float sinceOrder)
        => at != place && (ordered != place || (!orderRuns && sinceOrder >= ReorderSeconds));

    // Whether a summon Core holds (SummonControl.Hold) gets a move order: at once for a new cell, and for the cell it is held on like
    // a member (NeedsOrder) once it stands off it, Core's move no longer runs for it and ReorderSeconds have passed (a bubbleling
    // stopped three cells short of its place after the leader stood, and the unchanged cell sent it nothing more).
    internal static bool SummonSends(bool heldOnCell, (int X, int Y) at, (int X, int Y) cell, bool orderRuns, float sinceOrder)
        => !heldOnCell || NeedsOrder(at, cell, cell, orderRuns, sinceOrder);

    // Out of combat a member follows; at the fight's start it takes its place at once; in the fight it goes back only after
    // ReturnQuietSeconds without an attack on it. Null: it stays where it is.
    internal static Reason? Why(bool fight, bool fightStarts, float now, float lastHit)
    {
        if (!fight) return Reason.Follow;
        if (fightStarts) return Reason.Start;
        return now - lastHit >= ReturnQuietSeconds ? Reason.Return : null;
    }

    // The places follow the leader out of a fight; in a fight only while a player's order moves it (in a fight
    // the formation does not move along), so the leader's own steps leave them where they are.
    internal static bool PlacesFollow(bool fight, bool playerLeads) => !fight || playerLeads;

    // In a fight a member in formation strikes only what is in its skill's reach from where it stands (the game's reach,
    // GroupAiChaseRules.InReach, on the cell distance GroupAiRules.Distance).
    internal static bool InReach(int fromX, int fromY, int toX, int toY, int range)
        => GroupAiChaseRules.InReach(GroupAiRules.Distance(fromX, fromY, toX, toY), range);

    internal static string Describe(Reason reason) => reason switch
    {
        Reason.Start => "start",
        Reason.Return => "return",
        _ => "follow"
    };

    // The places of the formation with anchors (while the leader stays and moves outside the tac grid around an anchor, the
    // members sent there do not move; the player's order goes first): the anchored members first, one placing per anchor around its
    // cell and turned its way, then the members around the leader. Every cell given before counts as a figure for the later ones
    // (TacticalFormationRules.Place: the fallback looks next to the slot, not on the mirrored side), so the leader's followers make way
    // for an anchored member and never the other way round; two members never get one cell. Without anchors the plain placing.
    internal static (int X, int Y)?[] PlaceWithAnchors((int X, int Y) leader, TacticalFormationRules.Facing facing,
        IReadOnlyList<TacticalFormationRules.Slot> slots,
        IReadOnlyDictionary<Guid, ((int X, int Y) Cell, TacticalFormationRules.Facing Facing)> anchors,
        Func<int, int, bool> free, Func<int, int, bool> figures)
    {
        if (anchors == null || anchors.Count == 0) return TacticalFormationRules.Place(leader.X, leader.Y, facing, slots, free, figures);
        var places = new (int X, int Y)?[slots.Count];
        var taken = new HashSet<(int X, int Y)>();
        bool Figure(int x, int y) => taken.Contains((x, y)) || (figures != null && figures(x, y));
        var groups = new List<(((int X, int Y) Cell, TacticalFormationRules.Facing Facing) Anchor, List<int> Indices)>();
        var around = new List<int>();
        for (var i = 0; i < slots.Count; i++)
        {
            if (!anchors.TryGetValue(slots[i].Unit, out var anchor))
            {
                around.Add(i);
                continue;
            }
            var group = groups.FindIndex(g => g.Anchor == anchor);
            if (group < 0) groups.Add((anchor, new List<int> { i }));
            else groups[group].Indices.Add(i);
        }
        foreach (var (anchor, indices) in groups)
            PlaceGroup(anchor.Cell, anchor.Facing, slots, indices, free, Figure, places, taken);
        PlaceGroup(leader, facing, slots, around, free, Figure, places, taken);
        return places;
    }

    private static void PlaceGroup((int X, int Y) at, TacticalFormationRules.Facing facing, IReadOnlyList<TacticalFormationRules.Slot> slots,
        List<int> indices, Func<int, int, bool> free, Func<int, int, bool> figure, (int X, int Y)?[] places, HashSet<(int X, int Y)> taken)
    {
        if (indices.Count == 0) return;
        var own = indices.ConvertAll(i => slots[i]);
        var cells = TacticalFormationRules.Place(at.X, at.Y, facing, own, free, figure);
        for (var j = 0; j < indices.Count; j++)
        {
            places[indices[j]] = cells[j];
            if (cells[j] is { } cell) taken.Add(cell);
        }
    }

    // CORE_AI_FORMATION: one move order to a place.
    internal static string Line(string unit, Reason reason, int x, int y, string facing)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION unit={0} reason={1} cell={2},{3} facing={4}", unit, Describe(reason), x, y, facing);
}
