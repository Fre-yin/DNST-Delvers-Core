namespace DungeonSettlersDelvers.Core;

internal enum LatePatchState { Waiting, Installed, Failed }

// A patch Core puts on by hand after the game has loaded its data, for a game type whose type initializer reads that data
// (trap list, row 10). Several hooks may ask for it: the end of the game's boot, every load and every campaign start. It is tried at most
// once per session: installed or failed stays until Core shuts down; while the data is missing nothing is touched and the next
// hook asks again.
internal sealed class LatePatchGate
{
    internal LatePatchState State { get; private set; } = LatePatchState.Waiting;

    internal bool ShouldTry(bool dataReady) => State == LatePatchState.Waiting && dataReady;

    internal void Record(bool patched)
    {
        if (State == LatePatchState.Waiting) State = patched ? LatePatchState.Installed : LatePatchState.Failed;
    }

    internal void Reset() => State = LatePatchState.Waiting;

    internal static string Line(string name, string at, LatePatchState state)
        => "CORE_LATE_PATCH name=" + name + " at=" + at + " state=" + state.ToString().ToLowerInvariant();
}
