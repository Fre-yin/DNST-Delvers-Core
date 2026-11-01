using HarmonyLib;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using SetSkillMoveTarget = global::Refactor.Task.SetSkillMoveTarget;
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
using SetRandomTarget = global::Refactor.Task.SetRandomTarget;
using IdleTask = global::Refactor.Task.IdleTask;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using SetSkillMoveTarget = Il2CppRefactor.Task.SetSkillMoveTarget;
using ActCombatBehaviourTask = Il2CppRefactor.Task.ActCombatBehaviourTask;
using SetRandomTarget = Il2CppRefactor.Task.SetRandomTarget;
using IdleTask = Il2CppRefactor.Task.IdleTask;
#endif

namespace DungeonSettlersDelvers.Core;

// The tactical formation in the dungeon, the group AI's part (GroupAiFormationRules). Every LookSeconds of game time, with a
// formation saved for the expedition (TacticalFormation.TryGet), the formation turns with the leader's walk
// (TacticalFormationRules.Next) and each member's place is found on the map (TacticalFormationRules.Place, a cell the game can
// walk on). A member with the group AI's gate open (GroupAiGate: auto attack, not hold position) and no player's order of the last
// seconds (GroupAiStance.PlayerQuiet) is sent there with the group AI's move order (GroupAiZones.OrderMove): out of combat to
// follow, at a fight's start at once, in the fight back again once nothing has attacked it for ReturnQuietSeconds: an enemy's damage
// execution on it, hit or not (missed, dodged, blocked, taken by a shield; GroupAiPartyHits passes every one on), or a fighting
// enemy with it as its target, read once per look. In the fight a held
// member strikes only what is in reach of its place: its target choice prefers a foe in reach (GroupAiDecideTargetPatch calls
// Retarget, only with the full group AI) and the game's move toward a target out of reach is cancelled (GroupAiFormationHoldPatch).
// The formation runs without the full
// group AI too and on every difficulty; then it only follows, takes the places and holds them, and the game picks the targets.
// A member whose formation the player switched off (TacticalFormation.IsOn) takes no part: its place stays empty, its cell taken.
// CORE_AI_FORMATION per order.
internal static class GroupAiFormation
{
    private static float nextLook;
    private static TacticalFormationRules.Turn turn = new(TacticalFormationRules.Facing.North);
    private static Vector2Int? lastLeader;
    // The leader's walk since it last stood (GroupAiFormationRules.Track), so the game's path wiggling between neighbouring cells
    // does not reset it (a walk south of (1,-2), (-1,-1), (0,-2) kept facing north).
    private static GroupAiFormationRules.Walk walk;
    private static bool fight;
    private static readonly Dictionary<Guid, (int X, int Y)> ordered = new();
    private static readonly Dictionary<Guid, float> orderedAt = new();
    // Where each member stood when its last order went out, and how many orders in a row ended there without a step.
    private static readonly Dictionary<Guid, (int X, int Y)> orderedFrom = new();
    private static readonly Dictionary<Guid, int> unmoved = new();
    private static readonly Dictionary<Guid, float> lastHit = new();
    private static readonly HashSet<Guid> held = new();
    // The leader and every member with a place in the formation that applies now; idle members of it stay put (StaysStill).
    private static readonly HashSet<Guid> members = new();
    private static readonly HashSet<Guid> idleLogged = new();
    private static readonly HashSet<Guid> freeLogged = new();

    // A formation applies on the dungeon map (last look); the player's orders are then watched even without the group AI.
    internal static bool Active { get; private set; }

    internal static void Reset()
    {
        nextLook = 0f;
        turn = new TacticalFormationRules.Turn(TacticalFormationRules.Facing.North);
        lastLeader = null;
        walk = default;
        fight = false;
        Active = false;
        ordered.Clear();
        orderedAt.Clear();
        orderedFrom.Clear();
        unmoved.Clear();
        lastHit.Clear();
        held.Clear();
        members.Clear();
        idleLogged.Clear();
        freeLogged.Clear();
    }

