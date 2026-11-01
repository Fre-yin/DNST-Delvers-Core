using DungeonSettlersDelvers.Core;
using static TestKit;

// Pack predicates for unique candidates.
internal static class UniquePredicatesSection
{
    internal static void Run()
    {
        // A neutral example pack stands in for a character pack's own identity rule.
        const string examplePackProfile = "UNITVISUAL_ExamplePack_Hero";
        const string examplePackBackground = "AFFECTER_ExamplePackHeroBackground";
        var uniquePredicates = new UniqueCandidatePredicateRegistry<UniqueCandidateIdentity>();
        var examplePredicate = new Func<UniqueCandidateIdentity, bool>(candidate =>
            candidate.ProfileKey == examplePackProfile && candidate.BackgroundTrait == examplePackBackground);
        var exampleCandidate = new UniqueCandidateIdentity(examplePackProfile, "Mira", examplePackBackground);
        var exampleLease = uniquePredicates.Register("example-pack", examplePredicate);
        var duplicateExampleLease = uniquePredicates.Register("example-pack", examplePredicate);
        Check(uniquePredicates.Count == 1 && uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
            "pack predicate registration is idempotent and recognizes its character after a custom rename");
        var conflictingPredicateRejected = false;
        try { uniquePredicates.Register("example-pack", _ => false); }
        catch (InvalidOperationException) { conflictingPredicateRejected = true; }
        Check(conflictingPredicateRejected, "a package cannot replace its active unique-candidate predicate");
        exampleLease.Dispose();
        exampleLease.Dispose();
        Check(uniquePredicates.Count == 1, "duplicate predicate lease survives idempotent disposal of the first lease");
        duplicateExampleLease.Dispose();
        Check(uniquePredicates.Count == 0 && !uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
            "unique-candidate predicate is removed after its last lease is released");
        var predicateFailureLogs = new List<string>();
        var failingPredicateLease = uniquePredicates.Register("broken-pack",
            _ => throw new InvalidOperationException("expected test exception"));
        var exampleAfterFailureLease = uniquePredicates.Register("example-pack", examplePredicate);
        var ordinaryAfterFailure = uniquePredicates.IsUnique(OrdinaryLowellLikeCandidate(),
            (packId, _) => predicateFailureLogs.Add(packId));
        var exampleAfterFailure = uniquePredicates.IsUnique(exampleCandidate,
            (packId, _) => predicateFailureLogs.Add(packId));
        Check(!ordinaryAfterFailure && exampleAfterFailure
            && predicateFailureLogs.SequenceEqual(new[] { "broken-pack" }),
            "a failing pack predicate is logged once and does not block other candidates or packs");
        failingPredicateLease.Dispose();
        exampleAfterFailureLease.Dispose();
        var cleanupLease = uniquePredicates.Register("cleanup-pack", _ => true);
        Check(uniquePredicates.Count == 1, "active predicate is present before Core shutdown cleanup");
        uniquePredicates.Clear();
        Check(uniquePredicates.Count == 0 && !uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
            "Core shutdown cleanup clears active unique-candidate registrations");
        cleanupLease.Dispose();
        Check(uniquePredicates.Count == 0, "a lease disposed after Core cleanup is harmless");
    }
}
