using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The author's drawn body blood (Blut-Positionen.aseprite): standing, one overlay per wound stage replaces the old
// marks completely ("yes, completely"). Each picture is 64x40 and covers the body sprite's frame; the overlay hangs under
// the unit's first active body renderer (PickSkin) and takes that sprite's pivot and PPU (PivotOf), so it wobbles with the
// walk. Frames 1-5 at 100 ms each, then frame 5 held
// for 700 ms ("leave picture 5 standing"): 1.2 s a loop, in game time. Lying, dead or a closed wound: no overlay.
internal static class WoundBodyArtRules
{
    internal const int Frames = 5;
    internal const int FrameMs = 100;
    internal const int HoldMs = 700;
    internal const int LoopMs = Frames * FrameMs + HoldMs;
    internal const int Width = 64;
    internal const int Height = 40;
    internal const string ObjectName = "DelversBodyBlood";

    internal static readonly string[] StageNames = { "leicht", "tief", "toedlich" };

    internal static string File(int stage, int frame)
        => string.Format(CultureInfo.InvariantCulture, "Koerperblut_{0}_f{1:00}.png", StageNames[stage], frame + 1);

    internal static IEnumerable<string> AllFiles()
    {
        for (var stage = 0; stage < StageNames.Length; stage++)
            for (var frame = 0; frame < Frames; frame++)
                yield return File(stage, frame);
    }

    // The wound stage of an overlay: light 0, deep 1, heavy 2, lethal 3; none -1.
    internal static int StageIndex(WoundRules.Stage stage) => stage switch
    {
        WoundRules.Stage.Light => 0,
        WoundRules.Stage.Deep => 1,
        WoundRules.Stage.Heavy => 2,
        WoundRules.Stage.Fatal => 3,
        _ => -1
    };

    internal static readonly string[] WoundStageNames = { "light", "deep", "heavy", "lethal" };

    // The
    // drawing a wound stage shows. Light and deep the drawing "leicht" (light still, deep running), heavy the drawing "tief",
    // lethal the drawing "toedlich".
    internal static int Drawing(int woundStage) => woundStage switch
    {
        0 or 1 => 0,
        2 => 1,
        3 => 2,
        _ => -1
    };

    // The frame (0-4) at a time in the loop: 0-499 ms frames 1-5, 500-1199 ms frame 5 held.
    internal static int FrameAt(double milliseconds)
    {
        if (double.IsNaN(milliseconds) || milliseconds < 0) return 0;
        var t = (long)milliseconds % LoopMs;
        return t < Frames * FrameMs ? (int)(t / FrameMs) : Frames - 1;
    }

    // The light stage stays on frame 1, the scratch
    // without running blood; deep and the higher stages play the loop.
    internal static int FrameFor(int stage, double milliseconds) => stage == 0 ? 0 : FrameAt(milliseconds);

    // What to do for a unit: "show", "swap", "keep", "remove:lying" or "remove:closed"; current is the shown stage or -1.
    internal static string Decide(bool lying, int stage, int current)
    {
        if (stage < 0) return current >= 0 ? "remove:closed" : "keep";
        if (lying) return current >= 0 ? "remove:lying" : "keep";
        if (current < 0) return "show";
        return current == stage ? "keep" : "swap";
    }

    // The overlay's sorting: above skin and armour (the higher of Body and Equipment_Body), below head and front hand. One
    // order above when that stays below both; else the same order, drawn in front by a hair of local Z. The hair is needed
    // because the parts often share one order (skin, armour, head and hand all 0), and then only z decides.
    internal static (int Order, float Z) Sort(int body, int equipment, int head, int hand)
    {
        var top = Math.Max(body, equipment);
        return top + 1 < Math.Min(head, hand) ? (top + 1, 0f) : (top, -0.001f);
    }

