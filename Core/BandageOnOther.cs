using HarmonyLib;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using UseSkillMode = global::Refactor.Control.UseSkillMode;
using UseSkillModeContext = global::Refactor.Main.Processor.UseSkillModeContext;
using ItemUseSpendRoutine = global::Refactor.Combat.ItemUseCondition._OnSuccessCondition_d__2;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using UseSkillMode = Il2CppRefactor.Control.UseSkillMode;
using UseSkillModeContext = Il2CppRefactor.Main.Processor.UseSkillModeContext;
using ItemUseSpendRoutine = Il2CppRefactor.Combat.ItemUseCondition._OnSuccessCondition_d__2;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ActCombatBehaviourTask = Il2CppRefactor.Task.ActCombatBehaviourTask;
#endif

namespace DungeonSettlersDelvers.Core;

// A bandage put on another unit (with Shift and Q other targets can be dressed). Q puts the bandage on
// the selected unit itself as before; with Shift held the press switches into the game's own target mode for the bandage's second
// skill (Bandages.OtherSkillOf, an entity target in reach 1 like the aid kit), the way PlayerControlEventHandler.TryUseItem does for
// items with a target: a UseSkillModeContext with the item, ControlMode.UseSkill. A click then lands in UseSkillMode, and for these
// skills BandageOtherTargetPatch picks the party unit with an open wound on the clicked cell (or next to it); the game walks the
// helper into reach and puts the bandage on, and Bandages.TakeOver dresses the target's wound. Inside the Extended Hotbar's context
// the pressed extra slot is read the same way, so Shift with its key works too. Right click or Escape leave the mode as for any item.
// The game itself drops a quick slot key pressed with Shift, as no binding has that combination; ShiftTwin adds it at runtime.
internal static class BandageOnOther
{
    // A posted Shift never reaches Input.GetKey, so a test campaign may hold Shift for the next presses
    // (DelversCoreRuntime.SetShiftForTest) until it lifts it or Core shuts down. It stands for the held key in this handler only and
    // never reaches the game's binding check, so it hid that a real Shift with the quick slot key never got here (ShiftTwin); the
    // players' way is checked with a real Shift key.
    private static bool shiftTest;

    internal static void SetShiftTest(bool held) => shiftTest = held;

    internal static void ResetLifecycle()
    {
        shiftTest = false;
        BandageOtherWatch.ResetLifecycle();
    }

    // True when Core took the press over.
    internal static bool OnQuickSlot(PlayerControlEventHandler handler)
    {
        if (!shiftTest && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return false;
        if (AidKitStanding.ResolveUnit(handler, out var unit) != null) return false;
        var bag = CoreGame.Component<InventoryComponent>(unit.Cast<IEntity>());
        var holder = bag == null ? null : AidKitStanding.SlotHolder(handler, unit, bag);
        var skillKey = holder == null || holder.IsEmpty() ? null : Bandages.OtherSkillOf(holder.Key);
        if (skillKey == null) return false;
        var guid = CoreGame.GuidOf(unit);
        var item = handler._entityContainer?.GetEntityOnMap<ItemEntity>(handler._currentMap, holder.Guid);
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        var processor = handler._modeProcessor;
        if (item == null || skill == null || processor == null)
        {
            DelversHost.Info($"CORE_BANDAGE_OTHER unit={guid:D} item={holder.Key} result=failed:{(item == null ? "item" : skill == null ? "skill" : "mode")}");
            return false;
        }
        var context = new UseSkillModeContext();
        context.AddUseItem(unit, skill, item);
        processor.SwitchMode(ControlMode.UseSkill);
        processor.SetContext(context.Cast<IControlModeContext>());
        DelversHost.Info($"CORE_BANDAGE_OTHER unit={guid:D} item={holder.Key} result=aim");
        return true;
    }

    // The bleeding party unit the item may be put on (BandageRules.TakesOnOther: an open wound, or the game's bleeding stacks for
    // the items that cut it) on the clicked cell, else the nearest one next to it; a fallen one too, never a
    // dead one and never the helper itself; the aid kit no fallen one (the game's own case). Null: none. Any bleeding unit counted, and a light bandage put on
    // the game's bleeding alone was used up for nothing.
    internal static UnitEntity TargetAt(UnitEntity helper, Vector2Int cell, BandageRules.Kind? kind)
    {
        UnitEntity best = null;
        var bestDistance = int.MaxValue;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead || (helper != null && unit.Guid == helper.Guid) || !Wounds.IsPlayer(unit)) continue;
            if (!BandageRules.TakesOnOther(kind, Wounds.IsOpen(CoreGame.GuidOf(unit)), BleedingStacks(unit), Bandages.IsFallen(unit))) continue;
            var at = CoreGame.Position(unit);
            var distance = Math.Max(Math.Abs(at.x - cell.x), Math.Abs(at.y - cell.y));
            if (distance > 1 || distance >= bestDistance) continue;
            best = unit;
            bestDistance = distance;
        }
        return best;
    }

    private static int BleedingStacks(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        return affecters != null && affecters.Has(Wounds.BleedingKey) ? affecters.GetStack(Wounds.BleedingKey) : 0;
    }

    // The target mode for an item put on another unit (Shift with its quick slot, or the inventory's option, UseOnOther): the
    // game's own way for items with a target. Returns null when the mode is on, else why not.
    internal static string Aim(UnitEntity unit, ItemEntity item, string skillKey)
    {
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        var processor = CoreGame.Flow?._eventProcessor?.ControlModeProcessor;
        if (unit == null || item == null) return "item";
        if (skill == null) return "skill";
        if (processor == null) return "mode";
        var context = new UseSkillModeContext();
        context.AddUseItem(unit, skill, item);
        processor.SwitchMode(ControlMode.UseSkill);
        processor.SetContext(context.Cast<IControlModeContext>());
        return null;
    }
}

