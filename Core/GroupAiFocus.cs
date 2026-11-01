#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// (spec 2026-10-13 sections 2/3, /): the party's choice of target. A
// damage decision of the group AI aims at the foe of the lowest rank (GroupAiRules.FocusRank: healer, ranged, melee,
// guard last for physical attackers), in reach, then by share, distance and the fight's seed. Not for the shield (it
// keeps its target and taunt), for the healer only in her damage rules, never while the player forces a target, and
// for 2 game seconds after the player let go of one. Bosses keep adds-first (GroupAiPolicy.Target).
internal static class GroupAiFocus
{
    private static readonly Dictionary<string, string> roles = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (string Target, float Since)> held = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> fightStart = new(StringComparer.Ordinal);
    // (a clicked target stays the focus until a new one is chosen): per unit the foe the player clicked.
    private static readonly Dictionary<string, string> clicked = new(StringComparer.Ordinal);

    internal static void SetClick(string unit, string target)
    {
        if (string.IsNullOrEmpty(target)) clicked.Remove(unit);
        else clicked[unit] = target;
    }

    // The held focus when it ranks 0 or 1 and stands in the skill's reach (GroupAiReachRules.RainOnFocus), else null.
    internal static IEntity RainTarget(GroupAiSituation s, int range)
    {
        var guid = HeldTarget(s.Guid);
        var focus = guid == null ? null : s.Enemies.Find(e => e.Guid == guid);
        if (focus == null) return null;
        var rank = GroupAiRules.FocusRank(RoleOf(focus.Unit), s.Role is GroupAiRole.Melee or GroupAiRole.Ranged);
        return GroupAiReachRules.RainOnFocus(rank, GroupAiRules.InRange(focus.Distance, range)) ? focus.Unit.Cast<IEntity>() : null;
    }

    // The foe the unit holds as its focus, else null (GroupAiRangedReach).
    internal static string HeldTarget(string unit) => unit != null && held.TryGetValue(unit, out var h) ? h.Target : null;

    internal static void Reset()
    {
        roles.Clear();
        held.Clear();
        fightStart.Clear();
        clicked.Clear();
    }

    // The foe's part, cached per unit key: Core's bandits by kit; the game's units by pattern and default skills. The
    // game's healers count only while groupai.focus.native-healers is on.
    internal static string RoleOf(UnitEntity unit)
    {
        var key = unit?.Key ?? "";
        if (roles.TryGetValue(key, out var known)) return known;
        string coreRole = null;
        if (CoreUnitRules.TryGet(key, out var definition)) coreRole = definition.Role;
        var heals = false;
        var shield = false;
        var maxRange = 1;
        if (coreRole == null)
        {
            var data = DataSheetManager.Instance?.GetUnitData(key);
            heals = GroupAiRules.HealerPattern(data?.CombatPattern);
            var skills = data?.DefaultSkills;
            for (var i = 0; skills != null && i < skills.Length; i++)
            {
                var skillKey = skills[i];
                if (string.IsNullOrEmpty(skillKey)) continue;
                if (skillKey.Contains("Shield")) shield = true;
                var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
                if (skill == null) continue;
                if (GroupAiRules.HealsAllies((int)skill.AffectTarget, skillKey)) heals = true;
                if (skill.Range > maxRange) maxRange = skill.Range;
            }
            if (heals && !CoreSettings.FocusNativeHealers) heals = false;
        }
        var role = GroupAiRules.EnemyRoleFrom(coreRole, heals, shield, maxRange);
        roles[key] = role;
        return role;
    }

    // Whether the focus decides this attack: not for the shield, for the healer only in her damage rules.
    internal static bool Applies(GroupAiSituation s, string rule)
        => s.Role != GroupAiRole.Shield && (s.Role != GroupAiRole.Heal || (rule ?? "").StartsWith("heal-damage", StringComparison.Ordinal));

