using HarmonyLib;
using UnityEngine;
#if BEPINEX
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using TaskStatus = global::BehaviorDesigner.Runtime.Tasks.TaskStatus;
#else
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using TaskStatus = Il2CppBehaviorDesigner.Runtime.Tasks.TaskStatus;
#endif

namespace DungeonSettlersDelvers.Core;

// A decision of Core that is no skill (when our AI acts, the game's AI has no say): a dodge, Core's own walk, the
// healer's distance, the step into reach, the seed walk, a bandage, a role's walk or summon cast. The game asks for a skill and a target
// and lays the two on the unit (SetActCombatBehaviourTask), which must not get a null skill; so the game keeps its answer and Core stops
// only that last step. The node then fails, as the enemies' does with no pattern: on a success the tree's next node reads a fight task
// that is not there and throws. The closed gate (auto attack off, hold position), a field without foes and the
// global cooldown, where the game answers with the basic attack, stay the game's. Only DecideSkill marks, with the group AI on.
internal static class GroupAiEmptyMove
{
    private const float ReportSeconds = 10f;

    // The node deciding now: the game runs skill, target and task in one call.
    private static IntPtr marked;
    private static string markedCase = "";
    private static readonly Dictionary<string, int> counts = new();
    private static float reportAt;

    internal static void Clear() => marked = IntPtr.Zero;

    internal static void Mark(DecidePlayerCombatBehaviour decider, string reason)
    {
        marked = decider.Pointer;
        markedCase = reason;
    }

    internal static bool Marked(DecidePlayerCombatBehaviour decider) => marked != IntPtr.Zero && marked == decider.Pointer;

    internal static bool Take(DecidePlayerCombatBehaviour decider)
    {
        if (!Marked(decider)) return false;
        marked = IntPtr.Zero;
        counts[markedCase] = counts.TryGetValue(markedCase, out var count) ? count + 1 : 1;
        var now = Time.realtimeSinceStartup;
        if (now >= reportAt)
        {
            reportAt = now + ReportSeconds;
            DelversHost.Info("CORE_AI_EMPTY_MOVE " + string.Join(" ", counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
        }
        return true;
    }
}

// After every other prefix of this method: FalconOwnerDecidePlayer pairs its prefix with a finalizer.
[HarmonyPatch(typeof(DecidePlayerCombatBehaviour), "SetActCombatBehaviourTask")]
internal static class GroupAiEmptyMovePatch
{
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(DecidePlayerCombatBehaviour __instance, ref TaskStatus __result)
    {
        if (!GroupAiEmptyMove.Take(__instance)) return true;
        __result = TaskStatus.Failure;
        return false;
    }
}
