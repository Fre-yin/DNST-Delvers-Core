using HarmonyLib;
using UnityEngine;
#if BEPINEX
using MoveTask = global::Refactor.Task.MoveTask;
using VectorExtensions = global::Refactor.VectorExtensions;
using TaskBase = global::Refactor.Task.Task_Base;
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using SetSkillMoveTarget = global::Refactor.Task.SetSkillMoveTarget;
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
using SetRandomTarget = global::Refactor.Task.SetRandomTarget;
using IdleTask = global::Refactor.Task.IdleTask;
#else
using MoveTask = Il2CppRefactor.Task.MoveTask;
using VectorExtensions = Il2CppRefactor.VectorExtensions;
using TaskBase = Il2CppRefactor.Task.Task_Base;
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
// walk on). A member not waiting for orders (GroupAiFormationRules.OpenToFollow; auto attack off follows too) and no player's order of the last
// seconds (GroupAiStance.PlayerQuiet) is sent there with the group AI's move order (GroupAiZones.OrderMove): out of combat to
// follow, at a fight's start at once, in the fight back again once nothing has attacked it for ReturnQuietSeconds: an enemy's damage
// execution on it, hit or not (missed, dodged, blocked, taken by a shield; GroupAiPartyHits passes every one on), or a fighting
// enemy with it as its target, read once per look. In the fight a held
// member strikes only what is in reach of its place: its target choice prefers a foe in reach (GroupAiDecideTargetPatch calls
// Retarget, only with the full group AI) and the game's move toward a target out of reach is cancelled (GroupAiFormationHoldPatch).
// (Tac is only a simple version that improves the AI a little): the formation runs without the full
// group AI too and on every difficulty; then it only follows, takes the places and holds them, and the game picks the targets.
// A member whose formation the player switched off (TacticalFormation.IsOn) takes no part: its place stays empty, its cell taken.
// The player's orders go first: a group's order moves the places, a member's own walk is never
// cut, and while a member's attack order stands (GroupAiChase.HasOrder) the formation neither holds nor fetches it back; on a
// foe out of reach it is placed by hand (NoteClicks). The rules of the look are in GroupAiFormationRules (reach, goal, hands,
// constants); OnGameTick reads the game and sends the members to their places (SendToPlaces). CORE_AI_FORMATION per order.
internal static class GroupAiFormation
{
    private static float nextLook;
    private static TacticalFormationRules.Turn turn = new(TacticalFormationRules.Facing.North);
    private static Vector2Int? lastLeader;
    // The leader's walk since it last stood (GroupAiFormationRules.Track), so the game's path wiggling between neighbouring cells
    // does not reset it (a walk south of (1,-2), (-1,-1), (0,-2) kept facing north).
    private static GroupAiFormationRules.Walk walk;
    // The leader's cells since it last stood (StillLooks), for the direction where it stops (TacticalFormationRules.StopFacing).
    private static readonly List<(int X, int Y)> trail = new();
    // A sliding window of GroupAiFormationRules.TrailCap cells.
    // The player's move orders (the player's order goes first, the formation is kept, the player moves the
    // formation). Nothing of it is saved; it resets with ordered.
    //   leaderGoal: the cell a group's order sends the leader to, and the direction there; while the leader walks there, the places
    //     stand around it. An order for the leader alone sets none: the others follow as before.
    //   planned: the cells a group's order gave its members (TacticalFormationMove), taken as their orders; groupOrdered: members
    //     whose last player's order was the group's, so they need no quiet.
    //   anchors: members moved without the leader to a click outside the grid stand there, as if the leader stood at the click,
    //     while the leader stays outside the grid around it (while the leader stays and moves outside the tac
    //     grid, the others do not move); one per member, a group's order with the leader clears
    //     them all.
    //   placedByHand (the formation breaks only where the player moves one member away alone in a fight):
    //     a member with its formation on given a move order alone in a fight, or a click on a foe out of its reach (always a fight),
    //     stays where it goes until the player moves it with a group or the fight ends; then it returns after StanceQuietSeconds
    //     (handQuiet). A click on a foe in reach breaks nothing; an order only for the leader lets nobody go.
    private static (int X, int Y)? leaderGoal;
    private static TacticalFormationRules.Facing? goalFacing;
    // Where the leader stood when it got the goal, and when: the goal holds until it reaches it or stops after moving, and goes
    // after GroupAiFormationRules.GoalWaitSeconds when it never moved (GoalDone).
    private static (int X, int Y) goalFrom;
    private static float goalSetAt;
    // The corner, step 1 (the group goes round a corner and stands at the goal right after the last stretch,
    // without catching up afterwards): after a group's
    // order with the leader the next looks read the leader's path once the game has it (MoveSystem.GetPath, only read) and take the
    // way of its last stretch to the goal (TacticalFormationRules.StopFacing); behind a corner that differs from the straight line,
    // and the members are sent once to the places turned that way, so the formation does not turn again at the goal.
    private static bool pathPending;
    private static int pathLooks;
    // The map of the last look: the cells above belong to it (NewMap).
    private static IntPtr lastMap;
    private static Guid lastLeaderGuid;
    private static readonly Dictionary<Guid, ((int X, int Y) Cell, float At)> planned = new();
    // Since when a member placed by hand has stood without a fight (GroupAiFormationRules.IdleHandRelease).
    private static readonly Dictionary<Guid, float> handNoFight = new();
    // The foe each member placed by a click was sent at: GroupAiStance lets the game's forced target go after its quiet, so
    // the forced target is no sign the foe is gone (let go as foe-gone 3 s after the click, the foe alive).
    private static readonly Dictionary<Guid, Guid> handFoe = new();
    // Shift waypoints (with Tac on, a waypoint walk is a walk in formation): the cells the game queued as a member's waypoints
    // (TacticalFormationMove, WayPointContainer.AddWayPoint), and the members walking them. Such an order is a run in formation,
    // never placed by hand, without the quiet; the formation takes its places again after the last waypoint.
    private static readonly Dictionary<Guid, HashSet<(int X, int Y)>> waypointCells = new();
    private static readonly HashSet<Guid> waypointRun = new();
    private static readonly HashSet<Guid> groupOrdered = new();
    private static readonly Dictionary<Guid, ((int X, int Y) Cell, TacticalFormationRules.Facing Facing)> anchors = new();
    private static readonly HashSet<Guid> placedByHand = new();
    private static readonly Dictionary<Guid, float> handQuiet = new();
    private static readonly Dictionary<Guid, string> lastOrdered = new();
    private static bool fight;
    // In a fight the places stay around the cell and the way the leader had when it began (in a fight the
    // formation does not move along). The leader's own steps leave them where they are: the game's, a dodge, a tank gathering, a
    // damage dealer gone to the foes. Only a player's order moves them with it (a group's order, an order or the waypoints of the
    // leader), and they stay where that walk ends. After the fight the places take their bearings from the leader again.
    private static ((int X, int Y) Cell, TacticalFormationRules.Facing Facing)? fightAnchor;
    // The cell of the player's last order for the leader alone (OnPlayerOrder), while its move runs.
    private static (int X, int Y)? leaderOrder;
    // The cell of a member's own single order from the player while its walk runs: the player's order goes first and is never cut
    // (a member sent to a bridge was called back after 3 s,
    // CORE_AI_FORMATION_PULLED). The quiet of StanceQuietSeconds starts only when that walk ends, on arrival or when it breaks off
    // (handQuiet). A group's order or waypoints clear it.
    // With the time of the order: it counts from then on, before the game's brain has taken the task (PlanSeconds, as the leader's).
    private static readonly Dictionary<Guid, ((int X, int Y) Cell, float At)> memberOrders = new();
    private static float leaderOrderAt;
    private static readonly Dictionary<Guid, (int X, int Y)> ordered = new();
    private static readonly Dictionary<Guid, float> orderedAt = new();
    // The place aside a member was sent to while its slot was taken, with the anchor and direction it was placed by (HoldAside).
    private static readonly Dictionary<Guid, ((int X, int Y) Anchor, TacticalFormationRules.Facing Facing, (int X, int Y) Place)> asides = new();
    // Where each member stood when its last order went out, and how many orders in a row ended there without a step.
    private static readonly Dictionary<Guid, (int X, int Y)> orderedFrom = new();
    private static readonly Dictionary<Guid, int> unmoved = new();
    private static readonly Dictionary<Guid, float> lastHit = new();
    private static readonly HashSet<Guid> held = new();
    // The leader and every member with a place in the formation that applies now; idle members of it stay put (StaysStill).
    private static readonly HashSet<Guid> members = new();
    private static readonly HashSet<Guid> idleLogged = new();
    private static readonly HashSet<Guid> freeLogged = new();
    // The last CORE_AI_FORMATION_SKIP reason per member (NoteSkip), and the lines written.
    private static readonly Dictionary<Guid, string> skipped = new();
    // The player's ordered tasks that are no plain walk, per member: the task, its type name and the order's moment (busy).
    private static readonly Dictionary<Guid, (IntPtr Task, string Type, float At)> busy = new();
    private static int skipLines;
    private const int SkipLogCap = 200;

