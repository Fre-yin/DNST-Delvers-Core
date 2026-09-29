using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using ReadOnlyStrings = Il2CppSystem.Collections.Generic.IReadOnlyCollection<string>;

namespace DungeonSettlersDelvers.Core;

internal static class NativeFoundersEstablish
{
    private const uint RollsPerFounder = 24;

    internal static void TryReplace(EstablishUnitGenerator generator, string seed, int slotIndex,
        int refreshCount, ReadOnlyStrings excludedNames, ReadOnlyStrings excludedPortraits,
        EstablishUnitRestriction restriction, ref EstablishUnitGenerationResult result)
    {
        try
        {
            if (!string.IsNullOrEmpty(result.Token.PresetKey)) return;
            var entry = NativeFounders.ForCandidate(result.Candidate);
            if (entry == null || !NativeFounders.IsNativePresetAvailable(entry)
                || !MatchesRestriction(entry, restriction)) return;

            // Respect the player's manually locked character details. Presets
            // are fixed and must not silently overwrite those locks.
            var locks = restriction.Locks;
            if (locks.MajorStatsLocked || locks.MainTraitsLocked || locks.SubTraitsLocked) return;

            var profile = DataSheetManager.Instance.GetUnitProfileData(entry.ProfileKey);
            if (Contains(excludedPortraits, profile.Portrait)
                || Contains(excludedNames, entry.NameTextKey)
                || Contains(excludedNames, entry.Name)) return;

            // Deterministic rare outcome within each matching race/gender group.
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{seed}|{slotIndex}|{refreshCount}|{entry.PresetKey}|native-founder"));
            if (BitConverter.ToUInt32(bytes, 0) % RollsPerFounder != 0) return;

            var sourceToken = result.Token;
            var token = new EstablishUnitSelectionToken
            {
                SlotIndex = sourceToken.SlotIndex,
                RefreshCount = sourceToken.RefreshCount,
                GenerationSeed = sourceToken.GenerationSeed,
                ConstraintRelaxedFlag = sourceToken.ConstraintRelaxedFlag,
                PresetKey = entry.PresetKey,
                Restriction = sourceToken.Restriction,
                ExcludedPortraitKeys = sourceToken.ExcludedPortraitKeys,
                OverriddenNameTextKey = string.Empty
            };
            if (!generator.TryGenerateFromToken(token, excludedNames, out var candidate)
                || candidate == null || candidate.UnitProfileKey != entry.ProfileKey
                || candidate.NameTextKey != entry.NameTextKey)
            {
                DelversHost.Info(
                    $"NATIVE_FOUNDER_ESTABLISH_TOKEN_REJECTED name={entry.Name} slot={slotIndex} refresh={refreshCount}");
                return;
            }

            result = new EstablishUnitGenerationResult(candidate, token);
            DelversHost.Info(
                $"NATIVE_FOUNDER_ESTABLISH_ROLL name={entry.Name} slot={slotIndex} refresh={refreshCount} preset={entry.PresetKey}");
        }
        catch (Exception ex)
        {
            // Keep the native result if a preset cannot be assembled.
            DelversHost.Error("NATIVE_FOUNDER_ESTABLISH_FAILED: " + ex);
        }
    }

    private static bool MatchesRestriction(NativeFounders.Entry entry, EstablishUnitRestriction restriction)
        => (!restriction.UseRaceRestriction || restriction.RaceType == entry.Race)
            && (!restriction.UseGenderRestriction || restriction.GenderType == entry.Gender)
            && (!restriction.UseProfileRestriction || restriction.UnitProfileKey == entry.ProfileKey);

    private static bool Contains(ReadOnlyStrings values, string expected)
    {
        if (values == null || string.IsNullOrEmpty(expected)) return false;
        var set = values.TryCast<Il2CppSystem.Collections.Generic.HashSet<string>>();
        return set?.Contains(expected) == true;
    }
}

