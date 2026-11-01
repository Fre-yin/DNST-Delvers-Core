using DungeonSettlersDelvers.Core;
using static TestKit;

// The Core host binding: one owner, a second host is refused.
internal static class HostBindingSection
{
    internal static void Run()
    {
        var originalHost = new TestDelversHost("original-core-package");
        var duplicateHost = new TestDelversHost("duplicate-core-package");
        DelversHost.Bind(originalHost);
        try
        {
            var duplicateHostRejected = false;
            try { DelversHost.Bind(duplicateHost); }
            catch (InvalidOperationException) { duplicateHostRejected = true; }
            DelversHost.Unbind(duplicateHost);
            DelversHost.Info("original-host-still-active");
            Check(duplicateHostRejected && DelversHost.IsBound
                && DelversHost.PackageDirectory == "original-core-package"
                && originalHost.InfoMessages.SequenceEqual(new[] { "original-host-still-active" })
                && duplicateHost.InfoMessages.Count == 0,
                "second Core host is rejected without replacing or disabling the original");
        }
        finally
        {
            DelversHost.Unbind(duplicateHost);
            DelversHost.Unbind(originalHost);
        }
        Check(!DelversHost.IsBound, "the owning Core host can release its binding");
    }
}
