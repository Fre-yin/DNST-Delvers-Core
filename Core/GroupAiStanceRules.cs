namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the party healer's stance: her distance to the foes, the step in for a cast and back,
// the emergency, the player's order and the taunt.
internal static partial class GroupAiRules
{
    // ---- the party healer's distance and swing ----
    // She keeps about StanceDistance cells from the nearest foe (band ±1), steps in to at most StanceCastDistance for the
    // seed or Lifebloom when the comrade is out of reach and back right after the cast, and into danger only in an
    // emergency nobody can be healed from the safe zone. A player's order goes first, and StanceQuietSeconds after it.
    internal const int StanceDistanceDefault = 7;
    internal const int StanceCastDistanceDefault = 5;
    internal const string StanceDistanceKey = "groupai.healer.distance";
    internal const string StanceCastDistanceKey = "groupai.healer.castDistance";
    internal const float StanceQuietSeconds = 3f;
    internal const float StanceOrderSeconds = 1f;
    internal const int StanceBackSteps = 2;
    internal const int StanceInSteps = 8;
    internal const int StanceEmergencySteps = 12;

    // The author's emergency: a comrade down to head health only, or at no more than 30 % of it, or a charged bleeding of
    // about 70 % of his maximum health (Core's wound buffer).
    internal const double EmergencyHeadShare = 0.3;
    internal const double EmergencyWoundShare = 0.7;

    internal static bool HealerEmergency(double body, double head, double maxHead, double woundBuffer, double maxTotal)
        => body <= 0 || (maxHead > 0 && head <= EmergencyHeadShare * maxHead) || (maxTotal > 0 && woundBuffer >= EmergencyWoundShare * maxTotal);

    // A setting of 3 to 10 cells, else the default.
    internal static int StanceCells(double? value, int fallback)
        => value is { } v && !double.IsNaN(v) && v >= 3 && v <= 10 ? (int)Math.Round(v) : fallback;

    // The healer's stance: none (the player's turn), emergency, in (a heal out of reach), return (right after such a
    // cast), back (a foe nearer than the band), hold.
    // Held in formation, only the emergency walk to a comrade out of her reach moves the healer; every other stance waits.
    internal static bool StanceMovesHeld(bool held, string mode) => !held || mode == "emergency";

    internal static string HealerStance(int nearestFoe, int distance, bool castPending, bool justCast, bool emergencyUnreached, bool quiet,
        bool tauntApproach = false)
        => quiet ? "none"
            : emergencyUnreached ? "emergency"
            : tauntApproach ? "taunt"
            : castPending ? "in"
            : justCast ? "return"
            : nearestFoe < distance - 1 ? "back"
            : "hold";

    // A player's order is seen where the game takes it (TaskSystem.DirectOrder,
    // a task Core did not send) or as a new forced target without an enemy's taunt; she is quiet until StanceQuietSeconds
    // after the last one. The game's own tasks (fleeing, walking for a skill, idle) never count.
    internal static bool StancePlayerQuiet(float now, float lastPlayerOrder) => !GameClockRules.Elapsed(now, lastPlayerOrder, StanceQuietSeconds);

    // After the quiet time a clicked target stays her focus and the game's forced target is let go (so the game does not
    // walk her to it); a taunt keeps its forced target.
    internal static bool StanceReleaseClick(bool quiet, string forced, string clicked, bool taunted)
        => !quiet && !taunted && forced != null && forced == clicked;

    // (ForeignSeen stayed set from run 1, EndFight ran only when she decided without a foe and she fell
    // in every run): the fight also ends once she is down or StanceFightEndSeconds out of combat.
    internal const float StanceFightEndSeconds = 3f;

    internal static bool StanceFightOver(bool down, bool inCombat, float outOfCombatFor)
        => down || (!inCombat && outOfCombatFor >= StanceFightEndSeconds);

    // Under a taunt she attacks the taunter from where the stance keeps her; out of every reach she may close in until he
    // is in reach, never nearer than StanceTauntMinDistance to any foe.
    internal const int StanceTauntMinDistance = 3;

    internal static bool StanceTauntApproach(bool taunted, int distanceToTaunter, int maxReach)
        => taunted && maxReach > 0 && distanceToTaunter > maxReach;

    // The limits of the approach per stance: steps, foes passed within one cell, distance kept from every foe.
    internal static (int Steps, int PassFoes, int MinFoeDistance) StanceApproach(bool emergency, int castDistance)
        => emergency ? (StanceEmergencySteps, 1, 1) : (StanceInSteps, 0, castDistance);
}
