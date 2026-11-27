using System.Reflection;

namespace DungeonSettlersDelvers.Core;

// The game side of Core's chance (CoreRandomRules). Amfi's DsmpBridge (mod id dsmp-bridge) is found softly, without a reference:
// with it present and RandShared true (a draw inside the multiplayer step) the value is the bridge's RandValue("delvers", stream),
// the same on every player; else the seeded fallback over the campaign, the game tick and the unit. CORE_RANDOM names each source
// and reason once a session.
internal static class CoreRandom
{
    private const string BridgeType = "DsmpBridge.Multiplayer";
    private static readonly Dictionary<uint, uint> counts = new();
    private static long countTick = long.MinValue;
    private static bool bridgeLooked;
    private static PropertyInfo bridgeShared;
    private static MethodInfo bridgeValue;
    // Each source and reason once a session, so a run that changes between shared and local draws does not fill the log.
    private static readonly HashSet<string> noted = new(StringComparer.Ordinal);

    internal static void ResetLifecycle()
    {
        counts.Clear();
        countTick = long.MinValue;
        noted.Clear();
    }

    // The same for a unit by its id as text, as the game's own Guid does not convert.
    internal static double Next01(uint stream, string unit) => Next01(stream, Guid.TryParse(unit, out var guid) ? guid : Guid.Empty);

    // A value in [0, 1) of the stream, for the unit the draw is about (Guid.Empty for none).
    internal static double Next01(uint stream, Guid unit)
    {
        if (TryBridge(stream, out var shared))
        {
            Note("bridge", "shared");
            return shared;
        }
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        if (tick != countTick)
        {
            counts.Clear();
            countTick = tick;
        }
        counts.TryGetValue(stream, out var count);
        counts[stream] = count + 1;
        Note("seed", bridgeValue == null ? "no-bridge" : "local");
        return CoreRandomRules.Value(CoreSaveStore.Current?.Campaign ?? Guid.Empty, tick, unit, stream, count);
    }

    private static bool TryBridge(uint stream, out double value)
    {
        value = 0;
        try
        {
            if (!bridgeLooked) LookForBridge();
            if (bridgeValue == null || bridgeShared?.GetValue(null) is not true) return false;
            var raw = bridgeValue.Invoke(null, new object[] { CoreRandomRules.ModId, stream });
            if (raw is not float shared || shared < 0f || shared >= 1f) return false;
            value = shared;
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_RANDOM_BRIDGE_FAILED: " + ex.Message);
            bridgeValue = null;
            return false;
        }
    }

    private static void LookForBridge()
    {
        bridgeLooked = true;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(BridgeType, false);
            if (type == null) continue;
            bridgeShared = type.GetProperty("RandShared", BindingFlags.Public | BindingFlags.Static);
            bridgeValue = type.GetMethod("RandValue", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(uint) }, null);
            if (bridgeShared == null || bridgeValue == null) bridgeValue = null;
            return;
        }
    }

    private static void Note(string source, string reason)
    {
        var line = CoreRandomRules.Line(source, reason);
        if (noted.Add(line)) DelversHost.Info(line);
    }
}
