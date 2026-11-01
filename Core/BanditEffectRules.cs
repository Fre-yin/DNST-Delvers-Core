using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The bandits' effect log for the Toolkit's block of bandit skills (BanditEffectProbe), pure: every damage, heal or affecter whose
// user is a bandit or a bandit's summon (CoreFaction), with the side of its target. Test campaigns only, off until a test turns it
// on, capped.
internal static class BanditEffectRules
{
    internal const int LogCap = 600;

    // The target's side: party (a player figure or a summon of one), bandit (CoreFaction), else other.
    internal static string SideOf(bool party, bool bandit) => party ? "party" : bandit ? "bandit" : "other";

    // Whether an effect landed on the wrong side: damage or a debuff on a bandit, a heal on the party. Affecters are judged by
    // the Toolkit from their key; here only damage and heals.
    internal static bool WrongSide(string kind, string side)
        => (kind == "damage" && side == "bandit") || (kind == "heal" && side == "party");

    // dead: the target was a corpse; the game changes nothing on one, so the amount is only its log's (no side drop either), and
    // wrong counts only for a living target.
    internal static string Line(string user, string userKey, string target, string targetKey, string side, string kind, float amount, bool dead = false)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDIT_EFFECT user={0} userKey={1} target={2} targetKey={3} side={4} kind={5} amount={6:0.#} wrong={7} dead={8}",
            user ?? "-", userKey ?? "-", target ?? "-", targetKey ?? "-", side ?? "-", kind ?? "-", amount, !dead && WrongSide(kind, side) ? 1 : 0, dead ? 1 : 0);
}
