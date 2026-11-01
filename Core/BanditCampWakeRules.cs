using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// When the camp's bandits wake. (bandit draft): in combat Apocalypse they attack at once, like every enemy
// there; outside it after a lead time like normal enemies have, but shorter. That a party member entering the camp's whole room
// box wakes the boss party, and the lead of half the game's surprise window (SystemVar.Combat.SurprisedDuration, 4 s), is an
// interpretation, not confirmed. The wake on entering is kept, but not in this camp; so it is a
// property of the camp file (WakesOnEnter), off for today's camp, which wakes by the game's own aggro and a hit on a comrade
// (BanditGroupWake). Pure, so the offline tests cover it; BanditCampWake applies it in the game.
internal static class BanditCampWakeRules
{
    internal const float LeadShare = 0.5f;
    internal const float DefaultSurprisedSeconds = 4f;

    // The camp files that wake their bandits when a party member enters the room; a later camp may switch it on here. A file not
    // listed does not wake.
    private static readonly Dictionary<string, bool> wakesOnEnter = new(StringComparer.Ordinal)
    {
        [BanditCampRoomFiles.NormalFile] = false,
        [BanditCampRoomFiles.MirroredFile] = false
    };

    internal static bool WakesOnEnter(string campFile) => campFile != null && wakesOnEnter.TryGetValue(campFile, out var wakes) && wakes;

    internal static string OffLine(string campFile) => "CORE_BANDITS_CAMP_WAKE off camp=" + (campFile ?? "-");

    // The wake, with the party member whose step set it off, its cell and how far the nearest camp bandit stood (Chebyshev, -1
    // without one).
    internal static string WakeLine(int woken, float lead, string unit, int x, int y, int distance)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_CAMP_WAKE woken={0} lead={1:0.0} by={2} at={3},{4} nearest={5}",
            woken, lead, unit ?? "-", x, y, distance);

    internal static int Chebyshev(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    internal static bool Inside((int MinX, int MinY, int MaxX, int MaxY) box, int x, int y)
        => x >= box.MinX && x <= box.MaxX && y >= box.MinY && y <= box.MaxY;

    // Seconds from the party's entering until the camp wakes.
    internal static float LeadSeconds(bool instantCombat, float surprisedSeconds)
        => instantCombat ? 0f : LeadShare * (surprisedSeconds > 0f ? surprisedSeconds : DefaultSurprisedSeconds);

    internal static bool Due(float enteredAt, float now, float lead) => enteredAt >= 0f && now - enteredAt >= lead;
}
