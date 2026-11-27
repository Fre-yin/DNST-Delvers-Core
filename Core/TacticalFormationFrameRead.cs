namespace DungeonSettlersDelvers.Core;

// Reading one of the game's parts for the formation window's looks (TacticalFormationWindowFrames). After a game update a field
// the reader uses can be gone; the reader then fails when it runs, also before its first line when its code cannot be compiled
// against the game any more. Every failure ends here: the part counts as not found, its look is drawn instead, one line names it,
// and the window still opens.
internal static class TacticalFormationFrameRead
{
    internal static T Try<T>(Func<T> read, string part, Action<string> report) where T : class
    {
        if (read == null) return null;
        try { return read(); }
        catch (Exception ex)
        {
            try { report?.Invoke("CORE_TACTICAL_FRAMES_FAILED part=" + part + ": " + ex.GetType().Name + ": " + ex.Message); }
            catch (Exception) { /* a failing log line must not keep the window shut */ }
            return null;
        }
    }
}
