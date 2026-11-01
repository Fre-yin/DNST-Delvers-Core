using DungeonSettlersDelvers.Core;
using static TestKit;

// The start analysis line.
internal static class CoreStartupSection
{
    internal static void Run()
    {
        // Start analysis: Core only measures the seconds since the process started.
        Check(CoreStartup.Line("ready", 12.345) == "CORE_STARTUP at=ready sinceProcess=12.3s"
                && CoreStartup.Line("init", double.NaN) == "CORE_STARTUP at=init sinceProcess=-"
                && CoreStartup.Line("init", -1) == "CORE_STARTUP at=init sinceProcess=-",
            "startup line: seconds since the process started, one decimal, '-' when unknown");
    }
}