// UseSkillMode.GetPossibleTarget for the bandages on another unit: the wounded party unit at the clicked cell (BandageOnOther.TargetAt).
// The game itself finds entity targets only through its click interactions, which a standing comrade does not offer.
[HarmonyPatch(typeof(UseSkillMode), "GetPossibleTarget")]
internal static class BandageOtherTargetPatch
{
    private static int failures;

    // Every other skill runs through unchanged.
    private static bool Prefix(UnitEntity user, ActiveSkill_Base skill, Vector2Int position, ref ICombatTargetSelector __result)
    {
        if (skill == null || !Bandages.IsOtherSkill(skill.Key)) return true;
        try
        {
            var target = BandageOnOther.TargetAt(user, position, BandageRules.KindOnOther(Bandages.ItemOfOtherSkill(skill.Key)));
            DelversHost.Info($"CORE_BANDAGE_OTHER_TARGET helper={CoreGame.GuidOf(user):D} target={(target == null ? "-" : CoreGame.GuidOf(target).ToString("D"))} cell={position.x},{position.y}");
            if (target == null)
            {
                __result = null;
                return false;
            }
            BandageOtherWatch.OnAim(user, skill.Key);
            var targets = new Il2CppSystem.Collections.Generic.List<IEntity>();
            targets.Add(target.Cast<IEntity>());
            __result = EntityTargetSelector.SelectFromSpecificTargets(targets.Cast<Il2CppSystem.Collections.Generic.IEnumerable<IEntity>>()).Cast<ICombatTargetSelector>();
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_BANDAGE_OTHER_TARGET_FAILED: " + ex.Message);
            __result = null;
        }
        return false;
    }
}

// The aid kit put on a comrade is never lost (the author's rule: the kit stops everything). An entry opens only where the game uses up
// this very kit: the item condition's first step for SKILL_DelversCoreAidKitOther (ItemUseCondition.OnSuccessCondition, state 0,
// which takes the kit from the helper's hand; then the skill's routine runs). Core's dressing of that use (Bandages.TakeOver) closes
// it; an entry still open after BandageRules.RefundAfterSeconds gives the kit back (BandageRules.KitLedger): into the helper's bag
// through the game's own TryAddToInventory with the item's table durability, as the game's additem command does; with the bag full
// or the helper gone, onto the ground before the helper, or where it was used, through the game's DropToMapWithResult. A kit that
// leaves the bag any other way (dropped, given, traded, sold) opens nothing. Wounds.Tick looks once a second; a load or a new
// campaign clears the ledger (Wounds.ResetCampaign). CORE_BANDAGE_REFUNDED names the helper, the target, where the kit went and the
// kits in the bag before and after. A use that broke off before the kit was used up leaves it in the helper's hand; once the helper
// no longer uses an item it goes back into the bag (BandageRules.HandBack, CORE_BANDAGE_BACK), never as well as through the ledger.
internal static class BandageOtherWatch
{
    private readonly struct Spot
    {
        internal Spot(Guid target, MapType map, Vector2Int cell)
        {
            Target = target;
            Map = map;
            Cell = cell;
        }

        internal Guid Target { get; }
        internal MapType Map { get; }
        internal Vector2Int Cell { get; }
    }

