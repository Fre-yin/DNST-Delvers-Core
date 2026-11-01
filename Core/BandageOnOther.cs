using HarmonyLib;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using UseSkillMode = global::Refactor.Control.UseSkillMode;
using UseSkillModeContext = global::Refactor.Main.Processor.UseSkillModeContext;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using UseSkillMode = Il2CppRefactor.Control.UseSkillMode;
using UseSkillModeContext = Il2CppRefactor.Main.Processor.UseSkillModeContext;
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

    internal static void ResetLifecycle() => shiftTest = false;

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
    // dead one and never the helper itself. Null: none. Any bleeding unit counted, and a light bandage put on
    // the game's bleeding alone was used up for nothing (review of p4).
    internal static UnitEntity TargetAt(UnitEntity helper, Vector2Int cell, BandageRules.Kind? kind)
    {
        UnitEntity best = null;
        var bestDistance = int.MaxValue;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead || (helper != null && unit.Guid == helper.Guid) || !Wounds.IsPlayer(unit)) continue;
            if (!BandageRules.TakesOnOther(kind, Wounds.IsOpen(CoreGame.GuidOf(unit)), BleedingStacks(unit))) continue;
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
