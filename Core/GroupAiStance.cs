using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The party healer's distance and swing. She keeps about
// groupai.healer.distance (7) cells from the nearest foe, steps in to at most groupai.healer.castDistance (5) for the
// seed or Lifebloom when the comrade is out of reach and goes back right after the cast, and into danger only in
// the author's emergency (GroupAiRules.HealerEmergency) when no heal reaches the comrade from the safe zone. Walls and dead
// ends come from EscapeRules (an unsafe way out when surrounded).
// (a player's action interrupts the AI until 3 s without one; a clicked target stays her focus): a
// player's order is seen where the game takes it (GroupAiDirectOrderPatch on TaskSystem.DirectOrder, a task Core did
// not send) or as a new forced target without an enemy's taunt (a click on a foe); quiet until StanceQuietSeconds after
// the last one. The game's own tasks (fleeing, walking for a skill, idle) never count, the stance overrides them. After
// the quiet time a clicked foe stays her focus (GroupAiFocus.SetClick) and the game's forced target is let go.
// Movement goes through the party's move order (GroupAiZones.OrderMove, followed by GroupAiMoveWatch), at most one per
// StanceOrderSeconds; the game keeps its own choice of skill for that decision, as with a dodge.
internal static class GroupAiStance
{
    private const string SeedSkill = "SKILL_StrangeSeed";

    private sealed class State
    {
        internal UnitEntity Unit;
        internal string Mode = "";
        internal float LastOrder = float.MinValue;
        internal float LastPlayerOrder = float.MinValue;
        internal string QuietReason = "-";
        internal string LastForced;
        internal string Clicked;
        internal bool WasIn;
        // Since when she is out of combat (game seconds), float.NaN while in combat.
        internal float OutOfCombatSince = float.NaN;
        internal bool FightEnded;
    }

    private static readonly Dictionary<string, State> states = new(StringComparer.Ordinal);

    internal static void Reset() => states.Clear();

    // A player's order on the unit within StanceQuietSeconds (any role); the counter tank and the ranged reach wait.
    internal static bool PlayerQuiet(string guid)
        => guid != null && states.TryGetValue(guid, out var state) && GroupAiRules.StancePlayerQuiet(GameClock.Now, state.LastPlayerOrder);

    private static State StateOf(UnitEntity unit)
    {
        var guid = unit.Guid.ToString();
        if (!states.TryGetValue(guid, out var state)) states[guid] = state = new State();
        state.Unit = unit;
        return state;
    }

    // No enemy left (GroupAiDecideSkillPatch): the next fight starts without a clicked focus.
    internal static void EndFight(string guid)
    {
        if (!states.TryGetValue(guid, out var state)) return;
        state.Clicked = null;
        state.LastForced = null;
    }

    // A player's order on a party unit (GroupAiDirectOrderPatch), Core's own tasks excluded there.
    internal static void NotePlayerOrder(UnitEntity unit, string task, string via = null)
    {
        if (unit == null) return;
        var state = StateOf(unit);
        state.LastPlayerOrder = GameClock.Now;
        state.QuietReason = "player task=" + (task ?? "-");
        GroupAiPolicy.Log(GroupAiRules.StanceOrderLine(unit.Guid.ToString(), "player", task, null, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, via ?? "-"));
    }