[HarmonyPatch]
internal static class NativeFoundersEstablishRollPatch
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type PresenterType = typeof(CreateCampaignPresenter);
    private static readonly Type SlotType = PresenterType.GetNestedType("EstablishSlotState", BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo Slots = PresenterType.GetProperty("_establishSlots", AllInstance);
    private static readonly PropertyInfo Generator = PresenterType.GetProperty("_establishUnitGenerator", AllInstance);
    private static readonly PropertyInfo Seed = PresenterType.GetProperty("_seed", AllInstance);
    private static readonly PropertyInfo Candidate = SlotType?.GetProperty("Candidate", AllInstance);
    private static readonly PropertyInfo Token = SlotType?.GetProperty("Token", AllInstance);
    private static readonly PropertyInfo Restriction = SlotType?.GetProperty("Restriction", AllInstance);
    private static readonly PropertyInfo RefreshCount = SlotType?.GetProperty("RefreshCount", AllInstance);
    private static readonly MethodInfo OtherNames = PresenterType.GetMethod("GetOtherEstablishNames", AllInstance);
    private static readonly MethodInfo OtherPortraits = PresenterType.GetMethod("GetOtherEstablishPortraits", AllInstance);
    private static readonly MethodInfo RefreshPanels = PresenterType.GetMethod("RefreshEstablishUnitPanels", AllInstance);

    internal static bool BindingsReady => SlotType != null && Slots != null && Generator != null
        && Seed != null && Candidate?.CanRead == true && Candidate.CanWrite
        && Token?.CanRead == true && Token.CanWrite && Restriction != null
        && RefreshCount != null && OtherNames != null && OtherPortraits != null
        && RefreshPanels != null;

    private static MethodBase TargetMethod()
        => AccessTools.Method(PresenterType, "TryGenerateEstablishSlot", new[] { typeof(int), typeof(bool) });

    private static void Postfix(CreateCampaignPresenter __instance, int __0, bool __result)
    {
        if (!__result || __instance == null) return;
        object slot = null;
        RecruitCandidateData previousCandidate = null;
        EstablishUnitSelectionToken previousToken = default;
        var modified = false;
        try
        {
            var slots = Slots?.GetValue(__instance);
            var item = slots?.GetType().GetProperty("Item", AllInstance);
            slot = item?.GetValue(slots, new object[] { __0 });
            if (slot == null || !BindingsReady) return;

            previousCandidate = (RecruitCandidateData)Candidate.GetValue(slot);
            previousToken = (EstablishUnitSelectionToken)Token.GetValue(slot);
            if (previousCandidate == null || previousToken == null
                || !string.IsNullOrEmpty(previousToken.PresetKey)) return;
            var names = (Il2CppSystem.Collections.Generic.HashSet<string>)OtherNames.Invoke(__instance, new object[] { __0 });
            var portraits = (Il2CppSystem.Collections.Generic.HashSet<string>)OtherPortraits.Invoke(__instance, new object[] { __0 });
            var namesReadOnly = names?.TryCast<ReadOnlyStrings>();
            var portraitsReadOnly = portraits?.TryCast<ReadOnlyStrings>();
            if (namesReadOnly == null || portraitsReadOnly == null) return;

            var result = new EstablishUnitGenerationResult(previousCandidate, previousToken);
            NativeFoundersEstablish.TryReplace((EstablishUnitGenerator)Generator.GetValue(__instance),
                (string)Seed.GetValue(__instance), __0, (int)RefreshCount.GetValue(slot),
                namesReadOnly, portraitsReadOnly, (EstablishUnitRestriction)Restriction.GetValue(slot), ref result);
            if (string.IsNullOrEmpty(result.Token.PresetKey)) return;

            Token.SetValue(slot, result.Token);
            modified = true;
            Candidate.SetValue(slot, result.Candidate);
            RefreshPanels.Invoke(__instance, null);
        }
        catch (Exception ex)
        {
            if (modified && slot != null)
            {
                try
                {
                    Token?.SetValue(slot, previousToken);
                    Candidate?.SetValue(slot, previousCandidate);
                    RefreshPanels?.Invoke(__instance, null);
                }
                catch { /* The native slot remains the fallback on the next refresh. */ }
            }
            DelversHost.Error("NATIVE_FOUNDER_ESTABLISH_PANEL_FAILED: " + ex);
        }
    }
}
