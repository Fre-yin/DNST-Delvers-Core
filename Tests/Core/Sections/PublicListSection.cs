using static TestKit;

// The public file list of Core (Public/Core/DATEIEN.txt) against the files on disk, in both directions: every source,
// project, image and sound under Core, the Core adapters and the Core tests stands in the list, and every Core entry of
// the list has its file, so a gap shows before the files are published. Only the full source tree has the list; in the
// published tree, without Delvers.Canonical.props at its root, the comparison is reported as skipped.
internal static class PublicListSection
{
    private static readonly string[] Roots = { "Core", "Adapters/BepInEx/Core", "Adapters/Melon/Core", "Tests/Core" };
    private static readonly string[] Extensions = { ".cs", ".csproj", ".png", ".wav", ".json", ".txt" };

    internal static void Run()
    {
        Rule();
        DiskAgainstList();
    }

    // Files on disk that the list lacks, and list entries without a file; both sorted, as repository paths with '/'.
    internal static (List<string> Unlisted, List<string> Orphans) Compare(IEnumerable<string> onDisk, IEnumerable<string> listed)
    {
        var disk = new SortedSet<string>(onDisk, StringComparer.Ordinal);
        var list = new SortedSet<string>(listed, StringComparer.Ordinal);
        var unlisted = new List<string>();
        foreach (var file in disk)
            if (!list.Contains(file)) unlisted.Add(file);
        var orphans = new List<string>();
        foreach (var entry in list)
            if (!disk.Contains(entry)) orphans.Add(entry);
        return (unlisted, orphans);
    }

    // Whether a list entry belongs to the checked roots (the list also names the build script and other top files).
    internal static bool Covered(string path)
    {
        foreach (var root in Roots)
            if (path.StartsWith(root + "/", StringComparison.Ordinal)) return true;
        return false;
    }

    private static void Rule()
    {
        var (unlisted, orphans) = Compare(new[] { "Core/A.cs", "Core/New.cs" }, new[] { "Core/A.cs", "Core/Gone.cs" });
        Check(unlisted.SequenceEqual(new[] { "Core/New.cs" }) && orphans.SequenceEqual(new[] { "Core/Gone.cs" }),
            "public list: a new unlisted file and an entry without a file are both found");
        var (none, noOrphans) = Compare(new[] { "Core/A.cs" }, new[] { "Core/A.cs" });
        Check(none.Count == 0 && noOrphans.Count == 0 && Covered("Tests/Core/Program.cs") && Covered("Adapters/Melon/Core/CoreMod.cs")
                && !Covered("Build-Release.ps1") && !Covered("Frieren/FrierenRuntime.cs") && !Covered("CoreExtra/X.cs"),
            "public list: a matching list is clean, and only the Core roots are compared");
    }

    private static void DiskAgainstList()
    {
        var core = FindCoreSourceDirectory();
        var repository = core == null ? null : Path.GetDirectoryName(core);
        if (repository != null && !File.Exists(Path.Combine(repository, "Delvers.Canonical.props")))
        {
            Check(true, "public list: skipped (public export)");
            return;
        }
        var listFile = repository == null ? null : Path.Combine(repository, "Public", "Core", "DATEIEN.txt");
        if (listFile == null || !File.Exists(listFile))
        {
            Check(false, "public list: Public/Core/DATEIEN.txt found");
            return;
        }
        var onDisk = new List<string>();
        foreach (var root in Roots)
        {
            var folder = Path.Combine(repository, root.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var path = Path.GetRelativePath(repository, file).Replace('\\', '/');
                if (path.Contains("/bin/", StringComparison.Ordinal) || path.Contains("/obj/", StringComparison.Ordinal)) continue;
                if (Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) < 0) continue;
                onDisk.Add(path);
            }
        }
        var listed = new List<string>();
        foreach (var line in File.ReadAllLines(listFile))
        {
            var entry = line.Trim();
            if (entry.Length > 0 && Covered(entry)) listed.Add(entry);
        }
        var (unlisted, orphans) = Compare(onDisk, listed);
        Check(unlisted.Count == 0, "public list: every Core, Core adapter and Core test file stands in DATEIEN.txt"
            + (unlisted.Count == 0 ? "" : " (missing: " + string.Join(", ", unlisted) + ")"));
        Check(orphans.Count == 0, "public list: every Core entry of DATEIEN.txt has its file"
            + (orphans.Count == 0 ? "" : " (no file: " + string.Join(", ", orphans) + ")"));
    }
}
