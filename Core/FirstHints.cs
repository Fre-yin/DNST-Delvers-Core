using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// First-time hints: the first time a Core rule shows up in play (a wound, no
// pause in combat, instant combat, enemy abilities, a fallen party member while the revive scroll is
// offered), the game's own guide window (PopUpType.Tutorial) explains it. Requests wait in a queue;
// Tick shows at most one hint per SpacingSeconds of game time. A shown hint is remembered in Core's
// settings (hints.shown.<hint>), so each comes only once, across all campaigns. The menu
// switch (hints.enabled) turns them off; the Toolkit can switch them for the session only
// (SetForTest). The texts are in FirstHintText.
internal static class FirstHints
{
    private const double SpacingSeconds = 10.0;

    private static readonly Queue<string> pending = new();
    private static readonly HashSet<string> queued = new();
    private static long lastShownTick = long.MinValue;
    private static bool? testOverride;

    internal static bool Enabled => FirstHintText.EffectiveEnabled(testOverride, CoreSettings.HintsEnabled);

    internal static void SetForTest(bool on)
    {
        testOverride = on;
        pending.Clear();
        queued.Clear();
        DelversHost.Info("CORE_TEST_HINTS enabled=" + (on ? "true" : "false") + " scope=memory");
    }

    // Lifts the Toolkit's session override; the menu switch decides again (the
    // override hid the menu switch for the rest of the session).
    internal static void ClearTest()
    {
        testOverride = null;
        DelversHost.Info("CORE_TEST_HINTS override=cleared menu=" + (CoreSettings.HintsEnabled ? "true" : "false"));
    }

    internal static void ResetLifecycle()
    {
        testOverride = null;
        ResetCampaign();
    }

    internal static void ResetCampaign()
    {
        pending.Clear();
        queued.Clear();
        lastShownTick = long.MinValue;
    }

    internal static void Request(string hint)
    {
        if (Enabled && !CoreSettings.HintShown(hint) && queued.Add(hint)) pending.Enqueue(hint);
    }

    // Core's clock (ReviveCampaign): asks for the revive hint itself and shows the next waiting hint.
    internal static void Tick()
    {
        if (!Enabled) return;
        if (CoreSettings.ReviveScrollOffered && !CoreSettings.HintShown(FirstHintText.Revive) && AnyPartyMemberDead())
            Request(FirstHintText.Revive);
        if (pending.Count == 0) return;
        var now = GameTick();
        if (lastShownTick != long.MinValue && (now - lastShownTick) / WoundRules.TicksPerSecond < SpacingSeconds) return;
        var hint = pending.Dequeue();
        queued.Remove(hint);
        if (CoreSettings.HintShown(hint) || !Show(hint, "first")) return;
        CoreSettings.MarkHintShown(hint);
        lastShownTick = now;
    }

    // The game's guide window with the hint's page, the way the game opens its own tutorials. Core's
    // rows are made sure to stand in the game's text table, in the current language, first.
    internal static bool Show(string hint, string reason)
    {
        var flow = CoreGame.Flow;
        var popUps = flow?._eventHandler?.InputEventHandler?._popUpLoader;
        var dispatcher = flow?._viewDispatcher;
        if (popUps == null || dispatcher == null) return false;
        UniqueCandidateLocalization.EnsureCurrent();
        popUps.EnqueueActivatePool(PopUpType.Tutorial);
        dispatcher.AddViewUpdateCall(new ViewStringBoolData(UpdateType.Tutorial_Activate, hint, true).Cast<IViewUpdateData>());
        var campaign = CoreSaveStore.Current?.Campaign ?? Guid.Empty;
        DelversHost.Info($"CORE_HINT_SHOWN key={hint} campaign={campaign:D} reason={reason}");
        return true;
    }

    internal static string Status()
    {
        var shown = new List<string>();
        foreach (var hint in FirstHintText.Keys)
            if (CoreSettings.HintShown(hint)) shown.Add(hint);
        return "enabled=" + (Enabled ? "true" : "false")
            + " test=" + (testOverride switch { null => "-", false => "off", true => "on" })
            + " shown=" + string.Join(",", shown)
            + " pending=" + string.Join(",", pending);
    }

    // For the Toolkit: every hint counts as not shown again.
    internal static void ForgetAll()
    {
        foreach (var hint in FirstHintText.Keys) CoreSettings.ForgetHint(hint);
        pending.Clear();
        queued.Clear();
        lastShownTick = long.MinValue;
    }

    private static bool AnyPartyMemberDead()
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return false;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(true);
            for (var i = 0; units != null && i < units.Count; i++)
                if (units[i] != null && units[i].IsDead) return true;
        }
        return false;
    }

    private static long GameTick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}

// The picture of a hint's page and of a Delvers guide topic: one of the game's own guide pictures (FirstHintText.ImageKey,
// GuideTopicText.ImageKey), or the topic's own picture when the package brings it (CoreAssets.GuideImage).
[HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.LoadGameGuideImage))]
internal static class FirstHintImagePatch
{
    private static void Prefix(ref string key, out string __state)
    {
        __state = key;
        var image = FirstHintText.ImageKey(key) ?? GuideTopicText.ImageKey(key);
        if (image != null) key = image;
    }

    private static void Postfix(string __state, ref Sprite __result)
    {
        try
        {
            if (GuideTopicText.IdOf(__state) is int id && CoreAssets.GuideImage(id) is Sprite own) __result = own;
        }
        catch (Exception ex) { ApocalypseHooks.Report("guide-image", ex); }
    }
}
