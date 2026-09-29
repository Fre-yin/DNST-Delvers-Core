using System.Collections;
using DungeonSettlersDelvers.Core;

var checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

var originalHost = new TestDelversHost("original-core-package");
var duplicateHost = new TestDelversHost("duplicate-core-package");
DelversHost.Bind(originalHost);
try
{
    var duplicateHostRejected = false;
    try { DelversHost.Bind(duplicateHost); }
    catch (InvalidOperationException) { duplicateHostRejected = true; }
    DelversHost.Unbind(duplicateHost);
    DelversHost.Info("original-host-still-active");
    Check(duplicateHostRejected && DelversHost.IsBound
        && DelversHost.PackageDirectory == "original-core-package"
        && originalHost.InfoMessages.SequenceEqual(new[] { "original-host-still-active" })
        && duplicateHost.InfoMessages.Count == 0,
        "second Core host is rejected without replacing or disabling the original");
}
finally
{
    DelversHost.Unbind(duplicateHost);
    DelversHost.Unbind(originalHost);
}
Check(!DelversHost.IsBound, "the owning Core host can release its binding");

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
var ordinaryLowellLikeCandidate = new UniqueCandidateIdentity(UniqueCandidateRules.Lowell.ProfileKey,
    UniqueCandidateRules.Lowell.NameTextKey, "AFFECTER_BasicWarrior");
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

const UniqueCandidateLockPolicy founderLocks = UniqueCandidateLockPolicy.PrimaryTraits;
const UniqueCandidateLockPolicy packUniqueLocks = UniqueCandidateLockPolicy.ProfileOrPrimaryTraits;
const UniqueCandidateLockPolicy noUniqueLocks = UniqueCandidateLockPolicy.None;
var lockTruthTable = new[]
{
    (founderLocks, false, false, false, "founder with both locks open stays native"),
    (founderLocks, true, false, false, "founder with only the portrait locked can still reroll other start traits"),
    (founderLocks, false, true, true, "founder with only primary traits locked is blocked before a reroll can fail"),
    (founderLocks, true, true, true, "founder with portrait and primary traits locked gets the custom lock"),
    (packUniqueLocks, false, false, false, "pack unique with both locks open stays native"),
    (packUniqueLocks, true, false, true, "pack unique with only the portrait locked gets the custom lock"),
    (packUniqueLocks, false, true, true, "pack unique with only primary traits locked gets the custom lock"),
    (packUniqueLocks, true, true, true, "pack unique with both locks gets the custom lock"),
    (noUniqueLocks, true, true, false, "normal candidate with both locks never gets the custom lock")
};
foreach (var (policy, profileLocked, primaryTraitsLocked, expected, name) in lockTruthTable)
    Check(UniqueCandidateRerollDecision.ShouldApplyCustomLock(policy, profileLocked, primaryTraitsLocked)
        == expected, name);
Check(!UniqueCandidateRerollDecision.ShouldApplyCustomLock(founderLocks, true, true, generationFailed: true)
        && !UniqueCandidateRerollDecision.ShouldApplyCustomLock(packUniqueLocks, true, false, generationFailed: true),
    "native generation failure takes priority over every unique-candidate lock");

const string nativeRefreshEnglish = "Recruit again";
const string nativeRefreshGerman = "Erneut rekrutieren";
const string nativeFailureText = "Native generation failure";
var lockConflict = UniqueCandidateLocalizationText.LockConflictFallback;
var rerollButtonInteractable = true;
var rerollButtonText = nativeRefreshEnglish;
var rerollButtonColor = "native-color";
var presentationApplied = false;
var snapshotInteractable = false;
var snapshotText = string.Empty;
var snapshotColor = string.Empty;

void RestorePresentation()
{
    if (!presentationApplied) return;
    rerollButtonInteractable = snapshotInteractable;
    rerollButtonText = snapshotText;
    rerollButtonColor = snapshotColor;
    presentationApplied = false;
}

void ApplyRerollAction(UniqueCandidateRerollAction action, string conflictText)
{
    if (action is UniqueCandidateRerollAction.ApplyCustomLock
        or UniqueCandidateRerollAction.ApplyLockConflictText)
    {
        if (!presentationApplied)
        {
            snapshotInteractable = rerollButtonInteractable;
            snapshotText = rerollButtonText;
            snapshotColor = rerollButtonColor;
        }
        if (action == UniqueCandidateRerollAction.ApplyCustomLock)
            rerollButtonInteractable = false;
        rerollButtonText = conflictText;
        presentationApplied = true;
    }
    else if (action == UniqueCandidateRerollAction.RestoreNativeState)
        RestorePresentation();
    else if (action == UniqueCandidateRerollAction.DiscardStaleCustomState)
        presentationApplied = false;
}

var failureDisableApplied = false;
var failureOriginalInteractable = false;

UniqueCandidateRerollAction ReconcileRerollUi(UniqueCandidateLockPolicy policy, bool profileLocked,
    bool primaryTraitsLocked, bool generationFailed = false, string conflictText = "")
{
    // Every native state change is preceded by a prefix that restores the failure disable.
    if (failureDisableApplied)
    {
        rerollButtonInteractable = failureOriginalInteractable;
        failureDisableApplied = false;
    }
    var nativeInteractable = presentationApplied ? snapshotInteractable : rerollButtonInteractable;
    var action = UniqueCandidateRerollDecision.Evaluate(policy, profileLocked, primaryTraitsLocked,
        generationFailed, nativeInteractable, presentationApplied);
    ApplyRerollAction(action, string.IsNullOrEmpty(conflictText) ? lockConflict.English : conflictText);
    if (UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(policy, generationFailed))
    {
        failureOriginalInteractable = rerollButtonInteractable;
        rerollButtonInteractable = false;
        failureDisableApplied = true;
    }
    return action;
}

Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English
    && rerollButtonColor == "native-color",
    "unique candidate with both locks gets the custom disable and native lock-conflict text");

