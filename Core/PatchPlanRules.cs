namespace DungeonSettlersDelvers.Core;

// Patching the patch classes as a plan, rules without the game: every class is tried in the given order and a failure never
// stops the others. Each failure is handed to the caller as it happens (the log line), and the failures end up in one message
// that names every broken class and its target. What a failure means is the caller's decision: DelversCoreRuntime turns any
// failure into "Core is not ready" and takes everything back.
internal static class PatchPlanRules
{
    // One patch class that could not be patched: the class, the target it names (as the caller describes it) and the error.
    internal sealed class Failure
    {
        internal Failure(Type patchClass, string target, Exception error)
        {
            PatchClass = patchClass;
            Target = target;
            Error = error;
        }

        internal Type PatchClass { get; }
        internal string Target { get; }
        internal Exception Error { get; }
    }

    // Tries every class with patch, in the order of patchClasses. A class that throws gets its target described, is noted and
    // handed to failed at once; the next class is tried all the same. The result holds the failures in the same order.
    internal static List<Failure> Apply(IEnumerable<Type> patchClasses, Action<Type> patch,
        Func<Type, string> describeTarget, Action<Failure> failed)
    {
        var failures = new List<Failure>();
        foreach (var patchClass in patchClasses)
        {
            try { patch(patchClass); }
            catch (Exception error)
            {
                var failure = new Failure(patchClass, describeTarget(patchClass), error);
                failures.Add(failure);
                failed(failure);
            }
        }
        return failures;
    }

    // The message of the exception that stops the start: the number of failed classes and each one with its target.
    // Null when nothing failed.
    internal static string Message(IReadOnlyList<Failure> failures)
    {
        if (failures.Count == 0) return null;
        var entries = new List<string>();
        foreach (var failure in failures) entries.Add(failure.PatchClass.Name + " -> " + failure.Target);
        return failures.Count + " Harmony patch class(es) failed: " + string.Join("; ", entries);
    }
}
