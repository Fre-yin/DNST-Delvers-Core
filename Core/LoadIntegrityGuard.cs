using System.Collections;
using HarmonyLib;
#if BEPINEX
using global::Refactor;
using global::Refactor.Main;
using global::Refactor.Main.Event;
using global::Refactor.Map;
#else
using Il2CppRefactor;
using Il2CppRefactor.Main;
using Il2CppRefactor.Main.Event;
using Il2CppRefactor.Map;
#endif
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Tracks the destructive part of a native campaign-load transaction. The
// transaction starts only when CampaignDataContainer begins deserializing, so
// the game's optional pre-load autosave remains available. A load is trusted
// only after FinalizeCampaignLoad completed and the event queue reached its
// completion notification. The transaction rules live in LoadIntegrityState;
// this class adds locking, logging, the completion watchdog and the notice.
internal static class LoadIntegrityGuard
{
    // FinalizeCampaignLoad raises the completion event within the same call, so
    // a long gap after the first signal means the second one will not arrive.
    private const float SignalGapTimeoutSeconds = 30f;
    private const float LoadTimeoutSeconds = 30f * 60f;
    private const float NoticeDelayAfterLoadSeconds = 5f;
    private static readonly object Gate = new();
    private static readonly LoadIntegrityState State = new();
    private static CampaignDataContainer campaignContext;
    private static object watchdog;

    internal static bool IsLoadInProgress
    {
        get { lock (Gate) return State.IsLoadInProgress; }
    }

    internal static bool IsSaveAllowed
    {
        get { lock (Gate) return State.IsSaveAllowed; }
    }

    internal static string SaveBlockReason
    {
        get { lock (Gate) return State.SaveBlockReason; }
    }

    internal static void Begin(CampaignDataContainer campaign, CampaignSaveData saveData)
    {
        var campaignId = DescribeCampaign(saveData);
        int sequence;
        lock (Gate)
        {
            State.Begin(campaignId);
            sequence = State.LoadSequence;
            campaignContext = campaign;
        }
        DelversHost.Info("CORE_LOAD_INTEGRITY_BEGIN campaign=" + Safe(campaignId));
        StartWatchdog(sequence);
    }

