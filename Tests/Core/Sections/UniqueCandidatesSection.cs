using DungeonSettlersDelvers.Core;
using static TestKit;

// Unique candidates: founder recognition, guild price cap, reroll locks and the reroll button.
internal static class UniqueCandidatesSection
{
    const UniqueCandidateLockPolicy FounderLocks = UniqueCandidateLockPolicy.PrimaryTraits;
    const UniqueCandidateLockPolicy PackUniqueLocks = UniqueCandidateLockPolicy.ProfileOrPrimaryTraits;
    const UniqueCandidateLockPolicy NoUniqueLocks = UniqueCandidateLockPolicy.None;

    const string NativeRefreshEnglish = "Recruit again";
    const string NativeRefreshGerman = "Erneut rekrutieren";
    const string NativeFailureText = "Native generation failure";

    internal static void Run()
    {
        FounderRecognition();
        LockPolicy();
        RerollButton();
        NativeFounderReplacement();
    }

    static void FounderRecognition()
    {
        Check(UniqueCandidateRules.IsBuiltInUniqueCandidate(UniqueCandidateRules.Lowell),
            "Lowell is recognized by his stable profile and unique background trait");
        Check(UniqueCandidateRules.IsBuiltInUniqueCandidate(UniqueCandidateRules.Liana),
            "Liana is recognized by her stable profile and unique background trait");
        Check(UniqueCandidateRules.IsBuiltInUniqueCandidate(UniqueCandidateRules.Kragas),
            "Kragas is recognized by his stable profile and unique background trait");
        foreach (var founder in new[] { UniqueCandidateRules.Lowell, UniqueCandidateRules.Liana, UniqueCandidateRules.Kragas })
            Check(UniqueCandidateRules.IsBuiltInUniqueCandidate(new UniqueCandidateIdentity(
                founder.ProfileKey, "TEXTKEY_PLAYERNAME_Custom", founder.BackgroundTrait)),
                $"renamed founder with profile {founder.ProfileKey} remains unique");
        var ordinaryLowellLikeCandidate = OrdinaryLowellLikeCandidate();
        Check(!UniqueCandidateRules.IsBuiltInUniqueCandidate(ordinaryLowellLikeCandidate),
            "same profile and name with a normal background remains an ordinary candidate");
        var foreignLowellBackgroundCandidate = new UniqueCandidateIdentity("UNITVISUAL_OtherProfile_0",
            UniqueCandidateRules.Lowell.NameTextKey, UniqueCandidateRules.Lowell.BackgroundTrait);
        Check(!UniqueCandidateRules.IsBuiltInUniqueCandidate(foreignLowellBackgroundCandidate),
            "a founder background on a different profile remains an ordinary candidate");
        Check(UniqueCandidateRules.CapNativeFounderGuildPrice(UniqueCandidateRules.Lowell, 499) == 499,
            "Founder guild price below 500 gold is unchanged");
        Check(UniqueCandidateRules.CapNativeFounderGuildPrice(UniqueCandidateRules.Liana, 500) == 500,
            "Founder guild price at 500 gold is unchanged");
        Check(UniqueCandidateRules.CapNativeFounderGuildPrice(UniqueCandidateRules.Kragas, 850) == 500,
            "Founder guild price above 500 gold is capped at 500");
        foreach (var founder in new[] { UniqueCandidateRules.Lowell, UniqueCandidateRules.Liana, UniqueCandidateRules.Kragas })
            Check(UniqueCandidateRules.CapNativeFounderGuildPrice(new UniqueCandidateIdentity(
                founder.ProfileKey, "TEXTKEY_PLAYERNAME_Custom", founder.BackgroundTrait), 850) == 500,
                $"renamed founder with profile {founder.ProfileKey} keeps the guild price cap");
        Check(UniqueCandidateRules.CapNativeFounderGuildPrice(ordinaryLowellLikeCandidate, 850) == 850,
            "same profile and name with a normal background does not receive the guild price cap");
        Check(UniqueCandidateRules.CapNativeFounderGuildPrice(foreignLowellBackgroundCandidate, 850) == 850,
            "founder background on another profile does not receive the guild price cap");
    }

