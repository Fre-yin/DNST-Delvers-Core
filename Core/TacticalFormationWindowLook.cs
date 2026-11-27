using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The formation window's looks as the author chose them from the drafts, only how things are shown:
//   1 the role stands in the member's row at its right, symbol above the role's name, on the row's own background; the row ends
//      well before the grid (TacticalFormationWindowRoles.AddRoleField).
//   2 the grid keeps its two colours; a taken cell is framed, the leader's in gold, a summon place's in blue.
//   3 a member's summon places stand right below it, indented, joined to it by one line down with a branch to each place
//      (TacticalFormationListOrder); the line's colour is the one of the author's drawing.
//   4 a member down or away and a summon place without its summon: the picture grey in the row and in the grid, the row hatched.
//   5 no empty "+" rows: one button in the style of the bottom bar's buttons below the last row opens the choice.
//   7 the choice of what to add shows tiles with the picture above the name (TacticalFormationWindowSelection).
//   8 the role choice: a stripe in the role's colour, the game's check mark at the current role; the neutral ring is silver.
//   9 the list's scroll bar right at the rows' edge.
//  10 above the grid: "Front" larger, small arrows up spread over the grid's width, the line stays; the
//      ornament of the content box's sprite covered in the box's face colour (CoverOrnament).
//  11 the priority places' numbers, since 21 A as the skill slot's key badge (TacticalFormationWindowFrames).
//  12 the pager shows a box per formation, the shown one filled, centred between the arrows.
//  13 the place being chosen for is framed in gold; "clear slot" stands set apart at the end of the choice.
//  14 dragging in the grid: the target framed in gold and lightly tinted, the dragged picture half clear, the member faint at
//      its old place.
//  15 dragging in the list: the target row framed in gold, on the top row "Group leader" in gold, the member faint in its row.
//  20 the grid with its head stands at the content box's right, so the role and cast choices open between list and grid;
//      the tempo switch moves to the grid's lower left (TacticalFormationWindowTempo).
//  21 to 23: the game's frames on the places, the rows and the role, and the wider window (TacticalFormationWindowFrames).
// Nearness to the game: frames, buttons, colours and fonts come from the game's own parts where it has them.
internal static partial class TacticalFormationWindow
{
    // ---- 20: the grid at the right ----
    // The page's right padding: the grid ends about 50 units before the content box's right edge (at 41 the images on bfa691a showed
    // 63), and its left edge lies about 195 units into the box, clear of the role choice (180 wide from the role field, which
    // reaches some 35 units into the box; at 41 it touched the grid by two pixels). The window scales with the screen, the units stay.
    private const float GridRightMargin = 26f;
    // The grid's rectangle (AddGrid) and the room the role and cast choices keep before it.
    private static RectTransform gridRect;
    private const float GridGap = 4f;

    // ---- 5: the add button ----

    private const float AddButtonWidth = 170f, AddButtonHeight = 32f;
    private static Transform footerButton;

    // ---- 9 to 12 ----
    private const float FrontScale = 1.5f, FrontHeadHeight = 30f, FrontArrow = 12f;
    private static readonly float[] FrontArrowAt = { 0.06f, 0.19f, 0.32f, 0.68f, 0.81f, 0.94f };
    private static Sprite frontArrow;
    private static Color frontArrowColour = Color.white;
    private const float DotSize = 10f, DotGap = 6f, DotPadding = 8f;
    private static readonly List<(Image Fill, Image[] Frame)> pageDots = new();

    // ---- 13 to 15 ----
    private static readonly Color Pale = new(1f, 1f, 1f, 0.3f);
    private static readonly Color GhostTint = new(1f, 1f, 1f, 0.6f);
    private const float TargetTint = 0.25f;
    private static TMP_Text listTitle;
    private static (int Row, int Place) choosing = (-1, -1);
    // ---- 1: the role in the row ----
    private const float RoleColumn = 42f, RoleIconInRow = 24f;
    // The row: padding, portrait, name and casts, role, with the row's spacing between them.
    private const float RowWidth = 2f + Portrait + 6f + NameWidth + 6f + RoleColumn + 2f;

