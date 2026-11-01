using DungeonSettlersDelvers.Core;
using static TestKit;

// The plain list of Core's texts, the registered rows and the check of their ten languages.
internal static class CoreTextRowsSection
{
    internal static void Run()
    {
        RegisteredRows();
        ListedRows();
        ProbedEntries();
        CheckedSources();
        LanguagesAndDashes();
        HyphensInCoveredSources();
        RulesOnBrokenTexts();
        GuideTopics();
        ComfortOptions();
    }

    // The comfort options of the Delvers menu: three player settings, their own keys, every text in ten languages.
    static void ComfortOptions()
    {
        var texts = QolText.All.ToList();
        Check(texts.Count == 4 && texts.Select(t => t.Key).Distinct().Count() == 4
                && texts.All(t => t.Text.AllLanguages.Count == 10 && t.Text.AllLanguages.All(s => !string.IsNullOrWhiteSpace(s)))
                && CoreTextRows.Sources.Count(source => source.Key.StartsWith("qol.", StringComparison.Ordinal)) == 4
                && QolText.ShieldBandKey == "qol.shield-band" && QolText.TacticalKey == "qol.tactical" && QolText.SummonFollowKey == "qol.summons-follow-target"
                && new[] { QolText.ShieldBandKey, QolText.TacticalKey, QolText.SummonFollowKey }.Distinct().Count() == 3,
            "comfort options: shield band, tactical formation and summons following the mage's target as player settings with their own keys, four texts in ten languages");
    }

    // The Delvers topics of the game's guide: numbers far past the game's own GuideTypes, the keys the guide
    // builds from them, both rows registered right after the hints, a picture of the game's guide each, no "##" (the guide drops it).
    static void GuideTopics()
    {
        var topics = GuideTopicText.Topics;
        var keys = CoreTextRows.Registered.Select(row => row.Key).ToList();
        var lastHint = keys.IndexOf(FirstHintText.All.Last().Name);
        var guide = GuideTopicText.All.ToList();
        Check(topics.Length == 10 && topics.Select(t => t.Id).Distinct().Count() == 10 && topics.All(t => t.Id >= 9000)
                && topics[0].Id == GuideTopicText.First && GuideTopicText.NameKey(9003) == "TEXTKEY_GAMEGUIDE_9003_NAME"
                && GuideTopicText.DescKey(9003) == "TEXTKEY_GAMEGUIDE_9003_DESC"
                && guide.Count == 20 && guide.Select((row, i) => keys.IndexOf(row.Name) == lastHint + 1 + i).All(ok => ok)
                && GuideTopicText.ImageKey("9008") == "UnitBehaviorOptions" && topics[4].Id == 9009 && topics[3].Id == 9003
                && GuideTopicText.ImageKey("9009") == "DeadUnitTreatment",
            "guide topics: ten Delvers topics past the game's own, After a fall right after Revival, the formation last, their name and text rows registered right after the hints");
        var texts = topics.SelectMany(t => new[] { t.Title, t.Text }).ToList();
        Check(texts.All(t => new[] { t.English, t.Korean, t.French, t.German, t.Russian, t.ChineseSimplified, t.ChineseTraditional,
                    t.Japanese, t.Spanish, t.PortugueseBrazil }.All(s => !string.IsNullOrWhiteSpace(s) && !s.Contains("##")))
                && GuideTopicText.ImageKey("9004") == "PauseInCombat" && GuideTopicText.ImageKey("9000") == "Health"
                && GuideTopicText.ImageKey("Health") == null && GuideTopicText.ImageKey("8999") == null
                && GuideTopicText.IdOf("9004") == 9004 && GuideTopicText.IdOf("PauseInCombat") == null && GuideTopicText.IdOf(null) == null
                && GuideTopicText.IsTopic(9008) && GuideTopicText.IsTopic(9009) && !GuideTopicText.IsTopic(52) && !GuideTopicText.IsTopic(9010),
            "guide topics: every title and text in all ten languages, no page marks, a game guide picture per topic and none for other keys, the guide's selection and its memory know Core's topics only");
        Check(GuideTopicText.ImageFile(9000) == "Guide_9000.png" && GuideTopicText.ImageFile(9008) == "Guide_9008.png"
                && GuideTopicText.ImageWidth == 796 && GuideTopicText.ImageHeight == 284,
            "guide topics: a topic may bring its own picture Guide_<number>.png at 796 x 284, else it keeps the game's picture");
    }

