using HarmonyLib;
#if BEPINEX
using InputEventFactory = global::Refactor.Main.InputModule.InputEventFactory;
using Interaction = global::Refactor.Control.Interaction;
using InteractionFactory = global::Refactor.Control.InteractionFactory;
using InteractionUtil = global::Refactor.Control.InteractionUtil;
using InventoryList = Il2CppSystem.Collections.Generic.List<global::Refactor.Control.Interaction>;
using UIInput = global::Refactor.Main.InputModule.UIInput;
using UIInputType = global::Refactor.Main.InputModule.UIInputType;
#else
using InputEventFactory = Il2CppRefactor.Main.InputModule.InputEventFactory;
using Interaction = Il2CppRefactor.Control.Interaction;
using InteractionFactory = Il2CppRefactor.Control.InteractionFactory;
using InteractionUtil = Il2CppRefactor.Control.InteractionUtil;
using InventoryList = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.Control.Interaction>;
using UIInput = Il2CppRefactor.Main.InputModule.UIInput;
using UIInputType = Il2CppRefactor.Main.InputModule.UIInputType;
#endif

namespace DungeonSettlersDelvers.Core;

// The inventory's option "Use on another character" (beside Shift with the quick slot): for the four
// bandages, the game's aid kit, ointment and raw herbs of a party member. The game builds an item's options in
// InteractionFactory.CreateInventoryInteraction, names each by its text key (InteractionUtil.GetInteractionTextKey) and turns a
// chosen option's UIInput into an event (InputEventFactory.ConvertUIEventToEventData). Core adds one option of the game's use type
// that carries its mark in the UIInput (ParamString2; the owner in ParamString1, the bag slot in ParamIndex2), names it with its
// own text row, and when it is chosen switches into the target mode of BandageOnOther instead of the game's event (the caller
// skips a null event). The target and the dressing are BandageOnOther's and Bandages.TakeOver's. CORE_USE_ON_OTHER per choice.
internal static class UseOnOther
{
    internal const string Mark = "DelversCore.UseOnOther";
    private const UIInputType UseType = UIInputType.InteractionOption_UseItem;
    // Probe (group Items): whether the option went into an item's menu, per item of the four bandages, the kit, ointment and herbs.
    private const int ProbeCap = 40;
    private static int probeLines;

    internal static void AddOption(IEntity owner, ItemTableData item, int slotIndex, int selectIndex, InventoryList options)
    {
        if (options == null || item == null || Bandages.OtherSkillOf(item.Key) == null) return;
        var unit = owner?.TryCast<UnitEntity>();
        if (unit == null || unit.IsDead || unit.Faction != FactionType.Player)
        {
            Probe(owner, item.Key, slotIndex, options.Count, unit == null ? "skipped:owner" : unit.IsDead ? "skipped:dead" : "skipped:faction");
            return;
        }
        var input = new UIInput(UseType, selectIndex, slotIndex, unit.Guid);
        input.ParamString1 = unit.Guid.ToString();
        input.ParamString2 = Mark;
        input.ParamIndex2 = slotIndex;
        var option = new Interaction
        {
            IsPossible = true,
            UserEntity = owner,
            Index = options.Count,
            InteractionInputType = UseType,
            InteractionUIInput = input
        };
        if (options.Count > 0) option.InteractionType = options[0].InteractionType;
        // Interaction is a struct: through the wrapper, never List.Add (FALLEN-CORE, trap 1).
        DelversNativeList.AddValue(options, option);
        Probe(owner, item.Key, slotIndex, options.Count, "added");
    }

    private static void Probe(IEntity owner, string itemKey, int slot, int count, string result)
    {
        if (!CoreProbes.Items || probeLines >= ProbeCap) return;
        probeLines++;
        DelversHost.Info($"CORE_USE_ON_OTHER_OPTION unit={(owner != null ? owner.Guid.ToString() : "-")} item={itemKey} slot={slot} options={count} result={result}");
    }

    internal static bool IsOurs(Interaction interaction) => interaction != null && interaction.InteractionUIInput?.ParamString2 == Mark;

    // True when the input was ours: Core aims the item, the game gets no event.
    internal static bool OnInput(UIInput input)
    {
        if (input == null || input.UIInputType != UseType || input.ParamString2 != Mark) return false;
        var result = Aim(input.ParamString1, input.ParamIndex2, out var unitText, out var itemKey);
        DelversHost.Info($"CORE_USE_ON_OTHER unit={unitText} slot={input.ParamIndex2} item={itemKey} result={result ?? "aim"}");
        return true;
    }