RestorePresentation();
rerollButtonInteractable = false;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, false) == UniqueCandidateRerollAction.ApplyLockConflictText
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English
    && rerollButtonColor == "native-color",
    "unique candidate with one lock and a native-disabled button gets only the conflict explanation");

RestorePresentation();
rerollButtonInteractable = false;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyLockConflictText
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English
    && rerollButtonColor == "native-color",
    "both locks do not overwrite a native-disabled button decision");

RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
rerollButtonColor = "native-color";
Check(ReconcileRerollUi(founderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish
    && rerollButtonColor == "native-color",
    "unique candidate with an allowed native-active button keeps native refresh text and color");

rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock,
    "closing both locks applies the custom reroll lock");
RestorePresentation(); // Prefix before SetRestrictions restores the previous native state.
rerollButtonInteractable = false; // The native restriction setter decides this state.
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, false) == UniqueCandidateRerollAction.ApplyLockConflictText
    && !rerollButtonInteractable,
    "opening either required lock keeps the native-disabled state and explains the conflict");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish,
    "unlock restores the native active button, refresh text, and original color");
Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English,
    "relocking both conditions reapplies the custom disable and conflict text");

RestorePresentation();
rerollButtonInteractable = false;
rerollButtonText = nativeRefreshGerman;
Check(ReconcileRerollUi(founderLocks, true, false, conflictText: lockConflict.German)
        == UniqueCandidateRerollAction.ApplyLockConflictText
    && rerollButtonText == lockConflict.German,
    "language refresh reapplies the current language's native conflict fallback");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshGerman;
Check(ReconcileRerollUi(founderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonText == nativeRefreshGerman && rerollButtonColor == "native-color",
    "language refresh restores the native refresh text and color when the button is active");

rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock,
    "custom lock is active before a native generation-failed transition");
RestorePresentation(); // SetGenerationFailed Prefix restores before native error text is set.
rerollButtonText = nativeFailureText;
Check(ReconcileRerollUi(founderLocks, true, true, generationFailed: true)
        == UniqueCandidateRerollAction.KeepNative
    && !rerollButtonInteractable && rerollButtonText == nativeFailureText && !presentationApplied,
    "native generation-failed text stays, and a unique candidate's button is disabled like every other blocked unique reroll");
Check(UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(founderLocks, true)
        && UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(packUniqueLocks, true)
        && !UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(noUniqueLocks, true)
        && !UniqueCandidateRerollDecision.ShouldDisableAfterGenerationFailure(founderLocks, false),
    "only unique candidates get the disabled button after a native generation failure");
Check(UniqueCandidateRerollDecision.Evaluate(founderLocks, true, true, generationFailed: true,
        nativeButtonInteractable: true, presentationApplied: true)
        == UniqueCandidateRerollAction.DiscardStaleCustomState,
    "native generation failure discards stale presentation bookkeeping");

rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, true, true) == UniqueCandidateRerollAction.ApplyCustomLock,
    "custom lock reapplies after the native generation failure clears");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeFailureText;
ReconcileRerollUi(founderLocks, false, true, generationFailed: true);
rerollButtonText = nativeRefreshEnglish; // Native clears the failure text on the next successful roll.
Check(ReconcileRerollUi(founderLocks, false, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish && !failureDisableApplied,
    "a cleared generation failure restores the unique candidate's active reroll button");
rerollButtonInteractable = true;
rerollButtonText = nativeFailureText;
Check(ReconcileRerollUi(noUniqueLocks, false, true, generationFailed: true) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeFailureText,
    "normal candidates keep the native active button after a generation failure");
rerollButtonText = nativeRefreshEnglish;
RestorePresentation(); // SetUnitData Prefix restores before the pooled panel is reused.
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(noUniqueLocks, true, true) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish && !presentationApplied,
    "panel reuse for a normal candidate leaves native button state and text unchanged");

rerollButtonInteractable = false;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(noUniqueLocks, true, false) == UniqueCandidateRerollAction.KeepNative
    && !rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish,
    "normal candidates remain unchanged even when the native button is disabled");

rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(packUniqueLocks, true, false) == UniqueCandidateRerollAction.ApplyCustomLock
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English,
    "pack unique with only the portrait locked disables reroll and shows the conflict text");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(packUniqueLocks, false, true) == UniqueCandidateRerollAction.ApplyCustomLock
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English,
    "pack unique with only primary traits locked disables reroll and shows the conflict text");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(packUniqueLocks, false, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish && !presentationApplied,
    "opening both pack-unique locks restores the native reroll button");
Check(ReconcileRerollUi(founderLocks, true, false) == UniqueCandidateRerollAction.KeepNative
    && rerollButtonInteractable && rerollButtonText == nativeRefreshEnglish && !presentationApplied,
    "founder with only the portrait locked keeps the active native reroll button");
