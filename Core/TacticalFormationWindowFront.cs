using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if BEPINEX
using TooltipUI = global::Refactor.UI.TooltipUI;
#else
using TooltipUI = Il2CppRefactor.UI.TooltipUI;
#endif

namespace DungeonSettlersDelvers.Core;

// The window among the game's other layers (the author's hand test of 0.5.2):
//   P07 the game's tooltips in front of the window. The window's canvas sorts above the game's whole interface (SortingOrder),
//        the tooltips too, so the tooltip of "In Formation bewegen" lay behind it. While the window is open, the game's tooltip
//        layer (TooltipUI) gets a sorting of its own just above the window; closing gives it back exactly as it was.
//   Drag the window at its title bar, like the Modding Toolkit's window; it always stays wholly on the screen and keeps its place
//   for the next opening while the game runs (RestorePlace).
internal static partial class TacticalFormationWindow
{
    private const int TooltipAbove = 20;
    private static readonly TacticalFormationTooltipLift<Canvas> liftedTooltips = new();
    private static bool tooltipsLogged;

    // Called when the window is built: every TooltipUI in a scene sorts above the window.
    private static void LiftTooltips()
    {
        DropTooltips();
        var named = new List<string>();
        try
        {
            foreach (var layer in Resources.FindObjectsOfTypeAll<TooltipUI>())
            {
                if (layer == null || !layer.gameObject.scene.IsValid()) continue;
                var canvas = layer.GetComponent<Canvas>();
                var added = canvas == null;
                if (added) canvas = layer.gameObject.AddComponent<Canvas>();
                if (canvas == null) continue;
                liftedTooltips.Note(canvas, added, canvas.overrideSorting, canvas.sortingOrder);
                canvas.overrideSorting = true;
                canvas.sortingOrder = TacticalFormationTooltipLift<Canvas>.Order(canvas.sortingOrder, SortingOrder + TooltipAbove);
                named.Add(layer.gameObject.name + ":" + (added ? "added" : "own"));
            }
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TOOLTIP_FRONT_FAILED: " + ex.Message); }
        if (tooltipsLogged) return;
        tooltipsLogged = true;
        DelversHost.Info("CORE_TACTICAL_TOOLTIP_FRONT layers=" + (named.Count == 0 ? "-" : string.Join(",", named)) + " order=" + (SortingOrder + TooltipAbove));
    }

    // Called when the window closes: each lifted layer as it was, a canvas the window added taken away at once
    // (TacticalFormationTooltipLift.Drop: Destroy waits for the frame's end, and a new opening in the same frame lifted it again).
    private static void DropTooltips()
        => liftedTooltips.Drop(canvas =>
        {
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
        }, (canvas, overrideSorting, order) =>
        {
            if (canvas == null) return;
            canvas.overrideSorting = overrideSorting;
            canvas.sortingOrder = order;
        }, TacticalFormationButton.ReportOnce);

    // ---- dragging ----
    // The title bar is the band from the panel's top down to the content box; an invisible handle over it takes the drag, the
    // close button stays above it. The open choices close when a drag starts, they would stay behind.
    private const float TitleBandFallback = 90f;

    private static void AddTitleDrag(RectTransform panel)
    {
        var band = TitleBandFallback;
        try
        {
            if (contentBox != null)
            {
                var corners = new Il2CppStructArray<Vector3>(4);
                contentBox.GetWorldCorners(corners);
                var boxTop = panel.InverseTransformPoint(corners[1]).y;
                panel.GetWorldCorners(corners);
                var panelTop = panel.InverseTransformPoint(corners[1]).y;
                if (panelTop - boxTop is > 30f and < 200f) band = panelTop - boxTop;
            }
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TITLE_BAND_FAILED: " + ex.Message); }
        var handle = new GameObject("TitleDrag", Il2CppType.Of<RectTransform>());
        handle.transform.SetParent(panel, false);
        var rect = handle.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, band);
        handle.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var trigger = handle.AddComponent<EventTrigger>();
        AddPointer(trigger, EventTriggerType.BeginDrag, _ =>
        {
            ClosePicker();
            CloseAdder();
            CloseRolePicker();
        });
        AddPointer(trigger, EventTriggerType.Drag, pointer =>
        {
            if (pointer != null) MoveWindow(pointer.delta);
        });
        AddPointer(trigger, EventTriggerType.EndDrag, _ =>
        {
            RememberPlace();
            DelversHost.Info("CORE_TACTICAL_WINDOW_MOVED " + WindowPlace());
        });
        handle.transform.SetAsLastSibling();
        panel.Find("Btn_Exit")?.SetAsLastSibling();
    }

    // A new opening in the same scene at the same size takes it again, kept on the
    // screen; another size, another scene (back to the main menu and a load) or a new start of the game opens it in the middle.
    private static Vector3 placeShift, builtMiddle;
    private static int placeWidth, placeHeight, placeScene;
    private static bool placeKept;

    private static void ForgetPlace() => placeKept = false;

    private static Vector3 PanelMiddle()
    {
        var corners = new Il2CppStructArray<Vector3>(4);
        windowPanel.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private static int SceneNow() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;

    private static void RememberPlace()
    {
        try
        {
            if (windowPanel == null) return;
            placeShift = PanelMiddle() - builtMiddle;
            placeWidth = Screen.width;
            placeHeight = Screen.height;
            placeScene = SceneNow();
            placeKept = true;
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PLACE_FAILED: " + ex.Message); }
    }

    // Called when the window is built.
    private static void RestorePlace()
    {
        try
        {
            if (windowPanel == null) return;
            builtMiddle = PanelMiddle();
            if (!placeKept) return;
            if (placeWidth != Screen.width || placeHeight != Screen.height || placeScene != SceneNow())
            {
                placeKept = false;
                return;
            }
            windowPanel.position += placeShift;
            KeepOnScreen();
            DelversHost.Info("CORE_TACTICAL_WINDOW_PLACE_KEPT " + WindowPlace());
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_PLACE_FAILED: " + ex.Message); }
    }

    // By a screen delta; the window's panel is in screen pixels on the overlay canvas.
    internal static bool MoveWindow(Vector2 delta)
    {
        if (windowPanel == null) return false;
        windowPanel.position += new Vector3(delta.x, delta.y, 0f);
        KeepOnScreen();
        return true;
    }

    // The whole panel on the screen: pushed back by what sticks out, its left and top edge first when it is larger.
    private static void KeepOnScreen()
    {
        if (windowPanel == null) return;
        var corners = new Il2CppStructArray<Vector3>(4);
        windowPanel.GetWorldCorners(corners);
        float left = corners[0].x, bottom = corners[0].y, right = corners[2].x, top = corners[2].y;
        var shift = new Vector3(TacticalFormationScreenRules.Shift(left, right, Screen.width, false),
            TacticalFormationScreenRules.Shift(bottom, top, Screen.height, true), 0f);
        if (shift != Vector3.zero) windowPanel.position += shift;
    }

    // "dx=<px> dy=<px> inside=1": the panel's middle from the screen's middle, for the log and the tests.
    internal static string WindowPlace()
    {
        if (windowPanel == null) return "open=0";
        var corners = new Il2CppStructArray<Vector3>(4);
        windowPanel.GetWorldCorners(corners);
        var middle = (corners[0] + corners[2]) * 0.5f;
        var inside = corners[0].x >= -0.5f && corners[0].y >= -0.5f && corners[2].x <= Screen.width + 0.5f && corners[2].y <= Screen.height + 0.5f;
        return "dx=" + Mathf.RoundToInt(middle.x - Screen.width * 0.5f) + " dy=" + Mathf.RoundToInt(middle.y - Screen.height * 0.5f) + " inside=" + (inside ? 1 : 0);
    }
}
