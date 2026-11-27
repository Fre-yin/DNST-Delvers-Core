using HarmonyLib;
using UnityEngine;
#if BEPINEX
using FormationMoveOrder = global::Refactor.Control.FormationMoveOrder;
using FormationOrder = global::Refactor.Control.FormationOrder;
using FormationQuery = global::Refactor.Control.FormationQuery;
using WayPointContainer = global::Refactor.Main.WayPointContainer;
#else
using FormationMoveOrder = Il2CppRefactor.Control.FormationMoveOrder;
using FormationOrder = Il2CppRefactor.Control.FormationOrder;
using FormationQuery = Il2CppRefactor.Control.FormationQuery;
using WayPointContainer = Il2CppRefactor.Main.WayPointContainer;
#endif

namespace DungeonSettlersDelvers.Core;

// The group's move order keeps the formation (the formation holds when all four are moved at once, while the
// formation is on; the player's order goes first, and it moves the formation instead of breaking it). A right click with several units selected runs the game's own spread: FormationMoveOrder.TryOrder(selected,
// position) fills its result list through TryAssign(query, results) and then sends that list itself (the body of SendOrders is built
// into TryOrder; SendOrders is never called, Core's check of the game). After that TryAssign, inside TryOrder only, Core plans
// the cells from the units in the list and the cells the game resolved (TacticalFormationMoveRules): the anchor is the resolved click
// (_target) where the leader can stand, else the leader's resolved cell (TacticalFormationMoveRules.Anchor), so a click on a cell nobody
// can take keeps the game's cell for the leader. Never write into FormationMoveOrder's result list (List<FormationOrder>, a struct with a reference): Il2CppInterop's
// indexer and Add pass the boxed object, the list holds garbage, the game freezes in the next tick (the game's own
// targets written back froze too). The planned cells go
// to GroupAiFormation, which writes each into its unit's move task when the game's order arrives (OnPlayerOrder, DirectOrder). The
// waypoints' TryAssign (shift) runs outside TryOrder and stays as it is. The orders stay the player's.
// Only in the dungeon, where the formation applies, and only with the comfort option on.
internal static class TacticalFormationMove
{
    private const int SkipLineCap = 8;
    private static bool inTryOrder;
    private static bool plannedInTryOrder;
    private static int skipLines;

    internal static void Reset()
    {
        inTryOrder = false;
        plannedInTryOrder = false;
        skipLines = 0;
        waypoints = null;
    }

    internal static void Begin()
    {
        inTryOrder = true;
        plannedInTryOrder = false;
    }

    // TryOrder gave up (false): MoveOrder.All and CommandFlow.Move then send every selected unit to the clicked cell themselves, a
    // MoveTask each with the same position (in a fight). The plan is made from
    // the selection and the click, and the planned cells reach the tasks the same way (OnPlayerOrder).
    internal static void Fallback(Il2CppSystem.Collections.Generic.List<IEntity> selected, Vector2Int position)
    {
        if (plannedInTryOrder || selected == null || selected.Count < 2) return;
        var units = new List<(UnitEntity Unit, (int X, int Y) Game)>();
        for (var i = 0; i < selected.Count; i++)
            if (selected[i]?.TryCast<UnitEntity>() is { IsDead: false } unit) units.Add((unit, (position.x, position.y)));
        PlanFor(units, (position.x, position.y), position, "fallback");
    }

    // After TryOrder, also when it threw (a finalizer), so nothing of it reaches a waypoint's TryAssign.
    internal static void Done() => inTryOrder = false;

    // TryAssign inside TryOrder filled results: plan from them and hand every planned cell to GroupAiFormation (the list stays as it is).
    internal static void Rewrite(FormationMoveOrder order, Il2CppSystem.Collections.Generic.List<FormationOrder> results)
    {
        if (!inTryOrder || order == null || results == null || results.Count < 2) return;
        var units = new List<(UnitEntity Unit, (int X, int Y) Game)>();
        for (var i = 0; i < results.Count; i++)
        {
            var unit = results[i]?.Unit;
            if (unit == null || unit.IsDead) continue;
            var target = results[i].Target;
            units.Add((unit, (target.x, target.y)));
        }
        var resolved = order._target;
        PlanFor(units, null, resolved, "assign");
    }

