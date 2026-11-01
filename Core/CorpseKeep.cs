using System.Globalization;
#if BEPINEX
using Interaction = global::Refactor.Control.Interaction;
using InteractionType = global::Refactor.Control.InteractionType;
#else
using Interaction = Il2CppRefactor.Control.Interaction;
using InteractionType = Il2CppRefactor.Control.InteractionType;
#endif

namespace DungeonSettlersDelvers.Core;

// Equipment stays on the body (step 1):
// - When a party member dies the game drops its whole equipment: EntityEventHandler.DropUnitItem calls
//   ItemControlProcessor.DropAllEquipItem (one caller, own method). While the game's death of that unit runs
//   (OnEvent(DeadUnitRequested)) Core skips this drop.
//   Inventory and the carried item still fall; the equipment stays in the corpse's slots, which the game
//   saves and loads for the dead as they are. Retirement calls DropUnitItem without a death, untouched.
// - A dead bandit drops nothing: no kit (CoreUnitDropPatch) and no goblin loot (CoreUnits.Copy); Core
//   notes the corpse for undressing (step 2).
// - Probe for steps 2 and 3, reading only and capped: the game's action list at a corpse, or at a
//   building while the user carries someone (CORE_CORPSE_PROBE_OPTION), and the ways off a map
//   (CORE_CORPSE_PROBE_LEAVE). Both lines belong to the group Corpse (CoreProbes) and are off in a release.
// Step 1 runs in every campaign, without a gate. Step 2 (undressing): Core's own option at every dressed corpse
// (AddUndressOption, CorpsePatches); a bandit's kit drops in its own condition (KitDrops).
// Burying a dressed corpse or leaving it at the base portal (both ClearDeadUnit) ends in EntityLifecycleHelper.DespawnUnit.
// The equipment is ruined then, after the game's yes/no dialog (CorpseBurialWarning, setting
// corpse.burial.warning); the game's despawn clears the item slots (ClearItemSlots). It broke because Core's
// own reader had given the equipment an empty search cache (EquippedKeys, fixed); the game run A6 shows whether that was all.
internal static class CorpseKeep
{
    private const int ProbeLogCap = 200;
    private const int LeaveLogCap = 50;

    private static IntPtr dying = IntPtr.Zero;
    private static readonly Dictionary<string, (UnitEntity Unit, MapType Map)> deadBandits = new(StringComparer.Ordinal);
    private static readonly HashSet<string> probed = new(StringComparer.Ordinal);
    private static int probeLines;
    private static int leaveLines;

    internal static void ResetCampaign()
    {
        dying = IntPtr.Zero;
        deadBandits.Clear();
        CoreUnits.ClearBodies();
        probed.Clear();
        probeLines = leaveLines = 0;
        CorpseBurialWarning.ResetCampaign();
    }

    // ---- the game's death of a unit (CorpseDeathPatch) ----

    internal static void DeathStarts(UnitEntity unit) => dying = unit != null ? unit.Pointer : IntPtr.Zero;

    internal static void DeathEnds() => dying = IntPtr.Zero;

    // Prefix of DropAllEquipItem: true keeps the dying party member's equipment on the body.
    internal static bool Keeps(IEntity entity)
    {
        var unit = entity?.TryCast<UnitEntity>();
        if (!CorpseRules.KeepsEquipment(dying != IntPtr.Zero, unit != null && unit.Pointer == dying, unit != null && unit.Faction == FactionType.Player))
            return false;
        var keys = EquippedKeys(entity);
        DelversHost.Info(CorpseRules.KeepLine(unit.Guid.ToString(), keys.Count, CorpseRules.Keys(keys)));
        return true;
    }

