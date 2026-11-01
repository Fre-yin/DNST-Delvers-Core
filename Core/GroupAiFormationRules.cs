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
    // slow leader (one cell a second) never covered two cells in the last second, so the formation never turned (Toolkit's
    // corridor pull: the tank walked four cells and the back row kept standing in front of it).
    internal const int StillLooks = 3;

    internal static bool IsWalk(int dx, int dy) => Math.Max(Math.Abs(dx), Math.Abs(dy)) >= WalkCells;

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
    // while the walk goes on, so the turn's second look comes with the next cell (Toolkit's corridor pull with the AI on: a
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
        // A turn back starts a new walk; so does a right angle after a walk of WalkCells (Toolkit's corridor pull: three cells
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

    // CORE_AI_FORMATION_IDLE: the game's idle walk of a member kept on its cell.
    internal static string IdleLine(string unit, int x, int y)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION_IDLE unit={0} cell={1},{2}", unit, x, y);

    // Orders to a place that ended without a single step from the same cell before a member steps aside first (with the AI
    // on: an archer that stopped on a cell another member was just leaving got three orders, none took a step,
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

    // A member keeps its place when it is free (alive, gate open, no player's order) and its formation is switched on
    // (TacticalFormation.IsOn, the button switches it per member). Switched off it neither follows, holds nor
    // returns; the others still follow a switched off leader. A member lying at near-death or unconscious cannot walk: it
    // gets no order until it stands again (Toolkit's barricade pull: a downed archer got a return order every look).
    internal static bool Keeps(bool free, bool on, bool lying) => free && on && !lying;

    // Only the cell of a member that will move counts as free for the places; the leader's and those of members that stay
    // (switched off among them) stay taken.
    internal static bool FreesCell(bool leader, bool keeps) => !leader && keeps;

    // Whether a member gets a move order to its place: it is not there, and it was not already sent to that very cell, or that
    // order is over and ReorderSeconds have passed since.
    internal static bool NeedsOrder((int X, int Y) at, (int X, int Y) place, (int X, int Y)? ordered, bool orderRuns, float sinceOrder)
        => at != place && (ordered != place || (!orderRuns && sinceOrder >= ReorderSeconds));

    // Out of combat a member follows; at the fight's start it takes its place at once; in the fight it goes back only after
    // ReturnQuietSeconds without an attack on it. Null: it stays where it is.
    internal static Reason? Why(bool fight, bool fightStarts, float now, float lastHit)
    {
        if (!fight) return Reason.Follow;
        if (fightStarts) return Reason.Start;
        return now - lastHit >= ReturnQuietSeconds ? Reason.Return : null;
    }

    // In a fight a member in formation strikes only what is in its skill's reach from where it stands.
    internal static bool InReach(int fromX, int fromY, int toX, int toY, int range)
        => Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY)) <= Math.Max(1, range);

    internal static string Describe(Reason reason) => reason switch
    {
        Reason.Start => "start",
        Reason.Return => "return",
        _ => "follow"
    };

    // CORE_AI_FORMATION: one move order to a place.
    internal static string Line(string unit, Reason reason, int x, int y, string facing)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_FORMATION unit={0} reason={1} cell={2},{3} facing={4}", unit, Describe(reason), x, y, facing);
}
