using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Equipment stays on the body: the rules without
// the game, tested offline. The game side is CorpseKeep.
internal static class CorpseRules
{
    // ClearDeadUnit (InteractionType 9, task ClearUnit, BurialApplyData) at a grave and at the base portal despawns the carried
    // unit, and the game's despawn clears its item slots with it (EntityLifecycleHelper.ClearItemSlots; the game run A6 still has
    // to show it for a dressed corpse). Before such an order for a dressed party corpse Core asks with the game's yes/no dialog;
    // the order runs only on yes. Setting corpse.burial.warning, on by default.
    internal const string BurialWarningKey = "corpse.burial.warning";
    // InteractionType.ClearDeadUnit 9 and TaskType.ClearDeadBody 9 share the number by chance;
    // the action menu sends the task (DirectTaskRequested), InteractionCheckSet_ClearDeadUnit links both.
    internal const int ClearDeadUnitType = 9;
    internal const int ClearDeadBodyTask = 9;

    internal static bool BurialWarns(bool enabled, int interactionType, bool partyCorpse, int equipped)
        => enabled && interactionType == ClearDeadUnitType && partyCorpse && equipped > 0;

    // On yes: the held order runs only in the campaign it was asked in, with its target still on the map,
    // while the same user still carries the same corpse and it still wears something; otherwise it is stale.
    internal static bool BurialStillValid(bool sameCampaign, bool targetThere, bool sameCorpseCarried, bool corpseDead, int equipped)
        => sameCampaign && targetThere && sameCorpseCarried && corpseDead && equipped > 0;

    // way: execute (right click, ActionOrder.Execute) or task (action menu, DirectTaskRequested).
    internal static string BurialLine(string result, string corpse, int equipped, string way = null)
        => "CORE_CORPSE_BURIAL_WARNING result=" + result + " corpse=" + (string.IsNullOrEmpty(corpse) ? "-" : corpse) + " equipped=" + equipped
            + (way == null ? "" : " way=" + way);

    // One question at a time: while one is open a second burial order is dropped (result=busy), so it
    // cannot replace the first. An open question older than BurialQuestionSeconds counts as gone (the dialog may have ended
    // without an answer, e.g. a scene change), so burials are never blocked for good.
    internal const double BurialQuestionSeconds = 120;

    internal static bool BurialBusy(bool open, double openedAt, double now) => open && now - openedAt < BurialQuestionSeconds;


    // Core's own value for the action "undress" at a corpse (step 2, planned), far from the game's TaskType values
    // (-1 to 1001). Which field of the game's interaction carries it is settled when step 2 is built.
    internal const int UndressTask = 9101;

    // Bandits drop exactly what they wear, when undressed (step 2); no loot from the goblin template.
    internal static readonly string[] BanditDeathLoot = Array.Empty<string>();

    // The game drops a dying unit's whole equipment (DropAllEquipItem); a party member keeps it on the
    // body. Only while the game's death of that very unit runs: retirement calls the same drop without a
    // death and stays as it is.
    internal static bool KeepsEquipment(bool deathRuns, bool sameUnit, bool player) => deathRuns && sameUnit && player;

    // Item keys joined with "+", "-" for none.
    internal static string Keys(IEnumerable<string> keys)
    {
        var list = new List<string>();
        if (keys != null)
            foreach (var key in keys)
                if (!string.IsNullOrEmpty(key)) list.Add(key);
        return list.Count == 0 ? "-" : string.Join("+", list);
    }

    // Step 2: only a lying corpse (a carried one is put down first) with something on: a
    // party member with an equipment slot filled, a bandit not undressed yet. And only a humanoid's corpse, never a monster's
    // (player characters and human NPCs): a party member, one of Core's bandits, or a unit with the game's
    // humanoid look (UnitProfileComponent), the body that shows equipment (CorpseKeep.Undressable decides it).
    internal static bool CanUndress(bool dead, bool carried, bool player, int items, bool bandit, bool undressed, bool humanoid)
        => humanoid && dead && !carried && items > 0 && (player || (bandit && !undressed));