    // True when a move order was given; the caller then leaves the decision to the game.
    internal static bool TryMove(GroupAiSituation s, string lastRule)
    {
        if (s.Role != GroupAiRole.Heal || s.Enemies.Count == 0) return false;
        var state = StateOf(s.Owner);
        var now = GameClock.Now;
        var at = s.Self.Position;
        var forced = Forced(s.Owner, out var taunted);
        var quiet = GroupAiRules.StancePlayerQuiet(now, state.LastPlayerOrder);

        var distance = GroupAiRules.StanceCells(CoreSettings.Number(GroupAiRules.StanceDistanceKey), GroupAiRules.StanceDistanceDefault);
        var castDistance = GroupAiRules.StanceCells(CoreSettings.Number(GroupAiRules.StanceCastDistanceKey), GroupAiRules.StanceCastDistanceDefault);
        var nearest = int.MaxValue;
        foreach (var enemy in s.Enemies) nearest = Math.Min(nearest, enemy.Distance);

        // The comrade she wants to heal: one in the author's emergency first, else the lowest below HealShare.
        GroupAiSituation.Figure wanted = null;
        var wantedEmergency = false;
        foreach (var ally in s.Allies)
        {
            if (ally.Unit == null || ally.Unit.IsDead || !Emergency(ally.Unit)) continue;
            if (wanted == null || ally.Share < wanted.Share) wanted = ally;
            wantedEmergency = true;
        }
        if (wanted == null && s.LowestAlly != null && s.LowestAlly.Share < GroupAiRules.HealShare && s.LowestAlly.Distance > 0) wanted = s.LowestAlly;
        // In formation she heals from her place; only the author's emergency moves her (GroupAiFormation).
        var holds = GroupAiFormation.Holds(s.Owner);
        if (!wantedEmergency && holds) return false;
        var reach = HealReach(s);
        var castPending = wanted != null && reach > 0 && wanted.Distance > reach;
        var justCast = state.WasIn && IsHealRule(lastRule);
        // Under an enemy's taunt the target stays forced; out of every reach she closes in, never nearer than 3.
        var taunter = taunted ? s.Enemies.Find(e => e.Guid == forced) : null;
        var maxReach = MaxReach(s);
        var tauntApproach = taunter != null && GroupAiRules.StanceTauntApproach(true, taunter.Distance, maxReach);
        var mode = GroupAiRules.HealerStance(nearest, distance, castPending, justCast, castPending && wantedEmergency, quiet, tauntApproach);
        // Held in formation, the emergency lets her walk to the comrade out of her reach, nothing else: with him already in reach
        // the stance fell to "back" and she fled from her place at full health (three escapes one cell off it).
        if (!GroupAiRules.StanceMovesHeld(holds, mode)) return false;
        if (mode is "none" or "hold" || !GameClockRules.Elapsed(now, state.LastOrder, GroupAiRules.StanceOrderSeconds))
        {
            Note(s, state, mode, nearest, null, quiet ? "quiet:" + state.QuietReason : taunted ? "taunt-ignored" : null);
            return false;
        }

        var foes = s.Enemies.ConvertAll(e => new EscapeRules.Foe(e.Position.x, e.Position.y, false));
        var occupied = new HashSet<(int, int)>();
        foreach (var e in s.Enemies) occupied.Add((e.Position.x, e.Position.y));
        foreach (var a in s.Allies) occupied.Add((a.Position.x, a.Position.y));
        bool Walkable(int x, int y) => !occupied.Contains((x, y)) && IsMovable(s, x, y);
        var seed = EscapeRules.SeedOf(s.Guid, (long)now);
        EscapeRules.Pick pick;
        if (mode == "taunt")
            pick = EscapeRules.Approach(at.x, at.y, taunter.Position.x, taunter.Position.y, maxReach, foes, GroupAiRules.StanceInSteps, 0,
                GroupAiRules.StanceTauntMinDistance, Walkable, seed);
        else if (mode is "in" or "emergency")
        {
            var (steps, passFoes, minFoe) = GroupAiRules.StanceApproach(mode == "emergency", castDistance);
            pick = EscapeRules.Approach(at.x, at.y, wanted.Position.x, wanted.Position.y, reach, foes, steps, passFoes, minFoe, Walkable, seed);
        }
        else
        {
            var friends = s.Allies.ConvertAll(a => (a.Position.x, a.Position.y));
            var hurt = new List<(int X, int Y)>();
            if (wanted != null) hurt.Add((wanted.Position.x, wanted.Position.y));
            pick = EscapeRules.Choose(at.x, at.y, foes, friends, reach > 0 ? reach : distance, GroupAiRules.StanceBackSteps, Walkable, seed, hurt);
        }
        if (pick == null || (pick.X == at.x && pick.Y == at.y))
        {
            Note(s, state, mode, nearest, null, "nocell");
            return false;
        }
        var to = new Vector2Int(pick.X, pick.Y);
        if (!GroupAiZones.OrderMove(s.Owner, to, "stance-" + mode)) return false;
        state.LastOrder = now;
        state.WasIn = mode is "in" or "emergency";
        state.Mode = "";
        Note(s, state, mode, nearest, pick.X + "," + pick.Y, taunted ? pick.Mode + ",taunt-ignored" : pick.Mode);
        return true;
    }

