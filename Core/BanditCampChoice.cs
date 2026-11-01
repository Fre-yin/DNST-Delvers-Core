using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Whether the forest bandits' camp comes on a floor, in which form and in which order the anchor rooms are tried, pure so the
// offline tests cover it. The floor is the game's room graph as BanditCampRules reads it.
// - The camp belongs to the forest (region 0) on floor index 3, the game's phase 4. Elsewhere it never comes.
// - The Alpha's room is the forest's only elite room of that phase: a node of the elite type or one whose table row names the
//   Alpha. A floor with it gets no camp; the camp takes the floors without one.
// - Variant: the floor's seed, region and floor pick normal (0) or mirrored (1), the same for the same floor, so nothing is
//   stored. While the mirrored form is not approved the game side passes mirroredAllowed = false and the pick is always normal.
// - Anchors: the candidate rooms of BanditCampRules.Evaluate (never entry, portal, elite, quest room, mandatory path),
//   ranked as there: dead ends first, then the longest and narrowest access corridor, then a hash of seed and room id.
//   The builder tries them in this order and takes the first with room for the camp.
// A decision carries the reason for every excluded room and the reason for no camp, for the log.
internal static class BanditCampChoice
{
    // The camp's floor: forest, floor index 3 (the game counts from 0), phase 4.
    internal const int ForestRegion = 0;
    internal const int CampFloor = 3;
    internal const int CampPhase = 4;

    internal const int Normal = 0;
    internal const int Mirrored = 1;

    // Reasons for no camp.
    internal const string GateReason = "gate";
    internal const string AlphaReason = "alpha";
    internal const string NoAnchorReason = "no-anchor";
    internal const string GraphReasonPrefix = "graph-";

    // Mixed into the variant's hash so it does not follow the boss item pick (BossItemRules.ChosenIndex), which uses the same
    // seed, region and floor.
    private const uint VariantSalt = 0x7A3C5E91u;

    internal sealed class Decision
    {
        internal int Seed;
        internal int Region;
        internal int Floor;
        // The floor holds the Alpha's room.
        internal bool Alpha;
        // Normal or Mirrored.
        internal int Variant;
        // A test passed over the Alpha rule (BanditCampBuilder's force mode): the camp comes beside the Alpha's room.
        internal bool Forced;
        // Null when a camp comes, else why not (the reasons above).
        internal string Reason;
        // Node ids of the rooms to try, first choice first; empty without a camp.
        internal List<string> Anchors = new();
        // Every room that cannot hold the camp with the reason (BanditCampRules.ReasonOf, "graph" on an unusable graph).
        internal List<(string Id, string Reason)> Excluded = new();

        internal bool CampComes => Reason == null;
    }

    internal static bool InGate(int region, int floor) => region == ForestRegion && floor == CampFloor;

    // The Alpha's room is on the floor: an elite room, or any room whose table row names the Alpha. Corridors never count.
    internal static bool HasAlpha(IReadOnlyList<BanditCampRules.Node> nodes)
    {
        if (nodes == null) return false;
        foreach (var node in nodes)
        {
            if (node.Type == BanditCampRules.EliteType) return true;
            if (node.Type != BanditCampRules.CorridorType && BanditCampRules.HoldsAlpha(node)) return true;
        }
        return false;
    }

    // 0 (normal) or 1 (mirrored) from the floor, built like BossItemRules.ChosenIndex over its own salt.
    internal static int Variant(int seed, int region, int floor)
    {
        unchecked
        {
            var h = (uint)seed * 0x9E3779B1u ^ (uint)region * 0x85EBCA77u ^ (uint)floor * 0xC2B2AE3Du ^ VariantSalt;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (int)(h % 2u);
        }
    }

    // As Variant, but always normal while the mirrored form is not allowed.
    internal static int Variant(int seed, int region, int floor, bool mirroredAllowed)
        => mirroredAllowed ? Variant(seed, region, floor) : Normal;

    // The decision for one generated floor. nodes: rooms and corridors with their links, as BanditCampProbe builds them.
    // ignoreAlpha, only for tests: a floor with the Alpha still gets a camp; its room and its neighbours stay excluded as anchors.
    // allowMainPath, only for the seed scan's widest measure: anchors on the mandatory path too (BanditCampRules.Evaluate).
    internal static Decision Decide(IReadOnlyList<BanditCampRules.Node> nodes, ICollection<string> questRoomKeys, int longCorridor,
        int seed, int region, int floor, bool mirroredAllowed, bool ignoreAlpha = false, bool allowMainPath = false)
    {
        var decision = new Decision
        {
            Seed = seed,
            Region = region,
            Floor = floor,
            Alpha = HasAlpha(nodes),
            Variant = Variant(seed, region, floor, mirroredAllowed)
        };
        if (!InGate(region, floor)) return Refuse(decision, GateReason);
        if (decision.Alpha && !ignoreAlpha) return Refuse(decision, AlphaReason);
        decision.Forced = decision.Alpha;
        var result = BanditCampRules.Evaluate(nodes ?? new List<BanditCampRules.Node>(), questRoomKeys, longCorridor, seed, allowMainPath);
        var candidates = new List<BanditCampRules.Room>();
        foreach (var room in result.Rooms)
        {
            if (room.Reason == null) candidates.Add(room);
            else decision.Excluded.Add((room.Node.Id, room.Reason));
        }
        if (result.Problem != null) return Refuse(decision, GraphReasonPrefix + result.Problem);
        candidates.Sort((a, b) => Compare(a, b, seed));
        foreach (var room in candidates) decision.Anchors.Add(room.Node.Id);
        return decision.Anchors.Count == 0 ? Refuse(decision, NoAnchorReason) : decision;
    }

    private static Decision Refuse(Decision decision, string reason)
    {
        decision.Reason = reason;
        return decision;
    }

    // The order of BanditCampRules.Better as a comparison; equal only for the same id.
    private static int Compare(BanditCampRules.Room a, BanditCampRules.Room b, int seed)
    {
        if (BanditCampRules.Better(a, b, seed)) return -1;
        return BanditCampRules.Better(b, a, seed) ? 1 : 0;
    }

    // ---- log line ----

    internal static string VariantName(int variant) => variant == Mirrored ? "mirrored" : "normal";

    // CORE_BANDITS_CAMP_CHOICE: once per generated floor of the camp's region and floor. first: the first anchor to try, reason: why no camp.
    internal static string ChoiceLine(Decision d)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_CAMP_CHOICE seed={0} region={1} floor={2} alpha={3} variant={4} candidates={5} first={6} reason={7}",
            d.Seed, d.Region, d.Floor, d.Alpha ? "yes" : "no", VariantName(d.Variant), d.Anchors.Count,
            d.Anchors.Count == 0 ? "none" : BanditCampRules.LogId(d.Anchors[0]), d.Reason ?? "none") + (d.Forced ? " forced=yes" : "");
}
