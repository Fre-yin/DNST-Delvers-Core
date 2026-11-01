using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// A summon takes its owner's side (as a party water mage's does). The game gives a summon the faction from
// AdditionalSpawnInfo, else from its table row, and SummonBubbling passes none, so a bandit's bubbleling would be Ally/Player, on the
// players' side. Its sub faction is always read from the unit's table row (UnitEntity.Data). For a Core owner the summon's faction
// and the sub faction of a copy of its row become the owner's; the key stays, so look, summon limit and pattern stay the game's.
// The game in SummonSide. No game types here.
internal static class SummonSideRules
{
    internal const string UnitPrefix = "UNIT_";
    internal const int LogCap = 40;

    // Whether the rule acts for this summon: a unit and an owner of Core's. Player summons stay the game's.
    internal static bool Applies(bool ownerIsCore, string key)
        => ownerIsCore && !string.IsNullOrEmpty(key) && key.StartsWith(UnitPrefix, StringComparison.Ordinal);

    // Effect lines a summon writes while the probe runs, and how many summons get lines at all (run 20261031-202955: one cap of 120
    // for the whole process was used up before a later summon's first line).
    internal const int EffectCap = 12;
    internal const int EffectSummons = 256;

    // One effect of a summon while the probe runs: whose summon (core, party, none when its owner is gone), the target, its side and
    // what it got.
    internal static string EffectLine(string summon, string ownerKind, string target, string side, string kind, float amount)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_EFFECT summon={0} owner-kind={1} target={2} side={3} kind={4} amount={5:0.#}",
            summon ?? "-", ownerKind ?? "-", target ?? "-", side ?? "-", kind ?? "-", amount);

    // The map's summon cap counts a unit of the summon's key only on the summon's side: Core's summons among themselves, the
    // party's among theirs (SummonLimit). The cap per unit filters by summoner anyway.
    internal static bool CountsTogether(bool summonIsCore, bool otherIsCore) => summonIsCore == otherIsCore;

    internal const int LimitLogCap = 40;

    internal static string LimitLine(string summon, string key, string leftOut, bool summonIsCore)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_LIMIT summon={0} key={1} left-out={2} side={3}",
            summon ?? "-", key ?? "-", leftOut ?? "-", summonIsCore ? "core" : "party");

    // A death the game's cap queued: the dying summon's key and side, the new summon's side, and whether Core's cap per side ran.
    internal static string LimitDeathLine(string key, string dies, bool summonIsCore, bool diesIsCore, bool perSide)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_LIMIT death key={0} dies={1} dies-side={2} summon-side={3} per-side={4}",
            key ?? "-", dies ?? "-", diesIsCore ? "core" : "party", summonIsCore ? "core" : "party", perSide ? "1" : "0");

    internal static string SaveLine(int left)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_SIDE stage=save dead-left-out={0}", left);

    internal static string DeathLine(string owner, int summons)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_SIDE stage=death owner={0} summons={1}", owner ?? "-", summons);

    internal static string Line(string stage, string summon, string key, string owner, int faction, int sub, bool changed)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SUMMON_SIDE stage={0} summon={1} key={2} owner={3} faction={4} sub={5} changed={6}",
            stage, summon ?? "-", key ?? "-", owner ?? "-", faction, sub, changed ? 1 : 0);

    // One summon in the probe's status: id:key:faction:sub:dead:cells to the owner:relation and IsHostile to a party unit:
    // relation to the owner:hits on the party:hits on Core's units:what CombatDataHandler's hostile lists say since the spawn,
    // "party,owner" as 1 foe / 0 not / -1 no such unit ("-" unread). They come from the pair registered at the spawn; one that
    // disagrees with relParty or relOwner is the fault of fix A (65db29b, reverted), KeyNotFound at the next spawn.
    internal static string Entry(string id, string key, int faction, int sub, bool dead, int cells, string relationToParty, bool hostileToParty,
        string relationToOwner, int hitsParty, int hitsCore, string registered)
        => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}",
            id, key, faction, sub, dead ? 1 : 0, cells, relationToParty ?? "-", hostileToParty ? 1 : 0, relationToOwner ?? "-", hitsParty, hitsCore,
            registered ?? "-");

    internal static string Pair(int faction, int sub) => faction.ToString(CultureInfo.InvariantCulture) + "," + sub.ToString(CultureInfo.InvariantCulture);
}
