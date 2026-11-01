using System.Diagnostics;
using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Start analysis: some starts took over two minutes before the plugins loaded. Core only measures:
// the seconds since the game process started, when Core begins (at=init) and when it is ready (at=ready). Cold starts are
// watched with it; above about 30 s at init counts as unusual. Nothing in Windows or the loader is changed.
internal static class CoreStartup
{
    internal static string Line(string at, double seconds)
        => "CORE_STARTUP at=" + at + " sinceProcess="
            + (double.IsNaN(seconds) || seconds < 0 ? "-" : seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s");

    internal static void Log(string at)
    {
        double seconds;
        try
        {
            using var process = Process.GetCurrentProcess();
            seconds = (DateTime.Now - process.StartTime).TotalSeconds;
        }
        catch { seconds = double.NaN; }
        DelversHost.Info(Line(at, seconds));
    }
}
