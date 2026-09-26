using HarmonyLib;
#if BEPINEX
using global::Refactor;
using global::Refactor.Component;
#else
using Il2CppRefactor;
using Il2CppRefactor.Component;
#endif
#if BEPINEX
using global::Refactor.Main;
#else
using Il2CppRefactor.Main;
#endif
#if BEPINEX
using global::Refactor.Main.Event;
#else
using Il2CppRefactor.Main.Event;
#endif
#if BEPINEX
using global::Refactor.Map;
#else
using Il2CppRefactor.Map;
#endif
#if BEPINEX
using global::Refactor.Util;
#else
using Il2CppRefactor.Util;
#endif
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

public static class DelversCoreRuntime
{
    public const string ApiVersion = "1.1.0";
    public const string MinimumApiVersionForUniqueCandidatePredicates = "1.1.0";
    public const string MelonAssemblyName = "DungeonSettlersDelvers.Core.MelonLoader";
    public const string BepInExPluginId = "fre-yin.dungeonsettlers.delvers.core";
    public const string SupportedBuilds = "DS_B.0.4.23 / Steam builds 25269660 and 25284551 (same verified binary signature)";
    private const string GameAssemblySha256 = "8049A17906060F10A5A35ACB530740C6D61C2F07E6AE814A5902B5CF76A63017";
    private const string MetadataSha256 = "8D462701B21307252B6A0A5B0E77EECDFE7E10BC54F9D66EA59A1127E371DF47";
    private static readonly object Gate = new();
    private static string loaderProfile;
    private static bool initialized;
    private static readonly PackIntegrationRegistry<RecruitHelper> Integrations = new();
    private static readonly UniqueCandidatePredicateRegistry<RecruitCandidateData> UniqueCandidatePredicates = new();

    public static bool IsReady { get { lock (Gate) return initialized; } }
    public static string LoaderProfile { get { lock (Gate) return loaderProfile; } }

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
                harmony.PatchAll(typeof(DelversCoreRuntime).Assembly);
                try { UniqueCandidateLocalization.EnsureCurrent(); }
                catch (Exception ex) { DelversHost.Warning("CORE_LOCALIZATION_REGISTRATION_FAILED: " + ex.Message); }
                initialized = true;
                DelversHost.Info($"DELVERS_CORE_READY api={ApiVersion} loader={loader} founders=Lowell,Liana,Kragas; {SupportedBuilds} verified.");
            }
            catch
            {
                harmony.UnpatchSelf();
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

    public static void Shutdown(string loader)
    {
        lock (Gate)
        {
            if (!initialized || !string.Equals(loaderProfile, loader, StringComparison.Ordinal)) return;
            initialized = false;
            loaderProfile = null;
            Integrations.Clear();
            UniqueCandidatePredicates.Clear();
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
