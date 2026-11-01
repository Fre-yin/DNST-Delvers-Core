using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// A ranged party unit whose
// target stands beyond its kit's reach (the basic attack's range) walks GroupAiReachRules.ReachSteps toward its focus, else the
// foe nearest to the healer, else the nearest foe, by Core's own move order (GroupAiZones.OrderMove, so not a player's
// order); the limit is checked again at every step since each order is short. A player's order goes first.
// (no CORE_AI_REACH at all, some bandit always stood within 5): it walks when its target stands
// beyond the kit's reach, whoever else is near; the author's limit (no foe within 3) stays.
internal static class GroupAiRangedReach
{
    private static readonly Dictionary<string, string> lastLogged = new(StringComparer.Ordinal);

    internal static void Reset() => lastLogged.Clear();

    // True when a move order was given; the caller then leaves the decision to the game.
    internal static bool TryMove(GroupAiSituation s)
    {
        if (s == null || s.Role != GroupAiRole.Ranged || s.State != CombatState.InCombat || s.Enemies.Count == 0 || GroupAiStance.PlayerQuiet(s.Guid)) return false;
        // In formation a ranged member keeps its place and shoots what comes into reach (GroupAiFormation).
        if (GroupAiFormation.Holds(s.Owner)) return false;
        var range = KitRange(s);
        var nearest = s.ClosestEnemy;
        var nearestDistance = nearest?.Distance ?? int.MaxValue;
        var target = TargetOf(s);
        var action = GroupAiReachRules.ReachAction(target != null, target?.Distance ?? 0, range, nearestDistance);
        if (action == "none") return false;
        if (action == "blocked")
        {
            Note(s, action, target, null, range, nearestDistance);
            return false;
        }
        var (x, y) = GroupAiReachRules.ReachStep(s.Self.Position.x, s.Self.Position.y, target.Position.x, target.Position.y, range);
        if (x == s.Self.Position.x && y == s.Self.Position.y) return false;
        if (!GroupAiZones.OrderMove(s.Owner, new Vector2Int(x, y), "reach")) return false;
        Note(s, action, target, x + "," + y, range, nearestDistance);
        return true;
    }

    private static int KitRange(GroupAiSituation s)
    {
        var data = s.Find(s.BasicAttack)?.Data ?? DataSheetManager.Instance?.GetSkillData(s.BasicAttack);
        return Math.Max(1, data?.Range ?? 1);
    }

    private static GroupAiSituation.Figure TargetOf(GroupAiSituation s)
    {
        var focus = GroupAiFocus.HeldTarget(s.Guid);
        var held = focus == null ? null : s.Enemies.Find(e => e.Guid == focus);
        if (held != null) return held;
        GroupAiSituation.Figure healer = null;
        foreach (var ally in s.Allies)
            if (GroupAiRoles.Known(ally.Guid) == GroupAiRole.Heal)
            {
                healer = ally;
                break;
            }
        if (healer == null) return s.ClosestEnemy is { } closest && InCombat(closest) ? closest : null;
        GroupAiSituation.Figure best = null;
        var bestDistance = int.MaxValue;
        foreach (var enemy in s.Enemies)
        {
            if (!InCombat(enemy)) continue;
            var distance = GroupAiSituation.Distance(enemy.Position, healer.Position);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    // Only foes already fighting count, never a camp further off that has not woken.
    private static bool InCombat(GroupAiSituation.Figure enemy)
        => CoreGame.Component<CombatInfoComponent>(enemy.Unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;

    // One line per change of action and target, and per step.
    private static void Note(GroupAiSituation s, string action, GroupAiSituation.Figure target, string to, int range, int nearest)
    {
        var key = action + "|" + (target?.Guid ?? "-") + "|" + (to ?? "-");
        if (lastLogged.TryGetValue(s.Guid, out var last) && last == key) return;
        lastLogged[s.Guid] = key;
        GroupAiPolicy.Log(GroupAiReachRules.ReachLine(s.Guid, action, target?.Guid, to, target?.Distance ?? -1, range, nearest,
            CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }
}