    // Every game tick (GroupAiBanditTickPatch): the fight's end, a click on a foe, and the forced target let go after the
    // quiet time.
    internal static void OnGameTick()
    {
        if (states.Count == 0) return;
        var now = GameClock.Now;
        foreach (var pair in states)
        {
            var state = pair.Value;
            if (state.Unit == null) continue;
            // The fight ends once she is down or out of combat for a while; the focus starts anew.
            var down = state.Unit.IsDead || GroupAiBandits.IsDowned(state.Unit);
            var info = CoreGame.Component<CombatInfoComponent>(state.Unit.Cast<IEntity>());
            var inCombat = !down && info?.GetCombatState() == CombatState.InCombat;
            if (inCombat) state.OutOfCombatSince = float.NaN;
            else if (float.IsNaN(state.OutOfCombatSince)) state.OutOfCombatSince = now;
            var over = GroupAiRules.StanceFightOver(down, inCombat, float.IsNaN(state.OutOfCombatSince) ? 0f : now - state.OutOfCombatSince);
            if (over && !state.FightEnded)
            {
                state.FightEnded = true;
                state.Clicked = null;
                state.LastForced = null;
                GroupAiFocus.EndFight(pair.Key);
            }
            else if (inCombat) state.FightEnded = false;
            if (down || info == null) continue;
            var forced = Forced(state.Unit, out var taunted);
            // A new forced target without a taunt is a click on a foe: a player's action and her focus to come.
            if (forced != null && !taunted && forced != state.LastForced)
            {
                state.LastPlayerOrder = now;
                state.Clicked = forced;
                state.QuietReason = "click forced=" + forced;
                GroupAiPolicy.Log(GroupAiRules.StanceOrderLine(pair.Key, "player", "click", forced, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
            }
            state.LastForced = forced;
            var quiet = GroupAiRules.StancePlayerQuiet(now, state.LastPlayerOrder);
            if (GroupAiRules.StanceReleaseClick(quiet, forced, state.Clicked, taunted))
            {
                info.SetForceAttackTarget(null);
                state.LastForced = null;
                GroupAiFocus.SetClick(pair.Key, state.Clicked);
            }
        }
    }

    // The forced target's GUID (null for none) and whether an enemy's taunt (AFFECTER_Taunt) lies on her.
    private static string Forced(UnitEntity unit, out bool taunted)
    {
        var forced = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetForceAttackTarget();
        taunted = forced != null && CoreGame.Affecters(unit)?.Has(GroupAiRules.TauntKey) == true;
        return forced?.Guid.ToString();
    }

    // The longest reach among her skills on the bar, for an attack on a taunter.
    private static int MaxReach(GroupAiSituation s)
    {
        var reach = 0;
        foreach (var slot in s.Slots)
            if (slot?.Data != null) reach = Math.Max(reach, slot.Data.Range);
        return reach;
    }

    // A comrade in the author's emergency (GroupAiRules.HealerEmergency), for the healer's priority casts (GroupAiPriority).
    internal static bool AllyInEmergency(GroupAiSituation s)
    {
        foreach (var ally in s.Allies)
            if (ally.Unit != null && !ally.Unit.IsDead && Emergency(ally.Unit)) return true;
        return false;
    }

    private static bool Emergency(UnitEntity unit)
        => GroupAiRules.HealerEmergency(CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.HealthHead),
            CoreGame.Stat(unit, StatType.MaxHealthHead), Wounds.Buffer(CoreGame.GuidOf(unit)), CoreGame.Stat(unit, StatType.MaxHealthTotal));

    // The reach of her free heals that can be cast at a comrade's place: Lifebloom and the seed.
    private static int HealReach(GroupAiSituation s)
    {
        var reach = 0;
        foreach (var key in new[] { LifebloomRules.SkillKey, SeedSkill })
        {
            var slot = s.Find(key);
            if (slot?.Data != null && slot.Free) reach = Math.Max(reach, slot.Data.Range);
        }
        return reach;
    }

    private static bool IsHealRule(string rule)
        => rule != null && rule.StartsWith("heal-", StringComparison.Ordinal) && !rule.StartsWith("heal-damage", StringComparison.Ordinal)
           && rule != "heal-wait";

    private static bool IsMovable(GroupAiSituation s, int x, int y)
    {
        try
        {
            var tile = new Vector2Int(x, y);
            return s.InMap.GetNearMovablePosition(tile) == tile;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // CORE_AI_STANCE on a change of mode and on every order, with her share of health at that moment.
    private static void Note(GroupAiSituation s, State state, string mode, int nearest, string to, string reason)
    {
        if (to == null && state.Mode == mode) return;
        state.Mode = to == null ? mode : "";
        GroupAiPolicy.Log(GroupAiRules.StanceLine(s.Guid, mode, nearest, to, reason, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0, s.Self.Share));
    }
}