    internal static void OnGameTick()
    {
        var now = GameClock.Now;
        if (now < nextLook) return;
        nextLook = now + GroupAiFormationRules.LookSeconds;
        held.Clear();
        members.Clear();
        Active = false;
        // The comfort option tactical formation (QolText) off: no follow, no hold, no idle block; saved formations stay.
        if (!CoreSettings.TacticalEnabled || !TacticalFormation.InDungeon) return;
        var party = TacticalFormation.MapMembers();
        if (party.Count < 2 || !TacticalFormation.TryGet(TacticalFormation.Guids(party), out var leaderGuid, out var slots)) return;
        Active = true;
        members.Add(leaderGuid);
        foreach (var slot in slots) members.Add(slot.Unit);
        var leader = party.Find(unit => CoreGame.GuidOf(unit) == leaderGuid);
        var inMap = CoreGame.InMap;
        if (leader == null || inMap == null) return;
        // The crown holds too: the whole group waits for the player's input and strikes what comes into reach (the leader
        // walked toward foes still out of reach and the others followed).
        if (Keeps(leader)) held.Add(leaderGuid);
        var at = CoreGame.Position(leader);
        // Only a look on which the leader moved turns the formation: a walk of one cell a second shows a standstill every other
        // look, and Next forgets a pending turn on a standstill (six cells south, still facing north).
        // A single cell is no walk: the game's last correcting step at the goal turned the formation (north
        // walk, facing southeast at the end).
        var (track, moved) = GroupAiFormationRules.Track(walk, (at.x, at.y), lastLeader.HasValue ? (lastLeader.Value.x, lastLeader.Value.y) : null);
        walk = track;
        if (moved is { } step)
        {
            var facing = turn.Facing;
            turn = TacticalFormationRules.Next(turn, step.Dx, step.Dy, now);
            if (turn.Facing != facing) walk = GroupAiFormationRules.Restart((at.x, at.y));
        }
        lastLeader = at;
        var nowFight = party.Exists(InCombat);
        var starts = nowFight && !fight;
        fight = nowFight;
        // A fight's start sends every member to its place again, even where it was sent before.
        if (starts) ordered.Clear();
        if (fight) NoteTargeted(party, now);
        // A cell a member that will move stands on counts as free: the game's movable check sees the member itself (two members
        // went back and forth between neighbouring cells every look). A member that stays (no auto attack,
        // hold position, a player's order, its formation switched off) and the leader keep their cells taken.
        var standing = new HashSet<(int, int)>();
        foreach (var member in party)
            if (GroupAiFormationRules.FreesCell(CoreGame.GuidOf(member) == leaderGuid, Keeps(member))) { var p = CoreGame.Position(member); standing.Add((p.x, p.y)); }
        var places = TacticalFormationRules.Place(at.x, at.y, turn.Facing, slots,
            (x, y) => { var cell = new Vector2Int(x, y); return standing.Contains((x, y)) || inMap.GetNearMovablePosition(cell) == cell; });
        for (var i = 0; i < slots.Count; i++)
        {
            var unit = party.Find(member => CoreGame.GuidOf(member) == slots[i].Unit);
            // A member staying in a seed is not sent to its place (GroupAiSeedStay).
            if (unit == null || !Keeps(unit) || GroupAiSeedStay.Staying(unit)) continue;
            var guid = slots[i].Unit;
            // Held whether or not a free cell was found for it this look, and already before the game counts the fight: the healer's
            // swing and the ranged reach step act on foes in sight, which comes before the combat state (the
            // healer stepped back at full health, members walked toward foes still more than 4 cells away).
            held.Add(guid);
            if (places[i] == null) continue;
            var why = GroupAiFormationRules.Why(fight, starts, now, lastHit.TryGetValue(guid, out var hit) ? hit : float.NegativeInfinity);
            var place = places[i].Value;
            var stands = CoreGame.Position(unit);
            var since = orderedAt.TryGetValue(guid, out var sentAt) ? now - sentAt : float.PositiveInfinity;
            if (why == null || !GroupAiFormationRules.NeedsOrder((stands.x, stands.y), place, ordered.TryGetValue(guid, out var sent) ? sent : null,
                    GroupAiMoveWatch.Holds(unit), since)) continue;
            var from = (stands.x, stands.y);
            var stuck = GroupAiFormationRules.Unmoved(unmoved.TryGetValue(guid, out var count) ? count : 0, GroupAiMoveWatch.LastUnmoved(unit.Guid.ToString()),
                from, orderedFrom.TryGetValue(guid, out var before) ? before : null);
            orderedFrom[guid] = from;
            if (stuck >= GroupAiFormationRules.AsideAfter && StepAside(unit, guid, from, place, stuck, inMap, now)) continue;
            unmoved[guid] = stuck;
            if (!GroupAiZones.OrderMove(unit, new Vector2Int(place.X, place.Y), "formation")) continue;
            ordered[guid] = place;
            orderedAt[guid] = now;
            GroupAiPolicy.Log(GroupAiFormationRules.Line(guid.ToString("D"), why.Value, place.X, place.Y, turn.Facing.ToString().ToLowerInvariant()));
        }
    }

