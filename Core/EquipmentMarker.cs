using HarmonyLib;
#if BEPINEX
using ItemControlProcessor = global::Refactor.Main.Processor.ItemControlProcessor;
#else
using ItemControlProcessor = Il2CppRefactor.Main.Processor.ItemControlProcessor;
#endif

namespace DungeonSettlersDelvers.Core;

// Commit 1: the game's hooks of the equipment marker (rules and ledger: EquipmentMarkerRules). Only party members'
// equipment is watched (faction Player, no summoner, no Core unit); a holder that leaves a slot is a departure, an arrival of
// its key anywhere pays it, one that stays unpaid after a full tick is logged. Frames of the game's own ways mark a departure
// as sunk (destroyed, last arrow, a dead unit's despawn), in transit, despawned or a load repair. Only reads, never changes
// the game: every prefix returns true, every finalizer gives the exception back; no reader, no AddEvents.
internal static class EquipmentMarker
{
    // One holder as the rules see it; a missing holder (null in the interop) is an empty one.
    private readonly struct Held
    {
        internal readonly string Key;
        internal readonly Guid Guid;
        internal readonly int Amount;

        private Held(string key, Guid guid, int amount)
        {
            Key = key;
            Guid = guid;
            Amount = amount;
        }

        internal static Held Of(ItemHolder holder) => holder == null ? default : new Held(holder.Key, Sys(holder.Guid), holder.Amount);

        internal bool IsZero => EquipmentMarkerRules.IsZero(Key, Guid, Amount);
    }

    private readonly struct Note
    {
        internal readonly string Unit;
        internal readonly int Map;
        internal readonly int Slot;
        internal readonly Held Holder;

        internal Note(string unit, int map, int slot, Held holder)
        {
            Unit = unit;
            Map = map;
            Slot = slot;
            Holder = holder;
        }
    }

    private static readonly EquipmentMarkerRules.Ledger ledger = new();
    private static readonly EquipmentMarkerRules.Failures failures = new();
    private static readonly List<EquipmentMarkerRules.Frame> frames = new();
    private static readonly List<Note> notes = new();
    private static long postfixes;
    private static int retires;

    // ---- departures ----

    internal static void OnRemoveItem(InventoryComponent inventory, int index)
    {
        if (failures.Off) return;
        try
        {
            var unit = PartyMember(inventory);
            if (unit == null) return;
            var holder = Held.Of(HolderAt(inventory, index));
            if (holder.IsZero)
            {
                ledger.CountZero();
                return;
            }
            Route(unit, index, holder, "unexplained");
        }
        catch (Exception ex) { Fail("departure", ex); }
    }

    // A departure inside a frame of the game's own ways, else an open departure for the arrival check.
    private static void Route(UnitEntity unit, int slot, Held holder, string why)
    {
        var frame = EquipmentMarkerRules.FrameFor(frames, unit.Pointer, slot);
        switch (EquipmentMarkerRules.RouteOf(frame))
        {
            case EquipmentMarkerRules.Route.Sunk:
                Write(ledger.Sink(NewDeparture(unit, slot, holder, why)));
                break;
            case EquipmentMarkerRules.Route.Transit:
                Missing(unit, slot, holder, "transfer", "slot_cleared", true);
                break;
            case EquipmentMarkerRules.Route.Despawn:
                Missing(unit, slot, holder, "despawn", EquipmentMarkerRules.DespawnWhy(retires > 0), false);
                break;
            case EquipmentMarkerRules.Route.Load:
                notes.Add(new Note(unit.Guid.ToString(), MapOf(unit), slot, holder));
                break;
            default:
                Write(ledger.Depart(NewDeparture(unit, slot, holder, why)));
                break;
        }
    }

    // AddItem on any inventory: an overwritten party holder departs first (stage overwrite), then the new holder arrives
    // (stage arrival). Only an arrival that overwrote another party holder may leave credit, and only for the same frame.
    internal static void OnAddItem(InventoryComponent inventory, int index, Il2CppSystem.Guid guid, string key, int amount)
    {
        if (failures.Off) return;
        var old = default(Held);
        var newGuid = Guid.Empty;
        var overwrote = false;
        try
        {
            old = Held.Of(HolderAt(inventory, index));
            newGuid = Sys(guid);
            var unit = PartyMember(inventory);
            if (unit != null) overwrote = Overwrite(unit, index, old, newGuid, key, amount);
        }
        catch (Exception ex) { Fail("overwrite", ex); }
        if (failures.Off) return;
        try
        {
            var arrival = EquipmentMarkerRules.AddItemArrival(old.Key, old.Guid, old.Amount, key, newGuid, amount);
            Write(ledger.Arrive(key, arrival, overwrote ? UnityEngine.Time.frameCount : EquipmentMarkerRules.NoCredit));
        }
        catch (Exception ex) { Fail("arrival", ex); }
    }

