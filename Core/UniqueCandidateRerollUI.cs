using System.Reflection;
using HarmonyLib;
#if BEPINEX
using global::Refactor;
#else
using Il2CppRefactor;
#endif
#if BEPINEX
using global::Refactor.UI;
#else
using Il2CppRefactor.UI;
#endif
#if BEPINEX
using TMPro;
#else
using Il2CppTMPro;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

internal static class UniqueCandidateRerollUI
{
    private static readonly object Gate = new();
    private static readonly Dictionary<IntPtr, PanelState> States = new();
    private static bool uiWarningLogged;

    internal static bool BindingsReady
    {
        get
        {
            var panelType = typeof(SubUI_EstablishUnitPanel);
            return AccessTools.Method(panelType, "SetUnitData", new[] { typeof(RecruitCandidateData) }) != null
                && AccessTools.Method(panelType, "SetRestrictions", new[] { typeof(EstablishUnitRestriction) }) != null
                && AccessTools.Method(panelType, "SetGenerationFailed", new[] { typeof(bool) }) != null
                && AccessTools.Method(panelType, "RefreshLanguage", Type.EmptyTypes) != null
                && panelType.GetProperty("_refreshButton") != null
                && panelType.GetProperty("_refreshButtonText") != null
                && panelType.GetProperty("_isProfileLocked") != null
                && panelType.GetProperty("_isMainTraitsLocked") != null
                && panelType.GetProperty("_generationFailed") != null;
        }
    }

    internal static bool HasRequiredHooks
    {
        get
        {
            var panelType = typeof(SubUI_EstablishUnitPanel);
            return HasPrefixAndPostfix(AccessTools.Method(panelType, "SetUnitData", new[] { typeof(RecruitCandidateData) }),
                    typeof(UniqueCandidateSetUnitDataPatch))
                && HasPrefixAndPostfix(AccessTools.Method(panelType, "SetRestrictions", new[] { typeof(EstablishUnitRestriction) }),
                    typeof(UniqueCandidateSetRestrictionsPatch))
                && HasPrefixAndPostfix(AccessTools.Method(panelType, "SetGenerationFailed", new[] { typeof(bool) }),
                    typeof(UniqueCandidateSetGenerationFailedPatch))
                && HasPrefixAndPostfix(AccessTools.Method(panelType, "RefreshLanguage", Type.EmptyTypes),
                    typeof(UniqueCandidateRefreshLanguagePatch));
        }
    }

    internal static void BeforeSetUnitData(SubUI_EstablishUnitPanel panel)
    {
        var state = RemoveState(panel);
        if (state == null) return;
        RestoreGenerationFailureDisable(state);
        RestoreOrDiscard(panel, state);
    }

    internal static void AfterSetUnitData(SubUI_EstablishUnitPanel panel, RecruitCandidateData candidate)
    {
        if (panel == null) return;
        var state = new PanelState
        {
            Panel = panel,
            LockPolicy = DelversCoreRuntime.GetUniqueCandidateLockPolicy(candidate),
            ProfileLocked = IsProfileLocked(panel),
            PrimaryTraitsLocked = ArePrimaryTraitsLocked(panel),
            NativeGenerationFailed = IsGenerationFailed(panel)
        };
        lock (Gate) States[panel.Pointer] = state;
        Reconcile(panel, state);
    }

    internal static void BeforeNativeStateChange(SubUI_EstablishUnitPanel panel)
    {
        var state = GetState(panel);
        RestoreGenerationFailureDisable(state);
        if (state?.OverrideApplied != true) return;
        if (IsGenerationFailed(panel))
        {
            state.NativeGenerationFailed = true;
            DiscardSnapshot(state);
            return;
        }
        RestoreSnapshot(state);
    }

    internal static void AfterRestrictions(SubUI_EstablishUnitPanel panel, EstablishUnitRestriction restrictions)
    {
        var state = GetState(panel);
        if (state == null) return;
        try
        {
            state.ProfileLocked = restrictions.UseProfileRestriction;
            var locks = restrictions.Locks;
            state.PrimaryTraitsLocked = locks != null && locks.MainTraitsLocked;
        }
        catch (Exception ex)
        {
            WarnUiOnce("Core could not read the native reroll restrictions: " + ex.Message);
            state.ProfileLocked = IsProfileLocked(panel);
            state.PrimaryTraitsLocked = ArePrimaryTraitsLocked(panel);
        }
        state.NativeGenerationFailed = IsGenerationFailed(panel);
        Reconcile(panel, state);
    }

    internal static void AfterGenerationFailed(SubUI_EstablishUnitPanel panel, bool generationFailed)
    {
        var state = GetState(panel);
        if (state == null) return;
        state.NativeGenerationFailed = generationFailed || IsGenerationFailed(panel);
        Reconcile(panel, state);
    }

