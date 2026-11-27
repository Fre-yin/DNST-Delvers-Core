using System.Reflection;
using HarmonyLib;
using UnityEngine;
#if BEPINEX
using CheckCombatState = global::Refactor.Task.CheckCombatState;
using DecideNonPlayerCombatBehaviour = global::Refactor.Task.DecideNonPlayerCombatBehaviour;
using EntityBrain = global::Refactor.Task.EntityBrain;
using TaskStatus = global::BehaviorDesigner.Runtime.Tasks.TaskStatus;
#else
using CheckCombatState = Il2CppRefactor.Task.CheckCombatState;
using DecideNonPlayerCombatBehaviour = Il2CppRefactor.Task.DecideNonPlayerCombatBehaviour;
using EntityBrain = Il2CppRefactor.Task.EntityBrain;
using TaskStatus = Il2CppBehaviorDesigner.Runtime.Tasks.TaskStatus;
#endif

namespace DungeonSettlersDelvers.Core;

// Core takes a summon over (when our AI acts, the game's AI has no say). Not tied to a side: the caller
// decides which summon Core steers (GroupAiSummonWalk for the party's; the bandits' may follow) and gives the cell. Hold sends one
// move order the NPC's way (GroupAiZones.OrderMove) for each new cell, and while the summon is held the game's own decisions for it
// are skipped (a run found where the game replaced Core's MoveTask in the same tick):
//   EntityBrain.SetPersonalTask: the idle the game sets once Core's order is in (out of a fight);
//   DecideNonPlayerCombatBehaviour.OnUpdate (SummonFollowOwnerPatch): the summon's own fight decision (in a fight). The node answers as
//   with no pattern, as for a bandit on Core's walk; the tree must not get past it, the next node reads the fight task and would throw.
//   CheckCombatState.OnUpdate (FalconOwnerCheckCombatState): out of a fight the summon's tree has no branch that walks a MoveTask, only
//   the idle; a held summon answers "in a fight" and takes the fight branch, which walks it behind SetMoveIfAvailable (CoreUnitMovePatch).
// Arrived, the summon stands on its cell without a task until Core lets it go (Release); no order every tick. Stopped short of its
// cell, it is sent again like a member (GroupAiFormationRules.SummonSends). CORE_SUMMON_HOLD on hold, a new cell, a repeat (again=1)
// and release.
// All three step out first with nothing held, and on a plain set of the held summons' brain pointers, without a call into the game. A tree node's brain is asked
// once and kept until the next release or a newly held brain: a freed node's
// pointer may come back as another unit's node, also as a node of a summon held next. CORE_SUMMON_HOLD_COST every CostSeconds: calls a second and the skipped ones.
internal static class SummonControl
{
    private const int LineCap = 200;
    private const float CostSeconds = 10f;
    private static readonly Dictionary<string, (UnitEntity Unit, Vector2Int Cell, string Reason, IntPtr Brain, float SentAt)> held = new(StringComparer.Ordinal);
    private static readonly HashSet<IntPtr> heldBrains = new();
    private static readonly Dictionary<IntPtr, IntPtr> nodeBrains = new();
    private static long calls, skipped;
    private static float costSince;
    private static bool installed;
    private static int lines;

    internal static void Reset()
    {
        held.Clear();
        heldBrains.Clear();
        nodeBrains.Clear();
        lines = 0;
        calls = skipped = 0;
        costSince = GameClock.Now;
    }

    // From each look: the cost line once every CostSeconds while the hooks are in.
    internal static void Tally(float now)
    {
        if (!installed || now - costSince < CostSeconds) return;
        var seconds = Math.Max(0.001f, now - costSince);
        Note(string.Format(System.Globalization.CultureInfo.InvariantCulture, "CORE_SUMMON_HOLD_COST calls={0} persec={1:0.0} skipped={2} held={3}",
            calls, calls / seconds, skipped, held.Count));
        calls = skipped = 0;
        costSince = now;
    }

    // Whether Core holds any summon: the hooks' first, free question.
    internal static bool Any => held.Count != 0;

    internal static bool Holds(IEntity unit) => unit != null && held.ContainsKey(unit.Guid.ToString());

