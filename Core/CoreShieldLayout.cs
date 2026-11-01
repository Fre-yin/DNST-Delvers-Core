using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// Where a shield band goes (CoreShieldBand): the bars' corners in a parent's space, their common parent, a fill's visible outer
// rect, the clipping between, and the place right after the bars.
internal static class CoreShieldLayout
{
    // The sibling index right after the last child of parent that holds one of the bars; -1 when none does.
    internal static int AfterBars(Transform parent, Transform[] bars)
    {
        var last = -1;
        for (var i = 0; bars != null && i < bars.Length; i++)
        {
            var t = bars[i];
            while (t != null && t.parent != null && t.parent.Pointer != parent.Pointer) t = t.parent;
            if (t != null && t.parent != null && t.parent.Pointer == parent.Pointer) last = Math.Max(last, t.GetSiblingIndex());
        }
        return last < 0 ? -1 : last + 1;
    }

    // A transform's four world corners in another's local space (bottom left, top left, top right, bottom right).
    internal static Vector3[] Corners(Transform transform, Transform space)
    {
        var corners = new Vector3[4];
        var rect = transform.TryCast<RectTransform>();
        if (rect == null)
        {
            for (var c = 0; c < 4; c++) corners[c] = space.InverseTransformPoint(transform.position);
            return corners;
        }
        var world = new Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(world);
        for (var c = 0; c < 4; c++) corners[c] = space.InverseTransformPoint(world[c]);
        return corners;
    }

    // The lowest parent of both bars. Of the status window (the honeycomb only on the body bar):
    // under the parent drawn later, one bar's frame, the band was cut to that bar; under the common parent, as its last child,
    // it lies over both bars and out of each bar's own clipping.
    internal static Transform CommonParent(Transform a, Transform b)
    {
        if (a == null) return b?.parent;
        if (b == null) return a.parent;
        var above = new HashSet<IntPtr>();
        for (var t = a.parent; t != null; t = t.parent) above.Add(t.Pointer);
        for (var t = b.parent; t != null; t = t.parent)
            if (above.Contains(t.Pointer)) return t;
        return a.parent;
    }

    // A fill's visible bar: its nearest clipping parent below the band's parent (the bar's frame cuts the fill to the bar),
    // else the background given, else the fill itself. - measured on the fills, the bar's own dark rim showed between honeycomb and the outer frame.
    internal static RectTransform OuterOf(RectTransform fill, RectTransform background, Transform stop)
    {
        for (var t = fill != null ? fill.parent : null; t != null && (stop == null || t.Pointer != stop.Pointer); t = t.parent)
            if (t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null) return t.TryCast<RectTransform>() ?? fill;
        return background ?? fill;
    }

    // How many clipping components lie between a bar and the band's parent (for the log).
    internal static int ClipsBetween(Transform bar, Transform parent)
    {
        var clips = 0;
        for (var t = bar != null ? bar.parent : null; t != null && t.Pointer != parent.Pointer; t = t.parent)
            if (t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null) clips++;
        return clips;
    }
}