    static void LockPolicy()
    {
        var lockTruthTable = new[]
        {
            (FounderLocks, false, false, false, "founder with both locks open stays native"),
            (FounderLocks, true, false, false, "founder with only the portrait locked can still reroll other start traits"),
            (FounderLocks, false, true, true, "founder with only primary traits locked is blocked before a reroll can fail"),
            (FounderLocks, true, true, true, "founder with portrait and primary traits locked gets the custom lock"),
            (PackUniqueLocks, false, false, false, "pack unique with both locks open stays native"),
            (PackUniqueLocks, true, false, true, "pack unique with only the portrait locked gets the custom lock"),
            (PackUniqueLocks, false, true, true, "pack unique with only primary traits locked gets the custom lock"),
            (PackUniqueLocks, true, true, true, "pack unique with both locks gets the custom lock"),
            (NoUniqueLocks, true, true, false, "normal candidate with both locks never gets the custom lock")
        };
        foreach (var (policy, profileLocked, primaryTraitsLocked, expected, name) in lockTruthTable)
            Check(UniqueCandidateRerollDecision.ShouldApplyCustomLock(policy, profileLocked, primaryTraitsLocked)
                == expected, name);
        Check(!UniqueCandidateRerollDecision.ShouldApplyCustomLock(FounderLocks, true, true, generationFailed: true)
                && !UniqueCandidateRerollDecision.ShouldApplyCustomLock(PackUniqueLocks, true, false, generationFailed: true),
            "native generation failure takes priority over every unique-candidate lock");
    }

    static void RerollButton()
    {
        var button = new FakeRerollPanel(NativeRefreshEnglish);
        InitialLockStates(button);
        LockTransitions(button);
        LanguageRefresh(button);
        GenerationFailure(button);
        NormalCandidates(button);
        PackUniqueButton(button);
        LockConflictText();
    }

