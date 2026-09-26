namespace DungeonSettlersDelvers.Core;

internal enum UniqueCandidateRerollAction
{
    KeepNative,
    ApplyCustomLock,
    ApplyLockConflictText,
    RestoreNativeState,
    DiscardStaleCustomState
}

internal enum UniqueCandidateLockPolicy
{
    None,
    // Native founders: a locked portrait alone still rerolls their other start traits.
    // Their primary traits hold the story-preset background, which native generation
    // can never roll, so a primary-trait lock blocks before the reroll fails.
    PrimaryTraits,
    // Pack uniques carry a unique trait that cannot be rerolled, so either lock blocks.
    ProfileOrPrimaryTraits
}

// This policy stays pure so lock combinations and UI transitions can be
// validated without starting Unity or IL2CPP.
internal static class UniqueCandidateRerollDecision
{
    internal static bool ShouldApplyCustomLock(UniqueCandidateLockPolicy policy, bool profileLocked,
        bool primaryTraitsLocked, bool generationFailed = false)
        => !generationFailed && policy switch
        {
            UniqueCandidateLockPolicy.PrimaryTraits => primaryTraitsLocked,
            UniqueCandidateLockPolicy.ProfileOrPrimaryTraits => profileLocked || primaryTraitsLocked,
            _ => false
        };

    // Native generation failure only swaps the button text and keeps the button
    // active. For unique candidates the failure is caused by their fixed identity,
    // so the button is disabled as well: every blocked unique reroll looks the same.
    internal static bool ShouldDisableAfterGenerationFailure(UniqueCandidateLockPolicy policy, bool generationFailed)
        => generationFailed && policy != UniqueCandidateLockPolicy.None;

    internal static UniqueCandidateRerollAction Evaluate(UniqueCandidateLockPolicy policy, bool profileLocked,
        bool primaryTraitsLocked, bool generationFailed, bool nativeButtonInteractable,
        bool presentationApplied)
    {
        if (generationFailed)
            return presentationApplied
                ? UniqueCandidateRerollAction.DiscardStaleCustomState
                : UniqueCandidateRerollAction.KeepNative;

        var isUniqueCandidate = policy != UniqueCandidateLockPolicy.None;
        var customLockApplies = ShouldApplyCustomLock(policy, profileLocked, primaryTraitsLocked);
        var shouldDisableButton = customLockApplies && nativeButtonInteractable;
        var shouldExplainLockConflict = isUniqueCandidate && (customLockApplies || !nativeButtonInteractable);
        if (presentationApplied)
            return shouldDisableButton || shouldExplainLockConflict
                ? UniqueCandidateRerollAction.KeepNative
                : UniqueCandidateRerollAction.RestoreNativeState;

        if (shouldDisableButton) return UniqueCandidateRerollAction.ApplyCustomLock;
        if (shouldExplainLockConflict) return UniqueCandidateRerollAction.ApplyLockConflictText;
        return UniqueCandidateRerollAction.KeepNative;
    }
}