    // The rows the game's text table gets: distinct keys, in the registration order.
    static void RegisteredRows()
    {
        var keys = CoreTextRows.Registered.Select(row => row.Key).ToList();
        Check(keys.Count > 0 && keys.All(key => !string.IsNullOrWhiteSpace(key)) && keys.Distinct().Count() == keys.Count,
            "text rows: every registered row has its own key");
        Check(keys[0] == UniqueCandidateLocalizationText.NativeLockConflictKey
                && keys[1] == LoadIntegrityNoticeText.SaveBlockedKey
                && keys[^1] == TacticalFormationText.DescKey,
            "text rows: the registration order starts with the lock conflict and the load notices and ends with the formation tooltip");
    }

    // The rows of the enemy abilities and of the first hints are made from their own lists: the same keys and texts in the
    // order of the lists, the abilities first and the hints right behind them.
    static void ListedRows()
    {
        var registered = CoreTextRows.Registered.ToList();
        var abilities = EnemyAbilityText.All.ToList();
        var hints = FirstHintText.All.ToList();
        var abilityStart = registered.FindIndex(row => row.Key == abilities[0].Name);
        Check(abilityStart >= 0 && HoldsRun(registered, abilityStart, abilities)
                && HoldsRun(registered, abilityStart + abilities.Count, hints),
            "text rows: the enemy ability rows and the hint rows are the rows of their lists, in the order of the lists");
    }

    static bool HoldsRun(List<(string Key, UniqueCandidateTranslations Text)> registered, int start,
        List<(string Name, UniqueCandidateTranslations Text)> list)
    {
        if (start + list.Count > registered.Count) return false;
        for (var i = 0; i < list.Count; i++)
            if (registered[start + i].Key != list[i].Name || !ReferenceEquals(registered[start + i].Text, list[i].Text)) return false;
        return true;
    }

    // A new entry that stands only in the list of the abilities or of the hints is registered, right behind the last
    // entry of its list, and the other rows keep their order.
    static void ProbedEntries()
    {
        var probe = EnemyAbilityText.SilenceName;
        var ability = ("TEXTKEY_Probe_Ability", probe);
        var hint = ("TEXTKEY_Probe_Hint", probe);
        var rows = CoreTextRows.RegisteredWith(EnemyAbilityText.All.Append(ability), FirstHintText.All.Append(hint));
        var keys = rows.Select(row => row.Key).ToList();
        var normal = CoreTextRows.Registered.Select(row => row.Key).ToList();
        Check(rows.Length == normal.Count + 2
                && keys.IndexOf(ability.Item1) == keys.IndexOf(EnemyAbilityText.All.Last().Name) + 1
                && keys.IndexOf(hint.Item1) == keys.IndexOf(FirstHintText.All.Last().Name) + 1
                && keys.Where(key => key != ability.Item1 && key != hint.Item1).SequenceEqual(normal),
            "text rows: a new entry only in the list of the enemy abilities or of the hints is registered, the other rows keep their order");
    }

    // The check reads the registered rows and the words every text source holds, the menu words included.
    static void CheckedSources()
    {
        var checkedTexts = CoreTextRows.Checked.Select(row => row.Text).ToList();
        var sourceTexts = new[]
        {
            EnemyAbilityText.SilenceName, EnemyAbilityText.SleepDescription, FirstHintText.WoundsDescription,
            FirstHintText.MenuToggle, FirstHintText.MenuHint, CoreUnitRules.BladeName, CoreUnitRules.HealerDescription,
            BanditBossBarRules.Name, LifebloomRules.SkillDescription, CorpseText.LeaveWarning,
            ApocalypseText.NoPauseHint, DelversMenuText.Close,
            GroupAiText.Notice, ReviveText.BurialNotice, UniqueCandidateLocalizationText.LockConflictFallback
        };
        Check(sourceTexts.All(checkedTexts.Contains) && CoreTextRows.Registered.All(row => checkedTexts.Contains(row.Text)),
            "text rows: the check covers every registered row, the formerly unchecked sources and the menu words");
        // The language check of the game (ValidateAllLanguages) reads this list: a hint text of FirstHintText.All is in it, so a dash or a
        // missing language in a new hint is thrown there, not found by a player.
        var hints = FirstHintText.All.Select(entry => entry.Text).ToList();
        Check(hints.Count > 0 && hints.All(checkedTexts.Contains) && CoreTextRows.Checked.Select(row => row.Key).Contains(FirstHintText.DescKey(FirstHintText.Wounds)),
            "text rows: the language check of the game reads every text of the first hints");
    }

