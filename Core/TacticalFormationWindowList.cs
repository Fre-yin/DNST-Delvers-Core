using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The formation window's group list: at the window's left edge, a row per member and summon place. The top
// row wears the crown; a member dragged onto another row moves there, so dragging to the top gives the crown. Beside each name five
// priority casts: a click opens the member's learned active skills as the game's icons, the last row set apart empties the place.
// The AI side reads them through TacticalFormation.PriorityCasts. Each member's summon places stand right below it, indented, a
// row each (TacticalFormationListOrder); the free row at the end holds the add button that opens the choice of what to add, a
// right click takes a row out (TacticalFormationWindowSelection). Eight rows are seen; more scroll, with a copy of the options
// window's scroll bar, hidden while everything fits (packs allow bigger groups; the looks,
// TacticalFormationWindowLook).
internal static partial class TacticalFormationWindow
{
    // Name and casts stand on top of each other beside the portrait, so the list fits the window's left column.
    private const float NameWidth = 128f, NameLine = 15f;
    private const float ChoiceWidth = 220f, ChoiceHeight = 30f, ChoiceIcon = 24f;
    // The skills shown at once in the cast choice before it scrolls: with the window's scale it then fits above or below every place.
    private const int MaxChoiceLines = 7;
    private static readonly Color RowColor = new(0f, 0f, 0f, 0.15f);
    private static readonly Color CastColor = new(0.12f, 0.1f, 0.1f, 1f);
    // A cast that sits out (TacticalFormation.CastTakesPart) is faded, without a word in the window, but clearly still there
    // (at 35 % it looked gone). A cooldown is not shown: the places are only an order of priority.
    private static readonly Color Faded = new(0.75f, 0.75f, 0.75f, 0.6f);
    // A place with a skill whose icon the game did not give: the box itself shows that the place is taken.
    private static readonly Color TakenWithoutIcon = new(0.45f, 0.33f, 0.22f, 1f);

    private sealed class Row
    {
        // The line holding the row, which indents it under its summoner (TacticalFormationWindowLook).
        internal HorizontalLayoutGroup Line;
        internal float Indent;
        internal Image TreeDown;
        internal Image TreeBranch;
        internal Image Back;
        internal RawImage Hatch;
        internal Image Face;
        internal Image Crown;
        internal TMP_Text Name;
        internal GameObject Casts;
        internal readonly Image[] Icons = new Image[TacticalFormationGrid.CastSlots];
        internal GameObject Role;
        internal Image RoleIcon;
        internal TMP_Text RoleName;
        // The free row's button, the row's frame while it is a drop target, each place's frame and empty mark (TacticalFormationWindowLook).
        internal GameObject AddButton;
        internal (Image Face, Image[] Frame) AddHover;
        internal Image IndentHit;
        internal Image[] Frame;
        internal readonly Image[][] CastFrames = new Image[TacticalFormationGrid.CastSlots][];
        internal readonly GameObject[] CastMarks = new GameObject[TacticalFormationGrid.CastSlots];
        // The place's face (the game's skill slot or the drawn one) and the row's portrait frame (TacticalFormationWindowFrames).
        internal readonly Image[] CastFaces = new Image[TacticalFormationGrid.CastSlots];
        internal GameObject RowFrame;
    }

    private const float RowSpacing = 4f, BarGap = 2f;
    private static readonly List<Row> rows = new();
    private static Transform listRows;
    private static ScrollRect listScroll;
    // The options window's scroll bar the list copies, also for the add choice's tiles.
    private static Scrollbar listBar;
    // The column's size is logged once per session (Core's lifecycle reset clears it with the grey copies).
    private static bool columnLogged;
    private static readonly Dictionary<string, Sprite> skillIcons = new(StringComparer.Ordinal);
    private static int listFrom = -1;
    private static int listHover = -1;
    private static GameObject picker;
    private static GameObject pickerBlocker;

    private static void ClearList()
    {
        rows.Clear();
        listRows = null;
        listScroll = null;
        listBar = null;
        listFrom = listHover = -1;
        picker = pickerBlocker = null;
    }

    // ---- building ----

