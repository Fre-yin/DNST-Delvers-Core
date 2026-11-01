using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The Apocalypse difficulty, one step above the game's highest level Downfall, for combat and
// management separately. Step 1: it takes Downfall's values; harsher
// rules follow. The campaign screen shows the step when the switch in the Delvers menu is on
// and Downfall is unlocked. The game keeps the value Downfall, so iron mode is forced for
// combat exactly as on Downfall; Core remembers Apocalypse per campaign GUID.
internal static class ApocalypseDifficulty
{
    private const int Downfall = ApocalypseDifficultyRules.Downfall;
    private static ApocalypseDifficultyRules.State state;

    // The loaded or newly created campaign's Apocalypse parts.
    internal static bool ActiveCombat => state.ActiveCombat;
    internal static bool ActiveManagement => state.ActiveManagement;

    internal static bool Offered => CoreSettings.ApocalypseAvailable;

    internal static void ResetLifecycle()
    {
        state = state.SelectionCleared().ActiveCleared();
        ApocalypseAggro.ResetCampaign();
        ApocalypseTime.ResetCampaign();
        ApocalypseAmbush.ResetLifecycle();
        ApocalypseRules.ResetLifecycle();
    }

    internal static void ResetSelection() => state = state.SelectionCleared();

    // Arrow step on the campaign screen. Returns true when Core handled it; the game's value
    // stays Downfall either way.
    internal static bool Step(CreateCampaignPresenter presenter, bool combat, bool increase)
    {
        if (presenter == null) return false;
        var current = combat ? (int)presenter._combatDifficulty : (int)presenter._managementDifficulty;
        var step = ApocalypseDifficultyRules.Step(current, state.Selected(combat), increase,
            Offered && presenter._isDownfallDifficultyUnlocked);
        state = state.WithSelected(combat, step.Selected);
        if (!step.Changed) return step.Handled;
        var panel = presenter.View?._campaignSettingPanel;
        if (combat) panel?.SetCombatDifficultyText(presenter._combatDifficulty);
        else panel?.SetManagementDifficultyText(presenter._managementDifficulty);
        presenter.RefreshDifficultyIncreaseButtonState();
        DelversHost.Info($"CORE_APOCALYPSE_SELECTED part={(combat ? "combat" : "management")} selected={step.Selected.ToString().ToLowerInvariant()}");
        return true;
    }

    // The game locks the up arrow at Downfall; open it again when Apocalypse is the next step.
    internal static void RefreshIncreaseButtons(CreateCampaignPresenter presenter)
    {
        var view = presenter?.View;
        if (view == null || !Offered || !presenter._isDownfallDifficultyUnlocked) return;
        if ((int)presenter._combatDifficulty == Downfall && !state.CombatSelected)
            view.SetCombatDifficultyIncreaseLocked(false, string.Empty);
        if ((int)presenter._managementDifficulty == Downfall && !state.ManagementSelected)
            view.SetManagementDifficultyIncreaseLocked(false, string.Empty);
    }

    // True while the campaign screen shows Apocalypse in place of the game's Downfall step.
    internal static bool Shows(bool combat, int difficulty)
        => state.Shows(combat, difficulty);

    internal static void ApplyText(SubUI_CampaignSetting panel, bool combat, int difficulty)
    {
        if (panel == null || !state.Shows(combat, difficulty)) return;
        UniqueCandidateLocalization.EnsureCurrent();
        if (combat)
        {
            panel._combatDifficultyTitleText?.SetTextKey(ApocalypseText.TitleKey);
            panel._combatDifficultyDescriptionText?.SetTextKey(ApocalypseText.CombatDescriptionKey);
        }
        else
        {
            panel._managementDifficultyTitleText?.SetTextKey(ApocalypseText.TitleKey);
            panel._managementDifficultyDescriptionText?.SetTextKey(ApocalypseText.ManagementDescriptionKey);
        }
    }

    // The screen's last call before the game creates the campaign.
    internal static void RememberStart(int combatDifficulty, int managementDifficulty)
        => state = state.Remembered(combatDifficulty, managementDifficulty);

    // Before the game applies the new campaign's difficulty. Without a request (the game may start
    // a campaign without the screen) nothing of an earlier campaign may stay active.
    internal static void BeginNewCampaign(CreateCampaignRequested request)
    {
        var requested = Requested(request);
        state = state.Begun(requested);
        CoreSaveStore.Unbind();
        StartCampaignRules(request != null, requested is { } values ? (values.Combat, values.Management) : null);
    }

    private static ApocalypseDifficultyRules.RequestedDifficulty? Requested(CreateCampaignRequested request)
    {
        if (request == null) return null;
        return new ApocalypseDifficultyRules.RequestedDifficulty((int)request.CombatDifficulty, (int)request.ManagementDifficulty);
    }