    // The plan for the units of an order and the cells the game gave them: the anchor is the click, or the resolved click where the
    // leader can stand and else the game's cell for the leader when it is among them (TacticalFormationMoveRules.Anchor).
    private static void PlanFor(List<(UnitEntity Unit, (int X, int Y) Game)> entries, (int X, int Y)? clicked, Vector2Int resolved, string source)
    {
        if (!CoreSettings.TacticalEnabled || !TacticalFormation.InDungeon) return;
        var inMap = CoreGame.InMap;
        var party = TacticalFormation.MapMembers();
        if (inMap == null || !TacticalFormation.TryGet(TacticalFormation.Guids(party), out var leaderGuid, out var slots))
        {
            Skip("no-formation", entries.Count);
            return;
        }
        var leader = party.Find(unit => CoreGame.GuidOf(unit) == leaderGuid);
        if (leader == null)
        {
            Skip("no-leader", entries.Count);
            return;
        }
        var units = new List<TacticalFormationMoveRules.Unit>();
        var game = new Dictionary<Guid, (int X, int Y)>();
        foreach (var (unit, cell) in entries)
        {
            var guid = CoreGame.GuidOf(unit);
            var at = CoreGame.Position(unit);
            game[guid] = cell;
            units.Add(new TacticalFormationMoveRules.Unit(guid, (at.x, at.y), unit.Faction == FactionType.Player && TacticalFormation.IsOn(guid)));
        }
        var leaderAt = CoreGame.Position(leader);
        // The units of the order leave their cells, so those count as free (GroupAiFormation.Walkable).
        var leaving = new HashSet<(int, int)>();
        foreach (var unit in units) leaving.Add(unit.At);
        var free = GroupAiFormation.Walkable(leaving, inMap);
        var figures = GroupAiFormation.Figures(leaving, inMap);
        (int X, int Y)? leaderCell = game.TryGetValue(leaderGuid, out var given) ? given : null;
        var click = clicked ?? TacticalFormationMoveRules.Anchor(leaderCell, (resolved.x, resolved.y), free, figures);
        var plan = TacticalFormationMoveRules.Plan(leaderGuid, (leaderAt.x, leaderAt.y), click, slots, units, free, figures);
        if (plan == null)
        {
            Skip("nothing", units.Count);
            return;
        }
        var cells = new Dictionary<Guid, (int X, int Y)>();
        var taken = new HashSet<(int, int)>();
        foreach (var pair in game)
        {
            var cell = plan.Cells.TryGetValue(pair.Key, out var planned) ? planned : TacticalFormationMoveRules.AsideFor(plan, pair.Key, pair.Value, taken);
            taken.Add(cell ?? pair.Value);
            // Every planned cell, also one the game chose too: otherwise that member's order reads as a single one.
            if (cell is { } to) cells[pair.Key] = to;
        }
        plannedInTryOrder = true;
        GroupAiFormation.NotePlan(plan, cells, (leaderAt.x, leaderAt.y));
        DelversHost.Info($"CORE_TACTICAL_MOVE_PLAN units={units.Count} placed={plan.Placed.Count} leader={(plan.WithLeader ? 1 : 0)} facing={plan.Facing.ToString().ToLowerInvariant()} click={click.X},{click.Y} resolved={resolved.x},{resolved.y} source={source}");
        foreach (var line in plan.Lines) DelversHost.Info(line);
    }

    // Shift waypoints, the minimal way: the game's own WayPointContainer is only read. A fuller way would plan a
    // place per waypoint: WaypointOrder.Reserve takes each unit's start (GetStart), runs FormationMoveOrder.TryAssign(query, results)
    // outside TryOrder and writes each result through IWayPointWriter.AddWayPoint; a prefix on AddWayPoint(guid, ref wayPoint) could place it without touching the result list (FALLEN-CORE trap 1).
    private static WayPointContainer waypoints;

    internal static void NoteWaypoint(WayPointContainer container, Il2CppSystem.Guid unit, Vector2Int cell)
    {
        waypoints = container;
        if (Guid.TryParse(unit.ToString(), out var guid)) GroupAiFormation.NoteWaypoint(guid, (cell.x, cell.y));
    }

    // How many waypoints the game still holds for the unit; 0 before any was queued.
    internal static int WaypointsLeft(Guid unit)
    {
        if (waypoints == null) return 0;
        try { return waypoints.CountWayPoints(Il2CppSystem.Guid.Parse(unit.ToString("D"))); }
        catch (Exception) { return 0; }
    }

    private static void Skip(string reason, int count)
    {
        if (skipLines++ < SkipLineCap) DelversHost.Info($"CORE_TACTICAL_MOVE_SKIP reason={reason} units={count}");
    }
}

[HarmonyPatch(typeof(FormationMoveOrder), nameof(FormationMoveOrder.TryOrder))]
internal static class TacticalFormationMovePlanPatch
{
    private static void Prefix() => TacticalFormationMove.Begin();

    private static void Postfix(Il2CppSystem.Collections.Generic.List<IEntity> selected, ref Vector2Int position, bool __result)
    {
        if (__result) return;
        try { TacticalFormationMove.Fallback(selected, position); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_MOVE_FAILED: " + ex.Message); }
    }

    private static Exception Finalizer(Exception __exception)
    {
        TacticalFormationMove.Done();
        return __exception;
    }
}

[HarmonyPatch(typeof(WayPointContainer), nameof(WayPointContainer.AddWayPoint))]
internal static class TacticalFormationWaypointPatch
{
    private static void Postfix(WayPointContainer __instance, Il2CppSystem.Guid guid, Vector2Int wayPoint)
    {
        try { TacticalFormationMove.NoteWaypoint(__instance, guid, wayPoint); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_WAYPOINT_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(FormationMoveOrder), nameof(FormationMoveOrder.TryAssign), typeof(FormationQuery), typeof(Il2CppSystem.Collections.Generic.List<FormationOrder>))]
internal static class TacticalFormationMovePatch
{
    private static void Postfix(FormationMoveOrder __instance, Il2CppSystem.Collections.Generic.List<FormationOrder> results, bool __result)
    {
        if (!__result) return;
        try { TacticalFormationMove.Rewrite(__instance, results); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_MOVE_FAILED: " + ex.Message); }
    }
}
