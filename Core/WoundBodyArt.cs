using UnityEngine;
#if BEPINEX
using VFXView = global::Refactor.FX.VFXView;
#else
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// The author's drawn body blood (WoundBodyArtRules): one SpriteRenderer per bleeding standing unit, hung under the unit's first
// active body renderer below Pivot/Body (Body itself when there is none), so it wobbles with the walk (P_Move animates Body);
// it takes that body sprite's pivot and PPU (WoundBodyArtRules.PivotOf). Sorted above skin and armour, below head and front
// hand, from the active renderers (WoundBodyArtRules.BodyOrder, Sort). The stage picks the
// picture set, the game clock the frame (every game tick, GroupAiBanditTickPatch). Lying, dead, gone or a closed wound
// removes it; units the tests keep out of the AI get none (WoundFx.Skipped). The unit's object comes from the game's
// VFXView, remembered from Core's own VFX hooks. CORE_WOUND_FX_BODYART once per unit and change, with the lines _GEOMETRY, _TREE
// and _PLACE next to it; these four only log and belong to the group Wound (CoreProbes): each of their functions asks it first.
internal static class WoundBodyArt
{
    private sealed class Overlay
    {
        internal GameObject Object;
        internal SpriteRenderer Renderer;
        // The unit's own body sprite renderer (Pivot/Body/Body/Body): the overlay hangs under it and copies its pivot and flip.
        internal SpriteRenderer Skin;
        internal int Stage;
        internal int Frame = -1;
    }

    private static readonly Dictionary<Guid, Overlay> overlays = new();
    private static VFXView view;
    private static int lines;

    // The game's VFX view, seen in Core's VFX hooks; it holds the entity objects.
    internal static void SeeView(VFXView seen)
    {
        if (seen != null && (view == null || view.Pointer != seen.Pointer)) view = seen;
    }

    internal static void ResetCampaign()
    {
        foreach (var guid in new List<Guid>(overlays.Keys)) Remove(guid, "load");
        overlays.Clear();
        view = null;
        lines = 0;
        failed.Clear();
        geometryLogged.Clear();
        placeLogged.Clear();
        treeLogged = false;
    }

    // From WoundFx.OnTick (a bleeding unit's tick) and WoundFx.Forget (stage None: the wound closed).
    internal static void OnWound(UnitEntity unit, Guid guid, WoundRules.Stage stage, bool lying)
    {
        var index = WoundBodyArtRules.StageIndex(stage);
        var current = overlays.TryGetValue(guid, out var overlay) ? overlay.Stage : -1;
        var action = WoundBodyArtRules.Decide(lying, index, current);
        switch (action)
        {
            case "keep":
                return;
            case "remove:lying":
            case "remove:closed":
                Remove(guid, action.Substring("remove:".Length));
                return;
            case "swap":
                overlay.Stage = index;
                overlay.Frame = -1;
                Note(guid, index, "swapped");
                Paint(overlay);
                return;
            default:
                Show(unit, guid, index);
                return;
        }
    }

    // A unit that was not ticked this round (dead, gone): its overlay goes.
    internal static void EndTick(HashSet<Guid> ticked)
    {
        if (overlays.Count == 0) return;
        List<Guid> gone = null;
        foreach (var guid in overlays.Keys)
            if (!ticked.Contains(guid)) (gone ??= new List<Guid>()).Add(guid);
        if (gone != null)
            foreach (var guid in gone) Remove(guid, "gone");
    }

    // Every game tick: the frame of each overlay from the game clock.
    internal static void OnGameTick()
    {
        if (overlays.Count == 0) return;
        foreach (var overlay in overlays.Values) Paint(overlay);
    }

    private static void Paint(Overlay overlay)
    {
        if (overlay?.Renderer == null || !overlay.Renderer) return;
        var skin = overlay.Skin != null && overlay.Skin ? overlay.Skin : null;
        if (skin != null)
        {
            overlay.Renderer.flipX = skin.flipX;
            overlay.Renderer.flipY = skin.flipY;
        }
        var frame = WoundBodyArtRules.FrameFor(overlay.Stage, GameClock.Now * 1000.0);
        if (frame == overlay.Frame) return;
        overlay.Frame = frame;
        var body = skin?.sprite;
        var (px, py, ppu) = body != null
            ? WoundBodyArtRules.PivotOf(body.pivot.x, body.pivot.y, body.rect.width, body.rect.height, body.pixelsPerUnit)
            : WoundBodyArtRules.PivotOf(0f, 0f, 0f, 0f, 0f);
        var sprite = CoreAssets.BodyBlood(WoundBodyArtRules.Drawing(overlay.Stage), frame, new Vector2(px, py), ppu);
        if (sprite) overlay.Renderer.sprite = sprite;
    }

