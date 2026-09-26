using HarmonyLib;
#if BEPINEX
using global::Refactor;
#else
using Il2CppRefactor;
#endif
#if BEPINEX
using global::Refactor.Main.Event;
#else
using Il2CppRefactor.Main.Event;
#endif
#if BEPINEX
using global::Refactor.UI;
#else
using Il2CppRefactor.UI;
#endif
#if BEPINEX
using global::Refactor.Util;
#else
using Il2CppRefactor.Util;
#endif

namespace DungeonSettlersDelvers.Core;

// The campaign generator needs a live campaign injector. This startup audit
// checks the native presets and hook registration without creating a campaign.
internal static class NativeFoundersAudit
{
    internal static void Run()
    {
        var manager = DataSheetManager.Instance;
        var portraits = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in NativeFounders.All)
        {
            Require(NativeFounders.IsNativePresetAvailable(entry), "native preset " + entry.Name);
            var preset = manager.GetCandidatePresetData(entry.PresetKey);
            Require(preset.Level == 1 && preset.UnitProfileKey == entry.ProfileKey
                && preset.NameTextKey == entry.NameTextKey
                && preset.BackgroundTrait == entry.BackgroundTrait
                && preset.RaceType == entry.Race,
                "preset identity " + entry.Name);
            var portrait = manager.GetUnitProfileData(entry.ProfileKey)?.Portrait;
            Require(!string.IsNullOrEmpty(portrait) && portraits.Add(portrait),
                "unique native portrait " + entry.Name);
        }

        // The story and tutorial presets reuse these portraits. In particular,
        // BasicWarrior also reuses Lowell's name; profile/name alone is not
        // sufficient evidence for a once-per-campaign marker.
        var basicWarrior = manager.GetCandidatePresetData("CADIDATEPRESET_BasicWarrior");
        Require(basicWarrior != null
            && basicWarrior.UnitProfileKey == NativeFounders.Lowell.ProfileKey
            && basicWarrior.NameTextKey == NativeFounders.Lowell.NameTextKey
            && basicWarrior.BackgroundTrait != NativeFounders.Lowell.BackgroundTrait,
            "ordinary warrior must not count as Lowell");

        var target = AccessTools.Method(typeof(CreateCampaignPresenter),
            "TryGenerateEstablishSlot", new[] { typeof(int), typeof(bool) });
        var patched = target == null ? null : HarmonyLib.Harmony.GetPatchInfo(target);
        Require(patched?.Postfixes.Any(patch => patch.PatchMethod?.DeclaringType
            == typeof(NativeFoundersEstablishRollPatch)) == true,
            "custom-campaign generation hook");
        Require(NativeFoundersEstablishRollPatch.BindingsReady,
            "custom-campaign slot bindings");

        Require(UniqueCandidateRerollUI.BindingsReady && UniqueCandidateRerollUI.HasRequiredHooks,
            "unique-candidate reroll, restriction, and language hooks");

        var guildTarget = AccessTools.Method(typeof(RecruitHelper), "CreateRecruitCandidates");
        var guildPatched = guildTarget == null ? null : HarmonyLib.Harmony.GetPatchInfo(guildTarget);
        Require(guildPatched?.Postfixes.Any(patch => patch.PatchMethod?.DeclaringType
            == typeof(NativeFoundersGuildCandidatesPatch)) == true,
            "guild candidate hook");

        UniqueCandidateLocalization.ValidateAllLanguages();
        DelversHost.Info("NATIVE_FOUNDERS_DATA_AUDIT_PASS presets=3 establishHook=true guildHook=registered uniqueRerollUiHooks=true lockConflictFallback=true");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Native founder audit: " + name);
    }
}
