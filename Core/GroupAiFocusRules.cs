namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the party's choice of target: the foes' roles and ranks, who is in reach, the melee switch and the
// hold on a chosen target.
internal static partial class GroupAiRules
{
    // ---- the party's choice of target (spec 2026-10-13 sections 2/3, /) ----
    // Rank: healer 0, ranged 1, melee 2, guard 3. The guard blocks physical damage only, so for a caster (magic) it counts
    // as melee. Within a rank: the lowest share, then the nearest, then the fight's seed. Reachable first: ranged and
    // magic in the skill's exact range (no +1), everyone within FocusReach; one out of reach only when none is in it.
    // Melee switches at most FocusMeleeExtra cells further than the nearest and not past another foe next to the line,
    // and keeps a target below FocusKeepShare unless a healer comes in reach. A chosen target holds FocusHoldSeconds.
    internal const int FocusReach = 6;
    internal const int FocusMeleeExtra = 3;
    internal const float FocusKeepShare = 0.25f;
    internal const float FocusHoldSeconds = 2f;
    internal const string FocusNativeHealersKey = "groupai.focus.native-healers";

    internal const string EnemyHeal = "heal", EnemyRanged = "ranged", EnemyMelee = "melee", EnemyGuard = "guard";

    // A foe's part: Core's bandits by their kit, the game's units by their data (heals or revives allies, shield, range).
    internal static string EnemyRoleFrom(string coreRole, bool healsAllies, bool shield, int maxRange)
        => coreRole switch
        {
            "healer" => EnemyHeal,
            "archer" => EnemyRanged,
            "blade" => EnemyMelee,
            "guard" => EnemyGuard,
            _ => healsAllies ? EnemyHeal : shield ? EnemyGuard : maxRange > 1 ? EnemyRanged : EnemyMelee
        };

    // A skill heals or revives allies when it aims at allies (AffectTarget SingleAlly 7, MultipleAlly 8) and its key says so.
    internal static bool HealsAllies(int affectTarget, string skillKey)
        => (affectTarget == 7 || affectTarget == 8) && skillKey != null
           && (skillKey.Contains("Heal") || skillKey.Contains("Resurrection") || skillKey.Contains("Revive") || skillKey.Contains("Cure"));

    // The game's patterns that look for the lowest ally (FemaleGloth) or a dead one to raise (GoblinChiefShaman).
    internal static bool HealerPattern(string pattern)
        => pattern != null && (pattern.Contains("FemaleGloth") || pattern.Contains("GoblinChiefShaman"));

    internal static int FocusRank(string enemyRole, bool physicalAttacker)
        => enemyRole switch
        {
            EnemyHeal => 0,
            EnemyRanged => 1,
            EnemyGuard => physicalAttacker ? 3 : 2,
            _ => 2
        };

    internal sealed class FocusCandidate
    {
        internal string Guid;
        internal int Rank;
        internal float Share;
        internal int Distance;
        internal bool Reachable;
    }

    // The best candidate's GUID, null for none; ties after rank, share and distance go by the fight's seed.
    internal static string FocusPick(IReadOnlyList<FocusCandidate> candidates, long fightSeed)
    {
        FocusCandidate best = null;
        var bestTie = 0;
        if (candidates == null) return null;
        foreach (var c in candidates)
        {
            if (c?.Guid == null) continue;
            var tie = EscapeRules.SeedOf(c.Guid, fightSeed);
            if (best == null || FocusBefore(c, tie, best, bestTie))
            {
                best = c;
                bestTie = tie;
            }
        }
        return best?.Guid;
    }

    private static bool FocusBefore(FocusCandidate a, int aTie, FocusCandidate b, int bTie)
    {
        if (a.Reachable != b.Reachable) return a.Reachable;
        if (a.Rank != b.Rank) return a.Rank < b.Rank;
        if (Math.Abs(a.Share - b.Share) > 0.0001f) return a.Share < b.Share;
        if (a.Distance != b.Distance) return a.Distance < b.Distance;
        return aTie < bTie;
    }

    // Whether a foe is in reach for the attacker: ranged and magic by the skill's exact range, melee by the extra cells over
    // the nearest foe; everyone within FocusReach.
    internal static bool FocusReachable(bool melee, int distance, int skillRange, int nearestDistance)
        => distance <= FocusReach && (melee ? distance <= nearestDistance + FocusMeleeExtra : distance <= Math.Max(1, skillRange));

    // Melee may switch to the candidate: not past another foe next to the line, and not away from a target below
    // FocusKeepShare unless the candidate is a healer.
    internal static bool MeleeSwitchAllowed(bool foeOnLine, float? currentShare, bool candidateHeals)
        => !foeOnLine && (candidateHeals || currentShare is not { } share || share >= FocusKeepShare);

    // The cells between attacker and target (both ends excluded) on the straight line, Bresenham.
    internal static List<(int X, int Y)> CellsBetween(int fromX, int fromY, int toX, int toY)
    {
        var cells = new List<(int X, int Y)>();
        int dx = Math.Abs(toX - fromX), dy = -Math.Abs(toY - fromY), sx = fromX < toX ? 1 : -1, sy = fromY < toY ? 1 : -1, err = dx + dy;
        int x = fromX, y = fromY;
        while (true)
        {
            if (x == toX && y == toY) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
            if (x == toX && y == toY) break;
            cells.Add((x, y));
        }
        return cells;
    }

    // Another foe stands next to (or on) a cell of the way to the target.
    internal static bool FoeOnLine(int fromX, int fromY, int toX, int toY, IEnumerable<(int X, int Y)> otherFoes)
    {
        var line = CellsBetween(fromX, fromY, toX, toY);
        if (line.Count == 0 || otherFoes == null) return false;
        foreach (var foe in otherFoes)
            foreach (var cell in line)
                if (Distance(foe.X, foe.Y, cell.X, cell.Y) <= 1) return true;
        return false;
    }

    // A target chosen at holdSince stays while it lives and is reachable, for FocusHoldSeconds game seconds; also after the
    // player let go of a forced target. A healer newly in reach breaks the hold.
    internal static bool FocusHolds(float now, float holdSince, bool targetValid, bool healerNewlyReachable)
        => targetValid && !healerNewlyReachable && !GameClockRules.Elapsed(now, holdSince, FocusHoldSeconds);
}
