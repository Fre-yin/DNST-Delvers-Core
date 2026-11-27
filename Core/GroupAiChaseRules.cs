using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The group AI's movement (where Core steers, the game has no say; values approved by the), only for party members with the group AI and
// never against a player's order:
// - The walk toward a skill's target out of reach: melee and shield walk while the target stands at most ChaseCells from the group's
//   middle; ranged members, casters and healers walk only into their skill's reach and never for a skill of reach 1 (no walk into
//   melee). "The healer never walks toward foes" is read as never into melee: a heal role also places its seed on the focus and casts
//   its own spells (held for every walk, the water mage of the support role and the seed at the focus
//   stopped). A walk toward a comrade stays as before. Only a walk is held: a target in the skill's reach needs none, and the game's
//   move step runs for it too (the rain came the moment the target's fromMiddle fell to 6).
// - A held cast counts as refused for CancelSeconds: the skill and the target leave the next decisions, so the choice does not come back
//   to a move the hold cancels (GroupAiChase.MarkRefused, Blocks). The same when one choice comes back for StuckSeconds without a cast,
//   whatever stopped it: at least StuckDecisions decisions, none StuckGapSeconds apart, on one cell. A running cast or channel makes no
//   decisions, so the gap ends its count, and a skill without a cooldown is left out, as it can rightly repeat.
// - A player's attack order goes first (the group stands, the player orders the advance): its target is never
//   held, for any reason, until the target dies, the player orders anew or the fight the member fought ends. The order is the attack
//   task the player hands a member (TaskSystem.DirectOrder: a right-click on a foe, or a skill and then the foe; Core's own tasks
//   excluded). A forced target alone is no order: the game sets one only by a taunt (AFFECTER_Taunt with SKILL_GuardiansTaunt,
//   SKILL_VileInsult and SKILL_ChainGrap), never by the player's input.
// - The idle wander without a formation: the member stands, unless it is more than GatherCells from the group's middle; then its
//   idle target is the middle.
// Rules without the game.
internal static class GroupAiChaseRules
{
    internal const string ChaseKey = "groupai.chase.cells";
    internal const string GatherKey = "groupai.gather.cells";
    internal const double ChaseDefault = 6.0;
    internal const double GatherDefault = 4.0;
    internal const float CancelSeconds = 3f;
    internal const int StuckDecisions = 8;
    internal const float StuckSeconds = 2f;
    internal const float StuckGapSeconds = 1f;
    internal const string StuckReason = "stuck";
    // The state of a refused skill's slot: the rules take only free slots.
    internal const string RefusedState = "refused";
    internal const float RefusalReportSeconds = 10f;
    internal const string OrderReason = "order";
    internal const int LogCap = 300;

    internal static int Cells(double? setting, double fallback)
        => (int)Math.Round(setting is double value && !double.IsNaN(value) ? Math.Clamp(value, 1.0, 20.0) : fallback);

    // The game's own reach, without Core's extra cell (GroupAiRules.InRange).
    internal static bool InReach(int distance, int skillRange) => distance <= Math.Max(1, skillRange);

    // Why the member holds instead of walking toward the target, or null when it may walk. fromMiddle: the target's distance to the
    // group's middle (cells); inReach: the member's skill reaches the target from where it stands.
    internal static string Hold(GroupAiRole role, bool targetIsFoe, int skillRange, int fromMiddle, int chaseCells, bool inReach)
    {
        // A comrade as the target (a heal, a buff): the walk stays as before.
        if (!targetIsFoe || inReach) return null;
        if (role is GroupAiRole.Ranged or GroupAiRole.Caster or GroupAiRole.Heal && skillRange <= 1)
            return role == GroupAiRole.Heal ? "healer-melee" : "ranged-melee";
        return fromMiddle > chaseCells ? "far" : null;
    }

    // The hold with the player's order: a target the player ordered the member at is never held.
    internal static string Hold(GroupAiRole role, bool targetIsFoe, int skillRange, int fromMiddle, int chaseCells, bool inReach, bool ordered)
        => ordered ? null : Hold(role, targetIsFoe, skillRange, fromMiddle, chaseCells, inReach);

    // The target of a player's order: an attack task the player handed the member, on a foe; null for anything else.
    internal static string OrderTarget(bool directOrder, bool attackTask, bool targetIsFoe, string target)
        => directOrder && attackTask && targetIsFoe && !string.IsNullOrEmpty(target) ? target : null;

    // A member's order after one step: a new order of the player replaces it (a move or other task ends it), the target's death ends
    // it, and so does the end of a fight the member fought since the order.
    internal static PlayerOrder NextOrder(PlayerOrder current, bool newOrder, string newTarget, bool targetGone, bool inCombat, bool fightOver)
    {
        if (newOrder) return newTarget == null ? null : new PlayerOrder { Target = newTarget };
        if (current == null || targetGone || (fightOver && current.Fought)) return null;
        if (inCombat) current.Fought = true;
        return current;
    }

    internal static bool Ordered(PlayerOrder order, string target) => order != null && target != null && order.Target == target;

    // The idle target: null to stay on its own cell, else the middle the member walks back to.
    internal static (int X, int Y)? Gather((int X, int Y) self, (int X, int Y) middle, int gatherCells)
        => GroupAiRules.Distance(self.X, self.Y, middle.X, middle.Y) > gatherCells ? middle : null;

