namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the tool API: test campaigns by GUID, the settings copy, the Apocalypse options for tests, and the
// revive API with its sickness. Tools and packs call these by name, some through reflection, so names and signatures stay.
// The pack API, the registries and the state are in DelversCoreRuntime.cs.
public static partial class DelversCoreRuntime
{
    // Test campaigns (review, H7 to H10). A campaign counts as a test campaign only by
    // the GUID a test has marked through MarkTestCampaign, never by its clan name. Uniqueness is
    // strict everywhere else, also when a switch is left on. GUIDs are strings, so tools without
    // a reference to Core can call these methods through reflection.
    public static bool MarkTestCampaign(string campaignGuid)
    {
        if (!TryCampaign(campaignGuid, out var campaign) || !CampaignPresence.MarkTestCampaign(campaign)) return false;
        DelversHost.Info("CORE_TEST_CAMPAIGN_MARKED campaign=" + campaign.ToString("D"));
        return true;
    }

    public static bool IsTestCampaign(string campaignGuid)
        => TryCampaign(campaignGuid, out var campaign) && CampaignPresence.IsTestCampaign(campaign);
    // API 1.9.0: the GUID of the campaign Core is bound to ("D" format), null in the main menu or before a campaign is
    // loaded or begun. Read it from the load integrations on or later; then ask IsTestCampaign with it.
    public static string CurrentCampaignGuid() => CampaignGuidRules.Text(CoreSaveStore.Current?.Campaign ?? Guid.Empty);

    // API 1.9.0: whether the campaign Core is bound to is a test campaign; false without one.
    public static bool IsCurrentCampaignTest()
    {
        var current = CurrentCampaignGuid();
        return current != null && IsTestCampaign(current);
    }

    // API 1.10.0: Core's settings are kept per game copy. The full path of this copy's settings.json (it may not exist
    // yet), its id, and the id of any game folder, so a tool that writes settings before the game starts finds the same file:
    // <persistentDataPath>\DungeonSettlersDelvers\Core\copies\<id>\settings.json. The rule of the id: CoreSettingsCopyRules.
    public static string SettingsFilePath() => CoreSettings.FilePath;

    // Internal before 0.5.0: no tool or pack calls them; a tool outside the game computes the id by the rule in the guide.
    internal static string SettingsCopyId() => CoreSettings.CopyId;

    internal static string SettingsCopyIdFor(string gameFolder) => CoreSettingsCopyRules.CopyId(gameFolder);

