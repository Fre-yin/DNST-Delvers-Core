using HarmonyLib;
using UnityEngine;
#if BEPINEX
using PathElement = global::Refactor.UI.Path.PathElement;
using PathView = global::Refactor.UI.Path.PathView;
#else
using PathElement = Il2CppRefactor.UI.Path.PathElement;
using PathView = Il2CppRefactor.UI.Path.PathView;
#endif

namespace DungeonSettlersDelvers.Core;

// The look of the path lines with the formation on (TacticalFormationPathRules): the game draws a line per figure
// with the LineRenderer of a PathElement, which PathView hands out through GetElement for the drawn path, the temporary one and the
// loaded one, and keeps in a pool for other figures. After GetElement only the renderer's width, colours and material are set; the
// element, its points and every order stay the game's. Per element the game's own values and a dashed copy of its material are made
// once, on first use, and the game's values come back whenever the rule gives the game's line. Who leads and follows is only read
// (TacticalFormationPathRules.Read), nothing of the formation is written. A change that draws no path anew (the option or a member
// switched off, another set, the leader gone) is caught from Core's late frame: the cached elements look again every half second. The cache
// holds the renderer it was made for and is dropped when that renderer is gone or another one stands in the element, and on every
// load (ResetSummons), which first gives every living element the game's look back and destroys the dashed copies.
internal static class TacticalFormationPathLook
{
    private const float LookSeconds = 0.5f;
    // No rule gives it: set while values are being written.
    private const TacticalFormationPathRules.Line Unknown = (TacticalFormationPathRules.Line)(-1);

    private sealed class Look
    {
        internal LineRenderer Renderer;
        internal float Width;
        internal Color Start;
        internal Color End;
        internal Material Material;
        internal LineTextureMode Mode;
        internal Material Dashed;
        internal Guid Unit;
        internal TacticalFormationPathRules.Line Shown;
    }

    private static readonly Dictionary<IntPtr, Look> looks = new();
    private static HashSet<Guid> followers = new();
    private static Guid leader;
    private static int readFrame = -1;
    private static float lookedAt;
    private static Texture2D dash;

    // An element may outlive a load (the path view is the interface's, not the map's): it gets the game's look back first, so the
    // next use does not take the strong or dashed look for the game's own; the dashed copies go with the cache.
    internal static void Reset()
    {
        foreach (var look in looks.Values)
        {
            try
            {
                if (look.Renderer != null) Restore(look);
            }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PATH_LOOK_RESET_FAILED: " + ex.Message); }
            finally { Drop(look); }
        }
        looks.Clear();
        followers = new HashSet<Guid>();
        leader = Guid.Empty;
        readFrame = -1;
        lookedAt = 0f;
    }

    internal static void Show(IEntity unit, PathElement element)
    {
        var renderer = element != null ? element._renderer : null;
        if (unit == null || renderer == null) return;
        var key = element.Pointer;
        if (!looks.TryGetValue(key, out var look) || look.Renderer == null || look.Renderer.Pointer != renderer.Pointer)
        {
            if (look != null)
            {
                try
                {
                    if (look.Renderer != null) Restore(look);
                }
                finally { Drop(look); }
            }
            look = new Look
            {
                Renderer = renderer,
                Width = renderer.widthMultiplier,
                Start = renderer.startColor,
                End = renderer.endColor,
                Material = renderer.sharedMaterial,
                Mode = renderer.textureMode,
                Shown = TacticalFormationPathRules.Line.Game
            };
            looks[key] = look;
        }
        look.Unit = GuidOf(unit);
        Apply(look);
    }

