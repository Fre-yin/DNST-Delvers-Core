namespace DungeonSettlersDelvers.Core;

// Core's own page in the Delvers menu: what is installed, whether saving is allowed, and which
// mods offer their settings here. Later the Hotbar's key profile belongs on this page too.
internal static class DelversCoreMenuPage
{
    internal const string PackId = "delvers-core";
    internal const string Title = "Delvers Core";

    internal static IEnumerable<IReadOnlyDictionary<string, object>> Rows()
    {
        yield return DelversMenuRows.Section(Title);
        yield return DelversMenuRows.Text(() =>
            $"Version {CoreVersion} | API {DelversCoreRuntime.ApiVersion} | {DelversCoreRuntime.LoaderProfile}");
        yield return DelversMenuRows.Text(() => MenuText.Pick(DelversMenuText.VerifiedGame) + GameVersion, muted: true);
        yield return DelversMenuRows.Text(() =>
            MenuText.Pick(DelversCoreRuntime.IsSaveAllowed ? DelversMenuText.SaveAllowed : DelversMenuText.SaveBlocked));
        // Founders can be switched off one by one (Core without the
        // founders, or only some of them), side by side in one row.
        // Recruited founders are not affected.
        yield return DelversMenuRows.Section(MenuText.Pick(DelversMenuText.Founders));
        yield return DelversMenuRows.Text(() => MenuText.Pick(DelversMenuText.FoundersHint), muted: true);
        yield return DelversMenuRows.Toggles(NativeFounders.All.Select(FounderToggle).ToList());
        // Apocalypse above the game's highest level.
        // No explaining lines under the switches; what each rule does stands in the game's guide (GuideTopicText).
        yield return DelversMenuRows.Section(MenuText.Pick(ApocalypseText.MenuSection));
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.Title),
            () => CoreSettings.ApocalypseAvailable, CoreSettings.SetApocalypseAvailable);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.NoPause),
            () => CoreSettings.ApocalypseNoPause, CoreSettings.SetApocalypseNoPause);
        // The Apocalypse options for other difficulties (ApocalypseRules), read when a campaign is loaded or created.
        yield return DelversMenuRows.Section(MenuText.Pick(ApocalypseText.RulesSection));
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.RulesWounds),
            () => CoreSettings.Option(ApocalypseOptionRules.Wounds), SetWoundsOption);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.RulesNoPause),
            () => CoreSettings.Option(ApocalypseOptionRules.NoPause), SetNoPauseOption);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.RulesInstantCombat),
            () => CoreSettings.Option(ApocalypseOptionRules.InstantCombat), SetInstantCombatOption);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.RulesEnemyAbilities),
            () => CoreSettings.Option(ApocalypseOptionRules.EnemyAbilities), SetEnemyAbilitiesOption);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(BanditBossBarRules.Name),
            () => CoreSettings.Option(ApocalypseOptionRules.ForestBandits), SetBanditsOption);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ApocalypseText.RulesWeakBandits),
            () => CoreSettings.Option(ApocalypseOptionRules.SmallerBandits), SetWeakBanditsOption);
        // The comfort options, player settings of this game copy, on by default.
        yield return DelversMenuRows.Section(MenuText.Pick(QolText.Section));
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(QolText.ShieldBand), () => CoreSettings.ShieldBandShown, CoreSettings.SetShieldBandShown);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(QolText.Tactical), () => CoreSettings.TacticalEnabled, CoreSettings.SetTacticalEnabled);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(QolText.SummonFollow), () => CoreSettings.SummonsFollowTarget, CoreSettings.SetSummonsFollowTarget);
        // The first hints (FirstHints), in every campaign.
        yield return DelversMenuRows.Section(MenuText.Pick(FirstHintText.MenuSection));
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(FirstHintText.MenuToggle),
            () => CoreSettings.HintsEnabled, CoreSettings.SetHintsEnabled);
        yield return DelversMenuRows.Text(() => MenuText.Pick(FirstHintText.MenuHint), muted: true);
        // The revive scroll at merchants.
        yield return DelversMenuRows.Section(MenuText.Pick(ReviveText.MenuSection));
        yield return DelversMenuRows.Text(() => MenuText.Pick(ReviveText.MenuScrollHint), muted: true);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ReviveText.MenuScroll),
            () => CoreSettings.ReviveScrollOffered, CoreSettings.SetReviveScrollOffered);
        // The warning before burying a party member in its equipment (CorpseBurialWarning).
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(ReviveText.MenuBurialWarning),
            () => CoreSettings.BurialWarning, CoreSettings.SetBurialWarning);
        // The party's group AI: rather an accessibility feature, locked from Very Hard up; both off by
        // default, the notice on switching it on (CoreSettings.SetGroupAiEnabled).
        yield return DelversMenuRows.Section(MenuText.Pick(GroupAiText.MenuSection));
        yield return DelversMenuRows.Text(() => MenuText.Pick(GroupAiText.MenuHint), muted: true);
        yield return DelversMenuRows.Text(() => MenuText.Pick(GroupAiText.MenuLocked), visible: GroupAiLocked, warning: true);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(GroupAiText.MenuEnabled),
            () => CoreSettings.GroupAiEnabled, CoreSettings.SetGroupAiEnabled, enabled: GroupAiOpen);
        yield return DelversMenuRows.Toggle(() => MenuText.Pick(GroupAiText.MenuBandagesFromBag),
            () => CoreSettings.GroupAiBandagesFromBag, CoreSettings.SetGroupAiBandagesFromBag, enabled: GroupAiOpen);
        yield return DelversMenuRows.Section(MenuText.Pick(DelversMenuText.Mods));
        yield return DelversMenuRows.Text(() =>
        {
            var others = DelversCoreRuntime.MenuPages.Snapshot()
                .Where(page => page.PackId != PackId).Select(page => page.Title).ToList();
            return others.Count == 0 ? MenuText.Pick(DelversMenuText.NoOtherMods) : string.Join(", ", others);
        });
    }

    // The running campaign's difficulty locks the group AI's switches (GroupAiLockRules); outside a campaign they stay open.
    private static bool GroupAiLocked() => GroupAiSettings.Locked;
    private static bool GroupAiOpen() => !GroupAiSettings.Locked;

    private static void SetWoundsOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.Wounds, on);
    private static void SetBanditsOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.ForestBandits, on);
    private static void SetWeakBanditsOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.SmallerBandits, on);
    private static void SetNoPauseOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.NoPause, on);
    private static void SetInstantCombatOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.InstantCombat, on);
    private static void SetEnemyAbilitiesOption(bool on) => CoreSettings.SetOption(ApocalypseOptionRules.EnemyAbilities, on);

    // The founder's name as the game writes it in the current language.
    private static IReadOnlyDictionary<string, object> FounderToggle(NativeFounders.Entry entry)
        => DelversMenuRows.Toggle(() => FounderName(entry),
            () => CoreSettings.IsFounderEnabled(entry), enabled => CoreSettings.SetFounderEnabled(entry, enabled));

    private static string FounderName(NativeFounders.Entry entry)
    {
        try
        {
            var table = DataSheetManager.Instance?._text?._textTable;
            if (table != null && table.TryGetValue(entry.NameTextKey, out var name) && !string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch { /* The English name below is always readable. */ }
        return entry.Name;
    }

    private static string CoreVersion
    {
        get
        {
            var version = typeof(DelversCoreRuntime).Assembly.GetName().Version;
            return version == null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    private static string GameVersion
    {
        get
        {
            var builds = DelversCoreRuntime.SupportedBuilds;
            var cut = builds.IndexOf(" /", StringComparison.Ordinal);
            return cut > 0 ? builds.Substring(0, cut) : builds;
        }
    }
}
