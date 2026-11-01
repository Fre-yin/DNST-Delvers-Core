using System.Reflection;
using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// The game
// offers the kit only on a fallen unit: its click interaction needs AFFECTER_SevereDamage (IsSevereDamagedUnitExposer).
// Q raises UseItemQuickSlotRequested; PlayerControlEventHandler.OnEvent resolves the selected unit and its quick slot item
// and returns before TryUseItem when the item's skill key is empty or "NULL" (no line from a prefix on TryUseItem). Way B:
// the handler stays as it is; only for ITEM_AidKit, when the
// selected unit itself stands and bleeds (Core's wound or the game's bleeding), Core orders SKILL_AidKit on itself the way
// the bandage AI does (GroupAiBandageUse.OrderOnSelf) and skips the handler. Core's aid kit hook (WoundAidKitPatch) then
// closes the wound and the game's bleeding. CORE_AIDKIT_Q at every Q with the kit, whatever the result.
internal static class AidKitStanding
{
    internal const string SevereDamageKey = "AFFECTER_SevereDamage";
    // The Extended Hotbar's Harmony id (its Plugin.Id, 1.0.2). Its prefix on the same handler sets the pressed slot as
    // context, and its patches on UnitQuickSlotContainer then answer for that slot.
    internal const string ExtendedHotbarHarmonyId = "fre-yin.dnst.extended-hotbar";

    private static MethodInfo resolveUnit;
    private static MethodInfo slotIndex;
    private static string hotbar;

    // True when Core took the Q over. One CORE_AIDKIT_Q_CALL line for every call, first thing, with
    // the step it stopped at (handler, methods, unit, slot, bag, holder, item key), so a missing line means the prefix
    // did not run at all. The line also names the slot context, the quick slot key the game reads for this
    // press (the hotbar's extra slot when one was pressed) and whether the hotbar's prefix is on the handler.
    internal static bool OnQuickSlot(PlayerControlEventHandler handler)
    {
        string step = "start", key = "-", unitText = "-", quickText = "-";
        try
        {
            var unitStep = ResolveUnit(handler, out var unit);
            if (unitStep != null) { step = unitStep; return false; }
            unitText = CoreGame.GuidOf(unit).ToString("D");
            var quickSlot = QuickSlotKey(unit);
            quickText = string.IsNullOrEmpty(quickSlot) ? "-" : quickSlot;
            var bag = CoreGame.Component<InventoryComponent>(unit.Cast<IEntity>());
            if (bag == null) { step = "nobag"; return false; }
            var holder = SlotHolder(handler, unit, bag);
            if (holder == null)
            {
                // (step=noslot for the kit, never for herbs or the book): the game finds the Q item's bag slot
                // through the item's inventory interactions (TryGetItemQuickSlotIndex, InteractionOptionUtility.GetInteractions_Inventory), and the
                // kit has none for its own carrier. So the Q key itself (UnitQuickSlotContainer), then its first bag slot.
                if (quickSlot != GroupAiBandageRules.AidKitItemKey) { step = "noslot"; key = quickText; return false; }
                holder = FirstOf(bag, quickSlot);
                if (holder == null) { step = "noslot:notinbag"; key = quickSlot; return false; }
                step = "slotbykey";
            }
            if (holder == null || holder.IsEmpty()) { step = "empty"; return false; }
            key = holder.Key;
            if (key != GroupAiBandageRules.AidKitItemKey) { step = "other"; return false; }
            var item = handler._entityContainer?.GetEntityOnMap<ItemEntity>(handler._currentMap, holder.Guid);
            var (takeOver, result) = Decide(unit, item);
            step = step == "slotbykey" ? "kit:bykey" : "kit";
            var guid = CoreGame.GuidOf(unit);
            var affecters = CoreGame.Affecters(unit);
            var bleeding = affecters != null && affecters.Has(Wounds.BleedingKey) ? affecters.GetStack(Wounds.BleedingKey) : 0;
            DelversHost.Info($"CORE_AIDKIT_Q unit={guid:D} stage={WoundRules.Describe(Wounds.Stage(guid))} bleeding={bleeding} result={result}");
            return takeOver;
        }
        finally
        {
            // The log line never throws out of here: an exception would replace the result and let the game use the kit too.
            if (calls++ < 200)
            {
                try { DelversHost.Info("CORE_AIDKIT_Q_CALL unit=" + unitText + " step=" + step + " item=" + key + " quick=" + quickText + " hotbar=" + Hotbar()); }
                catch (Exception) { }
            }
        }
    }

    private static int calls;

