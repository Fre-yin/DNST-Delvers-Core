using DungeonSettlersDelvers.Core;
using static TestKit;

// Patching the patch classes as a plan: a failing class does not stop the others, every failure stands in the message, and
// the runtime patches through the plan.
internal static class PatchPlanSection
{
    sealed class First { }

    sealed class Second { }

    sealed class Third { }

    static readonly Type[] Classes = { typeof(First), typeof(Second), typeof(Third) };

    internal static void Run()
    {
        TheFirstFailureStopsNothing();
        EveryFailureStandsInTheMessage();
        NothingFailingBreaksNothing();
        FailuresAreReportedWhenTheyHappen();
        TheTargetIsDescribedForFailuresOnly();
        TheRuntimePatchesThroughThePlan();
    }

    static string TargetOf(Type patchClass) => "Target" + patchClass.Name + "()";

    // Runs the plan; patch throws for the classes in failing. Returns the failures and what was attempted.
    static (List<PatchPlanRules.Failure> Failures, List<string> Attempted) Attempt(IEnumerable<Type> classes, params Type[] failing)
    {
        var attempted = new List<string>();
        var failures = PatchPlanRules.Apply(classes, type =>
        {
            attempted.Add(type.Name);
            if (failing.Contains(type)) throw new InvalidOperationException("broken " + type.Name);
        }, TargetOf, _ => { });
        return (failures, attempted);
    }

    // When the first class fails, the others are still tried (a failure used to be one among many, never a stop).
    static void TheFirstFailureStopsNothing()
    {
        var (failures, attempted) = Attempt(Classes, typeof(First));
        Check(attempted.SequenceEqual(new[] { "First", "Second", "Third" }),
            "patch plan: every class is tried in order although the first one fails");
        Check(failures.Count == 1 && failures[0].PatchClass == typeof(First),
            "patch plan: only the class that threw is a failure");
    }

    // Two broken classes: both in the message, in the order of the classes, with the short class name and the target.
    static void EveryFailureStandsInTheMessage()
    {
        var (failures, _) = Attempt(Classes, typeof(First), typeof(Third));
        Check(PatchPlanRules.Message(failures)
                == "2 Harmony patch class(es) failed: First -> TargetFirst(); Third -> TargetThird()",
            "patch plan: both failures stand in the message, counted, in the order of the classes");
        var (single, _) = Attempt(Classes, typeof(Second));
        Check(PatchPlanRules.Message(single) == "1 Harmony patch class(es) failed: Second -> TargetSecond()",
            "patch plan: a single failure is counted as one");
    }

    // Without a failure nothing stops the start and nothing is reported.
    static void NothingFailingBreaksNothing()
    {
        var reported = 0;
        var attempted = new List<string>();
        var failures = PatchPlanRules.Apply(Classes, type => attempted.Add(type.Name), TargetOf, _ => reported++);
        Check(failures.Count == 0 && PatchPlanRules.Message(failures) == null && reported == 0 && attempted.Count == 3,
            "patch plan: without a failure there is no message, no report and every class is patched");
        var (none, nothing) = Attempt(Array.Empty<Type>());
        Check(none.Count == 0 && nothing.Count == 0 && PatchPlanRules.Message(none) == null,
            "patch plan: no class, no failure and no message");
    }

    // The report comes right after the failing class and before the next one is tried, with the very exception that was thrown.
    static void FailuresAreReportedWhenTheyHappen()
    {
        var events = new List<string>();
        var errors = new List<Exception>();
        var thrown = new InvalidOperationException("broken First");
        PatchPlanRules.Apply(Classes, type =>
        {
            events.Add("patch " + type.Name);
            if (type == typeof(First)) throw thrown;
        }, TargetOf, failure =>
        {
            events.Add("report " + failure.PatchClass.Name + " " + failure.Target);
            errors.Add(failure.Error);
        });
        Check(events.SequenceEqual(new[] { "patch First", "report First TargetFirst()", "patch Second", "patch Third" }),
            "patch plan: a failure is reported at once, before the next class is patched");
        Check(errors.Count == 1 && ReferenceEquals(errors[0], thrown),
            "patch plan: the report carries the exception that was thrown");
    }

    // Describing a target can be costly (it reads the Harmony attributes); a class that patched fine is never described.
    static void TheTargetIsDescribedForFailuresOnly()
    {
        var described = new List<string>();
        PatchPlanRules.Apply(Classes, type =>
        {
            if (type != typeof(Second)) throw new InvalidOperationException("broken " + type.Name);
        }, type =>
        {
            described.Add(type.Name);
            return "t";
        }, _ => { });
        Check(described.SequenceEqual(new[] { "First", "Third" }),
            "patch plan: only the classes that failed get their target described");
    }

    // The runtime's PatchAllClasses hands the loop to the plan: no loop and no catch of its own, one log line in one place.
    static void TheRuntimePatchesThroughThePlan()
    {
        var core = FindCoreSourceDirectory();
        var path = core == null ? null : Path.Combine(core, "DelversCoreRuntimeLifecycle.cs");
        Check(path != null && File.Exists(path), "patch plan: the runtime's life cycle part stands in Core's sources");
        if (path == null || !File.Exists(path)) return;
        var source = File.ReadAllText(path);
        var body = MethodBody(source, "private static void PatchAllClasses(");
        Check(body != null && body.Contains("PatchPlanRules.Apply(", StringComparison.Ordinal)
                && body.Contains("PatchPlanRules.Message(", StringComparison.Ordinal)
                && !body.Contains("foreach", StringComparison.Ordinal) && !body.Contains("catch", StringComparison.Ordinal),
            "patch plan: PatchAllClasses patches through the plan, without a loop or a catch of its own");
        Check(CountOf(source, "CORE_PATCH_CLASS_FAILED class=") == 1,
            "patch plan: the failure line of a patch class is written in one place");
    }

    // The text from the opening brace after the signature to its closing brace; null when the method is not found.
    internal static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return null;
        var open = source.IndexOf('{', start);
        if (open < 0) return null;
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
        }
        return null;
    }

    static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
