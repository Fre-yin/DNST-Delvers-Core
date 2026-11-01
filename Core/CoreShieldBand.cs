using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The honeycomb shield over a pair of health bars (the boss bar's look, then the same on the party's
// bars): Core's own flat honeycomb tile (ShieldRules.HoneyKind), flush with the bars' outer edges, framed white,
// growing from the left with the shield's share. Above 100 % a second layer in another colour replaces the first from the left
// (ShieldRules.ShieldLayers: two layers from the 200 % cap, colours ui.shield.colour.1-2), the frame outside, a wave
// over the honeycomb (WaveFactor), texts that would lie under it lifted above (Lift). No game image.
internal sealed class CoreShieldBand
{
    // One layer: a root whose place and width follow the share, its white edges outside it, and inside a clipping
    // area (RectMask2D) with the tiled honeycomb at the band's full width, fixed at the band's left edge. A narrower layer
    // therefore loses tiles at its edge; the cells keep their size (never squeezed). The honeycomb stays flush with the bars.
    // The honeycomb of a layer is a row of narrow strips (RawImage over the repeating tile, each with its part of the
    // pattern), so the wave can set an opacity per strip; the pattern runs on across them.
    internal sealed class Layer
    {
        internal RectTransform Clip;
        internal RectTransform Mask;
        internal RectTransform Tiles;
        internal RawImage[] Strips;
        internal float[] Centres;
        internal Color Colour;
        internal Image[] Frame;
    }

    private Layer under;
    private Layer top;
    // The Blood Warden's own section, left of the others (ShieldRules.BloodSections).
    private Layer blood;
    private float full;
    private float leftX;
    // The band's area in its parent's space (min x, min y, max x, max y), for the texts it overlaps (Lift).
    private (float MinX, float MinY, float MaxX, float MaxY) area;
    // The party's band: its white frame takes the band's opacity too (the bars must stay readable).
    private bool frameFollows;

    internal int Rows { get; private set; }

    // For the wave (CoreShieldWave): the two layers and whether this is a party band (its own settings).
    internal Layer Under => under;
    internal Layer Top => top;
    internal Layer Blood => blood;
    internal bool IsParty => frameFollows;

    // False once Unity destroyed the band (a scene change).
    internal bool Alive => top?.Clip != null;

    // Two layers (the one below, the current one by its share) from left to right between bottom and top, in the parent's
    // space; hidden until Show. cellAlpha: the honeycomb cells' own alpha (null: the tile's own, ShieldRules.HoneyColours).
    internal static CoreShieldBand Create(RectTransform parent, string name, float left, float bottom, float right, float top, float cell,
        byte? cellAlpha = null, bool frameFollows = false)
    {
        var honeycomb = Honeycomb(cellAlpha);
        var band = new CoreShieldBand { full = Math.Max(0f, right - left), frameFollows = frameFollows };
        band.under = MakeLayer(parent, name + "_Below", honeycomb, left, bottom, top, cell, band.full, out var rows);
        band.top = MakeLayer(parent, name, honeycomb, left, bottom, top, cell, band.full, out _);
        band.blood = MakeLayer(parent, name + "_Blood", honeycomb, left, bottom, top, cell, band.full, out _);
        band.leftX = band.top.Clip.anchoredPosition.x;
        band.Rows = rows;
        band.Show(0f, 0f);
        CoreShieldWave.Register(band);
        return band;
    }

    // The shield's share of the health (1 = 100 %). The current layer replaces the one below where it lies;
    // the one below shows only right of it (ShieldRules.LowerSpan), so no two layers lie on top of each other.
    // bloodShare: the Blood Warden's shield, its own section left of the other shields (ShieldRules.BloodSections).
    internal void Show(float share, float alpha, float bloodShare = 0f)
    {
        if (bloodShare > 0f)
        {
            var (bloodWidth, otherOffset, otherWidth, otherLayer) = ShieldRules.BloodSections(full, share, bloodShare);
            Set(blood, BloodLayer, 0f, bloodWidth, alpha, true, true);
            Set(top, otherLayer, otherOffset, otherWidth, alpha, true, true);
            Set(under, -1, 0f, 0f, alpha, true, true);
            return;
        }
        Set(blood, -1, 0f, 0f, alpha, true, true);
        var (below, layer, part) = ShieldRules.ShieldLayers(share, ShieldRules.ShieldLayerCount);
        var (offset, remain) = ShieldRules.LowerSpan(full, part);
        // The frame's line where the current layer ends inside the band (setting ui.shield.divider, the author picks).
        var divider = below < 0 || BanditBossBarRules.ViewOn(CoreSettings.Number(ShieldRules.DividerKey));
        Set(top, layer, 0f, ShieldRules.ShieldWidth(full, part), alpha, true, divider);
        Set(under, below, below >= 0 ? offset : 0f, below >= 0 ? remain : 0f, alpha, offset <= 0f, true);
    }

    // The layer number the blood part is painted with: its own colour, the party's boost as a layer above the first.
    private const int BloodLayer = 1;

    internal void Destroy()
    {
        CoreShieldWave.Unregister(this);
        DropLifted();
        if (under?.Clip != null) UnityEngine.Object.Destroy(under.Clip.gameObject);
        if (top?.Clip != null) UnityEngine.Object.Destroy(top.Clip.gameObject);
        if (blood?.Clip != null) UnityEngine.Object.Destroy(blood.Clip.gameObject);
    }

    // Texts inside a bar's own container draw below the band, which lies after that container (the head
    // numbers and the slash of the status window under the honeycomb). Each
    // such text gets a copy right after the band, at the same place, and the original is switched off; the copy takes over
    // the original's text and colour at every refresh (SyncLifted). Destroy gives the originals back.
    private readonly List<(TMPro.TMP_Text Original, TMPro.TMP_Text Copy)> lifted = new();
    private const string LiftedSuffix = "_DelversAboveShield";

    // Only the texts whose place overlaps the band (Overlaps), not every text before it.
    internal int Lift()
    {
        var parent = top?.Clip != null ? top.Clip.parent : null;
        if (parent == null) return 0;
        var index = top.Clip.GetSiblingIndex();
        var found = new List<TMPro.TMP_Text>();
        for (var i = 0; i < index && i < parent.childCount; i++)
        {
            var texts = parent.GetChild(i).GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (var j = 0; texts != null && j < texts.Length; j++)
            {
                // A copy another band already lifted is never copied again.
                if (texts[j] == null || texts[j].name.EndsWith(LiftedSuffix, StringComparison.Ordinal)) continue;
                var c = CoreShieldLayout.Corners(texts[j].rectTransform, parent);
                if (ShieldRules.Overlaps(c[0].x, c[0].y, c[2].x, c[2].y, area.MinX, area.MinY, area.MaxX, area.MaxY)) found.Add(texts[j]);
            }
        }
        foreach (var original in found)
        {
            var copyObject = UnityEngine.Object.Instantiate(original.gameObject, parent, true);
            copyObject.name = original.name + LiftedSuffix;
            copyObject.transform.SetSiblingIndex(Math.Min(top.Clip.GetSiblingIndex() + 1, parent.childCount - 1));
            var copy = copyObject.GetComponent<TMPro.TMP_Text>();
            if (copy == null)
            {
                UnityEngine.Object.Destroy(copyObject);
                continue;
            }
            lifted.Add((original, copy));
            original.enabled = false;
        }
        SyncLifted();
        return lifted.Count;
    }

    internal void SyncLifted()
    {
        // A copy whose original the game rebuilt (destroyed) goes too.
        for (var i = lifted.Count - 1; i >= 0; i--)
        {
            if (lifted[i].Original != null) continue;
            if (lifted[i].Copy != null) UnityEngine.Object.Destroy(lifted[i].Copy.gameObject);
            lifted.RemoveAt(i);
        }
        foreach (var (original, copy) in lifted)
        {
            if (original == null || copy == null) continue;
            if (copy.text != original.text) copy.text = original.text;
            copy.color = original.color;
            if (copy.gameObject.activeSelf != original.gameObject.activeInHierarchy) copy.gameObject.SetActive(original.gameObject.activeInHierarchy);
        }
    }

    private void DropLifted()
    {
        foreach (var (original, copy) in lifted)
        {
            if (original != null) original.enabled = true;
            if (copy != null) UnityEngine.Object.Destroy(copy.gameObject);
        }
        lifted.Clear();
    }

    // The bars the band lies over; it is placed right after them under the common parent (Place).
    private Transform[] bars;

    // Right after the bars (a later bar may have been added after the band), before any text that follows them.
    internal void ToFront() => Place();

    private void Place()
    {
        var parent = top?.Clip != null ? top.Clip.parent : null;
        if (parent == null) return;
        var index = CoreShieldLayout.AfterBars(parent, bars);
        if (index < 0)
        {
            under?.Clip?.SetAsLastSibling();
            top?.Clip?.SetAsLastSibling();
            return;
        }
        // Lower layer first, the current one above it, both right after the last child that holds a bar.
        if (under?.Clip != null) under.Clip.SetSiblingIndex(Math.Min(index, parent.childCount - 1));
        if (top?.Clip != null) top.Clip.SetSiblingIndex(Math.Min(under?.Clip != null ? under.Clip.GetSiblingIndex() + 1 : index, parent.childCount - 1));
    }

    // Texts under the parent that still draw below the band (inside a bar's own container): their names for the log.
    internal string TextsBelow()
    {
        var parent = top?.Clip != null ? top.Clip.parent : null;
        if (parent == null) return "-";
        var index = top.Clip.GetSiblingIndex();
        var names = new List<string>();
        for (var i = 0; i < index && i < parent.childCount; i++)
        {
            var texts = parent.GetChild(i).GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (var j = 0; texts != null && j < texts.Length; j++) if (texts[j] != null) names.Add(texts[j].name);
        }
        return names.Count == 0 ? "none" : string.Join("+", names);
    }

    // A layer from offset (from the band's left edge) over width; its tiles stay where the band starts.
    private void Set(Layer item, int layer, float offset, float width, float alpha, bool leftEdge, bool rightEdge)
    {
        if (item?.Clip == null || item.Strips == null) return;
        // No frame stroke for a share too small to show (narrower than the frame on both sides).
        var shown = layer >= 0 && ShieldRules.ShieldShown(width);
        if (item.Clip.gameObject.activeSelf != shown) item.Clip.gameObject.SetActive(shown);
        if (!shown) return;
        item.Clip.anchoredPosition = new Vector2(leftX + offset, item.Clip.anchoredPosition.y);
        item.Clip.sizeDelta = new Vector2(width, item.Clip.sizeDelta.y);
        if (item.Tiles != null) item.Tiles.anchoredPosition = new Vector2(-offset, 0f);
        var (r, g, b) = item == blood
            ? ShieldRules.ShieldColour(CoreSettings.Number(ShieldRules.BloodColourKey), ShieldRules.BloodColourDefault)
            : ShieldRules.ShieldColour(CoreSettings.Number(ShieldRules.ShieldColourKey(layer)), ShieldRules.ShieldColourDefault(layer));
        // The party's band: a layer above the first shows stronger (ui.shield.boost); the boss bar keeps one opacity.
        if (frameFollows) alpha = ShieldRules.LayerOpacity(alpha, layer, CoreSettings.Number(ShieldRules.PartyBoostKey));
        item.Colour = new Color(r, g, b, alpha);
        CoreShieldWave.Paint(this, item, CoreShieldWave.Time());
        for (var i = 0; item.Frame != null && i < item.Frame.Length; i++)
        {
            if (item.Frame[i] == null) continue;
            if (frameFollows) item.Frame[i].color = new Color(1f, 1f, 1f, alpha);
            var on = i == 0 ? leftEdge : i != 1 || rightEdge;
            if (item.Frame[i].gameObject.activeSelf != on) item.Frame[i].gameObject.SetActive(on);
        }
    }

    // One layer: the root with its white edges outside, the clipping area and the tiled honeycomb inside at the band's full width.
    private static Layer MakeLayer(RectTransform parent, string name, Sprite honeycomb, float left, float bottom, float top, float cell, float fullWidth, out int rows)
    {
        var clipObject = new GameObject(name);
        var clip = clipObject.AddComponent<RectTransform>();
        clip.SetParent(parent, false);
        var (centreY, height) = ShieldRules.ShieldBand(bottom, top, cell);
        var middle = parent.rect.center;
        clip.anchorMin = clip.anchorMax = new Vector2(0.5f, 0.5f);
        clip.pivot = new Vector2(0f, 0.5f);
        clip.anchoredPosition = new Vector2(left - middle.x, centreY - middle.y);
        clip.sizeDelta = new Vector2(0f, height);
        clip.SetAsLastSibling();
        var maskObject = new GameObject("DelversShieldMask");
        var mask = maskObject.AddComponent<RectTransform>();
        mask.SetParent(clip, false);
        mask.anchorMin = Vector2.zero;
        mask.anchorMax = Vector2.one;
        mask.pivot = new Vector2(0f, 0.5f);
        mask.anchoredPosition = Vector2.zero;
        mask.sizeDelta = Vector2.zero;
        maskObject.AddComponent<RectMask2D>();
        var tilesObject = new GameObject("DelversShieldTiles");
        var tiles = tilesObject.AddComponent<RectTransform>();
        tiles.SetParent(mask, false);
        tiles.anchorMin = new Vector2(0f, 0f);
        tiles.anchorMax = new Vector2(0f, 1f);
        tiles.pivot = new Vector2(0f, 0.5f);
        tiles.anchoredPosition = Vector2.zero;
        tiles.sizeDelta = new Vector2(fullWidth, 0f);
        // The strips: each shows its part of the repeating tile (uvRect in tiles), whole rows over the height.
        rows = ShieldRules.TileRows(height, cell);
        var tileHeight = height / rows;
        var tileWidth = tileHeight * ShieldRules.HoneyWidth / ShieldRules.HoneyHeight;
        var count = ShieldRules.WaveStrips(fullWidth);
        var stripWidth = fullWidth / count;
        var strips = new RawImage[count];
        var centres = new float[count];
        for (var i = 0; i < count; i++)
        {
            var stripObject = new GameObject("DelversShieldStrip");
            var strip = stripObject.AddComponent<RectTransform>();
            strip.SetParent(tiles, false);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = new Vector2(i * stripWidth, 0f);
            strip.sizeDelta = new Vector2(stripWidth, 0f);
            var raw = stripObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = honeycomb.texture;
            raw.uvRect = new Rect(i * stripWidth / tileWidth, 0f, stripWidth / tileWidth, rows);
            strips[i] = raw;
            centres[i] = (i + 0.5f) * stripWidth;
        }
        // Outside the band: the side edges reach over the corners, so the frame closes.
        var border = ShieldRules.ShieldBorder;
        var frame = new[]
        {
            Edge(clip, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(border, 2f * border)),
            Edge(clip, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(border, 2f * border)),
            Edge(clip, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, border)),
            Edge(clip, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, border))
        };
        return new Layer { Clip = clip, Mask = mask, Tiles = tiles, Strips = strips, Centres = centres, Frame = frame };
    }

    // One white strip of a band's frame, just outside the band along one side (pivot on the far side), stretched with it.
    private static Image Edge(RectTransform band, string side, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        var strip = new GameObject("DelversShieldBorder_" + side);
        var rect = strip.AddComponent<RectTransform>();
        rect.SetParent(band, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        var image = strip.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    // Core's own honeycomb tile, drawn once; again when Unity dropped it. A failure on the way destroys the half made texture
    // and throws on to the caller, which logs it.
    private static readonly Dictionary<int, Sprite> honeycombTiles = new();

    internal static Sprite Honeycomb(byte? cellAlpha = null)
    {
        var key = cellAlpha ?? -1;
        if (honeycombTiles.TryGetValue(key, out var cached) && cached) return cached;
        int width = ShieldRules.HoneyWidth, height = ShieldRules.HoneyHeight;
        var pixels = new Il2CppStructArray<Color32>(width * height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var kind = ShieldRules.HoneyKind(x, y);
                var c = ShieldRules.HoneyColours[kind];
                pixels[y * width + x] = new Color32(c[0], c[1], c[2], kind == 0 && cellAlpha is byte a ? a : c[3]);
            }
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var tile = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            tile.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tile.name = "DelversHoneycomb" + (cellAlpha is byte b ? "_" + b : "");
            honeycombTiles[key] = tile;
            return tile;
        }
        catch
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            honeycombTiles.Remove(key);
            throw;
        }
    }

    // The band over a body and a head bar: the outer edges of both, in the space of their common parent; one band over both
    // and the gap, or one per bar (ShieldRules.ShieldGapKey 0). Null when there is no body bar.
    // bodyOuter, headOuter: the bars' visible outer rects when the bars given are only their fills (null: the bars themselves).
    // A band's area with its white frame, for the texts it overlaps (Lift).
    private static (float, float, float, float) AreaOf(float left, float bottom, float right, float top)
        => (left - ShieldRules.ShieldBorder, bottom - ShieldRules.ShieldBorder, right + ShieldRules.ShieldBorder, top + ShieldRules.ShieldBorder);

    internal static (CoreShieldBand Body, CoreShieldBand Head, string Line) Over(RectTransform body, RectTransform head, string name,
        byte? cellAlpha = null, bool frameFollows = false, RectTransform bodyOuter = null, RectTransform headOuter = null)
    {
        if (body == null) return (null, null, "nobody");
        var parent = CoreShieldLayout.CommonParent(body, head)?.TryCast<RectTransform>();
        if (parent == null) return (null, null, "noparent");
        var bodyCorners = CoreShieldLayout.Corners(bodyOuter ?? body, parent);
        var headCorners = head != null ? CoreShieldLayout.Corners(headOuter ?? head, parent) : null;
        var (left, right) = ShieldRules.ShieldEdges(bodyCorners[0].x, bodyCorners[2].x, headCorners != null,
            headCorners?[0].x ?? 0f, headCorners?[2].x ?? 0f);
        var cell = ShieldRules.ShieldCell(CoreSettings.Number(ShieldRules.ShieldCellKey));
        var overGap = headCorners == null || BanditBossBarRules.ViewOn(CoreSettings.Number(ShieldRules.ShieldGapKey));
        var bottom = bodyCorners[0].y;
        var top = bodyCorners[1].y;
        if (headCorners != null && overGap)
        {
            bottom = Math.Min(bottom, headCorners[0].y);
            top = Math.Max(top, headCorners[1].y);
        }
        var bodyBand = Create(parent, name, left, bottom, right, top, cell, cellAlpha, frameFollows);
        bodyBand.area = AreaOf(left, bottom, right, top);
        CoreShieldBand headBand = null;
        if (!overGap)
        {
            headBand = Create(parent, name + "_Head", left, headCorners[0].y, right, headCorners[1].y, cell, cellAlpha, frameFollows);
            // A band of its own over the head bar lifts the texts it overlaps too.
            headBand.area = AreaOf(left, headCorners[0].y, right, headCorners[1].y);
        }
        var placed = head != null ? new Transform[] { body, head } : new Transform[] { body };
        bodyBand.bars = placed;
        bodyBand.Place();
        if (headBand != null)
        {
            headBand.bars = placed;
            headBand.Place();
        }
        var line = FormattableString.Invariant($"band={right - left:0.#}x{top - bottom:0.#} left={left:0.#} right={right:0.#} bottom={bottom:0.#} top={top:0.#} head={(headCorners != null ? 1 : 0)} outer={(bodyOuter != null ? bodyOuter.name : "-")}/{(headOuter != null ? headOuter.name : "-")} gap={(overGap ? "over" : "split")} parent={parent.name} clips={CoreShieldLayout.ClipsBetween(body, parent)}/{CoreShieldLayout.ClipsBetween(head, parent)} textsBelow={bodyBand.TextsBelow()} cell={cell:0.#} rows={bodyBand.Rows}");
        return (bodyBand, headBand, line);
    }
}
