using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Core's switches in the Delvers menu, stored per game copy in
// <persistentDataPath>\DungeonSettlersDelvers\Core\copies\<id>\settings.json (CoreSettingsCopyRules). Default is the
// behaviour before the switches existed: every founder may be rolled.
internal static class CoreSettings
{
    private static CoreSettingsFile file;
    private static bool loadErrorReported;

    internal static bool IsFounderEnabled(NativeFounders.Entry entry)
        => entry != null && Get(FounderKey(entry), true);

    internal static void SetFounderEnabled(NativeFounders.Entry entry, bool enabled)
    {
        if (entry == null) return;
        Set(FounderKey(entry), enabled);
        DelversHost.Info($"CORE_SETTING founder={entry.PresenceId} enabled={enabled.ToString().ToLowerInvariant()}");
    }

    internal static string FounderKey(NativeFounders.Entry entry) => "founders." + entry.PresenceId + ".enabled";

    // Offers Apocalypse on the campaign screen. Off by default; existing campaigns keep theirs.
    internal const string ApocalypseKey = "apocalypse.offered";

    internal static bool ApocalypseAvailable => Get(ApocalypseKey, false);

    internal static void SetApocalypseAvailable(bool available)
    {
        Set(ApocalypseKey, available);
        DelversHost.Info($"CORE_SETTING apocalypse={available.ToString().ToLowerInvariant()}");
    }

    // Apocalypse refuses the player's pause and offers a short slow motion instead (ApocalypseTime).
    // On by default; the switch is for players who want Apocalypse without that rule.
    internal const string NoPauseKey = "apocalypse.no-pause";

    internal static bool ApocalypseNoPause => Get(NoPauseKey, true);

    internal static void SetApocalypseNoPause(bool on)
    {
        Set(NoPauseKey, on);
        DelversHost.Info($"CORE_SETTING apocalypse-no-pause={on.ToString().ToLowerInvariant()}");
    }

    // Single Apocalypse rules for campaigns without Apocalypse (ApocalypseRules), each off by
    // default and read when a campaign is loaded or created. Only the names in
    // ApocalypseOptionRules exist.
    internal static string OptionKey(string name) => "rules." + name;

    internal static bool Option(string name) => ApocalypseOptionRules.IsOption(name) && Get(OptionKey(name), false);

    internal static bool SetOption(string name, bool on)
    {
        if (!ApocalypseOptionRules.IsOption(name)) return false;
        Set(OptionKey(name), on);
        DelversHost.Info($"CORE_OPTION name={name} value={on.ToString().ToLowerInvariant()} scope=global");
        return true;
    }

    // The first hints (FirstHints): on by default; each hint shows once, and that is kept here.
    internal const string HintsKey = "hints.enabled";

    internal static bool HintsEnabled => Get(HintsKey, true);

    // The room graph probe of the bandits' camp (BanditCampProbe): only logs; off by default, set by hand.
    internal static bool CampProbeEnabled => Get(BanditCampProbe.EnabledKey, false);

    // The diagnostics groups (CoreProbeRules): one key each, off by default, set by hand or by a test run. A probe only logs.
    internal static List<string> ProbeGroupsOn() => CoreProbeRules.OnGroups(Get);

    // Bandits attack fallen player units as before (always) instead of only last (GroupAiRules.FinishReason).
    internal static bool FinishDownedAlways => Get(GroupAiRules.FinishDownedAlwaysKey, false);
    // Bandits choose only the basic attack, in a marked test campaign (GroupAiRules.BasicOnlyActive); off by default.
    internal static bool BanditsBasicOnly => Get(GroupAiRules.BasicOnlyKey, false);
    // The cone sparing of the greatsword (BanditConeRules), off by default.
    internal static bool SpareDownedInCone => Get(BanditConeRules.SpareKey, false);
    // A fallen unit in a skill's own area no longer rules it out; the more damaging choice counts (BanditAreaRules).
    internal static bool AreaIgnoresDowned => Get(BanditAreaRules.IgnoresDownedKey, true);

    // One bandit fighting wakes the band; on by default, switchable without a build.
    internal static bool GroupWake => Get(GroupAiRules.GroupWakeKey, true);