    private static readonly BandageRules.KitLedger<Spot> ledger = new();
    // Helpers whose kit Core's skill aimed and not yet used up, with the aim's time (BandageRules.HandBack).
    private static readonly Dictionary<Guid, float> aimed = new();

    internal static void ResetLifecycle()
    {
        ledger.Clear();
        aimed.Clear();
    }

    // Core's aid kit aimed at a comrade (BandageOtherTargetPatch): the game takes the kit into the helper's hand for the use.
    internal static void OnAim(UnitEntity helper, string skillKey)
    {
        if (helper != null && skillKey == Bandages.AidKitOtherSkillKey) aimed[CoreGame.GuidOf(helper)] = GameClock.Now;
    }

    // The game used up the kit of this use (BandageKitSpendPatch).
    internal static void OnKitUsedUp(SkillExecutionContext context)
    {
        if (context?.Publisher?.TryCast<ActiveSkill_Base>()?.Key != Bandages.AidKitOtherSkillKey) return;
        var helper = context.User?.TryCast<UnitEntity>();
        if (helper == null || !CoreGame.TryMapOfUnit(helper, out var map)) return;
        var target = context.TargetEntity?.TryCast<UnitEntity>();
        var spot = new Spot(target != null ? CoreGame.GuidOf(target) : Guid.Empty, map, CoreGame.Position(helper));
        aimed.Remove(CoreGame.GuidOf(helper));
        ledger.UsedUp(CoreGame.GuidOf(helper), spot, GameClock.Now);
    }

    // Core dressed with the kit of this helper's latest use: that entry closes, no other.
    internal static void OnDressed(UnitEntity helper)
    {
        if (helper != null && ledger.Count > 0) ledger.Dressed(CoreGame.GuidOf(helper));
    }

    internal static void Look()
    {
        if (aimed.Count > 0) LookHands();
        if (ledger.Count == 0) return;
        foreach (var entry in ledger.Due(GameClock.Now))
        {
            try { GiveBack(entry); }
            catch (Exception ex) { Failed("refund", entry.Helper, ex); }
        }
    }

    // A use that broke off before the kit was used up leaves it in the helper's hand: back into the bag (BandageRules.HandBack).
    private static void LookHands()
    {
        var now = GameClock.Now;
        foreach (var helperGuid in aimed.Keys.ToList())
        {
            var helper = CoreGame.FindUnit(helperGuid);
            if (helper == null || helper.IsDead)
            {
                aimed.Remove(helperGuid);
                continue;
            }
            var step = BandageRules.HandBack(false, UsesItem(helper), CarriesKit(helper), now - aimed[helperGuid]);
            if (step == BandageRules.HandStep.Wait) continue;
            aimed.Remove(helperGuid);
            if (step != BandageRules.HandStep.Back) continue;
            try { HandBack(helper, helperGuid); }
            catch (Exception ex) { Failed("hand", helperGuid, ex); }
        }
    }

    // The game's way: EndCarry takes the kit out of the hand, TryAddToInventory puts it into the bag, what does not fit goes onto
    // the ground before the helper (DropToMapWithResult). CORE_BANDAGE_BACK names where it went.
    private static void HandBack(UnitEntity helper, Guid helperGuid)
    {
        var processor = CoreGame.Flow?._eventProcessor?.ItemControlProcessor;
        if (processor == null || !CoreGame.TryMapOfUnit(helper, out var map)) return;
        var before = Kits(helper);
        // The amount in the hand, never null: under Il2Cpp the game's Nullable<int> is an object, and null throws before the game is
        // reached (every hand back failed so on core-e663f6e).
        var hand = CoreGame.Component<InventoryComponent>(helper.Cast<IEntity>())?.GetTempSlot();
        var inHand = hand != null && !hand.IsEmpty() ? hand.Amount : 0;
        if (inHand <= 0) return;
        var held = processor.EndCarry(map, helper.Cast<IEntity>(), new Il2CppSystem.Nullable<int>(inHand));
        if (held.IsEmpty() || held.Amount <= 0) return;
        var amount = held.Amount;
        processor.TryAddToInventory(map, helper.Cast<IEntity>(), held.Key, held.Tags, ref amount, held.Durability, held.AutoFix, true, false);
        var after = Kits(helper);
        var left = Math.Max(0, held.Amount - Math.Max(0, after - before));
        var to = left == 0 ? "bag" : "lost";
        var cell = CoreGame.Position(helper);
        if (left > 0 && processor.DropToMapWithResult(map, cell, held.Key, held.Tags, left, held.Durability, held.AutoFix)?.IsFullyDropped() == true)
            to = after > before ? "bag+ground" : "ground";
        var line = $"CORE_BANDAGE_BACK unit={helperGuid:D} item=aidkit to={to} kits={before}->{after} cell={cell.x},{cell.y}";
        if (to == "lost") DelversHost.Warning(line);
        else DelversHost.Info(line);
    }

