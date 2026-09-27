namespace DungeonSettlersDelvers.Core;

internal enum LoadCompletionResult
{
    NotInProgress,
    Trusted,
    Untrusted
}

internal readonly record struct SaveBlockDecision(bool Blocked, string Reason, bool ShouldLog,
    bool ShouldNotify, string Campaign);

// The pure transaction state behind LoadIntegrityGuard. A detected failure
// blocks saving until the game restarts, including later loads in the same
// session, because the failed load may have left partial native state behind.
// Callers serialize access; this class holds no lock of its own.
internal sealed class LoadIntegrityState
{
    internal const string LoadInProgressReason = "campaign load is still in progress";

    private LoadCompletionState completion;
    private bool completionStarted;
    private bool currentLoadFailed;
    private bool saveBlockReported;

    internal bool IsLoadInProgress { get; private set; }
    internal bool IsSaveBlocked { get; private set; }
    internal string FailureSource { get; private set; }
    internal string TargetCampaign { get; private set; }
    internal int LoadSequence { get; private set; }
    internal bool HasObservedCompletionSignal { get; private set; }

    internal bool IsSaveAllowed => !IsLoadInProgress && !IsSaveBlocked;

    internal string SaveBlockReason => IsLoadInProgress
        ? LoadInProgressReason
        : IsSaveBlocked ? LoadIntegrityDiagnostics.BuildSaveBlockedMessage(FailureSource) : null;

    internal void Begin(string campaign)
    {
        IsLoadInProgress = true;
        completion = default;
        completionStarted = false;
        currentLoadFailed = false;
        saveBlockReported = false;
        HasObservedCompletionSignal = false;
        TargetCampaign = campaign;
        LoadSequence++;
    }

    // Returns true only for the first failure of the current load.
    internal bool ReportFailure(string source)
    {
        if (!IsLoadInProgress) return false;
        var first = !currentLoadFailed;
        currentLoadFailed = true;
        IsSaveBlocked = true;
        FailureSource ??= source;
        return first;
    }

    // Returns true exactly once, when both completion signals have been seen.
    internal bool ObserveCompletionSignal(LoadCompletionSignal signal)
    {
        if (!IsLoadInProgress) return false;
        HasObservedCompletionSignal = true;
        completion = completion.Observe(signal);
        if (!completion.IsComplete || completionStarted) return false;
        completionStarted = true;
        return true;
    }

    internal LoadCompletionResult Complete(bool contextReady)
    {
        if (!IsLoadInProgress) return LoadCompletionResult.NotInProgress;
        var trusted = !currentLoadFailed && !IsSaveBlocked && contextReady && completion.IsComplete;
        IsLoadInProgress = false;
        completion = default;
        completionStarted = false;
        return trusted ? LoadCompletionResult.Trusted : LoadCompletionResult.Untrusted;
    }

    // The game abandoned the load (for example its own "load failed" notice)
    // or it never finished. Whatever was deserialized so far is partial.
    internal bool Abort(string source)
    {
        if (!IsLoadInProgress) return false;
        ReportFailure(source);
        Complete(contextReady: false);
        return true;
    }

    internal SaveBlockDecision DecideSave()
    {
        if (IsSaveAllowed) return new SaveBlockDecision(false, null, false, false, TargetCampaign);
        var shouldLog = !saveBlockReported;
        saveBlockReported = true;
        // A save during a healthy load is routine; only a real block is shown to the player.
        return new SaveBlockDecision(true, SaveBlockReason, shouldLog, !IsLoadInProgress, TargetCampaign);
    }

    internal void Reset()
    {
        IsLoadInProgress = false;
        IsSaveBlocked = false;
        completion = default;
        completionStarted = false;
        currentLoadFailed = false;
        saveBlockReported = false;
        HasObservedCompletionSignal = false;
        FailureSource = null;
        TargetCampaign = null;
    }
}
