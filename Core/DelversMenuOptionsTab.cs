using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// "Delvers" as the last entry of the game's options window (DelversMenuEntryRules). The game builds its tabs in
// GameOptionView.InitOptionButton (each tab of _gameOptionButtons gets its click) and colours them in UpdateGameOptionButton,
// which GameOptionPresenter.OnActivateView calls each time the window opens. Core adds a clone of the
// first tab (the same part the Delvers window clones) behind the game's tabs, outside the game's dictionary, so the game's tab
// logic never sees it; a click opens the Delvers menu over the options window. Each time the window comes up the entry is checked
// again: usable on the title screen, dimmed and not clickable while a campaign runs, with the hint under its name.
internal static class DelversMenuOptionsTab
{
    private const string Holder = "Panel_GameOptionUI/Panel_OptionCategoryButton";
    private static bool reported;

    internal static void ResetLifecycle() => reported = false;

    internal static void Ensure(GameOptionView view)
    {
        var holder = view != null ? view.transform.Find(Holder) : null;
        if (holder == null || holder.childCount == 0)
        {
            if (!reported) DelversHost.Warning("DELVERS_MENU_TAB_FAILED: the options window has no tab row");
            reported = true;
            return;
        }
        var existing = holder.Find(DelversMenuEntryRules.TabName);
        var tab = existing != null ? existing.gameObject : Create(holder);
        // After every tab of the game, also when the game added one later; never twice (found by name).
        tab.transform.SetAsLastSibling();
        var usable = DelversMenuEntryRules.Usable(CoreGame.Flow?.IsCampaignStarted == true);
        var button = tab.GetComponent<Button>();
        if (button != null) button.interactable = usable;
        var group = tab.GetComponent<CanvasGroup>();
        if (group == null) group = tab.AddComponent<CanvasGroup>();
        group.alpha = usable ? 1f : DelversMenuEntryRules.LockedAlpha;
        var image = tab.GetComponent<Image>();
        if (image != null && view._unselectedSprite != null) image.sprite = view._unselectedSprite;
        var text = tab.GetComponentInChildren<TMP_Text>(true);
        if (text != null) DelversMenuWindow.SetText(text, DelversMenuEntryRules.Label(usable, MenuText.Pick(DelversMenuText.OnlyInTitleMenu)));
        tab.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(holder.GetComponent<RectTransform>());
        if (reported) return;
        reported = true;
        DelversHost.Info($"DELVERS_MENU_TAB_READY index={tab.transform.GetSiblingIndex()} of={holder.childCount} usable={(usable ? 1 : 0)}");
    }

    private static GameObject Create(Transform holder)
    {
        var tab = DelversMenuWindow.Clone(holder.GetChild(0), holder, DelversMenuEntryRules.TabName);
        var button = DelversMenuWindow.EnsureButton(tab);
        button.onClick.AddListener((Action)(() => DelversMenuWindow.Open()));
        return tab;
    }
}

// The options window must come up even if the entry cannot be added.
[HarmonyPatch(typeof(GameOptionView), nameof(GameOptionView.InitOptionButton))]
internal static class DelversMenuOptionsTabInitPatch
{
    private static void Postfix(GameOptionView __instance)
    {
        try { DelversMenuOptionsTab.Ensure(__instance); }
        catch (Exception ex) { DelversHost.Warning("DELVERS_MENU_TAB_FAILED: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(GameOptionView), nameof(GameOptionView.UpdateGameOptionButton))]
internal static class DelversMenuOptionsTabRefreshPatch
{
    private static void Postfix(GameOptionView __instance)
    {
        try { DelversMenuOptionsTab.Ensure(__instance); }
        catch (Exception ex) { DelversHost.Warning("DELVERS_MENU_TAB_FAILED: " + ex.Message); }
    }
}