RestorePresentation();
rerollButtonInteractable = true;
rerollButtonText = nativeRefreshEnglish;
Check(ReconcileRerollUi(founderLocks, false, true) == UniqueCandidateRerollAction.ApplyCustomLock
    && !rerollButtonInteractable && rerollButtonText == lockConflict.English,
    "founder with only primary traits locked is blocked immediately, before the native reroll fails");

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

// A neutral example pack stands in for a character pack's own identity rule.
const string examplePackProfile = "UNITVISUAL_ExamplePack_Hero";
const string examplePackBackground = "AFFECTER_ExamplePackHeroBackground";
var uniquePredicates = new UniqueCandidatePredicateRegistry<UniqueCandidateIdentity>();
var examplePredicate = new Func<UniqueCandidateIdentity, bool>(candidate =>
    candidate.ProfileKey == examplePackProfile && candidate.BackgroundTrait == examplePackBackground);
var exampleCandidate = new UniqueCandidateIdentity(examplePackProfile, "Mira", examplePackBackground);
var exampleLease = uniquePredicates.Register("example-pack", examplePredicate);
var duplicateExampleLease = uniquePredicates.Register("example-pack", examplePredicate);
Check(uniquePredicates.Count == 1 && uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
    "pack predicate registration is idempotent and recognizes its character after a custom rename");
var conflictingPredicateRejected = false;
try { uniquePredicates.Register("example-pack", _ => false); }
catch (InvalidOperationException) { conflictingPredicateRejected = true; }
Check(conflictingPredicateRejected, "a package cannot replace its active unique-candidate predicate");
exampleLease.Dispose();
exampleLease.Dispose();
Check(uniquePredicates.Count == 1, "duplicate predicate lease survives idempotent disposal of the first lease");
duplicateExampleLease.Dispose();
Check(uniquePredicates.Count == 0 && !uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
    "unique-candidate predicate is removed after its last lease is released");

var predicateFailureLogs = new List<string>();
var failingPredicateLease = uniquePredicates.Register("broken-pack",
    _ => throw new InvalidOperationException("expected test exception"));
var exampleAfterFailureLease = uniquePredicates.Register("example-pack", examplePredicate);
var ordinaryAfterFailure = uniquePredicates.IsUnique(ordinaryLowellLikeCandidate,
    (packId, _) => predicateFailureLogs.Add(packId));
var exampleAfterFailure = uniquePredicates.IsUnique(exampleCandidate,
    (packId, _) => predicateFailureLogs.Add(packId));
Check(!ordinaryAfterFailure && exampleAfterFailure
    && predicateFailureLogs.SequenceEqual(new[] { "broken-pack" }),
    "a failing pack predicate is logged once and does not block other candidates or packs");
failingPredicateLease.Dispose();
exampleAfterFailureLease.Dispose();
var cleanupLease = uniquePredicates.Register("cleanup-pack", _ => true);
Check(uniquePredicates.Count == 1, "active predicate is present before Core shutdown cleanup");
uniquePredicates.Clear();
Check(uniquePredicates.Count == 0 && !uniquePredicates.IsUnique(exampleCandidate, (_, _) => { }),
    "Core shutdown cleanup clears active unique-candidate registrations");
cleanupLease.Dispose();
Check(uniquePredicates.Count == 0, "a lease disposed after Core cleanup is harmless");

// Fixed traits: a save written while a pack was inactive keeps only the profile.
var fixedOrder = new[] { "AFFECTER_Elf", "AFFECTER_ExampleBackground", "AFFECTER_ExampleIndividual" };
Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth" }, fixedOrder)
        .SequenceEqual(fixedOrder.Concat(new[] { "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth" })),
    "a wiped unit regains every fixed trait first, in registered order");
Check(FixedTraitRules.WithMissing(Array.Empty<string>(), fixedOrder).SequenceEqual(fixedOrder),
    "an empty affecter list regains every fixed trait");
Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_NewRecruit", "AFFECTER_Elf", "AFFECTER_Hungry" }, fixedOrder)
        .SequenceEqual(new[] { "AFFECTER_NewRecruit", "AFFECTER_Elf", "AFFECTER_ExampleBackground",
            "AFFECTER_ExampleIndividual", "AFFECTER_Hungry" }),
    "missing fixed traits follow their present predecessor; other keys keep their order");
Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_Elf", "AFFECTER_ExampleIndividual" }, fixedOrder)
        .SequenceEqual(fixedOrder),
    "a missing middle trait is inserted between its neighbours");
Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_ExampleIndividual" }, fixedOrder)
        .SequenceEqual(fixedOrder),
    "leading fixed traits are inserted before the first present one without duplicates");
Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_Hungry", "AFFECTER_ExampleIndividual", "AFFECTER_Elf",
        "AFFECTER_ExampleBackground" }, fixedOrder) == null,
    "an intact unit needs no change, whatever the saved order");
Check(FixedTraitRules.WithMissing(new[] { null, "AFFECTER_Elf", "AFFECTER_Elf" }, fixedOrder)
        .SequenceEqual(new[] { null, "AFFECTER_Elf", "AFFECTER_ExampleBackground", "AFFECTER_ExampleIndividual",
            "AFFECTER_Elf" }),
    "unreadable and duplicate entries keep their position and anchor only once");