    // The helper still uses the kit (BandageRules.StillUsing): its brain on its own map runs Core's kit skill as an
    // ActCombatBehaviourTask, on the way to the target or putting it on, or one of the game's UseItem tasks.
    private static bool UsesItem(UnitEntity unit)
    {
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return false;
        var task = CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.GetTask();
        if (task == null) return false;
        var skill = task.TryCast<ActCombatBehaviourTask>()?.Skill?.Key;
        return BandageRules.StillUsing(skill, task.GetIl2CppType().Name);
    }

    // One failing kit never stops the others: a warning per kind, at most WarnCap in all.
    private const int WarnCap = 5;
    private static int warnings;

    private static void Failed(string what, Guid helper, Exception ex)
    {
        if (warnings++ < WarnCap) DelversHost.Warning($"CORE_BANDAGE_WATCH_FAILED step={what} unit={helper:D}: {ex.GetType().Name}: {ex.Message}");
    }

    // An aid kit in the helper's hand (the bag's carry slot).
    private static bool CarriesKit(UnitEntity unit)
    {
        var hand = CoreGame.Component<InventoryComponent>(unit.Cast<IEntity>())?.GetTempSlot();
        return hand != null && !hand.IsEmpty() && hand.Key == BandageRules.AidKitItemKey;
    }

    private static void GiveBack(BandageRules.KitLedger<Spot>.Entry entry)
    {
        var processor = CoreGame.Flow?._eventProcessor?.ItemControlProcessor;
        var data = DataSheetManager.Instance?.GetItemData(BandageRules.AidKitItemKey);
        var helper = CoreGame.FindUnit(entry.Helper);
        var alive = helper != null && !helper.IsDead;
        var before = alive ? Kits(helper) : 0;
        var after = before;
        var to = "lost";
        var map = entry.Where.Map;
        var cell = entry.Where.Cell;
        if (processor != null && data != null)
        {
            if (alive && CoreGame.TryMapOfUnit(helper, out var helperMap))
            {
                var amount = 1;
                processor.TryAddToInventory(helperMap, helper.Cast<IEntity>(), BandageRules.AidKitItemKey, null, ref amount, data.Durability, true, true, false);
                after = Kits(helper);
                if (after > before) to = "bag";
                map = helperMap;
                cell = CoreGame.Position(helper);
            }
            if (to == "lost" && processor.DropToMapWithResult(map, cell, BandageRules.AidKitItemKey, null, 1, data.Durability, true)?.IsFullyDropped() == true)
                to = "ground";
        }
        var line = $"CORE_BANDAGE_REFUNDED unit={entry.Helper:D} item=aidkit target={entry.Where.Target:D} to={to} kits={before}->{after} cell={cell.x},{cell.y}";
        if (to == "lost") DelversHost.Warning(line);
        else DelversHost.Info(line);
    }

    // The aid kits in the helper's bag.
    private static int Kits(UnitEntity unit)
    {
        var slots = CoreGame.Component<InventoryComponent>(unit.Cast<IEntity>())?.GetAllItems();
        var count = 0;
        for (var i = 0; slots != null && i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot != null && !slot.IsEmpty() && slot.Key == BandageRules.AidKitItemKey) count += Math.Max(0, slot.Amount);
        }
        return count;
    }
}

// The game's item condition succeeded for a skill with an item (ItemUseCondition.OnSuccessCondition): at its first step (state 0)
// it takes the item from the user's hand. For Core's aid kit on a comrade the watch starts there (BandageOtherWatch).
[HarmonyPatch(typeof(ItemUseSpendRoutine), "MoveNext")]
internal static class BandageKitSpendPatch
{
    private static int failures;

    private static void Prefix(ItemUseSpendRoutine __instance)
    {
        try
        {
            if (__instance == null || __instance.__1__state != 0) return;
            BandageOtherWatch.OnKitUsedUp(__instance.context);
        }
        catch (Exception ex) { if (++failures <= 3) DelversHost.Warning("CORE_BANDAGE_WATCH_FAILED: " + ex.Message); }
    }
}