    // Postfix of DropUnitItem: a dying bandit dropped nothing; Core notes the corpse (step 2 undresses it).
    internal static void NoteBanditCorpse(MapType map, UnitEntity unit)
    {
        if (unit == null || unit.Pointer != dying || !CoreUnitRules.TryGet(unit.Key, out var definition)) return;
        var guid = unit.Guid.ToString();
        deadBandits[guid] = (unit, map);
        var partGone = BossItems.PartGone(definition);
        if (partGone) CoreUnits.DrawCorpse(guid, definition, false, true);
        DelversHost.Info(CorpseRules.BanditLine(guid, unit.Key, map.ToString(), CoreUnitRules.DropItems(definition, partGone).Count));
        WeakBandits.OnDied(map, unit, definition);
    }

    // The game removes a unit from its map (map change, cleanup): a noted bandit corpse is gone, with its body and
    // its place in Core's list of undressed corpses.
    internal static void Forget(UnitEntity unit)
    {
        if (unit == null) return;
        var guid = unit.Guid.ToString();
        deadBandits.Remove(guid);
        CoreUnits.ForgetBody(guid);
        if (CoreUnitRules.IsCoreUnit(unit.Key)) CoreSaveStore.Current?.SetUndressed(CoreGame.GuidOf(unit), false);
    }

    internal static bool IsUndressed(Guid unit) => CoreSaveStore.Current?.IsUndressed(unit) == true;


    // A bandit corpse of a loaded save: the game spawns its body on the shown map (CoreUnits.BeginSpawn).
    internal static void NoteLoadedCorpse(UnitEntity unit)
    {
        if (unit == null || !unit.IsDead || !CoreUnitRules.IsCoreUnit(unit.Key)) return;
        var guid = unit.Guid.ToString();
        if (!deadBandits.ContainsKey(guid)) deadBandits[guid] = (unit, CoreGame.CurrentMap);
    }

    // ---- step 3: the warning when leaving a dungeon map ----

