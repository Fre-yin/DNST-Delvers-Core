namespace DungeonSettlersDelvers.Core;

// GroupAiRules, moves and vines: when a move order has ended, how a slowed unit moves, and which line the
// vines take.
internal static partial class GroupAiRules
{
    // ---- move orders followed to their end (none of 106 bandit
    // move orders arrived; GroupAiMoveWatch) ----

    // Half a second after the order the task in the unit's brain is noted (task=).
    internal const int MoveProbeTicks = 30;

    // Time to arrive in game ticks (60 a second): one second to start and one a cell, at most 20 s.
    internal static int MoveDeadlineTicks(int cells) => Math.Min(60 + 60 * Math.Max(0, cells), 1200);

    // On the target cell; from two cells or more off a neighbour (Chebyshev 1) counts too, the target
    // may be taken. A one cell step must reach its cell, and so must a dodge (exact): its cell is the first one out of the area, the
    // neighbour may lie inside (F7: dodge 444,140 to 441,144 ended "arrived" at 441,143, inside the chain's
    // circle, and the chain hit 1.5 s later).
    internal static bool MoveArrived(int distance, int startDistance, bool exact = false)
        => distance == 0 || (!exact && distance == 1 && startDistance >= 2);

    // Without a step off the start cell within MoveBlockTicks (1.5 s) the move counts as blocked.
    internal const int MoveBlockTicks = 90;

    // The end of an order, or null while it runs: arrived; held at once (stun, bondage, unconscious);
    // interrupted once another task took the place of Core's MoveTask in the brain (hits,
    // the tree's own step); blocked without a step after MoveBlockTicks; timeout after the deadline.
    // elapsed counts game ticks weighted with the unit's movement speed (MoveSpeedFactor).
    internal static string MoveResult(int distance, int startDistance, long elapsed, int deadline, int moved, bool held, bool lost, bool exact = false)
    {
        if (MoveArrived(distance, startDistance, exact)) return "arrived";
        if (held) return "held";
        if (lost) return "interrupted";
        if (moved == 0 && elapsed >= MoveBlockTicks) return "blocked";
        if (elapsed < deadline) return null;
        return "timeout";
    }

    // Below this factor a unit counts as slowed (MOVE_DONE slowed=, KiteStepFor).
    internal const double SlowedBelow = 0.995;

    internal static bool Slowed(double speed) => speed < SlowedBelow;

    // ---- the vines: a kite and escape tool, not damage, never thrown blind (GroupAiVines) ----

    internal const string VinesSkill = "SKILL_ClingingVines";

    // SKILL_ClingingVines' routine passes 7 to CombatUtility.GetTilesInDirection (SkillRoutine).
    internal const int VinesRange = 7;

    // The hostiles the vines are for stand this close to the caster.
    internal const int VinesNearCells = 3;

    // V2: none while the caster is provoked (the game swaps the target, the cell is lost).
    internal static bool VinesAllowed(bool free, bool provoked) => free && !provoked;

    // Of the game's 8 lines (index as in CoreLineRules.Directions) only one that roots a hostile near the caster
    // counts: the line with the threat first, then the most near hostiles, then the lower index. Null: no vines.
    // B: why no vines line was found: a line with a foe went through a comrade, a wall ended a line, or no line held one.
    internal static string VinesNoneReason(bool friendCut, bool blocked) => friendCut ? "friend" : blocked ? "blocked" : "no-hit";

    // B: a hit counts as recent within the window (never after a jump back of the clock).
    internal static bool RecentHit(float now, float at, float window) => now >= at && now - at <= window;

    internal static int? VinesPick(IReadOnlyList<(bool HitsThreat, int NearHits)> lines)
    {
        int? best = null;
        for (var i = 0; lines != null && i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.NearHits <= 0) continue;
            if (best == null)
            {
                best = i;
                continue;
            }
            var held = lines[best.Value];
            if (line.HitsThreat != held.HitsThreat ? line.HitsThreat : line.NearHits > held.NearHits) best = i;
        }
        return best;
    }

    // (with Limping the healer never reached a 3-cell kite cell): a slowed
    // healer kites one cell shorter, at half speed or below one cell.
    internal static int KiteStepFor(double speed, int full)
        => speed <= 0.5 ? 1 : Slowed(speed) ? Math.Max(1, full - 1) : full;

    // The game's movement modifier (StatType.MovementSpeedPerModifier: Slow -30, Limping -40, in percent or as
    // a fraction) as a factor for the deadline's clock; 1 without a value, 0.2 to 2.
    internal static double MoveSpeedFactor(double perModifier)
    {
        if (double.IsNaN(perModifier) || double.IsInfinity(perModifier)) return 1.0;
        var fraction = Math.Abs(perModifier) >= 1.0 ? perModifier / 100.0 : perModifier;
        return Math.Clamp(1.0 + fraction, 0.2, 2.0);
    }

    // An open order holds the unit's decisions until its deadline (its other
    // ends close it at the next tick).
    internal static bool MoveHolds(bool open, long elapsed, int deadline) => open && elapsed >= 0 && elapsed < deadline;

    // A Core bandit's vines hit only player and ally units (FactionType 1 and 2), never the caster, its comrades or a
    // neutral unit (BanditVines; the game's IsHostile counts every faction for an Enemy caster).
    internal static bool CoreVinesHits(int otherFaction) => otherFaction is 1 or 2;

    // A vines line through a comrade of the caster is not taken (GroupAiVines.Choose): it counts as no hit at all.
    internal static (bool HitsThreat, int NearHits) VinesLineHits(bool hitsThreat, int nearHits, bool throughFriend)
        => throughFriend ? (false, 0) : (hitsThreat, nearHits);
}