    static void InitialLockStates(FakeRerollPanel button)
    {
        var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock
            && !button.Interactable && button.Text == lockConflict.English
            && button.Color == "native-color",
            "unique candidate with both locks gets the custom disable and native lock-conflict text");
        button.NativeStateChange();
        button.Interactable = false;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, false) == UniqueCandidateRerollAction.ApplyLockConflictText
            && !button.Interactable && button.Text == lockConflict.English
            && button.Color == "native-color",
            "unique candidate with one lock and a native-disabled button gets only the conflict explanation");
        button.NativeStateChange();
        button.Interactable = false;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyLockConflictText
            && !button.Interactable && button.Text == lockConflict.English
            && button.Color == "native-color",
            "both locks do not overwrite a native-disabled button decision");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        button.Color = "native-color";
        Check(button.Reconcile(FounderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish
            && button.Color == "native-color",
            "unique candidate with an allowed native-active button keeps native refresh text and color");
    }

    static void LockTransitions(FakeRerollPanel button)
    {
        var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock
            && button.Reconcile(FounderLocks, false, false) == UniqueCandidateRerollAction.RestoreNativeState
            && button.Interactable && button.Text == NativeRefreshEnglish,
            "closing both locks applies the custom reroll lock");
        button.NativeStateChange(); // Prefix before SetRestrictions restores the previous native state.
        button.Interactable = false; // The native restriction setter decides this state.
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, false) == UniqueCandidateRerollAction.ApplyLockConflictText
            && !button.Interactable,
            "opening either required lock keeps the native-disabled state and explains the conflict");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish,
            "unlock restores the native active button, refresh text, and original color");
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock
            && !button.Interactable && button.Text == lockConflict.English,
            "relocking both conditions reapplies the custom disable and conflict text");
    }

    static void LanguageRefresh(FakeRerollPanel button)
    {
        var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
        button.NativeStateChange();
        button.Interactable = false;
        button.Text = NativeRefreshGerman;
        Check(button.Reconcile(FounderLocks, true, false, conflictText: lockConflict.German)
                == UniqueCandidateRerollAction.ApplyLockConflictText
            && button.Text == lockConflict.German,
            "language refresh reapplies the current language's native conflict fallback");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshGerman;
        Check(button.Reconcile(FounderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
            && button.Text == NativeRefreshGerman && button.Color == "native-color",
            "language refresh restores the native refresh text and color when the button is active");
    }

    static void GenerationFailure(FakeRerollPanel button)
    {
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock,
            "custom lock is active before a native generation-failed transition");
        button.NativeStateChange(); // SetGenerationFailed Prefix restores before native error text is set.
        button.Text = NativeFailureText;
        Check(button.Reconcile(FounderLocks, true, true, generationFailed: true)
                == UniqueCandidateRerollAction.KeepNative
            && !button.Interactable && button.Text == NativeFailureText && !button.Rule.PresentationApplied,
            "native generation-failed text stays, and a unique candidate's button is disabled like every other blocked unique reroll");
        Check(UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(FounderLocks, true)
                && UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(PackUniqueLocks, true)
                && !UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(NoUniqueLocks, true)
                && !UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(FounderLocks, false),
            "only unique candidates get the disabled button after a native generation failure");
        Check(UniqueCandidateRerollDecision.Evaluate(FounderLocks, true, true, generationFailed: true,
                nativeButtonInteractable: true, presentationApplied: true)
                == UniqueCandidateRerollAction.DiscardStaleCustomState,
            "native generation failure discards stale presentation bookkeeping");
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock,
            "custom lock reapplies after the native generation failure clears");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeFailureText;
        button.Reconcile(FounderLocks, false, true, generationFailed: true);
        button.Text = NativeRefreshEnglish; // Native clears the failure text on the next successful roll.
        Check(button.Reconcile(FounderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish && !button.Rule.FailureDisableApplied,
            "a cleared generation failure restores the unique candidate's active reroll button");
    }

    static void NormalCandidates(FakeRerollPanel button)
    {
        button.Interactable = true;
        button.Text = NativeFailureText;
        Check(button.Reconcile(NoUniqueLocks, false, true, generationFailed: true) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeFailureText,
            "normal candidates keep the native active button after a generation failure");
        button.Text = NativeRefreshEnglish;
        button.NativeStateChange(); // SetUnitData Prefix restores before the pooled panel is reused.
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(NoUniqueLocks, true, true) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish && !button.Rule.PresentationApplied,
            "panel reuse for a normal candidate leaves native button state and text unchanged");
        button.Interactable = false;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(NoUniqueLocks, true, false) == UniqueCandidateRerollAction.KeepNative
            && !button.Interactable && button.Text == NativeRefreshEnglish,
            "normal candidates remain unchanged even when the native button is disabled");
    }

    static void PackUniqueButton(FakeRerollPanel button)
    {
        var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(PackUniqueLocks, true, false) == UniqueCandidateRerollAction.ApplyCustomLock
            && !button.Interactable && button.Text == lockConflict.English,
            "pack unique with only the portrait locked disables reroll and shows the conflict text");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(PackUniqueLocks, false, true) == UniqueCandidateRerollAction.ApplyCustomLock
            && !button.Interactable && button.Text == lockConflict.English,
            "pack unique with only primary traits locked disables reroll and shows the conflict text");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(PackUniqueLocks, false, false) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish && !button.Rule.PresentationApplied,
            "opening both pack-unique locks restores the native reroll button");
        Check(button.Reconcile(FounderLocks, true, false) == UniqueCandidateRerollAction.KeepNative
            && button.Interactable && button.Text == NativeRefreshEnglish && !button.Rule.PresentationApplied,
            "founder with only the portrait locked keeps the active native reroll button");
        button.NativeStateChange();
        button.Interactable = true;
        button.Text = NativeRefreshEnglish;
        Check(button.Reconcile(FounderLocks, false, true) == UniqueCandidateRerollAction.ApplyCustomLock
            && !button.Interactable && button.Text == lockConflict.English,
            "founder with only primary traits locked is blocked immediately, before the native reroll fails");
    }

    static void LockConflictText()
    {
        var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
        foreach (var (label, translations) in new[]
        {
            ("native lock-conflict fallback", lockConflict)
        })
        {
            Check(translations.AllLanguages.Count == 10
                && translations.AllLanguages.All(value => !string.IsNullOrWhiteSpace(value)),
                label + " has all ten nonempty translations");
        }
        Check(UniqueCandidateLocalizationText.NativeLockConflictKey == "TEXTKEY_CreateCampaign_LockConflict"
            && lockConflict.English.Contains("locks", StringComparison.OrdinalIgnoreCase)
            && !lockConflict.English.Contains("unique", StringComparison.OrdinalIgnoreCase),
            "the existing native lock-conflict key and generic fallback are used without unique-specific copy");
    }

    static void NativeFounderReplacement()
    {
        Check(NativeFounderReplacementRule.MayReplace(UniqueCandidateLockPolicy.None)
                && !NativeFounderReplacementRule.MayReplace(UniqueCandidateLockPolicy.ProfileOrPrimaryTraits)
                && !NativeFounderReplacementRule.MayReplace(UniqueCandidateLockPolicy.PrimaryTraits),
            "native founders replace only ordinary candidates, never a pack unique or another founder");
    }

    // Stands in for the game's recruitment panel and its reroll button. Only the button state lives here;
    // every decision comes from the real UniqueCandidateRerollState, which the panel patches drive.
    sealed class FakeRerollPanel : IUniqueRerollPanel, IUniqueRerollSurface
    {
        string conflictText = string.Empty;

        internal FakeRerollPanel(string text) => Text = text;

        internal UniqueCandidateRerollState Rule { get; private set; }
        public bool Interactable { get; set; } = true;
        public string Text { get; set; }
        internal string Color { get; set; } = "native-color";
        public bool GenerationFailed { get; private set; }
        public bool ButtonAlive => true;
        public bool TextAlive => true;
        public bool NativeInteractable => Interactable;
        public IUniqueRerollSurface PresentationSurface => this;
        public IUniqueRerollSurface FailureSurface => this;

        public object TextStyle
        {
            get => Color;
            set => Color = (string)value;
        }

        public string LockConflictText() => conflictText;

        public void Warn(string message) { }

        // The prefix that runs before every native state change of the panel.
        internal void NativeStateChange() => Rule?.BeforeNativeStateChange();

        // The postfix after SetUnitData, SetRestrictions, SetGenerationFailed or RefreshLanguage: the panel
        // reports the native lock and failure flags, then the rule reconciles. A different policy is a
        // different candidate in the pooled panel.
        internal UniqueCandidateRerollAction Reconcile(UniqueCandidateLockPolicy policy, bool profileLocked,
            bool primaryTraitsLocked, bool generationFailed = false, string conflictText = "")
        {
            if (Rule == null || Rule.LockPolicy != policy)
            {
                Rule?.Release();
                Rule = new UniqueCandidateRerollState(this, policy, profileLocked, primaryTraitsLocked, generationFailed);
            }
            this.conflictText = string.IsNullOrEmpty(conflictText)
                ? UniqueCandidateLocalizationText.LockConflictFallback.English : conflictText;
            GenerationFailed = generationFailed;
            Rule.ProfileLocked = profileLocked;
            Rule.PrimaryTraitsLocked = primaryTraitsLocked;
            Rule.NativeGenerationFailed = generationFailed;
            return Rule.Reconcile();
        }
    }
}