    private static void Show(UnitEntity unit, Guid guid, int stage)
    {
        try
        {
            var entityObject = CoreGame.EntityObjectOf(view?._objectContainer, unit.Guid);
            var pivot = entityObject?.Graphic?.TryCast<UnitGraphic>()?._pivotTransform;
            var body = pivot != null ? pivot.Find(BodyName) : null;
            if (body == null)
            {
                Note(guid, stage, view == null ? "failed:noview" : "failed:nobody");
                return;
            }
            // Pivot/Body/Body/Body is inactive (a prefab placeholder), and an overlay under it is too. The
            // overlay hangs under the unit's first active body renderer (WoundBodyArtRules.Kind "skin"), sharing its place, flip
            // and pivot; Body itself when there is none. Sorting from the active renderers only (BodyOrder).
            var parts = Parts(body);
            Tree(parts);
            var picked = WoundBodyArtRules.PickSkin(parts.ConvertAll(p => (p.Path, p.Active, p.HasSprite, p.Area)));
            var skin = picked >= 0 ? parts[picked].Renderer : null;
            var parent = skin != null ? skin.transform : body;
            var go = new GameObject(WoundBodyArtRules.ObjectName);
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.enabled = true;
            var (top, limit) = WoundBodyArtRules.BodyOrder(parts.ConvertAll(p => (p.Path, p.Active, p.Order)));
            var (order, z) = WoundBodyArtRules.Sort(top, top, limit, limit);
            var anyActive = parts.Find(p => p.Active);
            renderer.sortingLayerID = skin != null ? skin.sortingLayerID : anyActive != null ? anyActive.Renderer.sortingLayerID : 0;
            renderer.sortingOrder = order;
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var overlay = new Overlay { Object = go, Renderer = renderer, Stage = stage, Skin = skin };
            Geometry(guid, skin, parent, body);
            overlays[guid] = overlay;
            Note(guid, stage, "shown");
            Paint(overlay);
            Place(guid, overlay, body);
        }
        catch (Exception ex)
        {
            Note(guid, stage, "failed");
            DelversHost.Warning("CORE_WOUND_FX_BODYART_FAILED unit=" + guid + ": " + ex.Message);
        }
    }

    private const string BodyName = "Body";

    // Once per unit: the body sprite's name, pixel pivot, rect and PPU, the overlay's pivot, its parent and local place, and
    // Body's own local place (whether something is shifted twice).
    private static readonly HashSet<Guid> geometryLogged = new();

    private static void Geometry(Guid guid, SpriteRenderer skin, Transform parent, Transform body)
    {
        if (!CoreProbes.Wound) return;
        if (geometryLogged.Count >= 32 || !geometryLogged.Add(guid)) return;
        var sprite = skin != null ? skin.sprite : null;
        var (px, py, ppu) = sprite != null
            ? WoundBodyArtRules.PivotOf(sprite.pivot.x, sprite.pivot.y, sprite.rect.width, sprite.rect.height, sprite.pixelsPerUnit)
            : WoundBodyArtRules.PivotOf(0f, 0f, 0f, 0f, 0f);
        var local = skin != null ? skin.transform.localPosition : Vector3.zero;
        var bodyLocal = body.localPosition;
        DelversHost.Info(WoundBodyArtRules.GeometryLine(guid.ToString("D"), sprite != null ? sprite.name : null,
            sprite != null ? sprite.pivot.x : 0f, sprite != null ? sprite.pivot.y : 0f, sprite != null ? sprite.rect.width : 0f,
            sprite != null ? sprite.rect.height : 0f, ppu, px, py, parent != null ? parent.name : null, local.x, local.y, bodyLocal.x, bodyLocal.y));
    }

    private static float ZOf(SpriteRenderer renderer) => renderer != null ? renderer.transform.position.z : float.NaN;

    private sealed class Part
    {
        internal SpriteRenderer Renderer;
        internal string Path;
        internal bool Active;
        internal bool HasSprite;
        internal float Area;
        internal int Order;
    }

