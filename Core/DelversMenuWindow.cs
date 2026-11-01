using System.Collections;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The Delvers menu: a clone of the game's options window with its dimmed
// backdrop, one tab per page of DelversMenuRegistry. Every row is a clone of the window's own
// parts at its native size (label box 292×34, value box 224×34, toggle row, section title,
// divider 514×2); nothing is stretched. While the menu is open its values refresh four times a
// second; the game's Escape, the close buttons or a scene change close it.
internal static class DelversMenuWindow
{
    // The Modding Toolkit's window (30000) stays above the menu.
    private const int SortingOrder = 29000;
    private const float RefreshSeconds = 0.25f;
    private const float LabelWidth = 292f, ControlWidth = 224f, ControlHeight = 34f;
    // Between the label box and the control of a field row, and between toggles side by side.
    private const float FieldSpacing = 6f;
    private const float FieldWidth = LabelWidth + FieldSpacing + ControlWidth;
    private static readonly Color Beige = new(0.8f, 0.75f, 0.72f, 1f);
    private static readonly Color Muted = new(0.65f, 0.57f, 0.52f, 1f);
    private static readonly Color Danger = new(1f, 0.6f, 0.5f, 1f);
    private static readonly Color Warning = new(1f, 0.55f, 0.45f, 1f);

    private static Parts parts;
    private static GameObject root;
    private static RectTransform panel;
    private static Transform tabHolder;
    private static Transform content;
    private static ScrollRect scroll;
    private static readonly List<(string PackId, Image Image)> tabs = new();
    private static readonly List<Action> refreshers = new();
    private static GameObject page;
    private static GameObject dropdown;
    private static GameObject dropdownBlocker;
    private static DelversMenuRow dropdownOwner;
    private static string currentPack;
    private static int shownRevision = -1;
    private static int sceneHandle;
    private static int generation;
    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    internal static bool IsOpen => root != null;
    internal static string CurrentPack => IsOpen ? currentPack : null;
    internal static RectTransform Panel => panel;

    internal static void Open(string packId = null)
    {
        try
        {
            if (root == null && !Build()) return;
            var pages = DelversCoreRuntime.MenuPages.Snapshot();
            shownRevision = DelversCoreRuntime.MenuPages.Revision;
            BuildTabs(pages);
            ShowPage(Find(pages, packId) ?? Find(pages, currentPack) ?? pages.FirstOrDefault());
            DelversHost.Info($"DELVERS_MENU_OPENED pages={pages.Count} page={currentPack}");
        }
        catch (Exception ex)
        {
            DelversHost.Warning("DELVERS_MENU_OPEN_FAILED: " + ex);
            Close();
        }
    }

    internal static void Close()
    {
        if (root == null) return;
        generation++;
        CloseDropdown();
        UnityEngine.Object.Destroy(root);
        root = null;
        panel = null;
        tabHolder = null;
        content = null;
        scroll = null;
        page = null;
        tabs.Clear();
        refreshers.Clear();
        DelversHost.Info("DELVERS_MENU_CLOSED");
    }

    // The game's Escape (DelversMenuEscapePatch): while the menu is open it takes the key, so the options window below it stays
    // open. An open dropdown closes first, then the menu; the next Escape is the game's again.
    internal static bool TakeEscape()
    {
        if (root == null) return false;
        if (dropdown != null) CloseDropdown();
        else Close();
        return true;
    }

    internal static void ResetLifecycle()
    {
        Close();
        parts = null;
        currentPack = null;
        Reported.Clear();
    }

    private static DelversMenuRegistry.Page Find(IReadOnlyList<DelversMenuRegistry.Page> pages, string packId)
        => packId == null ? null : pages.FirstOrDefault(candidate => candidate.PackId == packId);