    // The single selected unit the game resolves for a quick slot press; null when found, else the step it stopped at.
    internal static string ResolveUnit(PlayerControlEventHandler handler, out UnitEntity unit)
    {
        unit = null;
        if (handler == null) return "nohandler";
        resolveUnit ??= AccessTools.Method(typeof(PlayerControlEventHandler), "TryResolveSingleSelectedUnit");
        slotIndex ??= AccessTools.Method(typeof(PlayerControlEventHandler), "TryGetItemQuickSlotIndex");
        if (resolveUnit == null || slotIndex == null) return "nomethod";
        var args = new object[] { null };
        if (!(bool)resolveUnit.Invoke(handler, args) || args[0] is not UnitEntity found) return "nounit";
        unit = found;
        return null;
    }

    // The bag slot the game reads for the pressed quick slot (in the hotbar's context its extra slot), else null.
    internal static ItemHolder SlotHolder(PlayerControlEventHandler handler, UnitEntity unit, InventoryComponent bag)
    {
        var slot = new object[] { unit, 0 };
        return (bool)slotIndex.Invoke(handler, slot) && slot[1] is int index && index >= 0 && index < bag.GetCapacity()
            ? bag.GetItemInfo(index) : null;
    }

    // The Q key the game reads for this press; inside the hotbar's context it is the pressed extra slot's key. Read on every
    // press now, so a failing read only leaves it unknown and never stops the slot path.
    private static string QuickSlotKey(UnitEntity unit)
    {
        try { return CoreGame.Flow?._campaignData?.PlayerUnitDataContainer?._unitQuickSlot?.GetItemQuickSlotKey(unit.Guid); }
        catch (Exception) { return null; }
    }

    // "1" when the Extended Hotbar's prefix sits on the handler, else "0"; read once.
    private static string Hotbar()
    {
        if (hotbar != null) return hotbar;
        var method = AccessTools.Method(typeof(PlayerControlEventHandler), nameof(PlayerControlEventHandler.OnEvent), new[] { typeof(UseItemQuickSlotRequested) });
        var info = method == null ? null : Harmony.GetPatchInfo(method);
        hotbar = info != null && info.Prefixes.Any(p => p.owner == ExtendedHotbarHarmonyId) ? "1" : "0";
        return hotbar;
    }

    private static ItemHolder FirstOf(InventoryComponent bag, string key)
    {
        for (var i = 0; i < bag.GetCapacity(); i++)
        {
            var holder = bag.GetItemInfo(i);
            if (holder != null && !holder.IsEmpty() && holder.Key == key) return holder;
        }
        return null;
    }
    // (take over, result): "ordered", "failed:..." (Core tried, the game's handler runs), or "native:..." (not ours, BandageRules.AidKitRefusal).
    private static (bool, string) Decide(UnitEntity unit, ItemEntity item)
    {
        var refusal = BandageRules.AidKitRefusal(ReadFacts(unit, item));
        if (refusal != null) return (false, refusal);
        var failed = GroupAiBandageUse.OrderOnSelf(unit, item, GroupAiBandageRules.AidKitSkillKey);
        return failed == null ? (true, "ordered") : (false, failed);
    }

    private static BandageRules.AidKitFacts ReadFacts(UnitEntity unit, ItemEntity item)
    {
        var affecters = CoreGame.Affecters(unit);
        var bleeding = affecters != null && affecters.Has(Wounds.BleedingKey) ? affecters.GetStack(Wounds.BleedingKey) : 0;
        return new BandageRules.AidKitFacts(
            hasItem: item != null,
            woundsRule: ApocalypseRules.Wounds,
            woundOpen: Wounds.IsOpen(CoreGame.GuidOf(unit)),
            livingPartyMember: !unit.IsDead && CoreGame.TryMapOfPartyMember(unit, out _),
            hasAffecters: affecters != null,
            down: affecters != null && (affecters.Has(SevereDamageKey) || affecters.Has(ReviveSnapshots.DeadlyKey)),
            bleedingStacks: bleeding);
    }
}

[HarmonyPatch(typeof(PlayerControlEventHandler), nameof(PlayerControlEventHandler.OnEvent), typeof(UseItemQuickSlotRequested))]
internal static class AidKitQuickSlotPatch
{
    // After the Extended Hotbar's own prefix on the same method: it sets the pressed slot as context, so
    // a press on an extra slot reads that slot's item instead of Q's. Without the hotbar the order changes nothing.
    [HarmonyAfter(AidKitStanding.ExtendedHotbarHarmonyId)]
    private static bool Prefix(PlayerControlEventHandler __instance)
    {
        try { return !BandageOnOther.OnQuickSlot(__instance) && !AidKitStanding.OnQuickSlot(__instance); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("aidkit-q", ex);
            return true;
        }
    }
}
