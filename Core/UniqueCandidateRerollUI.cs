using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

internal static class UniqueCandidateRerollUI
{
    private static readonly object Gate = new();
    private static readonly Dictionary<IntPtr, UniqueCandidateRerollState> States = new();
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
        RemoveState(panel)?.Release();
    }

    internal static void AfterSetUnitData(SubUI_EstablishUnitPanel panel, RecruitCandidateData candidate)
    {
        if (panel == null) return;
        var state = new UniqueCandidateRerollState(new NativePanel(panel),
            DelversCoreRuntime.GetUniqueCandidateLockPolicy(candidate), IsProfileLocked(panel),
            ArePrimaryTraitsLocked(panel), IsGenerationFailed(panel));
        lock (Gate) States[panel.Pointer] = state;
        state.Reconcile();
    }

    internal static void BeforeNativeStateChange(SubUI_EstablishUnitPanel panel)
    {
        GetState(panel)?.BeforeNativeStateChange();
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
        state.Reconcile();
    }

    internal static void AfterGenerationFailed(SubUI_EstablishUnitPanel panel, bool generationFailed)
    {
        var state = GetState(panel);
        if (state == null) return;
        state.NativeGenerationFailed = generationFailed || IsGenerationFailed(panel);
        state.Reconcile();
    }

    internal static void AfterRefreshLanguage(SubUI_EstablishUnitPanel panel)
    {
        var state = GetState(panel);
        if (state == null) return;
        state.ProfileLocked = IsProfileLocked(panel);
        state.PrimaryTraitsLocked = ArePrimaryTraitsLocked(panel);
        state.NativeGenerationFailed = IsGenerationFailed(panel);
        state.Reconcile();
    }

    internal static void ResetLifecycle()
    {
        UniqueCandidateRerollState[] states;
        lock (Gate)
        {
            states = States.Values.ToArray();
            States.Clear();
            uiWarningLogged = false;
        }
        foreach (var state in states) state.Release();
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

    private static UniqueCandidateRerollState GetState(SubUI_EstablishUnitPanel panel)
    {
        if (panel == null) return null;
        lock (Gate) return States.TryGetValue(panel.Pointer, out var state) ? state : null;
    }

    private static UniqueCandidateRerollState RemoveState(SubUI_EstablishUnitPanel panel)
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

    // The game's recruitment panel and its reroll button, as the reroll rule sees them.
    private sealed class NativePanel : IUniqueRerollPanel
    {
        private readonly SubUI_EstablishUnitPanel panel;

        internal NativePanel(SubUI_EstablishUnitPanel panel) => this.panel = panel;

        public bool GenerationFailed => IsGenerationFailed(panel);

        public bool NativeInteractable
        {
            get
            {
                var button = panel._refreshButton;
                return button != null && button.interactable;
            }
        }

        public IUniqueRerollSurface PresentationSurface
        {
            get
            {
                var button = panel._refreshButton;
                var text = panel._refreshButtonText;
                return button == null || text == null ? null : new NativeSurface(button, text);
            }
        }

        public IUniqueRerollSurface FailureSurface
        {
            get
            {
                var button = panel._refreshButton;
                return button == null ? null : new NativeSurface(button, null);
            }
        }

        public string LockConflictText() => UniqueCandidateLocalization.GetLockConflictText();

        public void Warn(string message) => WarnUiOnce(message);
    }

    private sealed class NativeSurface : IUniqueRerollSurface
    {
        private readonly Button button;
        private readonly TMP_Text text;

        internal NativeSurface(Button button, TMP_Text text)
        {
            this.button = button;
            this.text = text;
        }

        public bool ButtonAlive => button != null;

        public bool TextAlive => text != null;

        public bool Interactable
        {
            get => button.interactable;
            set => button.interactable = value;
        }

        public string Text
        {
            get => text.text;
            set => text.text = value;
        }

        public object TextStyle
        {
            get => text.color;
            set => text.color = (Color)value;
        }
    }
}

[HarmonyPatch]
internal static class UniqueCandidateSetUnitDataPatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "SetUnitData", new[] { typeof(RecruitCandidateData) });

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.BeforeSetUnitData(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance, RecruitCandidateData candidateData)
        => UniqueCandidateRerollUI.AfterSetUnitData(__instance, candidateData);
}

[HarmonyPatch]
internal static class UniqueCandidateSetRestrictionsPatch
{
    private static MethodBase TargetMethod()
        => AccessTools.Method(typeof(SubUI_EstablishUnitPanel), "SetRestrictions",
            new[] { typeof(EstablishUnitRestriction) });

    private static void Prefix(SubUI_EstablishUnitPanel __instance)
        => UniqueCandidateRerollUI.BeforeNativeStateChange(__instance);

    private static void Postfix(SubUI_EstablishUnitPanel __instance, EstablishUnitRestriction restriction)
        => UniqueCandidateRerollUI.AfterRestrictions(__instance, restriction);
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

    private static void Postfix(SubUI_EstablishUnitPanel __instance, bool failed)
        => UniqueCandidateRerollUI.AfterGenerationFailed(__instance, failed);
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