    // ---- 2: frames in the grid ----
    private const float FrameWidth = 2f;
    // A thin frame (a page box, a priority place): 1.5 units keep it a pixel wide down to 1280x720 at the game's usual interface scale;
    // at 1920x1080 it is a pixel and a half.
    private const float ThinFrame = 1.5f;
    private static readonly Color FrameMember = new(0.71f, 0.51f, 0.35f, 1f);
    private static readonly Color FrameLeader = new(0.886f, 0.714f, 0.369f, 1f);
    private static readonly Color FrameSummon = new(0.369f, 0.529f, 0.722f, 1f);
    private const float FrameAwayAlpha = 0.45f;
    private static readonly List<Image[]> cellFrames = new();

    // ---- 3: the line from a summoner to its places ----
    private const float Indent = 22f, TreeX = 10f, TreeWidth = 2f;
    // The core of the stroke in the author's drawing (#B1582A), not its darker edge.
    private static readonly Color TreeColor = new(0.694f, 0.345f, 0.165f, 1f);

    // ---- 4: grey and hatched ----
    private static readonly Color GreyTint = new(1f, 1f, 1f, 0.85f);
    private const float GreyShade = 0.8f;
    private const int HatchStep = 8;
    private static readonly Color HatchLine = new(1f, 1f, 1f, 0.07f);
    private static readonly Dictionary<int, Sprite> greys = new();
    private static Texture2D hatch;

    private static void ClearLook()
    {
        ResetHovers();
        hovers.Clear();
        cellFrames.Clear();
        pageDots.Clear();
        footerButton = null;
        contentBox = null;
        windowPanel = null;
        gridRect = null;
        frontArrow = null;
        listTitle = null;
        choosing = (-1, -1);
    }

