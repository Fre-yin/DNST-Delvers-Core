using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
#if BEPINEX
using SpawnExecutionData = global::Refactor.Combat.SpawnExecutionData;
using SpawnSimulator = global::Refactor.Combat.ExecutionProcess.SpawnSimulator;
#else
using SpawnExecutionData = Il2CppRefactor.Combat.SpawnExecutionData;
using SpawnSimulator = Il2CppRefactor.Combat.ExecutionProcess.SpawnSimulator;
#endif

namespace DungeonSettlersDelvers.Core;

// SummonSideRules in the game (a bandit's bubbleling behaves as a party water mage's does): a summon of a
// Core owner takes the owner's side, keeps it through saving and loading, and dies with its owner. Player summons stay the game's.
// The side never changes after the map registered the unit: CombatDataHandler removes a dead unit under the pair it reads then,
// and a pair changed in between broke the next spawn (Core's fix A, 65db29b, reverted).
// - Faction: SpawnSimulator.CalculateApply(data, faction) gets the faction the game worked out (AdditionalSpawnInfo, else the table
//   row); its only caller passes it as a plain argument, so a prefix sets the owner's (data.User, the caster). CalculateFaction
//   itself has no caller, the game inlines it.
// - Sub faction: the game reads it from UnitEntity.Data, the table row. After EntityDataContainer.CreateUnit, the first step of
//   UnitSpawner.SpawnNonPlayableUnit (InitializeBasic and the registration come after it), the new summon's Data
//   points at a copy of the row with the owner's faction and sub faction, made once per key and side; the readonly backing field
//   is written in place with the GC's write barrier, as DelversSummons reads its summoner.
// - Loading: the save keeps the faction, the row comes from the key again. UnitDataContainer.Deserialize only builds the units
//   (UnitEntity..ctor, the dictionary; no registration); the map registers them later, EntityDataContainer.ReloadAllEntity →
//   EntityLoadHelper.LoadUnit → EntityDataContainer.OnSpawnUnit, the only load path into it. A postfix on Deserialize gives
//   the summons of a Core owner the row copy before that. The owner is in the same container and already carries its Core key
//   (CoreSaveStore turns the stand-in keys back before the game reads the save); its side is its row's.
// - Death: the game kills a summoner's summons (EntityEventHandler.KillSummonedUnits, "SummonerDead") on a death only for a unit
//   of the Player faction (OnEvent(DeadUnitRequested)). For a Core owner Core calls that same method after the
//   game's death handling.
// - Saving: the game leaves dead Ally summons out of the save (UnitDataContainer.Serialize); a Core owner's summon is
//   Enemy and would stay in it, dead, read again at every load. A postfix takes the dead summons of Core owners out of the save
//   data the same way (UnitSaveDataDictionary, UnitSummonerDictionary); the running game keeps them as the game keeps its own.
internal static class SummonSide
{
    private const string DataField = "<Data>k__BackingField";
    private static int dataOffset = -1;
    private static readonly Dictionary<string, UnitTableData> rows = new(StringComparer.Ordinal);
    private static int lines;

    internal static void Reset() => lines = 0;

    internal static void OnCalculate(SpawnExecutionData data, ref FactionType faction)
    {
        if (data == null) return;
        var user = data.User?.TryCast<UnitEntity>();
        var key = data.Key;
        if (!SummonSideRules.Applies(user != null && CoreUnitRules.IsCoreUnit(user.Key), key)) return;
        Log(SummonSideRules.Line("faction", null, key, user.Guid.ToString(), (int)user.Faction, -1, user.Faction != faction));
        faction = user.Faction;
    }

    internal static void OnCreated(UnitEntity unit)
    {
        if (unit == null || DelversSummons.OwnerOf(unit.Cast<IEntity>()).Length == 0) return;
        var owner = DelversSummons.OwnerUnitOf(unit.Cast<IEntity>());
        if (owner == null || !SummonSideRules.Applies(CoreUnitRules.IsCoreUnit(owner.Key), unit.Key)) return;
        Take(unit, owner, owner.Faction, owner.SubFaction, "sub");
        BanditTint.NoteSummon(unit.Guid.ToString());
    }