    // The summon to the cell: one order for each new cell; on the cell it is held on again only once it stopped short of it.
    internal static void Hold(UnitEntity summon, Vector2Int cell, string reason)
    {
        if (summon == null || summon.IsDead) return;
        if (!installed) installed = Install();
        if (!installed) return;
        var guid = summon.Guid.ToString();
        var at = CoreGame.Position(summon);
        var again = held.TryGetValue(guid, out var now) && now.Cell == cell;
        if (!GroupAiFormationRules.SummonSends(again, (at.x, at.y), (cell.x, cell.y), GroupAiMoveWatch.Holds(summon), GameClock.Now - now.SentAt))
            return;
        var brain = now.Brain != IntPtr.Zero ? now.Brain
            : CoreGame.TickHandler(CoreGame.CurrentMap)?._taskSystem?._performers?.GetEntityBrain(summon.Cast<IEntity>())?.Pointer ?? IntPtr.Zero;
        if (brain == IntPtr.Zero) return;
        held[guid] = (summon, cell, reason, brain, GameClock.Now);
        // A node cached for a unit not held may since be a node of this summon.
        if (heldBrains.Add(brain)) nodeBrains.Clear();
        var sent = at == cell || GroupAiZones.OrderMove(summon, cell, reason);
        Note($"CORE_SUMMON_HOLD unit={guid} action=hold reason={reason} from={at.x},{at.y} cell={cell.x},{cell.y} sent={(sent ? 1 : 0)} again={(again ? 1 : 0)}");
    }

    // Lets the summon go back to the game.
    internal static void Release(IEntity summon, string why)
    {
        if (summon == null || !held.Remove(summon.Guid.ToString(), out var entry)) return;
        heldBrains.Remove(entry.Brain);
        nodeBrains.Clear();
        Note($"CORE_SUMMON_HOLD unit={summon.Guid} action=release reason={why}");
    }

    // Every held summon not kept by keep is let go (a look that no longer wants it); dead ones go too.
    internal static void ReleaseAllBut(HashSet<string> keep, string why)
    {
        foreach (var guid in new List<string>(held.Keys))
        {
            var entry = held[guid];
            if (keep.Contains(guid) && !entry.Unit.IsDead) continue;
            held.Remove(guid);
            heldBrains.Remove(entry.Brain);
            nodeBrains.Clear();
            Note($"CORE_SUMMON_HOLD unit={guid} action=release reason={(entry.Unit.IsDead ? "dead" : why)}");
        }
    }

    private static bool Install()
    {
        try
        {
            var harmony = new Harmony(DelversCoreRuntime.HarmonyOwner);
            var count = 0;
            foreach (var method in typeof(EntityBrain).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.IsGenericMethod || method.GetParameters().Length == 0 || method.Name != "SetPersonalTask") continue;
                harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(SummonControl), nameof(BeforePersonal))));
                count++;
            }
            DelversHost.Info($"CORE_SUMMON_HOLD installed methods={count}");
            return count > 0;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SUMMON_HOLD_FAILED: " + ex.Message);
            return false;
        }
    }

    // The game's idle for a held summon is skipped.
    private static bool BeforePersonal(EntityBrain __instance)
    {
        if (heldBrains.Count == 0) return true;
        calls++;
        if (!heldBrains.Contains(__instance.Pointer)) return true;
        skipped++;
        return false;
    }

    // A held summon takes the fight branch, also out of a fight: false skips the game's check.
    internal static bool CheckRuns(CheckCombatState node, ref TaskStatus result)
    {
        if (heldBrains.Count == 0) return true;
        calls++;
        try
        {
            if (!nodeBrains.TryGetValue(node.Pointer, out var brain))
                nodeBrains[node.Pointer] = brain = node.EntityBrain?.Value?.Pointer ?? IntPtr.Zero;
            if (!heldBrains.Contains(brain)) return true;
            skipped++;
            result = TaskStatus.Success;
            return false;
        }
        catch (Exception) { return true; }
    }

    // The game's fight decision for a held summon gives nothing: false skips the node.
    internal static bool DecisionRuns(DecideNonPlayerCombatBehaviour node, ref TaskStatus result)
    {
        if (heldBrains.Count == 0) return true;
        calls++;
        try
        {
            if (!nodeBrains.TryGetValue(node.Pointer, out var brain))
                nodeBrains[node.Pointer] = brain = node.EntityBrain?.Value?.Pointer ?? IntPtr.Zero;
            if (!heldBrains.Contains(brain)) return true;
            skipped++;
            result = TaskStatus.Failure;
            return false;
        }
        catch (Exception) { return true; }
    }

    private static void Note(string line)
    {
        if (lines++ < LineCap) DelversHost.Info(line + " tick=" + (CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }
}