    // Removes the campaign's uniqueness markers and its test mark. Refused for real campaigns.
    public static bool ForgetTestCampaign(string campaignGuid)
    {
        if (!TryCampaign(campaignGuid, out var campaign) || !CampaignPresence.IsTestCampaign(campaign)) return false;
        TestSwitches.Forget(campaign);
        ApocalypseDifficulty.Forget(campaign);
        try
        {
            CoreSavePaths.EnsureArchive();
            CoreSaveStore.ForgetArchive(campaign);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_TEST_CAMPAIGN_ARCHIVE_FORGET_FAILED: " + ex.Message); }
        var removed = CampaignPresence.Forget(campaign);
        if (removed < 0) return false;
        DelversHost.Info($"CORE_TEST_CAMPAIGN_FORGOTTEN campaign={campaign:D} files={removed}");
        return true;
    }

    // "Recruitment free": founders and pack characters can be recruited again, to test that
    // they can be recruited at all. Only in a test campaign.
    public static bool SetRecruitmentFree(string campaignGuid, bool free)
    {
        if (!TryCampaign(campaignGuid, out var campaign) || !TestSwitches.SetRecruitmentFree(campaign, free)) return false;
        DelversHost.Info($"CORE_TEST_RECRUITMENT_FREE campaign={campaign:D} free={free.ToString().ToLowerInvariant()}");
        return true;
    }

    public static bool UniquenessEnforced(string campaignGuid)
        => !TryCampaign(campaignGuid, out var campaign) || TestSwitches.UniquenessEnforced(campaign);

    // Every matching guild candidate becomes a founder, so a test needs no repeated rolls.
    public static bool SetFounderOffersForced(string campaignGuid, bool forced)
    {
        if (!TryCampaign(campaignGuid, out var campaign) || !TestSwitches.SetFounderOffersForced(campaign, forced)) return false;
        DelversHost.Info($"CORE_TEST_FOUNDER_OFFERS_FORCED campaign={campaign:D} forced={forced.ToString().ToLowerInvariant()}");
        return true;
    }

    internal static bool FounderOffersForced(string campaignGuid)
        => TryCampaign(campaignGuid, out var campaign) && TestSwitches.FounderOffersForced(campaign);

    // Apocalypse in the loaded test campaign: written into its save data, so it takes effect
    // after saving and loading again, never in the middle of a campaign (the ambush value must not
    // change while units carry it). Only in a test campaign that is loaded.
    public static bool SetApocalypseForTest(string campaignGuid, bool combat, bool management)
    {
        if (!TryCampaign(campaignGuid, out var campaign) || !CampaignPresence.IsTestCampaign(campaign)
            || !ApocalypseDifficulty.MarkForTest(campaign, new ApocalypseMark(combat, management))) return false;
        DelversHost.Info($"CORE_TEST_APOCALYPSE campaign={campaign:D} combat={combat.ToString().ToLowerInvariant()} management={management.ToString().ToLowerInvariant()} applies=after-save-and-load");
        return true;
    }

    // The single Apocalypse rules for campaigns without Apocalypse (ApocalypseOptionRules.Names),
    // as the Delvers menu sets them; they take effect with the next load. False for an unknown name.
    public static bool SetCoreOptionForTest(string name, bool on) => CoreSettings.SetOption(name, on);

    public static bool CoreOption(string name) => CoreSettings.Option(name);

    // Reviving a dead party member. Names agreed with the Modding Toolkit; GUIDs are strings, results are BCL types, so tools can call
    // these through reflection. Reason codes: see ReviveService.
    public const string MinimumApiVersionForRevive = "1.7.0";
    public const double ReviveStartHealthShare = ReviveRules.StartHealthShare;

    // Null when the corpse may be revived, otherwise a reason code.
    public static string CanRevive(string unitGuid)
        => ReviveCall(unitGuid, ReviveService.Blocker);

    // Null when the revive has started; it finishes over the next frames (LastReviveResult).
    public static string StartRevive(string unitGuid)
        => ReviveCall(unitGuid, ReviveService.Start);

    public static bool IsReviveRunning() => ReviveService.Running;

    // "" before the first revive, then "ok:<guid>" or "failed:<guid>:<code>".
    public static string LastReviveResult() => ReviveService.LastResult;

    // -1 means unlimited.
    public static int RevivesLeft(string unitGuid)
    {
        try { return Guid.TryParse(unitGuid, out var unit) ? ReviveService.RevivesLeft(unit) : 0; }
        catch (Exception) { return 0; }
    }

    // The permanent traits Core would give back, as "KEY:Stacks"; empty without a snapshot.
    public static string[] ReviveSnapshot(string unitGuid)
    {
        var data = CoreSaveStore.Current;
        if (data == null || !Guid.TryParse(unitGuid, out var unit)) return Array.Empty<string>();
        var list = data.Snapshot(unit);
        var result = new string[list.Count];
        for (var i = 0; i < list.Count; i++) result[i] = list[i].ToString();
        return result;
    }

    // The revival sickness: its effect keys, days left and in total, and the share of health and
    // energy the unit may have now (1 = healthy; a down's after-effect counts too).
    // Core draws the locked part of the bars itself (purple, the author's sketch) in every campaign;
    // tools that drew it before check this field and stop drawing their own.
    public const bool SicknessBarsShownByCore = true;
    public const string RevivalSicknessEffectKey = ReviveEffects.SicknessKey;
    public const string RevivalPanicEffectKey = ReviveEffects.PanicKey;

    public static double RevivalSicknessDaysLeft(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) ? Safe(() => ReviveSickness.DaysLeft(unit)) : 0.0;

    public static double RevivalSicknessDaysTotal(string unitGuid)
        => Guid.TryParse(unitGuid, out var unit) ? Safe(() => ReviveSickness.DaysTotal(unit)) : 0.0;

    public static double RevivalSicknessCap(string unitGuid)
    {
        if (!Guid.TryParse(unitGuid, out var unit)) return 1.0;
        return Safe(() =>
        {
            var found = CoreGame.FindUnit(unit);
            return found == null || found.IsDead ? 1.0 : ReviveSickness.Cap(found);
        }, 1.0);
    }

    // Test hook, only in the loaded test campaign: the sickness lasts this many more days; 0 ends it.
    public static bool SetRevivalSicknessDaysForTest(string unitGuid, double days)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || !LoadedTestCampaign()) return false;
        var done = Safe(() => ReviveSickness.SetDaysForTest(unit, days) ? 1.0 : 0.0) > 0;
        if (done) DelversHost.Info($"CORE_TEST_SICKNESS_DAYS unit={unit:D} days={days.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return done;
    }

