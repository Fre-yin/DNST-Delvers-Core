using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if BEPINEX
using TooltipFeedback = global::Refactor.UI.Util.TooltipFeedback;
#else
using TooltipFeedback = Il2CppRefactor.UI.Util.TooltipFeedback;
#endif

namespace DungeonSettlersDelvers.Core;

// The formation button in the option row under a selected party member (Core's plan), after Auto Skill, Hold
// Position and Prioritize Sleep. The row (EntitySummaryOptionUI) holds one ToggleButton per BehaviourOption of the game, a bit
// field that goes into the save; the formation is no such bit. Core copies the row's first button once per row, takes the
// game's toggle off the copy and keeps its frame, its tooltip and its place in the row's layout. Like the game's options a left
// click switches the formation of the selected members on or off and the button shows it; a right click opens the formation
// window. The switch goes into Core's block (TacticalFormation.Switch), nothing into the game's part.
// The row is filled anew for every selection (InitToggles, for units, items and buildings alike): the button hides there and
// shows again only for a party member (InitPlayerUnitUI) or a selection with party members (InitMultipleEntityUI).
internal static class TacticalFormationButton
{
    private const string ObjectName = "Btn_DelversFormation";

    private static readonly Dictionary<IntPtr, GameObject> buttons = new();
    private static readonly Dictionary<IntPtr, Image> icons = new();
    private static readonly List<UnitEntity> selection = new();
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);

    internal static void ResetLifecycle()
    {
        buttons.Clear();
        icons.Clear();
        selection.Clear();
    }

    internal static void Hide(EntitySummaryOptionUI row)
    {
        var button = Find(row, false);
        if (button != null && button.activeSelf) button.SetActive(false);
    }

    internal static void Show(EntitySummaryOptionUI row, IEnumerable<UnitEntity> units)
    {
        // The comfort option tactical formation (QolText) off: no button, until the next selection after switching it on.
        if (!CoreSettings.TacticalEnabled)
        {
            Hide(row);
            return;
        }
        selection.Clear();
        foreach (var unit in units)
            if (unit != null && unit.Faction == FactionType.Player && !unit.IsDead) selection.Add(unit);
        if (selection.Count == 0) return;
        var button = Find(row, true);
        if (button == null) return;
        button.transform.SetAsLastSibling();
        if (!button.activeSelf) button.SetActive(true);
        ShowState(row);
    }

    // The on or off picture for the selection.
    private static void ShowState(EntitySummaryOptionUI row)
    {
        if (icons.TryGetValue(row.Pointer, out var icon)) ShowState(icon);
    }

    private static void ShowState(Image icon)
    {
        if (icon == null) return;
        var sprite = TacticalFormation.AllOn(selection) ? CoreAssets.FormationButtonOn : CoreAssets.FormationButtonOff;
        if (sprite != null) icon.sprite = sprite;
    }

    // The formation switched without the button (Prioritize Sleep, TacticalFormationSleep): every row shows it anew.
    internal static void RefreshAll()
    {
        foreach (var icon in icons.Values) ShowState(icon);
    }

    // The row's own copy, made on first use; null when the row has no button to copy.
    private static GameObject Find(EntitySummaryOptionUI row, bool create)
    {
        if (row == null) return null;
        if (buttons.TryGetValue(row.Pointer, out var known) && known != null) return known;
        if (!create) return null;
        var made = Make(row, out var icon);
        if (made == null) return null;
        buttons[row.Pointer] = made;
        if (icon != null) icons[row.Pointer] = icon;
        return made;
    }

    private static GameObject Make(EntitySummaryOptionUI row, out Image icon)
    {
        icon = null;
        var source = row._btns != null && row._btns.Length > 0 ? row._btns[0] : null;
        if (source == null)
        {
            ReportOnce("CORE_TACTICAL_BUTTON_UNAVAILABLE reason=no button in the option row");
            return null;
        }
        var iconPath = PathBelow(source.transform, source._targetImage != null ? source._targetImage.transform : null);
        // The small box with the option's key at the top right (I4, the copy showed the word squeezed into it); the
        // formation has no key.
        var keyPath = PathBelow(source.transform, source._txtCase != null ? source._txtCase.transform : null);
        var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = ObjectName;
        // The game's toggle would switch the copy on and off and change its sprites on hover.
        var toggle = copy.GetComponent<ToggleButton>();
        if (toggle != null) UnityEngine.Object.DestroyImmediate(toggle);
        var button = copy.GetComponent<Button>();
        if (button == null) button = copy.AddComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        var shown = iconPath == null ? copy.GetComponent<Image>() : copy.transform.Find(iconPath)?.GetComponent<Image>();
        icon = shown;
        button.onClick.AddListener((Action)(() => OnSwitch(row)));
        var trigger = copy.GetComponent<EventTrigger>() ?? copy.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener((Action<BaseEventData>)(data =>
        {
            if (data?.TryCast<PointerEventData>()?.button == PointerEventData.InputButton.Right) OnOpen();
        }));
        trigger.triggers.Add(entry);
        if (shown == null || CoreAssets.FormationButtonOff == null || CoreAssets.FormationButtonOn == null) ReportOnce("CORE_TACTICAL_BUTTON_ICON_MISSING");
        var tooltip = copy.GetComponentInChildren<TooltipFeedback>(true);
        if (tooltip != null) tooltip.SetTooltipKey(TacticalFormationText.TooltipKey);
        else ReportOnce("CORE_TACTICAL_BUTTON_NO_TOOLTIP");
        var keyBox = string.IsNullOrEmpty(keyPath) ? null : copy.transform.Find(keyPath);
        if (keyBox != null) keyBox.gameObject.SetActive(false);
        foreach (var label in copy.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        DelversHost.Info($"CORE_TACTICAL_BUTTON_MADE icon={(shown != null && CoreAssets.FormationButtonOn != null ? "own" : "missing")} tooltip={(tooltip != null ? 1 : 0)} key={(keyBox != null ? "hidden" : "-")}");
        return copy;
    }

    internal static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((Action<BaseEventData>)(_ =>
        {
            try { action(); }
            catch (Exception ex) { ReportOnce("CORE_TACTICAL_UI_FAILED event=" + type + ": " + ex.Message); }
        }));
        trigger.triggers.Add(entry);
    }

    // Left click: the selection's formation the other way round, as the game's own options switch.
    private static void OnSwitch(EntitySummaryOptionUI row)
    {
        try
        {
            if (TacticalFormation.Switch(selection, !TacticalFormation.AllOn(selection))) ShowState(row);
        }
        catch (Exception ex) { ReportOnce("CORE_TACTICAL_SWITCH_FAILED: " + ex.Message); }
    }

    // Right click: the formation window for the selection's group.
    private static void OnOpen()
    {
        try { TacticalFormationWindow.Open(TacticalFormation.Group(selection)); }
        catch (Exception ex) { ReportOnce("CORE_TACTICAL_OPEN_FAILED: " + ex.Message); }
    }

    // The path of a child below a root, "" for the root itself, null when it is not below.
    private static string PathBelow(Transform root, Transform child)
    {
        if (child == null) return null;
        var parts = new List<string>();
        for (var at = child; at != null; at = at.parent)
        {
            if (at == root) return string.Join("/", Enumerable.Reverse(parts));
            parts.Add(at.name);
        }
        return null;
    }

    internal static void ReportOnce(string line)
    {
        if (reported.Add(line)) DelversHost.Warning(line);
    }
}