    // The member steps to the free neighbour nearest its place; its place is ordered again ReorderSeconds later, from there.
    private static bool StepAside(UnitEntity unit, Guid guid, (int X, int Y) from, (int X, int Y) place, int stuck, IInMapDataSupplier inMap, float now)
    {
        var aside = GroupAiFormationRules.Aside(from, place, (x, y) => { var cell = new Vector2Int(x, y); return inMap.GetNearMovablePosition(cell) == cell; });
        if (aside == null || !GroupAiZones.OrderMove(unit, new Vector2Int(aside.Value.X, aside.Value.Y), "formation-aside")) return false;
        unmoved[guid] = 0;
        ordered[guid] = place;
        orderedAt[guid] = now;
        GroupAiPolicy.Log(GroupAiFormationRules.AsideLine(guid.ToString("D"), from, aside.Value, place, stuck));
        return true;
    }

    // While the formation is on, idle wandering is blocked as with hold position. An idle member of the
    // formation that applies in the dungeon, the leader too, does not wander (GroupAiFormationIdlePatch); one switched off
    // wanders as before. The game's hold position bit stays untouched.
    internal static bool StaysStill(UnitEntity unit)
    {
        if (!Active || unit == null) return false;
        var guid = CoreGame.GuidOf(unit);
        return GroupAiFormationRules.StaysStill(members.Contains(guid), TacticalFormation.IsOn(guid));
    }

    // CORE_AI_FORMATION_IDLE once per member while the formation applies.
    internal static void NoteIdle(UnitEntity unit, Vector2Int cell)
    {
        var guid = CoreGame.GuidOf(unit);
        freeLogged.Remove(guid);
        if (idleLogged.Add(guid)) GroupAiPolicy.Log(GroupAiFormationRules.IdleLine(guid.ToString("D"), cell.x, cell.y));
    }

    // CORE_AI_FORMATION_IDLE_FREE once each time a member of the formation whose formation is switched off reaches the game's idle
    // wandering and is let through (a switched off member stood still for 90 s, so a run shows whether the
    // game's wandering node was reached at all).
    internal static void NoteFree(UnitEntity unit, IInMapDataSupplier inMap)
    {
        if (!Active || unit == null || inMap == null) return;
        var guid = CoreGame.GuidOf(unit);
        if (!members.Contains(guid) || TacticalFormation.IsOn(guid)) return;
        idleLogged.Remove(guid);
        if (!freeLogged.Add(guid)) return;
        var cell = inMap.GetLogicalPosition(unit.Guid);
        GroupAiPolicy.Log($"CORE_AI_FORMATION_IDLE_FREE unit={guid:D} cell={cell.x},{cell.y}");
    }

    // A member of the formation without a player's order: it keeps its place and fights from there.
    // Held on its place in the formation, or in the aura of the seed it stays at (GroupAiSeedStay).
    internal static bool Holds(UnitEntity unit)
        => unit != null && ((held.Contains(CoreGame.GuidOf(unit)) && Keeps(unit)) || GroupAiSeedStay.Holds(unit));