var fixedTraits = new FixedTraitRegistry();
var exampleFixedLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", fixedOrder);
var duplicateFixedLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", fixedOrder.ToList());
Check(fixedTraits.TryGet("UNITVISUAL_Example", out var fixedPack, out var registeredTraits)
        && fixedPack == "example-pack" && registeredTraits.SequenceEqual(fixedOrder) && fixedTraits.Count == 1,
    "a pack registers ordered fixed traits for its profile; a repeated identical registration is leased");
static bool RegisterFails(FixedTraitRegistry registry, string packId, string profileKey, string[] traits)
{
    try { registry.Register(packId, profileKey, traits).Dispose(); return false; }
    catch (ArgumentException) { return true; }
    catch (InvalidOperationException) { return true; }
}
Check(RegisterFails(fixedTraits, "other-pack", "UNITVISUAL_Example", fixedOrder)
        && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Example", new[] { "AFFECTER_Elf" }),
    "another pack or different traits cannot take over a registered profile");
Check(RegisterFails(fixedTraits, "Bad Pack", "UNITVISUAL_Other", fixedOrder)
        && RegisterFails(fixedTraits, "example-pack", "", fixedOrder)
        && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", Array.Empty<string>())
        && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", new[] { "AFFECTER_Elf", "AFFECTER_Elf" })
        && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", new[] { "AFFECTER_Elf", " " }),
    "invalid pack IDs, profiles and trait lists are rejected");
Check(fixedTraits.Count == 1 && fixedTraits.ShouldReportFailure("UNITVISUAL_Example")
        && !fixedTraits.ShouldReportFailure("UNITVISUAL_Example")
        && !fixedTraits.ShouldReportFailure("UNITVISUAL_Unregistered"),
    "a failing restore is reported once per registered profile");
duplicateFixedLease.Dispose();
Check(fixedTraits.TryGet("UNITVISUAL_Example", out _, out _), "one released lease keeps a duplicate registration");
exampleFixedLease.Dispose();
exampleFixedLease.Dispose();
Check(fixedTraits.Count == 0 && !fixedTraits.TryGet("UNITVISUAL_Example", out _, out _),
    "the last released lease removes the registration; double dispose is harmless");
var fixedCleanupLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", fixedOrder);
fixedTraits.Clear();
fixedCleanupLease.Dispose();
Check(fixedTraits.Count == 0, "Core shutdown cleanup clears fixed traits; a later lease release is harmless");

var integrations = new PackIntegrationRegistry<string>();
var observed = new List<string>();
var alphaObserver = new Action<string>(value => observed.Add("alpha:" + value));
var alphaPolicy = new Func<bool>(() => false);
var betaSuppress = false;
var betaObserver = new Action<string>(value => observed.Add("beta:" + value));
var betaPolicy = new Func<bool>(() => betaSuppress);
var alphaLease = integrations.Register("alpha", alphaObserver, alphaPolicy);
var duplicateAlphaLease = integrations.Register("alpha", alphaObserver, alphaPolicy);
var betaLease = integrations.Register("beta", betaObserver, betaPolicy);
Check(integrations.Count == 2, "pack integration registration is keyed and idempotent");
integrations.Notify("refresh", (_, _) => throw new InvalidOperationException("unexpected observer failure"));
Check(observed.SequenceEqual(new[] { "alpha:refresh", "beta:refresh" }),
    "campaign refresh dispatches to every registered pack once");
Check(!integrations.AnyPolicy((_, _) => throw new InvalidOperationException("unexpected policy failure")),
    "native-founder policies default to false when no pack requests suppression");
betaSuppress = true;
Check(integrations.AnyPolicy((_, _) => throw new InvalidOperationException("unexpected policy failure")),
    "native-founder suppression aggregates pack policies with any-true");
var duplicatePackIdRejected = false;
try { integrations.Register("beta", _ => { }, () => false); }
catch (InvalidOperationException) { duplicatePackIdRejected = true; }
Check(duplicatePackIdRejected, "a pack cannot replace another registration with the same ID");
alphaLease.Dispose();
alphaLease.Dispose();
Check(integrations.Count == 2, "integration leases are idempotently disposed while references remain");
duplicateAlphaLease.Dispose();
Check(integrations.Count == 1, "a pack integration is removed after its last lease is released");
betaLease.Dispose();
Check(integrations.Count == 0 && !integrations.AnyPolicy((_, _) => { }),
    "released integrations leave no campaign observer or suppression policy");

var failingPolicyLogCount = 0;
var failingPolicyLease = integrations.Register("broken-policy", _ => { },
    () => throw new InvalidOperationException("expected test exception"));
integrations.AnyPolicy((_, _) => failingPolicyLogCount++);
integrations.AnyPolicy((_, _) => failingPolicyLogCount++);
Check(failingPolicyLogCount == 1, "a failing pack policy is isolated and reported only once");
failingPolicyLease.Dispose();

