namespace DungeonSettlersDelvers.Core;

// The two game objects the reroll rule works on, behind small interfaces so the rule runs
// without Unity or IL2CPP: the recruitment panel and the button with its label.
internal interface IUniqueRerollSurface
{
    // The button and its label can be destroyed while a panel pool is cleared.
    bool ButtonAlive { get; }
    bool TextAlive { get; }
    bool Interactable { get; set; }
    string Text { get; set; }
    // The label colour; the rule only stores it and hands it back.
    object TextStyle { get; set; }
}

internal interface IUniqueRerollPanel
{
    // The native "generation failed" flag; reads as failed when the panel cannot be read.
    bool GenerationFailed { get; }
    // The native button state as the panel sets it; may throw when the button cannot be read.
    bool NativeInteractable { get; }
    // Button and label together, or null when either cannot be reached.
    IUniqueRerollSurface PresentationSurface { get; }
    // The button alone, or null when it cannot be reached.
    IUniqueRerollSurface FailureSurface { get; }
    string LockConflictText();
    void Warn(string message);
}

// What the unique-candidate patches do to one recruitment panel's reroll button, as plain state:
// which lock applies, the snapshot taken before Core changes the button, the button disable after
// a native generation failure, and when each of them is taken back. Evaluate and the lock rules
// stay in UniqueCandidateRerollDecision.
internal sealed class UniqueCandidateRerollState
{
    private readonly IUniqueRerollPanel panel;
    private IUniqueRerollSurface presentationSurface;
    private bool originalInteractable;
    private string originalText;
    private object originalStyle;
    private IUniqueRerollSurface failureSurface;
    private bool failureOriginalInteractable;

    internal UniqueCandidateRerollState(IUniqueRerollPanel panel, UniqueCandidateLockPolicy lockPolicy,
        bool profileLocked, bool primaryTraitsLocked, bool nativeGenerationFailed)
    {
        this.panel = panel;
        LockPolicy = lockPolicy;
        ProfileLocked = profileLocked;
        PrimaryTraitsLocked = primaryTraitsLocked;
        NativeGenerationFailed = nativeGenerationFailed;
    }

    internal UniqueCandidateLockPolicy LockPolicy { get; }
    internal bool ProfileLocked { get; set; }
    internal bool PrimaryTraitsLocked { get; set; }
    internal bool NativeGenerationFailed { get; set; }
    // True while the button shows Core's lock-conflict text and a snapshot of the native state exists.
    internal bool PresentationApplied { get; private set; }
    internal bool FailureDisableApplied { get; private set; }

    // Brings the button in line with the current locks and returns the action that was taken.
    internal UniqueCandidateRerollAction Reconcile()
    {
        NativeGenerationFailed = NativeGenerationFailed || panel.GenerationFailed;
        if (NativeGenerationFailed) return ReconcileGenerationFailure();
        RestoreGenerationFailureDisable();
        if (LockPolicy == UniqueCandidateLockPolicy.None && !PresentationApplied)
            return UniqueCandidateRerollAction.KeepNative;
        bool nativeButtonInteractable;
        try
        {
            // Read in either case: a button that cannot be read is reported and ends the lock, snapshot or not.
            var native = panel.NativeInteractable;
            nativeButtonInteractable = PresentationApplied ? originalInteractable : native;
        }
        catch (Exception ex)
        {
            if (PresentationApplied) RestoreSnapshot();
            panel.Warn("Core could not inspect the native reroll button: " + ex.Message);
            return UniqueCandidateRerollAction.KeepNative;
        }
        var action = UniqueCandidateRerollDecision.Evaluate(LockPolicy, ProfileLocked, PrimaryTraitsLocked,
            NativeGenerationFailed, nativeButtonInteractable, PresentationApplied);
        Perform(action);
        return action;
    }

    // Runs before every native state change of the panel: takes back the failure disable and the
    // lock presentation while the panel still has its previous native state.
    internal void BeforeNativeStateChange()
    {
        RestoreGenerationFailureDisable();
        if (!PresentationApplied) return;
        if (panel.GenerationFailed)
        {
            NativeGenerationFailed = true;
            DiscardSnapshot();
            return;
        }
        RestoreSnapshot();
    }

    // The panel is reused for another candidate or the lifecycle is reset.
    internal void Release()
    {
        RestoreGenerationFailureDisable();
        if (NativeGenerationFailed || panel.GenerationFailed) DiscardSnapshot();
        else RestoreSnapshot();
    }

    private UniqueCandidateRerollAction ReconcileGenerationFailure()
    {
        var action = UniqueCandidateRerollDecision.Evaluate(LockPolicy, ProfileLocked, PrimaryTraitsLocked,
            true, false, PresentationApplied);
        DiscardSnapshot();
        if (UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(LockPolicy, true))
            ApplyGenerationFailureDisable();
        return action;
    }

    private void Perform(UniqueCandidateRerollAction action)
    {
        switch (action)
        {
            case UniqueCandidateRerollAction.ApplyCustomLock:
                ApplyLockConflict(disableButton: true);
                break;
            case UniqueCandidateRerollAction.ApplyLockConflictText:
                ApplyLockConflict(disableButton: false);
                break;
            case UniqueCandidateRerollAction.RestoreNativeState:
                RestoreSnapshot();
                break;
            case UniqueCandidateRerollAction.DiscardStaleCustomState:
                DiscardSnapshot();
                break;
        }
    }

    private void ApplyLockConflict(bool disableButton)
    {
        try
        {
            var surface = panel.PresentationSurface;
            if (surface == null)
            {
                panel.Warn("Core could not reach the custom-expedition reroll button or its text.");
                return;
            }

            if (!PresentationApplied) CaptureSnapshot(surface);
            if (disableButton) surface.Interactable = false;
            surface.Text = panel.LockConflictText();
        }
        catch (Exception ex)
        {
            RestoreSnapshot();
            panel.Warn("Core could not apply the native lock-conflict explanation: " + ex.Message);
        }
    }

    // Only the interactable flag is touched; the native failure text stays in place.
    private void ApplyGenerationFailureDisable()
    {
        if (FailureDisableApplied) return;
        try
        {
            var surface = panel.FailureSurface;
            if (surface == null)
            {
                panel.Warn("Core could not reach the custom-expedition reroll button after a generation failure.");
                return;
            }
            failureSurface = surface;
            failureOriginalInteractable = surface.Interactable;
            surface.Interactable = false;
            FailureDisableApplied = true;
        }
        catch (Exception ex)
        {
            RestoreGenerationFailureDisable();
            panel.Warn("Core could not disable the reroll button after a generation failure: " + ex.Message);
        }
    }

    private void RestoreGenerationFailureDisable()
    {
        if (!FailureDisableApplied) return;
        try
        {
            if (failureSurface.ButtonAlive) failureSurface.Interactable = failureOriginalInteractable;
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        FailureDisableApplied = false;
    }

    private void CaptureSnapshot(IUniqueRerollSurface surface)
    {
        presentationSurface = surface;
        originalInteractable = surface.Interactable;
        originalText = surface.Text;
        originalStyle = surface.TextStyle;
        PresentationApplied = true;
    }

    private void RestoreSnapshot()
    {
        if (!PresentationApplied) return;
        try
        {
            if (presentationSurface.ButtonAlive) presentationSurface.Interactable = originalInteractable;
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        try
        {
            if (presentationSurface.TextAlive)
            {
                presentationSurface.Text = originalText;
                presentationSurface.TextStyle = originalStyle;
            }
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        PresentationApplied = false;
    }

    private void DiscardSnapshot() => PresentationApplied = false;
}