    // The grey copies go with Core's shutdown; a sprite kept as it is (Grey failed) is the game's and stays.
    private static void ForgetGreys()
    {
        foreach (var pair in greys)
        {
            var grey = pair.Value;
            if (grey == null || grey.GetInstanceID() == pair.Key) continue;
            var texture = grey.texture;
            UnityEngine.Object.Destroy(grey);
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
        greys.Clear();
        framePartsListed = false;
        columnLogged = false;
    }

    // A frame of four thin bars at the inner edge of a part: a grid cell, a row, a priority place, a page box.
    private static Image[] AddFrame(Transform cell, float width = FrameWidth)
    {
        var sides = new Image[4];
        for (var i = 0; i < 4; i++)
        {
            var side = new GameObject("Frame_" + i, Il2CppType.Of<RectTransform>());
            side.transform.SetParent(cell, false);
            (side.GetComponent<LayoutElement>() ?? side.AddComponent<LayoutElement>()).ignoreLayout = true;
            var image = side.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            var rect = side.GetComponent<RectTransform>();
            var horizontal = i < 2;
            var far = i % 2 == 1;
            rect.anchorMin = horizontal ? new Vector2(0f, far ? 0f : 1f) : new Vector2(far ? 1f : 0f, 0f);
            rect.anchorMax = horizontal ? new Vector2(1f, far ? 0f : 1f) : new Vector2(far ? 1f : 0f, 1f);
            rect.pivot = horizontal ? new Vector2(0.5f, far ? 0f : 1f) : new Vector2(far ? 1f : 0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = horizontal ? new Vector2(0f, width) : new Vector2(width, 0f);
            sides[i] = image;
        }
        return sides;
    }

    private static void ShowFrame(Image[] sides, Color? colour)
    {
        if (sides == null) return;
        foreach (var side in sides)
        {
            side.enabled = colour != null;
            if (colour is { } shown) side.color = shown;
        }
    }

    private static void ShowFrame(Image[] sides, Guid unit, bool shown)
    {
        var colour = unit == grid.Leader ? FrameLeader : summonPlaces.ContainsKey(unit) ? FrameSummon : FrameMember;
        if (shown && IsAway(unit)) colour.a = FrameAwayAlpha;
        foreach (var side in sides)
        {
            side.enabled = shown;
            side.color = colour;
        }
    }

    // A picture as shown in a row or cell: grey for a member down or away and a place without its summon.
    private static void ShowFace(Image image, Sprite face, bool away)
    {
        image.sprite = away ? Grey(face) : face;
        image.enabled = image.sprite != null;
        // Without a grey copy the picture is faded the old way, so absence still shows.
        image.color = !away ? Color.white : image.sprite != null && image.sprite == face ? AwayTint : GreyTint;
    }

    // The sprite's pixels in grey, made once per sprite through a render texture as for a summon's picture (Cropped), at the place
    // the packed pixels have in the sprite's full rectangle (textureRectOffset); the sprite as it is, kept for the session, when that
    // fails (ShowFace fades it the old way).
    private static Sprite Grey(Sprite sprite)
    {
        if (sprite == null) return null;
        var key = sprite.GetInstanceID();
        if (greys.TryGetValue(key, out var known) && known != null) return known;
        RenderTexture copy = null;
        var before = RenderTexture.active;
        try
        {
            var texture = sprite.texture;
            var area = sprite.textureRect;
            var offset = sprite.textureRectOffset;
            int w = (int)area.width, h = (int)area.height;
            int fullW = Math.Max(w, (int)sprite.rect.width), fullH = Math.Max(h, (int)sprite.rect.height);
            if (texture == null || w < 1 || h < 1)
            {
                greys[key] = sprite;
                return sprite;
            }
            copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, copy);
            RenderTexture.active = copy;
            var frame = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            frame.SetPixels32(new Color32[fullW * fullH]);
            frame.ReadPixels(new Rect(area.x, area.y, w, h), (int)offset.x, (int)offset.y);
            var pixels = frame.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var v = (byte)Mathf.Clamp((p.r * 0.3f + p.g * 0.59f + p.b * 0.11f) * GreyShade, 0f, 255f);
                pixels[i] = new Color32(v, v, v, p.a);
            }
            frame.SetPixels32(pixels);
            frame.Apply();
            var pivot = new Vector2(sprite.pivot.x / Mathf.Max(1f, sprite.rect.width), sprite.pivot.y / Mathf.Max(1f, sprite.rect.height));
            var grey = Sprite.Create(frame, new Rect(0f, 0f, fullW, fullH), pivot, sprite.pixelsPerUnit);
            grey.name = sprite.name + "_Grey";
            frame.hideFlags = HideFlags.DontUnloadUnusedAsset;
            grey.hideFlags = HideFlags.DontUnloadUnusedAsset;
            greys[key] = grey;
            return grey;
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_GREY_FAILED: " + ex.Message);
            greys[key] = sprite;
            return sprite;
        }
        finally
        {
            RenderTexture.active = before;
            if (copy != null) RenderTexture.ReleaseTemporary(copy);
        }
    }