    // The middle of the standing members: their mean cell, rounded.
    internal static (int X, int Y)? Middle(IReadOnlyCollection<(int X, int Y)> cells)
    {
        if (cells == null || cells.Count == 0) return null;
        long x = 0, y = 0;
        foreach (var cell in cells)
        {
            x += cell.X;
            y += cell.Y;
        }
        return ((int)Math.Round((double)x / cells.Count), (int)Math.Round((double)y / cells.Count));
    }

    internal static bool Fresh(CastRefusal refusal, float now) => refusal != null && now >= refusal.At && now - refusal.At <= CancelSeconds;

    internal static bool SkillRefused(CastRefusal refusal, string skill, float now) => Fresh(refusal, now) && refusal.Skill == skill;

    // Whether a skill may not go at this foe: it was refused, and for a held walk only while the skill still does not reach it.
    internal static bool Blocks(CastRefusal refusal, string target, bool inReach, float now)
        => Fresh(refusal, now) && refusal.Target == target && (refusal.Stuck || !inReach);

    // Whether a refused skill leaves the decision: unless it reaches a foe other than the refused one.
    internal static bool SlotRefused(CastRefusal refusal, string skill, int range, IEnumerable<(string Guid, int Distance)> foes, float now)
    {
        if (!SkillRefused(refusal, skill, now)) return false;
        foreach (var (guid, distance) in foes)
            if (guid != refusal.Target && InReach(distance, range)) return false;
        return true;
    }

    // The watch of one member after a choice of a skill with a cooldown: counted on while nothing but the time changed.
    internal static CastWatch Step(CastWatch watch, string skill, string target, (int X, int Y) cell, float now)
    {
        if (watch == null || watch.Skill != skill || watch.Target != target || watch.Cell != cell || now < watch.Last || now - watch.Last > StuckGapSeconds)
            return new CastWatch { Skill = skill, Target = target, Cell = cell, Since = now, Last = now, Decisions = 1 };
        watch.Last = now;
        watch.Decisions++;
        return watch;
    }

    internal static bool Stuck(CastWatch watch) => watch != null && watch.Decisions >= StuckDecisions && watch.Last - watch.Since >= StuckSeconds;

    internal static string RefusalLine(string unit, string role, string skill, string target, string reason, int distance, int range, int fromMiddle, int count, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_CAST_REJECTED unit={0} role={1} skill={2} target={3} reason={4} distance={5} range={6} fromMiddle={7} count={8} tick={9}",
            unit ?? "-", role ?? "-", skill ?? "-", target ?? "-", reason ?? "-", distance, range, fromMiddle, count, tick);

    internal static string ChaseLine(string unit, string role, string step, string reason, int fromMiddle, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_CHASE unit={0} role={1} step={2} reason={3} fromMiddle={4} tick={5}",
            unit ?? "-", role ?? "-", step, reason ?? "-", fromMiddle, tick);

    internal static string GatherLine(string unit, (int X, int Y) from, (int X, int Y) to, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_IDLE_GATHER unit={0} from={1},{2} to={3},{4} tick={5}",
            unit ?? "-", from.X, from.Y, to.X, to.Y, tick);
}

// A player's attack order on one member (GroupAiChaseRules.NextOrder): Fought once the member was in combat since.
internal sealed class PlayerOrder
{
    internal string Target = "";
    internal bool Fought;
}

// A cast Core called off or found stuck: Stuck blocks the target even in reach, as its cause is not the reach.
internal sealed class CastRefusal
{
    internal string Skill = "";
    internal string Target = "";
    internal float At;
    internal bool Stuck;
}

// One member's last choice of a skill with a cooldown, by GroupAiChaseRules.Step.
internal sealed class CastWatch
{
    internal string Skill = "";
    internal string Target = "";
    internal (int X, int Y) Cell;
    internal float Since;
    internal float Last;
    internal int Decisions;
}

// The tasks Core sent itself and that have not passed the game's DirectOrder yet (GroupAiDirectOrderPatch): each pointer is taken
// out when the order passes, so a second order of Core in the same tick is never read as the player's, and a pointer the game
// frees and hands out again for a player's task is no longer known as Core's. At most Cap kept; the oldest goes first. A pointer
// whose order never passed (its unit died first, the game dropped the event) goes after MaxAgeSeconds of game time, so a pointer the
// game hands out again later is never Core's; an order passes in the next tick.
internal sealed class OwnTasks
{
    internal const int Cap = 16;
    internal const float MaxAgeSeconds = 1f;
    private readonly List<(IntPtr Task, float At)> pointers = new();

    internal int Count => pointers.Count;

    internal void Add(IntPtr task, float now)
    {
        if (task == IntPtr.Zero) return;
        pointers.RemoveAll(entry => entry.Task == task);
        if (pointers.Count >= Cap) pointers.RemoveAt(0);
        pointers.Add((task, now));
    }

    // Whether the task is Core's own; a known one is taken out, and the ones too old to pass any more go first.
    internal bool Take(IntPtr task, float now)
    {
        if (pointers.Count == 0) return false;
        pointers.RemoveAll(entry => now < entry.At || now - entry.At > MaxAgeSeconds);
        if (task == IntPtr.Zero) return false;
        var index = pointers.FindIndex(entry => entry.Task == task);
        if (index < 0) return false;
        pointers.RemoveAt(index);
        return true;
    }

    internal void Clear() => pointers.Clear();
}
