using DungeonSettlersDelvers.Core;
using static TestKit;

// Load integrity: destructive load signals, notice texts, save block state and completion.
internal static class LoadIntegritySection
{
    internal static void Run()
    {
        DeserializationSignals();
        NoticeTexts();
        SaveBlockState();
        Diagnostics();
        CompletionState();
    }

    static void DeserializationSignals()
    {
        Check(LoadIntegritySignalRules.IsDeserializationFailure(
                "[ComponentList] Failed to deserialize component. Type=Affecter", true),
            "component deserialization failures are recognized as destructive load signals");
        Check(LoadIntegritySignalRules.IsDeserializationFailure(
                "CampaignDataContainer: Failed to deserialize section EntityContainerSaveData", true),
            "section deserialization failures are recognized as destructive load signals");
        Check(LoadIntegritySignalRules.IsDeserializationFailure(
                "FAILED TO DESERIALIZE COMPONENT", true),
            "destructive load signal matching is case insensitive");
        Check(!LoadIntegritySignalRules.IsDeserializationFailure(
                "A pack callback failed after the campaign loaded", true),
            "unrelated errors do not trigger the native deserialization signal");
        Check(!LoadIntegritySignalRules.IsDeserializationFailure(null, true),
            "empty native log messages do not trigger the load guard");
        Check(!LoadIntegritySignalRules.IsDeserializationFailure(
                "[OtherMod] Failed to deserialize component config, using defaults", false),
            "informational log lines with the same words do not trigger the load guard");
    }

