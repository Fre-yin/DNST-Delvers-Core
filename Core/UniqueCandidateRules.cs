namespace DungeonSettlersDelvers.Core;

internal readonly struct UniqueCandidateIdentity
{
    internal UniqueCandidateIdentity(string profileKey, string nameTextKey, string backgroundTrait)
    {
        ProfileKey = profileKey;
        NameTextKey = nameTextKey;
        BackgroundTrait = backgroundTrait;
    }

    internal string ProfileKey { get; }
    internal string NameTextKey { get; }
    internal string BackgroundTrait { get; }
}

// Stable identity for the three native Founder presets. Names can be changed,
// so the matching rule uses the exact profile and unique background trait.
// Keeping the rules independent of Unity and IL2CPP lets offline tests exercise
// the production match and guild-price cap.
internal static class UniqueCandidateRules
{
    internal static readonly UniqueCandidateIdentity Lowell = new(
        "UNITVISUAL_HumanStandardMale_0", "TEXTKEY_PLAYERNAME_Human_Male_0", "AFFECTER_CityGuard");
    internal static readonly UniqueCandidateIdentity Liana = new(
        "UNITVISUAL_HumanStandardFemale_0", "TEXTKEY_PLAYERNAME_Human_Female_0", "AFFECTER_Poacher");
    internal static readonly UniqueCandidateIdentity Kragas = new(
        "UNITVISUAL_LizardMan_0", "TEXTKEY_PLAYERNAME_LizardMan_Male_0", "AFFECTER_ExiledWarrior");

    internal const int FounderGuildPriceCap = 500;

    internal static bool IsBuiltInUniqueCandidate(UniqueCandidateIdentity candidate)
        => Matches(candidate, Lowell) || Matches(candidate, Liana) || Matches(candidate, Kragas);

    internal static int CapNativeFounderGuildPrice(UniqueCandidateIdentity candidate, int nativeCalculatedPrice)
        => IsBuiltInUniqueCandidate(candidate)
            ? Math.Min(nativeCalculatedPrice, FounderGuildPriceCap)
            : nativeCalculatedPrice;

    private static bool Matches(UniqueCandidateIdentity candidate, UniqueCandidateIdentity expected)
        => string.Equals(candidate.ProfileKey, expected.ProfileKey, StringComparison.Ordinal)
            && string.Equals(candidate.BackgroundTrait, expected.BackgroundTrait, StringComparison.Ordinal);
}
