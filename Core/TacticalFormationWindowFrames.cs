using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The frames in the formation window:
//   21 the five priority places as separate places (the author's choice A of the sheet): a thin brown outer edge, dark
//       inside, an even gap between them, the place's number as a small badge at its lower right.
//   22 a member's whole row in the frame of the unit panel's portrait (the author's choice of the drafts); the role symbol
//       and its name together in one framed block with room to every edge (choice 1 of the sheet).
//   23 the window wider, so the role and cast choices keep clear room before the grid.
// The row's frame is the game's own sprite, read once per session from the frame round EntitySummaryUIView's portrait and named
// once in the log (CORE_TACTICAL_FRAMES). Where it is not found, or reading it fails in any way (TacticalFormationFrameRead), it
// is drawn in the colours taken from the game's own bars (images at 1920x1080 on bfa691a), and the window opens all the same.
internal static partial class TacticalFormationWindow
{
    private sealed class GameFrame
    {
        internal Sprite Sprite;
        internal Image.Type Type;
        internal float Ppu;
    }

    private static GameFrame portraitFrame;
    private static bool framesRead;

    // The game's colours, for the places, the role block and the drawn stand-in of the row's frame.
    private static readonly Color BarLine = new(0.388f, 0.239f, 0.2f, 1f);     // #633D33
    private static readonly Color BarTop = new(0.569f, 0.345f, 0.278f, 1f);    // #915847
    private static readonly Color SlotRim = new(0.141f, 0.118f, 0.122f, 1f);   // #241E1F
    private static readonly Color SlotDark = new(0.11f, 0.102f, 0.118f, 1f);   // #1C1A1E
    private static readonly Color KeyDigit = new(0.725f, 0.698f, 0.745f, 1f);  // #B9B2BE

    // ---- 23: the wider window ----
    // 100 left the role choice of the lowest row about 12 units before the grid; 140 gives it about 50 and still fits at 1280x720.
    private const float WindowExtra = 140f;
    private static bool widenLogged;

    // ---- whole pixels (the author's marks on e64cabb) ----
    // The window draws about 1.1 screen pixels per unit at 1920x1080 and 0.73 at 1280x720, so a gap of 2 units came out 2 or 3
    // pixels wide. Sizes that repeat (the places, their gaps, the role symbol and its block) are taken in whole pixels of the
    // window's scale; the canvas snaps every part to pixels, so equal sizes stay equal on screen.
    private static float pixelsPerUnit;