    // A formation applies on the dungeon map (last look); the player's orders are then watched even without the group AI.
    internal static bool Active { get; private set; }
    // The player's roles of the formation that applies, the leader, the cell the places stand around and each
    // member's place, as of the last look (GroupAiRoles.PlayerOf, GroupAiRoleMoves, the support's seed).
    private static Dictionary<Guid, TacticalRole> roles = new();
    private static readonly Dictionary<Guid, (int X, int Y)> lastPlaces = new();
    internal static Guid Leader => lastLeaderGuid;
    internal static (int X, int Y) Center { get; private set; }

    internal static TacticalRole RoleOf(Guid unit) => Active && roles.TryGetValue(unit, out var role) ? role : TacticalRole.Neutral;

    internal static (int X, int Y)? PlaceOf(Guid unit) => Active && lastPlaces.TryGetValue(unit, out var place) ? place : null;

    internal static void Reset()
    {
        nextLook = 0f;
        turn = new TacticalFormationRules.Turn(TacticalFormationRules.Facing.North);
        lastLeader = null;
        walk = default;
        trail.Clear();
        leaderGoal = null;
        goalFacing = null;
        lastMap = IntPtr.Zero;
        lastLeaderGuid = Guid.Empty;
        planned.Clear();
        groupOrdered.Clear();
        anchors.Clear();
        placedByHand.Clear();
        handQuiet.Clear();
        handNoFight.Clear();
        handFoe.Clear();
        waypointCells.Clear();
        waypointRun.Clear();
        lastOrdered.Clear();
        fight = false;
        fightAnchor = null;
        leaderOrder = null;
        memberOrders.Clear();
        Active = false;
        roles = new Dictionary<Guid, TacticalRole>();
        lastPlaces.Clear();
        ordered.Clear();
        orderedAt.Clear();
        asides.Clear();
        orderedFrom.Clear();
        unmoved.Clear();
        lastHit.Clear();
        held.Clear();
        members.Clear();
        idleLogged.Clear();
        freeLogged.Clear();
        skipped.Clear();
        busy.Clear();
        skipLines = 0;
    }

    // A new map (the next floor): the anchors, the leader's goal and its walk are cells of the last map and go; a look on the
    // same map again (back from the base) keeps them.
    private static void NewMap(IntPtr map)
    {
        if (map == lastMap) return;
        var known = lastMap != IntPtr.Zero;
        lastMap = map;
        if (!known) return;
        anchors.Clear();
        leaderGoal = null;
        goalFacing = null;
        pathPending = false;
        lastLeader = null;
        walk = default;
        trail.Clear();
        waypointCells.Clear();
        waypointRun.Clear();
    }