    private static bool Build()
    {
        if (!Parts.TryHarvest(out parts, out var reason))
        {
            ReportOnce("DELVERS_MENU_UNAVAILABLE: " + reason);
            return false;
        }
        GameObject created = null;
        try
        {
            created = new GameObject("DelversMenuCanvas", Il2CppType.Of<RectTransform>());
            UnityEngine.Object.DontDestroyOnLoad(created);
            var canvas = created.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = created.AddComponent<CanvasScaler>();
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
            created.AddComponent<GraphicRaycaster>();

            // The whole options window: its root is the dimmed backdrop that keeps clicks off
            // the title menu behind it.
            var window = Clone(parts.Window, created.transform, "DelversMenu");
            var backdrop = window.GetComponent<RectTransform>();
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            panel = Require(window.transform, "Panel_GameOptionUI").GetComponent<RectTransform>();
            SetText(Require(panel, "Txt_Title").GetComponent<TMP_Text>(), "Delvers");
            tabHolder = Require(panel, "Panel_OptionCategoryButton");
            scroll = Require(panel, "ScrollView_SettingData").GetComponent<ScrollRect>();
            content = Require(panel, "ScrollView_SettingData/Viewport/Content");
            for (var i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            foreach (var unused in new[] { "Btn_Info", "Btn_ResetDefault", "Btn_Cancel" })
            {
                var part = panel.Find(unused);
                if (part != null) UnityEngine.Object.DestroyImmediate(part.gameObject);
            }
            panel.Find("Panel_KeySettingAwaitPanel")?.gameObject.SetActive(false);
            Wire(panel.Find("Btn_Exit"), null, Close);
            Wire(panel.Find("Btn_Confirm"), MenuText.Close, Close);

            root = created;
            sceneHandle = SceneManager.GetActiveScene().handle;
            var mine = ++generation;
            DelversHost.StartCoroutine(Loop(mine));
            return true;
        }
        catch (Exception ex)
        {
            ReportOnce("DELVERS_MENU_BUILD_FAILED: " + ex.Message);
            if (created != null) UnityEngine.Object.Destroy(created);
            root = null;
            panel = null;
            return false;
        }
    }

    private static IEnumerator Loop(int mine)
    {
        var next = 0f;
        while (mine == generation && root != null)
        {
            if (SceneManager.GetActiveScene().handle != sceneHandle)
            {
                Close();
                yield break;
            }
            if (Time.unscaledTime >= next)
            {
                next = Time.unscaledTime + RefreshSeconds;
                if (DelversCoreRuntime.MenuPages.Revision != shownRevision)
                {
                    var pages = DelversCoreRuntime.MenuPages.Snapshot();
                    shownRevision = DelversCoreRuntime.MenuPages.Revision;
                    BuildTabs(pages);
                    ShowPage(Find(pages, currentPack) ?? pages.FirstOrDefault());
                }
                Refresh();
            }
            yield return null;
        }
    }

    // ---- tabs and pages ----

    private static void BuildTabs(IReadOnlyList<DelversMenuRegistry.Page> pages)
    {
        tabs.Clear();
        for (var i = tabHolder.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(tabHolder.GetChild(i).gameObject);
        // The holder is sized for four tabs (4 × 30 + 3 × 8); it grows downwards, its top stays.
        var rect = tabHolder.GetComponent<RectTransform>();
        var height = Math.Max(1, pages.Count) * 30f + (Math.Max(1, pages.Count) - 1) * 8f;
        var top = rect.anchoredPosition.y + (1f - rect.pivot.y) * rect.sizeDelta.y;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top - (1f - rect.pivot.y) * height);
        foreach (var shown in pages)
        {
            var target = shown;
            var tab = Clone(parts.CategoryButton, tabHolder, "Btn_" + shown.PackId);
            var text = tab.GetComponentInChildren<TMP_Text>(true);
            if (text != null) SetText(text, shown.Title);
            var button = EnsureButton(tab);
            button.onClick.AddListener((Action)(() => ShowPage(target)));
            tabs.Add((shown.PackId, tab.GetComponent<Image>()));
            tab.SetActive(true);
        }
    }

    private static void ShowPage(DelversMenuRegistry.Page shown)
    {
        CloseDropdown();
        refreshers.Clear();
        if (page != null) UnityEngine.Object.DestroyImmediate(page);
        page = null;
        currentPack = shown?.PackId;
        // Like the options window: only the sprite marks the selected tab.
        foreach (var (packId, image) in tabs)
            if (image != null && parts.TabSelected != null && parts.TabUnselected != null)
                image.sprite = packId == currentPack ? parts.TabSelected : parts.TabUnselected;
        if (shown == null) return;

        page = Vertical(content, "Page_" + shown.PackId, 10f, new RectOffset(50, 30, 20, 16));
        List<IReadOnlyDictionary<string, object>> rows;
        try { rows = shown.Rows()?.ToList() ?? new(); }
        catch (Exception ex)
        {
            ReportOnce($"DELVERS_MENU_PAGE_FAILED pack={shown.PackId}: {ex.Message}");
            rows = new() { DelversMenuRows.Text(() => MenuText.PageFailed) };
        }
        var sections = 0;
        foreach (var values in rows)
        {
            var row = DelversMenuRow.Parse(values, out var error);
            if (row == null)
            {
                ReportOnce($"DELVERS_MENU_ROW_INVALID pack={shown.PackId}: {error}");
                continue;
            }
            if (row.Kind == "section" && sections++ > 0) AddDivider(page.transform);
            GameObject created = null;
            try { created = BuildRow(row, page.transform); }
            catch (Exception ex) { ReportOnce($"DELVERS_MENU_ROW_FAILED pack={shown.PackId} kind={row.Kind}: {ex.Message}"); }
            if (created == null || row.Visible == null) continue;
            var target = created;
            void UpdateVisible()
            {
                var visible = Ask(row.Visible, true);
                if (target.activeSelf != visible) target.SetActive(visible);
            }
            refreshers.Add(UpdateVisible);
            UpdateVisible();
        }
        if (scroll != null)
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        Refresh();
    }

    private static void Refresh()
    {
        foreach (var refresh in refreshers.ToArray())
        {
            try { refresh(); }
            catch (Exception ex) { ReportOnce($"DELVERS_MENU_REFRESH_FAILED pack={currentPack}: {ex.Message}"); }
        }
    }

    // ---- rows ----

    private static GameObject BuildRow(DelversMenuRow row, Transform parent) => row.Kind switch
    {
        "section" => BuildSection(row, parent),
        "text" => BuildText(row, parent),
        "toggle" => BuildToggle(row, parent),
        "toggles" => BuildToggles(row, parent),
        "choice" => BuildChoice(row, parent),
        "button" => BuildButton(row, parent),
        _ => null
    };

    private static GameObject BuildSection(DelversMenuRow row, Transform parent)
    {
        var clone = Clone(parts.SectionTitle, parent, "Section");
        var text = clone.GetComponent<TMP_Text>();
        text.enableWordWrapping = false;
        BindText(text, row.Label);
        Fixed(clone, -1f, 30f).flexibleWidth = 1f;
        clone.SetActive(true);
        return clone;
    }

    private static void AddDivider(Transform parent)
    {
        var holder = new GameObject("DividerRow", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(parent, false);
        Fixed(holder, 514f, 14f);
        var line = Clone(parts.Divider, holder.transform, "Divider");
        var rect = line.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(-2f, 0f);
        rect.sizeDelta = new Vector2(514f, 2f);
        line.SetActive(true);
    }

    private static GameObject BuildText(DelversMenuRow row, Transform parent)
    {
        var clone = Clone(parts.BodyText, parent, "Text");
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        var text = clone.GetComponent<TMP_Text>();
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        text.color = row.Warning ? Warning : row.Muted ? Muted : Beige;
        var element = clone.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        clone.SetActive(true);
        string last = null;
        void Update()
        {
            var now = Ask(row.Text, "");
            if (now == last) return;
            last = now;
            SetText(text, now);
            // An empty text takes no room.
            element.ignoreLayout = now.Length == 0;
        }
        refreshers.Add(Update);
        Update();
        return clone;
    }

    private static GameObject BuildToggle(DelversMenuRow row, Transform parent)
    {
        var clone = Clone(parts.ToggleRow, parent, "Toggle");
        Fixed(clone, -1f, 34f).flexibleWidth = 1f;
        // The native Toggle would fade the mark on its own; the sprite swap below replaces it.
        var toggle = clone.GetComponent<Toggle>();
        if (toggle != null) UnityEngine.Object.DestroyImmediate(toggle);
        var mark = clone.transform.Find("Img_Check")?.GetComponent<Image>();
        if (mark != null)
        {
            mark.enabled = true;
            mark.canvasRenderer.SetAlpha(1f);
            var markRect = mark.GetComponent<RectTransform>();
            markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = new Vector2(14f, 0f);
            markRect.sizeDelta = new Vector2(10f, 10f);
        }
        var text = clone.transform.Find("Txt_Title")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(55f, 0f);
            textRect.offsetMax = Vector2.zero;
            text.enableWordWrapping = true;
            BindText(text, row.Label);
        }
        // An invisible hit area makes the whole row clickable, like the native toggle.
        var hit = clone.GetComponent<Image>() ?? clone.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var button = EnsureButton(clone);
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((Action)(() =>
        {
            CloseDropdown();
            Act(Ask(row.Label, ""), () => row.SetBool(!Ask(row.GetBool, false)));
        }));
        bool? shown = null;
        refreshers.Add(() =>
        {
            var on = Ask(row.GetBool, false);
            if (mark != null && shown != on) mark.sprite = on ? parts.CheckOn : parts.CheckOff;
            shown = on;
        });
        BindEnabled(button, text, row.Enabled);
        clone.SetActive(true);
        return clone;
    }

    // Toggles side by side (DelversMenuRows.Toggles): a field row's width in equal parts, spaced like label and control. A label too
    // long for its part ends in an ellipsis on one line instead of wrapping out of the row.
    private static GameObject BuildToggles(DelversMenuRow row, Transform parent)
    {
        var holder = new GameObject("Toggles", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(parent, false);
        var layout = holder.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = FieldSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Fixed(holder, FieldWidth, ControlHeight);
        var width = (FieldWidth - (row.Items.Count - 1) * FieldSpacing) / row.Items.Count;
        foreach (var item in row.Items)
        {
            var toggle = BuildToggle(item, holder.transform);
            Fixed(toggle, width, ControlHeight);
            var text = toggle.transform.Find("Txt_Title")?.GetComponent<TMP_Text>();
            if (text == null) continue;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        return holder;
    }

    private static GameObject BuildChoice(DelversMenuRow row, Transform parent)
    {
        var holder = FieldRow(row, parent);
        var clone = ValueBox(holder.transform, "Choice");
        var text = clone.GetComponentInChildren<TMP_Text>(true);
        BindText(text, () =>
        {
            var options = AskOptions(row);
            var index = Ask(row.GetIndex, -1);
            return index >= 0 && index < options.Count ? options[index] : "";
        });
        var box = clone.GetComponent<RectTransform>();
        var button = EnsureButton(clone);
        button.onClick.AddListener((Action)(() =>
        {
            if (ReferenceEquals(dropdownOwner, row)) CloseDropdown();
            else OpenDropdown(row, box);
        }));
        BindEnabled(button, text, row.Enabled);
        clone.SetActive(true);
        return holder;
    }

    private static GameObject BuildButton(DelversMenuRow row, Transform parent)
    {
        var holder = FieldRow(row, parent);
        var clone = ValueBox(holder.transform, "Button");
        var text = clone.GetComponentInChildren<TMP_Text>(true);
        if (row.Danger && text != null) text.color = Danger;
        BindText(text, row.Caption);
        var button = EnsureButton(clone);
        button.onClick.AddListener((Action)(() =>
        {
            CloseDropdown();
            Act(Ask(row.Caption, ""), row.Click);
        }));
        BindEnabled(button, text, row.Enabled);
        clone.SetActive(true);
        return holder;
    }

    // Label box left (292×34), control right (224×34), as in the options window.
    private static GameObject FieldRow(DelversMenuRow row, Transform parent)
    {
        var holder = new GameObject("Field", Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(parent, false);
        var layout = holder.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = FieldSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Fixed(holder, FieldWidth, ControlHeight);
        var box = Clone(parts.LabelBox, holder.transform, "Label");
        Fixed(box, LabelWidth, ControlHeight);
        var label = box.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enableWordWrapping = false;
            BindText(label, row.Label);
        }
        box.SetActive(true);
        return holder;
    }

    private static GameObject ValueBox(Transform parent, string name)
    {
        var clone = Clone(parts.ValueBox, parent, name);
        Fixed(clone, ControlWidth, ControlHeight);
        return clone;
    }

    // The native dropdown: option slots of 224×30 directly below the box, over the page. An
    // invisible blocker over the whole window takes any other click and only closes the list.
    private static void OpenDropdown(DelversMenuRow row, RectTransform box)
    {
        CloseDropdown();
        var blocker = new GameObject("DropdownBlocker", Il2CppType.Of<RectTransform>());
        blocker.transform.SetParent(panel.parent != null ? panel.parent : panel, false);
        var blockRect = blocker.GetComponent<RectTransform>();
        blockRect.anchorMin = Vector2.zero;
        blockRect.anchorMax = Vector2.one;
        blockRect.offsetMin = Vector2.zero;
        blockRect.offsetMax = Vector2.zero;
        blocker.transform.SetParent(panel, true);
        blocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var blockButton = blocker.AddComponent<Button>();
        blockButton.transition = Selectable.Transition.None;
        NoNavigation(blockButton);
        blockButton.onClick.AddListener((Action)CloseDropdown);
        blocker.transform.SetAsLastSibling();
        dropdownBlocker = blocker;

        var popup = new GameObject("Dropdown", Il2CppType.Of<RectTransform>());
        popup.transform.SetParent(panel, false);
        var rect = popup.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        box.GetWorldCorners(corners);
        var bottomLeft = panel.InverseTransformPoint(corners[0]);
        var area = panel.rect;
        rect.anchoredPosition = new Vector2(bottomLeft.x - area.center.x, bottomLeft.y - area.center.y);
        var layout = popup.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = popup.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var options = AskOptions(row);
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var option = Clone(parts.DropdownRow, popup.transform, "Option");
            Fixed(option, ControlWidth, 30f);
            var text = option.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.enableWordWrapping = false;
                SetText(text, options[i]);
            }
            EnsureButton(option).onClick.AddListener((Action)(() =>
            {
                CloseDropdown();
                Act(Ask(row.Label, ""), () => row.SetIndex(index));
            }));
            option.SetActive(true);
        }
        if (parts.DropdownEnd != null)
        {
            var end = new GameObject("End", Il2CppType.Of<RectTransform>());
            end.transform.SetParent(popup.transform, false);
            end.AddComponent<Image>().sprite = parts.DropdownEnd;
            Fixed(end, ControlWidth, 4f);
        }
        popup.transform.SetAsLastSibling();
        dropdown = popup;
        dropdownOwner = row;
    }

    private static void CloseDropdown()
    {
        // Destroy waits for the end of the frame; hide at once so no second click reaches it.
        foreach (var open in new[] { dropdown, dropdownBlocker })
        {
            if (open == null) continue;
            open.SetActive(false);
            UnityEngine.Object.Destroy(open);
        }
        dropdown = null;
        dropdownBlocker = null;
        dropdownOwner = null;
    }

    // ---- helpers ----

    // A mod's action must never take the menu down: failures are logged with the mod's pack.
    private static void Act(string control, Action action)
    {
        try { action?.Invoke(); }
        catch (Exception ex)
        {
            DelversHost.Warning($"DELVERS_MENU_ACTION_FAILED pack={currentPack} control=\"{control}\": {ex.Message}");
        }
        Refresh();
    }

    private static T Ask<T>(Func<T> value, T fallback)
    {
        if (value == null) return fallback;
        try { return value(); }
        catch (Exception ex)
        {
            ReportOnce($"DELVERS_MENU_VALUE_FAILED pack={currentPack}: {ex.Message}");
            return fallback;
        }
    }

    private static IReadOnlyList<string> AskOptions(DelversMenuRow row)
        => Ask(row.Options, null) ?? Array.Empty<string>();

    private static void BindText(TMP_Text text, Func<string> value)
    {
        if (text == null) return;
        string last = null;
        void Update()
        {
            var now = Ask(value, "");
            if (now == last) return;
            last = now;
            SetText(text, now);
        }
        refreshers.Add(Update);
        Update();
    }

    private static void BindEnabled(Button button, TMP_Text text, Func<bool> enabled)
    {
        if (enabled == null) return;
        bool? shown = null;
        refreshers.Add(() =>
        {
            var now = Ask(enabled, true);
            if (shown == now) return;
            shown = now;
            button.interactable = now;
            if (text != null) text.alpha = now ? 1f : 0.4f;
        });
    }

    private static void Wire(Transform transform, string caption, Action click)
    {
        if (transform == null) return;
        var button = EnsureButton(transform.gameObject);
        if (caption != null)
        {
            var text = transform.GetComponentInChildren<TMP_Text>(true);
            if (text != null) SetText(text, caption);
        }
        button.onClick.AddListener((Action)(() => click()));
    }

    internal static Button EnsureButton(GameObject target)
    {
        var button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Graphic>();
        }
        NoNavigation(button);
        return button;
    }

