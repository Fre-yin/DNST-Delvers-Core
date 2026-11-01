using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The formation window (Core's plan): the frame, title, text and buttons of the game's options
// window, like the Delvers menu (DelversMenuWindow.Parts), with short words only (the explanation is the
// guide's topic "Formation"). Left the group's list with the crown on top and each member's priority casts
// (TacticalFormationWindowList). Right
// the 9 by 9 grid (TacticalFormationGrid): a member is dragged onto another cell, an occupied cell swaps the two, the cell under
// the pointer is marked while dragging. Below the options window's buttons: Reset, Test formation and Close. The places are
// saved when the window closes (TacticalFormation.Save); Test formation saves, closes and lines the group up
// (TacticalFormationTest). Built on opening, destroyed on closing; nothing runs while it is closed, and nothing per frame.
internal static partial class TacticalFormationWindow
{
    // Below the Delvers menu (29000) and the Modding Toolkit (30000).
    private const int SortingOrder = 28900;
    private const float Cell = 44f, CellGap = 2f, EntryHeight = 50f, Portrait = 40f, Crown = 16f;
    private static readonly Color CellDark = new(0.16f, 0.12f, 0.11f, 0.9f);
    private static readonly Color CellLight = new(0.21f, 0.16f, 0.14f, 0.9f);
    private static readonly Color CellTarget = new(0.55f, 0.45f, 0.25f, 1f);
    private static readonly Color Beige = new(0.8f, 0.75f, 0.72f, 1f);
    private static readonly Color FrontColor = new(0.71f, 0.51f, 0.35f, 1f);

    private static GameObject root;
    private static RectTransform canvasRect;
    private static TacticalFormationGrid grid;
    private static readonly Dictionary<Guid, UnitEntity> units = new();
    private static readonly Dictionary<Guid, Sprite> portraits = new();
    private static readonly List<(Image Back, Image Unit, Image Crown)> cells = new();
    private static int dragFrom = -1;
    private static int hovered = -1;
    private static Image ghost;
    // The options window's parts while the window is open, for the cast dropdown.
    private static DelversMenuWindow.Parts uiParts;

    internal static bool IsOpen => root != null;

    internal static void ResetLifecycle()
    {
        Close(false);
        ForgetIcons();
    }

    internal static void Open(IReadOnlyList<UnitEntity> group)
    {
        Close(true);
        if (group == null || group.Count == 0) return;
        var guids = TacticalFormation.Guids(group);
        book = TacticalFormation.Book();
        page = book.Active;
        order = guids;
        grid = GridFor(page, guids, TacticalFormation.AllPriorityCasts());
        for (var i = 0; i < group.Count; i++) units[guids[i]] = group[i];
        if (grid.Left > 0) DelversHost.Info($"CORE_TACTICAL_GROUP_CAPPED members={guids.Count} shown={grid.Members.Count}");
        try
        {
            if (!Build()) Close(false);
            else DelversHost.Info($"CORE_TACTICAL_WINDOW_OPENED members={grid.Members.Count} set={page + 1} saved={(book.Sets[page] != null ? 1 : 0)} dungeon={TacticalFormation.InDungeon}");
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_WINDOW_FAILED: " + ex.Message);
            Close(false);
        }
    }

    // save: the book with the shown set active goes into Core's block first (the window's own buttons and Escape).
    internal static void Close(bool save)
    {
        if (root == null) return;
        if (save)
        {
            try
            {
                StorePage();
                TacticalFormation.Save(book, grid);
            }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_SAVE_FAILED: " + ex.Message); }
        }
        ClosePicker();
        UnityEngine.Object.Destroy(root);
        root = null;
        canvasRect = null;
        grid = null;
        ghost = null;
        uiParts = null;
        units.Clear();
        portraits.Clear();
        cells.Clear();
        ClearList();
        ClearBook();
        dragFrom = hovered = -1;
    }

    // The game's Escape while the window is open closes it, with saving (TacticalFormationEscapePatch).
    internal static bool TakeEscape()
    {
        if (root == null) return false;
        Close(true);
        return true;
    }

    // ---- the same steps without the mouse, for the Toolkit's block (DelversCoreRuntime.Formation*ForTest) ----

    internal static bool SetCrown(Guid unit)
    {
        if (grid == null || !grid.SetCrown(unit)) return false;
        Refresh();
        return true;
    }

    // A member dropped on a cell, as the drag ends: an occupied cell swaps the two.
    internal static bool Move(Guid unit, int column, int row)
    {
        if (grid?.CellOf(unit) is not { } from || !grid.Drop(from.Column, from.Row, column, row)) return false;
        Refresh();
        return true;
    }

    internal static (int Column, int Row)? CellOf(Guid unit) => grid?.CellOf(unit);

    // A member dropped on a row of the list; the top row wears the crown.
    internal static bool MoveInList(Guid unit, int index)
    {
        if (grid == null || !grid.MoveInList(unit, index)) return false;
        Refresh();
        return true;
    }

    // A skill on one of the member's priority places, "" empties it.
    internal static bool SetCast(Guid unit, int place, string skill)
    {
        if (grid == null || !grid.SetCast(unit, place, skill)) return false;
        DelversHost.Info($"CORE_TACTICAL_CAST unit={unit:D} place={place} skill={(string.IsNullOrEmpty(skill) ? "-" : skill)}");
        Refresh();
        return true;
    }

    internal static bool ResetGrid()
    {
        if (grid == null) return false;
        grid.Reset();
        Refresh();
        return true;
    }

    internal static bool RunTest()
    {
        if (grid == null) return false;
        Test();
        return true;
    }

    // "open=1 set=3 leader=<guid> list=<guid>|… cells=<column>,<row>:<guid>|… casts=<guid>:<skill>,<skill>|…
    // saved=<set>:<leader>:<places>|… active=3 test=idle" for the Toolkit's checks; sets count from 1, as the pager shows them.
    internal static string Status()
    {
        var text = new System.Text.StringBuilder("open=" + (grid != null ? 1 : 0));
        if (grid != null) text.Append(" set=").Append(page + 1);
        if (grid != null)
        {
            text.Append(" leader=").Append(grid.Leader.ToString("D")).Append(" list=");
            for (var i = 0; i < grid.Members.Count; i++) text.Append(i > 0 ? "|" : "").Append(grid.Members[i].ToString("D"));
            text.Append(" cells=");
            var first = true;
            for (var row = 0; row < TacticalFormationGrid.Size; row++)
                for (var column = 0; column < TacticalFormationGrid.Size; column++)
                {
                    var unit = grid.At(column, row);
                    if (unit == Guid.Empty) continue;
                    if (!first) text.Append('|');
                    text.Append(column).Append(',').Append(row).Append(':').Append(unit.ToString("D"));
                    first = false;
                }
            text.Append(" casts=");
            var casts = grid.AllCasts();
            first = true;
            foreach (var pair in casts)
            {
                text.Append(first ? "" : "|").Append(pair.Key.ToString("D")).Append(':').Append(string.Join(",", pair.Value));
                first = false;
            }
            if (first) text.Append('-');
        }
        var saved = TacticalFormation.Book();
        text.Append(" saved=");
        var any = false;
        for (var i = 0; i < TacticalFormationBook.Size; i++)
        {
            var set = saved.Sets[i];
            if (set == null) continue;
            text.Append(any ? "|" : "").Append(i + 1).Append(':').Append(set.Leader.ToString("D")).Append(':').Append(set.Places.Count);
            any = true;
        }
        if (!any) text.Append('-');
        text.Append(" active=").Append(saved.Active + 1);
        text.Append(" test=").Append(TacticalFormationTest.Running ? "running" : "idle");
        return text.ToString();
    }

    private static void Test()
    {
        var leader = grid.Leader;
        var slots = grid.Slots();
        Close(true);
        if (!TacticalFormationTest.Start(leader, slots)) DelversHost.Info("CORE_TACTICAL_TEST_SKIPPED");
    }

    // ---- building ----

    private static bool Build()
    {
        if (!DelversMenuWindow.Parts.TryHarvest(out var parts, out var reason))
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_WINDOW_UNAVAILABLE: " + reason);
            return false;
        }
        uiParts = parts;
        root = new GameObject("DelversFormationCanvas", Il2CppType.Of<RectTransform>());
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        if (parts.Scaler != null)
        {
            scaler.uiScaleMode = parts.Scaler.uiScaleMode;
            scaler.referenceResolution = parts.Scaler.referenceResolution;
            scaler.screenMatchMode = parts.Scaler.screenMatchMode;
            scaler.matchWidthOrHeight = parts.Scaler.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = parts.Scaler.referencePixelsPerUnit;
        }
        root.AddComponent<GraphicRaycaster>();
        canvasRect = root.GetComponent<RectTransform>();

        var window = DelversMenuWindow.Clone(parts.Window, root.transform, "DelversFormation");
        var backdrop = window.GetComponent<RectTransform>();
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        var panel = Require(window.transform, "Panel_GameOptionUI");
        DelversMenuWindow.SetText(Require(panel, "Txt_Title").GetComponent<TMP_Text>(), MenuText.Pick(TacticalFormationText.Name));
        panel.Find("Panel_KeySettingAwaitPanel")?.gameObject.SetActive(false);
        panel.Find("Btn_Info")?.gameObject.SetActive(false);
        Wire(panel.Find("Btn_Exit"), null, () => Close(true));
        Wire(panel.Find("Btn_Confirm"), MenuText.Close, () => Close(true));
        Wire(panel.Find("Btn_ResetDefault"), MenuText.Pick(TacticalFormationText.Reset), () =>
        {
            grid.Reset();
            Refresh();
        });
        Wire(panel.Find("Btn_Cancel"), MenuText.Pick(TacticalFormationText.Test), Test);

        var content = Require(panel, "ScrollView_SettingData/Viewport/Content");
        for (var i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var page = Layout<VerticalLayoutGroup>(content, "Page", 10f, new RectOffset(16, 16, 16, 16));
        page.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        var columns = Layout<HorizontalLayoutGroup>(page.transform, "Columns", 20f, new RectOffset(0, 0, 0, 0));

        BuildList(parts, Require(panel, "Panel_OptionCategoryButton"));

        var right = Layout<VerticalLayoutGroup>(columns.transform, "Formation", 6f, new RectOffset(0, 0, 0, 0));
        var gridWidth = TacticalFormationGrid.Size * Cell + (TacticalFormationGrid.Size - 1) * CellGap;
        BuildPager(parts, panel);
        AddFront(parts, right.transform, gridWidth);
        AddGrid(right.transform, gridWidth);

        var shadow = new GameObject("DragGhost", Il2CppType.Of<RectTransform>());
        shadow.transform.SetParent(root.transform, false);
        ghost = shadow.AddComponent<Image>();
        ghost.raycastTarget = false;
        ghost.preserveAspect = true;
        shadow.GetComponent<RectTransform>().sizeDelta = new Vector2(Portrait, Portrait);
        shadow.SetActive(false);
        Refresh();
        return true;
    }

    // The front above the grid (the direction must be clear): a bar in the crown's colour with the word.
    private static void AddFront(DelversMenuWindow.Parts parts, Transform parent, float width)
    {
        var bar = Layout<VerticalLayoutGroup>(parent, "Front", 2f, new RectOffset(0, 0, 0, 0));
        bar.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Text(parts, bar.transform, MenuText.Pick(TacticalFormationText.Front), width);
        var text = bar.GetComponentInChildren<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = FrontColor;
        var line = new GameObject("Line", Il2CppType.Of<RectTransform>());
        line.transform.SetParent(bar.transform, false);
        line.AddComponent<Image>().color = FrontColor;
        Fixed(line, width, 3f);
    }

    private static void AddGrid(Transform parent, float width)
    {
        var holder = new GameObject("Grid", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(parent, false);
        Fixed(holder, width, width);
        var layout = holder.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(Cell, Cell);
        layout.spacing = new Vector2(CellGap, CellGap);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = TacticalFormationGrid.Size;
        for (var row = 0; row < TacticalFormationGrid.Size; row++)
            for (var column = 0; column < TacticalFormationGrid.Size; column++)
            {
                var index = cells.Count;
                var cell = new GameObject("Cell_" + column + "_" + row, Il2CppType.Of<RectTransform>());
                cell.transform.SetParent(holder.transform, false);
                var back = cell.AddComponent<Image>();
                var unit = Picture(cell.transform, "Unit", null, Portrait).GetComponent<Image>();
                var crown = Picture(cell.transform, "Crown", CoreAssets.FormationCrown, Crown);
                var crownRect = crown.GetComponent<RectTransform>();
                crownRect.anchorMin = crownRect.anchorMax = new Vector2(0.5f, 1f);
                crownRect.anchoredPosition = new Vector2(0f, -2f);
                cells.Add((back, unit, crown.GetComponent<Image>()));
                var trigger = cell.AddComponent<EventTrigger>();
                TacticalFormationButton.AddTrigger(trigger, EventTriggerType.BeginDrag, () => BeginDrag(index));
                AddPointer(trigger, EventTriggerType.Drag, MoveGhost);
                TacticalFormationButton.AddTrigger(trigger, EventTriggerType.EndDrag, EndDrag);
                TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerEnter, () => Hover(index));
                TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerExit, () => Hover(-1));
            }
    }

    // ---- dragging ----

    private static void BeginDrag(int index)
    {
        var (column, row) = CellAt(index);
        var unit = grid.At(column, row);
        // The crown stays in the middle; it changes through the list.
        if (unit == Guid.Empty || TacticalFormationGrid.IsCenter(column, row)) return;
        dragFrom = index;
        ghost.sprite = PortraitOf(unit);
        ghost.gameObject.SetActive(true);
        ghost.transform.SetAsLastSibling();
        Refresh();
    }

    private static void MoveGhost(PointerEventData pointer)
    {
        if (dragFrom < 0 || pointer == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer.position, null, out var local))
            ghost.rectTransform.anchoredPosition = local;
    }

    private static void EndDrag()
    {
        if (dragFrom < 0) return;
        var (fromColumn, fromRow) = CellAt(dragFrom);
        if (hovered >= 0)
        {
            var (toColumn, toRow) = CellAt(hovered);
            grid.Drop(fromColumn, fromRow, toColumn, toRow);
        }
        dragFrom = -1;
        ghost.gameObject.SetActive(false);
        Refresh();
    }

    private static void Hover(int index)
    {
        hovered = index;
        if (dragFrom >= 0) Refresh();
    }

    private static (int Column, int Row) CellAt(int index) => (index % TacticalFormationGrid.Size, index / TacticalFormationGrid.Size);

    // ---- showing ----

    private static void Refresh()
    {
        for (var i = 0; i < cells.Count; i++)
        {
            var (column, row) = CellAt(i);
            var (back, picture, crown) = cells[i];
            back.color = dragFrom >= 0 && i == hovered ? CellTarget : (column + row) % 2 == 0 ? CellLight : CellDark;
            var unit = grid.At(column, row);
            var shown = unit != Guid.Empty && i != dragFrom;
            picture.sprite = shown ? PortraitOf(unit) : null;
            picture.enabled = shown && picture.sprite != null;
            crown.enabled = shown && unit == grid.Leader && crown.sprite != null;
        }
        RefreshList();
        RefreshPager();
    }

    private static Sprite PortraitOf(Guid unit)
    {
        if (portraits.TryGetValue(unit, out var known)) return known;
        Sprite sprite = null;
        try
        {
            if (units.TryGetValue(unit, out var entity) && entity != null)
                sprite = UnitVisualDataManager.Instance?.GetPortraitSprite(entity.PortraitID, false);
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PORTRAIT_FAILED: " + ex.Message); }
        portraits[unit] = sprite;
        return sprite;
    }

    private static string NameOf(Guid unit)
    {
        if (!units.TryGetValue(unit, out var entity) || entity == null) return "?";
        var key = entity.NameTextKey;
        try
        {
            var table = DataSheetManager.Instance?._text?._textTable;
            if (table != null && key != null && table.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name)) return name;
        }
        catch (Exception) { /* The key below is still a name. */ }
        return string.IsNullOrWhiteSpace(key) ? "?" : key;
    }

    // ---- helpers ----

    private static void AddPointer(EventTrigger trigger, EventTriggerType type, Action<PointerEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((Action<BaseEventData>)(data =>
        {
            try { action(data?.TryCast<PointerEventData>()); }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_UI_FAILED event=" + type + ": " + ex.Message); }
        }));
        trigger.triggers.Add(entry);
    }

    private static void Wire(Transform transform, string caption, Action click)
    {
        if (transform == null) return;
        transform.gameObject.SetActive(true);
        if (caption != null)
        {
            var text = transform.GetComponentInChildren<TMP_Text>(true);
            if (text != null) DelversMenuWindow.SetText(text, caption);
        }
        DelversMenuWindow.EnsureButton(transform.gameObject).onClick.AddListener((Action)(() =>
        {
            try { click(); }
            catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_UI_FAILED button=" + transform.name + ": " + ex.Message); }
        }));
    }

    private static GameObject Layout<T>(Transform parent, string name, float spacing, RectOffset padding) where T : HorizontalOrVerticalLayoutGroup
    {
        var created = new GameObject(name, Il2CppType.Of<RectTransform>());
        created.transform.SetParent(parent, false);
        var layout = created.AddComponent<T>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return created;
    }

    private static GameObject Picture(Transform parent, string name, Sprite sprite, float size)
    {
        var created = new GameObject(name, Il2CppType.Of<RectTransform>());
        created.transform.SetParent(parent, false);
        var image = created.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        created.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
        Fixed(created, size, size).ignoreLayout = parent.GetComponent<LayoutGroup>() == null;
        return created;
    }

    private static void Text(DelversMenuWindow.Parts parts, Transform parent, string value, float width)
    {
        var clone = DelversMenuWindow.Clone(parts.BodyText, parent, "Text");
        var text = clone.GetComponent<TMP_Text>();
        text.enableWordWrapping = true;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Beige;
        text.raycastTarget = false;
        DelversMenuWindow.SetText(text, value);
        // The layout gave the copied label one line only (I4 012339, longer texts ran over the grid), so the height
        // for the width is measured here; a word that wraps in another language keeps its lines.
        var height = -1f;
        try { height = Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TEXT_HEIGHT_FAILED: " + ex.Message); }
        var element = Fixed(clone, width, height);
        element.flexibleHeight = 0f;
        clone.SetActive(true);
    }

    private static LayoutElement Fixed(GameObject target, float width, float height)
    {
        var element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        if (width > 0f) { element.preferredWidth = width; element.minWidth = width; element.flexibleWidth = 0f; }
        if (height > 0f) { element.preferredHeight = height; element.minHeight = height; element.flexibleHeight = 0f; }
        return element;
    }

    private static Transform Require(Transform parent, string path)
        => parent.Find(path) ?? throw new InvalidOperationException("missing part of the options window: " + path);
}

// Escape closes the formation window first, like the Delvers menu (DelversMenuEscapePatch).
[HarmonyPatch(typeof(InputEventHandler), nameof(InputEventHandler.OnEvent), new[] { typeof(EscapeActionRequested) })]
internal static class TacticalFormationEscapePatch
{
    private static bool Prefix()
    {
        try { return !TacticalFormationWindow.TakeEscape(); }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_ESCAPE_FAILED: " + ex.Message);
            return true;
        }
    }
}
