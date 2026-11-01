namespace DungeonSettlersDelvers.Core;

// The escape cell of Core's AI, shared by the bandits' herbalist and later
// Frieren's pendulum. No game types, so it can be tested on its own. From the start cell every cell reachable in
// up to maxStep steps (eight directions, walkable cells only) is weighed:
// - distance to the nearest foe first;
// - ways out: walkable cells one and two steps further that keep at least that distance; a dead end without any
//   only as a last choice;
// - the leash as a weight, not a limit: every cell beyond reach from the nearest friend costs;
// - a path that passes within one cell of a second foe (not the nearest one at the start) is left out.
// Surrounded (every path passes a second foe) the unsafe way out wins that passes only weak or stunned foes, never
// standing still. On a true tie a seed per fight chooses, so the escape stays unexpected but tests repeat.
internal static class EscapeRules
{
    internal readonly struct Foe
    {
        internal int X { get; }
        internal int Y { get; }
        internal bool Weak { get; }

        internal Foe(int x, int y, bool weak)
        {
            X = x;
            Y = y;
            Weak = weak;
        }
    }

    internal sealed class Pick
    {
        internal int X;
        internal int Y;
        internal string Mode = "escape";
        internal int Away;
        internal int Exits;
        internal int Leash;
        internal int Steps;
        internal int HealTargets;
    }

    internal const int AwayWeight = 1000;
    internal const int ExitWeight = 60;
    internal const int MaxCountedExits = 6;
    internal const int DeadEndPenalty = 2000;
    internal const int LeashWeight = 40;

    internal static int Chebyshev(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    // Null only when no cell around is walkable at all.
    // healTargets: comrades below 90 %; every one within reach of a cell adds HealTargetWeight, and the leash then
    // runs to the lowest of them (the first) instead of the nearest friend (leash=0 while she fled into a
    // corner, because a healthy comrade nearby set the leash to 0).
    internal const int HealTargetWeight = 150;

    internal static Pick Choose(int fromX, int fromY, IReadOnlyList<Foe> foes, IReadOnlyList<(int X, int Y)> friends, int reach,
        int maxStep, Func<int, int, bool> walkable, int seed, IReadOnlyList<(int X, int Y)> healTargets = null)
    {
        if (healTargets != null && healTargets.Count > 0) friends = new[] { healTargets[0] };
        if (walkable == null || maxStep <= 0) return null;
        foes ??= Array.Empty<Foe>();
        friends ??= Array.Empty<(int X, int Y)>();
        var primary = -1;
        var primaryDistance = int.MaxValue;
        for (var i = 0; i < foes.Count; i++)
        {
            var d = Chebyshev(fromX, fromY, foes[i].X, foes[i].Y);
            if (d < primaryDistance)
            {
                primaryDistance = d;
                primary = i;
            }
        }
        // Breadth first over walkable cells, with the cell each one was reached from.
        var start = (fromX, fromY);
        var depth = new Dictionary<(int, int), int> { [start] = 0 };
        var parent = new Dictionary<(int, int), (int, int)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var d = depth[cell];
            if (d >= maxStep) continue;
            foreach (var (nx, ny) in Around(cell.X, cell.Y))
            {
                if (depth.ContainsKey((nx, ny)) || !walkable(nx, ny)) continue;
                depth[(nx, ny)] = d + 1;
                parent[(nx, ny)] = cell;
                queue.Enqueue((nx, ny));
            }
        }
        Pick bestSafe = null, bestUnsafe = null;
        long safeScore = long.MinValue, unsafeScore = long.MinValue;
        var safeTies = new List<Pick>();
        var unsafeTies = new List<Pick>();
        foreach (var pair in depth)
        {
            if (pair.Key == start) continue;
            var (x, y) = pair.Key;
            var away = Nearest(foes, x, y);
            var exits = Exits(x, y, away, foes, walkable);
            var leash = Math.Max(0, NearestFriend(friends, x, y) - reach);
            var inReach = 0;
            if (healTargets != null)
                foreach (var (hx, hy) in healTargets)
                    if (Chebyshev(x, y, hx, hy) <= reach) inReach++;
            long score = (long)Math.Min(away, 50) * AwayWeight + Math.Min(exits, MaxCountedExits) * ExitWeight
                - (exits == 0 ? DeadEndPenalty : 0) - (long)leash * LeashWeight + (long)inReach * HealTargetWeight - pair.Value;
            var passes = PassesSecond(pair.Key, start, parent, foes, primary, out var onlyWeak);
            var pick = new Pick { X = x, Y = y, Away = away, Exits = exits, Leash = leash, Steps = pair.Value, HealTargets = inReach };
            if (!passes)
                Keep(pick, score, ref bestSafe, ref safeScore, safeTies);
            else if (onlyWeak)
                Keep(pick, score, ref bestUnsafe, ref unsafeScore, unsafeTies);
            else
                Keep(pick, score - 5 * AwayWeight, ref bestUnsafe, ref unsafeScore, unsafeTies);
        }
        if (bestSafe != null) return Tie(safeTies, seed, "escape");
        if (bestUnsafe != null) return Tie(unsafeTies, seed, "surrounded");
        return null;
    }

