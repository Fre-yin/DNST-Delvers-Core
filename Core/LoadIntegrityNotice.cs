using System.Collections;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Tells the player about a save block with the game's own UI. After a blocked
// load or a blocked manual save the native confirm dialog explains it and stays
// until confirmed; blocked autosaves only add the one-line help notice, at most
// once per BannerRepeatSeconds. A failed dialog falls back to the banner.
internal static class LoadIntegrityNotice
{
    private const float BannerRepeatSeconds = 30f;
    private const float DialogRepeatSeconds = 10f;
    private static readonly object Gate = new();
    private static bool scheduled;
    private static bool dialogRequested;
    private static float lastBanner = float.NegativeInfinity;
    private static float lastDialog = float.NegativeInfinity;
    private static object routine;
    private static Il2CppSystem.Action closeDialog;

    internal static void RequestDialog(float delaySeconds) => Request(true, delaySeconds);

    internal static void RequestBanner(float delaySeconds) => Request(false, delaySeconds);

    internal static void ResetLifecycle()
    {
        object running;
        lock (Gate)
        {
            running = routine;
            routine = null;
            scheduled = false;
            dialogRequested = false;
            lastBanner = float.NegativeInfinity;
            lastDialog = float.NegativeInfinity;
        }
        if (running == null) return;
        try { DelversHost.StopCoroutine(running); }
        catch (Exception ex) { DelversHost.Warning("CORE_SAVE_BLOCKED_NOTICE_STOP_FAILED: " + ex.Message); }
    }

    private static void Request(bool dialog, float delaySeconds)
    {
        lock (Gate)
        {
            dialogRequested |= dialog;
            if (scheduled) return;
            scheduled = true;
        }
        try
        {
            var started = DelversHost.StartCoroutine(ShowAfter(delaySeconds));
            lock (Gate) if (scheduled) routine = started;
        }
        catch (Exception ex)
        {
            lock (Gate) scheduled = false;
            DelversHost.Warning("CORE_SAVE_BLOCKED_NOTICE_FAILED: " + ex.Message);
        }
    }

    private static IEnumerator ShowAfter(float delaySeconds)
    {
        var due = Time.realtimeSinceStartup + delaySeconds;
        while (Time.realtimeSinceStartup < due) yield return null;
        bool dialog;
        lock (Gate)
        {
            dialog = dialogRequested;
            dialogRequested = false;
        }
        try
        {
            var now = Time.realtimeSinceStartup;
            if (dialog && now - lastDialog >= DialogRepeatSeconds)
            {
                if (TryShowDialog()) lastDialog = now;
                else if (TryShowBanner()) lastBanner = now;
            }
            else if (!dialog && now - lastBanner >= BannerRepeatSeconds && TryShowBanner())
                lastBanner = now;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SAVE_BLOCKED_NOTICE_FAILED: " + ex.Message);
        }
        finally
        {
            lock (Gate)
            {
                scheduled = false;
                routine = null;
            }
        }
    }

    private static bool TryShowDialog()
    {
        var dispatcher = UnityEngine.Object.FindObjectOfType<MainFlow>()?._viewDispatcher;
        if (dispatcher == null) return Deferred("dialog");
        try
        {
            dispatcher.DispatchViewUpdateImmediate(CreateDialog().Cast<IViewUpdateData>());
            DelversHost.Info("CORE_SAVE_BLOCKED_DIALOG_SHOWN");
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SAVE_BLOCKED_DIALOG_FAILED fallback=banner: " + ex.Message);
            return false;
        }
    }

    private static bool TryShowBanner()
    {
        var flow = UnityEngine.Object.FindObjectOfType<MainFlow>();
        var handlers = flow?._tickFlowHandlers;
        var map = flow != null ? flow.CurrentMap : default;
        var handler = handlers != null && handlers.ContainsKey(map)
            ? handlers[map]?.TryCast<TickFlowHandler>() : null;
        if (handler == null) return Deferred("banner");

        handler.EnqueueEvent(CreateBanner().Cast<IEventData>(), true);
        DelversHost.Info("CORE_SAVE_BLOCKED_NOTICE_SHOWN map=" + map);
        return true;
    }

    // The in-game self-test builds both notices without showing them.
    internal static ViewCallbackData CreateDialog()
    {
        UniqueCandidateLocalization.EnsureCurrent();
        closeDialog ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => { }));
        return new ViewCallbackData(UpdateType.CommonNotice_ShowConfirm,
            LoadIntegrityNoticeText.SaveBlockedKey, closeDialog, closeDialog);
    }

    internal static DataApplyRequested CreateBanner()
    {
        UniqueCandidateLocalization.EnsureCurrent();
        var notice = new HelpNoticeApplyData(LoadIntegrityNoticeText.SaveBlockedShortKey,
            new Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object>());
        return new DataApplyRequested(notice.Cast<IApplyData>());
    }

    // Title screen or map transition: the next blocked save or load asks again.
    private static bool Deferred(string kind)
    {
        DelversHost.Info("CORE_SAVE_BLOCKED_NOTICE_DEFERRED kind=" + kind + " reason=no-active-campaign-ui");
        return false;
    }
}