var loadIntegrations = new PackLoadIntegrationRegistry();
var loadCallbackTrace = new List<string>();
var componentSaveList = new ComponentSaveList();
var profileSaveData = new ComponentSaveBaseData();
var loadEntity = new TestEntity();
var loadedProfile = new UnitProfileComponent();
var loadedCampaign = new CampaignDataContainer();
var beforeComponents = new BeforeComponentsDeserializeHandler((saved, entity) =>
{
    Check(ReferenceEquals(saved, componentSaveList) && ReferenceEquals(entity, loadEntity),
        "before-components load callback receives the original deserialize context");
    loadCallbackTrace.Add("before-components");
});
var beforeProfile = new BeforeUnitProfileDeserializeHandler((saved, entity) =>
{
    Check(ReferenceEquals(saved, profileSaveData) && ReferenceEquals(entity, loadEntity),
        "before-profile load callback receives the original deserialize context");
    loadCallbackTrace.Add("before-profile");
});
var afterProfile = new AfterUnitProfileDeserializeHandler(profile =>
{
    Check(ReferenceEquals(profile, loadedProfile),
        "after-profile load callback receives the loaded profile");
    loadCallbackTrace.Add("after-profile");
});
var afterCampaign = new AfterCampaignLoadedHandler(campaign =>
{
    Check(ReferenceEquals(campaign, loadedCampaign),
        "after-campaign load callback receives the loaded campaign");
    loadCallbackTrace.Add("after-campaign");
});
var loadLease = loadIntegrations.Register("example-pack", beforeComponents, beforeProfile,
    afterProfile, afterCampaign);
var duplicateLoadLease = loadIntegrations.Register("example-pack", beforeComponents, beforeProfile,
    afterProfile, afterCampaign);
Check(loadIntegrations.Count == 1,
    "an identical load integration reuses one registration with independent leases");
var conflictingLoadIntegrationRejected = false;
try
{
    loadIntegrations.Register("example-pack", (_, _) => { }, beforeProfile, afterProfile,
        afterCampaign).Dispose();
}
catch (InvalidOperationException)
{
    conflictingLoadIntegrationRejected = true;
}
Check(conflictingLoadIntegrationRejected && loadIntegrations.Count == 1,
    "a pack cannot replace an existing load integration with different callbacks");

var unexpectedLoadFailureCount = 0;
void UnexpectedLoadFailure(string _, string __, Exception ___) => unexpectedLoadFailureCount++;
loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity, UnexpectedLoadFailure);
loadIntegrations.NotifyBeforeProfile(profileSaveData, loadEntity, UnexpectedLoadFailure);
loadIntegrations.NotifyAfterProfile(loadedProfile, UnexpectedLoadFailure);
loadIntegrations.NotifyAfterCampaign(loadedCampaign, UnexpectedLoadFailure);
Check(unexpectedLoadFailureCount == 0
        && loadCallbackTrace.SequenceEqual(new[]
        {
            "before-components", "before-profile", "after-profile", "after-campaign"
        }),
    "load integration dispatches every callback stage once despite duplicate leases");

var expectedLoadException = new InvalidOperationException("expected load callback failure");
var failureReports = new List<(string PackId, string Stage, Exception Error)>();
var callbacksAfterFailure = 0;
var failingLoadLease = loadIntegrations.Register("broken-pack",
    (_, _) => throw expectedLoadException, null, null, null);
var healthyLoadLease = loadIntegrations.Register("healthy-pack",
    (_, _) => callbacksAfterFailure++, null, null, null);
loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity,
    (packId, stage, error) => failureReports.Add((packId, stage, error)));
Check(failureReports.Count == 1
        && failureReports[0].PackId == "broken-pack"
        && failureReports[0].Stage == "before-components"
        && ReferenceEquals(failureReports[0].Error, expectedLoadException),
    "a failing load callback reports its pack, stage and original exception");
Check(callbacksAfterFailure == 1,
    "one failing load callback does not prevent another pack from receiving the stage");
failingLoadLease.Dispose();
healthyLoadLease.Dispose();

loadLease.Dispose();
loadLease.Dispose();
Check(loadIntegrations.Count == 1,
    "disposing one reused load-integration lease keeps the registration active");
duplicateLoadLease.Dispose();
Check(loadIntegrations.Count == 0,
    "disposing the last load-integration lease removes the registration");

var callbacksAfterClear = 0;
var clearedLoadLease = loadIntegrations.Register("cleanup-pack",
    (_, _) => callbacksAfterClear++, null, null, null);
loadIntegrations.Clear();
loadIntegrations.NotifyBeforeComponents(componentSaveList, loadEntity, UnexpectedLoadFailure);
clearedLoadLease.Dispose();
Check(loadIntegrations.Count == 0 && callbacksAfterClear == 0,
    "Core cleanup clears load integrations and later lease disposal remains harmless");

Check(LoadIntegritySignalRules.IsDeserializationFailure(
        "[ComponentList] Failed to deserialize component. Type=Affecter", true),
    "component deserialization failures are recognized as destructive load signals");
Check(LoadIntegritySignalRules.IsDeserializationFailure(
        "CampaignDataContainer: Failed to deserialize section EntityContainerSaveData", true),
    "section deserialization failures are recognized as destructive load signals");
Check(LoadIntegritySignalRules.IsDeserializationFailure(
        "FAILED TO DESERIALIZE COMPONENT", true),
    "destructive load signal matching is case insensitive");
Check(!LoadIntegritySignalRules.IsDeserializationFailure(
        "A pack callback failed after the campaign loaded", true),
    "unrelated errors do not trigger the native deserialization signal");
Check(!LoadIntegritySignalRules.IsDeserializationFailure(null, true),
    "empty native log messages do not trigger the load guard");
Check(!LoadIntegritySignalRules.IsDeserializationFailure(
        "[OtherMod] Failed to deserialize component config, using defaults", false),
    "informational log lines with the same words do not trigger the load guard");

