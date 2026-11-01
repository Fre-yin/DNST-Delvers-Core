using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiPolicy, the other roles: the shield (shield wall, taunts, counter tank, Iron Clad), the ranged unit (arrow rain on
// the focus, head aim, ankle break), the vines every role may cast to escape, and the role name prefix of a rule. The
// fields, the shared helpers (Damage, Log, Tick) and the aliases of the game's types stay in GroupAiPolicy.cs.
internal static partial class GroupAiPolicy
{
    private static Choice Shield(GroupAiSituation s)
    {
        var shieldUp = s.Find("SKILL_ShieldUp");
        var wall = WallReason(s);
        if ((shieldUp?.Free ?? false) && wall != null && s.Self.Share < GroupAiRules.ShieldUpShare)
        {
            var enemy = wall == "elite" ? s.EliteOnMe.Guid.ToString() : s.Leader?.Guid.ToString() ?? "-";
            Log(GroupAiRules.ShieldWallLine(s.Guid, wall, enemy, s.Self.Share, Tick()));
            return NewChoice(shieldUp.Key, null, "shield-up");
        }
        // A: the party's healer under attack comes before the boss taunt; only Shield Up in the shield's own need is earlier.
        var forHealer = GroupAiCounterTank.Choose(s, healerFirst: true);
        if (forHealer != null) return forHealer;
        var taunt = s.Find("SKILL_GuardiansTaunt");
        if (taunt != null && taunt.Free && s.Boss != null && s.BossTarget != null && s.BossTarget.Guid.ToString() != s.Guid)
        {
            if (GameClock.Since(bossTargetSince) > GroupAiRules.HoldSeconds) tauntLate++;
            return NewChoice(taunt.Key, s.Boss.Cast<IEntity>(), "shield-taunt");
        }
        // Attackers off the healer and off anyone below 35 % (GroupAiCounterTank).
        var counter = GroupAiCounterTank.Choose(s);
        if (counter != null) return counter;
        // Iron Clad once per fight, a fight being the unit and its combat target.
        var ironClad = s.Find("SKILL_IronClad");
        var fight = s.Guid + "|" + (s.CombatTarget != null ? s.CombatTarget.Guid.ToString() : "-");
        if (ironClad != null && ironClad.Free && s.State == CombatState.InCombat && ironCladFight.Add(fight))
        {
            return NewChoice(ironClad.Key, null, "shield-ironclad");
        }
        return Damage(s, "shield-damage", GroupAiRules.ShieldReserve);
    }

    private static Choice Ranged(GroupAiSituation s)
    {
        var rain = s.Find("SKILL_ArrowRain");
        // (with the herbalist as focus 126 rain hits went to others): the rain on a focus of rank 0 or 1 in reach,
        // also alone; without one the rule below.
        // (5 of 6 rains fell at the fight's start, before any focus was held): the focus is chosen first.
        if (rain != null && rain.Free && rain.Data != null) GroupAiFocus.Choose(s, rain.Data.Range, rain.Data.AffectTarget, s.CombatTarget, "ranged");
        var rainFocus = rain != null && rain.Free && rain.Data != null ? GroupAiFocus.RainTarget(s, rain.Data.Range) : null;
        if (rainFocus != null)
        {
            return NewChoice(rain.Key, rainFocus, "ranged-rain-focus");
        }
        if (rain != null && rain.Free && s.CombatTarget != null)
        {
            var at = s.PositionOf(s.CombatTarget);
            var radius = Math.Max(1, rain.Data.AreaRadius);
            // The count is a square around the target (Chebyshev, GroupAiSituation.Distance); the rain itself hits a diamond
            // (Manhattan 2), so a unit at a corner counts without being hit.
            var hit = 0;
            foreach (var enemy in s.Enemies)
                if (GroupAiSituation.Distance(at, enemy.Position) <= radius) hit++;
            if (hit >= 2 && GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, at), rain.Data.Range))
            {
                return NewChoice(rain.Key, s.CombatTarget, "ranged-rain");
            }
        }
        var headAim = s.Find("SKILL_HeadAim");
        if (headAim != null && headAim.Free && !BuffRunning(s, headAim) && HasReserve(s, headAim, GroupAiRules.DamageReserve))
        {
            return NewChoice(headAim.Key, null, "ranged-headaim");
        }
        var ankle = s.Find("SKILL_AnkleBreak");
        var target = EnemyFigure(s, s.CombatTarget);
        if (ankle != null && ankle.Free && target != null && target.Distance <= 2 && HasReserve(s, ankle, GroupAiRules.DamageReserve))
        {
            return NewChoice(ankle.Key, s.CombatTarget, "ranged-ankle");
        }
        return Damage(s, "ranged", GroupAiRules.DamageReserve, "SKILL_ArrowRain", "SKILL_AnkleBreak", "SKILL_HeadAim");
    }

    // The vines are a kite and escape tool, never damage and never thrown blind. Only when an
    // enemy targeting the unit stands within VinesNearCells and one of the game's 8 lines roots an enemy that near
    // (GroupAiVines); none while provoked.
    private static Choice EscapeVines(GroupAiSituation s)
    {
        var slot = s.Find(GroupAiRules.VinesSkill);
        if (slot == null || slot.Data == null || !slot.Free) return null;
        var near = new List<(IEntity Entity, Vector2Int Position)>();
        GroupAiSituation.Figure threat = null;
        foreach (var enemy in s.Enemies)
        {
            if (enemy.Distance > GroupAiRules.VinesNearCells) continue;
            var entity = enemy.Unit.Cast<IEntity>();
            near.Add((entity, enemy.Position));
            var aim = CoreGame.Component<CombatInfoComponent>(entity)?.GetCombatTarget();
            // B: a foe that hit the unit within GroupAiPartyHits.WindowSeconds is a threat too.
            var onMe = (aim != null && aim.Guid.ToString() == s.Guid) || GroupAiPartyHits.HitWithin(s.Guid, enemy.Guid);
            if (onMe && (threat == null || enemy.Distance < threat.Distance)) threat = enemy;
        }
        var tick = Tick();
        if (threat == null)
        {
            if (near.Count > 0) Log(GroupAiRules.VinesLine(true, s.Guid, "none:no-threat", 0, 0, "-", false, near.Count, tick));
            return null;
        }
        if (!GroupAiRules.VinesAllowed(slot.Free, GroupAiVines.Provoked(s.Self.Unit)))
        {
            Log(GroupAiRules.VinesLine(true, s.Guid, "provoked", 0, 0, "-", false, 0, tick));
            return null;
        }
        // B: the cells units stand on do not end a line (GroupAiVines); the party's lines are not cut by comrades, as before.
        var occupied = new HashSet<long>();
        foreach (var enemy in s.Enemies) occupied.Add(GroupAiRules.CellKey(enemy.Position.x, enemy.Position.y));
        foreach (var ally in s.Allies) occupied.Add(GroupAiRules.CellKey(ally.Position.x, ally.Position.y));
        var pick = GroupAiVines.Choose(s.InMap, s.Self.Position, near, threat.Unit.Cast<IEntity>(), null, occupied, out var reason);
        if (pick == null)
        {
            Log(GroupAiRules.VinesLine(true, s.Guid, "none:" + reason, 0, 0, "-", false, near.Count, tick));
            return null;
        }
        // The healer casts the vines only at a target within the skill's exact range, so the game does not walk her.
        if (s.Role == GroupAiRole.Heal && !InReach(s, GroupAiSituation.Distance(s.Self.Position, s.PositionOf(pick.Target)), slot.Data.Range))
        {
            Log(GroupAiRules.VinesLine(true, s.Guid, "range", 0, 0, pick.Target.Guid.ToString(), false, near.Count, tick));
            return null;
        }
        Log(GroupAiRules.VinesLine(true, s.Guid, "cast", pick.Direction.X, pick.Direction.Y, pick.Target.Guid.ToString(),
            pick.HitsThreat, pick.NearHits, tick));
        return NewChoice(slot.Key, pick.Target, GroupAiRules.RuleFor(RolePrefix(s.Role) + "-escape", slot.Key));
    }

    private static string RolePrefix(GroupAiRole role) => role switch
    {
        GroupAiRole.Heal => "heal",
        GroupAiRole.Shield => "shield",
        GroupAiRole.Ranged => "ranged",
        GroupAiRole.Caster => "caster",
        _ => "melee"
    };

    private static GroupAiSituation.Figure EnemyFigure(GroupAiSituation s, IEntity entity)
    {
        if (entity == null) return null;
        var guid = entity.Guid.ToString();
        foreach (var enemy in s.Enemies)
            if (enemy.Guid == guid) return enemy;
        return null;
    }
}