    // A campaign's rules start: the options read once (ApocalypseRules), per-campaign state back to
    // zero, and the ambush value set before the game builds any unit (ApocalypseAmbush). With
    // report, the rules and the enemies' values go to the log; a start without the screen is quiet.
    private static void StartCampaignRules(bool report, (int Combat, int Management)? game)
    {
        CoreProbes.Refresh();
        ApocalypseRules.Apply(report, game);
        EnemyAbilities.ResetCampaign();
        FirstHints.ResetCampaign();
        GroupAiPolicy.ResetCampaign();
        Lifebloom.ResetCampaign();
        EnemySkillTest.Reset();
        CorpseKeep.ResetCampaign();
        CorePartyShield.ResetCampaign();
        DelversCoreRuntime.ResetSummons();
        if (report) ApocalypseHooks.Run("enemy-ability-units", EnemyAbilities.ReportUnits);
        if (report) ApocalypseHooks.Run("unit-stats", CoreUnits.ReportStats);
        ApocalypseAggro.ResetCampaign();
        ApocalypseTime.ResetCampaign();
        ApocalypseHooks.Run("ambush-sync", ApocalypseAmbush.Sync);
    }

    // After the new campaign exists: Core's save data starts (CoreSaveStore), with Apocalypse
    // when it was chosen. The first save carries it.
    internal static void FinishNewCampaign()
    {
        var guid = CurrentCampaignGuid();
        if (guid == Guid.Empty)
        {
            DelversHost.Error("CORE_APOCALYPSE_MARK_FAILED: the new campaign has no GUID yet.");
            return;
        }
        CoreSavePaths.EnsureArchive();
        CoreSaveStore.BeginNewCampaign(guid);
        // A test armed "the next new campaign is a test campaign" (API 1.8.0). Only a real creation
        // from the screen counts: the game also runs this at the start of every load, with a
        // throwaway campaign.
        if (state.ClaimsTestMark(NextCampaignTest.Shared) && CampaignPresence.MarkTestCampaign(guid))
            DelversHost.Info($"CORE_TEST_CAMPAIGN_MARKED campaign={guid:D} by=next-new-campaign");
        state = state.Created();
        ReviveCampaign.OnCampaignBound();
        ReviveCampaign.OnNewCampaign();
        if (!ActiveCombat && !ActiveManagement) return;
        CoreSaveStore.Current.Apocalypse = new ApocalypseMark(ActiveCombat, ActiveManagement);
        DelversHost.Info($"CORE_APOCALYPSE_CAMPAIGN campaign={guid:D} combat={ActiveCombat.ToString().ToLowerInvariant()} management={ActiveManagement.ToString().ToLowerInvariant()}");
    }

    // A save is being applied; CoreSaveStore has bound its data just before. The campaign has the
    // Apocalypse parts that the mark in the save carries; a save without a mark is native Downfall. game: the save's own difficulty
    // values, which the game applies only after this.
    internal static void BindLoaded(Il2CppSystem.Guid campaign, (int Combat, int Management)? game)
    {
        // A load is never the armed creation; a test that stopped halfway must not mark a later one.
        if (NextCampaignTest.Shared.Pending)
        {
            NextCampaignTest.Shared.Disarm();
            DelversHost.Info("CORE_TEST_NEXT_CAMPAIGN_DISARMED reason=load");
        }
        var guid = Guid.TryParse(campaign.ToString(), out var parsed) ? parsed : Guid.Empty;
        var data = CoreSaveStore.Current;
        if (data != null && data.Campaign != guid) data = null;
        var mark = data?.Apocalypse ?? default;
        state = state.Loaded(mark);
        StartCampaignRules(report: true, game);
        if (mark.Any)
            DelversHost.Info($"CORE_APOCALYPSE_ACTIVE campaign={guid:D} combat={mark.Combat.ToString().ToLowerInvariant()} management={mark.Management.ToString().ToLowerInvariant()}");
    }

    // Only for the loaded campaign: written into its save data, active after saving and loading.
    internal static bool MarkForTest(Guid campaign, ApocalypseMark mark)
    {
        var data = CoreSaveStore.Current;
        if (data == null || data.Campaign != campaign) return false;
        data.Apocalypse = mark;
        return true;
    }

    internal static string Describe()
    {
        static string Flag(bool value) => value ? "true" : "false";
        return "combat=" + Flag(ActiveCombat) + " management=" + Flag(ActiveManagement)
            + " noPause=" + Flag(ApocalypseTime.Applies) + " slow=" + Flag(ApocalypseTime.Slow)
            + " charge=" + ApocalypseTime.Charge.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " ambushOff=" + Flag(ApocalypseAmbush.BonusOff)
            + " wounds=" + Flag(ApocalypseRules.Wounds) + " instantCombat=" + Flag(ApocalypseRules.InstantCombat)
            + " enemyAbilities=" + Flag(ApocalypseRules.EnemyAbilities)
            + " sharpened=" + ApocalypseAggro.Sharpened.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // Test cleanup: the loaded campaign's save data forgets Apocalypse.
    internal static void Forget(Guid campaign)
    {
        var data = CoreSaveStore.Current;
        if (data != null && data.Campaign == campaign) data.Apocalypse = default;
    }

    private static Guid CurrentCampaignGuid()
    {
        var clan = UnityEngine.Object.FindObjectOfType<MainFlow>()?._campaignData?.ClanDataContainer;
        return clan != null && Guid.TryParse(clan.CampaignGuid.ToString(), out var guid) ? guid : Guid.Empty;
    }
}