    // A selected button would take Enter, Space and WASD as Submit or Move and click again.
    private static void NoNavigation(Selectable selectable)
    {
        if (selectable != null) selectable.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static GameObject Vertical(Transform parent, string name, float spacing, RectOffset padding)
    {
        var created = new GameObject(name, Il2CppType.Of<RectTransform>());
        created.transform.SetParent(parent, false);
        var layout = created.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return created;
    }

    // Native graphics keep their size: preferred = min = the given size, never flexible.
    private static LayoutElement Fixed(GameObject target, float width, float height)
    {
        var element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        if (width > 0f) { element.preferredWidth = width; element.minWidth = width; element.flexibleWidth = 0f; }
        if (height > 0f) { element.preferredHeight = height; element.minHeight = height; element.flexibleHeight = 0f; }
        return element;
    }

    private static Transform Require(Transform parent, string path)
        => parent.Find(path) ?? throw new InvalidOperationException("missing part of the options window: " + path);

    private static void ReportOnce(string message)
    {
        if (Reported.Add(message)) DelversHost.Warning(message);
    }

    // DASText resolves text keys itself; turn that off before setting plain text. Characters the
    // game's fonts lack would show as boxes and are replaced.
    internal static void SetText(TMP_Text text, string value)
    {
        if (text == null) return;
        value = FitFont(text, value ?? "");
        var das = text.TryCast<DASText>();
        if (das != null)
        {
            try
            {
                das.SetUseTextKey(false);
                das.SetText(value);
            }
            catch (Exception ex) { ReportOnce("DELVERS_MENU_TEXT_FALLBACK: " + ex.Message); }
        }
        text.text = value;
    }

    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['·'] = "|", ['•'] = "|", ['✓'] = "OK", ['✗'] = "x", ['−'] = "-", ['→'] = "->", ['…'] = "..."
    };
    private static readonly Dictionary<(IntPtr Font, char Glyph), bool> GlyphCache = new();

