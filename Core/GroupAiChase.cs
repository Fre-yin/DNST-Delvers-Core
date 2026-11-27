#if BEPINEX
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using TaskBase = global::Refactor.Task.Task_Base;
#else
using ActCombatBehaviourTask = Il2CppRefactor.Task.ActCombatBehaviourTask;
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using TaskBase = Il2CppRefactor.Task.Task_Base;
#endif

namespace DungeonSettlersDelvers.Core;

// The game side of GroupAiChaseRules for party members with the group AI: at the game's skill move step (GroupAiFormationHoldPatch)
// whether the member walks toward its target out of reach (Holds), and at its idle step (GroupAiFormationIdlePatch) where it stands
// without a formation (IdleTarget). A held walk is kept for CancelSeconds, so the next decision picks another skill or a target in
// reach (MarkRefused, Blocks); NoteChoice does the same for a choice that stays without a cast. CORE_AI_CHASE and CORE_AI_IDLE_GATHER
// per change of a member's step, CORE_AI_CAST_REJECTED per change of skill, target or reason and every RefusalReportSeconds with the
// refusals since, all capped. The player's attack orders (NoteOrder, from GroupAiDirectOrderPatch, which also ends Core's open move
// order) are the one source of "the player ordered this target" for Holds, GroupAiFormation.CancelsMove, the hold patch before Core's
// move order, the formation's placing by hand and its free members (HasOrder), and the gathering tank's way back; an order ends with
// its target's death, the player's next order or the end of a fight the member fought (GroupAiChaseRules.NextOrder).
internal static class GroupAiChase
{
    private sealed class RefusalReport
    {
        internal string Skill = "";
        internal string Target = "";
        internal string Reason = "";
        internal float At;
        internal int Count;
    }

    private static readonly Dictionary<string, CastRefusal> refusals = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CastWatch> watches = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, RefusalReport> reports = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, PlayerOrder> orders = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> lastStep = new(StringComparer.Ordinal);
    private static readonly HashSet<string> gathering = new(StringComparer.Ordinal);
    private static int chaseCells = (int)GroupAiChaseRules.ChaseDefault, gatherCells = (int)GroupAiChaseRules.GatherDefault;
    private static int lines, refusalLines;
    // Holds runs at every pass of a member's tree (the game's ShouldCancelSkill): the party's middle and the ordered foes are read once a
    // tick.
    private static readonly List<(int X, int Y)> middleCells = new();
    private static (int X, int Y)? middleCell;
    private static long middleTick = -1, foesTick = -1;
    private static readonly Dictionary<string, UnitEntity> foes = new(StringComparer.Ordinal);
    // Ordered runs up to three times at one move step (the hold patch, CancelsMove, Holds): its answer per member and target, once a
    // tick, and anew whenever an order changes.
    private static readonly Dictionary<(IntPtr Unit, IntPtr Target), bool> orderedSeen = new();
    private static long orderedTick = -1;

    // At the start of a campaign and on every change of Core's settings (GroupAiPolicy.ResetCampaign).
    internal static void Reset()
    {
        refusals.Clear();
        watches.Clear();
        reports.Clear();
        orders.Clear();
        lastStep.Clear();
        gathering.Clear();
        middleTick = foesTick = orderedTick = -1;
        orderedSeen.Clear();
        middleCell = null;
        foes.Clear();
        chaseCells = GroupAiChaseRules.Cells(CoreSettings.Number(GroupAiChaseRules.ChaseKey), GroupAiChaseRules.ChaseDefault);
        gatherCells = GroupAiChaseRules.Cells(CoreSettings.Number(GroupAiChaseRules.GatherKey), GroupAiChaseRules.GatherDefault);
        lines = 0;
        refusalLines = 0;
    }

    // A party member with the group AI; summons are GroupAiSummonCast's and stay out.
    private static bool Steered(UnitEntity owner)
        => owner != null && owner.Faction == FactionType.Player && GroupAiSettings.Enabled && !Summoned(owner);

    private static bool Summoned(UnitEntity unit) => !string.IsNullOrEmpty(CoreGame.SummonerGuid(unit.Cast<IEntity>()));