    private static string Aim(string owner, int slot, out string unitText, out string itemKey)
    {
        unitText = owner ?? "-";
        itemKey = "-";
        var unit = Guid.TryParse(owner, out var guid) ? CoreGame.FindUnit(guid) : null;
        if (unit == null || unit.IsDead) return "failed:unit";
        var bag = CoreGame.Component<InventoryComponent>(unit.Cast<IEntity>());
        if (bag == null || slot < 0 || slot >= bag.GetCapacity()) return "failed:slot";
        var holder = bag.GetItemInfo(slot);
        if (holder == null || holder.IsEmpty()) return "failed:empty";
        itemKey = holder.Key;
        var skill = Bandages.OtherSkillOf(holder.Key);
        if (skill == null) return "failed:item";
        if (!CoreGame.TryMapOfPartyMember(unit, out var map)) return "failed:map";
        var item = ItemOf(map, holder);
        var refused = BandageOnOther.Aim(unit, item, skill);
        return refused == null ? null : "failed:" + refused;
    }

    // The bag item as the game finds it: the map's entities, then the entity container behind the map data.
    private static ItemEntity ItemOf(MapType map, ItemHolder holder)
    {
        var inMap = CoreGame.InMap;
        var item = inMap?.GetEntity(holder.Guid)?.TryCast<ItemEntity>();
        if (item != null) return item;
        return inMap?.TryCast<InMapDataInterface>()?._entityContainer?.GetEntityOnMap<ItemEntity>(map, holder.Guid);
    }
}

// The options of an item in a unit's bag, chosen by selection (UseOnOther).
[HarmonyPatch(typeof(InteractionFactory), nameof(InteractionFactory.CreateInventoryInteraction),
    new[] { typeof(IEntity), typeof(ItemTableData), typeof(int), typeof(int) })]
internal static class UseOnOtherBySelectionPatch
{
    private static void Postfix(IEntity ownerEntity, ItemTableData tableData, int selectIndex, int slotIndex, InventoryList __result)
    {
        try { UseOnOther.AddOption(ownerEntity, tableData, slotIndex, selectIndex, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("use-on-other-option", ex); }
    }
}

// The same options reached by the owner's GUID (UseOnOther).
[HarmonyPatch]
internal static class UseOnOtherByOwnerPatch
{
    // The overload with the owner's GUID third, found by its parameters. A game update that drops it (or adds a second one) only
    // costs this option's second way: Prepare leaves the class out, and Core's other patches go on (a patch class that fails takes
    // all of Core back).
    private static System.Reflection.MethodBase Target()
        => typeof(InteractionFactory).GetMethods(System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .FirstOrDefault(method => method.Name == nameof(InteractionFactory.CreateInventoryInteraction)
                && method.GetParameters().Length == 4
                && method.GetParameters()[0].ParameterType == typeof(IEntity)
                && method.GetParameters()[1].ParameterType == typeof(ItemTableData)
                && method.GetParameters()[2].ParameterType.Name == "Guid"
                && method.GetParameters()[3].ParameterType == typeof(int));

    private static bool Prepare()
    {
        if (Target() != null) return true;
        DelversHost.Warning("CORE_USE_ON_OTHER_OWNER_PATCH_SKIPPED: the game has no CreateInventoryInteraction(IEntity, ItemTableData, Guid, int)");
        return false;
    }

    private static System.Reflection.MethodBase TargetMethod() => Target();

    private static void Postfix(IEntity ownerEntity, ItemTableData tableData, int slotIndex, InventoryList __result)
    {
        try { UseOnOther.AddOption(ownerEntity, tableData, slotIndex, 0, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("use-on-other-option", ex); }
    }
}

// The option's name: Core's own text row (BandageText.UseOnOtherKey).
[HarmonyPatch(typeof(InteractionUtil), "GetInteractionTextKey")]
internal static class UseOnOtherTextPatch
{
    private static void Postfix(Interaction interaction, ref string __result)
    {
        try { if (UseOnOther.IsOurs(interaction)) __result = BandageText.UseOnOtherKey; }
        catch (Exception ex) { ApocalypseHooks.Report("use-on-other-text", ex); }
    }
}

// The chosen option: the target mode instead of the game's use event (UseOnOther).
[HarmonyPatch(typeof(InputEventFactory), nameof(InputEventFactory.ConvertUIEventToEventData))]
internal static class UseOnOtherInputPatch
{
    private static bool Prefix(UIInput uiInput, ref IEventData __result)
    {
        try
        {
            if (!UseOnOther.OnInput(uiInput)) return true;
            __result = null;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("use-on-other-input", ex);
            return true;
        }
    }
}