    private static string FitFont(TMP_Text text, string value)
    {
        StringBuilder result = null;
        var inTag = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            string replacement = null;
            if (!inTag && c > 0x7F && !char.IsSurrogate(c) && !HasGlyph(text, c))
                replacement = Replacements.TryGetValue(c, out var known) ? known : "?";
            if (replacement == null)
            {
                result?.Append(c);
                continue;
            }
            result ??= new StringBuilder(value, 0, i, value.Length + 16);
            result.Append(replacement);
        }
        return result?.ToString() ?? value;
    }

    private static bool HasGlyph(TMP_Text text, char glyph)
    {
        var font = text.font;
        if (font == null) return true;
        var key = (font.Pointer, glyph);
        if (GlyphCache.TryGetValue(key, out var known)) return known;
        bool has;
        try { has = font.HasCharacter(glyph, true, true); }
        catch (Exception) { has = true; }
        GlyphCache[key] = has;
        return has;
    }

    // Unity, TextMeshPro and the game's visual-only components survive cloning; game scripts
    // would act on the clone (the options window would save settings on close).
    private static readonly HashSet<string> KeepTypes = new(StringComparer.Ordinal)
    {
        "UnityEngine.RectTransform", "UnityEngine.CanvasRenderer", "UnityEngine.UI.Image", "UnityEngine.UI.RawImage",
        "UnityEngine.UI.Mask", "UnityEngine.UI.RectMask2D", "UnityEngine.UI.ScrollRect", "UnityEngine.UI.Scrollbar",
        "UnityEngine.UI.VerticalLayoutGroup", "UnityEngine.UI.HorizontalLayoutGroup", "UnityEngine.UI.GridLayoutGroup",
        "UnityEngine.UI.LayoutElement", "UnityEngine.UI.ContentSizeFitter", "UnityEngine.UI.Button", "UnityEngine.UI.Toggle",
        "UnityEngine.UI.Outline", "UnityEngine.UI.Shadow", "UnityEngine.CanvasGroup", "UnityEngine.Canvas",
        "UnityEngine.UI.GraphicRaycaster", "TMPro.TextMeshProUGUI", "TMPro.TMP_SubMeshUI",
        "Refactor.UI.DASText", "Refactor.UI.DASButton", "Refactor.UI.CommonUI.TextButton",
        "Refactor.UI.Util.InteractableButton", "Refactor.UI.ClickBigSoundButton", "Refactor.UI.ClickMiddleSoundButton",
        "Refactor.UI.ClickSmallSoundButton", "Refactor.UIScaleRoot"
    };

    internal static GameObject Clone(Transform source, Transform parent, string name)
    {
        var clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
        clone.name = name;
        foreach (var component in clone.GetComponentsInChildren<Component>(true))
        {
            if (component == null || KeepTypes.Contains(component.GetIl2CppType().FullName)) continue;
            UnityEngine.Object.DestroyImmediate(component);
        }
        // Persistent calls of the original prefab must never fire on a clone.
        foreach (var button in clone.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
        return clone;
    }

    // The window's own parts, taken from the prefab of the options window (GameOptionView); the formation window
    // (TacticalFormationWindow) is built from the same parts.
    internal sealed class Parts
    {
        internal Transform Window;
        internal Transform CategoryButton;
        internal Transform SectionTitle;
        internal Transform LabelBox;
        internal Transform ValueBox;
        internal Transform DropdownRow;
        internal Transform ToggleRow;
        internal Transform BodyText;
        internal Transform Divider;
        internal Sprite CheckOn;
        internal Sprite CheckOff;
        internal Sprite TabSelected;
        internal Sprite TabUnselected;
        internal Sprite DropdownEnd;
        internal CanvasScaler Scaler;

        internal static bool TryHarvest(out Parts parts, out string reason)
        {
            parts = null;
            try
            {
                var option = FindOptionPrefab();
                if (option == null)
                {
                    reason = "prefab of the options window (GameOptionView) not found";
                    return false;
                }
                var result = new Parts { Window = option.transform };
                var missing = new List<string>();
                Transform Need(string path)
                {
                    var found = option.transform.Find(path);
                    if (found == null) missing.Add(path);
                    return found;
                }
                var categories = Need("Panel_GameOptionUI/Panel_OptionCategoryButton");
                result.CategoryButton = categories != null && categories.childCount > 0 ? categories.GetChild(0) : null;
                const string general = "Panel_GameOptionUI/ScrollView_SettingData/Viewport/Content/SubUI_GeneralSetting/";
                result.SectionTitle = Need(general + "Panel_Language/Txt_Title");
                result.LabelBox = Need(general + "Panel_Language/Panel_LanguageLeft");
                result.ValueBox = Need(general + "Panel_Language/Btn_TaskOption");
                result.DropdownRow = Need(general + "Panel_Language/Layout_ProductionSettingList/Btn_Dropdown");
                result.ToggleRow = Need(general + "Panel_ToggleCollectingLog/Toggle");
                result.BodyText = Need(general + "Panel_ToggleCollectingLog/Toggle/Txt_Title");
                result.Divider = Need(general + "Panel_Divider/Panel_Divider_1");
                result.CheckOn = result.ToggleRow?.Find("Img_Check")?.GetComponent<Image>()?.sprite;
                result.CheckOff = Need(general + "Panel_CameraMovementSpeed/Toggle_OnOff/Img_DisableIcon")?.GetComponent<Image>()?.sprite;
                result.TabSelected = option._selectedSprite;
                result.TabUnselected = option._unselectedSprite;
                result.DropdownEnd = FindSprite("List_RightList_End");
                result.Scaler = FindGameScaler();
                if (result.CategoryButton == null) missing.Add("category button");
                if (result.CheckOn == null || result.CheckOff == null) missing.Add("check box sprites");
                if (missing.Count > 0)
                {
                    reason = "missing parts: " + string.Join(", ", missing);
                    return false;
                }
                parts = result;
                reason = "";
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        // Find, never create: a MonoSingleton's Instance getter may build an empty one.
        private static GameOptionView FindOptionPrefab()
        {
            try
            {
                var factory = UnityEngine.Object.FindObjectOfType<UIFactory>();
                var prefab = factory != null && factory._prefabs != null ? factory._prefabs.GetUIPrefab(PopUpType.GameOption) : null;
                var typed = prefab?.TryCast<GameOptionView>();
                if (typed != null) return typed;
            }
            catch (Exception) { /* No factory yet: search the loaded objects below. */ }
            GameOptionView fallback = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameOptionView>())
            {
                if (candidate == null) continue;
                if (!candidate.gameObject.scene.IsValid()) return candidate;
                fallback ??= candidate;
            }
            return fallback;
        }

        private static Sprite FindSprite(string name)
        {
            try
            {
                foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (sprite != null && sprite.name == name) return sprite;
            }
            catch (Exception) { /* Optional: the dropdown then ends without its closing line. */ }
            return null;
        }

        private static CanvasScaler FindGameScaler()
        {
            try
            {
                var parent = UnityEngine.Object.FindObjectOfType<UIFactory>()?._parent;
                var scaler = parent != null ? parent.GetComponentInParent<CanvasScaler>() : null;
                if (scaler != null) return scaler;
            }
            catch (Exception) { /* Fall through to any scene scaler. */ }
            foreach (var scaler in UnityEngine.Object.FindObjectsOfType<CanvasScaler>())
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) return scaler;
            return null;
        }
    }
}

