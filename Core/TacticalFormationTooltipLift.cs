namespace DungeonSettlersDelvers.Core;

// The game's tooltip layers the formation window lifts above itself while it is open (TacticalFormationWindowFront, P07): each
// with whether the window added its canvas and the sorting it had. Giving them back removes an added canvas at once, not at the
// frame's end: a new opening in the same frame (Open closes the window first) would otherwise find the canvas still there, lift
// it, and lose it right after, so its tooltips lay behind the window again (final review of 0.5.2, S1). Pure over the layer's
// type, so the offline tests cover it.
internal sealed class TacticalFormationTooltipLift<T> where T : class
{
    private readonly List<(T Layer, bool Added, bool Override, int Order)> lifted = new();

    internal int Count => lifted.Count;

    // The sorting a lifted layer gets: above the window, never lower than it was.
    internal static int Order(int current, int above) => Math.Max(current, above);

    internal void Note(T layer, bool added, bool overrideSorting, int order)
    {
        if (layer != null) lifted.Add((layer, added, overrideSorting, order));
    }

    // Each layer back as it was: an added canvas removed at once (removeNow must not wait for the frame's end), the others with
    // their own sorting. A failure with one layer leaves the others to be given back; it is reported and the list is empty after.
    internal void Drop(Action<T> removeNow, Action<T, bool, int> restore, Action<string> report)
    {
        foreach (var (layer, added, overrideSorting, order) in lifted)
        {
            try
            {
                if (added) removeNow?.Invoke(layer);
                else restore?.Invoke(layer, overrideSorting, order);
            }
            catch (Exception ex) { report?.Invoke("CORE_TACTICAL_TOOLTIP_BACK_FAILED: " + ex.Message); }
        }
        lifted.Clear();
    }
}