    static void NoticeTexts()
    {
        Check(LoadIntegrityNoticeText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text =>
                    !string.IsNullOrWhiteSpace(text) && text.StartsWith("Delvers Core", StringComparison.Ordinal))),
            "the in-game save-block dialog and banner have a Delvers Core text for every game language");
        Check(LoadIntegrityNoticeText.All.All(entry =>
                    entry.Key.StartsWith("TEXTKEY_DelversCore_", StringComparison.Ordinal))
                && LoadIntegrityNoticeText.All.Select(entry => entry.Key).Distinct().Count() == 2,
            "the save-block notices use distinct Core-owned text keys");
        Check(LoadIntegrityNoticeText.SaveBlockedShort.AllLanguages.All(text => text.Length <= 90),
            "the save-block banner stays short enough for the one-line help notice");
        Check(LoadIntegrityNoticeText.All.SelectMany(entry => entry.Text.AllLanguages)
                .All(text => !text.Contains('\u2013') && !text.Contains('\u2014')),
            "the save-block notices use natural sentences without en or em dashes");
    }

    static void SaveBlockState()
    {
        var healthyLoad = new LoadIntegrityState();
        Check(healthyLoad.IsSaveAllowed && healthyLoad.SaveBlockReason == null,
            "a fresh session allows saving");
        healthyLoad.Begin("campaign-a");
        var duringLoad = healthyLoad.DecideSave();
        Check(duringLoad.Blocked && !duringLoad.ShouldNotify
                && duringLoad.Reason == LoadIntegrityState.LoadInProgressReason,
            "a save during a running load is held back without a player notice");
        Check(!healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted)
                && healthyLoad.HasObservedCompletionSignal,
            "the first completion signal is recorded but does not complete the load");
        Check(healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted)
                && !healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted),
            "both completion signals start completion exactly once");
        Check(healthyLoad.Complete(contextReady: true) == LoadCompletionResult.Trusted
                && healthyLoad.IsSaveAllowed && !healthyLoad.DecideSave().Blocked,
            "a load with both signals and no failure is trusted and allows saving");
        Check(healthyLoad.Complete(contextReady: true) == LoadCompletionResult.NotInProgress,
            "completing twice is harmless");
        Check(!healthyLoad.ReportFailure("late-error") && healthyLoad.IsSaveAllowed,
            "a failure reported outside a load does not block saving");
        var failedLoad = new LoadIntegrityState();
        failedLoad.Begin("campaign-b");
        Check(failedLoad.ReportFailure("native-deserialization-log")
                && !failedLoad.ReportFailure("second-source"),
            "only the first failure of a load is reported");
        failedLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
        failedLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
        Check(failedLoad.Complete(contextReady: true) == LoadCompletionResult.Untrusted,
            "a load with a detected failure completes untrusted");
        var firstBlockedSave = failedLoad.DecideSave();
        var secondBlockedSave = failedLoad.DecideSave();
        Check(firstBlockedSave.Blocked && firstBlockedSave.ShouldLog && firstBlockedSave.ShouldNotify
                && secondBlockedSave.Blocked && !secondBlockedSave.ShouldLog && secondBlockedSave.ShouldNotify,
            "every blocked save notifies the player while the log entry is written once per load");
        Check(firstBlockedSave.Reason == LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native-deserialization-log"),
            "the save-block reason names the first detection source");
        failedLoad.Begin("campaign-c");
        failedLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
        failedLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
        Check(failedLoad.Complete(contextReady: true) == LoadCompletionResult.Untrusted
                && failedLoad.DecideSave().Blocked && failedLoad.FailureSource == "native-deserialization-log",
            "a clean later load in the same session stays blocked until restart");
        var missingContext = new LoadIntegrityState();
        missingContext.Begin("campaign-d");
        missingContext.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
        missingContext.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
        Check(missingContext.Complete(contextReady: false) == LoadCompletionResult.Untrusted,
            "a completed load without its campaign context is not trusted");
        var abortedLoad = new LoadIntegrityState();
        Check(!abortedLoad.Abort("CampaignLoadingEventHandler.ShowCampaignLoadFailedNotice")
                && abortedLoad.IsSaveAllowed,
            "the native load-failed notice outside a load changes nothing");
        abortedLoad.Begin("campaign-e");
        var abortSequence = abortedLoad.LoadSequence;
        Check(abortedLoad.Abort("load-completion-timeout") && !abortedLoad.IsLoadInProgress
                && abortedLoad.IsSaveBlocked && abortedLoad.FailureSource == "load-completion-timeout",
            "an abandoned or timed-out load ends the transaction and blocks saving");
        abortedLoad.Begin("campaign-f");
        Check(abortedLoad.LoadSequence == abortSequence + 1,
            "every load gets its own sequence so a stale watchdog cannot abort a newer load");
        abortedLoad.Reset();
        Check(abortedLoad.IsSaveAllowed && abortedLoad.FailureSource == null && !abortedLoad.IsLoadInProgress,
            "a lifecycle reset clears the block");
    }

    static void Diagnostics()
    {
        Check(LoadIntegrityDiagnostics.FailureNotice ==
                "Delvers Core detected mod data or an incompatible mod version during this load. "
                + "Saving is blocked to protect this save. "
                + "This is not an error in the native Dungeon Settlers patch. "
                + "Report the involved mod to its author, not to the game developers.",
            "load failure notice attributes the diagnosis and protection to Core and directs reports to mod authors");
        Check(LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native-deserialization-log") ==
                LoadIntegrityDiagnostics.FailureNotice
                + " Restart the game before saving again. Detection source: native-deserialization-log.",
            "save-block message explains protection, restart recovery and detection source");
        Check(LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native\r\n deserialization-log")
                .EndsWith("Detection source: native deserialization-log.", StringComparison.Ordinal),
            "line breaks in a detection source are normalized in the save-block message");
    }

    static void CompletionState()
    {
        var eventFirstCompletion = default(LoadCompletionState)
            .Observe(LoadCompletionSignal.CampaignEventCompleted);
        Check(!eventFirstCompletion.IsComplete,
            "campaign completion event alone keeps the load transaction incomplete");
        eventFirstCompletion = eventFirstCompletion.Observe(LoadCompletionSignal.NativeFinalizeCompleted);
        Check(eventFirstCompletion.IsComplete,
            "campaign completion event followed by native finalize completes the load transaction");
        var finalizeFirstCompletion = default(LoadCompletionState)
            .Observe(LoadCompletionSignal.NativeFinalizeCompleted);
        Check(!finalizeFirstCompletion.IsComplete,
            "native finalize alone keeps the load transaction incomplete");
        finalizeFirstCompletion = finalizeFirstCompletion.Observe(LoadCompletionSignal.CampaignEventCompleted);
        Check(finalizeFirstCompletion.IsComplete,
            "native finalize followed by campaign completion event completes the load transaction");
        var duplicateEventCompletion = default(LoadCompletionState)
            .Observe(LoadCompletionSignal.CampaignEventCompleted)
            .Observe(LoadCompletionSignal.CampaignEventCompleted);
        Check(!duplicateEventCompletion.IsComplete,
            "duplicate campaign completion events cannot replace the missing native finalize signal");
        var duplicateFinalizeCompletion = default(LoadCompletionState)
            .Observe(LoadCompletionSignal.NativeFinalizeCompleted)
            .Observe(LoadCompletionSignal.NativeFinalizeCompleted);
        Check(!duplicateFinalizeCompletion.IsComplete,
            "duplicate native finalize callbacks cannot replace the missing campaign completion event");
    }
}
