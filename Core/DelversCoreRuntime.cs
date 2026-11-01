using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

public static partial class DelversCoreRuntime
{
    // static readonly, not const: a const would be baked into every pack at its build time, so
    // an older Frieren would report its own Core version after a Core update (review, F9).
    // The minimum versions below stay const on purpose: they are the pack's own requirement.
    // It goes up with every change of Tests/Core/PublicApi.txt; the list's header line carries it and ApiSurfaceSection
    // fails when the two part. 1.11.0: new test API members (the tactical formation among them); four members that never
    // left a release became internal, so the pack and tool APIs of 1.x lost nothing.
    public static readonly string ApiVersion = "1.11.0";
    public const string MinimumApiVersionForUniqueCandidatePredicates = "1.1.0";
    public const string MinimumApiVersionForFixedTraits = "1.2.0";
    public const string MinimumApiVersionForLoadIntegrations = "1.3.0";
    public const string MinimumApiVersionForNativeValueLists = "1.4.0";
    public const string MinimumApiVersionForSaveKeyRenames = "1.4.0";
    public const string MinimumApiVersionForMenuPages = "1.5.0";
    // Test campaigns by GUID, central uniqueness, "recruitment free", forced founder offers.
    public const string MinimumApiVersionForTestCampaigns = "1.6.0";
    public const string MinimumApiVersionForCampaignGuid = CampaignGuidRules.ApiVersion;
    public const string MinimumApiVersionForSettingsCopy = CoreSettingsCopyRules.ApiVersion;
    public const string MelonAssemblyName = "DungeonSettlersDelvers.Core.MelonLoader";
    public const string BepInExPluginId = "fre-yin.dungeonsettlers.delvers.core";
    public static readonly string SupportedBuilds = "DS_B.0.4.29 / Steam build 25846389 (verified binary signature)";
    private const string GameAssemblySha256 = "4D7D851991C0DB7D16E68CA6FA2F4E33945A6289405C012100D9B5E6A570C04D";
    private const string MetadataSha256 = "C11FAC80BFC461E286BDB593E7D6CA06AD1717F3DE5D3086E4708EEB6DA357DB";
    private static readonly object Gate = new();
    private static string loaderProfile;
    private static string harmonyOwner;
    private static bool initialized;
    // Each registry announces itself here; Shutdown clears them all through CoreRegistries.
    private static readonly PackIntegrationRegistry<RecruitHelper> Integrations = CoreRegistries.Track(new PackIntegrationRegistry<RecruitHelper>());
    private static readonly UniqueCandidatePredicateRegistry<RecruitCandidateData> UniqueCandidatePredicates = CoreRegistries.Track(new UniqueCandidatePredicateRegistry<RecruitCandidateData>());
    internal static readonly FixedTraitRegistry FixedTraits = CoreRegistries.Track(new FixedTraitRegistry());
    private static readonly PackLoadIntegrationRegistry LoadIntegrations = CoreRegistries.Track(new PackLoadIntegrationRegistry());
    internal static readonly SaveKeyRenameRegistry SaveKeyRenames = CoreRegistries.Track(new SaveKeyRenameRegistry());
    internal static readonly DelversMenuRegistry MenuPages = CoreRegistries.Track(new DelversMenuRegistry());
    private static readonly TestCampaignSwitches TestSwitches = CoreRegistries.Track(
        new TestCampaignSwitches(CampaignPresence.IsTestCampaign));
    private static IDisposable corePage;

    public static bool IsReady { get { lock (Gate) return initialized; } }
    public static string LoaderProfile { get { lock (Gate) return loaderProfile; } }
    // The id of Core's Harmony instance, for patches put on by hand later (FalconCritInstall); null while Core is not ready.
    internal static string HarmonyOwner { get { lock (Gate) return harmonyOwner; } }
    public static bool IsSaveAllowed => LoadIntegrityGuard.IsSaveAllowed;
    public static string SaveBlockReason => LoadIntegrityGuard.SaveBlockReason;

    public static bool SupportsApi(string minimumVersion)
    {
        if (!Version.TryParse(ApiVersion, out var actual) || !Version.TryParse(minimumVersion, out var minimum))
            return false;
        return actual.Major == minimum.Major && actual >= minimum;
    }

