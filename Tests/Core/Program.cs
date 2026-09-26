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
