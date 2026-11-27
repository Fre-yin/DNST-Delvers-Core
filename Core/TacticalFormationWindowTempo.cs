using UnityEngine;
using UnityEngine.UI;
#if BEPINEX
using InteractableUI = global::Refactor.UI.Util.InteractableUI;
using TooltipFeedback = global::Refactor.UI.Util.TooltipFeedback;
#else
using InteractableUI = Il2CppRefactor.UI.Util.InteractableUI;
using TooltipFeedback = Il2CppRefactor.UI.Util.TooltipFeedback;
#endif

namespace DungeonSettlersDelvers.Core;

// The tempo button beside the grid (TacticalFormationTempo): a check box row like the game's options, off at
// first. A click switches it at once in Core's block of the save, like the formation's own switch; the next look of the formation
// applies it. It stands beside the grid at its lower edge, outside the grid's layout: below the grid the window has no room left
// (the row lay outside the visible part). Since the grid stands at the content box's right the room is at the
// grid's left, between the list and the grid; the row is right aligned against the grid.
internal static partial class TacticalFormationWindow
{
    // The room left of the grid, right of the list's scroll bar (at 132 the check mark lay over the bar on bfa691a).
    private const float TempoWidth = 112f, TempoHeight = 64f, TempoGap = 6f, TempoTextLeft = 16f;

    private static Image tempoMark;
    private static Sprite tempoOn;
    private static Sprite tempoOff;

    private static void ClearTempo()
    {
        tempoMark = null;
        tempoOn = null;
        tempoOff = null;
    }

    private static void AddTempo(DelversMenuWindow.Parts parts, Transform grid)
    {
        if (parts.ToggleRow == null) return;
        var row = DelversMenuWindow.Clone(parts.ToggleRow, grid, "Tempo");
        (row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-TempoGap, 0f);
        rect.sizeDelta = new Vector2(TempoWidth, TempoHeight);
        // The native Toggle would fade the mark on its own; the sprite swap below replaces it (as DelversMenuWindow's toggles).
        var toggle = row.GetComponent<Toggle>();
        if (toggle != null) UnityEngine.Object.DestroyImmediate(toggle);
        tempoOn = parts.CheckOn;
        tempoOff = parts.CheckOff;
        tempoMark = row.transform.Find("Img_Check")?.GetComponent<Image>();
        if (tempoMark != null)
        {
            tempoMark.enabled = true;
            tempoMark.canvasRenderer.SetAlpha(1f);
            var markRect = tempoMark.GetComponent<RectTransform>();
            markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = new Vector2(0f, 0f);
            markRect.sizeDelta = new Vector2(10f, 10f);
        }
        var text = row.transform.Find("Txt_Title")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TempoTextLeft, 0f);
            textRect.offsetMax = Vector2.zero;
            text.enableWordWrapping = true;
            text.enableAutoSizing = false;
            var label = MenuText.Pick(TacticalFormationText.Tempo);
            DelversMenuWindow.SetText(text, label);
            FitLongestWord(text, label, TempoWidth - TempoTextLeft);
        }
        // An invisible hit area makes the whole row clickable, like the native toggle.
        var hit = row.GetComponent<Image>() ?? row.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var button = DelversMenuWindow.EnsureButton(row);
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((Action)SwitchTempo);
        AddTempoTooltip(row, hit);
        row.SetActive(true);
        RefreshTempo();
    }

    // The game's own tooltip on hover, the way the formation button has it (TacticalFormationButton): the game's TooltipFeedback with
    // a key whose name and text are rows of the game's text table (TacticalFormationText.TempoTooltipKey). The options row the button
    // is cloned from brings none, so the row gets the game's hover component (InteractableUI) with the feedback on it.
    private static void AddTempoTooltip(GameObject row, Image area)
    {
        try
        {
            var feedback = row.GetComponentInChildren<TooltipFeedback>(true);
            if (feedback == null)
            {
                var ui = row.GetComponent<InteractableUI>() ?? row.AddComponent<InteractableUI>();
                feedback = row.AddComponent<TooltipFeedback>();
                feedback.Init(ui);
                ui.AddFeedback(feedback.Type, feedback);
                ui.SetArea(area);
            }
            feedback.SetTooltipKey(TacticalFormationText.TempoTooltipKey);
            DelversHost.Info("CORE_TACTICAL_TEMPO_TOOLTIP key=" + TacticalFormationText.TempoTooltipKey);
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TEMPO_TOOLTIP_FAILED: " + ex.Message); }
    }

    // The test hook's click (DelversCoreRuntime.FormationTempoForTest): only with the window open, like the row.
    internal static bool SetTempo(bool on)
    {
        var data = CoreSaveStore.Current;
        if (!IsOpen || tempoMark == null || data == null) return false;
        if (data.FormationTempo != on) SwitchTempo();
        return data.FormationTempo == on;
    }

    // The font gets smaller until the label's longest word fits the width: a word does not wrap, and the game's text with auto size
    // let it run over the edge (a long German word was cut at the right). A label without spaces (Chinese,
    // Japanese) wraps between any two characters and keeps its size.
    private static void FitLongestWord(TMP_Text text, string label, float width)
    {
        if (!label.Contains(' ')) return;
        var longest = "";
        foreach (var word in label.Split(' '))
            if (word.Length > longest.Length) longest = word;
        try
        {
            var smallest = Mathf.Round(text.fontSize * 0.6f);
            while (text.fontSize > smallest && text.GetPreferredValues(longest, 0f, 0f).x > width) text.fontSize -= 1f;
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TEXT_HEIGHT_FAILED: " + ex.Message); }
    }

    private static void SwitchTempo()
    {
        var data = CoreSaveStore.Current;
        if (data == null) return;
        data.SetFormationTempo(!data.FormationTempo);
        DelversHost.Info($"CORE_TACTICAL_TEMPO_SWITCH on={(data.FormationTempo ? 1 : 0)}");
        RefreshTempo();
    }

    private static void RefreshTempo()
    {
        if (tempoMark == null) return;
        var sprite = CoreSaveStore.Current?.FormationTempo == true ? tempoOn : tempoOff;
        if (sprite != null) tempoMark.sprite = sprite;
    }
}