Check(LoadIntegrityNoticeText.All.All(entry => entry.Text.AllLanguages.Count == 10
        && entry.Text.AllLanguages.All(text =>
            !string.IsNullOrWhiteSpace(text) && text.StartsWith("Delvers Core", StringComparison.Ordinal))),
    "the in-game save-block dialog and banner have a Delvers Core text for every game language");
Check(LoadIntegrityNoticeText.All.All(entry =>
            entry.Key.StartsWith("TEXTKEY_DelversCore_", StringComparison.Ordinal))
        && LoadIntegrityNoticeText.All.Select(entry => entry.Key).Distinct().Count() == 2,
    "the save-block notices use distinct Core-owned text keys");
Check(LoadIntegrityNoticeText.SaveBlockedShort.AllLanguages.All(text => text.Length <= 90),
    "the save-block banner stays short enough for the one-line help notice");
Check(LoadIntegrityNoticeText.All.SelectMany(entry => entry.Text.AllLanguages)
        .All(text => !text.Contains('\u2013') && !text.Contains('\u2014')),
    "the save-block notices use natural sentences without en or em dashes");

var healthyLoad = new LoadIntegrityState();
Check(healthyLoad.IsSaveAllowed && healthyLoad.SaveBlockReason == null,
    "a fresh session allows saving");
healthyLoad.Begin("campaign-a");
var duringLoad = healthyLoad.DecideSave();
Check(duringLoad.Blocked && !duringLoad.ShouldNotify
        && duringLoad.Reason == LoadIntegrityState.LoadInProgressReason,
    "a save during a running load is held back without a player notice");
Check(!healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted)
        && healthyLoad.HasObservedCompletionSignal,
    "the first completion signal is recorded but does not complete the load");
Check(healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted)
        && !healthyLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted),
    "both completion signals start completion exactly once");
Check(healthyLoad.Complete(contextReady: true) == LoadCompletionResult.Trusted
        && healthyLoad.IsSaveAllowed && !healthyLoad.DecideSave().Blocked,
    "a load with both signals and no failure is trusted and allows saving");
Check(healthyLoad.Complete(contextReady: true) == LoadCompletionResult.NotInProgress,
    "completing twice is harmless");
Check(!healthyLoad.ReportFailure("late-error") && healthyLoad.IsSaveAllowed,
    "a failure reported outside a load does not block saving");

var failedLoad = new LoadIntegrityState();
failedLoad.Begin("campaign-b");
Check(failedLoad.ReportFailure("native-deserialization-log")
        && !failedLoad.ReportFailure("second-source"),
    "only the first failure of a load is reported");
failedLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
failedLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
Check(failedLoad.Complete(contextReady: true) == LoadCompletionResult.Untrusted,
    "a load with a detected failure completes untrusted");
var firstBlockedSave = failedLoad.DecideSave();
var secondBlockedSave = failedLoad.DecideSave();
Check(firstBlockedSave.Blocked && firstBlockedSave.ShouldLog && firstBlockedSave.ShouldNotify
        && secondBlockedSave.Blocked && !secondBlockedSave.ShouldLog && secondBlockedSave.ShouldNotify,
    "every blocked save notifies the player while the log entry is written once per load");
Check(firstBlockedSave.Reason == LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native-deserialization-log"),
    "the save-block reason names the first detection source");
failedLoad.Begin("campaign-c");
failedLoad.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
failedLoad.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
Check(failedLoad.Complete(contextReady: true) == LoadCompletionResult.Untrusted
        && failedLoad.DecideSave().Blocked && failedLoad.FailureSource == "native-deserialization-log",
    "a clean later load in the same session stays blocked until restart");

var missingContext = new LoadIntegrityState();
missingContext.Begin("campaign-d");
missingContext.ObserveCompletionSignal(LoadCompletionSignal.NativeFinalizeCompleted);
missingContext.ObserveCompletionSignal(LoadCompletionSignal.CampaignEventCompleted);
Check(missingContext.Complete(contextReady: false) == LoadCompletionResult.Untrusted,
    "a completed load without its campaign context is not trusted");

var abortedLoad = new LoadIntegrityState();
Check(!abortedLoad.Abort("CampaignLoadingEventHandler.ShowCampaignLoadFailedNotice")
        && abortedLoad.IsSaveAllowed,
    "the native load-failed notice outside a load changes nothing");
abortedLoad.Begin("campaign-e");
var abortSequence = abortedLoad.LoadSequence;
Check(abortedLoad.Abort("load-completion-timeout") && !abortedLoad.IsLoadInProgress
        && abortedLoad.IsSaveBlocked && abortedLoad.FailureSource == "load-completion-timeout",
    "an abandoned or timed-out load ends the transaction and blocks saving");
abortedLoad.Begin("campaign-f");
Check(abortedLoad.LoadSequence == abortSequence + 1,
    "every load gets its own sequence so a stale watchdog cannot abort a newer load");
abortedLoad.Reset();
Check(abortedLoad.IsSaveAllowed && abortedLoad.FailureSource == null && !abortedLoad.IsLoadInProgress,
    "a lifecycle reset clears the block");

Check(LoadIntegrityDiagnostics.FailureNotice ==
        "Delvers Core detected mod data or an incompatible mod version during this load. "
        + "Saving is blocked to protect this save. "
        + "This is not an error in the native Dungeon Settlers patch. "
        + "Report the involved mod to its author, not to the game developers.",
    "load failure notice attributes the diagnosis and protection to Core and directs reports to mod authors");