    // A cell within reach of a target (the heal path and moving up): reachable in up to maxSteps over walkable
    // cells, its path passing at most passFoes foes within one cell, the cell itself at least minFoeDistance from every
    // foe. The fewest steps win, then the larger distance to the foes, then the fight's seed. Null when there is none.
    // closer: with no such cell within maxSteps, the reachable cell nearest the target instead (same path and foe limits),
    // when it is nearer than the start; the heal path and moving up then cover a far fight in several moves (the band
    // fought 20 to 30 cells from the herbalist, beyond 8 steps plus her reach, and she held).
    internal static Pick Approach(int fromX, int fromY, int targetX, int targetY, int reach, IReadOnlyList<Foe> foes, int maxSteps,
        int passFoes, int minFoeDistance, Func<int, int, bool> walkable, int seed, bool closer = false)
    {
        if (walkable == null || maxSteps <= 0) return null;
        foes ??= Array.Empty<Foe>();
        var start = (fromX, fromY);
        var depth = new Dictionary<(int, int), int> { [start] = 0 };
        var passed = new Dictionary<(int, int), int> { [start] = 0 };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var d = depth[cell];
            if (d >= maxSteps) continue;
            foreach (var (nx, ny) in Around(cell.X, cell.Y))
            {
                if (depth.ContainsKey((nx, ny)) || !walkable(nx, ny)) continue;
                var near = passed[cell] + (Nearest(foes, nx, ny) <= 1 ? 1 : 0);
                if (near > passFoes) continue;
                depth[(nx, ny)] = d + 1;
                passed[(nx, ny)] = near;
                queue.Enqueue((nx, ny));
            }
        }
        Pick best = null;
        long bestScore = long.MinValue;
        var ties = new List<Pick>();
        foreach (var pair in depth)
        {
            var (x, y) = pair.Key;
            if (Chebyshev(x, y, targetX, targetY) > reach) continue;
            var away = Nearest(foes, x, y);
            if (away < minFoeDistance) continue;
            long score = -(long)pair.Value * AwayWeight + Math.Min(away, 50);
            Keep(new Pick { X = x, Y = y, Away = away, Steps = pair.Value }, score, ref best, ref bestScore, ties);
        }
        if (best != null) return Tie(ties, seed, "approach");
        return closer ? Closer(depth, fromX, fromY, targetX, targetY, foes, minFoeDistance, seed) : null;
    }

    // The reachable cell nearest the target, at least minFoeDistance from every foe and nearer than the start; the nearest
    // wins, then the fewer steps, then the larger distance to the foes, then the fight's seed.
    private static Pick Closer(Dictionary<(int, int), int> depth, int fromX, int fromY, int targetX, int targetY, IReadOnlyList<Foe> foes,
        int minFoeDistance, int seed)
    {
        var start = Chebyshev(fromX, fromY, targetX, targetY);
        Pick best = null;
        long bestScore = long.MinValue;
        var ties = new List<Pick>();
        foreach (var pair in depth)
        {
            var (x, y) = pair.Key;
            var left = Chebyshev(x, y, targetX, targetY);
            var away = Nearest(foes, x, y);
            if (left >= start || away < minFoeDistance) continue;
            long score = -(long)left * AwayWeight * AwayWeight - (long)pair.Value * AwayWeight + Math.Min(away, 50);
            Keep(new Pick { X = x, Y = y, Away = away, Steps = pair.Value }, score, ref best, ref bestScore, ties);
        }
        return best == null ? null : Tie(ties, seed, "approach");
    }

    private static void Keep(Pick pick, long score, ref Pick best, ref long bestScore, List<Pick> ties)
    {
        if (score > bestScore)
        {
            bestScore = score;
            best = pick;
            ties.Clear();
            ties.Add(pick);
        }
        else if (score == bestScore)
        {
            ties.Add(pick);
        }
    }

    // A true tie: the fight's seed chooses (stable order first, so the same seed always picks the same cell).
    private static Pick Tie(List<Pick> ties, int seed, string mode)
    {
        ties.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var index = (int)((uint)Mix(seed) % (uint)ties.Count);
        var pick = ties[index];
        pick.Mode = mode;
        return pick;
    }

    // A small integer hash, the same on every machine (unlike string.GetHashCode).
    internal static int Mix(int value)
    {
        unchecked
        {
            var h = (uint)value;
            h ^= h >> 16;
            h *= 0x7feb352d;
            h ^= h >> 15;
            h *= 0x846ca68b;
            h ^= h >> 16;
            return (int)h;
        }
    }

    // A seed from a text (the bandit's id) and a number (the fight's start), stable across runs.
    internal static int SeedOf(string text, long number)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in text ?? "") h = h * 31 + c;
            return Mix(h ^ (int)number ^ (int)(number >> 32));
        }
    }

    private static IEnumerable<(int X, int Y)> Around(int x, int y)
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) yield return (x + dx, y + dy);
    }

    private static int Nearest(IReadOnlyList<Foe> foes, int x, int y)
    {
        var nearest = int.MaxValue;
        foreach (var foe in foes) nearest = Math.Min(nearest, Chebyshev(x, y, foe.X, foe.Y));
        return foes.Count == 0 ? 50 : nearest;
    }

    private static int NearestFriend(IReadOnlyList<(int X, int Y)> friends, int x, int y)
    {
        if (friends.Count == 0) return 0;
        var nearest = int.MaxValue;
        foreach (var (fx, fy) in friends) nearest = Math.Min(nearest, Chebyshev(x, y, fx, fy));
        return nearest;
    }

    // Walkable cells one and two steps beyond (x, y) that keep at least its distance to the foes.
    private static int Exits(int x, int y, int away, IReadOnlyList<Foe> foes, Func<int, int, bool> walkable)
    {
        var seen = new HashSet<(int, int)> { (x, y) };
        var frontier = new List<(int X, int Y)> { (x, y) };
        var count = 0;
        for (var step = 0; step < 2; step++)
        {
            var next = new List<(int X, int Y)>();
            foreach (var (cx, cy) in frontier)
            {
                foreach (var (nx, ny) in Around(cx, cy))
                {
                    if (!seen.Add((nx, ny)) || !walkable(nx, ny)) continue;
                    next.Add((nx, ny));
                    if (Nearest(foes, nx, ny) >= away) count++;
                }
            }
            frontier = next;
        }
        return count;
    }

    // Whether the path to the cell passes within one cell of a foe other than the nearest one at the start; onlyWeak
    // when every foe it passes is weak or stunned.
    private static bool PassesSecond((int X, int Y) cell, (int X, int Y) start, Dictionary<(int, int), (int, int)> parent,
        IReadOnlyList<Foe> foes, int primary, out bool onlyWeak)
    {
        onlyWeak = true;
        var passes = false;
        var at = cell;
        while (at != start)
        {
            for (var i = 0; i < foes.Count; i++)
            {
                if (i == primary || Chebyshev(at.X, at.Y, foes[i].X, foes[i].Y) > 1) continue;
                passes = true;
                if (!foes[i].Weak) onlyWeak = false;
            }
            at = parent[at];
        }
        return passes;
    }
}