    public static IDisposable RegisterRecruitmentIntegration(string packId,
        Action<RecruitHelper> onCampaignRefreshed, Func<bool> suppressNativeFounderRolls)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return Integrations.Register(packId, onCampaignRefreshed, suppressNativeFounderRolls);
        }
    }

    public static IDisposable RegisterUniqueCandidatePredicate(string packId,
        Func<RecruitCandidateData, bool> predicate)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return UniqueCandidatePredicates.Register(packId, predicate);
        }
    }

    // Traits a pack character always has, in the order a fresh unit stores them.
    // If a save was written while the pack was inactive, Core restores missing
    // ones from the surviving profile ID when the save is loaded again.
    public static IDisposable RegisterFixedTraits(string packId, string profileKey,
        IEnumerable<string> orderedTraitKeys)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return FixedTraits.Register(packId, profileKey, orderedTraitKeys);
        }
    }

    // IDs a pack renamed although players' saves still contain the old ones (old key to new
    // key). Core rewrites them in the save text before the game parses it. Keys may use
    // letters, digits and underscores; an old key belongs to one pack; chains are rejected.
    public static IDisposable RegisterSaveKeyRenames(string packId,
        IReadOnlyDictionary<string, string> renames)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return SaveKeyRenames.Register(packId, renames);
        }
    }

    public static IReadOnlyList<string> GetFixedTraits(string profileKey)
        => FixedTraits.TryGet(profileKey, out _, out var traits) ? traits : null;

    // A page of the Delvers menu, the entry under "Quit" in the title menu (Core shows the menu, every mod offers its
    // settings there). Rows are dictionaries, built
    // with DelversMenuRows or by hand, so mods without a reference to Core can register through
    // reflection. The menu asks for the rows whenever the page is opened. Since API 1.5.0.
    public static IDisposable RegisterMenuPage(string packId, string title,
        Func<IEnumerable<IReadOnlyDictionary<string, object>>> rows)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return MenuPages.Register(packId, title, rows);
        }
    }

    // Opens the Delvers menu, on the page of the given pack if it has one.
    public static void OpenMenu(string packId = null)
    {
        if (!IsReady) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
        DelversMenuWindow.Open(packId);
    }

    public static bool IsMenuOpen => DelversMenuWindow.IsOpen;

    public static void CloseMenu() => DelversMenuWindow.Close();

    public static IDisposable RegisterLoadIntegration(string packId,
        BeforeComponentsDeserializeHandler beforeComponentsDeserialize = null,
        BeforeUnitProfileDeserializeHandler beforeUnitProfileDeserialize = null,
        AfterUnitProfileDeserializeHandler afterUnitProfileDeserialize = null,
        AfterCampaignLoadedHandler afterCampaignLoaded = null)
    {
        lock (Gate)
        {
            if (!initialized) throw new InvalidOperationException("Dungeon Settlers Delvers Core is not initialized.");
            return LoadIntegrations.Register(packId, beforeComponentsDeserialize,
                beforeUnitProfileDeserialize, afterUnitProfileDeserialize, afterCampaignLoaded);
        }
    }

    public static void AuditFixedTraitRestore()
    {
        if (!IsReady) throw new InvalidOperationException("Core fixed-trait restore is not active.");
        FixedTraitRestore.RunAudit();
    }

    public static void AuditNativeFounders()
    {
        if (!IsReady) throw new InvalidOperationException("Core founder registration is not active.");
        NativeFoundersAudit.Run();
    }

    internal static bool SuppressNativeFounderRolls
    {
        get
        {
            return Integrations.AnyPolicy((packId, ex) =>
                DelversHost.Warning("CORE_PACK_POLICY_FAILED pack=" + packId + ": " + ex.Message));
        }
    }

    internal static UniqueCandidateLockPolicy GetUniqueCandidateLockPolicy(RecruitCandidateData candidate)
    {
        if (candidate == null) return UniqueCandidateLockPolicy.None;
        if (NativeFounders.IsUniqueCandidate(candidate)) return UniqueCandidateLockPolicy.PrimaryTraits;
        return UniqueCandidatePredicates.IsUnique(candidate, (packId, ex) =>
                DelversHost.Warning("CORE_UNIQUE_CANDIDATE_PREDICATE_FAILED pack=" + packId + ": " + ex.Message))
            ? UniqueCandidateLockPolicy.ProfileOrPrimaryTraits
            : UniqueCandidateLockPolicy.None;
    }

    internal static void NotifyCampaignRefreshed(RecruitHelper helper)
    {
        try { NativeFoundersGuild.ObservePlayerUnits(helper); }
        catch (Exception ex) { DelversHost.Warning("CORE_CAMPAIGN_REFRESH_FAILED: " + ex.Message); }
        Integrations.Notify(helper, (packId, ex) =>
            DelversHost.Warning("CORE_PACK_CAMPAIGN_REFRESH_FAILED pack=" + packId + ": " + ex.Message));
        try { NativeFoundersGuild.LoosenExistingForTest(helper); }
        catch (Exception ex) { DelversHost.Warning("CORE_TEST_RECRUITMENT_FREE_FAILED: " + ex.Message); }
    }

    internal static void NotifyBeforeComponentsDeserialize(
        ComponentSaveList saved,
        IEntity entity)
        => LoadIntegrations.NotifyBeforeComponents(saved, entity, ReportLoadIntegrationFailure);

    internal static void NotifyBeforeUnitProfileDeserialize(ComponentSaveBaseData saved, IEntity entity)
        => LoadIntegrations.NotifyBeforeProfile(saved, entity, ReportLoadIntegrationFailure);

    internal static void NotifyAfterUnitProfileDeserialize(UnitProfileComponent profile)
        => LoadIntegrations.NotifyAfterProfile(profile, ReportLoadIntegrationFailure);

    internal static void NotifyAfterCampaignLoaded(CampaignDataContainer campaign)
    {
        LoadIntegrations.NotifyAfterCampaign(campaign, ReportLoadIntegrationFailure);
        // Core's own work after a load never marks the load as failed: it only logs.
        ReviveCampaign.OnCampaignLoaded();
    }

    internal static void AuditLoadPatches()
        => LoadPatchAudit.Run(harmonyOwner);

    private static void ReportLoadIntegrationFailure(string packId, string stage, Exception exception)
    {
        DelversHost.Error("CORE_PACK_LOAD_CALLBACK_FAILED pack=" + packId
            + " stage=" + stage + " error=" + exception);
        LoadIntegrityGuard.ReportFailure("pack=" + packId + ":" + stage, exception);
    }
}

[HarmonyPatch(typeof(RecruitHelper), nameof(RecruitHelper.RefreshExist))]
internal static class DelversRecruitmentRefreshPatch
{
    private static void Postfix(RecruitHelper __instance)
        => DelversCoreRuntime.NotifyCampaignRefreshed(__instance);
}
