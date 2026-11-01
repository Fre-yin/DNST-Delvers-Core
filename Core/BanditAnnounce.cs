using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the area announcement: CheckArea decides for a skill with an area whether it may go, waits out its
// announcement or is skipped because it would hit a comrade; AlliesInArea and AlliesOnCells name the bandits in the way.
internal static partial class GroupAiBandits
{
    // As GroupAiPolicy.CheckArea, for the bandits' side.
    private static GroupAiRules.AreaPhase CheckArea(Situation s, Slot slot, Vector2Int at)
    {
        var allies = AlliesInArea(s, slot, at);
        var (castX, castY) = GroupAiRules.CastCell(s.Self.Position.x, s.Self.Position.y, at.x, at.y, slot.Data.Range);
        var from = new Vector2Int(castX, castY);
        var cells = GroupAiZones.AreaCells(slot.Data, from, at);
        if (from != s.Self.Position)
        {
            foreach (var onCell in AlliesOnCells(s, cells))
            {
                var known = false;
                foreach (var ally in allies)
                {
                    if (ally.Guid == onCell.Guid)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known) allies.Add(onCell);
            }
        }
        GroupAiZones.LogArea(GroupAiZones.Bandits, s.Guid, slot.Data, from, at, allies.Count);
        var key = s.Guid + "|" + slot.Key;
        var now = GameClock.Now;
        var marked = holdSince.TryGetValue(key, out var since) && since <= now;
        var phase = GroupAiRules.AreaPhaseOf(allies.Count, marked, since, now);
        var flag = GroupAiRules.FlagText((int)slot.Data.AffectTarget);
        if (phase != GroupAiRules.AreaPhase.Wait)
        {
            if (marked) Log(GroupAiRules.HoldEndLine(GroupAiZones.Bandits, s.Guid, slot.Key, phase == GroupAiRules.AreaPhase.Cast, now - since, Tick()));
            holdSince.Remove(key);
            GroupAiZones.Clear(s.Guid, slot.Key);
            if (phase == GroupAiRules.AreaPhase.Cast && cells != null)
                GroupAiZones.Mark(GroupAiZones.Bandits, s.Guid, slot.Key, flag, cells, now + GroupAiRules.HoldSeconds);
            if (phase == GroupAiRules.AreaPhase.Skip)
            {
                friendlyFireSkips++;
                if (s.SkippedSkill == null)
                {
                    s.SkippedSkill = slot.Key;
                    s.SkippedAlly = allies[0].Guid;
                    s.SkippedFlag = flag;
                }
            }
            return phase;
        }
        if (marked) return phase;
        holdSince[key] = now;
        holds++;
        var guids = new List<string>();
        var positions = new List<Vector2Int>();
        foreach (var ally in allies)
        {
            guids.Add(ally.Guid);
            positions.Add(ally.Position);
        }
        Log(GroupAiRules.AnnounceLine(true, s.Guid, slot.Key, guids, flag));
        var zone = cells ?? GroupAiZones.AroundCells(positions);
        GroupAiZones.Mark(GroupAiZones.Bandits, s.Guid, slot.Key, flag, zone, now + GroupAiRules.HoldSeconds);
        foreach (var ally in allies)
        {
            if (GroupAiZones.TryDodge(GroupAiZones.Bandits, ally.Unit, s.InMap, CombatTargetOf(ally.Unit), IsMelee(KitOf(ally.Unit))))
                dodges++;
        }
        return phase;
    }

    private static List<Figure> AlliesOnCells(Situation s, List<Vector2Int> cells)
    {
        var found = new List<Figure>();
        if (cells == null) return found;
        foreach (var ally in s.Allies)
        {
            foreach (var cell in cells)
            {
                if (cell == ally.Position)
                {
                    found.Add(ally);
                    break;
                }
            }
        }
        return found;
    }

    private static List<Figure> AlliesInArea(Situation s, Slot slot, Vector2Int at)
    {
        var found = new List<Figure>();
        try
        {
            var targets = slot.Skill?.GetAffectTargets(s.InMap, s.Unit.Cast<IEntity>(), at);
            for (var i = 0; targets != null && i < targets.Count; i++)
            {
                var entity = targets[i];
                if (entity == null) continue;
                var guid = entity.Guid.ToString();
                foreach (var ally in s.Allies)
                {
                    if (ally.Guid == guid)
                    {
                        found.Add(ally);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_AREA_FAILED slot=" + slot.Key + ": " + ex.Message);
        }
        return found;
    }
}
