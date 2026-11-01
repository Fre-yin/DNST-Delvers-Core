using DungeonSettlersDelvers.Core;
using static TestKit;

// The shared lease registry base and Core's registry cleanup.
internal static class LeaseRegistrySection
{
    internal static void Run()
    {
        // Shared registry base (review, G1). The registry tests above stay unchanged.
        Check(PackIds.IsValid("example-pack-2") && !PackIds.IsValid("Example") && !PackIds.IsValid("a_b")
                && !PackIds.IsValid("") && !PackIds.IsValid(null),
            "one rule for pack IDs: lowercase letters, digits and hyphens");
        var shared = new LeaseRegistry<string, string>(DuplicateRegistration.ShareIfEqual,
            (existing, added) => existing == added, StringComparer.Ordinal);
        var sharedRevision = shared.Revision;
        var sharedFirst = shared.Register("pack", "same", _ => new InvalidOperationException("conflict"));
        var sharedSecond = shared.Register("pack", "same", _ => new InvalidOperationException("conflict"));
        var sharedConflict = false;
        try { shared.Register("pack", "different", existing => new InvalidOperationException("taken by " + existing)); }
        catch (InvalidOperationException ex) { sharedConflict = ex.Message == "taken by same"; }
        sharedFirst.Dispose();
        var sharedAfterFirst = shared.Count;
        sharedSecond.Dispose();
        Check(sharedConflict && sharedAfterFirst == 1 && shared.Count == 0 && shared.Revision == sharedRevision + 2,
            "ShareIfEqual counts equal registrations, rejects different ones with the registry's own message");
        var rejecting = new LeaseRegistry<string, string>(DuplicateRegistration.Reject, keys: StringComparer.Ordinal);
        var rejectingLease = rejecting.Register("pack", "one", _ => new InvalidOperationException("taken"));
        var rejectedAgain = false;
        try { rejecting.Register("pack", "one", _ => new InvalidOperationException("taken")); }
        catch (InvalidOperationException) { rejectedAgain = true; }
        Check(rejectedAgain && rejecting.Count == 1 && rejecting.Snapshot().SequenceEqual(new[] { "one" }),
            "Reject refuses even an equal second registration");
        rejectingLease.Dispose();
        var atomic = new LeaseRegistry<string, string>(DuplicateRegistration.Reject, keys: StringComparer.Ordinal);
        var refusedByValidation = false;
        try
        {
            atomic.RegisterAll(new[] { new KeyValuePair<string, string>("a", "1"), new KeyValuePair<string, string>("b", "2") },
                _ => throw new InvalidOperationException("refused"));
        }
        catch (InvalidOperationException) { refusedByValidation = true; }
        var atomicLease = atomic.RegisterAll(new[] { new KeyValuePair<string, string>("a", "1") }, null);
        var refusedOverlap = false;
        try
        {
            atomic.RegisterAll(new[] { new KeyValuePair<string, string>("c", "3"), new KeyValuePair<string, string>("a", "x") }, null);
        }
        catch (InvalidOperationException) { refusedOverlap = true; }
        Check(refusedByValidation && refusedOverlap && atomic.Count == 1 && !atomic.TryGet("c", out _),
            "RegisterAll adds all keys or none");
        atomicLease.Dispose();
        var concurrent = new LeaseRegistry<string, string>(DuplicateRegistration.ShareIfEqual,
            (existing, added) => existing == added, StringComparer.Ordinal);
        var raced = concurrent.Register("pack", "entry", _ => new InvalidOperationException("conflict"));
        var kept = concurrent.Register("pack", "entry", _ => new InvalidOperationException("conflict"));
        Parallel.For(0, 64, _ => raced.Dispose());
        Check(concurrent.Count == 1 && concurrent.TryGet("pack", out var stillThere) && stillThere == "entry",
            "a lease disposed from many threads at once releases exactly once");
        kept.Dispose();
        var trackedA = CoreRegistries.Track(new LeaseRegistry<string, string>(DuplicateRegistration.Reject, keys: StringComparer.Ordinal));
        var trackedB = CoreRegistries.Track(new DelversMenuRegistry());
        CoreRegistries.Track(trackedA);
        var trackedCountBefore = CoreRegistries.Count;
        var lateLease = trackedA.Register("pack", "entry", _ => new InvalidOperationException("taken"));
        trackedB.Register("pack", "Title", () => Array.Empty<IReadOnlyDictionary<string, object>>());
        CoreRegistries.ClearAll();
        lateLease.Dispose();
        Check(trackedA.Count == 0 && trackedB.Snapshot().Count == 0 && CoreRegistries.Count == trackedCountBefore,
            "registries announce themselves once, Core's shutdown clears every one, a later lease release is harmless");
    }
}
