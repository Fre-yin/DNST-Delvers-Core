#if BEPINEX
using global::Refactor;
#else
using Il2CppRefactor;
#endif
#if BEPINEX
using global::Refactor.Util;
#else
using Il2CppRefactor.Util;
#endif
#if BEPINEX
using global::Util.Sheet;
#else
using Il2CppUtil.Sheet;
#endif

namespace DungeonSettlersDelvers.Core;

// These are the game's own story-character presets, not reconstructed copies.
internal static class NativeFounders
{
    internal sealed class Entry
    {
        internal readonly string Name;
        internal readonly string PresenceId;
        internal readonly string PresetKey;
        internal readonly string ProfileKey;
        internal readonly string NameTextKey;
        internal readonly string BackgroundTrait;
        internal readonly RaceType Race;
        internal readonly GenderType Gender;

        internal Entry(string name, string presenceId, string presetKey, string profileKey,
            string nameTextKey, string backgroundTrait, RaceType race, GenderType gender)
        {
            Name = name;
            PresenceId = presenceId;
            PresetKey = presetKey;
            ProfileKey = profileKey;
            NameTextKey = nameTextKey;
            BackgroundTrait = backgroundTrait;
            Race = race;
            Gender = gender;
        }
    }

    internal static readonly Entry Lowell = new("Lowell", "lowell", "CADIDATEPRESET_BuildWarrior",
        UniqueCandidateRules.Lowell.ProfileKey, UniqueCandidateRules.Lowell.NameTextKey,
        UniqueCandidateRules.Lowell.BackgroundTrait, RaceType.Human, GenderType.Male);
    internal static readonly Entry Liana = new("Liana", "liana", "CADIDATEPRESET_BuildArcher",
        UniqueCandidateRules.Liana.ProfileKey, UniqueCandidateRules.Liana.NameTextKey,
        UniqueCandidateRules.Liana.BackgroundTrait, RaceType.Human, GenderType.Female);
    internal static readonly Entry Kragas = new("Kragas", "kragas", "CADIDATEPRESET_BuildLizardman",
        UniqueCandidateRules.Kragas.ProfileKey, UniqueCandidateRules.Kragas.NameTextKey,
        UniqueCandidateRules.Kragas.BackgroundTrait, RaceType.LizardMan, GenderType.Male);

    internal static readonly Entry[] All = { Lowell, Liana, Kragas };

    internal static Entry ForCandidate(RecruitCandidateData candidate)
    {
        var profile = candidate == null ? null
            : DataSheetManager.Instance?.GetUnitProfileData(candidate.UnitProfileKey);
        if (profile == null) return null;
        var race = TextKeyExtensions.GetRaceType(profile.Key);
        foreach (var entry in All)
            if (entry.Race == race && entry.Gender == profile.GenderType) return entry;
        return null;
    }

    internal static bool IsUniqueCandidate(RecruitCandidateData candidate)
        => candidate != null && UniqueCandidateRules.IsBuiltInUniqueCandidate(
            new UniqueCandidateIdentity(candidate.UnitProfileKey, candidate.NameTextKey, candidate.BackgroundTrait));

    internal static Entry ForProfile(string profileKey)
    {
        foreach (var entry in All)
            if (entry.ProfileKey == profileKey) return entry;
        return null;
    }

    internal static bool IsNativePresetAvailable(Entry entry)
    {
        var manager = DataSheetManager.Instance;
        var preset = manager?.GetCandidatePresetData(entry.PresetKey);
        var profile = manager?.GetUnitProfileData(entry.ProfileKey);
        return preset != null && profile != null && preset.UnitProfileKey == entry.ProfileKey
            && preset.NameTextKey == entry.NameTextKey
            && preset.BackgroundTrait == entry.BackgroundTrait && preset.RaceType == entry.Race
            && profile.GenderType == entry.Gender
            && TextKeyExtensions.GetRaceType(profile.Key) == entry.Race;
    }
}