    // True when another holder departed, so the arrival may leave credit.
    private static bool Overwrite(UnitEntity unit, int slot, Held old, Guid guid, string key, int amount)
    {
        switch (EquipmentMarkerRules.Overwrite(old.Key, old.Guid, old.Amount, key, guid, amount))
        {
            case EquipmentMarkerRules.OverwriteKind.Reduced:
                ledger.CountReduced();
                return false;
            case EquipmentMarkerRules.OverwriteKind.Departure:
                Route(unit, slot, old, "overwrite");
                return true;
            default:
                return false;
        }
    }

    internal static void OnAddTempItem(string key, int amount)
    {
        if (failures.Off) return;
        try { Write(ledger.Arrive(key, amount)); }
        catch (Exception ex) { Fail("arrival", ex); }
    }

    internal static void OnSpawnItem(ItemDataContainer container, ItemEntity item, int amount)
    {
        if (failures.Off || container == null || item == null) return;
        try
        {
            // GetItemInfo gives the empty value for an unknown guid; Has only reads and keeps the intent plain.
            var oldAmount = container.Has(item.Guid) ? container.GetItemInfo(item.Guid).Amount : 0;
            Write(ledger.Arrive(item.Key, EquipmentMarkerRules.SpawnArrival(oldAmount, amount)));
        }
        catch (Exception ex) { Fail("arrival", ex); }
    }

    // ---- frames ----

    // True when a frame was opened; the finalizer closes only that one, so an outer frame is never lost.
    internal static bool Open(string hook, UnitEntity unit, int slot)
    {
        if (failures.Off || unit == null) return false;
        try
        {
            frames.Add(EquipmentMarkerRules.FrameOf(hook, unit.Pointer, slot, unit.IsDead));
            return true;
        }
        catch (Exception ex)
        {
            Fail("frame", ex);
            return false;
        }
    }

    internal static void Close(bool opened)
    {
        if (opened && frames.Count > 0) frames.RemoveAt(frames.Count - 1);
    }

    internal static void OpenRetire() => retires++;

    internal static void CloseRetire() => retires = Math.Max(0, retires - 1);

    // Before the game reloads every entity: the load's own cap starts over.
    internal static void BeginLoad() => ledger.ResetLoadCap();

    // After ReloadAllEntities, also when it threw: each load repair note is reported once (commit 1: without a marker). Notes
    // are taken one by one: a note whose report fails counts as dropped and the rest go on; when the marker switches off,
    // only the notes not reported yet count as dropped.
    internal static void EndLoad()
    {
        var saveLoad = LoadIntegrityGuard.IsLoadInProgress;
        while (notes.Count > 0 && !failures.Off)
        {
            var note = notes[0];
            notes.RemoveAt(0);
            try { ReportNote(note, saveLoad); }
            catch (Exception ex)
            {
                ledger.CountDropped();
                Fail("frame", ex);
            }
        }
        notes.Clear();
    }

