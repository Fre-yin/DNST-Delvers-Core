namespace DungeonSettlersDelvers.Core;

internal enum LoadCompletionSignal
{
    NativeFinalizeCompleted,
    CampaignEventCompleted
}

// The native completion event can be observed before Harmony reaches the
// FinalizeCampaignLoad postfix. Treat both callbacks as unordered success
// signals and trust the load only after both have been observed.
internal readonly struct LoadCompletionState
{
    private readonly bool nativeFinalizeCompleted;
    private readonly bool campaignEventCompleted;

    private LoadCompletionState(bool nativeFinalizeCompleted, bool campaignEventCompleted)
    {
        this.nativeFinalizeCompleted = nativeFinalizeCompleted;
        this.campaignEventCompleted = campaignEventCompleted;
    }

    internal bool IsComplete => nativeFinalizeCompleted && campaignEventCompleted;

    internal LoadCompletionState Observe(LoadCompletionSignal signal)
        => signal switch
        {
            LoadCompletionSignal.NativeFinalizeCompleted =>
                new LoadCompletionState(true, campaignEventCompleted),
            LoadCompletionSignal.CampaignEventCompleted =>
                new LoadCompletionState(nativeFinalizeCompleted, true),
            _ => this
        };
}

// The game catches these failures internally and writes them to ErrorLog.txt,
// so Harmony finalizers cannot observe them. Keep the matching narrow to the
// two proven destructive campaign-load diagnostics. The game logs both as
// errors; the same words in an informational line of another mod do not count.
internal static class LoadIntegritySignalRules
{
    internal static bool IsDeserializationFailure(string condition, bool isErrorLevel)
    {
        if (!isErrorLevel || string.IsNullOrWhiteSpace(condition)) return false;
        return condition.IndexOf("Failed to deserialize component",
                   StringComparison.OrdinalIgnoreCase) >= 0
            || condition.IndexOf("Failed to deserialize section",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