    // True when the member holds instead of walking toward the task's target out of reach. The game's move step runs for a target in
    // reach too, so the reach comes first (GroupAiChaseRules.Hold).
    internal static bool Holds(UnitEntity owner, ActCombatBehaviourTask task)
    {
        if (!Steered(owner) || task == null || task.TargetEntity == null || task.Skill == null) return false;
        var guid = owner.Guid.ToString();
        if (GroupAiRoles.Known(guid) is not GroupAiRole role) return false;
        var inMap = CoreGame.InMap;
        var middle = Middle();
        if (inMap == null || middle == null) return false;
        var target = task.TargetEntity;
        var range = SkillRange(task.Skill.Key);
        var at = inMap.GetLogicalPosition(target.Guid);
        var from = inMap.GetLogicalPosition(owner.Guid);
        var distance = GroupAiRules.Distance(from.x, from.y, at.x, at.y);
        var inReach = GroupAiChaseRules.InReach(distance, range);
        var fromMiddle = GroupAiRules.Distance(at.x, at.y, middle.Value.X, middle.Value.Y);
        var ordered = Ordered(owner, target);
        var reason = GroupAiChaseRules.Hold(role, IsFoe(target), range, fromMiddle, chaseCells, inReach, ordered);
        if (reason == null)
        {
            if (!inReach) Note(guid, role.ToString(), "walk", ordered ? GroupAiChaseRules.OrderReason : null, fromMiddle);
            return false;
        }
        Note(guid, role.ToString(), "hold", reason, fromMiddle);
        Refuse(guid, role.ToString(), task.Skill.Key, target.Guid.ToString(), reason, distance, range, fromMiddle);
        return true;
    }

    private static bool IsFoe(IEntity target)
    {
        var unit = target.TryCast<UnitEntity>();
        return (unit != null && unit.Faction == FactionType.Enemy) || CoreFaction.IsBandit(target);
    }