Check(LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native-deserialization-log") ==
        LoadIntegrityDiagnostics.FailureNotice
        + " Restart the game before saving again. Detection source: native-deserialization-log.",
    "save-block message explains protection, restart recovery and detection source");
Check(LoadIntegrityDiagnostics.BuildSaveBlockedMessage("native\r\n deserialization-log")
        .EndsWith("Detection source: native deserialization-log.", StringComparison.Ordinal),
    "line breaks in a detection source are normalized in the save-block message");

var eventFirstCompletion = default(LoadCompletionState)
    .Observe(LoadCompletionSignal.CampaignEventCompleted);
Check(!eventFirstCompletion.IsComplete,
    "campaign completion event alone keeps the load transaction incomplete");
eventFirstCompletion = eventFirstCompletion.Observe(LoadCompletionSignal.NativeFinalizeCompleted);
Check(eventFirstCompletion.IsComplete,
    "campaign completion event followed by native finalize completes the load transaction");

var finalizeFirstCompletion = default(LoadCompletionState)
    .Observe(LoadCompletionSignal.NativeFinalizeCompleted);
Check(!finalizeFirstCompletion.IsComplete,
    "native finalize alone keeps the load transaction incomplete");
finalizeFirstCompletion = finalizeFirstCompletion.Observe(LoadCompletionSignal.CampaignEventCompleted);
Check(finalizeFirstCompletion.IsComplete,
    "native finalize followed by campaign completion event completes the load transaction");

var duplicateEventCompletion = default(LoadCompletionState)
    .Observe(LoadCompletionSignal.CampaignEventCompleted)
    .Observe(LoadCompletionSignal.CampaignEventCompleted);
Check(!duplicateEventCompletion.IsComplete,
    "duplicate campaign completion events cannot replace the missing native finalize signal");

var duplicateFinalizeCompletion = default(LoadCompletionState)
    .Observe(LoadCompletionSignal.NativeFinalizeCompleted)
    .Observe(LoadCompletionSignal.NativeFinalizeCompleted);
Check(!duplicateFinalizeCompletion.IsComplete,
    "duplicate native finalize callbacks cannot replace the missing campaign completion event");

Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.1.0",
        "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
    "Extended Hotbar container restore is recognized as an exact compatible load patch");
Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.0.1.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "Extended Hotbar quick-slot restore is recognized as an exact compatible load patch");
Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlersHotbar.BepInEx", "1.0.1.0",
        "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
    "BepInEx Extended Hotbar container restore is recognized as an exact compatible load patch");
Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "DungeonSettlersHotbar.BepInEx", "1.0.1.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "BepInEx Extended Hotbar quick-slot restore is recognized as an exact compatible load patch");
Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.2.0",
        "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
    "Extended Hotbar 1.0.2 keeps the reviewed container restore");
Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "DungeonSettlersHotbar.BepInEx", "1.0.2.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "BepInEx Extended Hotbar 1.0.2 keeps the reviewed quick-slot restore");
Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.0.3.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "an unreviewed Extended Hotbar patch release remains a foreign patch");
Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.1.0.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "an unreviewed Extended Hotbar version remains a foreign patch");
Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.1.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "a changed Extended Hotbar patch phase remains a foreign patch");
Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
        "QuickSlotData.Deserialize", "prefix", "OtherHotbar", "1.0.1.0",
        "DungeonSettlers10Slots.LoadSlots", "Prefix"),
    "an unrelated assembly cannot impersonate the known Hotbar integration");