    // After a container was read from the save, before the map registers its units.
    internal static void OnLoaded(UnitDataContainer container)
    {
        var units = container?._units;
        if (units == null || units.Count == 0) return;
        var byId = new Dictionary<string, UnitEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in units.Values)
            if (unit != null) byId[unit.Guid.ToString()] = unit;
        foreach (var unit in byId.Values)
        {
            var owner = DelversSummons.OwnerOf(unit.Cast<IEntity>());
            if (owner.Length == 0 || !byId.TryGetValue(owner, out var summoner)) continue;
            if (!SummonSideRules.Applies(CoreUnitRules.IsCoreUnit(summoner.Key), unit.Key) || summoner.Data == null) continue;
            Take(unit, summoner, summoner.Data.Faction, summoner.Data.SubFaction, "load");
        }
    }

    // The save of a container: its dead summons of Core owners left out, as the game leaves out dead Ally summons. A summon is a
    // Core owner's when its owner in the container is a Core unit, or when its Data is one of the row copies (the owner may be gone).
    internal static void OnSaved(UnitDataContainer container, UnitContainerSaveData save)
    {
        var units = container?._units;
        var entries = save?.UnitSaveDataDictionary;
        if (units == null || entries == null || units.Count == 0) return;
        var byId = new Dictionary<string, UnitEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in units.Values)
            if (unit != null) byId[unit.Guid.ToString()] = unit;
        var left = 0;
        foreach (var unit in byId.Values)
        {
            if (!unit.IsDead) continue;
            var owner = DelversSummons.OwnerOf(unit.Cast<IEntity>());
            if (owner.Length == 0) continue;
            var core = byId.TryGetValue(owner, out var summoner) ? CoreUnitRules.IsCoreUnit(summoner.Key) : IsRowCopy(unit.Data);
            if (!SummonSideRules.Applies(core, unit.Key)) continue;
            if (entries.Remove(unit.Guid)) left++;
            save.UnitSummonerDictionary?.Remove(unit.Guid);
        }
        if (left > 0) Log(SummonSideRules.SaveLine(left));
    }

    private static bool IsRowCopy(UnitTableData data)
    {
        if (data == null) return false;
        foreach (var row in rows.Values)
            if (row.Pointer == data.Pointer) return true;
        return false;
    }

    // A Core owner died: its summons die the game's way, as a party summoner's do.
    internal static void OnDead(EntityEventHandler handler, DeadUnitRequested msg)
    {
        var unit = msg?.Target;
        if (handler == null || unit == null || unit.Faction == FactionType.Player || !CoreUnitRules.IsCoreUnit(unit.Key)) return;
        var summons = DelversSummons.SummonsOf(unit.Guid.ToString());
        if (summons.Count == 0) return;
        handler.KillSummonedUnits(msg.Map, unit);
        Log(SummonSideRules.DeathLine(unit.Guid.ToString(), summons.Count));
    }

    private static void Take(UnitEntity unit, UnitEntity owner, FactionType faction, SubFactionType sub, string stage)
    {
        var data = unit.Data;
        var changed = data != null && (data.Faction != faction || data.SubFaction != sub) && SetData(unit, RowFor(data, faction, sub));
        Log(SummonSideRules.Line(stage, unit.Guid.ToString(), unit.Key, owner.Guid.ToString(), (int)(unit.Data?.Faction ?? 0), (int)unit.SubFaction, changed));
    }

    private static void Log(string line)
    {
        if (lines++ < SummonSideRules.LogCap) DelversHost.Info(line);
    }

    // A copy of the table row with the owner's side, kept for the session so the unit's Data stays alive.
    private static UnitTableData RowFor(UnitTableData data, FactionType faction, SubFactionType sub)
    {
        var id = data.Key + "|" + (int)faction + "|" + (int)sub;
        if (rows.TryGetValue(id, out var row)) return row;
        var klass = Il2CppClassPointerStore<UnitTableData>.NativeClassPtr;
        var size = (int)IL2CPP.il2cpp_class_instance_size(klass);
        var copy = IL2CPP.il2cpp_object_new(klass);
        // The fields only, after the object header (class and monitor).
        var header = 2 * IntPtr.Size;
        var bytes = new byte[size - header];
        Marshal.Copy(data.Pointer + header, bytes, 0, bytes.Length);
        Marshal.Copy(bytes, 0, copy + header, bytes.Length);
        row = new UnitTableData(copy);
        row.Faction = faction; // trap-ok: a fresh copy of the table row, before the summon is registered
        row.SubFaction = sub; // trap-ok: a fresh copy of the table row, before the summon is registered
        rows[id] = row;
        return row;
    }

    private static bool SetData(UnitEntity unit, UnitTableData row)
    {
        if (dataOffset < 0)
        {
            var field = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<UnitEntity>.NativeClassPtr, DataField);
            dataOffset = field == IntPtr.Zero ? 0 : (int)IL2CPP.il2cpp_field_get_offset(field);
            DelversHost.Info("CORE_SUMMON_SIDE_FIELD offset=" + dataOffset);
        }
        if (dataOffset <= 0 || row == null || unit.Pointer == IntPtr.Zero) return false;
        IL2CPP.il2cpp_gc_wbarrier_set_field(unit.Pointer, unit.Pointer + dataOffset, row.Pointer);
        return unit.Data?.Pointer == row.Pointer;
    }
}

// The faction the game worked out for a spawn, before it builds the summon (SummonSide). Arguments bound by name.
[HarmonyPatch(typeof(SpawnSimulator), "CalculateApply")]
internal static class SummonSideFactionPatch
{
    private static void Prefix(SpawnExecutionData data, ref FactionType faction)
    {
        try { SummonSide.OnCalculate(data, ref faction); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-side-faction", ex); }
    }
}

// A new unit, before the map registers it (SummonSide).
[HarmonyPatch(typeof(EntityDataContainer), "CreateUnit")]
internal static class SummonSideCreatePatch
{
    private static void Postfix(UnitEntity __result)
    {
        try { SummonSide.OnCreated(__result); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-side-create", ex); }
    }
}

// The units read from a save, before the map registers them (SummonSide).
[HarmonyPatch(typeof(UnitDataContainer), nameof(UnitDataContainer.Deserialize))]
internal static class SummonSideLoadPatch
{
    private static void Postfix(UnitDataContainer __instance)
    {
        try { SummonSide.OnLoaded(__instance); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-side-load", ex); }
    }
}

// The save of the units: the dead summons of Core owners left out (SummonSide).
[HarmonyPatch(typeof(UnitDataContainer), nameof(UnitDataContainer.Serialize))]
internal static class SummonSideSavePatch
{
    private static void Postfix(UnitDataContainer __instance, UnitContainerSaveData __result)
    {
        try { SummonSide.OnSaved(__instance, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-side-save", ex); }
    }
}

// After the game handled a death: a Core owner's summons die with it (SummonSide).
[HarmonyPatch(typeof(EntityEventHandler), nameof(EntityEventHandler.OnEvent), new[] { typeof(DeadUnitRequested) })]
internal static class SummonSideDeathPatch
{
    private static void Postfix(EntityEventHandler __instance, DeadUnitRequested msg)
    {
        try { SummonSide.OnDead(__instance, msg); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-side-death", ex); }
    }
}