    // Every frame from Core's late frame (DelversCoreRuntime.LateFrame), at once back without a cached element: every element in sight
    // looks again, at most every half second, each on its own, and the entries of elements that are gone leave the cache.
    internal static void LookAgain()
    {
        if (looks.Count == 0 || Time.unscaledTime - lookedAt < LookSeconds) return;
        lookedAt = Time.unscaledTime;
        List<IntPtr> gone = null;
        foreach (var pair in looks)
        {
            if (pair.Value.Renderer == null)
            {
                Drop(pair.Value);
                (gone ??= new List<IntPtr>()).Add(pair.Key);
                continue;
            }
            // Only a line in sight: a pooled element takes its look again at its next GetElement.
            if (!pair.Value.Renderer.gameObject.activeInHierarchy) continue;
            try { Apply(pair.Value); }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PATH_LOOK_AGAIN_FAILED stage=element: " + ex.Message); }
        }
        if (gone != null) foreach (var key in gone) looks.Remove(key);
    }
    private static void Apply(Look look)
    {
        ReadParty();
        var line = TacticalFormationPathRules.LineOf(CoreSettings.TacticalEnabled, TacticalFormation.InDungeon, look.Unit, leader, followers);
        if (line == look.Shown) return;
        var renderer = look.Renderer;
        Restore(look);
        // Unknown until every value is written: a throw below leaves no rule matching, so the next look writes all again.
        look.Shown = Unknown;
        if (line == TacticalFormationPathRules.Line.Leader)
        {
            renderer.widthMultiplier = look.Width * TacticalFormationPathRules.LeaderWidth;
            renderer.startColor = Opaque(look.Start);
            renderer.endColor = Opaque(look.End);
        }
        else if (line == TacticalFormationPathRules.Line.Follower)
        {
            renderer.startColor = Faint(look.Start);
            renderer.endColor = Faint(look.End);
            if (look.Dashed == null && look.Material != null)
            {
                look.Dashed = new Material(look.Material) { name = look.Material.name + "_Dashed", mainTexture = Dash() };
                look.Dashed.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            if (look.Dashed != null)
            {
                renderer.sharedMaterial = look.Dashed;
                renderer.textureMode = LineTextureMode.Tile;
            }
        }
        // Set only once every value is written, so a throw leaves the next try open.
        look.Shown = line;
    }

    private static void Restore(Look look)
    {
        var renderer = look.Renderer;
        // The material first: should a later setter throw, the renderer never keeps the dashed copy that Drop then destroys.
        renderer.sharedMaterial = look.Material;
        renderer.textureMode = look.Mode;
        renderer.widthMultiplier = look.Width;
        renderer.startColor = look.Start;
        renderer.endColor = look.End;
    }

    // The active set's middle and its followers there now, read once per frame and only read (TacticalFormationPathRules.Read).
    private static void ReadParty()
    {
        if (readFrame == Time.frameCount) return;
        readFrame = Time.frameCount;
        leader = Guid.Empty;
        followers = new HashSet<Guid>();
        if (!CoreSettings.TacticalEnabled || !TacticalFormation.InDungeon) return;
        var book = TacticalFormation.Book();
        (leader, followers) = TacticalFormationPathRules.Read(book.Sets[book.Active], TacticalFormation.Guids(TacticalFormation.MapMembers()), TacticalFormation.IsOn);
    }

    // The game hands an IEntity whose Guid is Il2Cpp's, which only a string turns into ours; read on each call, a cache by the
    // entity's address could hand a new figure at the same address the old one's line.
    private static Guid GuidOf(IEntity unit)
        => Guid.TryParse(unit.Guid.ToString(), out var guid) ? guid : Guid.Empty;

    private static void Drop(Look look)
    {
        if (look.Dashed != null) UnityEngine.Object.Destroy(look.Dashed);
        look.Dashed = null;
    }

    private static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);

    private static Color Faint(Color c) => new(c.r, c.g, c.b, c.a * TacticalFormationPathRules.FollowerAlpha);

    // A dash and a gap, repeated along the line (LineTextureMode.Tile).
    private static Texture2D Dash()
    {
        if (dash != null) return dash;
        var size = 2 * TacticalFormationPathRules.DashPixels;
        dash = new Texture2D(size, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (var x = 0; x < size; x++) dash.SetPixel(x, 0, x < TacticalFormationPathRules.DashPixels ? Color.white : new Color(1f, 1f, 1f, 0f));
        dash.Apply();
        dash.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return dash;
    }
}

// PathView.GetElement(IEntity, IEntityObject) has no other overload; the patch names its exact signature anyway.
[HarmonyPatch(typeof(PathView), "GetElement", new[] { typeof(IEntity), typeof(IEntityObject) })]
internal static class TacticalFormationPathLookPatch
{
    private static void Postfix(IEntity unit, PathElement __result)
    {
        try { TacticalFormationPathLook.Show(unit, __result); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PATH_LOOK_FAILED: " + ex.Message); }
    }
}