    // Every checked row has ten non-empty languages and no dash; a broken row would be named by its key.
    static void LanguagesAndDashes()
    {
        var problems = CoreTextRules.Problems(CoreTextRows.Checked).ToList();
        Check(problems.Count == 0, "text rows: ten non-empty languages and no dash in every row" + Named(problems));
    }

    // Only the sources listed here are held to the hyphen rule.
    static void HyphensInCoveredSources()
    {
        var covered = DelversMenuText.All.Concat(QolText.All).Concat(WoundText.All).Concat(BandageText.All).Concat(LifebloomBookRules.All)
            .Concat(BanditSerratedRules.All).Concat(GroupAiText.All).Concat(EnemyAbilityText.All).Concat(FirstHintText.All)
            .Concat(LoadIntegrityNoticeText.All).Concat(GuideTopicText.All)
            .Select(entry => entry.Text)
            .Concat(new[]
            {
                UniqueCandidateLocalizationText.LockConflictFallback, FirstHintText.MenuToggle, FirstHintText.MenuHint,
                CoreUnitRules.BladeName, CoreUnitRules.GuardName, CoreUnitRules.ArcherName, CoreUnitRules.HealerName,
                CoreUnitRules.BladeDescription, CoreUnitRules.GuardDescription, CoreUnitRules.ArcherDescription,
                CoreUnitRules.HealerDescription, BanditBossBarRules.Name, LifebloomRules.Name, LifebloomRules.SkillDescription,
                LifebloomRules.EffectDescription, CorpseText.Undress, CorpseText.UndressFirst, CorpseText.LeaveWarning,
                ReviveText.ScrollFlavor, ApocalypseText.NoPauseHint
            })
            .ToList();
        var rows = CoreTextRows.Checked.Where(row => covered.Contains(row.Text)).ToList();
        var problems = CoreTextRules.HyphenProblems(rows).ToList();
        Check(covered.All(text => rows.Any(row => row.Text == text)) && problems.Count == 0,
            "text rows: no hyphen in the covered text sources" + Named(problems));
    }

    // The rules themselves: nine languages, a dash or a hyphen make a text a problem, a clean text does not.
    static void RulesOnBrokenTexts()
    {
        var clean = Enumerable.Range(0, CoreTextRules.LanguageCount).Select(index => "Text " + index).ToList();
        var nine = clean.Take(CoreTextRules.LanguageCount - 1).ToList();
        var empty = clean.Select((text, index) => index == 3 ? " " : text).ToList();
        var dash = clean.Select((text, index) => index == 4 ? "Text – text" : text).ToList();
        var hyphen = clean.Select((text, index) => index == 5 ? "Text-text" : text).ToList();
        Check(!CoreTextRules.Problems("clean", clean).Any() && !CoreTextRules.HyphenProblems("clean", clean).Any(),
            "text rules: ten clean languages are no problem");
        Check(CoreTextRules.Problems("nine", nine).Any() && CoreTextRules.Problems("empty", empty).Any(),
            "text rules: nine languages or an empty one are a problem");
        Check(CoreTextRules.Problems("dash", dash).Any() && !CoreTextRules.HyphenProblems("dash", dash).Any()
                && CoreTextRules.HyphenProblems("hyphen", hyphen).Any() && !CoreTextRules.Problems("hyphen", hyphen).Any(),
            "text rules: an en dash is a dash problem, a hyphen is a hyphen problem");
    }

    static string Named(List<string> problems) => problems.Count == 0 ? "" : " (" + string.Join("; ", problems) + ")";
}