    // Undressing is an option at every corpse that still wears something (CanUndress). The game has no
    // such option (its list at a dead unit holds only CarryUnit, CORE_CORPSE_PROBE_OPTION in the),
    // so Core adds its own, told apart by an interaction type the game does not use (its values end at 39).
    internal const int UndressInteraction = 90;

    internal static string UndressLine(string unit, string by, string kind, int items, string keys)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CORPSE_UNDRESS unit={0} by={1} kind={2} items={3} keys={4}", unit, by, kind, items, keys);

    // Step 3: a notice when dressed fallen stay behind on a dungeon map; leaving still works.
    internal static bool LeaveWarns(int dressed, bool dungeonMap) => dressed > 0 && dungeonMap;

    // The count behind that notice, party side: members of the map lying dead with something still in their equipment slots.
    // A living member never counts, nor a dead one already without equipment (undressed, or never dressed).
    internal static int DressedFallenParty(IEnumerable<(bool Dead, int Items)> members)
    {
        var count = 0;
        if (members != null)
            foreach (var member in members)
                if (member.Dead && member.Items > 0) count++;
        return count;
    }

    // The same, bandit side: noted bandit corpses of this map that are not undressed yet. OnMap is false for a corpse of
    // another map or a unit that is gone.
    internal static int DressedBanditCorpses(IEnumerable<(bool OnMap, bool Undressed)> corpses)
    {
        var count = 0;
        if (corpses != null)
            foreach (var corpse in corpses)
                if (corpse.OnMap && !corpse.Undressed) count++;
        return count;
    }

    internal static string LeaveWarningLine(string hook, string map, int party, int bandits, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CORPSE_LEAVE_WARNING hook={0} map={1} dressed={2} party={3} bandits={4} tick={5}",
            hook, map, party + bandits, party, bandits, tick);

    internal static string KeepLine(string unit, int items, string keys)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CORPSE_KEEP unit={0} items={1} keys={2}", unit, items, keys);

    internal static string BanditLine(string unit, string key, string map, int kit)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CORPSE_BANDIT unit={0} key={1} map={2} kit={3}", unit, key, map, kit);

    // For the Toolkit (CorpseStatusForTest): the corpse's state in one line. listed: Core noted the dead
    // bandit this session; undressed comes with step 2.
    internal static string StatusLine(string unit, bool found, bool dead, bool player, int items, string keys, bool carried, bool undressed, bool listed)
        => found
            ? string.Format(CultureInfo.InvariantCulture, "unit={0} found=1 dead={1} player={2} items={3} keys={4} carried={5} undressed={6} listed={7}",
                unit, Flag(dead), Flag(player), items, keys, Flag(carried), Flag(undressed), Flag(listed))
            : "unit=" + unit + " found=0";

    // Probe of step 2: one action of the game's list at a corpse or building.
    internal static string ProbeOptionLine(string where, string user, string target, bool dead, bool userCarries, int index,
        string type, int input, int task, bool possible, string text, string invalid)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_CORPSE_PROBE_OPTION where={0} user={1} target={2} dead={3} carries={4} index={5} type={6} input={7} task={8} possible={9} text=\"{10}\" invalid=\"{11}\"",
            where, user, target, Flag(dead), Flag(userCarries), index, type, input, task, Flag(possible), Clean(text), Clean(invalid));

    // Probe of step 3: a way off the map, with the party members lying dead there and those still dressed.
    internal static string ProbeLeaveLine(string hook, string map, int dead, int dressed, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_CORPSE_PROBE_LEAVE hook={0} map={1} dead={2} dressed={3} tick={4}", hook, map, dead, dressed, tick);

    private static string Flag(bool value) => value ? "1" : "0";

    private static string Clean(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
}