    private static double Safe(Func<double> read, double fallback = 0.0)
    {
        try { return read(); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_REVIVE_API_READ_FAILED: " + ex.Message);
            return fallback;
        }
    }

    // Test hooks, only in the loaded test campaign. "after-view-removed" stops the next revive
    // after the corpse's view was removed and before the unit is inserted again.
    public static bool InjectReviveFailureForTest(string phase)
    {
        if (phase != ReviveService.AfterViewRemoved || !LoadedTestCampaign()) return false;
        ReviveService.InjectFailureForTest(phase);
        DelversHost.Info("CORE_TEST_REVIVE_FAILURE phase=" + phase);
        return true;
    }

    public static bool SetRevivesLeftForTest(string unitGuid, int left)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || !LoadedTestCampaign()) return false;
        ReviveService.SetRevivesLeftForTest(unit, left);
        DelversHost.Info($"CORE_TEST_REVIVES_LEFT unit={unit:D} left={Math.Max(0, left)}");
        return true;
    }

    // Tool API, internal for now (the tool calls it through reflection): the bandit room's waterfall and bridge, set like any
    // item on the shown map, kept in Core's own save block (BanditRoomFeatureView, BanditRoomFeatureRules).
    internal static string[] RoomFeatureKinds() => (string[])BanditRoomFeatureRules.Kinds.Clone();

    internal static string PlaceRoomFeature(string kind, int x, int y) => BanditRoomFeatureView.Place(kind, x, y);

    internal static bool RemoveRoomFeature(int x, int y) => BanditRoomFeatureView.Remove(x, y);

    internal static string[] RoomFeatures() => BanditRoomFeatureView.List();

    // The list icon of a room part for the tool: the waterfall's first frame, the bridge with its rail; null otherwise.
    internal static UnityEngine.Sprite RoomFeatureIcon(string kind) => CoreAssets.RoomFeatureIcon(kind);

    private static string ReviveCall(string unitGuid, Func<Guid, string> call)
    {
        if (!Guid.TryParse(unitGuid, out var unit) || unit == Guid.Empty) return ReviveService.NotFound;
        try { return call(unit); }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_REVIVE_API_FAILED: " + ex);
            return ReviveService.Error;
        }
    }

    private static bool LoadedTestCampaign()
    {
        var data = CoreSaveStore.Current;
        return data != null && CampaignPresence.IsTestCampaign(data.Campaign);
    }

    private static bool TryCampaign(string campaignGuid, out Guid campaign)
        => Guid.TryParse(campaignGuid, out campaign) && campaign != Guid.Empty;
}
