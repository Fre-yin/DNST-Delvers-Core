using HarmonyLib;
using Il2CppInterop.Runtime;
#if BEPINEX
using global::Refactor;
#else
using Il2CppRefactor;
#endif
#if BEPINEX
using global::Refactor.Component;
#else
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
#if BEPINEX
using Il2CppCandidates = Il2CppSystem.Collections.Generic.List<global::Refactor.RecruitCandidateData>;
#else
using Il2CppCandidates = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.RecruitCandidateData>;
#endif

namespace DungeonSettlersDelvers.Core;

// The game's own story presets keep their original traits, equipment and
// native gold price. A marker remembers each character after death or reload.
internal static class NativeFoundersGuild
{
    internal const float RollChancePerMatchingCandidate = 0.08f;
    // Own generator: drawing from UnityEngine.Random would shift every later
    // native roll that uses the shared Unity random state.
    private static readonly System.Random Rolls = new();

    internal static bool Seen(Il2CppSystem.Guid campaign, NativeFounders.Entry entry)
        => entry != null && CampaignPresence.Seen(campaign.ToString(),
            entry.PresenceId, CampaignPresence.LegacyMarkerName(campaign.ToString(), "-" + entry.Name));

    internal static void Mark(Il2CppSystem.Guid campaign, NativeFounders.Entry entry)
    {
        if (entry == null) return;
        CampaignPresence.Mark(campaign.ToString(), entry.PresenceId,
            CampaignPresence.LegacyMarkerName(campaign.ToString(), "-" + entry.Name),
            entry.Name + " was present in this campaign.\n");
    }

    private static NativeFounders.Entry ExactMatch(string profile, string backgroundTrait)
    {
        foreach (var entry in NativeFounders.All)
            if (entry.ProfileKey == profile && entry.BackgroundTrait == backgroundTrait) return entry;
        return null;
    }

    private static bool UsesFounderPortrait(string profileKey, NativeFounders.Entry entry)
    {
        if (string.IsNullOrEmpty(profileKey)) return false;
        var manager = DataSheetManager.Instance;
        var expected = manager?.GetUnitProfileData(entry.ProfileKey)?.Portrait;
        return !string.IsNullOrEmpty(expected)
            && manager.GetUnitProfileData(profileKey)?.Portrait == expected;
    }

    internal static void ObservePlayerUnits(RecruitHelper helper)
    {
        try { ObservePlayerUnitsCore(helper); }
        catch (Exception ex)
        {
            DelversHost.Error("NATIVE_FOUNDER_HISTORY_SCAN_FAILED: " + ex);
        }
    }

    private static void ObservePlayerUnitsCore(RecruitHelper helper)
    {
        var clan = helper?._clanDataContainer;
        var entities = helper?._entityContainer?.GetAllEntitiesOnCampaign();
        if (clan == null || entities == null) return;
        foreach (var key in entities.Keys)
        {
            var unit = entities[key]?.TryCast<UnitEntity>();
            if (unit == null || unit.Faction != FactionType.Player) continue;
            // Names can change, and ordinary rolls can share a founder portrait.
            // The native preset's background trait distinguishes the founder.
            var entry = NativeFounders.ForProfile(unit.ProfileKey);
            if (entry != null && EntityComponent._instance?
                    .GetComponentOf<AffecterComponent>(unit.Cast<IEntity>())?.Has(entry.BackgroundTrait) == true)
                Mark(clan.CampaignGuid, entry);
        }
        foreach (var entry in NativeFounders.All)
            if (Seen(clan.CampaignGuid, entry))
            {
                helper._existProfiles?.Add(entry.ProfileKey);
                helper._existNames?.Add(entry.NameTextKey);
            }
    }

    private static bool HasLiveCollision(RecruitHelper helper, NativeFounders.Entry entry)
    {
        var entities = helper?._entityContainer?.GetAllEntitiesOnCampaign();
        if (entities == null) return true;
        foreach (var key in entities.Keys)
        {
            var unit = entities[key]?.TryCast<UnitEntity>();
            if (unit != null && unit.Faction == FactionType.Player
                && (unit.ProfileKey == entry.ProfileKey || unit.NameTextKey == entry.NameTextKey
                    || UsesFounderPortrait(unit.ProfileKey, entry))) return true;
        }
        return false;
    }