    internal static void OnGameTick()
    {
        var now = GameClock.Now;
        if (now < nextLook) return;
        nextLook = now + GroupAiFormationRules.LookSeconds;
        held.Clear();
        members.Clear();
        lastPlaces.Clear();
        Active = false;
        TacticalFormationTempo.BeginLook();
        // The comfort option tactical formation (QolText) off: no follow, no hold, no idle block; saved formations stay.
        if (!CoreSettings.TacticalEnabled || !TacticalFormation.InDungeon)
        {
            GroupAiSummonWalk.Forget();
            return;
        }
        var party = TacticalFormation.MapMembers();
        if (party.Count < 2 || !TacticalFormation.TryGet(TacticalFormation.Guids(party), out var leaderGuid, out var slots))
        {
            GroupAiSummonWalk.Forget();
            return;
        }
        Active = true;
        roles = TacticalFormation.ActiveRoles();
        members.Add(leaderGuid);
        foreach (var slot in slots) members.Add(slot.Unit);
        var leader = party.Find(unit => CoreGame.GuidOf(unit) == leaderGuid);
        lastLeaderGuid = leaderGuid;
        var inMap = CoreGame.InMap;
        if (leader == null || inMap == null) return;
        NewMap(inMap.Pointer);
        // The crown holds too: the whole group waits for the player's input and strikes what comes into reach (the leader walked
        // toward foes still out of reach and the others followed).
        // A leading tank out gathering is not held, and the formation neither turns nor follows it (GroupAiRoleMoves); in a fight it
        // follows only a player's order of the leader (fightAnchor).
        var gatherFrom = GroupAiRoleMoves.LeaderAnchor(leaderGuid);
        var playerLeads = PlayerLeads(leader, leaderGuid);
        var frozen = gatherFrom != null || (fightAnchor != null && !GroupAiFormationRules.PlacesFollow(fight, playerLeads));
        var at = CoreGame.Position(leader);
        // Only a look on which the leader moved turns the formation: a walk of one cell a second shows a standstill every other
        // look, and Next forgets a pending turn on a standstill (six cells south, still facing north).
        // A single cell is no walk: the game's last correcting step at the goal turned the formation (north
        // walk, facing southeast at the end).
        // That one cell rule holds only on the way: where the leader stops, the formation faces the way it walked, a single cell
        // too, once it stood StillLooks looks (StopTurn).
        if (!frozen)
        {
            var (track, moved) = GroupAiFormationRules.Track(walk, (at.x, at.y), lastLeader.HasValue ? (lastLeader.Value.x, lastLeader.Value.y) : null);
            walk = track;
            if (moved is { } step)
            {
                var facing = turn.Facing;
                turn = TacticalFormationRules.Next(turn, step.Dx, step.Dy, now);
                if (turn.Facing != facing) walk = GroupAiFormationRules.Restart((at.x, at.y));
            }
            StopTurn(party, at, inMap);
        }
        else walk = GroupAiFormationRules.Restart((at.x, at.y));
        lastLeader = at;
        GroupAiFightStart.Look(party, now, LastHit);
        var nowFight = party.Exists(InCombat);
        var starts = nowFight && !fight;
        var ended = TacticalFormationMoveRules.HandEnds(fight, nowFight);
        fight = nowFight;
        TacticalFormationTempo.Look(party, members, fight, Lying);
        GroupAiRoleMoves.Look(party, fight, now, LastHit);
        gatherFrom = GroupAiRoleMoves.LeaderAnchor(leaderGuid);
        // The crown holds unless it is out for its role: gathering as a tank, or gone to the foes as a damage dealer (also a
        // leader with the role damage goes after 3.5 s without a target; the places stay, fightAnchor).
        // A leader staying in a seed is neither held nor sent roaming, as the members (GroupAiSeedStay).
        if (Keeps(leader) && gatherFrom == null && !GroupAiSeedStay.Staying(leader) && !RoamsNow(leader, leaderGuid, now))
            held.Add(leaderGuid);
        if (ended)
        {
            EndHand(now);
            GroupAiFightStart.FightEnded();
        }
        NoteClicks(party, now);
        if (!fight) ReleaseIdleClicks(now);
        else handNoFight.Clear();
        DropAnchors(at);
        // The leader's goal holds until it stands there or stood still StillLooks looks (GroupAiFormationRules.GoalDone).
        if (leaderGoal is { } reached && GroupAiFormationRules.GoalDone((at.x, at.y), reached, goalFrom, walk.Still, now - goalSetAt))
        {
            // At the goal the formation keeps the way of the plan or of the leader's path: it is the direction where the leader
            // stops, and the trail starts again, so no stop turn follows (the crown stood on its goal,
            // the places fell back to the walk's direction for a look and turned again at the stop).
            if ((at.x, at.y) == reached && goalFacing is { } kept)
            {
                turn = new TacticalFormationRules.Turn(kept);
                trail.Clear();
            }
            leaderGoal = null;
            goalFacing = null;
            pathPending = false;
        }
        if (pathPending && leaderGoal != null) ReadLeaderPath(leader, party, inMap);
        var follow = leaderGoal ?? (at.x, at.y);
        var followFacing = leaderGoal != null && goalFacing is { } way ? way : turn.Facing;
        if (!fight) fightAnchor = null;
        else if (fightAnchor == null || GroupAiFormationRules.PlacesFollow(fight, PlayerLeads(leader, leaderGuid))) fightAnchor = (follow, followFacing);
        var anchor = gatherFrom ?? fightAnchor?.Cell ?? follow;
        Center = anchor;
        var anchorFacing = fightAnchor?.Facing ?? followFacing;
        // A fight's start sends every member to its place again, even where it was sent before.
        if (starts) ordered.Clear();
        if (fight) NoteTargeted(party, now);
        // A cell a member that will move stands on counts as free: the game's movable check sees the member itself (two
        // members went back and forth between neighbouring cells every look). A member that stays (waiting for
        // orders, a player's order, its formation switched off) and the leader keep their cells taken; the leader's own cell
        // counts as free while it walks to a group order's goal (the crown on its way through a
        // member's place moved that place aside for a look, CORE_AI_FORMATION_SLOT_TAKEN by=leader).
        var standing = new HashSet<(int, int)>();
        foreach (var member in party)
            if (GroupAiFormationRules.FreesCell(CoreGame.GuidOf(member) == leaderGuid, Keeps(member), leaderGoal != null)) { var p = CoreGame.Position(member); standing.Add((p.x, p.y)); }
        // A summon with a place walks to it (GroupAiSummonWalk), so its own cell does not push its place aside.
        foreach (var slot in slots)
            if (party.TrueForAll(member => CoreGame.GuidOf(member) != slot.Unit) && CoreGame.FindUnit(slot.Unit) is { IsDead: false } summon)
            {
                var p = CoreGame.Position(summon);
                standing.Add((p.x, p.y));
            }
        var figures = Figures(standing, inMap);
        var places = GroupAiFormationRules.PlaceWithAnchors((anchor.Item1, anchor.Item2), anchorFacing, slots, anchors, Walkable(standing, inMap), figures);
        HoldAsides(slots, places, anchor, anchorFacing, Walkable(standing, inMap), figures);
        for (var i = 0; i < slots.Count; i++)
            if (places[i] is { } spot) lastPlaces[slots[i].Unit] = spot;
        // The summons on their places or beside the most hurt (GroupAiSummonWalk); out of a fight the water mages summon up to their most
        // again, each only while it stands (GroupAiSummonRefill).
        GroupAiSummonWalk.Look(slots, places, party, fight);
        SummonControl.Tally(now);
        GroupAiSummonRefill.Look(party, fight,
            unit => GroupAiFormationRules.CoreActs(Keeps(unit), GroupAiGate.OpenFor(unit)) && CurrentMoveTarget(unit) == null && !GroupAiMoveWatch.Holds(unit) && walk.Still >= GroupAiFormationRules.StillLooks);
        SendToPlaces(party, slots, places, leader, anchor, anchorFacing, starts, now, inMap);
    }

