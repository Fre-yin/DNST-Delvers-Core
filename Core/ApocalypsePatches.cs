using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Hooks of the Apocalypse difficulty (see ApocalypseDifficulty). Every hook catches its own
// failures: the campaign screen and loading keep the native behaviour if Core fails.
[HarmonyPatch(typeof(CreateCampaignPresenter), nameof(CreateCampaignPresenter.ChangeCampaignCombatDifficulty))]
internal static class ApocalypseCombatStepPatch
{
    private static bool Prefix(CreateCampaignPresenter __instance, IViewUpdateData viewUpdateData)
        => !ApocalypseHooks.Step(__instance, viewUpdateData, true);
}

[HarmonyPatch(typeof(CreateCampaignPresenter), nameof(CreateCampaignPresenter.ChangeCampaignManagementDifficulty))]
internal static class ApocalypseManagementStepPatch
{
    private static bool Prefix(CreateCampaignPresenter __instance, IViewUpdateData viewUpdateData)
        => !ApocalypseHooks.Step(__instance, viewUpdateData, false);
}

[HarmonyPatch(typeof(CreateCampaignPresenter), nameof(CreateCampaignPresenter.RefreshDifficultyIncreaseButtonState))]
internal static class ApocalypseIncreaseButtonPatch
{
    private static void Postfix(CreateCampaignPresenter __instance)
        => ApocalypseHooks.Run("increase-buttons", () => ApocalypseDifficulty.RefreshIncreaseButtons(__instance));
}

[HarmonyPatch(typeof(CreateCampaignPresenter), nameof(CreateCampaignPresenter.OnActivateView))]
internal static class ApocalypseScreenOpenedPatch
{
    // Before the screen sets its defaults and texts: a new screen starts without Apocalypse.
    private static void Prefix() => ApocalypseHooks.Run("screen-opened", ApocalypseDifficulty.ResetSelection);
}

[HarmonyPatch(typeof(SubUI_CampaignSetting), nameof(SubUI_CampaignSetting.SetCombatDifficultyText))]
internal static class ApocalypseCombatTextPatch
{
    private static void Postfix(SubUI_CampaignSetting __instance, CombatDifficulty difficulty)
        => ApocalypseHooks.Run("combat-text", () => ApocalypseDifficulty.ApplyText(__instance, true, (int)difficulty));
}

[HarmonyPatch(typeof(SubUI_CampaignSetting), nameof(SubUI_CampaignSetting.SetManagementDifficultyText))]
internal static class ApocalypseManagementTextPatch
{
    private static void Postfix(SubUI_CampaignSetting __instance, ManagementDifficulty difficulty)
        => ApocalypseHooks.Run("management-text", () => ApocalypseDifficulty.ApplyText(__instance, false, (int)difficulty));
}

// The screen's final call; with Apocalypse the value is Downfall, so the tutorial question is
// skipped and the game always arrives here.
[HarmonyPatch(typeof(CreateCampaignView), nameof(CreateCampaignView.StartCampaign),
    new[] { typeof(string), typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool),
        typeof(bool), typeof(string), typeof(bool) })]
internal static class ApocalypseStartCampaignPatch
{
    private static void Prefix(int combatDifficulty, int managementDifficulty)
        => ApocalypseHooks.Run("start", () => ApocalypseDifficulty.RememberStart(combatDifficulty, managementDifficulty));
}

[HarmonyPatch(typeof(CampaignLoadingEventHandler), nameof(CampaignLoadingEventHandler.InitializeCampaignData))]
internal static class ApocalypseNewCampaignPatch
{
    // Before the body: the game applies balance, research and environment right there. The
    // new-campaign setup calls this, and so does the start of every load, with a throwaway
    // campaign before the save is applied (seen in the log). Without a request (msg null) BeginNewCampaign switches Apocalypse off; a load
    // then binds the real values in CampaignDataContainer.Deserialize.
    private static void Prefix(CreateCampaignRequested msg)
        => ApocalypseHooks.Run("new-campaign", () => ApocalypseDifficulty.BeginNewCampaign(msg));

    // Every new campaign starts Core's save data, also without a request.
    private static void Postfix() => ApocalypseHooks.Run("new-campaign-mark", ApocalypseDifficulty.FinishNewCampaign);
}

// A save is being applied: its GUID decides. Only the load routine calls this method, never a new
// campaign. Not CampaignSaveLoader.TryLoad: patching its out parameters made the Il2CppInterop
// trampoline crash every load in both loaders (AccessViolation in coreclr).
[HarmonyPatch(typeof(CampaignDataContainer), nameof(CampaignDataContainer.Deserialize),
    new[] { typeof(CampaignSaveData) })]
internal static class ApocalypseLoadPatch
{
    private static void Prefix(CampaignSaveData data)
    {
        if (data == null) return;
        try { ApocalypseDifficulty.BindLoaded(data.ClanSaveData.CampaignGuid, SavedDifficulty(data)); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("load", ex);
            LoadIntegrityGuard.ReportFailure(LoadFailureSources.ApocalypseBind, ex);
        }
    }

    // Only for the log line of the options; a save that does not give them leaves it to the campaign setting.
    private static (int Combat, int Management)? SavedDifficulty(CampaignSaveData data)
    {
        try
        {
            var start = data.CampaignSettingSaveData.CampaignStartSettingSaveData;
            return ((int)start.CombatDifficulty, (int)start.ManagementDifficulty);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal static class ApocalypseHooks
{
    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    internal static bool Step(CreateCampaignPresenter presenter, IViewUpdateData data, bool combat)
    {
        try
        {
            var change = data?.TryCast<ViewBoolData>();
            return change != null && ApocalypseDifficulty.Step(presenter, combat, change.Value);
        }
        catch (Exception ex)
        {
            Report("step", ex);
            return false;   // the native step runs
        }
    }

    internal static void Run(string stage, Action action)
    {
        try { action(); }
        catch (Exception ex) { Report(stage, ex); }
    }

    internal static void Report(string stage, Exception ex)
    {
        lock (Reported)
            if (!Reported.Add(stage)) return;
        DelversHost.Error("CORE_APOCALYPSE_FAILED stage=" + stage + ": " + ex);
    }
}
