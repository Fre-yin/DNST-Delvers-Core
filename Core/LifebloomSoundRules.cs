namespace DungeonSettlersDelvers.Core;

// Lifebloom's sounds: one with every cast (the 1 s cut is mild enough for each one, "bei jedem Cast")
// and one when it blooms; no sound per heal tick and none when a charge comes back (the game's skills have none when ready). Cuts from free sounds, each with its source in THIRD_PARTY_NOTICES.md; CoreSound plays them in the game's sound
// effect group, so the game's volume settings apply.
internal static class LifebloomSoundRules
{
    // Cast: 1 s of "Healing (Total Rejuvenation) 3" around its loud bloom, -9 dB. Bloom: its first 4 s, -3 dB, the last second
    // faded out.
    internal const string CastFile = "Lifebloom_Wirken.wav";
    internal const string BloomFile = "Lifebloom_Aufbluehen.wav";
    internal static readonly string[] Files = { CastFile, BloomFile };

    // On top of the cut's own level. The bloom is heard at every healed unit, at the selected one fully and at all others
    // quieter.
    internal const float CastVolume = 0.9f, BloomVolume = 1f, BloomOthersVolume = 0.45f;

    internal static float BloomVolumeFor(bool targetSelected) => targetSelected ? BloomVolume : BloomOthersVolume;

    // The bloom sound with every bloom that heals (its end, the refresh at full stacks, a removal); a death or a lost target
    // blooms nothing (burst null or 0).
    internal static bool BloomPlays(string burst, double amount) => burst != null && amount > 0;
}
