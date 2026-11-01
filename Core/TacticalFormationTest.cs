using System.Collections;
using HarmonyLib;
using UnityEngine;
#if BEPINEX
using EntityBrain = global::Refactor.Task.EntityBrain;
#else
using EntityBrain = Il2CppRefactor.Task.EntityBrain;
#endif

namespace DungeonSettlersDelvers.Core;

// "Test formation" in the window: the group lines up once around the crown, and after HoldSeconds
// everybody goes back to their work. It needs no group AI, only the game's move orders: each member gets the player's own order
// (GroupAiZones.OrderMove, DirectOrderRequested with a MoveTask, followed by GroupAiMoveWatch); the crown gets one to its own cell,
// which takes it off its work too. The order takes the unit out of the game's work auction (TaskSystem.DirectOrder ->
// RemoveWorkableUnit).
// While the test holds, the game's own decisions are shut for these units instead of being answered with new orders (Core sending
// them back once a second lost against the village's work, they danced around): the tree's request for
// work (the node RequestSharedTask, the only caller of EntityBrain.RequestSharedTask) finds none, and idle wandering keeps the
// unit's cell (GroupAiFormationIdlePatch). After the hold both open again and the game hands them their work. The log shows each
// unit's task before the test and when it is back at work, at most ReturnSeconds later (CORE_TACTICAL_TEST_*), so a run proves
// the return.
internal static class TacticalFormationTest
{
    internal const float HoldSeconds = 20f;
    // How long after the hold a unit may take to be back at work before RETURN counts it as not back.
    private const float ReturnSeconds = 15f;
    // How often the coroutine looks at the game clock while it waits.
    private const float LookSeconds = 0.5f;
    private const string Reason = "formation-test";

    private sealed class Member
    {
        internal UnitEntity Unit;
        internal Vector2Int To;
        internal string TaskBefore;
    }

    private static int generation;
    private static readonly HashSet<Guid> held = new();
    private static readonly HashSet<Guid> refusedLogged = new();

    internal static bool Running { get; private set; }

    internal static void ResetLifecycle()
    {
        generation++;
        Running = false;
        held.Clear();
        refusedLogged.Clear();
    }

    // A unit of the running test: the game gives it no work and lets it not wander.
    internal static bool Holds(UnitEntity unit) => Running && unit != null && held.Contains(CoreGame.GuidOf(unit));

    // CORE_TACTICAL_TEST_NOWORK once per unit and test, when its first request for work is refused.
    internal static void NoteRefused(UnitEntity unit)
    {
        var guid = CoreGame.GuidOf(unit);
        if (refusedLogged.Add(guid)) DelversHost.Info($"CORE_TACTICAL_TEST_NOWORK unit={guid:D}");
    }

    // Lines the group up around the leader's cell. A member whose formation is switched off (TacticalFormation.IsOn) stays
    // where it is and keeps its place empty. False when nothing could be ordered.
    internal static bool Start(Guid leader, IReadOnlyList<TacticalFormationRules.Slot> planned)
    {
        if (Running || !CoreGame.CampaignReady) return false;
        var inMap = CoreGame.InMap;
        var crown = CoreGame.FindUnit(leader);
        if (inMap == null || crown == null) return false;
        var at = CoreGame.Position(crown);
        var slots = new List<TacticalFormationRules.Slot>();
        foreach (var slot in planned)
            if (TacticalFormation.IsOn(slot.Unit)) slots.Add(slot);
        bool Free(int x, int y) => !inMap.IsBlocked(new Vector2Int(x, y));
        // The leader stands, so there is no walk to turn by: the window's top is north on the map, the view's top.
        var cells = TacticalFormationRules.Place(at.x, at.y, TacticalFormationRules.Facing.North, slots, Free);
        var members = new List<Member>();
        void Order(UnitEntity unit, Vector2Int to)
        {
            var member = new Member { Unit = unit, To = to, TaskBefore = TaskOf(unit) };
            if (!GroupAiZones.OrderMove(unit, to, Reason)) return;
            members.Add(member);
            DelversHost.Info($"CORE_TACTICAL_TEST_ORDER unit={unit.Guid} task={member.TaskBefore} to={to.x},{to.y}");
        }
        for (var i = 0; i < slots.Count; i++)
        {
            var unit = CoreGame.FindUnit(slots[i].Unit);
            if (unit != null && cells[i] is { } cell) Order(unit, new Vector2Int(cell.X, cell.Y));
        }
        var lined = members.Count;
        if (lined > 0) Order(crown, at);
        // members= counts the lined up units, crown= whether the crown's order went out; END and RETURN count both.
        DelversHost.Info($"CORE_TACTICAL_TEST_START leader={leader:D} at={at.x},{at.y} members={lined} of={slots.Count} off={planned.Count - slots.Count} crown={(members.Count > lined ? 1 : 0)}");
        // A test with nobody to line up had no closing line.
        if (lined == 0)
        {
            DelversHost.Info("CORE_TACTICAL_TEST_END members=0 back=0");
            return false;
        }
        held.Clear();
        refusedLogged.Clear();
        foreach (var member in members) held.Add(CoreGame.GuidOf(member.Unit));
        Running = true;
        var mine = ++generation;
        DelversHost.StartCoroutine(Hold(mine, members, GameClock.Now));
        return true;
    }

