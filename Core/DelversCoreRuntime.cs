using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

public static class DelversCoreRuntime
{
    public const string ApiVersion = "1.4.0";
    public const string MinimumApiVersionForUniqueCandidatePredicates = "1.1.0";
    public const string MinimumApiVersionForFixedTraits = "1.2.0";
    public const string MinimumApiVersionForLoadIntegrations = "1.3.0";
    public const string MinimumApiVersionForNativeValueLists = "1.4.0";
    public const string MinimumApiVersionForSaveKeyRenames = "1.4.0";
    public const string MelonAssemblyName = "DungeonSettlersDelvers.Core.MelonLoader";
    public const string BepInExPluginId = "fre-yin.dungeonsettlers.delvers.core";
    public const string SupportedBuilds = "DS_B.0.4.23 / Steam builds 25269660 and 25284551 (same verified binary signature)";
    private const string GameAssemblySha256 = "8049A17906060F10A5A35ACB530740C6D61C2F07E6AE814A5902B5CF76A63017";
    private const string MetadataSha256 = "8D462701B21307252B6A0A5B0E77EECDFE7E10BC54F9D66EA59A1127E371DF47";
    private static readonly object Gate = new();
    private static string loaderProfile;
    private static string harmonyOwner;
    private static bool initialized;
    private static readonly PackIntegrationRegistry<RecruitHelper> Integrations = new();
    private static readonly UniqueCandidatePredicateRegistry<RecruitCandidateData> UniqueCandidatePredicates = new();
    internal static readonly FixedTraitRegistry FixedTraits = new();
    private static readonly PackLoadIntegrationRegistry LoadIntegrations = new();
    internal static readonly SaveKeyRenameRegistry SaveKeyRenames = new();

    public static bool IsReady { get { lock (Gate) return initialized; } }
    public static string LoaderProfile { get { lock (Gate) return loaderProfile; } }
    public static bool IsSaveAllowed => LoadIntegrityGuard.IsSaveAllowed;
    public static string SaveBlockReason => LoadIntegrityGuard.SaveBlockReason;

    public static bool SupportsApi(string minimumVersion)
    {
        if (!Version.TryParse(ApiVersion, out var actual) || !Version.TryParse(minimumVersion, out var minimum))
            return false;
        return actual.Major == minimum.Major && actual >= minimum;
    }

    public static void Initialize(Harmony harmony, string loader)
    {
        if (harmony == null) throw new ArgumentNullException(nameof(harmony));
        if (loader is not ("Melon" or "BepInEx")) throw new ArgumentOutOfRangeException(nameof(loader));
        lock (Gate)
        {
            if (initialized)
            {
                if (!string.Equals(loaderProfile, loader, StringComparison.Ordinal))
                    throw new InvalidOperationException("A different Dungeon Settlers Delvers loader profile is already active.");
                return;
            }
            if (!DelversHost.IsBound) throw new InvalidOperationException("Core host services must be bound before initialization.");
            loaderProfile = loader;
            try
            {
                VerifyGameBuild();
                LoadIntegrityGuard.ResetLifecycle();
                harmony.PatchAll(typeof(DelversCoreRuntime).Assembly);
                harmonyOwner = harmony.Id;
                try { UniqueCandidateLocalization.EnsureCurrent(); }
                catch (Exception ex) { DelversHost.Warning("CORE_LOCALIZATION_REGISTRATION_FAILED: " + ex.Message); }
                initialized = true;
                DelversHost.Info($"DELVERS_CORE_READY api={ApiVersion} loader={loader} founders=Lowell,Liana,Kragas; {SupportedBuilds} verified.");
            }
            catch
            {
                harmony.UnpatchSelf();
                LoadIntegrityGuard.ResetLifecycle();
                harmonyOwner = null;
                loaderProfile = null;
                throw;
            }
        }
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
        => LoadIntegrations.NotifyAfterCampaign(campaign, ReportLoadIntegrationFailure);

    internal static void AuditLoadPatches()
        => LoadPatchAudit.Run(harmonyOwner);

    private static void ReportLoadIntegrationFailure(string packId, string stage, Exception exception)
    {
        DelversHost.Error("CORE_PACK_LOAD_CALLBACK_FAILED pack=" + packId
            + " stage=" + stage + " error=" + exception);
        LoadIntegrityGuard.ReportFailure("pack=" + packId + ":" + stage, exception);
    }

    public static void Shutdown(string loader)
    {
        lock (Gate)
        {
            if (!initialized || !string.Equals(loaderProfile, loader, StringComparison.Ordinal)) return;
            initialized = false;
            loaderProfile = null;
            harmonyOwner = null;
            Integrations.Clear();
            UniqueCandidatePredicates.Clear();
            FixedTraits.Clear();
            LoadIntegrations.Clear();
            SaveKeyRenames.Clear();
            LoadIntegrityGuard.ResetLifecycle();
            LoadPatchAudit.ResetLifecycle();
            UniqueCandidateRerollUI.ResetLifecycle();
            UniqueCandidateLocalization.ResetLifecycle();
        }
    }

    private static void VerifyGameBuild()
    {
        var root = Path.GetDirectoryName(Application.dataPath);
        var assemblyPath = Path.Combine(root, "GameAssembly.dll");
        var metadataPath = Path.Combine(Application.dataPath, "il2cpp_data", "Metadata", "global-metadata.dat");
        if (FileHash(assemblyPath) != GameAssemblySha256 || FileHash(metadataPath) != MetadataSha256)
            throw new InvalidOperationException("Diese Ausgabe benötigt die geprüfte Spielsignatur: " + SupportedBuilds + ".");
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var hash = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream));
    }

}

[HarmonyPatch(typeof(RecruitHelper), nameof(RecruitHelper.RefreshExist))]
internal static class DelversRecruitmentRefreshPatch
{
    private static void Postfix(RecruitHelper __instance)
        => DelversCoreRuntime.NotifyCampaignRefreshed(__instance);
}