    // A fine diagonal hatch over the whole row, behind its picture and text.
    private static RawImage AddHatch(GameObject entry)
    {
        var holder = new GameObject("Hatch", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(entry.transform, false);
        (holder.GetComponent<LayoutElement>() ?? holder.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = holder.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = Hatch();
        image.enabled = false;
        return image;
    }

    private static Texture2D Hatch()
    {
        if (hatch != null) return hatch;
        hatch = new Texture2D(HatchStep, HatchStep, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var clear = new Color(0f, 0f, 0f, 0f);
        for (var y = 0; y < HatchStep; y++)
            for (var x = 0; x < HatchStep; x++)
                hatch.SetPixel(x, y, (x + y) % HatchStep == 0 ? HatchLine : clear);
        hatch.Apply();
        hatch.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return hatch;
    }

    private static void ShowHatch(RawImage image, bool on, float width)
    {
        if (image == null) return;
        image.enabled = on;
        if (on) image.uvRect = new Rect(0f, 0f, width / HatchStep, EntryHeight / HatchStep);
    }

    // The line in a summon place's indent: down from the row above (its summoner or the place before), a branch to the row, and on
    // down to the next place unless this is the last one.
    private static (Image Down, Image Branch) AddTree(Transform line)
    {
        Image Bar(string name)
        {
            var bar = new GameObject(name, Il2CppType.Of<RectTransform>());
            bar.transform.SetParent(line, false);
            (bar.GetComponent<LayoutElement>() ?? bar.AddComponent<LayoutElement>()).ignoreLayout = true;
            var image = bar.AddComponent<Image>();
            image.color = TreeColor;
            image.raycastTarget = false;
            image.enabled = false;
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            return image;
        }
        var down = Bar("TreeDown");
        var downRect = down.GetComponent<RectTransform>();
        downRect.pivot = new Vector2(0.5f, 1f);
        downRect.anchoredPosition = new Vector2(TreeX, RowSpacing);
        var branch = Bar("TreeBranch");
        var branchRect = branch.GetComponent<RectTransform>();
        branchRect.pivot = new Vector2(0f, 0.5f);
        branchRect.anchoredPosition = new Vector2(TreeX - TreeWidth * 0.5f, -EntryHeight * 0.5f);
        branchRect.sizeDelta = new Vector2(Indent - TreeX + TreeWidth * 0.5f, TreeWidth);
        return (down, branch);
    }

    private static void ShowTree(Row row, bool under, bool last)
    {
        if (row.TreeDown == null || row.TreeBranch == null) return;
        row.TreeDown.enabled = row.TreeBranch.enabled = under;
        if (!under) return;
        var length = RowSpacing + (last ? EntryHeight * 0.5f + TreeWidth * 0.5f : EntryHeight);
        row.TreeDown.rectTransform.sizeDelta = new Vector2(TreeWidth, length);
    }

    // The row indented under its summoner or not; the layout is touched only when that changes.
    private static void Indented(Row row, bool under)
    {
        var indent = under ? Indent : 0f;
        if (row.Line == null || row.Indent == indent) return;
        row.Indent = indent;
        row.Line.padding = new RectOffset((int)indent, 0, 0, 0);
        Fixed(row.Back.gameObject, RowWidth - indent, EntryHeight);
        if (row.IndentHit != null)
        {
            row.IndentHit.enabled = indent > 0f;
            row.IndentHit.rectTransform.sizeDelta = new Vector2(indent, 0f);
        }
    }

    // A clear area over the indent left of the row, part of the row: hovering, dragging onto and right clicking the line there act
    // on the row as on its own ground.
    private static Image AddIndentHit(GameObject entry)
    {
        var hit = new GameObject("IndentHit", Il2CppType.Of<RectTransform>());
        hit.transform.SetParent(entry.transform, false);
        (hit.GetComponent<LayoutElement>() ?? hit.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = hit.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        var image = hit.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.enabled = false;
        return image;
    }

    // ---- 5: one add button in place of the empty rows ----

    // A copy of the bottom bar's Close button in the middle of the free row, captioned "+ Add"; the row's own click opens the same
    // choice, so a click beside the button still works.
    private static GameObject AddAddButton(GameObject entry, RectTransform entryRect, Row row)
    {
        if (footerButton == null) return null;
        var button = DelversMenuWindow.Clone(footerButton, entry.transform, "Btn_Add");
        // The Close button had its hover already: the copy brings those pictures along but not their events (DelversMenuWindow.Clone),
        // so exactly they go here and AddHover makes them anew.
        for (var i = button.transform.childCount - 1; i >= 0; i--)
        {
            var child = button.transform.GetChild(i);
            if (child.name is "Hover" or "Frame_0" or "Frame_1" or "Frame_2" or "Frame_3") UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        (button.GetComponent<LayoutElement>() ?? button.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(AddButtonWidth, AddButtonHeight);
        var caption = button.GetComponentInChildren<TMP_Text>(true);
        if (caption != null) DelversMenuWindow.SetText(caption, "+ " + MenuText.Pick(TacticalFormationText.AddButton));
        DelversMenuWindow.EnsureButton(button).onClick.AddListener((Action)(() =>
        {
            try { OpenAdder(entryRect); }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_UI_FAILED button=Btn_Add: " + ex.Message); }
        }));
        row.AddHover = AddHover(button);
        // The hover's event trigger takes the wheel too: it goes on to the list's scrolling, as on the rows.
        var trigger = button.GetComponent<EventTrigger>();
        if (trigger != null) AddPointer(trigger, EventTriggerType.Scroll, pointer =>
        {
            if (pointer != null) listScroll?.OnScroll(pointer);
        });
        button.SetActive(false);
        return button;
    }

    // ---- 8: the role's colour ----

    private static Color RoleColour(TacticalRole role) => role switch
    {
        TacticalRole.Tank => new Color(0.357f, 0.561f, 0.839f, 1f),
        TacticalRole.Damage => new Color(0.788f, 0.345f, 0.29f, 1f),
        TacticalRole.Support => new Color(0.435f, 0.722f, 0.353f, 1f),
        _ => new Color(0.64f, 0.66f, 0.69f, 1f)
    };

    // ---- 10: the head above the grid ----

    // The arrow of the game's work plan counter, turned to point up, for the head above the grid; a drawn triangle without it.
    private static void TakeFrontArrow(Button source)
    {
        var image = source != null ? source.image : null;
        if (image != null && image.sprite != null)
        {
            frontArrow = image.sprite;
            frontArrowColour = image.color;
            return;
        }
        frontArrow = Triangle();
        frontArrowColour = FrontColor;
    }

    private static Sprite triangle;

    // Made once and kept: only used when the work plan counter has no arrow to take.
    private static Sprite Triangle()
    {
        if (triangle != null) return triangle;
        const int size = 9;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        // Pointing right, as the counter's arrow does; the head turns it up.
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, Math.Abs(y - size / 2) <= (size - 1 - x) / 2 ? Color.white : new Color(0f, 0f, 0f, 0f));
        texture.Apply();
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        triangle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        triangle.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return triangle;
    }

    // "Front" large in the middle, small arrows up spread over the grid's width, the line below as before.
    private static void AddFront(DelversMenuWindow.Parts parts, Transform parent, float width)
    {
        var bar = Layout<VerticalLayoutGroup>(parent, "Front", 2f, new RectOffset(0, 0, 0, 0));
        bar.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        var head = new GameObject("FrontHead", Il2CppType.Of<RectTransform>());
        head.transform.SetParent(bar.transform, false);
        Fixed(head, width, FrontHeadHeight);
        Text(parts, head.transform, MenuText.Pick(TacticalFormationText.Front), width);
        var text = head.GetComponentInChildren<TMP_Text>();
        text.fontSize *= FrontScale;
        text.alignment = TextAlignmentOptions.Center;
        text.color = FrontColor;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        foreach (var at in FrontArrowAt)
        {
            var arrow = Picture(head.transform, "Arrow", frontArrow, FrontArrow);
            var rect = arrow.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(at, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localEulerAngles = new Vector3(0f, 0f, 90f);
            arrow.GetComponent<Image>().color = frontArrowColour;
        }
        var line = new GameObject("Line", Il2CppType.Of<RectTransform>());
        line.transform.SetParent(bar.transform, false);
        line.AddComponent<Image>().color = FrontColor;
        Fixed(line, width, 3f);
    }

    // A diagnosis only: every picture of the frame outside the list, the content and the buttons is named once per session with its
    // path, its rectangle and its sprite (CORE_TACTICAL_HEAD_PARTS). It showed that the ornament above the grid has no picture of its
    // own; CoverOrnament covers it.
    private static bool framePartsListed;

    private static void ListFrameParts(Transform panel, Transform content, Transform list)
    {
        // Once per session: it writes a long line (the layout before it is Build's).
        if (framePartsListed) return;
        framePartsListed = true;
        var parts = new List<string>();
        foreach (var image in panel.GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            var t = image.transform;
            if (t.IsChildOf(content) || t.IsChildOf(list) || t.name.StartsWith("Btn_", StringComparison.Ordinal)
                || (t.parent != null && t.parent.name.StartsWith("Btn_", StringComparison.Ordinal))) continue;
            var size = image.rectTransform.rect.size;
            parts.Add(PathIn(panel, t) + ":" + (image.sprite != null ? image.sprite.name : "-") + ":" + size.x.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                      + "x" + size.y.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                      + ":" + (image.enabled && t.gameObject.activeInHierarchy ? 1 : 0));
        }
        DelversHost.Info($"CORE_TACTICAL_HEAD_PARTS {(parts.Count == 0 ? "-" : string.Join(" ", parts))}");
    }

    // The ornament above the grid is part of the content box's own sprite (Img_OptionContent, ContentsBox; the frame's listing shows
    // no picture of its own for it): a line of two pixels in the frame's colour along the box's top edge, inside the box, from about
    // 23 to 78 per cent of its width (measured at 1920x1080 on 694a093). Taking the game's plain box instead lost the box's lighter
    // face and round corners, so the box keeps its sprite and only that strip is covered in the box's own face colour. Found by the
    // exact path and the sprite's name; anything else stays as it is. The box's bottom also bounds the choices (KeepInside), so they
    // stay above the bottom bar unless one is taller than the room left.
    private const string ContentBoxPath = "Img_OptionContent", ContentBoxSprite = "ContentsBox";
    private const float OrnamentFrom = 0.225f, OrnamentTo = 0.79f, OrnamentHeight = 3f;
    private static readonly Color ContentFace = new(0.169f, 0.157f, 0.184f, 1f);
    private static RectTransform contentBox;

    private static void CoverOrnament(Transform panel)
    {
        var box = panel.Find(ContentBoxPath)?.GetComponent<Image>();
        // The box bounds the choices whatever its sprite; only the cover is bound to the sprite's name.
        if (box != null) contentBox = box.rectTransform;
        var covered = box != null && box.sprite != null && box.sprite.name == ContentBoxSprite;
        if (covered)
        {
            var cover = new GameObject("OrnamentCover", Il2CppType.Of<RectTransform>());
            cover.transform.SetParent(box.transform, false);
            (cover.GetComponent<LayoutElement>() ?? cover.AddComponent<LayoutElement>()).ignoreLayout = true;
            var rect = cover.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(OrnamentFrom, 1f);
            rect.anchorMax = new Vector2(OrnamentTo, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, OrnamentHeight);
            var image = cover.AddComponent<Image>();
            image.color = ContentFace;
            image.raycastTarget = false;
        }
        DelversHost.Info($"CORE_TACTICAL_HEAD_BOX cover={(covered ? 1 : 0)} box={(box?.sprite != null ? box.sprite.name : "-")}");
    }

    private static string PathIn(Transform root, Transform part)
    {
        var names = new List<string>();
        for (var at = part; at != null && at != root; at = at.parent) names.Insert(0, at.name);
        return names.Count == 0 ? root.name : string.Join("/", names);
    }

    // ---- 7 and 13: a choice stays inside the window ----

    private static readonly Color TilesGround = new(0.12f, 0.11f, 0.13f, 0.96f);
    private static RectTransform windowPanel;

    // The window's copy is drawn larger than the canvas' own units (the game's interface scale comes along with the copied window);
    // a choice made under the canvas takes the same scale, so it is as large as the rows and its offsets, given in the window's
    // units, are scaled with it. Returns the scale.
    private static float ScaleLikeWindow(RectTransform popup)
    {
        if (popup == null || windowPanel == null || canvasRect == null) return 1f;
        var scale = windowPanel.lossyScale.x / Mathf.Max(0.0001f, canvasRect.lossyScale.x);
        if (scale <= 0f || float.IsNaN(scale)) return 1f;
        popup.localScale = new Vector3(scale, scale, 1f);
        return scale;
    }

    // The choices' own edges (the author's hand test of 0.5.2, the add choice looked placed unevenly). Their lines are the
    // options window's list row, whose picture keeps a clear margin at its left and right (3 pixels at 1920x1080, measured on
    // 3c8fb01); the dark ground of the add choice's tiles and the options window's end piece reached into that margin (the end piece
    // only at its right, its own left margin matches). Both now keep it, so every edge of a choice lines up.
    private const float ChoiceSide = 3f / 1.1f;

    // The end piece under a choice whose lines are width wide: as wide as their visible face, its own left margin included.
    private static void AddChoiceEnd(Transform parent, float width)
    {
        if (uiParts?.DropdownEnd == null) return;
        var end = new GameObject("End", Il2CppType.Of<RectTransform>());
        end.transform.SetParent(parent, false);
        end.AddComponent<Image>().sprite = uiParts.DropdownEnd;
        Fixed(end, width - ChoiceSide, 4f);
    }

    // The dark ground behind the tiles, inside the lines' margins; the part itself stays clear (it still takes the wheel and clicks).
    private static void AddChoiceGround(GameObject part)
    {
        var image = part.GetComponent<Image>() ?? part.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        var ground = new GameObject("Ground", Il2CppType.Of<RectTransform>());
        ground.transform.SetParent(part.transform, false);
        ground.transform.SetAsFirstSibling();
        (ground.GetComponent<LayoutElement>() ?? ground.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = ground.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(ChoiceSide, 0f);
        rect.offsetMax = new Vector2(-ChoiceSide, 0f);
        var groundImage = ground.AddComponent<Image>();
        groundImage.color = TilesGround;
        groundImage.raycastTarget = false;
    }

    // A dropdown that would run past the content box's bottom (the bottom bar lies below it; the window's when the box is not found)
    // opens above what opened it (the place, the role field, the add row), so that stays in sight; one that would then run past the
    // window's top is moved down to it. Laid out first so its size is known; the corners come through an Il2Cpp array (a managed one
    // stays empty under Il2Cpp). A choice taller than that room still runs past the bottom (the add and cast choices scroll before).
    // leftOfGrid: the role and cast choices end before the grid's left edge (point 20), moved left when they would reach into it.
    private static void KeepInside(RectTransform popup, RectTransform opener, bool leftOfGrid = false)
    {
        if (popup == null || windowPanel == null) return;
        try
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
            var own = new Il2CppStructArray<Vector3>(4);
            var frame = new Il2CppStructArray<Vector3>(4);
            var window = new Il2CppStructArray<Vector3>(4);
            popup.GetWorldCorners(own);
            (contentBox != null ? contentBox : windowPanel).GetWorldCorners(frame);
            windowPanel.GetWorldCorners(window);
            var shift = 0f;
            if (own[0].y < frame[0].y)
            {
                var top = frame[0].y;
                if (opener != null)
                {
                    var from = new Il2CppStructArray<Vector3>(4);
                    opener.GetWorldCorners(from);
                    top = from[1].y;
                }
                shift = top - own[0].y;
            }
            if (own[1].y + shift > window[1].y) shift = window[1].y - own[1].y;
            var left = 0f;
            if (leftOfGrid && gridRect != null)
            {
                var grid = new Il2CppStructArray<Vector3>(4);
                gridRect.GetWorldCorners(grid);
                var gap = GridGap * (grid[2].x - grid[0].x) / Mathf.Max(1f, gridRect.rect.width);
                if (own[2].x > grid[0].x - gap) left = Mathf.Max(window[0].x - own[0].x, grid[0].x - gap - own[2].x);
            }
            if (shift == 0f && left == 0f) return;
            popup.position += new Vector3(left, shift, 0f);
            DelversHost.Info("CORE_TACTICAL_KEEP_INSIDE popup=" + popup.name + " shift=" + shift.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                             + " left=" + left.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_KEEP_INSIDE_FAILED: " + ex.Message); }
    }

    // ---- 12: a box per formation ----

    // Between the arrows, as wide as the boxes with the same margin at both ends, so the row stays centred for any count.
    private static void AddPageDots(Transform parent)
    {
        var count = TacticalFormationBook.Size;
        var holder = Layout<HorizontalLayoutGroup>(parent, "Pages", DotGap, new RectOffset((int)DotPadding, (int)DotPadding, 0, 0));
        holder.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Fixed(holder, count * DotSize + (count - 1) * DotGap + 2f * DotPadding, ArrowHeight);
        for (var i = 0; i < count; i++)
        {
            var dot = new GameObject("Page_" + (i + 1), Il2CppType.Of<RectTransform>());
            dot.transform.SetParent(holder.transform, false);
            Fixed(dot, DotSize, DotSize);
            var fill = dot.AddComponent<Image>();
            fill.raycastTarget = false;
            pageDots.Add((fill, AddFrame(dot.transform, ThinFrame)));
        }
    }

    // The boxes in a narrower gap: smaller spaces between them down to two units with a margin of two, then smaller boxes down to six.
    private static void FitPageDots(float width)
    {
        if (pageDots.Count < 2 || pageDots[0].Fill == null) return;
        var holder = pageDots[0].Fill.transform.parent;
        var count = pageDots.Count;
        if (count * DotSize + (count - 1) * DotGap + 2f * DotPadding <= width) return;
        var gap = Mathf.Max(2f, (width - count * DotSize - 4f) / (count - 1));
        var size = Mathf.Clamp((width - 4f - (count - 1) * gap) / count, 6f, DotSize);
        var layout = holder.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = gap;
        layout.padding = new RectOffset(2, 2, 0, 0);
        foreach (var (fill, _) in pageDots) Fixed(fill.gameObject, size, size);
        Fixed(holder.gameObject, count * size + (count - 1) * gap + 4f, ArrowHeight);
        DelversHost.Info("CORE_TACTICAL_PAGES_FIT room=" + width.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " gap=" + gap.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                         + " box=" + size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void ShowPageDots()
    {
        for (var i = 0; i < pageDots.Count; i++)
        {
            var (fill, frame) = pageDots[i];
            fill.color = i == page ? FrontColor : new Color(0f, 0f, 0f, 0.25f);
            ShowFrame(frame, FrontColor);
        }
    }

    // ---- 18: hover on the window's buttons ----

    // The game's buttons in the window show no change under the pointer; ours frame them in gold and light their face, behind the
    // caption (on all of the window's buttons).
    private static readonly Color HoverLight = new(1f, 1f, 1f, 0.12f);
    private static readonly List<(Image Face, Image[] Frame)> hovers = new();

    // Every hover back to rest when the window closes.
    private static void ResetHovers()
    {
        foreach (var hover in hovers) ResetHover(hover);
    }

    // One button's hover back to rest: also when it is hidden under the pointer, which then sends no exit, so it would show gold
    // when it comes back.
    private static void ResetHover((Image Face, Image[] Frame) hover)
    {
        if (hover.Face == null) return;
        hover.Face.enabled = false;
        ShowFrame(hover.Frame, null);
    }

    private static (Image Face, Image[] Frame) AddHover(GameObject button)
    {
        if (button == null) return default;
        var light = new GameObject("Hover", Il2CppType.Of<RectTransform>());
        light.transform.SetParent(button.transform, false);
        light.transform.SetAsFirstSibling();
        (light.GetComponent<LayoutElement>() ?? light.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = light.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var face = light.AddComponent<Image>();
        face.color = HoverLight;
        face.raycastTarget = false;
        face.enabled = false;
        var frame = AddFrame(button.transform);
        hovers.Add((face, frame));
        var trigger = button.GetComponent<EventTrigger>() ?? button.AddComponent<EventTrigger>();
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            face.enabled = true;
            ShowFrame(frame, FrameLeader);
        });
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            face.enabled = false;
            ShowFrame(frame, null);
        });
        return (face, frame);
    }

    // ---- 13: the place being chosen for ----

    private static void ShowChoosing(bool on)
    {
        var (row, place) = choosing;
        if (row < 0 || row >= rows.Count || place < 0 || place >= rows[row].CastFrames.Length) return;
        ShowFrame(rows[row].CastFrames[place], on ? FrameLeader : null);
    }
}
