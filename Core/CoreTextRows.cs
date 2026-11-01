namespace DungeonSettlersDelvers.Core;

// Core's texts as one plain list. UniqueCandidateLocalization registers the rows in this order in the game's
// text table; CoreTextRules checks the same list. A new text row is added here, except the enemy abilities' and the first
// hints' rows, which are added to their own lists.
internal static class CoreTextRows
{
    // The rows of the game's text table: key and the ten languages. The rows of the enemy abilities and of the first hints
    // come from their own lists (EnemyAbilityText.All, FirstHintText.All) between the two parts below.
    internal static readonly (string Key, UniqueCandidateTranslations Text)[] Registered =
        RegisteredWith(EnemyAbilityText.All, FirstHintText.All);

    // The registration order: the rows before the enemy abilities, the abilities, the hints, the rows after them. A new entry
    // of either list is registered here without a second line. The two lists are parameters so a test can probe a new entry.
    internal static (string Key, UniqueCandidateTranslations Text)[] RegisteredWith(
        IEnumerable<(string Name, UniqueCandidateTranslations Text)> enemyAbilities,
        IEnumerable<(string Name, UniqueCandidateTranslations Text)> firstHints)
    {
        var rows = new List<(string Key, UniqueCandidateTranslations Text)>(RowsBeforeEnemyAbilities());
        // The enemy abilities' effects (EnemyAbilities).
        foreach (var (key, text) in enemyAbilities) rows.Add((key, text));
        // The first hints (FirstHints): a title and one sentence each, titles shared with the menu.
        foreach (var (key, text) in firstHints) rows.Add((key, text));
        // The Delvers topics of the game's guide (GuideTopics).
        foreach (var (key, text) in GuideTopicText.All) rows.Add((key, text));
        rows.AddRange(RowsAfterFirstHints());
        return rows.ToArray();
    }