    // The display of Apocalypse's slow motion (ApocalypseSlowDisplay, read once per campaign): bar and
    // effects on by default; the rim's strength from 0 (off) to 1, 0.42 by default (B2).
    internal static bool SlowBarEnabled => Get(ApocalypseSlowRules.BarKey, true);

    // The pool from a deep wound on only after the unit stood still this long (WoundRules.PoolStill).
    internal static double PoolStillSeconds => WoundRules.PoolStill(Number(WoundRules.PoolStillKey));

    internal static double SlowRimStrength => ApocalypseSlowRules.RimStrength(Number(ApocalypseSlowRules.RimKey));

    internal static void SetHintsEnabled(bool on)
    {
        Set(HintsKey, on);
        DelversHost.Info($"CORE_SETTING hints={on.ToString().ToLowerInvariant()}");
    }

    internal static bool HintShown(string hint) => Get("hints.shown." + hint, false);
    internal static void MarkHintShown(string hint) => Set("hints.shown." + hint, true);
    internal static void ForgetHint(string hint) => Set("hints.shown." + hint, false);

    // The party's group AI (GroupAiSettings), off by default (a player option later, as a small Core
    // patch; rather an accessibility feature, locked from Very Hard up, GroupAiLockRules).
    // Core's own enemies and Lifebloom have no switch any more; ApocalypseRules.Bandits decides.
    internal static bool GroupAiEnabled => Get(GroupAiRules.EnabledKey, false);
    internal static bool GroupAiAddsFirst => Get(GroupAiRules.AddsFirstKey, true);

    // The group AI's menu section; both off by default. Switching the AI on from off shows the notice.
    internal static bool GroupAiBandagesFromBag => Get(GroupAiBandageRules.FromBagKey, false);

    internal static void SetGroupAiEnabled(bool on)
    {
        var before = GroupAiEnabled;
        Set(GroupAiRules.EnabledKey, on);
        DelversHost.Info($"CORE_SETTING groupai={on.ToString().ToLowerInvariant()}");
        GroupAiPolicy.ResetCampaign();
        if (GroupAiNotice.Due(before, on)) GroupAiNotice.Show();
    }

    internal static void SetGroupAiBandagesFromBag(bool on)
    {
        Set(GroupAiBandageRules.FromBagKey, on);
        DelversHost.Info($"CORE_SETTING groupai-bandages-from-bag={on.ToString().ToLowerInvariant()}");
    }

    // The lethal wound (WoundRules): only from heavy on (on by default); herbal and salve take a
    // share of it unless this closes it (off by default).
    internal static bool FatalFromHeavyOnly => Get(WoundRules.FromHeavyOnlyKey, true);
    internal static bool FatalBandageCloses => Get(WoundRules.FatalBandageClosesKey, false);

    // The game's healers and revivers count as healers in the party's focus (GroupAiFocus).
    internal static bool FocusNativeHealers => Get(GroupAiRules.FocusNativeHealersKey, true);

    // Merchants offer the revive scroll (ReviveScroll). On by default.
    internal const string ReviveScrollKey = "revive.scroll.offered";

    internal static bool ReviveScrollOffered => Get(ReviveScrollKey, true);

    // The comfort options (QolText), on by default: the shield band over the health bars and the tactical formation with its
    // priority places. Read every frame or look, so a change acts at once.
    internal static bool ShieldBandShown => Get(QolText.ShieldBandKey, true);

    internal static void SetShieldBandShown(bool on)
    {
        Set(QolText.ShieldBandKey, on);
        DelversHost.Info($"CORE_SETTING shield-band={on.ToString().ToLowerInvariant()}");
    }

    internal static bool TacticalEnabled => Get(QolText.TacticalKey, true);

    internal static void SetTacticalEnabled(bool on)
    {
        Set(QolText.TacticalKey, on);
        DelversHost.Info($"CORE_SETTING tactical={on.ToString().ToLowerInvariant()}");
    }

    // A party water mage's bubblelings attack the mage's target (SummonFollowTarget); a bandit's do always.
    internal static bool SummonsFollowTarget => Get(QolText.SummonFollowKey, true);