    internal static void MarkNativeFinalizeCompleted()
        => ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);

    internal static void ReportFailure(string source, Exception exception)
    {
        string campaign;
        bool firstFailure;
        lock (Gate)
        {
            firstFailure = State.ReportFailure(source);
            campaign = State.TargetCampaign;
        }
        if (!firstFailure) return;
        LogFailure(source, campaign, exception);
    }

    internal static void OnEventCompleted()
        => ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);

    // The game's own "load failed" notice ends a load that will never complete.
    internal static void Abort(string source)
        => AbortLoad(source, new InvalidOperationException("The game abandoned the campaign load."), -1);

    private static void ObserveCompletionSignal(LoadCompletionSignal signal)
    {
        CampaignDataContainer campaign;
        lock (Gate)
        {
            if (!State.ObserveCompletionSignal(signal)) return;
            campaign = campaignContext;
        }
        if (campaign == null)
        {
            ReportFailure("campaign-context-missing",
                new InvalidOperationException("The completed load did not retain its campaign data container."));
            Complete(contextReady: false);
            return;
        }

        DelversCoreRuntime.NotifyAfterCampaignLoaded(campaign);
        Complete(contextReady: true);
    }

    // A blocked manual save gets the dialog; blocked autosaves only the banner,
    // since they repeat and the exit autosave closes the game right after.
    internal static bool TryBlockSave(bool isAutoSave, out string reason)
    {
        SaveBlockDecision decision;
        lock (Gate) decision = State.DecideSave();
        reason = decision.Reason;
        if (!decision.Blocked) return false;
        if (decision.ShouldLog)
            DelversHost.Error("CORE_SAVE_BLOCKED " + reason
                + " campaign=" + Safe(decision.Campaign)
                + (decision.ShouldNotify ? " action=restart-game-before-saving" : " action=retry-after-load"));
        if (decision.ShouldNotify)
        {
            if (isAutoSave) LoadIntegrityNotice.RequestBanner(0f);
            else LoadIntegrityNotice.RequestDialog(0f);
        }
        return true;
    }

    internal static Exception ObserveFinalizer(Exception exception, string source)
    {
        if (exception != null) ReportFailure(source, exception);
        return exception;
    }

    internal static void ResetLifecycle()
    {
        object running;
        lock (Gate)
        {
            State.Reset();
            campaignContext = null;
            running = watchdog;
            watchdog = null;
        }
        StopRoutine(running);
        LoadIntegrityNotice.ResetLifecycle();
    }

    private static void Complete(bool contextReady)
    {
        LoadCompletionResult result;
        bool blocked;
        string campaign;
        lock (Gate)
        {
            result = State.Complete(contextReady);
            if (result == LoadCompletionResult.NotInProgress) return;
            blocked = State.IsSaveBlocked;
            campaign = State.TargetCampaign;
            campaignContext = null;
        }
        DelversHost.Info("CORE_LOAD_INTEGRITY_COMPLETE campaign=" + Safe(campaign)
            + " trusted=" + (result == LoadCompletionResult.Trusted ? "true" : "false")
            + " saveBlocked=" + blocked.ToString().ToLowerInvariant());
        if (blocked) LoadIntegrityNotice.RequestDialog(NoticeDelayAfterLoadSeconds);
    }

    // expectedSequence < 0 aborts whatever load is running.
    private static void AbortLoad(string source, Exception reason, int expectedSequence)
    {
        bool firstFailure;
        bool aborted;
        string campaign;
        lock (Gate)
        {
            if (expectedSequence >= 0 && State.LoadSequence != expectedSequence) return;
            firstFailure = State.ReportFailure(source);
            aborted = State.Abort(source);
            campaign = State.TargetCampaign;
            if (aborted) campaignContext = null;
        }
        if (!aborted) return;
        if (firstFailure) LogFailure(source, campaign, reason);
        DelversHost.Error("CORE_LOAD_INTEGRITY_ABORTED campaign=" + Safe(campaign)
            + " source=" + Safe(source) + " saveBlocked=true");
        LoadIntegrityNotice.RequestDialog(NoticeDelayAfterLoadSeconds);
    }

    private static void LogFailure(string source, string campaign, Exception exception)
        => DelversHost.Error("CORE_LOAD_INTEGRITY_FAILED "
            + LoadIntegrityDiagnostics.BuildFailureMessage(source)
            + " source=" + Safe(source)
            + " campaign=" + Safe(campaign)
            + " saveBlocked=true error=" + exception);

    private static void StartWatchdog(int sequence)
    {
        object previous;
        lock (Gate)
        {
            previous = watchdog;
            watchdog = null;
        }
        StopRoutine(previous);
        try
        {
            var started = DelversHost.StartCoroutine(WatchCompletion(sequence));
            lock (Gate) watchdog = started;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_LOAD_WATCHDOG_UNAVAILABLE: " + ex.Message);
        }
    }

    private static IEnumerator WatchCompletion(int sequence)
    {
        var begun = Time.realtimeSinceStartup;
        var firstSignal = -1f;
        while (true)
        {
            yield return null;
            bool current;
            bool signalSeen;
            lock (Gate)
            {
                current = State.LoadSequence == sequence && State.IsLoadInProgress;
                signalSeen = State.HasObservedCompletionSignal;
            }
            if (!current) yield break;

            var now = Time.realtimeSinceStartup;
            if (signalSeen && firstSignal < 0) firstSignal = now;
            var timedOut = firstSignal >= 0
                ? now - firstSignal > SignalGapTimeoutSeconds
                : now - begun > LoadTimeoutSeconds;
            if (!timedOut) continue;

            DelversHost.Error("CORE_LOAD_COMPLETION_TIMEOUT sequence=" + sequence
                + " completionSignalSeen=" + signalSeen.ToString().ToLowerInvariant());
            AbortLoad("load-completion-timeout",
                new TimeoutException("The campaign load did not deliver both completion signals."), sequence);
            yield break;
        }
    }

    private static void StopRoutine(object routine)
    {
        if (routine == null) return;
        try { DelversHost.StopCoroutine(routine); }
        catch (Exception ex) { DelversHost.Warning("CORE_LOAD_WATCHDOG_STOP_FAILED: " + ex.Message); }
    }

    private static string DescribeCampaign(CampaignSaveData saveData)
    {
        try
        {
            var guid = saveData.ClanSaveData.CampaignGuid;
            return guid == Il2CppSystem.Guid.Empty ? "unknown" : guid.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private static string Safe(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace(' ', '_');
}

[HarmonyPatch(typeof(CampaignDataContainer), nameof(CampaignDataContainer.Deserialize),
    new[] { typeof(CampaignSaveData) })]
internal static class DelversCampaignDeserializeGuardPatch
{
    private static void Prefix(CampaignDataContainer __instance, CampaignSaveData __0)
    {
        LoadIntegrityGuard.Begin(__instance, __0);
        DelversCoreRuntime.AuditLoadPatches();
    }

    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "CampaignDataContainer.Deserialize");
}

[HarmonyPatch(typeof(CampaignLoadingEventHandler), "FinalizeCampaignLoad",
    new[] { typeof(MapType) })]
internal static class DelversCampaignLoadFinalizedPatch
{
    private static void Postfix() => LoadIntegrityGuard.MarkNativeFinalizeCompleted();

    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception,
            "CampaignLoadingEventHandler.FinalizeCampaignLoad");
}

[HarmonyPatch(typeof(SystemEventHandler), nameof(SystemEventHandler.OnEvent),
    new[] { typeof(CampaignEventCompletedRequested) })]
internal static class DelversCampaignLoadCompletionPatch
{
    private static void Postfix() => LoadIntegrityGuard.OnEventCompleted();

    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception,
            "SystemEventHandler.OnEvent(CampaignEventCompletedRequested)");
}

// The game shows this notice when it gives up on a load after deserialization
// may already have started; the transaction would otherwise stay open.
[HarmonyPatch(typeof(CampaignLoadingEventHandler), "ShowCampaignLoadFailedNotice")]
internal static class DelversCampaignLoadFailedPatch
{
    private static void Prefix()
    {
        try { LoadIntegrityGuard.Abort("CampaignLoadingEventHandler.ShowCampaignLoadFailedNotice"); }
        catch { /* The native failure notice must still be shown. */ }
    }
}

[HarmonyPatch(typeof(CampaignSaveLoader), nameof(CampaignSaveLoader.Save),
    new[] { typeof(string), typeof(bool), typeof(string) })]
internal static class DelversCampaignSaveGuardPatch
{
    private static bool Prefix(bool __1, ref bool __result)
    {
        if (!LoadIntegrityGuard.TryBlockSave(__1, out _)) return true;
        __result = false;
        return false;
    }
}