    // The list stands in the options window's tab column, the free column at its left edge (further to
    // the left); the tabs go, the column keeps its place in the frame. Measuring the frame does not work while it is built
    // (I4 014616 and 014758: the content box read left of the frame), so the list takes the column's own top left corner.
    private static void BuildList(DelversMenuWindow.Parts parts, Transform column, ScrollRect template)
    {
        for (var i = column.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(column.GetChild(i).gameObject);
        var holder = column.GetComponent<LayoutGroup>();
        if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
        var list = Layout<VerticalLayoutGroup>(column, "Group", 4f, new RectOffset(0, 0, 0, 0));
        var fitter = list.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = list.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        var size = column.GetComponent<RectTransform>().rect.size;
        if (!columnLogged)
        {
            columnLogged = true;
            DelversHost.Info("CORE_TACTICAL_LIST_COLUMN width=" + size.x.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " height="
                             + size.y.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " layout=" + (holder != null ? 1 : 0));
        }
        Text(parts, list.transform, MenuText.Pick(TacticalFormationText.LeaderTitle), RowWidth);
        // The title stands in the middle above the list.
        listTitle = list.transform.GetChild(list.transform.childCount - 1).GetComponent<TMP_Text>();
        listTitle.alignment = TextAlignmentOptions.Center;
        listRows = AddListScroll(list.transform, template);
        for (var i = 0; i < TacticalFormationGrid.VisibleRows; i++) rows.Add(AddRow(parts, listRows, i));
    }

    // The rows' scrolling area: eight rows high; the scroll bar beside it is a copy of the options window's own.
    private static Transform AddListScroll(Transform parent, ScrollRect template)
    {
        var bar = template != null ? template.verticalScrollbar : null;
        listBar = bar;
        var barRect = bar != null ? bar.GetComponent<RectTransform>() : null;
        var barWidth = barRect != null && barRect.anchorMin.x == barRect.anchorMax.x && barRect.sizeDelta.x > 0f ? barRect.sizeDelta.x : 12f;
        // As wide as the rows with their role, so the mask cuts nothing off.
        var viewWidth = RowWidth + 2f;
        var viewHeight = TacticalFormationGrid.VisibleRows * EntryHeight + (TacticalFormationGrid.VisibleRows - 1) * RowSpacing;
        var scroll = new GameObject("Scroll", Il2CppType.Of<RectTransform>());
        scroll.transform.SetParent(parent, false);
        Fixed(scroll, viewWidth + BarGap + barWidth, viewHeight);

        var view = new GameObject("Viewport", Il2CppType.Of<RectTransform>());
        view.transform.SetParent(scroll.transform, false);
        var viewRect = view.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.pivot = new Vector2(0f, 1f);
        viewRect.offsetMin = Vector2.zero;
        viewRect.offsetMax = new Vector2(-(BarGap + barWidth), 0f);
        // Clear, but it takes the wheel between the rows.
        view.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        view.AddComponent<RectMask2D>();

        var content = Layout<VerticalLayoutGroup>(view.transform, "Rows", RowSpacing, new RectOffset(0, 0, 0, 0));
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        listScroll = scroll.AddComponent<ScrollRect>();
        listScroll.content = contentRect;
        listScroll.viewport = viewRect;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = template != null && template.scrollSensitivity > 0f ? template.scrollSensitivity : EntryHeight * 0.5f;
        if (bar != null)
        {
            var copy = DelversMenuWindow.Clone(bar.transform, scroll.transform, "Scrollbar");
            var rect = copy.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(barWidth, 0f);
            rect.anchoredPosition = Vector2.zero;
            listScroll.verticalScrollbar = copy.GetComponent<Scrollbar>();
            listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        DelversHost.Info($"CORE_TACTICAL_LIST_SCROLL bar={(bar != null ? 1 : 0)} width={barWidth:0} view={viewWidth:0}x{viewHeight:0}");
        return content.transform;
    }

    // The rows the list needs: every member and summon place and one free row with the add button (no column
    // of empty "+" rows below).
    private static int RowsNeeded() => Math.Min(grid.Count + 1, TacticalFormationGrid.MaxRows);

    private static Row AddRow(DelversMenuWindow.Parts parts, Transform parent, int index)
    {
        var line = Layout<HorizontalLayoutGroup>(parent, "Line_" + index, 0f, new RectOffset(0, 0, 0, 0));
        Fixed(line, RowWidth, EntryHeight);
        var entry = Layout<HorizontalLayoutGroup>(line.transform, "Member_" + index, 6f, new RectOffset(2, 2, 2, 2));
        entry.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        Fixed(entry, RowWidth, EntryHeight);
        var row = new Row { Line = line.GetComponent<HorizontalLayoutGroup>(), Back = entry.AddComponent<Image>() };
        (row.TreeDown, row.TreeBranch) = AddTree(line.transform);
        row.Hatch = AddHatch(entry);
        var entryRect = entry.GetComponent<RectTransform>();
        row.Face = Picture(entry.transform, "Portrait", null, Portrait).GetComponent<Image>();
        var crown = Picture(row.Face.transform, "Crown", CoreAssets.FormationCrown, Crown);
        var crownRect = crown.GetComponent<RectTransform>();
        crownRect.anchorMin = crownRect.anchorMax = new Vector2(0.5f, 1f);
        crownRect.anchoredPosition = new Vector2(0f, -2f);
        row.Crown = crown.GetComponent<Image>();
        var side = Layout<VerticalLayoutGroup>(entry.transform, "Side", 1f, new RectOffset(0, 0, 0, 0));
        Text(parts, side.transform, "+", NameWidth);
        row.Name = side.transform.GetChild(side.transform.childCount - 1).GetComponent<TMP_Text>();
        // One line high, so name and places end as far inside the row's frame as the portrait and the role (TacticalFormationWindowFrames).
        Fixed(row.Name.gameObject, NameWidth, NameLine);
        // The places apart, each with its own edge (TacticalFormationWindowFrames, the author's choice A of the sheet).
        row.Casts = Layout<HorizontalLayoutGroup>(side.transform, "Casts", Px(SlotGap), new RectOffset(0, 0, 0, 0));
        row.Casts.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        for (var place = 0; place < TacticalFormationGrid.CastSlots; place++)
        {
            var box = new GameObject("Cast_" + place, Il2CppType.Of<RectTransform>());
            box.transform.SetParent(row.Casts.transform, false);
            box.AddComponent<Image>();
            Fixed(box, Px(SlotSize), Px(SlotSize));
            row.CastFaces[place] = AsSlot(box);
            // The same whole number of pixels round the icon on each side, so it stands in the place's middle.
            row.Icons[place] = Picture(box.transform, "Icon", null, SlotIcon).GetComponent<Image>();
            row.CastMarks[place] = AddKeyBadge(box.transform, place + 1);
            row.CastFrames[place] = AddFrame(box.transform, ThinFrame);
            var (at, which) = (index, place);
            DelversMenuWindow.EnsureButton(box).onClick.AddListener((Action)(() => OpenPicker(at, which, box.GetComponent<RectTransform>())));
        }
        var trigger = entry.AddComponent<EventTrigger>();
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.BeginDrag, () => BeginListDrag(index));
        AddPointer(trigger, EventTriggerType.Drag, MoveGhost);
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.EndDrag, EndListDrag);
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerEnter, () => HoverList(index));
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerExit, () => HoverList(-1));
        AddPointer(trigger, EventTriggerType.PointerClick, pointer => ClickRow(index, pointer, entryRect));
        AddRoleField(entry, row, index);
        row.AddButton = AddAddButton(entry, entryRect, row);
        row.IndentHit = AddIndentHit(entry);
        row.RowFrame = AddRowFrame(entry);
        row.Frame = AddFrame(entry.transform);
        // The row's trigger takes every event, the wheel too: it goes on to the list's scrolling.
        AddPointer(trigger, EventTriggerType.Scroll, pointer =>
        {
            if (pointer != null) listScroll?.OnScroll(pointer);
        });
        return row;
    }

    // ---- showing ----

    private static void RefreshList()
    {
        var needed = RowsNeeded();
        while (rows.Count < needed && uiParts != null && listRows != null) rows.Add(AddRow(uiParts, listRows, rows.Count));
        var shownRows = ListOrder();
        // The drop under the pointer while a member is dragged, and whether it changes anything (TacticalFormationListOrder.DropIndex).
        var dragged = listFrom >= 0 && listFrom < shownRows.Count && shownRows[listFrom].Member ? shownRows[listFrom].MemberIndex : -1;
        var hint = TacticalFormationListOrder.DropHint(shownRows, listHover, grid?.Members.Count ?? 0, dragged);
        var dropChanges = hint.Changes;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            row.Line.gameObject.SetActive(i < needed);
            if (i >= needed) continue;
            var shown = i < shownRows.Count ? shownRows[i] : default;
            var unit = i < shownRows.Count ? shown.Unit : Guid.Empty;
            var filled = unit != Guid.Empty;
            var member = filled && shown.Member;
            var away = filled && IsAway(unit);
            Indented(row, shown.UnderSummoner);
            ShowTree(row, shown.UnderSummoner, shown.LastUnder);
            // The free row is only its add button; a drop target is framed in gold, the dragged member stays faint in its row.
            row.Back.color = filled || row.AddButton == null ? RowColor : Color.clear;
            if (row.AddButton != null && row.AddButton.activeSelf == filled)
            {
                ResetHover(row.AddHover);
                row.AddButton.SetActive(!filled);
            }
            ShowFrame(row.Frame, dropChanges && i == listHover ? FrameLeader : null);
            ShowHatch(row.Hatch, away, RowWidth - row.Indent);
            ShowFace(row.Face, filled ? FaceOf(unit) : null, away);
            if (i == listFrom) row.Face.color = Pale;
            row.Crown.enabled = member && i == 0 && i != listFrom && row.Crown.sprite != null;
            row.Role?.SetActive(member && i != listFrom);
            row.RowFrame?.SetActive(member);
            if (row.RoleIcon != null && member)
            {
                var role = grid.RoleOf(unit);
                row.RoleIcon.sprite = CoreAssets.RoleIcon(role);
                row.RoleIcon.enabled = row.RoleIcon.sprite != null;
                row.RoleIcon.color = away ? AwayTint : Color.white;
                if (row.RoleName != null)
                {
                    DelversMenuWindow.SetText(row.RoleName, MenuText.Pick(TacticalFormationText.RoleName(role)));
                    row.RoleName.color = away ? AwayText : Beige;
                }
            }
            // A summon place under its summoner needs no "summoned by": the line shows whose it is.
            var note = filled && !shown.UnderSummoner ? RowNote(unit) : "";
            // The note below the name where the row shows no casts (a summon, a member away); beside it where the casts take the
            // second line (a member down; "Gefallen" ran into the casts).
            var below = !member || awayAt.ContainsKey(unit);
            // Without the add button (no Close button to copy) the free row shows its "+" on the row's ground as before.
            DelversMenuWindow.SetText(row.Name, !filled ? (row.AddButton == null ? "+" : "") : note.Length == 0 ? RowName(unit)
                : RowName(unit) + (below ? "\n" : "  ") + "<size=70%><i>" + note + "</i></size>");
            row.Name.richText = true;
            row.Name.color = away ? AwayText : Beige;
            // A member away keeps its casts, shown faded; a summon place has none.
            row.Casts.SetActive(member && !awayAt.ContainsKey(unit));
            if (!member || awayAt.ContainsKey(unit)) continue;
            var casts = grid.Casts(unit);
            units.TryGetValue(unit, out var entity);
            for (var place = 0; place < row.Icons.Length; place++)
            {
                var icon = row.Icons[place];
                icon.sprite = SkillIcon(casts[place]);
                icon.enabled = icon.sprite != null;
                icon.color = icon.enabled && !TakesPart(entity, casts[place]) ? Faded : Color.white;
                // A place with a skill whose icon the game did not give is tinted; else the face shows as the game's slot (21 A).
                var face = row.CastFaces[place];
                if (face != null) face.color = casts[place].Length > 0 && !icon.enabled ? TakenWithoutIcon : SlotDark;
            }
        }
        // Gold only while the drop would really pass the crown: onto the top row, by another member (TacticalFormationListOrder.DropIndex).
        var crown = hint.Crown;
        if (listTitle != null) listTitle.color = crown ? FrameLeader : Beige;
    }

    // ---- dragging in the list ----

    // The list's rows in the order shown: each member with its summon places below it.
    private static List<TacticalFormationListOrder.Row> ListOrder()
        => grid == null ? new List<TacticalFormationListOrder.Row>()
            : TacticalFormationListOrder.Rows(grid.Members, grid.Summons, id => summonPlaces.TryGetValue(id, out var place) ? place.Summoner : Guid.Empty);

    private static void BeginListDrag(int index)
    {
        var shownRows = ListOrder();
        if (index >= shownRows.Count || !shownRows[index].Member) return;
        ClosePicker();
        CloseAdder();
        listFrom = index;
        ghost.sprite = FaceOf(shownRows[index].Unit);
        ghost.gameObject.SetActive(true);
        // As large as the portraits in the window's cells and rows (the window's scale, ScaleLikeWindow).
        ScaleLikeWindow(ghost.rectTransform);
        ghost.transform.SetAsLastSibling();
        Refresh();
    }

    private static void EndListDrag()
    {
        if (listFrom < 0) return;
        var shownRows = ListOrder();
        if (listHover >= 0 && listFrom < shownRows.Count && shownRows[listFrom].Member)
            grid.MoveInList(shownRows[listFrom].Unit, TacticalFormationListOrder.DropIndex(shownRows, listHover, grid.Members.Count, shownRows[listFrom].MemberIndex));
        listFrom = -1;
        ghost.gameObject.SetActive(false);
        Refresh();
    }

    private static void HoverList(int index)
    {
        listHover = index;
        if (listFrom >= 0) Refresh();
    }

    // ---- choosing a cast ----

    // The choice opens as a dropdown right below the clicked place (it may cover the row below while a member
    // is edited), built like the Delvers menu's dropdown from the options window's list rows and their closing line. Each row
    // shows the skill's icon and its name from the game's text table. (the author's choice of the drafts) the place
    // being chosen for is framed in gold, and the row with a drawn cross that clears the place stands set apart at the end.
    private static void OpenPicker(int index, int place, RectTransform box)
    {
        ClosePicker();
        if (box == null || RowUnit(index) is not { } unit || !grid.Members.Contains(unit)) return;
        units.TryGetValue(unit, out var entity);
        var skills = TacticalFormation.ActiveSkills(entity);

        pickerBlocker = new GameObject("CastPickerBlocker", Il2CppType.Of<RectTransform>());
        pickerBlocker.transform.SetParent(root.transform, false);
        var blockerRect = pickerBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        pickerBlocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        DelversMenuWindow.EnsureButton(pickerBlocker).onClick.AddListener((Action)ClosePicker);

        picker = new GameObject("CastPicker", Il2CppType.Of<RectTransform>());
        picker.transform.SetParent(root.transform, false);
        var layout = picker.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = picker.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = picker.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.position = box.position;
        rect.anchoredPosition += new Vector2(-Px(SlotSize) * 0.5f, -Px(SlotSize) * 0.5f) * ScaleLikeWindow(rect);

        // More skills than MaxChoiceLines scroll in their own area with a copy of the options window's scroll bar, half a line more
        // in sight; so the choice always fits above or below its place and never covers it (KeepInside). "Clear slot" stays below.
        var choices = skills.Count > MaxChoiceLines ? ChoiceScroll() : picker.transform;
        foreach (var skill in skills) AddChoice(unit, place, skill, choices);
        var apart = new GameObject("Apart", Il2CppType.Of<RectTransform>());
        apart.transform.SetParent(picker.transform, false);
        Fixed(apart, ChoiceWidth, 1f);
        // The line between the skills and "Clear slot" as wide as the lines' face (TacticalFormationWindowLook.ChoiceSide).
        AddChoiceGround(apart);
        apart.transform.GetChild(0).GetComponent<Image>().color = new Color(Beige.r, Beige.g, Beige.b, 0.3f);
        AddChoice(unit, place, "", picker.transform);
        choosing = (index, place);
        ShowChoosing(true);
        AddChoiceEnd(picker.transform, ChoiceWidth);
        picker.transform.SetAsLastSibling();
        KeepInside(picker.GetComponent<RectTransform>(), box, true);
        DelversHost.Info($"CORE_TACTICAL_CASTS_OFFERED unit={unit:D} place={place} skills={skills.Count}");
    }

    // The skills' scrolling area in the cast choice: MaxChoiceLines and a half lines high, the rows in a column that the scroll rect
    // moves, the bar at the right. Returns the column for the rows.
    private static Transform ChoiceScroll()
    {
        var barWidth = 0f;
        if (listBar != null)
        {
            var barRect = listBar.GetComponent<RectTransform>();
            barWidth = barRect != null && barRect.sizeDelta.x > 0f ? barRect.sizeDelta.x : 12f;
        }
        var view = new GameObject("ChoicesScroll", Il2CppType.Of<RectTransform>());
        view.transform.SetParent(picker.transform, false);
        view.AddComponent<Image>().color = CastColor;
        view.AddComponent<RectMask2D>();
        Fixed(view, ChoiceWidth + (barWidth > 0f ? barWidth + 2f : 0f), (MaxChoiceLines + 0.5f) * ChoiceHeight);
        var scroll = view.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ChoiceHeight * 0.5f;
        scroll.viewport = view.GetComponent<RectTransform>();
        var column = Layout<VerticalLayoutGroup>(view.transform, "Choices", 0f, new RectOffset(0, 0, 0, 0));
        var fitter = column.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = columnRect.anchorMax = new Vector2(0f, 1f);
        columnRect.pivot = new Vector2(0f, 1f);
        columnRect.anchoredPosition = Vector2.zero;
        columnRect.sizeDelta = new Vector2(ChoiceWidth, 0f);
        scroll.content = columnRect;
        if (listBar != null)
        {
            var copy = DelversMenuWindow.Clone(listBar.transform, view.transform, "Scrollbar");
            var barRect = copy.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(1f, 1f);
            barRect.sizeDelta = new Vector2(barWidth, 0f);
            barRect.anchoredPosition = Vector2.zero;
            scroll.verticalScrollbar = copy.GetComponent<Scrollbar>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            copy.SetActive(true);
        }
        return column.transform;
    }

    // One row of the dropdown: the options window's list row with the skill's icon at its left and its name beside it.
    private static void AddChoice(Guid unit, int place, string skill, Transform parent)
    {
        var name = skill.Length == 0 ? "Choice_Empty" : "Choice_" + skill;
        GameObject choice;
        if (uiParts?.DropdownRow != null) choice = DelversMenuWindow.Clone(uiParts.DropdownRow, parent, name);
        else
        {
            choice = new GameObject(name, Il2CppType.Of<RectTransform>());
            choice.transform.SetParent(parent, false);
            choice.AddComponent<Image>().color = CastColor;
        }
        Fixed(choice, ChoiceWidth, ChoiceHeight);
        var icon = Picture(choice.transform, "Icon", skill.Length == 0 ? CoreAssets.FormationCross : SkillIcon(skill), ChoiceIcon);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(6f, 0f);
        var text = choice.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.enableWordWrapping = false;
            text.margin = new Vector4(ChoiceIcon + 12f, 0f, 4f, 0f);
            DelversMenuWindow.SetText(text, skill.Length == 0 ? MenuText.Pick(TacticalFormationText.ClearCast) : SkillName(skill));
            if (skill.Length == 0) text.color = AwayText;
        }
        DelversMenuWindow.EnsureButton(choice).onClick.AddListener((Action)(() =>
        {
            SetCast(unit, place, skill);
            ClosePicker();
        }));
        choice.SetActive(true);
    }

    // The skill's name as the game writes it in the current language, the key when the table has none.
    private static string SkillName(string skill)
    {
        try
        {
            var table = DataSheetManager.Instance?._text?._textTable;
            if (table != null && table.TryGetValue("TEXTKEY_" + skill + "_NAME", out var name) && !string.IsNullOrWhiteSpace(name)) return name;
        }
        catch (Exception) { /* The key below still names the skill. */ }
        return skill;
    }

    private static void ClosePicker()
    {
        ShowChoosing(false);
        choosing = (-1, -1);
        foreach (var open in new[] { picker, pickerBlocker })
        {
            if (open == null) continue;
            open.SetActive(false);
            UnityEngine.Object.Destroy(open);
        }
        picker = pickerBlocker = null;
    }

    private static bool TakesPart(UnitEntity unit, string skill)
    {
        try { return TacticalFormation.CastTakesPart(unit, skill); }
        catch (Exception) { return true; }
    }

    // The game's icon of a skill, kept for the session once it was given (a place looked empty after its
    // skill was cast; a failed or lost icon was kept as nothing until the window closed). A failure is asked again next time
    // and logged once per skill.
    private static Sprite SkillIcon(string skill)
    {
        if (string.IsNullOrEmpty(skill)) return null;
        if (skillIcons.TryGetValue(skill, out var known) && known != null) return known;
        Sprite icon = null;
        try { icon = ResourceLoader.Instance?.LoadSkillIcon(skill); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_SKILL_ICON_FAILED: " + ex.Message); }
        if (icon != null) skillIcons[skill] = icon;
        else TacticalFormationButton.ReportOnce("CORE_TACTICAL_CAST_NO_ICON skill=" + skill);
        return icon;
    }

    private static void ForgetIcons() => skillIcons.Clear();
}
