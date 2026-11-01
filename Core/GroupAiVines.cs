using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The vines (SKILL_ClingingVines) of Core's AI, for the bandits' healer and the party alike: a kite and escape tool,
// not damage, and never thrown blind. The game's own 8 lines from the
// caster (CoreLineRules.DirectionTiles, range 7, a wall ends a line) are checked against the hostiles near the
// caster (GroupAiRules.VinesNearCells); only a line that roots one of them counts (GroupAiRules.VinesPick).
// The target is the threat when the line holds it, else the nearest hostile on the line: its cell lies exactly
// on the line, so the game's snapping (SnapTo8Direction) keeps that direction. 34 of 77 cast
// vines were aimed at a cell off the snapped line and could not root their target.
internal static class GroupAiVines
{
    internal sealed class Pick
    {
        internal IEntity Target;
        internal (int X, int Y) Direction;
        internal bool HitsThreat;
        internal int NearHits;
    }

    // near: the hostiles within VinesNearCells of the caster; threat: the one to stop first (may be null); friends:
    // the caster's comrades, a line through one of them is left out (GroupAiRules.VinesLineHits).
    internal static Pick Choose(IInMapDataSupplier inMap, Vector2Int from, IReadOnlyList<(IEntity Entity, Vector2Int Position)> near, IEntity threat,
        IReadOnlyList<Vector2Int> friends = null)
        => Choose(inMap, from, near, threat, friends, null, out _);

    // B (12 of 19 decisions ended in none with a foe beside her): occupied, the cells units stand on,
    // do not end a line, a unit is what the vines root; reason: why no line was found (no-hit, friend, blocked).
    internal static Pick Choose(IInMapDataSupplier inMap, Vector2Int from, IReadOnlyList<(IEntity Entity, Vector2Int Position)> near, IEntity threat,
        IReadOnlyList<Vector2Int> friends, HashSet<long> occupied, out string reason)
    {
        reason = "no-hit";
        if (inMap == null || near == null || near.Count == 0) return null;
        var friendCut = false;
        var blockedNear = new bool[1];
        var lines = new List<(bool HitsThreat, int NearHits)>(CoreLineRules.Directions.Count);
        var firsts = new List<IEntity>(CoreLineRules.Directions.Count);
        foreach (var (dx, dy) in CoreLineRules.Directions)
        {
            var cells = new HashSet<long>();
            foreach (var (x, y) in CoreLineRules.DirectionTiles(from.x, from.y, dx, dy, GroupAiRules.VinesRange, (x, y) => Blocked(inMap, x, y, occupied, blockedNear)))
                cells.Add(GroupAiRules.CellKey(x, y));
            var hits = 0;
            var hitsThreat = false;
            IEntity first = null;
            var firstDistance = int.MaxValue;
            foreach (var (entity, position) in near)
            {
                if (entity == null || !cells.Contains(GroupAiRules.CellKey(position.x, position.y))) continue;
                hits++;
                if (threat != null && entity.Pointer == threat.Pointer) hitsThreat = true;
                var distance = GroupAiRules.Distance(from.x, from.y, position.x, position.y);
                if (distance < firstDistance)
                {
                    firstDistance = distance;
                    first = entity;
                }
            }
            var throughFriend = false;
            if (friends != null)
                foreach (var friend in friends)
                    if (friend != from && cells.Contains(GroupAiRules.CellKey(friend.x, friend.y))) throughFriend = true;
            if (throughFriend && hits > 0) friendCut = true;
            lines.Add(GroupAiRules.VinesLineHits(hitsThreat, hits, throughFriend));
            firsts.Add(first);
        }
        var index = GroupAiRules.VinesPick(lines);
        if (index == null)
        {
            reason = GroupAiRules.VinesNoneReason(friendCut, blockedNear[0]);
            return null;
        }
        return new Pick
        {
            Target = lines[index.Value].HitsThreat ? threat : firsts[index.Value],
            Direction = CoreLineRules.Directions[index.Value],
            HitsThreat = lines[index.Value].HitsThreat,
            NearHits = lines[index.Value].NearHits
        };
    }

    // The game forces a provoked unit's target and the vines' cell is lost (V2: no vines then).
    internal static bool Provoked(UnitEntity unit)
        => unit != null && CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetForceAttackTarget() != null;

    private const int ProbeCap = 10;
    private static int probes;

    internal static void Reset() => probes = 0;

    // A cell the game calls blocked ends a line unless a unit stands on it (b); the probe logs, capped, when the game
    // calls a unit's cell blocked, which is what made the vines miss a foe next to the caster.
    private static bool Blocked(IInMapDataSupplier inMap, int x, int y, HashSet<long> occupied, bool[] blockedNear)
    {
        bool blocked;
        try { blocked = inMap.IsBlocked(new Vector2Int(x, y)); }
        catch (Exception) { return true; }
        if (!blocked) return false;
        var unitCell = occupied != null && occupied.Contains(GroupAiRules.CellKey(x, y));
        if (unitCell && probes < ProbeCap)
        {
            probes++;
            DelversHost.Info(GroupAiRules.VinesProbeLine(x, y));
        }
        if (unitCell) return false;
        blockedNear[0] = true;
        return true;
    }
}
