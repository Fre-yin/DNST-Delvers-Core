namespace DungeonSettlersDelvers.Core;

// Core's clock in game seconds (GameClockRules) instead of real-time calls, for the AI and the body blood's frames.
// Other code still reads the game tick on its own.
internal static class GameClock
{
    internal static float Now => GameClockRules.SecondsOf(CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0);

    internal static float Since(float stamp) => GameClockRules.Since(Now, stamp);

    internal static bool Elapsed(float stamp, float seconds) => GameClockRules.Elapsed(Now, stamp, seconds);
}