    // The overlay under Pivot/Body/Body/Body was inactive (active=0), so that renderer is a
    // prefab placeholder. The body's renderer is the first active one with a sprite named "Body" below Body, else the
    // largest active one that is no head, hand or armour; -1 for none (the overlay then hangs under Body).
    internal static int PickSkin(IReadOnlyList<(string Path, bool Active, bool HasSprite, float Area)> parts)
    {
        var best = -1;
        for (var i = 0; i < parts.Count; i++)
            if (parts[i].Active && parts[i].HasSprite && Leaf(parts[i].Path) == "Body") return i;
        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (!p.Active || !p.HasSprite || KindOf(p.Path) != "body") continue;
            if (best < 0 || p.Area > parts[best].Area) best = i;
        }
        return best;
    }

    // "head", "hand", "armour" or "body" for a renderer's path below Body.
    internal static string KindOf(string path)
    {
        if (string.IsNullOrEmpty(path)) return "body";
        if (path.IndexOf("Head", StringComparison.Ordinal) >= 0) return "head";
        if (path.IndexOf("Hand", StringComparison.Ordinal) >= 0) return "hand";
        return path.IndexOf("Equipment", StringComparison.Ordinal) >= 0 ? "armour" : "body";
    }

    private static string Leaf(string path)
    {
        var i = path?.LastIndexOf('/') ?? -1;
        return i >= 0 ? path.Substring(i + 1) : path;
    }

    // From the active renderers only: the highest order of body and armour, and the lowest of the front head and hand
    // (paths with "Front"); int.MaxValue when there is none in front.
    internal static (int Top, int Limit) BodyOrder(IReadOnlyList<(string Path, bool Active, int Order)> parts)
    {
        var top = 0;
        var any = false;
        var limit = int.MaxValue;
        foreach (var p in parts)
        {
            if (!p.Active) continue;
            var kind = KindOf(p.Path);
            if (kind == "body" || kind == "armour")
            {
                top = any ? Math.Max(top, p.Order) : p.Order;
                any = true;
            }
            else if (p.Path.IndexOf("Front", StringComparison.Ordinal) >= 0) limit = Math.Min(limit, p.Order);
        }
        return (top, limit);
    }

    internal static string PlaceLine(string unit, string path, bool active, float scaleX, float scaleY, float skinZ, float armourZ, float headZ,
        float handZ, float overlayZ, float skinCentreY, float overlayCentreY, float middleX, float middleY)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_WOUND_FX_BODYART_PLACE unit={0} path={1} active={2} scale={3:0.###},{4:0.###} z skin={5:0.####} armour={6:0.####} head={7:0.####} hand={8:0.####} overlay={9:0.####} centreY skin={10:0.###} overlay={11:0.###} middleLocal={12:0.###},{13:0.###}",
            unit, path ?? "-", active ? 1 : 0, scaleX, scaleY, skinZ, armourZ, headZ, handZ, overlayZ, skinCentreY, overlayCentreY, middleX, middleY);

    // Picture 19: the overlay takes the body sprite's own pivot (its pixel pivot over its rect, 0..1) and PPU; without a
    // readable body sprite the centre and 16.
    internal static (float X, float Y, float Ppu) PivotOf(float pivotX, float pivotY, float width, float height, float ppu)
        => width > 0f && height > 0f && ppu > 0f ? (pivotX / width, pivotY / height, ppu) : (0.5f, 0.5f, 16f);

    internal static string SpriteKey(int stage, int frame, float pivotX, float pivotY, float ppu)
        => string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2:0.###}_{3:0.###}_{4:0.##}", stage, frame, pivotX, pivotY, ppu);

    internal static string GeometryLine(string unit, string sprite, float pivotX, float pivotY, float rectW, float rectH, float ppu,
        float overlayX, float overlayY, string parent, float localX, float localY, float bodyX, float bodyY)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_WOUND_FX_BODYART_GEOMETRY unit={0} bodySprite={1} pivot={2:0.##},{3:0.##} rect={4:0}x{5:0} ppu={6:0.##} overlayPivot={7:0.###},{8:0.###} parent={9} localPos={10:0.###},{11:0.###} bodyLocal={12:0.###},{13:0.###}",
            unit, sprite ?? "-", pivotX, pivotY, rectW, rectH, ppu, overlayX, overlayY, parent ?? "-", localX, localY, bodyX, bodyY);

    internal static string Line(string unit, string stage, string result)
        => string.Format(CultureInfo.InvariantCulture, "CORE_WOUND_FX_BODYART unit={0} stage={1} result={2}", unit, stage ?? "-", result);
}