    private static (string Key, UniqueCandidateTranslations Text)[] RowsBeforeEnemyAbilities() => new[]
    {
        (UniqueCandidateLocalizationText.NativeLockConflictKey,
            UniqueCandidateLocalizationText.LockConflictFallback),
        (LoadIntegrityNoticeText.SaveBlockedKey, LoadIntegrityNoticeText.SaveBlocked),
        (LoadIntegrityNoticeText.SaveBlockedShortKey, LoadIntegrityNoticeText.SaveBlockedShort),
        (ApocalypseText.TitleKey, ApocalypseText.TitleColoured),
        (ApocalypseText.CombatDescriptionKey, ApocalypseText.CombatDescription),
        (ApocalypseText.ManagementDescriptionKey, ApocalypseText.ManagementDescription),
        (ReviveText.SicknessNameKey, ReviveText.SicknessName),
        (ReviveText.SicknessDescKey, ReviveText.SicknessDescription),
        (ReviveText.PanicNameKey, ReviveText.PanicName),
        (ReviveText.PanicDescKey, ReviveText.PanicDescription),
        (ReviveText.ScrollNameKey, ReviveText.ScrollName),
        (ReviveText.ScrollDescKey, ReviveText.ScrollDescription),
        (ReviveText.ScrollFlavorKey, ReviveText.ScrollFlavor),
        (WoundText.NameKey(WoundText.LightKey), WoundText.LightName),
        (WoundText.DescKey(WoundText.LightKey), WoundText.LightDescription),
        (WoundText.NameKey(WoundText.DeepKey), WoundText.DeepName),
        (WoundText.DescKey(WoundText.DeepKey), WoundText.DeepDescription),
        (WoundText.NameKey(WoundText.HeavyKey), WoundText.HeavyName),
        (WoundText.DescKey(WoundText.HeavyKey), WoundText.HeavyDescription),
        (WoundText.NameKey(WoundText.FatalKey), WoundText.FatalName),
        (WoundText.DescKey(WoundText.FatalKey), WoundText.FatalDescription),
        (BandageText.NameKey(BandageText.LightItemKey), BandageText.LightName),
        (BandageText.DescKey(BandageText.LightItemKey), BandageText.LightDescription),
        (BandageText.FlavorKey(BandageText.LightItemKey), BandageText.LightFlavor),
        (BandageText.NameKey(BandageText.HeavyItemKey), BandageText.HeavyName),
        (BandageText.DescKey(BandageText.HeavyItemKey), BandageText.HeavyDescription),
        (BandageText.FlavorKey(BandageText.HeavyItemKey), BandageText.HeavyFlavor),
        (BandageText.NameKey(BandageText.HerbalItemKey), BandageText.HerbalName),
        (BandageText.DescKey(BandageText.HerbalItemKey), BandageText.HerbalDescription),
        (BandageText.FlavorKey(BandageText.HerbalItemKey), BandageText.HerbalFlavor),
        (BandageText.NameKey(BandageText.SalveItemKey), BandageText.SalveName),
        (BandageText.DescKey(BandageText.SalveItemKey), BandageText.SalveDescription),
        (BandageText.FlavorKey(BandageText.SalveItemKey), BandageText.SalveFlavor),
        (BandageText.ResearchNameKey(BandageText.FirstAidResearchKey), BandageText.FirstAidName),
        // The inventory's option that puts an item on another character (UseOnOther; -I5: without this row the menu
        // showed the raw key).
        (BandageText.UseOnOtherKey, BandageText.UseOnOtherName),
        // The author's skill book for Lifebloom (LifebloomBook).
        (LifebloomBookRules.NameKey, LifebloomBookRules.Name),
        (LifebloomBookRules.DescKey, LifebloomBookRules.Description),
        (LifebloomBookRules.FlavorKey, LifebloomBookRules.Flavor),
        (LifebloomBookRules.LearnedNoticeKey, LifebloomBookRules.LearnedNotice),
        // The boss bandits' parts (BossItems): item name and flavour, the equipment effect's name and text.
        (BandageText.NameKey(BossItemRules.GreatSwordKey), BossItemText.GreatSwordName),
        (WoundText.NameKey(BossItemRules.EffectKey(BossItemRules.GreatSwordKey)), BossItemText.GreatSwordEffectName),
        (WoundText.DescKey(BossItemRules.EffectKey(BossItemRules.GreatSwordKey)), BossItemText.GreatSwordDescription),
        (BandageText.FlavorKey(BossItemRules.GreatSwordKey), BossItemText.GreatSwordFlavor),
        (BandageText.NameKey(BossItemRules.ShieldKey), BossItemText.ShieldName),
        (WoundText.NameKey(BossItemRules.EffectKey(BossItemRules.ShieldKey)), BossItemText.ShieldEffectName),
        (WoundText.DescKey(BossItemRules.EffectKey(BossItemRules.ShieldKey)), BossItemText.ShieldDescription),
        (BandageText.FlavorKey(BossItemRules.ShieldKey), BossItemText.ShieldFlavor),
        (BandageText.NameKey(BossItemRules.BowKey), BossItemText.BowName),
        (WoundText.NameKey(BossItemRules.EffectKey(BossItemRules.BowKey)), BossItemText.BowEffectName),
        (WoundText.DescKey(BossItemRules.EffectKey(BossItemRules.BowKey)), BossItemText.BowDescription),
        (BandageText.FlavorKey(BossItemRules.BowKey), BossItemText.BowFlavor),
        (BandageText.NameKey(BossItemRules.StaffKey), BossItemText.StaffName),
        (WoundText.NameKey(BossItemRules.EffectKey(BossItemRules.StaffKey)), BossItemText.StaffEffectName),
        (WoundText.DescKey(BossItemRules.EffectKey(BossItemRules.StaffKey)), BossItemText.StaffDescription),
        (BandageText.FlavorKey(BossItemRules.StaffKey), BossItemText.StaffFlavor),
        // The blade's Serrated Blade (BanditSerrated), drafts.
        (BanditSerratedRules.NameKey, BanditSerratedRules.Name),
        (BanditSerratedRules.DescKey, BanditSerratedRules.Description),
        // The notice when the group AI is switched on (GroupAiNotice).
        (GroupAiText.NoticeKey, GroupAiText.Notice),
        // The question before a dressed party corpse is buried or left at the base portal (CorpseBurialWarning; without this
        // row the dialog showed the raw key).
        (ReviveText.BurialNoticeKey, ReviveText.BurialNotice)
    };

