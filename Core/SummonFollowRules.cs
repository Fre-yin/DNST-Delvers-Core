using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A water mage's bubblelings attack their mage's target, the same for the players and the bandits
// (SummonFollowTarget). A bandit's always; a party mage's while the comfort option is on (QolText.SummonFollowKey, on by default).
// Without a target of the owner's the bubbleling keeps the game's own choice. No game types here.
internal static class SummonFollowRules
{
    internal const int LogCap = 40;

    internal enum ForcedStep { None, Set, Clear, Order }

    // What to do with the summon's forced target (SummonFollowTarget): forced is the one it has now, mine the one Core gave it, to
    // the owner's target it should follow (null: none to follow). A forced target Core did not give is the player's order and
    // stays; Core's own is set when the owner's target changed and cleared when there is none to follow.
    internal static ForcedStep Forced(string forced, string mine, string to)
    {
        if (forced != null && !string.Equals(forced, mine, StringComparison.OrdinalIgnoreCase)) return ForcedStep.Order;
        if (to == null) return forced != null ? ForcedStep.Clear : ForcedStep.None;
        return string.Equals(forced, to, StringComparison.OrdinalIgnoreCase) ? ForcedStep.None : ForcedStep.Set;
    }

    // Whether the summon follows its owner's target: a Core owner always, a party owner while the option is on, and only with a
    // target that is there and hostile to the summon.
    internal static bool Follows(bool ownerIsCore, bool ownerIsParty, bool optionOn, bool targetUsable)
        => targetUsable && (ownerIsCore || (ownerIsParty && optionOn));

    // Logged when a summon takes a target of its owner's it did not follow before.
    internal static string Line(string summon, string owner, string target, bool core)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_FOLLOW summon={0} owner={1} target={2} owner-kind={3}",
            summon ?? "-", owner ?? "-", target ?? "-", core ? "core" : "party");
}