    internal static void AfterRefreshLanguage(SubUI_EstablishUnitPanel panel)
    {
        var state = GetState(panel);
        if (state == null) return;
        state.ProfileLocked = IsProfileLocked(panel);
        state.PrimaryTraitsLocked = ArePrimaryTraitsLocked(panel);
        state.NativeGenerationFailed = IsGenerationFailed(panel);
        Reconcile(panel, state);
    }

    internal static void ResetLifecycle()
    {
        PanelState[] states;
        lock (Gate)
        {
            states = States.Values.ToArray();
            States.Clear();
            uiWarningLogged = false;
        }
        foreach (var state in states)
        {
            RestoreGenerationFailureDisable(state);
            if (state.NativeGenerationFailed || IsGenerationFailed(state.Panel)) DiscardSnapshot(state);
            else RestoreSnapshot(state);
        }
    }

    private static void Reconcile(SubUI_EstablishUnitPanel panel, PanelState state)
    {
        if (panel == null || state == null) return;
        state.NativeGenerationFailed = state.NativeGenerationFailed || IsGenerationFailed(panel);
        if (state.NativeGenerationFailed)
        {
            DiscardSnapshot(state);
            if (UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(state.LockPolicy, true))
                ApplyGenerationFailureDisable(panel, state);
            return;
        }
        RestoreGenerationFailureDisable(state);
        if (state.LockPolicy == UniqueCandidateLockPolicy.None && !state.OverrideApplied) return;
        bool nativeButtonInteractable;
        try
        {
            var button = panel._refreshButton;
            nativeButtonInteractable = state.HasSnapshot
                ? state.OriginalInteractable
                : button != null && button.interactable;
        }
        catch (Exception ex)
        {
            if (state.OverrideApplied) RestoreSnapshot(state);
            WarnUiOnce("Core could not inspect the native reroll button: " + ex.Message);
            return;
        }
        var action = UniqueCandidateRerollDecision.Evaluate(state.LockPolicy, state.ProfileLocked,
            state.PrimaryTraitsLocked, state.NativeGenerationFailed, nativeButtonInteractable,
            state.OverrideApplied);

        switch (action)
        {
            case UniqueCandidateRerollAction.ApplyCustomLock:
                ApplyLockConflict(panel, state, disableButton: true);
                break;
            case UniqueCandidateRerollAction.ApplyLockConflictText:
                ApplyLockConflict(panel, state, disableButton: false);
                break;
            case UniqueCandidateRerollAction.RestoreNativeState:
                RestoreSnapshot(state);
                break;
            case UniqueCandidateRerollAction.DiscardStaleCustomState:
                DiscardSnapshot(state);
                break;
        }
    }

    private static void ApplyLockConflict(SubUI_EstablishUnitPanel panel, PanelState state, bool disableButton)
    {
        try
        {
            var button = panel._refreshButton;
            var text = panel._refreshButtonText;
            if (button == null || text == null)
            {
                WarnUiOnce("Core could not reach the custom-expedition reroll button or its text.");
                return;
            }

            if (!state.HasSnapshot) CaptureSnapshot(button, text, state);
            if (disableButton) button.interactable = false;
            text.text = UniqueCandidateLocalization.GetLockConflictText();
            state.OverrideApplied = true;
        }
        catch (Exception ex)
        {
            RestoreSnapshot(state);
            WarnUiOnce("Core could not apply the native lock-conflict explanation: " + ex.Message);
        }
    }

    // Only the interactable flag is touched; the native failure text stays in place.
    private static void ApplyGenerationFailureDisable(SubUI_EstablishUnitPanel panel, PanelState state)
    {
        if (state.FailureDisableApplied) return;
        try
        {
            var button = panel._refreshButton;
            if (button == null)
            {
                WarnUiOnce("Core could not reach the custom-expedition reroll button after a generation failure.");
                return;
            }
            state.FailureButton = button;
            state.FailureOriginalInteractable = button.interactable;
            button.interactable = false;
            state.FailureDisableApplied = true;
        }
        catch (Exception ex)
        {
            RestoreGenerationFailureDisable(state);
            WarnUiOnce("Core could not disable the reroll button after a generation failure: " + ex.Message);
        }
    }

