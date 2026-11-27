using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The AI's health lines with a look ahead and a buffer, one rule for the party's group AI, the bandits and
// the formation's roles (GroupAiLow on the game side):
// - Look ahead: from the damage of the last WindowSeconds, the loss per second; a line that the health would cross within
//   AheadSeconds counts as crossed already. Only with at least MinHits hits in the window, so a single hit makes no noise.
//   Net (the default): the heals in the window are taken off the damage, the bar's own course; a switch
//   counts damage only.
// - Buffer: a figure that was below a line counts as below for BufferSeconds after it rose above it again.
// The lines (LowLine): the emergency share 0.35 (GroupAiRules.EmergencyShare), the entry into a seed seeds.stay.below (0.30;
// the exit seeds.stay.until stays as it is, without look ahead or buffer), the roles' critical share 0.30 and, for the support water
// mage (GroupAiWaterRules), the sacrifice of a bubbleling with the wave below 0.50 and its bubblelings' guard below 0.70. Not for the normal
// heal below 0.9 or the taunt break, which stay fixed lines. Rules without the game.
internal enum LowLine
{
    Emergency,
    SeedStay,
    Critical,
    Sacrifice,
    Guard
}

internal static class GroupAiLowRules
{
    internal const string WindowKey = "groupai.low.window";
    internal const string HitsKey = "groupai.low.hits";
    internal const string AheadKey = "groupai.low.ahead";
    internal const string BufferKey = "groupai.low.buffer";
    internal const string NetKey = "groupai.low.net";
    internal const double WindowDefault = 3.0;
    internal const double HitsDefault = 2.0;
    internal const double AheadDefault = 2.0;
    internal const double BufferDefault = 2.0;
    internal const bool NetDefault = true;
    // The roles' critical share stays the roles' own value (GroupAiRoleRules).
    internal const float CriticalShare = GroupAiRoleRules.CriticalShare;
    internal const int LogCap = 200;

    // Below a line, a share is moved this far under it, so every site's own comparison (< or <=) sees it below.
    internal const float Under = 0.001f;

    internal readonly record struct Settings(float Window, int MinHits, float Ahead, float Buffer, bool Net);

    internal static Settings Read(double? window, double? hits, double? ahead, double? buffer, bool net)
        => new((float)Clamp(window, WindowDefault, 0.5, 10.0), (int)Math.Round(Clamp(hits, HitsDefault, 1.0, 10.0)),
            (float)Clamp(ahead, AheadDefault, 0.0, 10.0), (float)Clamp(buffer, BufferDefault, 0.0, 10.0), net);

    private static double Clamp(double? value, double fallback, double min, double max)
        => value is double v && !double.IsNaN(v) ? Math.Clamp(v, min, max) : fallback;

    internal static float Threshold(LowLine line, double seedBelow) => line switch
    {
        LowLine.Emergency => GroupAiRules.EmergencyShare,
        LowLine.SeedStay => (float)seedBelow,
        LowLine.Sacrifice => GroupAiWaterRules.SacrificeShare,
        LowLine.Guard => GroupAiWaterRules.GuardShare,
        _ => CriticalShare
    };

    // One change of a figure's health in the window: damage (positive) or a heal.
    internal readonly record struct Change(float At, float Amount, bool Heal);

    // The loss per second as a share of the figure's maximum, and the hits in the window; 0 without enough hits.
    internal static float LossPerSecond(IReadOnlyList<Change> changes, float now, Settings settings, float maxHealth, out int hits)
    {
        hits = 0;
        var sum = 0f;
        foreach (var change in changes ?? Array.Empty<Change>())
        {
            if (change.At > now || now - change.At > settings.Window) continue;
            if (change.Heal)
            {
                if (settings.Net) sum -= change.Amount;
                continue;
            }
            hits++;
            sum += change.Amount;
        }
        if (hits < settings.MinHits || maxHealth <= 0f || sum <= 0f) return 0f;
        return sum / maxHealth / settings.Window;
    }

    // Why a figure counts as below a line now: "now" (it is), "ahead" (it will be within Ahead), "buffer" (it was, within Buffer),
    // or null. holdUntil: until when the buffer holds, renewed while below or ahead.
    internal static string Below(float share, float threshold, float lossPerSecond, float now, Settings settings, ref float holdUntil)
    {
        string reason = null;
        if (share <= threshold) reason = "now";
        else if (lossPerSecond > 0f && share - lossPerSecond * settings.Ahead <= threshold) reason = "ahead";
        if (reason != null)
        {
            holdUntil = now + settings.Buffer;
            return reason;
        }
        return now < holdUntil ? "buffer" : null;
    }

    // The share the sites compare: the real one, or just under the line while the figure counts as below by look ahead or buffer.
    internal static float Adjusted(float share, float threshold, string reason)
        => reason == "ahead" || reason == "buffer" ? Math.Min(share, threshold - Under) : share;

    internal static string Line(string unit, LowLine line, string reason, float share, float loss, int hits, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_LOW unit={0} line={1} state={2} share={3:0.00} loss={4:0.000} hits={5} tick={6}",
            unit ?? "-", line.ToString().ToLowerInvariant(), reason ?? "above", share, loss, hits, tick);
}
