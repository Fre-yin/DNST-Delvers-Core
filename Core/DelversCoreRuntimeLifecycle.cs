using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, its life cycle: Initialize patches the game and wires Core up (PatchAllClasses names every broken
// patch class), LateFrame is the loaders' frame hook, Shutdown takes everything down again, and VerifyGameBuild checks the
// game's build signature. Versions, registries and state are in DelversCoreRuntime.cs.
public static partial class DelversCoreRuntime
{
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
                CoreStartup.Log("init");
                VerifyGameBuild();
                LoadIntegrityGuard.ResetLifecycle();
                PatchAllClasses(harmony);
                harmonyOwner = harmony.Id;
                try { UniqueCandidateLocalization.EnsureCurrent(); }
                catch (Exception ex) { DelversHost.Warning("CORE_LOCALIZATION_REGISTRATION_FAILED: " + ex.Message); }
                corePage ??= MenuPages.Register(DelversCoreMenuPage.PackId, DelversCoreMenuPage.Title, DelversCoreMenuPage.Rows);
                CoreSaveStore.BeforeOwnSave = ReviveCampaign.BeforeSave;
                CoreSaveStore.LegacyPresence = CampaignPresence.LegacyMarkers;
                ReviveService.AfterRevive = ReviveSickness.Begin;
                ReviveScroll.Initialize();
                Bandages.Initialize();
                BossItems.Initialize();
                LifebloomBook.Initialize();
                // The bandits' moves go through the group AI's zones (GroupAiZones) without the two
                // knowing each other.
                GroupAiBanditMoves.Order = GroupAiZones.OrderMove;
                CoreProbes.Refresh();
                initialized = true;
                DelversHost.Info($"DELVERS_CORE_READY api={ApiVersion} loader={loader} founders=Lowell,Liana,Kragas; {SupportedBuilds} verified.");
                CoreStartup.Log("ready");
            }
            catch
            {
                harmony.UnpatchSelf();
                corePage?.Dispose();
                corePage = null;
                LoadIntegrityGuard.ResetLifecycle();
                harmonyOwner = null;
                loaderProfile = null;
                throw;
            }
        }
    }

    // PatchAll stops at the first broken class and may only say "IL Compile Error (unknown
    // location)" (Melon, DS_B.0.4.29). Patching class by class names every broken
    // class and its target in one start. Any failure still leaves Core not ready: the caller
    // unpatches everything. The probe classes are not part of it: CoreProbes patches them when
    // their group is on, and a failing one never stops Core.
    private static void PatchAllClasses(Harmony harmony)
    {
        var failures = PatchPlanRules.Apply(CoreProbeRules.WithoutProbeClasses(AccessTools.GetTypesFromAssembly(typeof(DelversCoreRuntime).Assembly)),
            type => harmony.CreateClassProcessor(type).Patch(), DescribePatchTarget, ReportPatchClassFailed);
        var message = PatchPlanRules.Message(failures);
        if (message != null) throw new InvalidOperationException(message);
    }

    private static void ReportPatchClassFailed(PatchPlanRules.Failure failure)
        => DelversHost.Error("CORE_PATCH_CLASS_FAILED class=" + failure.PatchClass.FullName + " target=" + failure.Target
            + " error=" + DescribeInnermost(failure.Error));

    private static string DescribePatchTarget(Type patchClass)
    {
        try
        {
            var info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
            var name = (info.declaringType?.Name ?? "?") + "." + (info.methodName ?? "TargetMethod()");
            if (info.argumentTypes == null) return name;
            var arguments = new List<string>();
            foreach (var argument in info.argumentTypes) arguments.Add(argument?.Name ?? "?");
            return name + "(" + string.Join(", ", arguments) + ")";
        }
        catch (Exception) { return "unknown"; }
    }

    internal static string DescribeInnermost(Exception error)
    {
        var inner = error;
        while (inner.InnerException != null) inner = inner.InnerException;
        var text = inner == error ? error.Message : error.Message + " / " + inner.GetType().Name + ": " + inner.Message;
        return text.Replace('\n', ' ').Replace('\r', ' ');
    }

    // Called by the loader adapter every frame after the game's animations (LateUpdate), before drawing. Internal: only the
    // Core adapters call it (InternalsVisibleTo in the Core project); a pack has no business driving Core's frame.
    internal static void LateFrame()
    {
        if (!IsReady) return;
        try { BanditTint.LateFrame(); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED frame: " + ex.Message); }
        try { CoreShieldWave.AnimateAll(); }
        catch (Exception ex) { ShieldWaveFailed(ex); }
    }

    private static int shieldWaveErrors;

    // A failure outside the bands' own step (each band catches its own, CoreShieldBand.WaveFailed): three lines, then one note.
    private static void ShieldWaveFailed(Exception ex)
    {
        shieldWaveErrors++;
        if (shieldWaveErrors <= 3) DelversHost.Warning("CORE_SHIELD_WAVE_FAILED frame: " + ex.GetType().Name + ": " + ex.Message);
        else if (shieldWaveErrors == 4) DelversHost.Warning("CORE_SHIELD_WAVE_FAILED frame: further failures not logged");
    }

    public static void Shutdown(string loader)
    {
        lock (Gate)
        {
            if (!initialized || !string.Equals(loaderProfile, loader, StringComparison.Ordinal)) return;
            initialized = false;
            loaderProfile = null;
            harmonyOwner = null;
            CoreProbes.Shutdown();
            CoreRegistries.ClearAll();
            corePage = null;
            DelversMenuWindow.ResetLifecycle();
            DelversMenuOptionsTab.ResetLifecycle();
            LoadIntegrityGuard.ResetLifecycle();
            LoadPatchAudit.ResetLifecycle();
            CombatDifficultyTest.ResetLifecycle();
            BanditDifficulty.ResetLifecycle();
            UniqueCandidateRerollUI.ResetLifecycle();
            UniqueCandidateLocalization.ResetLifecycle();
            CoreSettings.ResetLifecycle();
            ApocalypseDifficulty.ResetLifecycle();
            BanditCampProbe.ResetLifecycle();
            BanditCampBuilder.ResetLifecycle();
            BanditCampFurnish.ResetLifecycle();
            BanditCampFootprint.ResetLifecycle();
            BanditCampWake.ResetLifecycle();
            CampGuardianStones.ResetLifecycle();
            BanditCampTemplates.ResetLifecycle();
            HealSources.ResetLifecycle();
            BossEffects.ResetLifecycle();
            FalconEyes.ResetLifecycle();
            TargetSortGuard.ResetLifecycle();
            SummonLimit.ResetLifecycle();
            ApocalypseSlowDisplay.ResetLifecycle();
            CoreSaveStore.ResetLifecycle();
            ReviveCampaign.ResetLifecycle();
            ReviveScroll.ResetLifecycle();
            BandageContent.ResetLifecycle();
            BandageOnOther.ResetLifecycle();
            WoundProbe.ResetLifecycle();
            GroupAiSettings.ResetLifecycle();
            FirstHints.ResetLifecycle();
            GroupAiBanditMoves.Order = null;
            GroupAiBandits.ProtectHealer = true;
            CoreAssets.ResetLifecycle();
            CoreSaveItems.Clear();
            TacticalFormationWindow.ResetLifecycle();
            ResetSummons();
            TacticalFormationTest.ResetLifecycle();
            TacticalFormationButton.ResetLifecycle();
            WeakBandits.Reset();
        }
    }

    // What the summons' parts know of a campaign's units (owners, followed targets, the probe's counts): a campaign that starts or
    // is loaded begins without the last one's (ApocalypseDifficulty.StartCampaignRules), and so does a lifecycle reset.
    internal static void ResetSummons()
    {
        DelversSummons.Forget();
        SummonProbe.Reset();
        SummonSide.Reset();
        SummonFollowTarget.Reset();
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
