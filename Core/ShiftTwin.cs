using HarmonyLib;
using UnityEngine;
#if BEPINEX
using KeyInput = global::Refactor.Main.InputModule.KeyInput;
using KeyInputType = global::Refactor.Main.InputModule.KeyInputType;
#else
using KeyInput = Il2CppRefactor.Main.InputModule.KeyInput;
using KeyInputType = Il2CppRefactor.Main.InputModule.KeyInputType;
#endif

namespace DungeonSettlersDelvers.Core;

// The game side of ShiftTwinRules: after KeySetting.RebuildInputIndex (it runs on every change of the bindings, never per
// frame) the runtime index gets the Shift twins of the item quick slot keys. KeyInput is a blittable struct in the interop, so
// the index is written through the dictionary itself. CORE_SHIFT_TWIN once per rebuild, capped.
internal static class ShiftTwin
{
    private static int lines;

    internal static void Add(KeySetting setting)
    {
        var index = setting?._inputsByKeyInput;
        if (index == null) return;
        var entries = new List<ShiftTwinRules.Entry>();
        foreach (var pair in index)
        {
            var types = pair.Value;
            var bound = new int[types?.Count ?? 0];
            for (var i = 0; i < bound.Length; i++) bound[i] = (int)types[i];
            entries.Add(new ShiftTwinRules.Entry((int)pair.Key.KeyCode, pair.Key.IsKeyDown, (int)pair.Key.ModifierKey, bound));
        }
        var twins = ShiftTwinRules.Plan(entries, ItemSlots());
        foreach (var twin in twins)
        {
            var slots = new Il2CppSystem.Collections.Generic.List<KeyInputType>();
            foreach (var type in twin.Types) slots.Add((KeyInputType)type);
            index[new KeyInput((KeyCode)twin.Key, twin.Down, (KeyCode)twin.Modifier)] = slots;
        }
        if (lines++ < ShiftTwinRules.LogCap) DelversHost.Info(ShiftTwinRules.Line(twins));
    }

    private static HashSet<int> ItemSlots()
    {
        var slots = new HashSet<int> { (int)KeyInputType.UseItemQuickSlot_1 };
        foreach (var slot in ShiftTwinRules.HotbarExtraItemSlots) slots.Add(slot);
        return slots;
    }
}

[HarmonyPatch(typeof(KeySetting), nameof(KeySetting.RebuildInputIndex))]
internal static class ShiftTwinPatch
{
    private static void Postfix(KeySetting __instance)
    {
        try { ShiftTwin.Add(__instance); }
        catch (Exception ex) { ApocalypseHooks.Report("shift-twin", ex); }
    }
}