var tempRoot = Path.Combine(Path.GetTempPath(), "DelversPresenceTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
try
{
    var campaign = Guid.Parse("a3d91e23-e351-4b2d-b09a-331260fbda9e");
    var store = new CampaignPresenceStore(tempRoot);
    var stableMarker = store.GetMarkerPath(campaign, "example-hero");
    Check(!store.HasSeen(campaign, "example-hero"), "new campaign starts unmarked");
    Check(store.Mark(campaign, "example-hero", "recruited"), "stable marker is written");
    Check(File.Exists(stableMarker) && store.HasSeen(campaign, "example-hero"),
        "written marker is visible immediately");
    Check(new CampaignPresenceStore(tempRoot).HasSeen(campaign, "example-hero"),
        "marker survives a fresh store instance");
    Check(!Directory.EnumerateFiles(Path.GetDirectoryName(stableMarker)!, "*.tmp").Any(),
        "atomic write leaves no temporary marker");
    Check(!store.HasSeen(Guid.NewGuid(), "example-hero"), "marker is scoped to its campaign");
    Check(!store.HasSeen(campaign, "lowell"), "marker is scoped to its character");
    Check(!store.HasSeen(Guid.Empty, "example-hero"), "empty campaign identity is rejected");
    Check(stableMarker.Contains(Path.Combine("DungeonSettlersDelvers", "Core", "recruited")),
        "new core markers use a neutral Delvers path");

    var hierarchicalLegacyCampaign = Guid.NewGuid();
    var hierarchicalLegacyDirectory = Path.Combine(tempRoot, "FrierenPortrait", "recruited",
        hierarchicalLegacyCampaign.ToString("D"));
    Directory.CreateDirectory(hierarchicalLegacyDirectory);
    File.WriteAllText(Path.Combine(hierarchicalLegacyDirectory, "liana.marker"), "legacy");
    Check(store.HasSeen(hierarchicalLegacyCampaign, "liana"),
        "legacy campaign/stableId markers remain readable");

    var legacyDirectory = Path.Combine(tempRoot, "FrierenPortrait", "recruited");
    Directory.CreateDirectory(legacyDirectory);
    var secondCampaign = Guid.NewGuid();
    var legacyName = secondCampaign.ToString("D") + "-Lowell.marker";
    File.WriteAllText(Path.Combine(legacyDirectory, legacyName), "legacy");
    Check(store.HasSeen(secondCampaign, "lowell", legacyName), "legacy marker is recognized");
    Check(store.Mark(secondCampaign, "lowell", "new", legacyName),
        "marking an existing legacy presence stays idempotent");
    var blockedRoot = Path.Combine(tempRoot, "root-is-file");
    File.WriteAllText(blockedRoot, "not a directory");
    var failingStore = new CampaignPresenceStore(blockedRoot);
    var failedCampaign = Guid.NewGuid();
    var writeFailed = false;
    try { failingStore.Mark(failedCampaign, "example-hero", "unwritable"); }
    catch (IOException) { writeFailed = true; }
    catch (UnauthorizedAccessException) { writeFailed = true; }
    Check(writeFailed, "marker write failure is reported");
    Check(!failingStore.HasSeen(failedCampaign, "example-hero"),
        "failed marker write does not mark the in-memory cache");
    var rejectedUnsafeId = false;
    try { store.GetMarkerPath(secondCampaign, "../outside"); }
    catch (ArgumentException) { rejectedUnsafeId = true; }
    Check(rejectedUnsafeId, "invalid marker IDs cannot resolve to a path");
}
finally
{
    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
}

var saveKeyRenames = new SaveKeyRenameRegistry();
var renameLease = saveKeyRenames.Register("example-pack", new Dictionary<string, string>
{
    ["AFFECTER_OldTrait"] = "AFFECTER_NewTrait",
    ["UNITVISUAL_Example_OldProfile"] = "UNITVISUAL_Example_NewProfile"
});
var renamedSave = saveKeyRenames.Apply(
    "{\"Key\":\"AFFECTER_OldTrait\",\"Traits\":[\"AFFECTER_OldTrait\",\"AFFECTER_OldTraitMood\"],"
    + "\"ProfileKey\":\"UNITVISUAL_Example_OldProfile\",\"AFFECTER_OldTrait\":1}",
    out var renamedTokens, out var renamedPacks);
Check(renamedSave == "{\"Key\":\"AFFECTER_NewTrait\",\"Traits\":[\"AFFECTER_NewTrait\",\"AFFECTER_OldTraitMood\"],"
        + "\"ProfileKey\":\"UNITVISUAL_Example_NewProfile\",\"AFFECTER_NewTrait\":1}"
        && renamedTokens == 4 && renamedPacks.SequenceEqual(new[] { "example-pack" }),
    "save key renames replace complete JSON strings and property names, never part of a longer key");
const string UntouchedSave = "{\"Key\":\"AFFECTER_OtherTrait\"}";
Check(ReferenceEquals(saveKeyRenames.Apply(UntouchedSave, out var noTokens, out var noPacks), UntouchedSave)
        && noTokens == 0 && noPacks.Count == 0,
    "a save without renamed keys is returned unchanged without copying");
string RenameFailure(IReadOnlyDictionary<string, string> renames)
{
    try { saveKeyRenames.Register("other-pack", renames).Dispose(); return null; }
    catch (ArgumentException ex) { return ex.Message; }
    catch (InvalidOperationException ex) { return ex.Message; }
}
Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER Old"] = "AFFECTER_New" }) != null,
    "a save key rename with characters outside letters, digits and underscores is rejected");
Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_Same"] = "AFFECTER_Same" }) != null,
    "a save key rename to the same key is rejected");
var duplicateRename = RenameFailure(new Dictionary<string, string> { ["AFFECTER_OldTrait"] = "AFFECTER_Other" });
Check(duplicateRename != null && !duplicateRename.Contains("OldTrait"),
    "another pack cannot rename an already renamed key, and the message names no key");
Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_NewTrait"] = "AFFECTER_Third" }) != null,
    "a rename chain from an existing target is rejected");
Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_Third"] = "AFFECTER_OldTrait" }) != null,
    "a rename chain into an existing old key is rejected");
Check(RenameFailure(new Dictionary<string, string> { ["AFFECTER_A"] = "AFFECTER_B", ["AFFECTER_B"] = "AFFECTER_C" }) != null,
    "a rename chain within one registration is rejected");
renameLease.Dispose();
renameLease.Dispose();
var afterRelease = saveKeyRenames.Apply("{\"Key\":\"AFFECTER_OldTrait\"}", out var afterTokens, out _);
Check(saveKeyRenames.Count == 0 && afterTokens == 0 && afterRelease == "{\"Key\":\"AFFECTER_OldTrait\"}",
    "releasing the lease removes the renames, and a second release changes nothing");

Console.WriteLine($"PASS: {checks} deterministic checks");

sealed class TestDelversHost : IDelversHostServices
{
    internal TestDelversHost(string packageDirectory) => PackageDirectory = packageDirectory;

    public string PackageDirectory { get; }
    internal List<string> InfoMessages { get; } = new();
    public void Info(string message) => InfoMessages.Add(message);
    public void Warning(string message) { }
    public void Error(string message) { }
    public object StartCoroutine(IEnumerator routine) => throw new NotSupportedException();
    public void StopCoroutine(object handle) { }
}

sealed class TestEntity : IEntity
{
}
