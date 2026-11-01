using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Every move order of the group AI is followed to its end (74 kite
// and 32 formation orders of the bandits logged ok=True, none arrived). One line per order when it
// arrives, is held, interrupted, blocked, times out, is replaced by the unit's next order or the unit dies
// (GroupAiRules.MoveDoneLine). Looked at every game tick from GroupAiBanditTickPatch; nothing runs without
// an open order.
// - While an order runs Core lays no skill or basic attack on the unit (Holds, waits=) and the enemy tree's
//   move check sets no step of its own toward the hostile but lets the tree walk Core's MoveTask (Stops,
//   stops=; the formation walked to the party; answering "no" there
//   kept every bandit order from moving at all).
// - The unit's brain is read every tick: once Core's MoveTask was seen there and another task took its
//   place (a hit, a knockback, another node), the order ends as interrupted, by= names that task, and the
//   unit decides again at once. own= says whether Core's MoveTask was the brain's task half a second
//   after the order.
// - The deadline runs slower while the unit walks slower (MovementSpeedPerModifier, e.g. Limping -40 %);
//   speed= is the slowest factor seen.
internal static class GroupAiMoveWatch
{
    private sealed class Order
    {
        internal UnitEntity Unit;
        internal string Guid;
        internal bool Player;
        internal MapType Map;
        internal Vector2Int From;
        internal Vector2Int To;
        internal int StartDistance;
        internal string Reason;
        internal string Path;
        internal IntPtr TaskPointer;
        internal long Tick;
        internal long LastTick;
        internal double Budget;
        internal double SlowestSpeed = 1.0;
        internal int Deadline;
        internal int Moved;
        internal string Task = "-";
        internal bool Own;
        internal bool Seen;
        internal string By = "-";
        internal bool Probed;
        internal int Waits;
        internal int Stops;
    }

    private const int LogCap = 2000;

    private static readonly Dictionary<string, Order> orders = new(StringComparer.Ordinal);
    // Units whose last order ended without a single step (GroupAiRules.KiteAgainNow).
    private static readonly HashSet<string> lastUnmoved = new(StringComparer.Ordinal);
    // (all 9 quiet:task=MoveTask came right after Core's own stance order arrived): the
    // MoveTask Core sent last per unit, kept after its order ends, while the brain may still hold it.
    private static readonly Dictionary<string, IntPtr> lastOwnTask = new(StringComparer.Ordinal);
    private static long lastTick = -1;
    private static int logLines;
    private static int warnings;

    internal static void Reset()
    {
        orders.Clear();
        lastUnmoved.Clear();
        lastOwnTask.Clear();
        lastTick = -1;
        logLines = warnings = 0;
    }

    // A new order of the unit; its open one ends as replaced. task is the MoveTask Core sent.
    internal static void Start(UnitEntity unit, bool player, MapType map, Vector2Int to, string reason, string path, IntPtr task)
    {
        var inMap = InMapOf(map);
        if (unit == null || inMap == null) return;
        var guid = unit.Guid.ToString();
        var from = inMap.GetLogicalPosition(unit.Guid);
        var tick = Tick();
        if (orders.TryGetValue(guid, out var open)) Finish(open, from, "replaced", tick);
        if (task != IntPtr.Zero) lastOwnTask[guid] = task;
        var startDistance = GroupAiRules.Distance(from.x, from.y, to.x, to.y);
        orders[guid] = new Order
        {
            Unit = unit,
            Guid = guid,
            Player = player,
            Map = map,
            From = from,
            To = to,
            StartDistance = startDistance,
            Reason = string.IsNullOrEmpty(reason) ? "-" : reason,
            Path = path,
            TaskPointer = task,
            Tick = tick,
            LastTick = tick,
            Deadline = GroupAiRules.MoveDeadlineTicks(startDistance)
        };
    }

    // Whether a brain task is the MoveTask Core sent last to this unit, also after its order ended.
    internal static bool IsOwnTask(UnitEntity unit, IntPtr task)
        => unit != null && task != IntPtr.Zero && lastOwnTask.TryGetValue(unit.Guid.ToString(), out var own) && own == task;

    // The unit's own order still runs (GroupAiRules.MoveHolds): CoreUnitCombat and GroupAiDecideSkillPatch
    // then lay nothing on it. count notes one decision held back (waits=).
    internal static bool Holds(UnitEntity unit, bool count = false)
    {
        var o = Open(unit);
        if (o == null) return false;
        if (count) o.Waits++;
        return true;
    }

    // The enemy tree's own step toward the hostile while the unit's order runs (CoreUnitMovePatch): held
    // back and counted (stops=).
    internal static bool Stops(UnitEntity unit)
    {
        var o = Open(unit);
        if (o == null) return false;
        o.Stops++;
        return true;
    }

    // The unit's open order has not moved it off its start cell within GroupAiRules.MoveProbeTicks (a fresh order ended
    // in its first tick as "stuck").
    internal static bool Unmoved(UnitEntity unit)
    {
        var o = Open(unit);
        return o != null && o.Moved == 0 && Tick() - o.Tick >= GroupAiRules.MoveProbeTicks;
    }

    // The unit's last order ended without a single step.
    internal static bool LastUnmoved(string guid) => guid != null && lastUnmoved.Contains(guid);

    // Ends the unit's open order now (result, e.g. "threat": the healer decides again).
    internal static void End(UnitEntity unit, string result)
    {
        if (unit == null || !orders.TryGetValue(unit.Guid.ToString(), out var o)) return;
        var inMap = InMapOf(o.Map);
        Finish(o, inMap != null ? inMap.GetLogicalPosition(o.Unit.Guid) : o.From, result, Tick());
        orders.Remove(o.Guid);
    }

    // The tail of CORE_AI_BANDIT_MOVE for the unit's open order: from, path, deadline, tick.
    internal static string Fields(string guid)
        => guid != null && orders.TryGetValue(guid, out var o)
            ? GroupAiRules.MoveOrderFields(o.From.x, o.From.y, o.Path, o.Deadline, o.Tick)
            : "";

    // Every game tick (postfix of TickFlowHandler.Tick; once per tick however many maps tick).
    internal static void OnGameTick()
    {
        if (orders.Count == 0) return;
        var tick = Tick();
        if (tick == lastTick) return;
        lastTick = tick;
        List<string> done = null;
        foreach (var pair in orders)
        {
            var o = pair.Value;
            var at = o.From;
            string result;
            try
            {
                result = Look(o, tick, ref at);
            }
            catch (Exception ex)
            {
                result = "failed";
                if (warnings++ < GroupAiRules.WarningCap) DelversHost.Warning("CORE_AI_MOVE_WATCH_FAILED unit=" + o.Guid + ": " + ex.Message);
            }
            if (result == null) continue;
            Finish(o, at, result, tick);
            (done ??= new List<string>()).Add(pair.Key);
        }
        if (done != null)
            foreach (var key in done) orders.Remove(key);
    }

    private static Order Open(UnitEntity unit)
    {
        if (unit == null || orders.Count == 0 || !orders.TryGetValue(unit.Guid.ToString(), out var o)) return null;
        return GroupAiRules.MoveHolds(true, (long)o.Budget, o.Deadline) ? o : null;
    }

    // The end of the order (GroupAiRules.MoveResult), or null while it runs.
    private static string Look(Order o, long tick, ref Vector2Int at)
    {
        if (o.Unit == null || o.Unit.IsDead) return "dead";
        var inMap = InMapOf(o.Map);
        if (inMap == null) return "gone";
        at = inMap.GetLogicalPosition(o.Unit.Guid);
        o.Moved = Math.Max(o.Moved, GroupAiRules.Distance(o.From.x, o.From.y, at.x, at.y));
        var speed = GroupAiRules.MoveSpeedFactor(CoreGame.Stat(o.Unit, StatType.MovementSpeedPerModifier));
        o.SlowestSpeed = Math.Min(o.SlowestSpeed, speed);
        o.Budget += speed * Math.Max(0, tick - o.LastTick);
        o.LastTick = tick;
        var current = BrainTask(o);
        var own = current != null && o.TaskPointer != IntPtr.Zero && current.Pointer == o.TaskPointer;
        if (own) o.Seen = true;
        if (!o.Probed && tick - o.Tick >= GroupAiRules.MoveProbeTicks)
        {
            o.Probed = true;
            o.Task = NameOf(current);
            o.Own = own;
        }
        var lost = o.Seen && !own;
        if (lost) o.By = NameOf(current);
        var distance = GroupAiRules.Distance(at.x, at.y, o.To.x, o.To.y);
        return GroupAiRules.MoveResult(distance, o.StartDistance, (long)o.Budget, o.Deadline, o.Moved, Held(o.Unit), lost, o.Reason == "dodge");
    }

    private static void Finish(Order o, Vector2Int at, string result, long tick)
    {
        if (result == "arrived" || result == "held" || o.Moved > 0) lastUnmoved.Remove(o.Guid);
        else lastUnmoved.Add(o.Guid);
        if (logLines++ >= LogCap) return;
        DelversHost.Info(GroupAiRules.MoveDoneLine(o.Player, o.Guid, o.From.x, o.From.y, o.To.x, o.To.y, at.x, at.y,
            result, o.Reason, o.Path, o.Moved, tick - o.Tick, o.Task, o.Own, o.By, o.Waits, o.Stops, o.SlowestSpeed, tick));
    }

    // The task in the unit's brain (TickFlowHandler._taskSystem._performers), or null.
    private static Il2CppSystem.Object BrainTask(Order o)
    {
        var brain = CoreGame.TickHandler(o.Map)?._taskSystem?._performers?.GetEntityBrain(o.Unit.Cast<IEntity>());
        return brain != null ? brain.GetTask() : null;
    }

    private static string NameOf(Il2CppSystem.Object task) => task != null ? task.GetIl2CppType().Name : "none";

    // The unit is held in place right now (no new move order then, GroupAiBanditMoves.TryOrder).
    internal static bool IsHeld(UnitEntity unit) => unit != null && Held(unit);

    // Stun, bondage (vines) or unconscious hold the unit in place.
    // Why a unit is held, for the log (held=1 without a reason): the categories in Held, and Deadly when it lies at
    // near-death; "-" when free.
    internal static string HeldWhy(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null) return "-";
        var why = new List<string>();
        if (affecters.HasCategory(AffecterCategory.Stun)) why.Add("stun");
        if (affecters.HasCategory(AffecterCategory.Bondage)) why.Add("bondage");
        if (affecters.HasCategory(AffecterCategory.Unconscious)) why.Add("unconscious");
        if (affecters.Has(ReviveSnapshots.DeadlyKey)) why.Add("deadly");
        return why.Count == 0 ? "-" : string.Join("+", why);
    }

    private static bool Held(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        return affecters != null
            && (affecters.HasCategory(AffecterCategory.Stun) || affecters.HasCategory(AffecterCategory.Bondage)
                || affecters.HasCategory(AffecterCategory.Unconscious));
    }

    private static IInMapDataSupplier InMapOf(MapType map)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        return maps != null && maps.ContainsKey(map) ? maps[map] : null;
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}
