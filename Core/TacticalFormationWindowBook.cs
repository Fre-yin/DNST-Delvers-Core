using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The formation window's book (up to eight formations to keep; a box per formation): a narrow pager at the
// bottom between the window's buttons, with the arrows of the game's work plan counter. The shown set is the active one; turning the page keeps the shown set in the
// book first. An empty set starts with the default places around the list's top member. Casts stay with the members.
internal static partial class TacticalFormationWindow
{
    private const float ArrowWidth = 40f, ArrowHeight = 30f, PagerSpacing = 4f, PagerMargin = 8f;

    private static TacticalFormationBook book;
    private static int page;
    private static List<Guid> order;

    private static void ClearBook()
    {
        book = null;
        page = 0;
        order = null;
    }

    // The shown set back into the book: its rows as the window shows them (the fixed selection), none when every row went.
    private static void StorePage()
    {
        if (book == null || grid == null) return;
        KeepCasts();
        book.Sets[page] = grid.Leader == Guid.Empty ? null : new TacticalFormationBook.Set(grid.Leader, grid.Slots(), SummonPlacesOf(grid), grid.AllRoles());
        book.Active = page;
    }

    // Turns to another set (0 to 7); a set without rows starts with the group the window opened for.
    internal static bool ShowPage(int index)
    {
        if (book == null || grid == null || index < 0 || index >= TacticalFormationBook.Size || index == page) return false;
        StorePage();
        grid = GridFor(index);
        page = index;
        book.Active = index;
        ClosePicker();
        Refresh();
        DelversHost.Info($"CORE_TACTICAL_PAGE set={index + 1} leader={grid.Leader:D} saved={(book.Sets[index] != null ? 1 : 0)}");
        return true;
    }

    private static void Turn(int step) => ShowPage((page + step + TacticalFormationBook.Size) % TacticalFormationBook.Size);

    // In the gap of the bottom bar between Test formation and Reset (I4 015849: in the bar's middle the right arrow lay over
    // Reset), at the height of the Close button. Read from the buttons' own anchors, never measured.
    private static void BuildPager(DelversMenuWindow.Parts parts, Transform panel)
    {
        var pager = Layout<HorizontalLayoutGroup>(panel, "Pager", PagerSpacing, new RectOffset(0, 0, 0, 0));
        pager.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        var fitter = pager.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = pager.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        var close = panel.Find("Btn_Confirm")?.GetComponent<RectTransform>();
        var height = close != null && close.anchorMin.y == 0f && close.anchorMax.y == 0f
            ? close.anchoredPosition.y + (0.5f - close.pivot.y) * close.sizeDelta.y
            : 40f;
        var test = panel.Find("Btn_Cancel")?.GetComponent<RectTransform>();
        var reset = panel.Find("Btn_ResetDefault")?.GetComponent<RectTransform>();
        var width = panel.GetComponent<RectTransform>().rect.width;
        var middle = 0f;
        var room = -1f;
        if (test != null && reset != null && test.anchorMin.x == test.anchorMax.x && reset.anchorMin.x == reset.anchorMax.x)
        {
            var testRight = (test.anchorMin.x - 0.5f) * width + test.anchoredPosition.x + (1f - test.pivot.x) * test.sizeDelta.x;
            var resetLeft = (reset.anchorMin.x - 0.5f) * width + reset.anchoredPosition.x - reset.pivot.x * reset.sizeDelta.x;
            if (resetLeft > testRight)
            {
                middle = (testRight + resetLeft) / 2f;
                room = resetLeft - testRight;
            }
        }
        rect.anchoredPosition = new Vector2(middle, height);

        // Narrow and in the game's style. The arrows are the single steps of the game's work plan counter
        // (WorkPlanArrowUI minusOne, plusOne) at their own size. Without the counter the tab buttons of the options window stand in.
        var counter = FindCounter();
        TakeFrontArrow(counter != null ? counter.plusOne : null);
        var previous = counter != null ? GameArrow(counter.minusOne, pager.transform, "Btn_PagePrevious", () => Turn(-1)) : null;
        previous ??= Arrow(parts, pager.transform, "Btn_PagePrevious", "<", () => Turn(-1));
        // A box per formation instead of the number, the shown one filled.
        AddPageDots(pager.transform);
        var next = counter != null ? GameArrow(counter.plusOne, pager.transform, "Btn_PageNext", () => Turn(1)) : null;
        next ??= Arrow(parts, pager.transform, "Btn_PageNext", ">", () => Turn(1));
        var size = previous.GetComponent<RectTransform>().sizeDelta;
        // The boxes narrow down to the gap between Test formation and Reset, so the arrows never lie over those buttons.
        if (room > 0f) FitPageDots(room - LaidWidth(previous) - LaidWidth(next) - 2f * PagerSpacing - PagerMargin);
        DelversHost.Info($"CORE_TACTICAL_PAGER arrows={(counter != null ? "workplan" : "tabs")} size={size.x:0}x{size.y:0}");
    }

    // The width the pager's layout gives a part: its LayoutElement's (GameArrow and Arrow set it), else its own.
    private static float LaidWidth(GameObject part)
    {
        var element = part.GetComponent<LayoutElement>();
        return element != null && element.preferredWidth > 0f ? element.preferredWidth : part.GetComponent<RectTransform>().sizeDelta.x;
    }

    // The work plan's counter, found and never created: a prefab or a loaded view.
    private static WorkPlanArrowUI FindCounter()
    {
        try
        {
            WorkPlanArrowUI fallback = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<WorkPlanArrowUI>())
            {
                if (candidate == null || candidate.minusOne == null || candidate.plusOne == null) continue;
                if (!candidate.gameObject.scene.IsValid()) return candidate;
                fallback ??= candidate;
            }
            return fallback;
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_PAGER_COUNTER_FAILED: " + ex.Message);
            return null;
        }
    }

    // A copy of one of the game's arrow buttons at its own size; null when it cannot be copied.
    private static GameObject GameArrow(Button source, Transform parent, string name, Action turn)
    {
        if (source == null) return null;
        try
        {
            var arrow = DelversMenuWindow.Clone(source.transform, parent, name);
            var own = source.GetComponent<RectTransform>().rect.size;
            Fixed(arrow, own.x, own.y);
            arrow.SetActive(true);
            Wire(arrow.transform, null, turn);
            return arrow;
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_PAGER_ARROW_FAILED: " + ex.Message);
            return null;
        }
    }

    private static GameObject Arrow(DelversMenuWindow.Parts parts, Transform parent, string name, string caption, Action turn)
    {
        var arrow = DelversMenuWindow.Clone(parts.CategoryButton, parent, name);
        var image = arrow.GetComponent<Image>();
        if (image != null && parts.TabUnselected != null) image.sprite = parts.TabUnselected;
        var text = arrow.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            DelversMenuWindow.SetText(text, caption);
            text.alignment = TextAlignmentOptions.Center;
            var place = text.rectTransform;
            place.anchorMin = Vector2.zero;
            place.anchorMax = Vector2.one;
            place.offsetMin = place.offsetMax = Vector2.zero;
        }
        Fixed(arrow, ArrowWidth, ArrowHeight);
        Wire(arrow.transform, null, turn);
        return arrow;
    }

    private static void RefreshPager() => ShowPageDots();
}