    // The focused foe for an attack with this range, or fallback when the focus does not apply or finds none.
    internal static IEntity Choose(GroupAiSituation s, int range, AffectTarget affect, IEntity fallback, string rule)
    {
        if (!Applies(s, rule) || s.Enemies.Count == 0) return fallback;
        if (!(affect is AffectTarget.SingleHostile or AffectTarget.MultipleHostile)) return fallback;
        var now = GameClock.Now;
        var forced = CoreGame.Component<CombatInfoComponent>(s.Owner.Cast<IEntity>())?.GetForceAttackTarget();
        if (forced != null)
        {
            // A player's forced target: nothing changes, and it holds on for 2 game seconds after the player lets go.
            held[s.Guid] = (forced.Guid.ToString(), now);
            return fallback;
        }
        // The clicked foe stays the focus while it stands among the enemies (any skill that reaches it).
        if (clicked.TryGetValue(s.Guid, out var click))
        {
            var target = s.Enemies.Find(e => e.Guid == click);
            if (target != null) return target.Unit.Cast<IEntity>();
            clicked.Remove(s.Guid);
        }
        if (!fightStart.ContainsKey(s.Guid)) fightStart[s.Guid] = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        var melee = s.Role == GroupAiRole.Melee;
        var physical = s.Role is GroupAiRole.Melee or GroupAiRole.Ranged;
        var nearest = int.MaxValue;
        foreach (var enemy in s.Enemies) nearest = Math.Min(nearest, enemy.Distance);
        var currentGuid = held.TryGetValue(s.Guid, out var hold) ? hold.Target : s.CombatTarget?.Guid.ToString();
        var current = s.Enemies.Find(e => e.Guid == currentGuid);
        var candidates = new List<GroupAiRules.FocusCandidate>();
        var healerReachable = false;
        foreach (var enemy in s.Enemies)
        {
            var role = RoleOf(enemy.Unit);
            var reachable = Reachable(s, melee, enemy.Distance, range, nearest, enemy.Guid == currentGuid || role == GroupAiRules.EnemyHeal);
            if (melee && enemy != current)
            {
                var others = new List<(int X, int Y)>();
                foreach (var other in s.Enemies)
                    if (other != enemy) others.Add((other.Position.x, other.Position.y));
                var onLine = GroupAiRules.FoeOnLine(s.Self.Position.x, s.Self.Position.y, enemy.Position.x, enemy.Position.y, others);
                if (!GroupAiRules.MeleeSwitchAllowed(onLine, current?.Share, role == GroupAiRules.EnemyHeal)) continue;
            }
            var rank = GroupAiRules.FocusRank(role, physical);
            if (rank == 0 && reachable) healerReachable = true;
            candidates.Add(new GroupAiRules.FocusCandidate { Guid = enemy.Guid, Rank = rank, Share = enemy.Share, Distance = enemy.Distance, Reachable = reachable });
        }
        var currentReachable = current != null && Reachable(s, melee, current.Distance, range, nearest, true);
        var healerNew = healerReachable && current != null && RoleOf(current.Unit) != GroupAiRules.EnemyHeal;
        if (current != null && hold.Target == current.Guid && GroupAiRules.FocusHolds(now, hold.Since, currentReachable, healerNew))
            return Shot(s, current, range, nearest);
        var pick = GroupAiRules.FocusPick(candidates, fightStart[s.Guid]);
        var chosen = pick == null ? null : s.Enemies.Find(e => e.Guid == pick);
        if (chosen == null) return fallback;
        if (hold.Target != pick || !held.ContainsKey(s.Guid))
        {
            held[s.Guid] = (pick, now);
            var chosenRole = RoleOf(chosen.Unit);
            // Reason=lost when the foe held before is gone or out of reach.
            var reason = healerNew ? "healer" : current == null ? (currentGuid != null ? "lost" : "start") : !currentReachable ? "lost" : "rank";
            GroupAiPolicy.Log(GroupAiRules.FocusLine(s.Guid, pick, currentGuid, GroupAiRules.FocusRank(chosenRole, physical), chosenRole, reason,
                CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        }
        return Shot(s, chosen, range, nearest);
    }

    // Ranged units see foes up to GroupAiReachRules.FocusReachRanged (they walk in, GroupAiRangedReach).
    // Keep (the foe held, an enemy healer) ignores the nearness limit for ranged units, so the focus no longer swings.
    private static bool Reachable(GroupAiSituation s, bool melee, int distance, int range, int nearest, bool keep)
        => s.Role == GroupAiRole.Ranged ? GroupAiReachRules.RangedReachable(distance, range, nearest, keep) : GroupAiRules.FocusReachable(melee, distance, range, nearest);

    // A ranged unit whose focus stands beyond the skill's range while a foe within 3 bars walking in shoots the nearest
    // foe in range instead; the focus stays held (no CORE_AI_TARGET line).
    private static IEntity Shot(GroupAiSituation s, GroupAiSituation.Figure focus, int range, int nearest)
    {
        if (s.Role != GroupAiRole.Ranged || !GroupAiReachRules.RangedInterim(focus.Distance, range, nearest)) return focus.Unit.Cast<IEntity>();
        GroupAiSituation.Figure interim = null;
        foreach (var enemy in s.Enemies)
            if (enemy.Distance <= Math.Max(1, range) && (interim == null || enemy.Distance < interim.Distance)) interim = enemy;
        return (interim ?? focus).Unit.Cast<IEntity>();
    }

    // A player unit out of combat starts its next fight with a new seed and no hold.
    internal static void EndFight(string guid)
    {
        fightStart.Remove(guid);
        held.Remove(guid);
        clicked.Remove(guid);
    }
}