    private static void ReportNote(Note n, bool saveLoad)
    {
        if (n.Holder.IsZero)
        {
            ledger.CountZero();
            return;
        }
        var line = EquipmentMarkerRules.MissingLine(n.Unit, n.Map, n.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture),
            n.Holder.Key, n.Holder.Amount, 0, Tick(), "load", "load_repair", saveLoad);
        Write(ledger.ReportMissing(line, true, false, n.Unit, n.Slot, n.Holder.Key, n.Holder.Amount, "load_repair"));
    }

    // ---- settling ----

    // The tick postfix (GroupAiBanditTickPatch): due departures are settled, then this postfix counts as done. Whether the
    // campaign is a test campaign (departure lines) is read here once a tick, not at every write.
    internal static void OnGameTick()
    {
        if (failures.Off) return;
        try
        {
            ledger.DepartureLines = TestCampaign();
            Write(ledger.SettleDue(postfixes));
        }
        catch (Exception ex) { Fail("settle", ex); }
        finally { postfixes++; }
    }

    // Every campaign bound (ReviveCampaign.OnCampaignBound): open departures settled, the summary, then all starts over. The
    // old campaign settles with its own departure flag; then the flag is read for the campaign just bound (CoreSaveStore binds
    // it before this call), so a load writes departure lines only in a test campaign, also before the first tick.
    // The summary at the game's own save belongs to commit 3 (the marker in the save).
    internal static void OnCampaignBound()
    {
        if (!failures.Off)
        {
            try
            {
                Write(ledger.SettleAll());
                DelversHost.Info(EquipmentMarkerRules.SummaryLine(ledger.Counters));
            }
            catch (Exception ex) { Fail("settle", ex); }
        }
        ledger.Reset();
        notes.Clear();
        frames.Clear();
        retires = 0;
        try { ledger.DepartureLines = TestCampaign(); }
        catch (Exception ex) { Fail("settle", ex); }
    }

    // Test hook (DelversCoreRuntime.EquipmentMarkerSummaryForTest): the summary since the last bind, null once off.
    internal static string SummaryForTest() => failures.Off ? null : EquipmentMarkerRules.SummaryLine(ledger.Counters);

    // ---- helpers ----

    private static EquipmentMarkerRules.Departure NewDeparture(UnitEntity unit, int slot, Held holder, string why)
        => new()
        {
            Unit = unit.Guid.ToString(),
            Map = MapOf(unit),
            Slot = slot,
            Key = holder.Key,
            ItemGuid = holder.Guid,
            Amount = holder.Amount,
            Tick = Tick(),
            AtPostfix = postfixes,
            Frame = UnityEngine.Time.frameCount,
            SaveLoad = LoadIntegrityGuard.IsLoadInProgress,
            Why = why
        };

    private static void Missing(UnitEntity unit, int slot, Held holder, string during, string why, bool transit)
    {
        var guid = unit.Guid.ToString();
        var line = EquipmentMarkerRules.MissingLine(guid, MapOf(unit), slot.ToString(System.Globalization.CultureInfo.InvariantCulture),
            holder.Key, holder.Amount, 0, Tick(), during, why, LoadIntegrityGuard.IsLoadInProgress);
        Write(ledger.ReportMissing(line, false, transit, guid, slot, holder.Key, holder.Amount, why));
    }

    // The unit owning a watched equipment, else null; a Core unit or a summon counts ignored.
    private static UnitEntity PartyMember(InventoryComponent inventory)
    {
        var equipment = inventory != null && inventory.TryCast<EquipmentComponent>() != null;
        var unit = equipment ? inventory._owner?.TryCast<UnitEntity>() : null;
        var watch = EquipmentMarkerRules.WatchOf(equipment, unit != null,
            unit != null && CoreUnitRules.IsCoreUnit(unit.Key),
            unit != null && !string.IsNullOrEmpty(CoreGame.SummonerGuid(unit.Cast<IEntity>())),
            unit != null && unit.Faction == FactionType.Player);
        if (watch == EquipmentMarkerRules.Watch.Ignored) ledger.CountIgnored();
        return watch == EquipmentMarkerRules.Watch.Party ? unit : null;
    }

    // One GetAllItems per call; null for a missing list or an index outside it.
    private static ItemHolder HolderAt(InventoryComponent inventory, int index)
    {
        var holders = inventory?.GetAllItems();
        return holders != null && index >= 0 && index < holders.Length ? holders[index] : null;
    }

    // The map the unit stands on; the shown map when the unit is on none of the loaded maps; -1 without the game's flow.
    private static int MapOf(UnitEntity unit)
    {
        if (CoreGame.TryMapOfPartyMember(unit, out var map)) return (int)map;
        return CoreGame.Flow != null ? (int)CoreGame.CurrentMap : -1;
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? -1;

    // The game's Guid as the rules' Guid (they stay free of the game's types).
    private static Guid Sys(Il2CppSystem.Guid guid) => Guid.TryParse(guid.ToString(), out var value) ? value : Guid.Empty;

    private static bool TestCampaign()
    {
        var data = CoreSaveStore.Current;
        return data != null && CampaignPresence.IsTestCampaign(data.Campaign);
    }

    private static void Write(IEnumerable<EquipmentMarkerRules.Report> reports)
    {
        foreach (var report in reports) DelversHost.Info(report.Line);
    }

    // A failure of one stage: one line per stage and kind through the existing reporter; after M failures the marker is off
    // until the game restarts, with the summary (dropped counted) and the OFF line (effect 8).
    private static void Fail(string stage, Exception ex)
    {
        ledger.CountError();
        ApocalypseHooks.Report("equipment-marker:" + stage + ":" + ex.GetType().Name, ex);
        if (!failures.Record()) return;
        ledger.Drop(notes.Count);
        notes.Clear();
        frames.Clear();
        DelversHost.Info(EquipmentMarkerRules.OffSummaryLine(ledger.Counters));
        DelversHost.Warning(EquipmentMarkerRules.OffLine(stage));
    }
}

