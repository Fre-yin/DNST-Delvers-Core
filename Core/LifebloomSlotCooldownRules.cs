using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// What the Lifebloom slot's cooldown display shows for the caster's charges.
// - "ours": 1 to MaxCharges - 1 charges; Core draws the countdown to the next charge (the game's cooldown stays off);
// - "game": no charge left; the game's own cooldown runs exactly until the next charge (LifebloomRules.BlockSeconds) and
//   locks the skill, its seconds stay the game's. The game's circle is full at the last spend, though part of the charge's
//   time has passed by then, so it did not match the seconds; Core draws the circle as for "ours",
//   the share of the charge's time still left;
// - "none": all charges back; Core clears what it drew.
internal static class LifebloomSlotCooldownRules
{
    internal static (string Mode, float Fill, float Seconds) Show(int charges, double nextAt, double now, double cooldown)
    {
        if (charges >= LifebloomRules.MaxCharges) return ("none", 0f, 0f);
        var left = Math.Max(0, nextAt - now);
        var fill = cooldown > 0 ? (float)Math.Min(1, left / cooldown) : 0f;
        // Whole seconds, rounded up: "1" until the charge is back, as a countdown reads.
        return (charges <= 0 ? "game" : "ours", fill, (float)Math.Ceiling(left));
    }

    // The slot's cooldown image turns against the direction the game gives it, in every mode (ours and the game's
    // own countdown at no charge), so a charge timer never reads as a global cooldown.
    internal static bool SlotClockwise(bool gameClockwise) => !gameClockwise;

    // While a
    // charge is left ("ours") the skill can be cast, so the shade is OursShade of the game's; with no charge left the game's
    // shade stays, the skill is locked then.
    internal const float OursShade = 0.45f;

    internal static float SlotAlpha(string mode, float gameAlpha) => mode == "ours" ? gameAlpha * OursShade : gameAlpha;

    internal static string ShowLine(Guid caster, int charges, string mode, float seconds, int slots)
        => string.Format(CultureInfo.InvariantCulture, "CORE_LIFEBLOOM_CD_SHOW caster={0:D} charges={1} mode={2} left={3:0} slots={4}",
            caster, charges, mode, seconds, slots);

    internal static string ProbeLine(string skill, float cooldown, float duration, float cooldownFill, float durationFill, float durationBarFill)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_LIFEBLOOM_CD_PROBE skill={0} cooldown={1:0.00} duration={2:0.00} imgCooldown={3:0.00} imgSkillduration={4:0.00} imgSkillDurationBar={5:0.00}",
            skill ?? "-", cooldown, duration, cooldownFill, durationFill, durationBarFill);
}
