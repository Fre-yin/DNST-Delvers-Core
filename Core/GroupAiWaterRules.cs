using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The party's water mage by the player's role, the pure part; GroupAiPolicy.RoleFirst and
// GroupAiSummonRefill act on it. A water mage is a member with the bubbleling's summon or the embracing wave on its bar; the game lets
// each mage keep at most two bubblelings and one toad, and the toad and the wave each use up one of the mage's own bubblelings.
//   Support: shields keep damage off. The mage keeps two bubblelings that attack the mage's target; once a member is below
//     GuardShare (LowLine.Guard, with look ahead and buffer) they are to stand beside the most hurt (open: GroupAiSummonRefill). A
//     member below SacrificeShare
//     (LowLine.Sacrifice, with look ahead and buffer) gets the wave, which sacrifices a bubbleling for its strong shield; the
//     weakest first. A new bubbleling takes the place of the one that went.
//   Damage: the mage keeps the most summons a player may have, two bubblelings and the toad, always aggressive: a bubbleling when
//     none stands, then the toad that eats it, then bubblelings up to two. It sacrifices bubblelings only for extra damage, never
//     for a shield: Glacier Drop (SKILL_GlacierDrop, uses up the nearest bubbleling, range 5) on its focus once it is free and a
//     bubbleling is left over, one held back for the toad while none stands; Water Dive (SKILL_WaterDive, summons two and bursts
//     them all on the tile) only with the toad standing and at least DiveFoes foes on or next to the tile. Toad and bubblelings
//     attack the mage's target, else the nearest foe.
// A neutral mage's summons attack its target by the comfort option (SummonFollowTarget), otherwise as in the game.
// Out of a fight the mage summons up to its most again (the damage dealer a toad and two bubblelings, the support two bubblelings),
// while it stands, has the energy and the skill is ready, so it goes full into the next fight. The summons' walk to their places in
// the formation is open (GroupAiSummonRefill).
internal static class GroupAiWaterRules
{
    // The game's cap per mage: two bubblelings and one toad.
    internal const int Bubblings = 2;
    internal const float GuardShare = 0.7f;
    internal const float SacrificeShare = 0.5f;
    internal const string GlacierDrop = "SKILL_GlacierDrop";
    internal const string WaterDive = "SKILL_WaterDive";
    internal const int DiveFoes = 2;

    internal enum Step { None, Wave, Bubbling, Toad, Glacier, Dive }

    internal static bool IsWaterMage(IEnumerable<string> slots)
    {
        foreach (var slot in slots ?? Array.Empty<string>())
            if (slot == WeakBanditKitKeys.SummonBubbling || slot == WeakBanditKitKeys.EmbracingWave) return true;
        return false;
    }

    // Support: the wave on a member below SacrificeShare while a bubbleling stands to sacrifice; else a bubbleling while fewer than
    // Bubblings stand.
    internal static Step Support(bool waveFree, bool sacrificeTarget, int bubblings, bool summonFree)
    {
        if (waveFree && sacrificeTarget && bubblings > 0) return Step.Wave;
        return summonFree && bubblings < Bubblings ? Step.Bubbling : Step.None;
    }

    // Damage: the toad while none stands and a bubbleling is there for it; Water Dive with the toad standing and DiveFoes foes on or
    // next to the focus' tile; Glacier Drop on the focus in reach while a bubbleling is left over (one held for a missing toad);
    // bubblelings up to Bubblings. Never the wave.
    internal static Step Damage(int bubblings, int toads, bool toadFree, bool summonFree, bool glacierFree, bool glacierReach, bool diveFree,
        int diveFoes)
    {
        if (toadFree && WeakBanditMageRules.WantsToad(toads, bubblings)) return Step.Toad;
        if (diveFree && toads > 0 && diveFoes >= DiveFoes) return Step.Dive;
        if (glacierFree && glacierReach && bubblings > (toads == 0 ? 1 : 0)) return Step.Glacier;
        return summonFree && bubblings < Bubblings ? Step.Bubbling : Step.None;
    }

    // Out of a fight: the toad for a damage dealer while none stands and a bubbleling is there, else bubblelings up to Bubblings.
    internal static Step Refill(bool damage, int bubblings, int toads, bool toadReady, bool summonReady)
    {
        if (damage && toadReady && WeakBanditMageRules.WantsToad(toads, bubblings)) return Step.Toad;
        return summonReady && bubblings < Bubblings ? Step.Bubbling : Step.None;
    }

    // In a fight a support mage's bubblelings are to guard the most hurt member once one is below GuardShare (the share as GroupAiLow
    // gives it for LowLine.Guard); kept for the open guard walk.
    internal static bool Guards(float lowestShare) => lowestShare < GuardShare;

    // CORE_AI_ROLE_WATER: a water mage's step (wave, bubbling, toad, glacier, dive) or a guarding bubbleling's walk (guard).
    internal static string Line(string unit, string role, string action, string target, float share, int bubblings, int toads, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_ROLE_WATER unit={0} role={1} action={2} target={3} share={4:0.00} bubblings={5} toads={6} tick={7}",
            unit, role ?? "neutral", action, target ?? "-", share, bubblings, toads, tick);
}
