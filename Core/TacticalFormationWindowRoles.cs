using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The role column of the formation window: a field in each member's row, with the role's
// symbol (the author's own, CoreAssets.RoleIcon); a click opens the four roles with symbol and name. Each formation keeps its own
// roles (TacticalFormationBook.Set.Roles), a member starts Neutral. Summon places have no role, they follow their summoner. The
// group AI reads the role through TacticalFormation.RoleOf. (the author's choice of the drafts) the field stands
// inside the row at its right, the symbol above the role's name; both in one framed block (TacticalFormationWindowFrames).
internal static partial class TacticalFormationWindow
{
    private const float RoleChoiceWidth = 180f, RoleStripe = 4f, RoleCheck = 16f;
    private static GameObject rolePicker;

    private static void ClearRoles() => rolePicker = null;

    // The field as the row's last part: clear, the whole column clickable.
    private static void AddRoleField(GameObject entry, Row row, int index)
    {
        // Symbol and name in one block (TacticalFormationWindowFrames.AddRoleBlock, the author's choice 1 of the sheet).
        var field = Layout<VerticalLayoutGroup>(entry.transform, "Role_" + index, 0f, new RectOffset(0, 0, RoleBlockTop, 0));
        field.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        field.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        Fixed(field, RoleColumn, EntryHeight - 4f);
        var rect = field.GetComponent<RectTransform>();
        row.Role = field;
        row.RoleIcon = AddRoleBlock(field.transform, out var name);
        row.RoleName = name;
        DelversMenuWindow.EnsureButton(field).onClick.AddListener((Action)(() => OpenRolePicker(index, rect)));
    }

    // A member's role in the shown formation, from the window or the test hook.
    internal static bool SetRole(Guid unit, TacticalRole role)
    {
        if (grid == null || !grid.SetRole(unit, role)) return false;
        DelversHost.Info($"CORE_TACTICAL_ROLE unit={unit:D} role={(TacticalRoles.Word(role) ?? "neutral")} set={page + 1}");
        CloseRolePicker();
        Refresh();
        return true;
    }

    // The four roles below the field, built like the casts' dropdown from the options window's list rows.
    private static void OpenRolePicker(int index, RectTransform field)
    {
        ClosePicker();
        CloseAdder();
        CloseRolePicker();
        if (grid == null || field == null || RowUnit(index) is not { } unit || !grid.Members.Contains(unit)) return;
        pickerBlocker = new GameObject("RoleBlocker", Il2CppType.Of<RectTransform>());
        pickerBlocker.transform.SetParent(root.transform, false);
        var blockerRect = pickerBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        pickerBlocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        DelversMenuWindow.EnsureButton(pickerBlocker).onClick.AddListener((Action)CloseRolePicker);

        rolePicker = new GameObject("RolePicker", Il2CppType.Of<RectTransform>());
        rolePicker.transform.SetParent(root.transform, false);
        var layout = rolePicker.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rolePicker.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = rolePicker.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.position = field.position;
        // Docked at the field's bottom left corner (the field is RoleColumn wide and the row's height less its padding).
        rect.anchoredPosition += new Vector2(-RoleColumn * 0.5f, -(EntryHeight - 4f) * 0.5f) * ScaleLikeWindow(rect);
        foreach (var role in TacticalRoles.All)
        {
            var chosen = role;
            // The role it has already closes the choice too; SetRole stays false for it (the test hook reports that).
            RoleLine(chosen, MenuText.Pick(TacticalFormationText.RoleName(chosen)), grid.RoleOf(unit) == chosen, () =>
            {
                if (!SetRole(unit, chosen)) CloseRolePicker();
            });
        }
        AddChoiceEnd(rolePicker.transform, RoleChoiceWidth);
        rolePicker.transform.SetAsLastSibling();
        KeepInside(rolePicker.GetComponent<RectTransform>(), field, true);
    }

    // One line: a stripe in the role's colour at its left edge, symbol and name, the game's check mark at the current role.
    private static void RoleLine(TacticalRole role, string name, bool current, Action pick)
    {
        var icon = CoreAssets.RoleIcon(role);
        GameObject line;
        if (uiParts?.DropdownRow != null) line = DelversMenuWindow.Clone(uiParts.DropdownRow, rolePicker.transform, "Role");
        else
        {
            line = new GameObject("Role", Il2CppType.Of<RectTransform>());
            line.transform.SetParent(rolePicker.transform, false);
            line.AddComponent<Image>().color = CastColor;
        }
        Fixed(line, RoleChoiceWidth, ChoiceHeight);
        var picture = Picture(line.transform, "Icon", icon, ChoiceIcon);
        var pictureRect = picture.GetComponent<RectTransform>();
        pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0f, 0.5f);
        pictureRect.pivot = new Vector2(0f, 0.5f);
        pictureRect.anchoredPosition = new Vector2(6f + RoleStripe, 0f);
        var text = line.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.enableWordWrapping = false;
            text.margin = new Vector4(ChoiceIcon + 12f + RoleStripe, 0f, RoleCheck + 12f, 0f);
            text.alignment = TextAlignmentOptions.Left;
            text.color = current ? FrontColor : Beige;
            DelversMenuWindow.SetText(text, name);
        }
        var stripe = new GameObject("Stripe", Il2CppType.Of<RectTransform>());
        stripe.transform.SetParent(line.transform, false);
        (stripe.GetComponent<LayoutElement>() ?? stripe.AddComponent<LayoutElement>()).ignoreLayout = true;
        var stripeImage = stripe.AddComponent<Image>();
        stripeImage.color = RoleColour(role);
        stripeImage.raycastTarget = false;
        var stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.anchorMin = Vector2.zero;
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        // On the line's face, not in the clear margin of its picture (TacticalFormationWindowLook.ChoiceSide).
        stripeRect.anchoredPosition = new Vector2(ChoiceSide, 0f);
        stripeRect.sizeDelta = new Vector2(RoleStripe, 0f);
        if (current && uiParts?.CheckOn != null)
        {
            var check = Picture(line.transform, "Check", uiParts.CheckOn, RoleCheck);
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(1f, 0.5f);
            checkRect.pivot = new Vector2(1f, 0.5f);
            checkRect.anchoredPosition = new Vector2(-8f, 0f);
        }
        DelversMenuWindow.EnsureButton(line).onClick.AddListener(pick);
        line.SetActive(true);
    }

    private static void CloseRolePicker()
    {
        foreach (var open in new[] { rolePicker, pickerBlocker })
        {
            if (open == null) continue;
            open.SetActive(false);
            UnityEngine.Object.Destroy(open);
        }
        rolePicker = pickerBlocker = null;
    }
}