    private static void Refuse(string guid, string role, string skill, string target, string reason, int distance, int range, int fromMiddle)
    {
        var now = GameClock.Now;
        if (!refusals.TryGetValue(guid, out var refusal)) refusals[guid] = refusal = new CastRefusal();
        refusal.Skill = skill;
        refusal.Target = target;
        refusal.At = now;
        refusal.Stuck = reason == GroupAiChaseRules.StuckReason;
        if (!reports.TryGetValue(guid, out var report) || report.Skill != skill || report.Target != target || report.Reason != reason)
            reports[guid] = report = new RefusalReport { Skill = skill, Target = target, Reason = reason, At = float.NegativeInfinity };
        report.Count++;
        if (now >= report.At && now - report.At < GroupAiChaseRules.RefusalReportSeconds) return;
        if (refusalLines++ < GroupAiChaseRules.LogCap)
            GroupAiPolicy.Log(GroupAiChaseRules.RefusalLine(guid, role, skill, target, reason, distance, range, fromMiddle, report.Count,
                CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        report.At = now;
        report.Count = 0;
    }

    // A player's order on a party unit (GroupAiDirectOrderPatch, Core's own tasks excluded there): an attack task on a foe becomes the
    // member's order, any other task ends it. Whatever was refused on the ordered target before goes.
    internal static void NoteOrder(UnitEntity unit, TaskBase task)
    {
        if (unit == null) return;
        var guid = unit.Guid.ToString();
        var attack = task?.TryCast<ActCombatBehaviourTask>();
        var target = attack?.TargetEntity;
        var ordered = GroupAiChaseRules.OrderTarget(true, attack != null, target != null && IsFoe(target), target?.Guid.ToString());
        orders.TryGetValue(guid, out var current);
        Keep(guid, GroupAiChaseRules.NextOrder(current, true, ordered, false, false, false));
        if (ordered == null) return;
        if (refusals.TryGetValue(guid, out var refusal) && refusal.Target == ordered) refusals.Remove(guid);
        watches.Remove(guid);
    }

    // Every game tick for a unit the player gave orders (GroupAiStance.OnGameTick): its combat and the end of its fight.
    internal static void NoteFight(string unit, bool inCombat, bool fightOver)
    {
        if (unit != null && orders.TryGetValue(unit, out var current))
            Keep(unit, GroupAiChaseRules.NextOrder(current, false, null, false, inCombat, fightOver));
    }

    // A skill's range from the game's skill table, 1 when unknown (Holds, GroupAiFormation's reach checks).
    internal static int SkillRange(string key) => DataSheetManager.Instance?.GetSkillData(key)?.Range ?? 1;

    // Whether a player's attack order of the member stands, without looking up its target (GroupAiFormation, GroupAiRoleMoves;
    // OrderedTarget drops an order on a dead target at the formation's next look).
    internal static bool HasOrder(Guid unit) => orders.Count > 0 && orders.ContainsKey(unit.ToString("D"));

    // The foe the player ordered this member at, while it lives; null for none.
    internal static IEntity OrderedTarget(UnitEntity unit)
    {
        if (unit == null) return null;
        var guid = unit.Guid.ToString();
        if (!orders.TryGetValue(guid, out var order)) return null;
        var foe = FoeOf(order.Target);
        var next = GroupAiChaseRules.NextOrder(order, false, null, foe == null || foe.IsDead, false, false);
        Keep(guid, next);
        return next == null ? null : foe.Cast<IEntity>();
    }

    // The ordered foe's unit, looked up once a tick.
    private static UnitEntity FoeOf(string target)
    {
        var tick = Tick();
        if (tick != foesTick)
        {
            foes.Clear();
            foesTick = tick;
        }
        if (tick >= 0 && foes.TryGetValue(target, out var known)) return known;
        var foe = Guid.TryParse(target, out var foeGuid) ? CoreGame.FindUnit(foeGuid) : null;
        if (tick >= 0) foes[target] = foe;
        return foe;
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? -1;

    internal static bool Ordered(UnitEntity unit, IEntity target)
    {
        if (orders.Count == 0 || unit == null || target == null) return false;
        var tick = Tick();
        if (tick != orderedTick)
        {
            orderedSeen.Clear();
            orderedTick = tick;
        }
        var key = (unit.Pointer, target.Pointer);
        if (tick >= 0 && orderedSeen.TryGetValue(key, out var seen)) return seen;
        var ordered = OrderedTarget(unit) != null && orders.TryGetValue(unit.Guid.ToString(), out var order)
            && GroupAiChaseRules.Ordered(order, target.Guid.ToString());
        if (tick >= 0) orderedSeen[key] = ordered;
        return ordered;
    }

    private static void Keep(string unit, PlayerOrder order)
    {
        orders.TryGetValue(unit, out var before);
        if (!ReferenceEquals(before, order)) orderedSeen.Clear();
        if (order == null) orders.Remove(unit);
        else orders[unit] = order;
    }

    // The member's walk toward this target was held within CancelSeconds: the target choice takes one in reach instead.
    internal static bool Cancelled(UnitEntity unit, IEntity target)
        => unit != null && target != null && refusals.TryGetValue(unit.Guid.ToString(), out var refusal)
           && GroupAiChaseRules.Fresh(refusal, GameClock.Now) && refusal.Target == target.Guid.ToString();

    // The skill was refused within CancelSeconds (GroupAiPriority: a priority cast waits like a held member's).
    internal static bool SkillRefused(string unit, string skill)
        => refusals.TryGetValue(unit, out var refusal) && GroupAiChaseRules.SkillRefused(refusal, skill, GameClock.Now);

    // Whether a skill of this range may not go at this foe now (GroupAiFocus, GroupAiPolicy.Target).
    internal static bool Blocks(string unit, string target, int distance, int range)
        => refusals.TryGetValue(unit, out var refusal)
           && GroupAiChaseRules.Blocks(refusal, target, GroupAiChaseRules.InReach(distance, range), GameClock.Now);

    // Before the rules: a refused skill that reaches no other foe is not free for this decision, on any of the ways a skill is chosen.
    internal static void MarkRefused(GroupAiSituation s)
    {
        if (s == null || !refusals.TryGetValue(s.Guid, out var refusal)) return;
        var slot = s.Find(refusal.Skill);
        if (slot == null || !slot.Free || !GroupAiChaseRules.SkillRefused(refusal, slot.Key, GameClock.Now)) return;
        var foes = new List<(string Guid, int Distance)>();
        foreach (var enemy in s.Enemies) foes.Add((enemy.Guid, enemy.Distance));
        if (GroupAiChaseRules.SlotRefused(refusal, slot.Key, slot.Data?.Range ?? 1, foes, GameClock.Now)) slot.State = GroupAiChaseRules.RefusedState;
    }

    // After every retarget of a decision: the choice on a foe, counted while it comes back unchanged (GroupAiChaseRules.Step). Only a
    // skill with a cooldown counts: one that went off is not chosen again until that ends.
    internal static void NoteChoice(UnitEntity owner, ActiveSkill_Base skill, IEntity target)
    {
        if (!Steered(owner) || skill == null || target == null || !IsFoe(target) || Ordered(owner, target)) return;
        if ((GroupAiRoles.Sheet(skill.Key)?.Cooldown ?? 0f) <= 0f || CoreGame.InMap is not { } inMap) return;
        var guid = owner.Guid.ToString();
        var from = inMap.GetLogicalPosition(owner.Guid);
        watches.TryGetValue(guid, out var watch);
        watches[guid] = watch = GroupAiChaseRules.Step(watch, skill.Key, target.Guid.ToString(), (from.x, from.y), GameClock.Now);
        if (!GroupAiChaseRules.Stuck(watch)) return;
        watches.Remove(guid);
        var at = inMap.GetLogicalPosition(target.Guid);
        var middle = Middle();
        Refuse(guid, GroupAiRoles.Known(guid)?.ToString(), skill.Key, target.Guid.ToString(), GroupAiChaseRules.StuckReason,
            GroupAiRules.Distance(from.x, from.y, at.x, at.y), skill.Range, middle == null ? -1 : GroupAiRules.Distance(at.x, at.y, middle.Value.X, middle.Value.Y));
    }

    // The idle target of a member without a formation: its own cell, or the group's middle when it stands too far off; null leaves
    // the game's wander (no group AI).
    internal static (int X, int Y)? IdleTarget(UnitEntity owner, IInMapDataSupplier inMap)
    {
        if (!Steered(owner) || inMap == null) return null;
        var at = inMap.GetLogicalPosition(owner.Guid);
        var self = (at.x, at.y);
        var middle = Middle();
        if (middle == null) return self;
        var gather = GroupAiChaseRules.Gather(self, middle.Value, gatherCells);
        var guid = owner.Guid.ToString();
        if (gather is { } to)
        {
            if (gathering.Add(guid) && lines++ < GroupAiChaseRules.LogCap)
                GroupAiPolicy.Log(GroupAiChaseRules.GatherLine(guid, self, to, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
            return to;
        }
        gathering.Remove(guid);
        return self;
    }

    // The middle of the standing party members on the shown map, once a tick.
    private static (int X, int Y)? Middle()
    {
        var inMap = CoreGame.InMap;
        if (inMap == null) return null;
        var tick = Tick();
        if (tick >= 0 && tick == middleTick) return middleCell;
        var cells = middleCells;
        cells.Clear();
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead || TacticalFormation.IsDown(unit) || Summoned(unit)) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            cells.Add((at.x, at.y));
        }
        middleCell = GroupAiChaseRules.Middle(cells);
        middleTick = tick;
        return middleCell;
    }

    private static void Note(string guid, string role, string step, string reason, int fromMiddle)
    {
        var key = step + "|" + reason;
        if (lastStep.TryGetValue(guid, out var last) && last == key) return;
        lastStep[guid] = key;
        if (lines++ < GroupAiChaseRules.LogCap)
            GroupAiPolicy.Log(GroupAiChaseRules.ChaseLine(guid, role, step, reason, fromMiddle, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }
}
