using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The formation window's group list: at the window's left edge, MaxMembers rows, an empty row shows a "+".
// The top row wears the crown; a member dragged onto another row moves there, so dragging to the top gives the crown. Beside
// each name five priority casts: a click opens the member's learned active skills as the game's icons, the first box empties
// the place. The AI side reads them through TacticalFormation.PriorityCasts.
internal static partial class TacticalFormationWindow
{
    // Name and casts stand on top of each other beside the portrait, so the list fits the window's left column.
    private const float ListWidth = 180f, NameWidth = 128f, CastBox = 22f, CastIcon = 18f;
    private const float ChoiceWidth = 220f, ChoiceHeight = 30f, ChoiceIcon = 24f;
    private static readonly Color RowColor = new(0f, 0f, 0f, 0.15f);
    private static readonly Color CastColor = new(0.12f, 0.1f, 0.1f, 1f);
    private static readonly Color PlusColor = new(0.8f, 0.75f, 0.72f, 0.35f);
    // A cast that sits out (TacticalFormation.CastTakesPart) is faded, without a word in the window, but clearly still there
    // (at 35 % it looked gone). A cooldown is not shown: the places are only an order of priority.
    private static readonly Color Faded = new(0.75f, 0.75f, 0.75f, 0.6f);
    // A place with a skill whose icon the game did not give: the box itself shows that the place is taken.
    private static readonly Color TakenWithoutIcon = new(0.45f, 0.33f, 0.22f, 1f);

    private sealed class Row
    {
        internal Image Back;
        internal Image Face;
        internal Image Crown;
        internal TMP_Text Name;
        internal GameObject Casts;
        internal readonly Image[] Icons = new Image[TacticalFormationGrid.CastSlots];
    }

    private static readonly List<Row> rows = new();
    private static readonly Dictionary<string, Sprite> skillIcons = new(StringComparer.Ordinal);
    private static int listFrom = -1;
    private static int listHover = -1;
    private static GameObject picker;
    private static GameObject pickerBlocker;

    private static void ClearList()
    {
        rows.Clear();
        listFrom = listHover = -1;
        picker = pickerBlocker = null;
    }

    // ---- building ----

