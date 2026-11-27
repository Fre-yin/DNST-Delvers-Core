namespace DungeonSettlersDelvers.Core;

// How far the dragged formation window moves back so it stays wholly on the screen (TacticalFormationWindowFront). One axis at a
// time, in screen pixels: low and high are the window's edges, size the screen's. A window larger than the screen keeps its start
// edge on the screen: the left edge, and at the vertical axis the top edge (startAtHigh), where the title bar is.
internal static class TacticalFormationScreenRules
{
    internal static float Shift(float low, float high, float size, bool startAtHigh)
    {
        if (high - low > size) return startAtHigh ? size - high : -low;
        if (low < 0f) return -low;
        if (high > size) return size - high;
        return 0f;
    }
}