    private static void MeasurePixels()
    {
        pixelsPerUnit = 0f;
        try
        {
            var scale = windowPanel != null ? windowPanel.lossyScale.x : 0f;
            if (scale > 0.05f && scale < 20f) pixelsPerUnit = scale;
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PIXELS_FAILED: " + ex.Message); }
    }

    // Units that give a whole number of screen pixels, at least one; the units themselves before the scale is known.
    private static float Px(float units)
        => pixelsPerUnit > 0f ? Mathf.Max(1f, Mathf.Round(units * pixelsPerUnit)) / pixelsPerUnit : units;

    private static void ForgetFrames()
    {
        pixelsPerUnit = 0f;
        framesRead = false;
        widenLogged = false;
        portraitFrame = null;
    }

    // Once per session. The game's fields are read in a method of their own, so a field gone after an update fails inside the
    // guarded call (also when that method cannot be compiled any more) and the row's frame is drawn instead.
    private static void ReadFrames()
    {
        if (framesRead) return;
        framesRead = true;
        portraitFrame = TacticalFormationFrameRead.Try(ReadPortraitFrame, "portrait", TacticalFormationButton.ReportOnce);
        DelversHost.Info("CORE_TACTICAL_FRAMES portrait=" + Named(portraitFrame));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GameFrame ReadPortraitFrame()
    {
        foreach (var view in Resources.FindObjectsOfTypeAll<EntitySummaryUIView>())
        {
            var portrait = view != null ? view._imgPortrait : null;
            if (portrait == null || portrait.transform.parent == null) continue;
            // The first sliced picture round the portrait is its frame.
            for (var at = portrait.transform.parent; at != null; at = at.parent)
            {
                var image = at.GetComponent<Image>();
                if (image != null && image.sprite != null && image.type == Image.Type.Sliced) return FrameOf(image);
            }
            foreach (var image in portrait.transform.parent.GetComponentsInChildren<Image>(true))
                if (image != null && image != portrait && image.sprite != null && image.type == Image.Type.Sliced) return FrameOf(image);
        }
        return null;
    }

    private static GameFrame FrameOf(Image image)
        => image == null || image.sprite == null ? null : new GameFrame { Sprite = image.sprite, Type = image.type, Ppu = image.pixelsPerUnitMultiplier };

    private static string Named(GameFrame frame) => frame == null ? "drawn" : frame.Sprite.name + ":" + frame.Type.ToString().ToLowerInvariant();

    // The game's sprite on a picture as the game draws it; a sprite with borders is sliced so the frame keeps its width.
    private static void UseFrame(Image image, GameFrame frame, bool fillCenter)
    {
        image.sprite = frame.Sprite;
        image.type = frame.Type == Image.Type.Simple && frame.Sprite.border != Vector4.zero ? Image.Type.Sliced : frame.Type;
        image.pixelsPerUnitMultiplier = frame.Ppu > 0f ? frame.Ppu : 1f;
        image.fillCenter = fillCenter;
        image.color = Color.white;
    }

    // A stretched picture inside parent, inset by the same distance on every side; behind the parent's other parts when first.
    private static Image Inset(Transform parent, string name, float inset, Color color, bool first)
    {
        var part = new GameObject(name, Il2CppType.Of<RectTransform>());
        part.transform.SetParent(parent, false);
        if (first) part.transform.SetAsFirstSibling();
        (part.GetComponent<LayoutElement>() ?? part.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = part.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = -rect.offsetMin;
        var image = part.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ---- 21: the places ----
    // A place is SlotSize square with a gap of SlotGap to the next (4 pixels at 1920x1080), its icon SlotIconRoom inside the edge.
    private const float SlotSize = 20f, SlotGap = 3.6f, SlotIconRoom = 2f, KeyBadge = 9f;

    private static float SlotIcon => Px(SlotSize) - 2f * Px(SlotIconRoom);

    // The place's brown outer edge, one pixel wide, and its dark inside; returns the inside to tint.
    private static Image AsSlot(GameObject box)
    {
        box.GetComponent<Image>().color = BarLine;
        return Inset(box.transform, "Face", Px(1f), SlotDark, true);
    }

    // The place's number as a small badge at its lower right, inside the edge, above the icon.
    private static GameObject AddKeyBadge(Transform box, int number)
    {
        var badge = new GameObject("Key", Il2CppType.Of<RectTransform>());
        badge.transform.SetParent(box, false);
        (badge.GetComponent<LayoutElement>() ?? badge.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-Px(1f), Px(1f));
        rect.sizeDelta = new Vector2(Px(KeyBadge), Px(KeyBadge));
        var image = badge.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = SlotRim;
        if (uiParts != null)
        {
            var label = DelversMenuWindow.Clone(uiParts.BodyText, badge.transform, "Number");
            var text = label.GetComponent<TMP_Text>();
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.fontSize = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = KeyDigit;
            (label.GetComponent<LayoutElement>() ?? label.AddComponent<LayoutElement>()).ignoreLayout = true;
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            DelversMenuWindow.SetText(text, number.ToString());
            label.SetActive(true);
        }
        return badge;
    }

    // ---- 22: the row's frame and the role block ----

    // The frame of the unit panel's portrait round a member's row: only its border, the row's own ground stays.
    private static GameObject AddRowFrame(GameObject entry)
    {
        var holder = new GameObject("RowFrame", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(entry.transform, false);
        // Behind the row's parts: the frame's inner shade ran over the top of the name.
        holder.transform.SetAsFirstSibling();
        (holder.GetComponent<LayoutElement>() ?? holder.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (portraitFrame != null)
        {
            var image = holder.AddComponent<Image>();
            image.raycastTarget = false;
            UseFrame(image, portraitFrame, false);
        }
        else
        {
            ShowFrame(AddFrame(holder.transform, FrameWidth), BarLine);
            var top = AddFrame(holder.transform, 1f);
            ShowFrame(new[] { top[0] }, BarTop);
            for (var i = 1; i < top.Length; i++) top[i].enabled = false;
        }
        holder.SetActive(false);
        return holder;
    }

    // The role block: a brown edge of one pixel, dark inside, the symbol at its top and the role's name at its bottom, each
    // RoleBlockRoom inside the edge. Its sides are the same whole number of pixels from the symbol, so the symbol stands exactly in
    // its middle. It starts RoleBlockTop below the role field, so it keeps room to the row frame's shade above and its edge below
    // (at 1920x1080 two pixels each, as in the sheet).
    private const int RoleBlockTop = 7;
    private const float RoleBlockHeight = 34f, RoleBlockSide = 7f, RoleBlockRoom = 2f, RoleNameLine = 9f;

    private static Image AddRoleBlock(Transform field, out TMP_Text name)
    {
        name = null;
        var icon = Px(RoleIconInRow);
        var block = new GameObject("RoleBlock", Il2CppType.Of<RectTransform>());
        block.transform.SetParent(field, false);
        Fixed(block, icon + 2f * Px(RoleBlockSide), Px(RoleBlockHeight));
        var edge = block.AddComponent<Image>();
        edge.color = BarLine;
        edge.raycastTarget = false;
        Inset(block.transform, "Inside", Px(1f), SlotRim, true);
        var symbol = Picture(block.transform, "Icon", null, icon);
        var symbolRect = symbol.GetComponent<RectTransform>();
        symbolRect.anchorMin = symbolRect.anchorMax = new Vector2(0.5f, 1f);
        symbolRect.pivot = new Vector2(0.5f, 1f);
        symbolRect.anchoredPosition = new Vector2(0f, -Px(RoleBlockRoom));
        if (uiParts != null)
        {
            var label = DelversMenuWindow.Clone(uiParts.BodyText, block.transform, "Name");
            var text = label.GetComponent<TMP_Text>();
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = 9f;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Bottom;
            text.raycastTarget = false;
            text.color = Beige;
            DelversMenuWindow.SetText(text, "");
            (label.GetComponent<LayoutElement>() ?? label.AddComponent<LayoutElement>()).ignoreLayout = true;
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, Px(RoleBlockRoom));
            rect.sizeDelta = new Vector2(-2f * Px(RoleBlockRoom), Px(RoleNameLine));
            label.SetActive(true);
            name = text;
        }
        return symbol.GetComponent<Image>();
    }

    // ---- 23 ----

    // The window's panel wider by extra: parts on its left keep their place to the left edge, parts on its right to the right edge,
    // the title in the middle stays, and the wide parts (the frame's lines) grow with it. The content box and its scroll view grow
    // only to the right: their left edge stays, so the list moved left with the panel's edge no longer reaches into the box. Measured
    // in the panel's own units after a layout pass, moved through the parts' offsets, which works for any anchoring.
    private static void Widen(RectTransform panel, float extra)
    {
        var parts = new List<(RectTransform Rect, float Left, float Right, bool Grow, bool Box)>();
        var width = panel.rect.width;
        for (var i = 0; i < panel.childCount; i++)
        {
            var part = panel.GetChild(i).GetComponent<RectTransform>();
            if (part == null) continue;
            var (left, right) = Edges(panel, part);
            var box = part.name is "Img_OptionContent" or "ScrollView_SettingData";
            parts.Add((part, left, right, box || right - left >= 0.6f * width, box));
        }
        panel.sizeDelta += new Vector2(extra, 0f);
        var moved = new List<string>();
        foreach (var (part, left, right, grow, box) in parts)
        {
            float wantLeft, wantRight;
            if (grow)
            {
                wantLeft = box ? left : left - extra * 0.5f;
                wantRight = right + extra * 0.5f;
            }
            else
            {
                var middle = (left + right) * 0.5f;
                var shift = Math.Abs(middle) < 0.02f * width ? 0f : Math.Sign(middle) * extra * 0.5f;
                wantLeft = left + shift;
                wantRight = right + shift;
            }
            var (nowLeft, nowRight) = Edges(panel, part);
            part.offsetMin += new Vector2(wantLeft - nowLeft, 0f);
            part.offsetMax += new Vector2(wantRight - nowRight, 0f);
            moved.Add(part.name + ":" + (box ? "growright" : grow ? "grow" : wantLeft < left ? "left" : wantLeft > left ? "right" : "stay"));
        }
        if (widenLogged) return;
        widenLogged = true;
        DelversHost.Info("CORE_TACTICAL_WIDEN extra=" + extra.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                         + " width=" + width.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " parts=" + string.Join(",", moved));
    }

    private static (float Left, float Right) Edges(RectTransform panel, RectTransform part)
    {
        var corners = new Il2CppStructArray<Vector3>(4);
        part.GetWorldCorners(corners);
        return (panel.InverseTransformPoint(corners[0]).x, panel.InverseTransformPoint(corners[2]).x);
    }
}