    // Each kept member of the formation to its place: held there, and sent with Core's move order when its place needs one
    // (GroupAiFormationRules.Why, NeedsOrder); a member that did not move off its cell is stepped aside first. CORE_AI_FORMATION per
    // order.
    private static void SendToPlaces(List<UnitEntity> party, IReadOnlyList<TacticalFormationRules.Slot> slots, (int X, int Y)?[] places,
        UnitEntity leader, (int X, int Y) anchor, TacticalFormationRules.Facing anchorFacing, bool starts, float now, IInMapDataSupplier inMap)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            var unit = party.Find(member => CoreGame.GuidOf(member) == slots[i].Unit);
            if (unit == null) continue;
            var guid = slots[i].Unit;
            // A member staying in a seed is not sent to its place (GroupAiSeedStay); a tank out gathering or a damage dealer gone to
            // the foes is neither held nor sent to its place (GroupAiRoleMoves).
            var skip = NotKept(unit) ?? (GroupAiSeedStay.Staying(unit) ? "seed" : null)
                ?? (GroupAiRoleMoves.Out(guid) || RoamsNow(unit, guid, now) ? "role" : null);
            if (skip != null)
            {
                NoteSkip(guid, skip);
                continue;
            }
            // Held whether or not a free cell was found for it this look, and already before the game counts the fight: the healer's
            // swing and the ranged reach step act on foes in sight, which comes before the combat state (the
            // healer stepped back at full health, members walked toward foes still more than 4 cells away).
            held.Add(guid);
            NoteSkip(guid, places[i] == null ? "noplace" : null);
            if (places[i] == null) continue;
            var why = GroupAiFormationRules.Why(fight, starts, now, lastHit.TryGetValue(guid, out var hit) ? hit : float.NegativeInfinity);
            var place = places[i].Value;
            var stands = CoreGame.Position(unit);
            var since = orderedAt.TryGetValue(guid, out var sentAt) ? now - sentAt : float.PositiveInfinity;
            if (why == null || !GroupAiFormationRules.NeedsOrder((stands.x, stands.y), place, SentCell(guid),
                    GroupAiMoveWatch.Holds(unit) || PlayerMoveRuns(unit, guid), since)) continue;
            var from = (stands.x, stands.y);
            var stuck = GroupAiFormationRules.Unmoved(unmoved.TryGetValue(guid, out var count) ? count : 0, GroupAiMoveWatch.LastUnmoved(unit.Guid.ToString()),
                from, orderedFrom.TryGetValue(guid, out var before) ? before : null);
            orderedFrom[guid] = from;
            if (stuck >= GroupAiFormationRules.AsideAfter && StepAside(unit, guid, from, place, stuck, inMap, now)) continue;
            unmoved[guid] = stuck;
            if (ordered.TryGetValue(guid, out var sentTo) && sentTo == place) LogPulled(unit, guid, place, from);
            if (!GroupAiZones.OrderMove(unit, new Vector2Int(place.X, place.Y), "formation")) continue;
            if (!anchors.ContainsKey(guid)) LogAside(guid, slots[i], anchor, anchorFacing, place, places, leader, party);
            ordered[guid] = place;
            orderedAt[guid] = now;
            NoteAside(guid, slots[i], anchor, anchorFacing, place);
            GroupAiPolicy.Log(GroupAiFormationRules.Line(guid.ToString("D"), why.Value, place.X, place.Y, anchorFacing.ToString().ToLowerInvariant()) + " tick=" + Tick());
        }
    }

    // A member sent to another cell than its slot names what took the slot: the goal itself, another member's place, the leader or a
    // member standing on it, or the map. CORE_AI_FORMATION_SLOT_TAKEN, only with an order going out.
    private static void LogAside(Guid guid, TacticalFormationRules.Slot slot, (int X, int Y) anchor, TacticalFormationRules.Facing facing,
        (int X, int Y) place, (int X, int Y)?[] places, UnitEntity leader, List<UnitEntity> party)
    {
        var cell = TacticalFormationRules.CellOf(anchor.X, anchor.Y, slot.X, slot.Y, facing);
        if (cell == place) return;
        var by = "map";
        var leaderAt = CoreGame.Position(leader);
        if (cell == anchor) by = "anchor";
        else if (Array.Exists(places, other => other == cell)) by = "place";
        else if ((leaderAt.x, leaderAt.y) == cell) by = "leader";
        else
            foreach (var member in party)
            {
                var p = CoreGame.Position(member);
                if ((p.x, p.y) == cell) { by = "member:" + CoreGame.GuidOf(member).ToString("D").Substring(0, 8); break; }
            }
        GroupAiPolicy.Log($"CORE_AI_FORMATION_SLOT_TAKEN unit={guid:D} slot={cell.X},{cell.Y} place={place.X},{place.Y} by={by} goal={(leaderGoal != null ? "on" : "off")} tick={Tick()}");
    }

    // A cell a living unit stands on, of any side, except the cells of units that will move away (as Walkable): what blocks such a
    // place is a figure, what blocks a place without one is the terrain (TacticalFormationRules.Place: terrain mirrors first, a figure
    // takes the nearest free cell first). The units are read once, at the first question.
    internal static Func<int, int, bool> Figures(HashSet<(int, int)> leaving, IInMapDataSupplier inMap)
    {
        HashSet<(int, int)> cells = null;
        return (x, y) =>
        {
            if (cells == null)
            {
                cells = new HashSet<(int, int)>();
                var units = inMap.GetAliveUnitsOnMap();
                for (var i = 0; units != null && i < units.Count; i++)
                {
                    var p = CoreGame.Position(units[i]);
                    if (!leaving.Contains((p.x, p.y))) cells.Add((p.x, p.y));
                }
            }
            return cells.Contains((x, y));
        };
    }

    // A cell a unit can take: the game's movable check, or a cell a unit that will move away stands on (its own cell reads as taken
    // to that check). The formation's places, the direction where the leader stops and the group's move order use it.
    internal static Func<int, int, bool> Walkable(HashSet<(int, int)> standing, IInMapDataSupplier inMap)
        => (x, y) =>
        {
            var cell = new Vector2Int(x, y);
            return standing.Contains((x, y)) || inMap.GetNearMovablePosition(cell) == cell;
        };

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;

    // A group's move order planned its members' cells (TacticalFormationMove): those are their orders now. With the leader in it the
    // leader's goal and direction are the click and the plan's way, and every anchor goes. Without the leader a click outside the
    // grid around the leader anchors the placed members there; a click inside it sets none, they stand around the leader.
    internal static void NotePlan(TacticalFormationMoveRules.Result plan, IReadOnlyDictionary<Guid, (int X, int Y)> cells, (int X, int Y) leaderAt)
    {
        if (plan == null) return;
        var now = GameClock.Now;
        foreach (var pair in cells) planned[pair.Key] = (pair.Value, now);
        if (plan.WithLeader)
        {
            trail.Clear();
            leaderGoal = plan.Click;
            goalFrom = leaderAt;
            goalSetAt = GameClock.Now;
            goalFacing = plan.Facing;
            pathPending = true;
            pathLooks = 0;
            foreach (var unit in anchors.Keys.ToList()) DropAnchor(unit, "group");
            return;
        }
        var anchored = TacticalFormationMoveRules.OutsideGrid(leaderAt, plan.Click);
        foreach (var unit in plan.Placed)
        {
            if (!anchored)
            {
                DropAnchor(unit, "inside");
                continue;
            }
            anchors[unit] = (plan.Click, plan.Facing);
            GroupAiPolicy.Log($"CORE_AI_FORMATION_ANCHOR unit={unit:D} on cell={plan.Click.X},{plan.Click.Y} facing={plan.Facing.ToString().ToLowerInvariant()} reason=order tick={Tick()}");
        }
    }

    private static void DropAnchor(Guid unit, string reason)
    {
        if (anchors.Remove(unit)) GroupAiPolicy.Log($"CORE_AI_FORMATION_ANCHOR unit={unit:D} off reason={reason} tick={Tick()}");
    }

    // The leader inside the grid around an anchor lets that anchor go: its members close up around the leader again.
    private static void DropAnchors(Vector2Int leaderAt)
    {
        foreach (var pair in anchors.ToList())
            if (!TacticalFormationMoveRules.OutsideGrid(pair.Value.Cell, (leaderAt.x, leaderAt.y))) DropAnchor(pair.Key, "leader");
    }

    // A member around the leader's anchor whose slot is still taken keeps the place aside it was sent to (GroupAiFormationRules.HoldAside):
    // open is walkable, without a figure and no other member's place this look.
    private static void HoldAsides(IReadOnlyList<TacticalFormationRules.Slot> slots, (int X, int Y)?[] places, (int X, int Y) anchor,
        TacticalFormationRules.Facing facing, Func<int, int, bool> free, Func<int, int, bool> figures)
    {
        if (asides.Count == 0) return;
        for (var i = 0; i < slots.Count; i++)
        {
            var unit = slots[i].Unit;
            if (places[i] is not { } place || anchors.ContainsKey(unit) || !asides.TryGetValue(unit, out var sent)) continue;
            var slot = TacticalFormationRules.CellOf(anchor.X, anchor.Y, slots[i].X, slots[i].Y, facing);
            var kept = GroupAiFormationRules.HoldAside(place, slot, anchor, facing, sent,
                (x, y) => free(x, y) && !figures(x, y) && !Array.Exists(places, other => other == (x, y)));
            if (kept is { } cell) places[i] = cell;
            else asides.Remove(unit);
        }
    }

    // An order to a place aside of the slot around the leader's anchor is kept for HoldAsides; one to the slot ends it.
    private static void NoteAside(Guid guid, TacticalFormationRules.Slot slot, (int X, int Y) anchor, TacticalFormationRules.Facing facing, (int X, int Y) place)
    {
        if (anchors.ContainsKey(guid) || TacticalFormationRules.CellOf(anchor.X, anchor.Y, slot.X, slot.Y, facing) == place) asides.Remove(guid);
        else asides[guid] = (anchor, facing, place);
    }

    // A player's order on a party unit (GroupAiDirectOrderPatch, after NotePlayerOrder). A member of the group's order takes its
    // planned cell as its order; a single member with its formation on moved in a fight is placed by hand.
    internal static void OnPlayerOrder(UnitEntity unit, TaskBase task)
    {
        if (unit == null) return;
        var guid = CoreGame.GuidOf(unit);
        // Any order of the player ends a waypoint run noted before, also while the formation does not apply (IsWaypointRun).
        if (!Active)
        {
            waypointCells.Remove(guid);
            waypointRun.Remove(guid);
            return;
        }
        var now = GameClock.Now;
        // A task other than a walk or an attack on a foe (feeding, an item, a bandage, carrying, a rescue, a cast): the member is busy
        // until that very task ends (GroupAiFormationRules.BusyOrder, RunsTask).
        if (task != null && GroupAiFormationRules.BusyOrder(task.TryCast<MoveTask>() != null, AttacksFoe(task)))
            busy[guid] = (task.Pointer, task.GetIl2CppType().Name, now);
        else busy.Remove(guid);
        if (planned.Remove(guid, out var plan) && now - plan.At <= GroupAiFormationRules.PlanSeconds)
        {
            var cell = plan.Cell;
            // The group's order goes out with the game's cell; the planned cell is written into its task here, not into the game's
            // list (writing that list hung the game).
            // Only a plain move (InteractionRange 0, no way to a chest or a person), only in the prefix, only with a fresh plan (Core).
            if (task?.TryCast<MoveTask>() is { InteractionRange: 0 } planMove && (planMove.TargetPosition.x, planMove.TargetPosition.y) != cell)
                planMove._TargetPosition_k__BackingField = new Vector2Int(cell.X, cell.Y);
            ordered[guid] = cell;
            orderedAt[guid] = GameClock.Now;
            groupOrdered.Add(guid);
            memberOrders.Remove(guid);
            waypointCells.Remove(guid);
            waypointRun.Remove(guid);
            handQuiet.Remove(guid);
            ReleaseHand(guid, "group");
            return;
        }
        groupOrdered.Remove(guid);
        if (guid == lastLeaderGuid)
        {
            waypointCells.Remove(guid);
            leaderOrder = task?.TryCast<MoveTask>() is { } leaderMove ? (leaderMove.TargetPosition.x, leaderMove.TargetPosition.y) : null;
            leaderOrderAt = now;
            // An order for the leader alone: the others follow its walk as before and face its way where it stops; it lets
            // nobody placed by hand go. Its walk starts anew here, so two orders given
            // within StillLooks do not read as one (south then east read as southeast).
            trail.Clear();
            return;
        }
        if (task?.TryCast<MoveTask>() is { } way && IsWaypointRun(guid, (way.TargetPosition.x, way.TargetPosition.y)))
        {
            waypointRun.Add(guid);
            groupOrdered.Add(guid);
            memberOrders.Remove(guid);
            // The last waypoint taken: its cells go, so a later single order to one of them is no waypoint run.
            if (TacticalFormationMove.WaypointsLeft(guid) == 0) waypointCells.Remove(guid);
            // Moved with the group, so a member placed by hand is let go (as by a group's order).
            ReleaseHand(guid, "group");
            ordered[guid] = (way.TargetPosition.x, way.TargetPosition.y);
            orderedAt[guid] = GameClock.Now;
            handQuiet.Remove(guid);
            return;
        }
        waypointRun.Remove(guid);
        waypointCells.Remove(guid);
        if (task?.TryCast<MoveTask>() is { } single) memberOrders[guid] = ((single.TargetPosition.x, single.TargetPosition.y), now);
        else memberOrders.Remove(guid);
        if (!members.Contains(guid) || !TacticalFormation.IsOn(guid) || task?.TryCast<MoveTask>() == null) return;
        // The fight read at the order itself, from the same source as the look: an order right at the fight's start, before the
        // next look, belongs to the fight too.
        if (!fight && !TacticalFormation.MapMembers().Exists(InCombat)) return;
        if (placedByHand.Add(guid)) HandLine(guid, true, "fight-order");
    }

    // A member of a group's order still walking it: the game's move runs for it and its order is the planned cell. NeedsOrder knows
    // only Core's own orders as running, so it sent the member again after ReorderSeconds.
    // The leader's path, read once the game has it: the cells from where it stood to the goal, the direction of its last stretch that
    // sees the goal. A different direction than the plan's turns the places; their members go there on this look (NeedsOrder, their
    // ordered cell differs now). CORE_AI_FORMATION_PATH names the cells, both directions and the tick.
    private static void ReadLeaderPath(UnitEntity leader, List<UnitEntity> party, IInMapDataSupplier inMap)
    {
        if (++pathLooks > GroupAiFormationRules.PathLooksMax)
        {
            pathPending = false;
            GroupAiPolicy.Log($"CORE_AI_FORMATION_PATH none looks={GroupAiFormationRules.PathLooksMax} tick={Tick()}");
            return;
        }
        var moves = CoreGame.TickHandler(CoreGame.CurrentMap)?._moveSystem;
        var entity = leader.Cast<IEntity>();
        if (moves == null || !moves.HasPath(entity) || leaderGoal is not { } goal) return;
        var path = moves.GetPath(entity)?.ToArray();
        if (path == null) return;
        pathPending = false;
        var cells = new List<(int X, int Y)> { goalFrom };
        foreach (var point in path)
        {
            var cell = VectorExtensions.ToLogicalPosition(point);
            if (cells[cells.Count - 1] != (cell.x, cell.y)) cells.Add((cell.x, cell.y));
        }
        if (cells[cells.Count - 1] != goal) cells.Add(goal);
        // Only the path's last TrailCap cells, as the trail's window: a long path costs the movable check once per cell.
        if (cells.Count > GroupAiFormationRules.TrailCap) cells.RemoveRange(0, cells.Count - GroupAiFormationRules.TrailCap);
        var stands = new HashSet<(int, int)>();
        foreach (var member in party)
        {
            var p = CoreGame.Position(member);
            stands.Add((p.x, p.y));
        }
        var way = TacticalFormationRules.StopFacing(cells, Walkable(stands, inMap), out var from);
        var before = goalFacing;
        GroupAiPolicy.Log($"CORE_AI_FORMATION_PATH cells={cells.Count} from={from.X},{from.Y} facing={(way?.ToString() ?? "-").ToLowerInvariant()} planned={(before?.ToString() ?? "-").ToLowerInvariant()} tick={Tick()}");
        if (way is { } turned && turned != before) goalFacing = turned;
    }

    // The cell the member was sent to: a fresh plan's cell before its order arrives (NeedsOrder compares it first),
    // else its last order.
    private static (int X, int Y)? SentCell(Guid guid)
    {
        if (planned.TryGetValue(guid, out var plan) && GameClock.Now - plan.At <= GroupAiFormationRules.PlanSeconds) return plan.Cell;
        return ordered.TryGetValue(guid, out var sent) ? sent : null;
    }

    // DIAGNOSTIC: a member that was already sent to its place stands more than one cell off it and is
    // sent back. CORE_AI_FORMATION_PULLED names the task in its brain, the one that took it away (in play: after fights a
    // member left its place without a Core line). At most PulledLineCap lines per session; it stays, shrinks or goes after the finding.
    private const int PulledLineCap = 20;
    private static int pulledLines;

    private static void LogPulled(UnitEntity unit, Guid guid, (int X, int Y) place, (int X, int Y) at)
    {
        if (pulledLines >= PulledLineCap || Math.Max(Math.Abs(at.X - place.X), Math.Abs(at.Y - place.Y)) <= 1) return;
        pulledLines++;
        string task;
        try { task = CoreGame.TickHandler(CoreGame.CurrentMap)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.GetTask()?.GetIl2CppType().Name ?? "-"; }
        catch (Exception ex) { task = "error:" + ex.GetType().Name; }
        GroupAiPolicy.Log($"CORE_AI_FORMATION_PULLED unit={guid:D} place={place.X},{place.Y} at={at.X},{at.Y} task={task} tick={Tick()}");
    }

    // The target of the move task in the unit's brain (as GroupAiMoveWatch.BrainTask reads it), null without one. Only a
    // move to the member's ordered cell counts as its order still running, not any later move.
    private static (int X, int Y)? CurrentMoveTarget(UnitEntity unit)
    {
        var brain = CoreGame.TickHandler(CoreGame.CurrentMap)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>());
        var move = brain?.GetTask()?.TryCast<MoveTask>();
        return move == null ? null : (move.TargetPosition.x, move.TargetPosition.y);
    }

    // The game queued a waypoint for a unit (TacticalFormationMove's AddWayPoint postfix). Only a member's waypoints while the
    // formation applies are a run in formation; the leader's or those of a time without it would later make a single order to one
    // of their cells look like a run (IsWaypointRun).
    internal static void NoteWaypoint(Guid unit, (int X, int Y) cell)
    {
        if (!Active || unit == lastLeaderGuid) return;
        if (!waypointCells.TryGetValue(unit, out var cells)) waypointCells[unit] = cells = new HashSet<(int X, int Y)>();
        cells.Add(cell);
    }

    // A move to one of the member's queued waypoints, or while some still wait: a run in formation.
    private static bool IsWaypointRun(Guid unit, (int X, int Y) target)
        => (waypointCells.TryGetValue(unit, out var cells) && cells.Contains(target)) || TacticalFormationMove.WaypointsLeft(unit) > 0;

    // A fresh plan counts as well: the look may come between TryOrder and the order's arrival in the same tick
    // (Core sent a member to its planned cell once more just before the player's order arrived).
    private static bool PlayerMoveRuns(UnitEntity unit, Guid guid)
        => (planned.TryGetValue(guid, out var plan) && GameClock.Now - plan.At <= GroupAiFormationRules.PlanSeconds)
           || (groupOrdered.Contains(guid) && ordered.TryGetValue(guid, out var cell) && CurrentMoveTarget(unit) == cell)
           || (waypointRun.Contains(guid) && TacticalFormationMove.WaypointsLeft(guid) > 0);

    // Whether the leader walks by a player's order: a group's order to its goal, a planned cell, or its own order still running.
    private static bool PlayerLeads(UnitEntity leader, Guid guid)
    {
        if (leaderGoal != null || PlayerMoveRuns(leader, guid)) return true;
        if (leaderOrder is not { } cell) return false;
        // The order counts from the moment it is given, before the game's brain has taken the task (GroupAiFormationRules.PlanSeconds).
        if (GameClock.Now - leaderOrderAt <= GroupAiFormationRules.PlanSeconds || CurrentMoveTarget(leader) == cell) return true;
        leaderOrder = null;
        return false;
    }

    // The formation switched off and on again by hand forgets the hand and the anchor (TacticalFormation.Switch).
    internal static void ForgetHand(Guid unit)
    {
        ReleaseHand(unit, "switch");
        handQuiet.Remove(unit);
        DropAnchor(unit, "switch");
    }

    private static void ReleaseHand(Guid unit, string reason)
    {
        handFoe.Remove(unit);
        if (placedByHand.Remove(unit)) HandLine(unit, false, reason);
    }

    // No fight runs: a member placed by a click on a foe is let go once that foe is gone or dead, at the latest after
    // ClickNoFightSeconds; it returns after StanceQuietSeconds. While the player's attack order stands the hand holds and its
    // time without a fight starts again (GroupAiFormationRules.IdleHandRelease).
    private static void ReleaseIdleClicks(float now)
    {
        foreach (var guid in placedByHand.ToList())
        {
            var ordered = GroupAiChase.HasOrder(guid);
            if (ordered) handNoFight.Remove(guid);
            if (!handNoFight.TryGetValue(guid, out var since)) handNoFight[guid] = since = now;
            var foe = !ordered && handFoe.TryGetValue(guid, out var clicked) ? CoreGame.FindUnit(clicked) : null;
            var reason = GroupAiFormationRules.IdleHandRelease(ordered, foe == null || foe.IsDead, now - since);
            if (reason == null) continue;
            ReleaseHand(guid, reason);
            handNoFight.Remove(guid);
            handFoe.Remove(guid);
            handQuiet[guid] = now + GroupAiRules.StanceQuietSeconds;
        }
    }

    // The fight is over: every member placed by hand is let go and returns after StanceQuietSeconds.
    private static void EndHand(float now)
    {
        foreach (var unit in placedByHand.ToList())
        {
            ReleaseHand(unit, "fight-end");
            handQuiet[unit] = now + GroupAiRules.StanceQuietSeconds;
        }
    }

    // A player's attack on a foe out of the member's reach places it by hand: it walks there and attacks what it reaches, a fight even
    // if the game does not count one yet. In reach it breaks nothing. The one source is the player's attack order (GroupAiChase, the
    // game's DirectOrder of a right-click or of a skill on a foe); a forced target is no order, the game sets one only with a taunt.
    private static void NoteClicks(List<UnitEntity> party, float now)
    {
        foreach (var unit in party)
        {
            var guid = CoreGame.GuidOf(unit);
            // The crown too: held, it would never walk to the foe it was sent at (30 s without a step).
            if (!members.Contains(guid) || !TacticalFormation.IsOn(guid)) continue;
            var order = GroupAiChase.OrderedTarget(unit);
            var ordered = order?.Guid.ToString();
            lastOrdered.TryGetValue(guid, out var orderedBefore);
            lastOrdered[guid] = ordered;
            if (ordered != null && ordered != orderedBefore && !placedByHand.Contains(guid)) PlaceByHand(unit, guid, order, "attack-order");
        }
    }

    // The member is placed by hand on an attack at a foe out of its basic attack's reach; false when the foe is in reach.
    private static bool PlaceByHand(UnitEntity unit, Guid guid, IEntity foe, string reason)
    {
        var basic = CoreGame.Component<SkillComponent>(unit.Cast<IEntity>())?.GetBasicAttackSkill();
        var range = GroupAiChase.SkillRange(basic);
        var inMap = CoreGame.InMap;
        if (inMap == null) return false;
        var from = inMap.GetLogicalPosition(unit.Guid);
        var to = inMap.GetLogicalPosition(foe.Guid);
        if (GroupAiFormationRules.InReach(from.x, from.y, to.x, to.y, range)) return false;
        placedByHand.Add(guid);
        handQuiet.Remove(guid);
        if (Guid.TryParse(foe.Guid.ToString(), out var foeGuid)) handFoe[guid] = foeGuid;
        HandLine(guid, true, reason);
        return true;
    }

    private static void HandLine(Guid unit, bool on, string reason)
        => GroupAiPolicy.Log($"CORE_AI_FORMATION_HAND unit={unit:D} {(on ? "on" : "off")} reason={reason} tick={Tick()}");

    // The leader's walk is noted cell by cell; once it has stood StillLooks looks, the formation faces the way it walked (the last
    // stretch that sees the stop, TacticalFormationRules.StopFacing), and the next walk starts there. Party members do not block
    // the line, foes and terrain do.
    private static void StopTurn(List<UnitEntity> party, Vector2Int at, IInMapDataSupplier inMap)
    {
        var cell = (at.x, at.y);
        if (lastLeader is { } last && (last.x, last.y) != cell)
        {
            if (trail.Count == 0) trail.Add((last.x, last.y));
            if (trail.Count >= GroupAiFormationRules.TrailCap) trail.RemoveAt(0);
            trail.Add(cell);
            return;
        }
        if (walk.Still < GroupAiFormationRules.StillLooks || trail.Count == 0) return;
        var stands = new HashSet<(int, int)>();
        foreach (var member in party)
        {
            var p = CoreGame.Position(member);
            stands.Add((p.x, p.y));
        }
        var facing = TacticalFormationRules.StopFacing(trail, Walkable(stands, inMap), out var from);
        var start = trail[0];
        trail.Clear();
        if (facing is not { } stop || stop == turn.Facing) return;
        turn = new TacticalFormationRules.Turn(stop);
        GroupAiPolicy.Log($"CORE_AI_FORMATION_STOP facing={stop.ToString().ToLowerInvariant()} start={start.X},{start.Y} from={from.X},{from.Y} to={cell.Item1},{cell.Item2} tick={Tick()}");
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
        var range = GroupAiChase.SkillRange(skill.Key);
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
    // is out of its skill's reach; never toward the target of the player's attack order (GroupAiChase.Ordered).
    internal static bool CancelsMove(UnitEntity owner, ActCombatBehaviourTask task)
    {
        if (task == null || !Holds(owner) || GroupAiChase.Ordered(owner, task.TargetEntity)) return false;
        var target = task.TargetEntity;
        var skill = task.Skill;
        var inMap = CoreGame.InMap;
        if (target == null || skill == null || inMap == null) return false;
        var range = GroupAiChase.SkillRange(skill.Key);
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

    // Free to take its place: alive, not waiting for orders, not held by the player. A member with its formation on is held when
    // placed by hand or while its attack order stands (GroupAiFormationRules.HeldByPlayer), or for the player's quiet after its own
    // single order; a group's order holds nobody (its cells are the places).
    // A member with its formation off is held for the quiet as before.
    // Why the member is not free this look, null when it is (CORE_AI_FORMATION_SKIP names it). Only "wait for orders" holds it back,
    // auto attack off does not (GroupAiFormationRules.OpenToFollow).
    private static string NotFree(UnitEntity unit)
    {
        if (unit.IsDead) return "dead";
        if (!GroupAiFormationRules.OpenToFollow(GroupAiGate.WaitsForOrder(unit))) return "wait";
        var guid = CoreGame.GuidOf(unit);
        if (GroupAiFormationRules.HeldByPlayer(placedByHand.Contains(guid), GroupAiChase.HasOrder(guid))) return "hand";
        if (busy.TryGetValue(guid, out var ordered))
        {
            if (GroupAiFormationRules.BusyWithOrder(true, GameClockRules.Since(GameClock.Now, ordered.At), RunsTask(unit, ordered.Task, ordered.Type)))
                return "busy";
            busy.Remove(guid);
        }
        if (handQuiet.TryGetValue(guid, out var until))
        {
            if (GameClock.Now < until) return "quiet";
            handQuiet.Remove(guid);
        }
        if (TacticalFormation.IsOn(guid) && groupOrdered.Contains(guid)) return null;
        if (memberOrders.TryGetValue(guid, out var order))
        {
            // The player's walk runs while the member has a MoveTask that is not Core's own: the game may pick another cell on
            // the way than the clicked one (the quiet began before the arrival). A look between the order and the game's task
            // (the same tick) finds no MoveTask yet; the order holds from its moment for PlanSeconds.
            if (GameClockRules.Since(GameClock.Now, order.At) <= GroupAiFormationRules.PlanSeconds) return "walk";
            if (CurrentMoveTarget(unit) != null && !GroupAiMoveWatch.Holds(unit)) return "walk";
            memberOrders.Remove(guid);
            handQuiet[guid] = GameClock.Now + GroupAiRules.StanceQuietSeconds;
            return "quiet";
        }
        return GroupAiStance.PlayerQuiet(guid.ToString("D")) ? "stance" : null;
    }

    // Why the member is not kept (free, its formation switched on and standing), null when it is: it follows, holds and returns to
    // its place.
    private static string NotKept(UnitEntity unit)
    {
        var notFree = NotFree(unit);
        var on = TacticalFormation.IsOn(CoreGame.GuidOf(unit));
        var lying = Lying(unit);
        if (GroupAiFormationRules.Keeps(notFree == null, on, lying)) return null;
        return notFree ?? (!on ? "off" : "lying");
    }

    private static bool Keeps(UnitEntity unit) => NotKept(unit) == null;

    // The member's brain, on its own map, still runs the ordered task itself (GroupAiFormationRules.SameTask).
    private static bool RunsTask(UnitEntity unit, IntPtr ordered, string type)
    {
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return false;
        var task = CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.GetTask();
        return task != null && GroupAiFormationRules.SameTask(task.Pointer == ordered, task.GetIl2CppType().Name == type);
    }

    // The player's order attacks a foe: a combat task without an item on a unit of neither the player's side nor its allies.
    private static bool AttacksFoe(TaskBase task)
    {
        var act = task?.TryCast<ActCombatBehaviourTask>();
        if (act == null || act.UseItem != null) return false;
        var target = act.TargetEntity?.TryCast<UnitEntity>();
        return target != null && target.Faction != FactionType.Player && target.Faction != FactionType.Ally;
    }

    // A damage dealer gone to the foes by its role (GroupAiRoleMoves.Roams): Core's own action, so only behind the group AI's gate
    // (GroupAiFormationRules.CoreActs); a member with auto attack off follows, but Core sends it nowhere on its own.
    private static bool RoamsNow(UnitEntity unit, Guid guid, float now)
        => GroupAiGate.OpenFor(unit) && GroupAiRoleMoves.Roams(unit, guid, fight, now);

    // CORE_AI_FORMATION_SKIP once a member's reason changes; a member sent again clears it.
    private static void NoteSkip(Guid guid, string why)
    {
        skipped.TryGetValue(guid, out var before);
        if (why == null)
        {
            skipped.Remove(guid);
            return;
        }
        skipped[guid] = why;
        if (GroupAiFormationRules.LogsSkip(before, why) && skipLines++ < SkipLogCap)
            GroupAiPolicy.Log(GroupAiFormationRules.SkipLine(guid.ToString("D"), why) + " tick=" + Tick());
    }

    private static float LastHit(Guid unit) => lastHit.TryGetValue(unit, out var at) ? at : float.NegativeInfinity;

    // At near-death or downed: it cannot walk.
    internal static bool Lying(UnitEntity unit)
        => WoundRules.IsLying(CoreGame.Affecters(unit)?.Has(ReviveSnapshots.DeadlyKey) == true, GroupAiBandits.IsDowned(unit));

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
// (TacticalFormationTest.Holds), keeps its own cell as the idle target instead; a member with the group AI and no formation stands
// or walks back to the group's middle (GroupAiChase).
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
            // The camp's boss party idles inside its camp (BanditCampHome).
            if (BanditCampHome.IdleCell(owner, __instance._inMapData) is { } camp && __instance._task?.TryCast<IdleTask>() is { } campIdle)
            {
                campIdle.SetTargetPosition(new Vector2Int(camp.X, camp.Y));
                return false;
            }
            var formation = GroupAiFormation.StaysStill(owner);
            if (!formation && !TacticalFormationTest.Holds(owner))
            {
                GroupAiFormation.NoteFree(owner, __instance._inMapData);
                // Without a formation, with the group AI: stand, or walk back to the group's middle (GroupAiChase.IdleTarget).
                if (GroupAiChase.IdleTarget(owner, __instance._inMapData) is { } stand && __instance._task?.TryCast<IdleTask>() is { } own)
                {
                    own.SetTargetPosition(new Vector2Int(stand.X, stand.Y));
                    return false;
                }
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
// Movement of the group AI (where Core steers, the game has no say), part 1: while Core's own move order of a
// party member with the group AI runs (GroupAiMoveWatch), the game's step toward the skill's target is cancelled and counted (stops=),
// as CoreUnitMovePatch does for the bandits; the member follows Core's order alone.
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
            if (owner == null) return;
            var task = __instance._task?.TryCast<ActCombatBehaviourTask>();
            // The player's attack on its ordered target goes before Core's open move order.
            if (owner.Faction == FactionType.Player && GroupAiSettings.Enabled && !GroupAiChase.Ordered(owner, task?.TargetEntity)
                && GroupAiMoveWatch.Stops(owner))
            {
                __result = true;
                return;
            }
            if (GroupAiFormation.CancelsMove(owner, task)) __result = true;
            // Part 2: the walk toward the target by role, with and without a formation (GroupAiChase).
            else if (GroupAiChase.Holds(owner, task)) __result = true;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_AI_FORMATION_HOLD_FAILED: " + ex.Message);
        }
    }
}