    // The target a held member strikes: the chosen one when in reach, else the nearest foe in reach; null leaves the choice as it is.
    internal static IEntity Retarget(UnitEntity owner, ActiveSkill_Base skill, IEntity target, IInMapDataSupplier inMap)
    {
        if (!Holds(owner) || skill == null || target == null || inMap == null || target.Guid == owner.Guid) return null;
        var range = DataSheetManager.Instance?.GetSkillData(skill.Key)?.Range ?? 1;
        var from = inMap.GetLogicalPosition(owner.Guid);
        var to = inMap.GetLogicalPosition(target.Guid);
        if (target.Faction == owner.Faction || GroupAiFormationRules.InReach(from.x, from.y, to.x, to.y, range)) return null;
        var foes = new List<UnitEntity>();
        var spots = new List<(int X, int Y)>();
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit == null || unit.IsDead || unit.Faction != FactionType.Enemy) continue;
            var spot = inMap.GetLogicalPosition(unit.Guid);
            foes.Add(unit);
            spots.Add((spot.x, spot.y));
        }
        var nearest = GroupAiZoneRules.NearestInReach(from.x, from.y, range, spots);
        return nearest < 0 ? null : foes[nearest].Cast<IEntity>();
    }

    // The game's move toward the chosen target (the skill task of SetSkillMoveTarget), cancelled for a held member when the target
    // is out of its skill's reach.
    internal static bool CancelsMove(UnitEntity owner, ActCombatBehaviourTask task)
    {
        if (task == null || !Holds(owner)) return false;
        var target = task.TargetEntity;
        var skill = task.Skill;
        var inMap = CoreGame.InMap;
        if (target == null || skill == null || inMap == null) return false;
        var range = DataSheetManager.Instance?.GetSkillData(skill.Key)?.Range ?? 1;
        var from = inMap.GetLogicalPosition(owner.Guid);
        var to = inMap.GetLogicalPosition(target.Guid);
        return !GroupAiFormationRules.InReach(from.x, from.y, to.x, to.y, range);
    }

    // A foe within range of where the unit stands (GroupAiPriority: a held member's priority cast needs one).
    internal static bool FoeInReach(UnitEntity owner, int range)
    {
        var inMap = CoreGame.InMap;
        if (owner == null || inMap == null) return false;
        var from = inMap.GetLogicalPosition(owner.Guid);
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit == null || unit.IsDead || unit.Faction != FactionType.Enemy) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            if (GroupAiFormationRules.InReach(from.x, from.y, at.x, at.y, range)) return true;
        }
        return false;
    }

    private static bool InCombat(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;

    // Free to take its place: alive, the group AI's gate open, no player's order of the last seconds.
    private static bool Free(UnitEntity unit) => !unit.IsDead && GroupAiGate.OpenFor(unit) && !GroupAiStance.PlayerQuiet(CoreGame.GuidOf(unit).ToString("D"));

    // Free, its formation switched on and standing: it follows, holds and returns to its place.
    private static bool Keeps(UnitEntity unit)
        => GroupAiFormationRules.Keeps(Free(unit), TacticalFormation.IsOn(CoreGame.GuidOf(unit)),
            WoundRules.IsLying(CoreGame.Affecters(unit)?.Has(ReviveSnapshots.DeadlyKey) == true, GroupAiBandits.IsDowned(unit)));

    // An enemy's damage execution on a party unit, whatever it did (GroupAiPartyHits.OnDamage).
    internal static void OnAttacked(UnitEntity target)
    {
        if (target != null) lastHit[CoreGame.GuidOf(target)] = GameClock.Now;
    }

    // A fighting enemy with a member as its target counts as attacking it.
    private static void NoteTargeted(List<UnitEntity> party, float now)
    {
        var guids = new HashSet<Guid>(party.ConvertAll(CoreGame.GuidOf));
        foreach (var enemy in CoreGame.MapUnits())
        {
            if (enemy == null || enemy.IsDead || enemy.Faction != FactionType.Enemy || !InCombat(enemy)) continue;
            var target = CoreGame.Component<CombatInfoComponent>(enemy.Cast<IEntity>())?.GetCombatBehaviour()?.TargetEntity?.TryCast<UnitEntity>();
            var guid = target == null ? Guid.Empty : CoreGame.GuidOf(target);
            if (guids.Contains(guid)) lastHit[guid] = now;
        }
    }
}

// The game's idle wandering (the tree node SetRandomTarget, "idle only: a random tile in range", which sets IdleTask's target
// to a random cell of GetGridsInDistance): a member of the formation, or a unit of the running formation test
// (TacticalFormationTest.Holds), keeps its own cell as the idle target instead.
[HarmonyPatch(typeof(SetRandomTarget), "SetTarget")]
internal static class GroupAiFormationIdlePatch
{
    private static int failures;

    private static bool Prefix(SetRandomTarget __instance)
    {
        try
        {
            var owner = __instance._owner?.TryCast<UnitEntity>();
            if (owner == null) return true;
            var formation = GroupAiFormation.StaysStill(owner);
            if (!formation && !TacticalFormationTest.Holds(owner))
            {
                GroupAiFormation.NoteFree(owner, __instance._inMapData);
                return true;
            }
            var idle = __instance._task?.TryCast<IdleTask>();
            var inMap = __instance._inMapData;
            if (idle == null || inMap == null) return true;
            var cell = inMap.GetLogicalPosition(owner.Guid);
            idle.SetTargetPosition(cell);
            if (formation) GroupAiFormation.NoteIdle(owner, cell);
            return false;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_FORMATION_IDLE_FAILED: " + ex.Message);
            return true;
        }
    }
}

// The hold at the game's skill move step, like GroupAiZoneHoldPatch: a held member does not walk toward a target out of reach.
[HarmonyPatch(typeof(SetSkillMoveTarget), "ShouldCancelSkill")]
internal static class GroupAiFormationHoldPatch
{
    private static int failures;

    private static void Postfix(SetSkillMoveTarget __instance, ref bool __result)
    {
        if (__result) return;
        try
        {
            var owner = __instance._owner?.TryCast<UnitEntity>();
            if (owner != null && GroupAiFormation.CancelsMove(owner, __instance._task?.TryCast<ActCombatBehaviourTask>())) __result = true;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_FORMATION_HOLD_FAILED: " + ex.Message);
        }
    }
}
