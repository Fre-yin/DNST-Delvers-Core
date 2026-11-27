using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The camp's boss party stays in its camp (only the normal bandits patrol): after a fight or an aborted one
// each of its bandits walks once back to the cell it was placed on, and its idle stays inside the camp's box. The cells stand in
// Core's block with the camp (CoreSaveData.CampHomes), so this also follows a load. Rules without the game.
internal static class BanditCampHomeRules
{
    internal const float LookSeconds = 1f;
    internal const int LogCap = 60;

    internal static bool Inside((int MinX, int MinY, int MaxX, int MaxY) box, (int X, int Y) cell)
        => cell.X >= box.MinX && cell.X <= box.MaxX && cell.Y >= box.MinY && cell.Y <= box.MaxY;

    // Whether the bandit walks home now: not fighting, not there, once since its last fight; fought: it fought since the last walk,
    // or it stands outside the box (a load after an aborted fight).
    internal static bool WalksHome(bool inCombat, bool fought, bool ordered, (int X, int Y) at, (int X, int Y) home, (int MinX, int MinY, int MaxX, int MaxY) box)
        => !inCombat && !ordered && at != home && (fought || !Inside(box, at));

    // Its idle cell: where it stands while inside the box, else its home, so the game's idle wander never leaves the camp.
    internal static (int X, int Y) IdleCell((int X, int Y) at, (int X, int Y) home, (int MinX, int MinY, int MaxX, int MaxY) box)
        => Inside(box, at) ? at : home;

    internal static string Line(string unit, (int X, int Y) at, (int X, int Y) home, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_HOME unit={0} at={1},{2} home={3},{4} tick={5}", unit ?? "-", at.X, at.Y, home.X, home.Y, tick);
}

// The measurement of the gap after waking (plan C, probe only): from the game's change of a Core bandit to its fight until Core's
// first decision for it, with the task in its brain at the change. Rules without the game.
internal static class BanditTakeoverRules
{
    internal const int LogCap = 40;

    internal static string Line(string unit, long wakeTick, long firstTick, string task)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_TAKEOVER unit={0} wake={1} first={2} ticks={3} task={4}",
            unit ?? "-", wakeTick, firstTick, Math.Max(0L, firstTick - wakeTick), task ?? "-");
}
