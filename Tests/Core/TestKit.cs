using System.Collections;
using DungeonSettlersDelvers.Core;

// What every section shares: the check counter, the section runner, the summary line and the test doubles.
internal static class TestKit
{
    static int checks;
    static int failures;

    // A failed check is named and counted; the run goes on.
    internal static void Check(bool condition, string name)
    {
        checks++;
        if (condition)
        {
            Console.WriteLine("PASS: " + name);
            return;
        }
        failures++;
        Console.WriteLine("FAIL: " + name);
    }

    // A section that throws counts as one failed check; the next section still runs.
    internal static void RunSection(string name, Action run)
    {
        try { run(); }
        catch (Exception error)
        {
            Check(false, "section " + name + " ends without an exception (" + error.GetType().Name + ": " + error.Message + ")");
        }
    }

    // The last line of the run; the return value is the process exit code.
    internal static int Summarize()
    {
        if (failures == 0)
        {
            Console.WriteLine($"PASS: {checks} deterministic checks");
            return 0;
        }
        Console.WriteLine($"FAIL: {failures} of {checks} deterministic checks failed");
        return 1;
    }

    // A candidate with Lowell's profile and name but an ordinary background, so no founder.
    internal static UniqueCandidateIdentity OrdinaryLowellLikeCandidate() => new(
        UniqueCandidateRules.Lowell.ProfileKey, UniqueCandidateRules.Lowell.NameTextKey, "AFFECTER_BasicWarrior");

    internal static string FindCoreSourceDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Core");
            if (File.Exists(Path.Combine(candidate, "DungeonSettlersDelvers.Core.csproj"))) return candidate;
        }
        return null;
    }
}

sealed class TestDelversHost : IDelversHostServices
{
    internal TestDelversHost(string packageDirectory) => PackageDirectory = packageDirectory;

    public string PackageDirectory { get; }
    internal List<string> InfoMessages { get; } = new();
    internal List<string> AllMessages { get; } = new();
    public void Info(string message) { InfoMessages.Add(message); AllMessages.Add(message); }
    public void Warning(string message) => AllMessages.Add(message);
    public void Error(string message) => AllMessages.Add(message);
    public object StartCoroutine(IEnumerator routine) => throw new NotSupportedException();
    public void StopCoroutine(object handle) { }
}

sealed class TestEntity : IEntity
{
}

public static class TestHotbarSlots
{
    public static string[] ExtraItemKeys(string unit)
    {
        if (unit == "throw") throw new InvalidOperationException("test");
        return unit == "u1" ? new[] { BandageText.SalveItemKey, "" } : new string[0];
    }

    public static string[] ExtraItemKeys(Guid unit) => new[] { "", BandageText.HerbalItemKey };

    public static int ExtraItemCount(string unit) => 0;
}