    internal static void SetSummonsFollowTarget(bool on)
    {
        Set(QolText.SummonFollowKey, on);
        DelversHost.Info($"CORE_SETTING summons-follow-target={on.ToString().ToLowerInvariant()}");
    }

    // The warning before burying a dressed party corpse (CorpseBurialWarning), on by default.
    internal static bool BurialWarning => Get(CorpseRules.BurialWarningKey, true);

    internal static void SetBurialWarning(bool on)
    {
        Set(CorpseRules.BurialWarningKey, on);
        DelversHost.Info($"CORE_SETTING burial-warning={on.ToString().ToLowerInvariant()}");
    }

    internal static void SetReviveScrollOffered(bool on)
    {
        Set(ReviveScrollKey, on);
        DelversHost.Info($"CORE_SETTING revive-scroll={on.ToString().ToLowerInvariant()}");
        try { ReviveScroll.ApplyMerchantSetting(); }
        catch (Exception ex) { DelversHost.Warning("CORE_REVIVE_SCROLL_SETTING_FAILED: " + ex.Message); }
    }

    internal static void ResetLifecycle()
    {
        file = null;
        loadErrorReported = false;
    }

    private static bool Get(string key, bool fallback)
    {
        try
        {
            var settings = File();
            var value = settings.Get(key, fallback);
            if (settings.LoadError != null && !loadErrorReported)
            {
                loadErrorReported = true;
                DelversHost.Warning("CORE_SETTINGS_UNREADABLE defaults=active: " + settings.LoadError);
            }
            for (var line = settings.TakeTypeWarning(); line != null; line = settings.TakeTypeWarning()) DelversHost.Warning(line);
            return value;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SETTINGS_READ_FAILED: " + ex.Message);
            return fallback;
        }
    }

    // A number from the file, null when it is missing or unreadable; the caller keeps its own
    // default and range (CoreUnits).
    internal static double? Number(string key)
    {
        try
        {
            var settings = File();
            var value = settings.GetNumber(key);
            if (settings.LoadError != null && !loadErrorReported)
            {
                loadErrorReported = true;
                DelversHost.Warning("CORE_SETTINGS_UNREADABLE defaults=active: " + settings.LoadError);
            }
            return value;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_SETTINGS_READ_FAILED: " + ex.Message);
            return null;
        }
    }

    private static void Set(string key, bool value)
    {
        try { File().Set(key, value); }
        catch (Exception ex)
        {
            // The value stays active for this session; only saving it failed.
            DelversHost.Error("CORE_SETTINGS_WRITE_FAILED key=" + key + ": " + ex);
        }
    }

    // The file of this game copy (CoreSettingsCopyRules), prepared once a session.
    internal static string FilePath
    {
        get
        {
            if (copyPath != null) return copyPath;
            var root = Application.persistentDataPath;
            var game = GameFolder();
            try
            {
                var (path, id, migrated) = CoreSettingsCopyRules.Prepare(root, game);
                copyPath = path;
                copyId = id;
                DelversHost.Info(CoreSettingsCopyRules.Line(id, game, migrated));
            }
            catch (Exception ex)
            {
                // The copy's file stays the target; only its folder, note or first values failed. Defaults stay active.
                copyPath = CoreSettingsCopyRules.CopyPath(root, game);
                copyId = CoreSettingsCopyRules.CopyId(game);
                DelversHost.Warning("CORE_SETTINGS_COPY_FAILED id=" + (copyId ?? "-") + ": " + ex.Message);
            }
            return copyPath;
        }
    }

    internal static string CopyId
    {
        get
        {
            _ = FilePath;
            return copyId;
        }
    }

    private static string copyPath, copyId;

    private static string GameFolder()
    {
        try
        {
            var data = Application.dataPath;
            return string.IsNullOrWhiteSpace(data) ? null : Path.GetDirectoryName(Path.GetFullPath(data));
        }
        catch { return null; }
    }

    private static CoreSettingsFile File()
    {
        var path = FilePath;
        if (file == null || !string.Equals(file.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            file = new CoreSettingsFile(path);
        return file;
    }
}