    private static IEnumerator Hold(int mine, List<Member> members, float started)
    {
        while (mine == generation && !GameClock.Elapsed(started, HoldSeconds)) yield return new WaitForSecondsRealtime(LookSeconds);
        if (mine != generation) yield break;
        Running = false;
        held.Clear();
        var ended = GameClock.Now;
        foreach (var member in members)
            DelversHost.Info($"CORE_TACTICAL_TEST_RELEASED unit={member.Unit.Guid} at={Cell(member.Unit)} to={member.To.x},{member.To.y}");
        // The game's work auction hands work out over several ticks (three of four were still idle 2.5 s after
        // the release, all at work later), so each unit is watched until it works or ReturnSeconds have passed.
        var waiting = new List<Member>(members);
        var back = 0;
        while (mine == generation && waiting.Count > 0)
        {
            yield return new WaitForSecondsRealtime(LookSeconds);
            if (mine != generation) yield break;
            var late = GameClock.Elapsed(ended, ReturnSeconds);
            for (var i = waiting.Count - 1; i >= 0; i--)
            {
                var member = waiting[i];
                var after = TaskOf(member.Unit);
                var working = Working(member.TaskBefore, after);
                if (!working && !late) continue;
                if (working) back++;
                waiting.RemoveAt(i);
                DelversHost.Info($"CORE_TACTICAL_TEST_RETURN unit={member.Unit.Guid} before={member.TaskBefore} after={after} back={working} seconds={GameClock.Since(ended).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }
        DelversHost.Info($"CORE_TACTICAL_TEST_END members={members.Count} back={back}");
    }

    // Back at work: a task other than standing or walking, or the same task as before the test.
    private static bool Working(string before, string after) => (after != "Move" && after != "Idle") || before == after;

    // The unit's task as the game's TaskComponent names it, "-" when unknown.
    private static string TaskOf(UnitEntity unit)
    {
        try { return CoreGame.Component<TaskComponent>(unit?.Cast<IEntity>())?.TaskType.ToString() ?? "-"; }
        catch (Exception) { return "-"; }
    }

    private static string Cell(UnitEntity unit)
    {
        var inMap = CoreGame.InMap;
        if (inMap == null || unit == null) return "-";
        var at = inMap.GetLogicalPosition(unit.Guid);
        return at.x + "," + at.y;
    }
}

// The tree's request for shared work (the node RequestSharedTask: no task, then EntityBrain.RequestSharedTask; false makes the
// node fail as when there is no work): refused for a unit of the running formation test.
[HarmonyPatch(typeof(EntityBrain), nameof(EntityBrain.RequestSharedTask))]
internal static class TacticalFormationTestWorkPatch
{
    private static int failures;

    private static bool Prefix(EntityBrain __instance, ref bool __result)
    {
        try
        {
            var owner = __instance.Owner?.TryCast<UnitEntity>();
            if (!TacticalFormationTest.Holds(owner)) return true;
            TacticalFormationTest.NoteRefused(owner);
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_TACTICAL_TEST_NOWORK_FAILED: " + ex.Message);
            return true;
        }
    }
}