[HarmonyPatch(typeof(EntitySummaryOptionUI), nameof(EntitySummaryOptionUI.InitToggles))]
internal static class TacticalFormationRowPatch
{
    private static void Postfix(EntitySummaryOptionUI __instance)
    {
        try { TacticalFormationButton.Hide(__instance); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_BUTTON_FAILED stage=row: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(EntitySummaryUIView), nameof(EntitySummaryUIView.InitPlayerUnitUI))]
internal static class TacticalFormationPlayerUnitPatch
{
    private static void Postfix(EntitySummaryUIView __instance, UnitEntity unit)
    {
        try { TacticalFormationButton.Show(__instance._optionUI, new[] { unit }); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_BUTTON_FAILED stage=unit: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(EntitySummaryUIView), nameof(EntitySummaryUIView.InitMultipleEntityUI))]
internal static class TacticalFormationSelectionPatch
{
    // The game's parameter is called entites.
    private static void Postfix(EntitySummaryUIView __instance, Il2CppSystem.Collections.Generic.List<IEntity> entites)
    {
        try
        {
            var units = new List<UnitEntity>();
            for (var i = 0; entites != null && i < entites.Count; i++)
            {
                var unit = entites[i]?.TryCast<UnitEntity>();
                if (unit != null) units.Add(unit);
            }
            TacticalFormationButton.Show(__instance._optionUI, units);
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_BUTTON_FAILED stage=selection: " + ex.Message); }
    }
}