// ---- Harmony hooks: each only reads and never stops the original ----

[HarmonyPatch(typeof(InventoryComponent), nameof(InventoryComponent.RemoveItem))]
internal static class EquipmentMarkerRemovePatch
{
    private static bool Prefix(InventoryComponent __instance, int index)
    {
        EquipmentMarker.OnRemoveItem(__instance, index);
        return true;
    }
}

[HarmonyPatch(typeof(InventoryComponent), nameof(InventoryComponent.AddItem))]
internal static class EquipmentMarkerAddPatch
{
    private static bool Prefix(InventoryComponent __instance, int index, Il2CppSystem.Guid guid, string key, int amount)
    {
        EquipmentMarker.OnAddItem(__instance, index, guid, key, amount);
        return true;
    }
}

[HarmonyPatch(typeof(InventoryComponent), nameof(InventoryComponent.AddTempItem))]
internal static class EquipmentMarkerAddTempPatch
{
    private static bool Prefix(string key, int amount)
    {
        EquipmentMarker.OnAddTempItem(key, amount);
        return true;
    }
}

[HarmonyPatch(typeof(ItemDataContainer), nameof(ItemDataContainer.OnSpawn))]
internal static class EquipmentMarkerSpawnPatch
{
    private static bool Prefix(ItemDataContainer __instance, ItemEntity item, int amount)
    {
        EquipmentMarker.OnSpawnItem(__instance, item, amount);
        return true;
    }
}

[HarmonyPatch(typeof(ItemControlProcessor), nameof(ItemControlProcessor.DestroyItem))]
internal static class EquipmentMarkerDestroyFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(UnitEntity target, int index, out bool __state)
    {
        __state = EquipmentMarker.Open("destroy", target, index);
        return true;
    }

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        EquipmentMarker.Close(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(ItemEventHandler), nameof(ItemEventHandler.OnEvent), new[] { typeof(ConsumeAmmoRequested) })]
internal static class EquipmentMarkerAmmoFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ConsumeAmmoRequested msg, out bool __state)
    {
        __state = EquipmentMarker.Open("ammo", msg?.Owner?.TryCast<UnitEntity>(), EquipmentMarkerRules.AmmoSlot);
        return true;
    }

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        EquipmentMarker.Close(__state);
        return __exception;
    }
}

// Before CoreUnitDespawnPatch (Priority.First): a dead unit's slots sink, a living party member's are reported.
[HarmonyPatch(typeof(EntityLifecycleHelper), nameof(EntityLifecycleHelper.DespawnUnit))]
internal static class EquipmentMarkerDespawnFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(UnitEntity entity, out bool __state)
    {
        __state = EquipmentMarker.Open("despawn", entity, -1);
        return true;
    }

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        EquipmentMarker.Close(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(EntityEventHandler), nameof(EntityEventHandler.OnEvent), new[] { typeof(RetireUnitRequested) })]
internal static class EquipmentMarkerRetireFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        EquipmentMarker.OpenRetire();
        return true;
    }

    private static Exception Finalizer(Exception __exception)
    {
        EquipmentMarker.CloseRetire();
        return __exception;
    }
}

[HarmonyPatch(typeof(EntityLifecycleHelper), nameof(EntityLifecycleHelper.ExtractUnitFrom))]
internal static class EquipmentMarkerTransferFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(UnitEntity unit, out bool __state)
    {
        __state = EquipmentMarker.Open("transfer", unit, -1);
        return true;
    }

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        EquipmentMarker.Close(__state);
        return __exception;
    }
}

// Private, bound by name like CoreUnitPatches' DespawnUnit hook.
[HarmonyPatch(typeof(EntityLoadHelper), "LoadEquipmentSlots")]
internal static class EquipmentMarkerLoadSlotsFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(IEntity owner, out bool __state)
    {
        __state = EquipmentMarker.Open("load", owner?.TryCast<UnitEntity>(), -1);
        return true;
    }

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        EquipmentMarker.Close(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(CampaignEntityLoadHelper), nameof(CampaignEntityLoadHelper.ReloadAllEntities))]
internal static class EquipmentMarkerReloadFrame
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        EquipmentMarker.BeginLoad();
        return true;
    }

    private static Exception Finalizer(Exception __exception)
    {
        EquipmentMarker.EndLoad();
        return __exception;
    }
}
