using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#if BEPINEX
using Interaction = global::Refactor.Control.Interaction;
using CheckRepairableItem = global::Refactor.Task.CheckRepairableItem;
using DirectTaskHelper = global::Refactor.Main.Event.DirectTaskHelper;
#else
using Interaction = Il2CppRefactor.Control.Interaction;
using CheckRepairableItem = Il2CppRefactor.Task.CheckRepairableItem;
using DirectTaskHelper = Il2CppRefactor.Main.Event.DirectTaskHelper;
#endif

namespace DungeonSettlersDelvers.Core;

// Probe of the group Items (probes.items, off by default; CoreProbeRules): the game's repair path for the boss parts and the
// blood wood shield: the order (DirectTaskHelper.OrderPersonal), the task's item check (CheckRepairableItem.CheckItems) and its
// two tests (NeedAtLeastOneUnitRepair, CanRepairOnce), each with key and result. Watches only, capped at ProbeCap lines; it
// showed in October 2026 why the boss parts were not repaired (RepairTask needs the RECIPE_ row, BossItemRules.RepairRecipeKey).
internal static class BossItemRepairProbe
{
    private const int ProbeCap = 200;
    private const string BloodWoodShield = "ITEM_BloodWoodShield";
    private static int lines;

    internal static bool Watched(string key) => key == BloodWoodShield || BossItemRules.IsBossItem(key);

    internal static void Line(string step, string detail)
    {
        if (!CoreProbes.Items || lines >= ProbeCap) return;
        lines++;
        DelversHost.Info("CORE_BOSS_ITEM_REPAIR_PROBE step=" + step + " " + detail);
    }

    internal static string Keys(Il2CppReferenceArray<ItemHolder> holders, out bool watched)
    {
        watched = false;
        if (holders == null) return "-";
        var keys = new List<string>();
        for (var i = 0; i < holders.Length; i++)
        {
            var holder = holders[i];
            if (holder == null || holder.IsEmpty()) continue;
            if (Watched(holder.Key)) watched = true;
            keys.Add(holder.Key + "@" + holder.Guid.ToString("D").Substring(0, 8));
        }
        return keys.Count == 0 ? "-" : string.Join(",", keys);
    }
}

[HarmonyPatch(typeof(DirectTaskHelper), nameof(DirectTaskHelper.OrderPersonal))]
[ProbeGroup(CoreProbeRules.Items)]
internal static class BossItemRepairProbeOrder
{
    private static void Postfix(Interaction interaction)
    {
        try
        {
            var type = (int)interaction.InteractionType;
            if (type != 10 && type != 11) return;
            var item = interaction.ItemEntity;
            BossItemRepairProbe.Line("order", "interaction=" + type + " task=" + (int)interaction.TaskType + " item=" + (item?.Key ?? "-")
                + " guid=" + (item != null ? item.Guid.ToString("D").Substring(0, 8) : "-"));
        }
        catch (Exception ex) { BossItemRepairProbe.Line("order-failed", ex.Message); }
    }
}

[HarmonyPatch(typeof(CheckRepairableItem), "CheckItems")]
[ProbeGroup(CoreProbeRules.Items)]
internal static class BossItemRepairProbeCheckItems
{
    private static void Postfix(Il2CppReferenceArray<ItemHolder> holders, bool __result)
    {
        try
        {
            var keys = BossItemRepairProbe.Keys(holders, out var watched);
            if (watched) BossItemRepairProbe.Line("check-items", "result=" + __result + " holders=" + keys);
        }
        catch (Exception ex) { BossItemRepairProbe.Line("check-items-failed", ex.Message); }
    }
}

[HarmonyPatch(typeof(CheckRepairableItem), "NeedAtLeastOneUnitRepair")]
[ProbeGroup(CoreProbeRules.Items)]
internal static class BossItemRepairProbeNeed
{
    private static void Postfix(ItemEntity entity, bool __result)
    {
        try
        {
            if (entity != null && BossItemRepairProbe.Watched(entity.Key))
                BossItemRepairProbe.Line("need-unit", "key=" + entity.Key + " guid=" + entity.Guid.ToString("D").Substring(0, 8) + " result=" + __result);
        }
        catch (Exception ex) { BossItemRepairProbe.Line("need-unit-failed", ex.Message); }
    }
}

[HarmonyPatch(typeof(CheckRepairableItem), "CanRepairOnce")]
[ProbeGroup(CoreProbeRules.Items)]
internal static class BossItemRepairProbeCanRepair
{
    private static void Postfix(string itemKey, bool __result)
    {
        try
        {
            if (BossItemRepairProbe.Watched(itemKey)) BossItemRepairProbe.Line("can-repair", "key=" + itemKey + " result=" + __result);
        }
        catch (Exception ex) { BossItemRepairProbe.Line("can-repair-failed", ex.Message); }
    }
}
