using DungeonSettlersDelvers.Core;
using static TestKit;

// Pack integrations: observers and policies by pack ID.
internal static class PackIntegrationSection
{
    internal static void Run()
    {
        var integrations = new PackIntegrationRegistry<string>();
        var observed = new List<string>();
        var alphaObserver = new Action<string>(value => observed.Add("alpha:" + value));
        var alphaPolicy = new Func<bool>(() => false);
        var betaSuppress = false;
        var betaObserver = new Action<string>(value => observed.Add("beta:" + value));
        var betaPolicy = new Func<bool>(() => betaSuppress);
        var alphaLease = integrations.Register("alpha", alphaObserver, alphaPolicy);
        var duplicateAlphaLease = integrations.Register("alpha", alphaObserver, alphaPolicy);
        var betaLease = integrations.Register("beta", betaObserver, betaPolicy);
        Check(integrations.Count == 2, "pack integration registration is keyed and idempotent");
        integrations.Notify("refresh", (_, _) => throw new InvalidOperationException("unexpected observer failure"));
        Check(observed.SequenceEqual(new[] { "alpha:refresh", "beta:refresh" }),
            "campaign refresh dispatches to every registered pack once");
        Check(!integrations.AnyPolicy((_, _) => throw new InvalidOperationException("unexpected policy failure")),
            "native-founder policies default to false when no pack requests suppression");
        betaSuppress = true;
        Check(integrations.AnyPolicy((_, _) => throw new InvalidOperationException("unexpected policy failure")),
            "native-founder suppression aggregates pack policies with any-true");
        var duplicatePackIdRejected = false;
        try { integrations.Register("beta", _ => { }, () => false); }
        catch (InvalidOperationException) { duplicatePackIdRejected = true; }
        Check(duplicatePackIdRejected, "a pack cannot replace another registration with the same ID");
        alphaLease.Dispose();
        alphaLease.Dispose();
        Check(integrations.Count == 2, "integration leases are idempotently disposed while references remain");
        duplicateAlphaLease.Dispose();
        Check(integrations.Count == 1, "a pack integration is removed after its last lease is released");
        betaLease.Dispose();
        Check(integrations.Count == 0 && !integrations.AnyPolicy((_, _) => { }),
            "released integrations leave no campaign observer or suppression policy");
        var failingPolicyLogCount = 0;
        var failingPolicyLease = integrations.Register("broken-policy", _ => { },
            () => throw new InvalidOperationException("expected test exception"));
        integrations.AnyPolicy((_, _) => failingPolicyLogCount++);
        integrations.AnyPolicy((_, _) => failingPolicyLogCount++);
        Check(failingPolicyLogCount == 1, "a failing pack policy is isolated and reported only once");
        failingPolicyLease.Dispose();
    }
}