    private static void RestoreGenerationFailureDisable(PanelState state)
    {
        if (state == null || !state.FailureDisableApplied) return;
        try
        {
            if (state.FailureButton != null) state.FailureButton.interactable = state.FailureOriginalInteractable;
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        state.FailureDisableApplied = false;
    }

    private static void CaptureSnapshot(Button button, TMP_Text text, PanelState state)
    {
        state.Button = button;
        state.ButtonText = text;
        state.OriginalInteractable = button.interactable;
        state.OriginalText = text.text;
        state.OriginalColor = text.color;
        state.HasSnapshot = true;
    }

    private static void RestoreOrDiscard(SubUI_EstablishUnitPanel panel, PanelState state)
    {
        if (state.NativeGenerationFailed || IsGenerationFailed(panel)) DiscardSnapshot(state);
        else RestoreSnapshot(state);
    }

    private static void RestoreSnapshot(PanelState state)
    {
        if (state == null || !state.HasSnapshot) return;
        try
        {
            if (state.Button != null) state.Button.interactable = state.OriginalInteractable;
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        try
        {
            if (state.ButtonText != null)
            {
                state.ButtonText.text = state.OriginalText;
                state.ButtonText.color = state.OriginalColor;
            }
        }
        catch { /* A panel can be destroyed while its pool is being cleared. */ }
        state.HasSnapshot = false;
        state.OverrideApplied = false;
    }

    private static void DiscardSnapshot(PanelState state)
    {
        if (state == null) return;
        state.HasSnapshot = false;
        state.OverrideApplied = false;
    }

    private static bool IsGenerationFailed(SubUI_EstablishUnitPanel panel)
    {
        try { return panel == null || panel._generationFailed; }
        catch { return true; }
    }

    private static bool IsProfileLocked(SubUI_EstablishUnitPanel panel)
    {
        try { return panel != null && panel._isProfileLocked; }
        catch { return false; }
    }

    private static bool ArePrimaryTraitsLocked(SubUI_EstablishUnitPanel panel)
    {
        try { return panel != null && panel._isMainTraitsLocked; }
        catch { return false; }
    }

    private static PanelState GetState(SubUI_EstablishUnitPanel panel)
    {
        if (panel == null) return null;
        lock (Gate) return States.TryGetValue(panel.Pointer, out var state) ? state : null;
    }

    private static PanelState RemoveState(SubUI_EstablishUnitPanel panel)
    {
        if (panel == null) return null;
        lock (Gate)
        {
            if (!States.TryGetValue(panel.Pointer, out var state)) return null;
            States.Remove(panel.Pointer);
            return state;
        }
    }

    private static bool HasPrefixAndPostfix(MethodBase method, Type patchType)
    {
        var info = method == null ? null : Harmony.GetPatchInfo(method);
        return info?.Prefixes.Any(patch => patch.PatchMethod?.DeclaringType == patchType) == true
            && info?.Postfixes.Any(patch => patch.PatchMethod?.DeclaringType == patchType) == true;
    }

    private static void WarnUiOnce(string message)
    {
        lock (Gate)
        {
            if (uiWarningLogged) return;
            uiWarningLogged = true;
        }
        try { DelversHost.Warning("CORE_UNIQUE_REROLL_UI_FAILED: " + message); }
        catch { }
    }

    private sealed class PanelState
    {
        internal SubUI_EstablishUnitPanel Panel;
        internal UniqueCandidateLockPolicy LockPolicy;
        internal bool ProfileLocked;
        internal bool PrimaryTraitsLocked;
        internal bool NativeGenerationFailed;
        internal Button Button;
        internal TMP_Text ButtonText;
        internal bool OriginalInteractable;
        internal string OriginalText;
        internal Color OriginalColor;
        internal bool HasSnapshot;
        internal bool OverrideApplied;
        internal Button FailureButton;
        internal bool FailureOriginalInteractable;
        internal bool FailureDisableApplied;
    }
}

[HarmonyPatch]
internal static class UniqueCandidateSetUnitDataPatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "SetUnitData", new[] { typeof(RecruitCandidateData) });

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.BeforeSetUnitData(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance, RecruitCandidateData __0)
        => UniqueCandidateRerollUI.AfterSetUnitData(__instance, __0);
}

[HarmonyPatch]
internal static class UniqueCandidateSetRestrictionsPatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "SetRestrictions",
            new[] { typeof(EstablishUnitRestriction) });

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.BeforeNativeStateChange(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance, EstablishUnitRestriction __0)
        => UniqueCandidateRerollUI.AfterRestrictions(__instance, __0);
}

[HarmonyPatch]
internal static class UniqueCandidateSetGenerationFailedPatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "SetGenerationFailed", new[] { typeof(bool) });

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        // Restore while the panel still has its previous native state; the
        // postfix records the new error state without reapplying the custom lock.
        => UniqueCandidateRerollUI.BeforeNativeStateChange(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance, bool __0)
        => UniqueCandidateRerollUI.AfterGenerationFailed(__instance, __0);
}

[HarmonyPatch]
internal static class UniqueCandidateRefreshLanguagePatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "RefreshLanguage", Type.EmptyTypes);

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.BeforeNativeStateChange(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.AfterRefreshLanguage(__instance);
}
