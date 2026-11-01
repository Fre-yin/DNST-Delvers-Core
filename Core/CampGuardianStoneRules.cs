using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The bandit camp's guardian stones (BUILDING_LesserGuardianStone). Neutral (no vision), the aura as in the
// game. Core spawns every camp building neutral (BanditCampFurnishRules), and so the stone's own passive gives no aura; Core
// gives it in its place, with the game's values: AFFECTER_LesserGuardianStone (FloatValues 7|10) grants
// AFFECTER_LesserGuardianAura for 10 s to player units closer than 7 (Euclidean, Passive_LesserGuardianStone.AddAffecter), every
// 10 s. Only the camp's stones: neutral ones inside the camp's box on its floor; the stones of the party's own campsite stay the
// game's. The game in CampGuardianStones. No game types here.
internal static class CampGuardianStoneRules
{
    internal const string StoneKey = "BUILDING_LesserGuardianStone";
    internal const string AuraKey = "AFFECTER_LesserGuardianAura";
    internal const float Radius = 7f;
    internal const float AuraSeconds = 10f;
    internal const float PulseSeconds = 10f;
    internal const int LogCap = 40;

    // Closer than the radius, as the game measures it.
    internal static bool InReach(int unitX, int unitY, int stoneX, int stoneY)
    {
        var dx = unitX - stoneX;
        var dy = unitY - stoneY;
        return dx * dx + dy * dy < Radius * Radius;
    }

    // Whether a pulse is due: the first one at once, then every PulseSeconds of game time; a clock that went back (a load) starts
    // anew.
    internal static bool Due(float lastPulse, float now) => lastPulse < 0f || now < lastPulse || now - lastPulse >= PulseSeconds;

    internal static string FoundLine(int stones, int seed)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CAMP_GUARDIAN_STONES found={0} seed={1}", stones, seed);

    internal static string AuraLine(string unit, string stone, int x, int y)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CAMP_GUARDIAN_AURA unit={0} stone={1} at={2},{3} seconds={4:0}",
            unit ?? "-", stone ?? "-", x, y, AuraSeconds);
}