    // Party members lying dead in their gear and bandit corpses not undressed on the map; with any, the game's own
    // help notice (CorpseText.LeaveWarning). Leaving goes on either way.
    internal static void LeaveWarning(string hook, MapType map)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        var members = new List<(bool Dead, int Items)>();
        var units = inMap?.GetAllPlayerUnits(true);
        for (var i = 0; units != null && i < units.Count; i++)
        {
            var unit = units[i];
            var dead = unit != null && unit.IsDead;
            members.Add((dead, dead ? EquippedKeys(unit.Cast<IEntity>()).Count : 0));
        }
        var corpses = new List<(bool OnMap, bool Undressed)>();
        foreach (var noted in deadBandits.Values)
        {
            var onMap = noted.Unit != null && noted.Map == map;
            corpses.Add((onMap, onMap && IsUndressed(CoreGame.GuidOf(noted.Unit))));
        }
        var party = CorpseRules.DressedFallenParty(members);
        var bandits = CorpseRules.DressedBanditCorpses(corpses);
        if (!CorpseRules.LeaveWarns(party + bandits, map != MapType.Base)) return;
        CoreGame.Apply(map, new HelpNoticeApplyData(CorpseText.LeaveWarningKey, null).Cast<IApplyData>());
        DelversHost.Info(CorpseRules.LeaveWarningLine(hook, map.ToString(), party, bandits, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }

    // ---- step 2: undress (at once by click; a carried corpse is put down first) ----

    // A party member's equipment falls through the game's own drop (DropAllEquipItem, skipped only while a death
    // runs) on and around the corpse's cell; a bandit's kit falls as items (CoreUnits.DropKit), its corpse is drawn
    // naked and noted in Core's block. Null when there is nothing to undress (CorpseRules.CanUndress), else the
    // CORE_CORPSE_UNDRESS line.
    internal static string Undress(UnitEntity corpse, UnitEntity by)
    {
        var keys = Undressable(corpse, out var player, out var definition);
        if (keys == null) return null;
        var guid = corpse.Guid.ToString();
        var entity = corpse.Cast<IEntity>();
        if (!TryMapOf(corpse, guid, out var map)) return null;
        if (player)
        {
            var processor = CoreGame.Flow?._eventProcessor?.ItemControlProcessor;
            if (processor == null) return null;
            processor.DropAllEquipItem(map, entity);
        }
        else
        {
            CoreUnits.DropKit(map, corpse);
            if (CoreSaveStore.Current == null) DelversHost.Warning("CORE_CORPSE_UNDRESS_UNSAVED unit=" + guid);
            CoreSaveStore.Current?.SetUndressed(CoreGame.GuidOf(corpse), true);
            CoreUnits.DrawNaked(guid, definition);
        }
        var line = CorpseRules.UndressLine(guid, by != null ? by.Guid.ToString() : "-", player ? "player" : "bandit", keys.Count, CorpseRules.Keys(keys));
        DelversHost.Info(line);
        return line;
    }

    // What undressing the corpse would drop, or null when it may not be undressed now (CorpseRules.CanUndress).
    private static List<string> Undressable(UnitEntity corpse, out bool player, out CoreUnitRules.Definition definition)
    {
        player = false;
        definition = null;
        if (corpse == null) return null;
        var entity = corpse.Cast<IEntity>();
        player = corpse.Faction == FactionType.Player;
        var bandit = CoreUnitRules.TryGet(corpse.Key, out definition);
        var keys = player ? EquippedKeys(entity) : bandit ? CoreUnitRules.DropItems(definition, BossItems.PartGone(definition)) : new List<string>();
        var undressed = bandit && IsUndressed(CoreGame.GuidOf(corpse));
        var carried = CoreGame.Component<CarryComponent>(entity)?.IsCarried() == true;
        // A humanoid body: a party member, one of Core's bandits (dressed by Core, not through the game's unit profile; the
        //  found their corpses without one) or any unit with the game's humanoid look.
        var humanoid = player || bandit || CoreGame.Component<UnitProfileComponent>(entity) != null;
        return CorpseRules.CanUndress(corpse.IsDead, carried, player, keys.Count, bandit, undressed, humanoid) ? keys : null;
    }

    // Core's option in the game's list at a corpse (every dressed corpse), last, so the game's own first
    // option stays the quick action. Controls as in the game, the menu by Alt and right click; a plain right
    // click runs the game's first option (Carry), as everywhere.
    internal static void AddUndressOption(UnitEntity user, UnitEntity corpse, Il2CppSystem.Collections.Generic.List<Interaction> list)
    {
        if (user == null || list == null || Undressable(corpse, out _, out _) == null) return;
        DelversNativeList.AddValue(list, new Interaction
        {
            InteractionType = (InteractionType)CorpseRules.UndressInteraction,
            UserEntity = user.Cast<IEntity>(),
            TargetEntity = corpse.Cast<IEntity>(),
            IsPossible = true,
            TargetText = MenuText.Pick(CorpseText.Undress)
        });
    }

    internal static bool IsUndressOption(Interaction option) => (int)option.InteractionType == CorpseRules.UndressInteraction;

    private static bool TryMapOf(UnitEntity corpse, string guid, out MapType map)
    {
        if (deadBandits.TryGetValue(guid, out var noted))
        {
            map = noted.Map;
            return true;
        }
        if (CoreGame.TryMapOfPartyMember(corpse, out map)) return true;
        map = CoreGame.CurrentMap;
        return CoreGame.Flow != null;
    }

    // The item keys in the unit's equipment slots, read straight from the component's slots (GetAllItems). Never through a new
    // EquipmentReader: its constructor gives the equipment a second, empty search cache that stays registered for good, and the
    // game's next removal from that equipment then throws KeyNotFoundException in ItemSearchCache.Remove (the burial lost one
    // item and left the corpse on the ground).
    internal static List<string> EquippedKeys(IEntity entity)
    {
        var keys = new List<string>();
        var slots = CoreGame.Component<EquipmentComponent>(entity)?.GetAllItems();
        for (var i = 0; slots != null && i < slots.Length; i++)
        {
            var holder = slots[i];
            if (holder != null && !string.IsNullOrEmpty(holder.Key) && !holder.IsEmpty()) keys.Add(holder.Key);
        }
        return keys;
    }

    // CorpseStatusForTest: a party member of the shown map (living or dead), a living unit there, or a
    // bandit corpse Core noted this session.
    internal static string Status(Guid guid)
    {
        var text = guid.ToString("D");
        var unit = CoreGame.FindUnit(guid);
        if (unit == null && deadBandits.TryGetValue(text, out var noted)) unit = noted.Unit;
        if (unit == null) return CorpseRules.StatusLine(text, false, false, false, 0, "-", false, IsUndressed(guid), false);
        var entity = unit.Cast<IEntity>();
        var keys = EquippedKeys(entity);
        var carried = CoreGame.Component<CarryComponent>(entity)?.IsCarried() == true;
        return CorpseRules.StatusLine(text, true, unit.IsDead, unit.Faction == FactionType.Player, keys.Count, CorpseRules.Keys(keys),
            carried, IsUndressed(guid), deadBandits.ContainsKey(text));
    }

    // ---- probe for steps 2 and 3 (reads only) ----

    // The game's actions for a user at a dead unit, or at a building while the user carries someone;
    // each different list once. Called by the two probe classes of the group Corpse only.
    internal static void ProbeOptions(string where, UnitEntity user, IEntity target, bool targetDead, Il2CppSystem.Collections.Generic.List<Interaction> list)
    {
        if (!CoreProbes.Corpse) return;
        if (probeLines >= ProbeLogCap || user == null || list == null) return;
        var carries = Carries(user);
        if (!targetDead && !carries) return;
        var userGuid = user.Guid.ToString();
        var targetGuid = target != null ? target.Guid.ToString() : "-";
        var signature = new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var option = list[i];
            signature.Add(option == null ? "null" : ((int)option.TaskType).ToString(CultureInfo.InvariantCulture) + ":" + option.IsPossible);
        }
        if (!probed.Add(where + "|" + userGuid + "|" + targetGuid + "|" + string.Join(",", signature))) return;
        if (list.Count == 0)
        {
            probeLines++;
            // The input path that asked for the list, if any (GroupAiOrderSource).
            DelversHost.Info(CorpseRules.ProbeOptionLine(where, userGuid, targetGuid, targetDead, carries, -1, "-", 0, 0, false, "", "") + " via=" + (GroupAiOrderSource.Current ?? "-"));
            return;
        }
        for (var i = 0; i < list.Count && probeLines < ProbeLogCap; i++)
        {
            var option = list[i];
            if (option == null) continue;
            probeLines++;
            DelversHost.Info(CorpseRules.ProbeOptionLine(where, userGuid, targetGuid, targetDead, carries, i, option.InteractionType.ToString(),
                (int)option.InteractionInputType, (int)option.TaskType, option.IsPossible, option.TargetText, option.InvalidMessage) + " via=" + (GroupAiOrderSource.Current ?? "-"));
        }
    }

    // A way off the map: the party members lying dead there and how many still wear equipment. The probe line only; it asks the
    // group Corpse first, and the warning (LeaveWarning, called next to it by the portal patches) does not depend on it.
    internal static void ProbeLeave(string hook, MapType map)
    {
        if (!CoreProbes.Corpse) return;
        if (leaveLines >= LeaveLogCap) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        var dead = 0;
        var dressed = 0;
        var units = inMap?.GetAllPlayerUnits(true);
        for (var i = 0; units != null && i < units.Count; i++)
        {
            var unit = units[i];
            if (unit == null || !unit.IsDead) continue;
            dead++;
            if (EquippedKeys(unit.Cast<IEntity>()).Count > 0) dressed++;
        }
        leaveLines++;
        DelversHost.Info(CorpseRules.ProbeLeaveLine(hook, map.ToString(), dead, dressed, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }

    private static bool Carries(UnitEntity user)
    {
        var carry = CoreGame.Component<CarryComponent>(user.Cast<IEntity>());
        return carry != null && Guid.TryParse(carry.GetCarriedGuid().ToString(), out var carried) && carried != Guid.Empty;
    }
}