    private static bool CollidesWithOtherCandidates(Il2CppCandidates candidates, int index, NativeFounders.Entry entry)
    {
        for (var otherIndex = 0; otherIndex < candidates.Count; otherIndex++)
        {
            if (otherIndex == index) continue;
            var other = candidates[otherIndex];
            if (other != null && (other.UnitProfileKey == entry.ProfileKey
                || other.NameTextKey == entry.NameTextKey
                || UsesFounderPortrait(other.UnitProfileKey, entry))) return true;
        }
        return false;
    }

    internal static void AddNativePresets(RecruitHelper helper, Il2CppCandidates candidates)
    {
        try { AddNativePresetsCore(helper, candidates); }
        catch (Exception ex)
        {
            // Keep the native list usable if a preset cannot be built.
            DelversHost.Error("NATIVE_FOUNDER_GUILD_ROLL_FAILED: " + ex);
        }
    }

    private static void AddNativePresetsCore(RecruitHelper helper, Il2CppCandidates candidates)
    {
        if (helper == null || candidates == null || helper._clanDataContainer == null
            || DelversCoreRuntime.SuppressNativeFounderRolls
            || helper._campaignSetting?.CampaignStart?.GetScenario() == ScenarioType.CrawlingDoom) return;
        var campaign = helper._clanDataContainer.CampaignGuid;
        // A stable campaign identity is required for the once-only guarantee.
        if (!CampaignPresence.IsValidCampaign(campaign.ToString())) return;
        for (var index = 0; index < candidates.Count; index++)
        {
            var current = candidates[index];
            var entry = NativeFounders.ForCandidate(current);
            if (entry == null || Seen(campaign, entry) || HasLiveCollision(helper, entry)
                || CollidesWithOtherCandidates(candidates, index, entry)
                || !NativeFounders.IsNativePresetAvailable(entry)
                || Rolls.NextDouble() >= RollChancePerMatchingCandidate) continue;
            var preset = DataSheetManager.Instance.GetCandidatePresetData(entry.PresetKey);
            var nativeCandidate = helper.CreatePresetCandidate(preset);
            if (nativeCandidate == null || ExactMatch(nativeCandidate.UnitProfileKey,
                    nativeCandidate.BackgroundTrait) != entry) continue;
            // Native CreatePresetCandidate calculates the ordinary gold price.
            nativeCandidate.RecruitPrice = UniqueCandidateRules.CapNativeFounderGuildPrice(
                new UniqueCandidateIdentity(nativeCandidate.UnitProfileKey, nativeCandidate.NameTextKey,
                    nativeCandidate.BackgroundTrait), nativeCandidate.RecruitPrice);
            candidates[index] = nativeCandidate;
            DelversHost.Info($"NATIVE_FOUNDER_GUILD_ROLL name={entry.Name} priceGold={nativeCandidate.RecruitPrice}");
        }
    }

    internal static void OnPlayerSpawn(EntityLifecycleHelper helper, RecruitCandidateData candidate,
        FactionType faction, UnitEntity unit)
    {
        try
        {
            if (helper?._clanDataContainer == null || unit == null || faction != FactionType.Player) return;
            // A player may rename the candidate before the first spawn. The
            // native background trait distinguishes it from the basic preset
            // even when both use the same profile and original name.
            var entry = candidate == null ? null : NativeFounders.ForProfile(candidate.UnitProfileKey);
            if (entry != null && candidate.BackgroundTrait == entry.BackgroundTrait
                && unit.ProfileKey == entry.ProfileKey)
                Mark(helper._clanDataContainer.CampaignGuid, entry);
        }
        catch (Exception ex)
        {
            DelversHost.Error("NATIVE_FOUNDER_SPAWN_HISTORY_FAILED: " + ex);
        }
    }
}

[HarmonyPatch(typeof(RecruitHelper), "CreateRecruitCandidates")]
internal static class NativeFoundersGuildCandidatesPatch
{
    private static void Postfix(RecruitHelper __instance, Il2CppCandidates __result)
        => NativeFoundersGuild.AddNativePresets(__instance, __result);
}

[HarmonyPatch(typeof(EntityLifecycleHelper), nameof(EntityLifecycleHelper.SpawnUnitFromCandidateData),
    new[] { typeof(MapType), typeof(RecruitCandidateData), typeof(Vector2Int), typeof(FactionType) })]
internal static class NativeFoundersSpawnPatch
{
    private static void Postfix(EntityLifecycleHelper __instance, RecruitCandidateData __1,
        FactionType __3, UnitEntity __result)
        => NativeFoundersGuild.OnPlayerSpawn(__instance, __1, __3, __result);
}
