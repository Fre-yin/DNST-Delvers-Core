using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// (Frieren fell 9/10 after 20.5 s): the shield pulls attackers off the healer
// and off any party member below GroupAiReachRules.CounterShare, the healer first. Guardian's Taunt (self, Manhattan
// radius from the skill data) when an attacker is in its reach and that attacker's time limit is over; otherwise the
// shield's own attack turns to the attacker at once, or from another attacker of the victim once the picked one stood
// CounterHoldSeconds on it. Shield Up at low health and
// the boss taunt stay before it (GroupAiPolicy.Shield); a player's order on the shield goes first.
// An attacker whose taunt would end at once (its healer fell hard, GroupAiBandits.TauntWouldBreak) counts as out of the
// taunt's reach, so with none left the shield turns its attack instead.
internal static class GroupAiCounterTank
{
    private const string TauntSkill = "SKILL_GuardiansTaunt";
    // attacker guid -> game time of the last counter taunt; victim|attacker -> since when the attacker stands on it.
    private static readonly Dictionary<string, float> lastTaunt = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> onVictimSince = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> lastLogged = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        lastTaunt.Clear();
        onVictimSince.Clear();
        lastLogged.Clear();
    }

    // healerFirst (a, Frieren fell with only 1-2 counter taunts in her last 10 s): the call before
    // the boss taunt, for the party's healer only; the healer is taunted for at once, without the time limit.
    internal static GroupAiPolicy.Choice Choose(GroupAiSituation s, bool healerFirst = false)
    {
        if (s == null || s.Enemies.Count == 0 || GroupAiStance.PlayerQuiet(s.Guid)) return null;
        var now = GameClock.Now;
        // Who each enemy attacks right now (its combat target).
        var attackers = new Dictionary<string, List<GroupAiSituation.Figure>>(StringComparer.Ordinal);
        foreach (var enemy in s.Enemies)
        {
            var target = CoreGame.Component<CombatInfoComponent>(enemy.Unit.Cast<IEntity>())?.GetCombatTarget();
            if (target == null) continue;
            var guid = target.Guid.ToString();
            if (!attackers.TryGetValue(guid, out var list)) attackers[guid] = list = new List<GroupAiSituation.Figure>();
            list.Add(enemy);
        }
        string healer = null;
        var allies = new List<(string Guid, float Share, int Attackers)>();
        foreach (var ally in s.Allies)
        {
            if (GroupAiRoles.Known(ally.Guid) == GroupAiRole.Heal) healer ??= ally.Guid;
            allies.Add((ally.Guid, ally.Share, attackers.TryGetValue(ally.Guid, out var on) ? on.Count : 0));
        }
        ForgetGone(attackers, now);
        var victim = GroupAiReachRules.CounterVictim(healer, allies);
        if (victim == null || (healerFirst && victim != healer)) return null;
        var reason = victim == healer ? "healer" : "low";
        var taunt = s.Find(TauntSkill);
        var reach = taunt?.Data != null ? Math.Max(1, taunt.Data.AreaRadius) : 0;
        var seconds = GroupAiReachRules.CounterSeconds(CoreSettings.Number(GroupAiReachRules.CounterSecondsKey));
        GroupAiSituation.Figure pick = null;
        var pickHeld = 0f;
        var tauntable = false;
        foreach (var attacker in attackers[victim])
        {
            var held = now - onVictimSince[victim + "|" + attacker.Guid];
            var inReach = taunt != null && Manhattan(s.Self.Position, attacker.Position) <= reach && !GroupAiBandits.TauntWouldBreak(attacker.Unit);
            var due = GroupAiReachRules.CounterDue(victim == healer, lastTaunt.TryGetValue(attacker.Guid, out var at), now, at, seconds);
            // A tauntable attacker first, then the one longest on the victim.
            var better = pick == null || (inReach && due && !tauntable) || ((inReach && due) == tauntable && held > pickHeld);
            if (!better) continue;
            pick = attacker;
            pickHeld = held;
            tauntable = inReach && due;
        }
        if (pick == null) return null;
        var current = s.CombatTarget?.Guid.ToString();
        var onPick = current != null && current == pick.Guid;
        var onOther = current != null && !onPick && attackers[victim].Exists(a => a.Guid == current);
        var (action, why) = GroupAiReachRules.CounterAction(taunt?.Free ?? false, tauntable, tauntable, pickHeld, onPick, onOther);
        Note(s, victim, pick.Guid, action, reason, why);
        if (action == "taunt")
        {
            foreach (var attacker in attackers[victim])
                if (Manhattan(s.Self.Position, attacker.Position) <= reach) lastTaunt[attacker.Guid] = now;
            return new GroupAiPolicy.Choice { Skill = taunt.Key, Target = null, Rule = "shield-counter-taunt" };
        }
        if (action == "retarget" && s.BasicAttack.Length > 0)
            return new GroupAiPolicy.Choice { Skill = s.BasicAttack, Target = pick.Unit.Cast<IEntity>(), Rule = "shield-counter-retarget" };
        return null;
    }

    // Guardian's Taunt from the damage path is skipped when every foe in its reach would
    // break it at once (GroupAiBandits.TauntWouldBreak); with no foe in reach the old rule stays.
    internal static bool TauntWasted(GroupAiSituation s, string skill, SkillTableData data)
    {
        if (skill != TauntSkill || data == null) return false;
        var reach = Math.Max(1, data.AreaRadius);
        var inReach = 0;
        var breaking = 0;
        foreach (var enemy in s.Enemies)
        {
            if (Manhattan(s.Self.Position, enemy.Position) > reach) continue;
            inReach++;
            if (GroupAiBandits.TauntWouldBreak(enemy.Unit)) breaking++;
        }
        return GroupAiReachRules.TauntWasted(inReach, breaking);
    }

    // Stamps when each attacker began on its target; drops pairs no longer seen.
    private static void ForgetGone(Dictionary<string, List<GroupAiSituation.Figure>> attackers, float now)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in attackers)
            foreach (var attacker in pair.Value)
            {
                var key = pair.Key + "|" + attacker.Guid;
                seen.Add(key);
                if (!onVictimSince.ContainsKey(key)) onVictimSince[key] = now;
            }
        var gone = new List<string>();
        foreach (var key in onVictimSince.Keys)
            if (!seen.Contains(key)) gone.Add(key);
        foreach (var key in gone) onVictimSince.Remove(key);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    // One line per change of victim, attacker or action.
    private static void Note(GroupAiSituation s, string victim, string attacker, string action, string reason, string why)
    {
        var key = victim + "|" + attacker + "|" + action + "|" + why;
        if (lastLogged.TryGetValue(s.Guid, out var last) && last == key) return;
        lastLogged[s.Guid] = key;
        GroupAiPolicy.Log(GroupAiReachRules.CounterLine(s.Guid, victim, attacker, action, reason, why, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }
}
