namespace DungeonSettlersDelvers.Core;

// The AI's waits and windows run in game time, not in real time, so they
// hold the same number of game ticks at speed 1 and 3.5, stand still in the pause and slow down in slow motion. The game
// clock is campaignData.TimeData.Tick, 60 ticks per game second (WoundRules.TicksPerSecond). No game types here.
// A stamp after "now" (the tick jumped back after a load) counts as long ago: a wait is over, a window closed, a
// running buff ended; never negative, never a lock that holds forever.
internal static class GameClockRules
{
    internal const float TicksPerSecond = 60f;

    internal static float SecondsOf(long ticks) => ticks / TicksPerSecond;

    // Game seconds since a stamp; float.MaxValue when the stamp lies after now.
    internal static float Since(float now, float stamp) => now < stamp ? float.MaxValue : now - stamp;

    // Whether at least the given game seconds have passed since a stamp (always after a jump back).
    internal static bool Elapsed(float now, float stamp, float seconds) => Since(now, stamp) >= seconds;

    // Whether something that started at start and lasts until until still runs now (not after a jump back).
    internal static bool Running(float now, float start, float until) => now >= start && now < until;
}