// The menu's own words in the game's language (DelversMenuText); English if one is missing.
internal static class MenuText
{
    internal static string Pick(UniqueCandidateTranslations text)
    {
        LanguageType language;
        try { language = LanguageSetting.GetCurrentLanguage(); }
        catch (Exception) { return text.English; }
        var value = language switch
        {
            LanguageType.Korean => text.Korean,
            LanguageType.French => text.French,
            LanguageType.German => text.German,
            LanguageType.Russian => text.Russian,
            LanguageType.ChineseSimplified => text.ChineseSimplified,
            LanguageType.ChineseTraditional => text.ChineseTraditional,
            LanguageType.Japanese => text.Japanese,
            LanguageType.Spanish => text.Spanish,
            LanguageType.PortugueseBrazil => text.PortugueseBrazil,
            _ => text.English
        };
        return string.IsNullOrWhiteSpace(value) ? text.English : value;
    }

    internal static string Close => Pick(DelversMenuText.Close);
    internal static string PageFailed => Pick(DelversMenuText.PageFailed);
}

// The game sends Escape as EscapeActionRequested to its input handler, which closes the topmost popup, the options window under the
// Delvers menu. While the menu is open the key is the menu's and the game does not see it.
[HarmonyPatch(typeof(InputEventHandler), nameof(InputEventHandler.OnEvent), new[] { typeof(EscapeActionRequested) })]
internal static class DelversMenuEscapePatch
{
    private static bool Prefix()
    {
        try { return !DelversMenuWindow.TakeEscape(); }
        catch (Exception ex)
        {
            DelversHost.Warning("DELVERS_MENU_ESCAPE_FAILED: " + ex.Message);
            return true;
        }
    }
}