    // Every sprite renderer under Pivot/Body, inactive ones too, with its path below Body.
    private static List<Part> Parts(Transform body)
    {
        var parts = new List<Part>();
        var all = body.GetComponentsInChildren<SpriteRenderer>(true);
        for (var n = 0; all != null && n < all.Length; n++)
        {
            var r = all[n];
            if (r == null || r.gameObject.name == WoundBodyArtRules.ObjectName) continue;
            var path = r.transform.name;
            for (var p = r.transform.parent; p != null && p != body; p = p.parent) path = p.name + "/" + path;
            var size = r.sprite != null ? r.bounds.size : Vector3.zero;
            parts.Add(new Part { Renderer = r, Path = path, Active = r.gameObject.activeInHierarchy && r.enabled, HasSprite = r.sprite != null,
                Area = size.x * size.y, Order = r.sortingOrder });
        }
        return parts;
    }

    // Once per campaign: the unit's renderers, path:active:order:sprite, so the body's real renderer can be named.
    private static bool treeLogged;

    private static void Tree(List<Part> parts)
    {
        if (!CoreProbes.Wound) return;
        if (treeLogged) return;
        treeLogged = true;
        var items = new List<string>();
        foreach (var p in parts)
            if (items.Count < 40) items.Add(p.Path + ":" + (p.Active ? 1 : 0) + ":" + p.Order + ":" + (p.Renderer.sprite != null ? p.Renderer.sprite.name : "-"));
        DelversHost.Info("CORE_WOUND_FX_BODYART_TREE n=" + parts.Count + " " + string.Join(" ", items));
    }

    // Once per unit, after the first frame: where the overlay hangs, whether it is active, its scale, the z of the parts and
    // of the overlay, the world centre height of skin and overlay, and the middle Body's local place (Pivot/Body/Body).
    private static readonly HashSet<Guid> placeLogged = new();

    private static void Place(Guid guid, Overlay overlay, Transform body)
    {
        if (!CoreProbes.Wound) return;
        if (placeLogged.Count >= 32 || !placeLogged.Add(guid) || overlay?.Object == null) return;
        var t = overlay.Object.transform;
        var path = t.name;
        for (var p = t.parent; p != null && p != body.parent; p = p.parent) path = p.name + "/" + path;
        var scale = t.lossyScale;
        var middle = body.Find("Body");
        var middleLocal = middle != null ? middle.localPosition : Vector3.zero;
        var r = overlay.Renderer;
        DelversHost.Info(WoundBodyArtRules.PlaceLine(guid.ToString("D"), path, overlay.Object.activeInHierarchy && r.enabled,
            scale.x, scale.y, ZOf(overlay.Skin), ZOf(RendererAt(body, "Body/Equipment_Body")), ZOf(RendererAt(body, "Head_Front/Head")),
            ZOf(RendererAt(body, "Hand_Front/Hand")), t.position.z, overlay.Skin != null ? overlay.Skin.bounds.center.y : float.NaN,
            r.sprite != null ? r.bounds.center.y : float.NaN, middleLocal.x, middleLocal.y)
            + $" self={(overlay.Object.activeSelf ? 1 : 0)} parentActive={(t.parent != null && t.parent.gameObject.activeInHierarchy ? 1 : 0)}"
            + $" enabled={(r.enabled ? 1 : 0)} sprite={(r.sprite != null ? r.sprite.name : "-")} layer={r.sortingLayerID} order={r.sortingOrder}"
            + $" skin={(overlay.Skin != null ? overlay.Skin.name + ":" + (overlay.Skin.gameObject.activeInHierarchy ? 1 : 0) + ":" + overlay.Skin.sortingOrder : "-")}");
    }

    private static SpriteRenderer RendererAt(Transform body, string path)
    {
        var t = body.Find(path);
        return t != null ? t.GetComponent<SpriteRenderer>() : null;
    }

    private static void Remove(Guid guid, string reason)
    {
        if (!overlays.Remove(guid, out var overlay)) return;
        if (overlay.Object != null && overlay.Object) UnityEngine.Object.Destroy(overlay.Object);
        Note(guid, overlay.Stage, "removed:" + reason);
    }

    private static readonly HashSet<string> failed = new(StringComparer.Ordinal);

    private static void Note(Guid guid, int stage, string result)
    {
        if (!CoreProbes.Wound) return;
        // A failure repeats every wound tick until the object is there; it is logged once per unit and kind.
        if (result.StartsWith("failed", StringComparison.Ordinal) && !failed.Add(guid.ToString("D") + "|" + result)) return;
        if (lines++ >= 400) return;
        DelversHost.Info(WoundBodyArtRules.Line(guid.ToString("D"), stage >= 0 && stage < WoundBodyArtRules.WoundStageNames.Length ? WoundBodyArtRules.WoundStageNames[stage] : null, result));
    }
}