    // The list stands in the options window's tab column, the free column at its left edge (further to
    // the left); the tabs go, the column keeps its place in the frame. Measuring the frame does not work while it is built
    // (I4 014616 and 014758: the content box read left of the frame), so the list takes the column's own top left corner.
    private static void BuildList(DelversMenuWindow.Parts parts, Transform column)
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
        DelversHost.Info($"CORE_TACTICAL_LIST_COLUMN width={size.x:0} height={size.y:0} layout={(holder != null ? 1 : 0)}");
        Text(parts, list.transform, MenuText.Pick(TacticalFormationText.LeaderTitle), ListWidth);
        // The title stands in the middle above the list.
        list.transform.GetChild(list.transform.childCount - 1).GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        for (var i = 0; i < TacticalFormationGrid.MaxMembers; i++) rows.Add(AddRow(parts, list.transform, i));
    }

    private static Row AddRow(DelversMenuWindow.Parts parts, Transform parent, int index)
    {
        var entry = Layout<HorizontalLayoutGroup>(parent, "Member_" + index, 6f, new RectOffset(2, 2, 2, 2));
        entry.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        Fixed(entry, ListWidth, EntryHeight);
        var row = new Row { Back = entry.AddComponent<Image>() };
        row.Face = Picture(entry.transform, "Portrait", null, Portrait).GetComponent<Image>();
        var crown = Picture(row.Face.transform, "Crown", CoreAssets.FormationCrown, Crown);
        var crownRect = crown.GetComponent<RectTransform>();
        crownRect.anchorMin = crownRect.anchorMax = new Vector2(0.5f, 1f);
        crownRect.anchoredPosition = new Vector2(0f, -2f);
        row.Crown = crown.GetComponent<Image>();
        var side = Layout<VerticalLayoutGroup>(entry.transform, "Side", 1f, new RectOffset(0, 0, 0, 0));
        Text(parts, side.transform, "+", NameWidth);
        row.Name = side.transform.GetChild(side.transform.childCount - 1).GetComponent<TMP_Text>();
        row.Casts = Layout<HorizontalLayoutGroup>(side.transform, "Casts", 2f, new RectOffset(0, 0, 0, 0));
        row.Casts.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        for (var place = 0; place < TacticalFormationGrid.CastSlots; place++)
        {
            var box = new GameObject("Cast_" + place, Il2CppType.Of<RectTransform>());
            box.transform.SetParent(row.Casts.transform, false);
            box.AddComponent<Image>().color = CastColor;
            Fixed(box, CastBox, CastBox);
            row.Icons[place] = Picture(box.transform, "Icon", null, CastIcon).GetComponent<Image>();
            var (at, which) = (index, place);
            DelversMenuWindow.EnsureButton(box).onClick.AddListener((Action)(() => OpenPicker(at, which, box.GetComponent<RectTransform>())));
        }
        var trigger = entry.AddComponent<EventTrigger>();
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.BeginDrag, () => BeginListDrag(index));
        AddPointer(trigger, EventTriggerType.Drag, MoveGhost);
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.EndDrag, EndListDrag);
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerEnter, () => HoverList(index));
        TacticalFormationButton.AddTrigger(trigger, EventTriggerType.PointerExit, () => HoverList(-1));
        return row;
    }

    // ---- showing ----

    private static void RefreshList()
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var unit = i < grid.Members.Count ? grid.Members[i] : Guid.Empty;
            var member = unit != Guid.Empty;
            row.Back.color = listFrom >= 0 && i == listHover ? CellTarget : RowColor;
            row.Face.sprite = member && i != listFrom ? PortraitOf(unit) : null;
            row.Face.enabled = row.Face.sprite != null;
            row.Crown.enabled = member && i == 0 && i != listFrom && row.Crown.sprite != null;
            DelversMenuWindow.SetText(row.Name, member ? NameOf(unit) : "+");
            row.Name.color = member ? Beige : PlusColor;
            row.Casts.SetActive(member);
            if (!member) continue;
            var casts = grid.Casts(unit);
            units.TryGetValue(unit, out var entity);
            for (var place = 0; place < row.Icons.Length; place++)
            {
                var icon = row.Icons[place];
                icon.sprite = SkillIcon(casts[place]);
                icon.enabled = icon.sprite != null;
                icon.color = icon.enabled && !TakesPart(entity, casts[place]) ? Faded : Color.white;
                var box = icon.transform.parent.GetComponent<Image>();
                if (box != null) box.color = casts[place].Length > 0 && !icon.enabled ? TakenWithoutIcon : CastColor;
            }
        }
    }

    // ---- dragging in the list ----

    private static void BeginListDrag(int index)
    {
        if (index >= grid.Members.Count) return;
        ClosePicker();
        listFrom = index;
        ghost.sprite = PortraitOf(grid.Members[index]);
        ghost.gameObject.SetActive(true);
        ghost.transform.SetAsLastSibling();
        Refresh();
    }

    private static void EndListDrag()
    {
        if (listFrom < 0) return;
        if (listHover >= 0 && listFrom < grid.Members.Count)
            grid.MoveInList(grid.Members[listFrom], Math.Min(listHover, grid.Members.Count - 1));
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
    // shows the skill's icon and its name from the game's text table; the first row, with a drawn cross, clears the place.
    private static void OpenPicker(int index, int place, RectTransform box)
    {
        ClosePicker();
        if (index >= grid.Members.Count || box == null) return;
        var unit = grid.Members[index];
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
        rect.anchoredPosition += new Vector2(-CastBox * 0.5f, -CastBox * 0.5f);

        AddChoice(unit, place, "");
        foreach (var skill in skills) AddChoice(unit, place, skill);
        if (uiParts?.DropdownEnd != null)
        {
            var end = new GameObject("End", Il2CppType.Of<RectTransform>());
            end.transform.SetParent(picker.transform, false);
            end.AddComponent<Image>().sprite = uiParts.DropdownEnd;
            Fixed(end, ChoiceWidth, 4f);
        }
        picker.transform.SetAsLastSibling();
        DelversHost.Info($"CORE_TACTICAL_CASTS_OFFERED unit={unit:D} place={place} skills={skills.Count}");
    }

    // One row of the dropdown: the options window's list row with the skill's icon at its left and its name beside it.
    private static void AddChoice(Guid unit, int place, string skill)
    {
        var name = skill.Length == 0 ? "Choice_Empty" : "Choice_" + skill;
        GameObject choice;
        if (uiParts?.DropdownRow != null) choice = DelversMenuWindow.Clone(uiParts.DropdownRow, picker.transform, name);
        else
        {
            choice = new GameObject(name, Il2CppType.Of<RectTransform>());
            choice.transform.SetParent(picker.transform, false);
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