    private static (string Key, UniqueCandidateTranslations Text)[] RowsAfterFirstHints() => new[]
    {
        // Core's own enemies (CoreUnits) and Lifebloom.
        (CoreUnitRules.NameKey(CoreUnitRules.Prefix + "ForestBanditBlade"), CoreUnitRules.BladeName),
        (CoreUnitRules.NameKey(CoreUnitRules.Prefix + "ForestBanditGuard"), CoreUnitRules.GuardName),
        (CoreUnitRules.NameKey(CoreUnitRules.Prefix + "ForestBanditArcher"), CoreUnitRules.ArcherName),
        (CoreUnitRules.NameKey(CoreUnitRules.Prefix + "ForestBanditHealer"), CoreUnitRules.HealerName),
        (CoreUnitRules.DescKey(CoreUnitRules.Prefix + "ForestBanditBlade"), CoreUnitRules.BladeDescription),
        (CoreUnitRules.DescKey(CoreUnitRules.Prefix + "ForestBanditGuard"), CoreUnitRules.GuardDescription),
        (CoreUnitRules.DescKey(CoreUnitRules.Prefix + "ForestBanditArcher"), CoreUnitRules.ArcherDescription),
        (CoreUnitRules.DescKey(CoreUnitRules.Prefix + "ForestBanditHealer"), CoreUnitRules.HealerDescription),
        // The weak bandits (WeakBanditRules): one plain name and description for every level and class.
    }.Concat(WeakBanditRules.TextRows()).Concat(new[]
    {
        // The forest bandits' boss bar (BanditBossBar).
        (BanditBossBarRules.NameKey, BanditBossBarRules.Name),
        (LifebloomRules.SkillNameKey, LifebloomRules.Name),
        (LifebloomRules.SkillDescKey, LifebloomRules.SkillDescription),
        (LifebloomRules.EffectNameKey, LifebloomRules.Name),
        (LifebloomRules.EffectDescKey, LifebloomRules.EffectDescription),
        // Equipment on the body (CorpseText): the undress action, its red reason, the warning when leaving.
        (CorpseText.UndressKey, CorpseText.Undress),
        (CorpseText.UndressFirstKey, CorpseText.UndressFirst),
        (CorpseText.LeaveWarningKey, CorpseText.LeaveWarning),
        // The formation button's tooltip in the option row (TacticalFormationButton).
        (TacticalFormationText.NameKey, TacticalFormationText.Name),
        (TacticalFormationText.DescKey, TacticalFormationText.Description)
    }).ToArray();

    // The lists of the text sources: registered rows and the words the menus pick straight from their source.
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> Sources => DelversMenuText.All
        .Concat(QolText.All).Concat(ApocalypseText.All).Concat(ReviveText.All).Concat(WoundText.All).Concat(BandageText.All)
        .Concat(LifebloomBookRules.All).Concat(BanditSerratedRules.All).Concat(GroupAiText.All)
        .Concat(LoadIntegrityNoticeText.All).Concat(EnemyAbilityText.All).Concat(FirstHintText.All)
        .Concat(FirstHintMenuTexts).Concat(TacticalFormationText.All);

    // The switch and the hint of the first hints in Core's menu page; FirstHintText.All holds the hint rows only.
    private static IEnumerable<(string Key, UniqueCandidateTranslations Text)> FirstHintMenuTexts => new[]
    {
        (nameof(FirstHintText) + "." + nameof(FirstHintText.MenuSection), FirstHintText.MenuSection),
        (nameof(FirstHintText) + "." + nameof(FirstHintText.MenuToggle), FirstHintText.MenuToggle),
        (nameof(FirstHintText) + "." + nameof(FirstHintText.MenuHint), FirstHintText.MenuHint)
    };

    // Every registered row and every source word: the list the check of the languages reads.
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> Checked => Registered.Concat(Sources);
}

// What a game text must satisfy, as pure functions. Problems: ten non-empty languages and no dash;
// HyphenProblems: no hyphen. Both name the key of every broken row.
internal static class CoreTextRules
{
    internal const int LanguageCount = 10;

    // Hyphen, non-breaking hyphen, figure dash, en dash, em dash, horizontal bar and minus sign.
    private static readonly char[] DashCharacters = { '‐', '‑', '‒', '–', '—', '―', '−' };

    private static readonly char[] HyphenOnly = { '-' };

    // Written as loops, without lambdas: a lambda over the rows (SelectMany, tuple parameter) needs NullableAttribute, which the
    // IL2CPP reference set lacks (CS0656).
    internal static IEnumerable<string> Problems(IEnumerable<(string Key, UniqueCandidateTranslations Text)> rows)
    {
        foreach (var (key, text) in rows)
            foreach (var problem in Problems(key, text.AllLanguages)) yield return problem;
    }

    internal static IEnumerable<string> Problems(string key, IReadOnlyList<string> languages)
    {
        if (languages.Count != LanguageCount) yield return "Core text without ten languages: " + key;
        if (AnyEmpty(languages)) yield return "Core text with an empty language: " + key;
        if (AnyContainingAny(languages, DashCharacters)) yield return "Core text with a dash: " + key;
    }

    internal static IEnumerable<string> HyphenProblems(IEnumerable<(string Key, UniqueCandidateTranslations Text)> rows)
    {
        foreach (var (key, text) in rows)
            foreach (var problem in HyphenProblems(key, text.AllLanguages)) yield return problem;
    }

    internal static IEnumerable<string> HyphenProblems(string key, IReadOnlyList<string> languages)
    {
        if (AnyContainingAny(languages, HyphenOnly)) yield return "Core text with a hyphen: " + key;
    }

    private static bool AnyEmpty(IReadOnlyList<string> languages)
    {
        foreach (var language in languages)
            if (string.IsNullOrWhiteSpace(language)) return true;
        return false;
    }

    private static bool AnyContainingAny(IReadOnlyList<string> languages, char[] characters)
    {
        foreach (var language in languages)
            if (language != null && language.IndexOfAny(characters) >= 0) return true;
        return false;
    }
}
