using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// A bandit's kit dropped by undressing (CoreUnits.DropKit) in the condition KitDropRules gives it: the drop is noted, and when
// the game spawns the item near there (EntityDataContainer.OnSpawnItem) its durability (StatType.Durability 69 of the item,
// durability and repair) is set through the item's status (StatusComponent.AddValue).
internal static class KitDrops
{
    // Drops waiting for the game's spawn: key and cell. One undressed corpse at a time.
    private const int PendingCap = 16;
    private static readonly List<(string Key, int X, int Y)> pending = new();
    private static readonly System.Random roll = new();
    private static int unmatchedLines;

    internal static void ResetCampaign() => pending.Clear();

    internal static void NoteDrop(string key, Vector2Int at)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (pending.Count >= PendingCap) pending.RemoveAt(0);
        pending.Add((key, at.x, at.y));
    }

    // A spawned item that belongs to a noted drop (KitDropRules.MatchDrop) gets its drop condition.
    internal static void OnSpawned(ItemEntity item, Vector2Int at)
    {
        if (item == null) return;
        var boss = BossItemRules.IsBossItem(item.Key);
        var i = pending.Count == 0 ? -1 : KitDropRules.MatchDrop(pending, item.Key, at.x, at.y);
        if (i < 0)
        {
            if (boss && unmatchedLines++ < PendingCap) DelversHost.Info(KitDropRules.UnmatchedLine(item.Key, at.x, at.y, pending.Count));
            return;
        }
        pending.RemoveAt(i);
        var status = CoreGame.Component<StatusComponent>(item.Cast<IEntity>());
        if (status == null)
        {
            DelversHost.Warning(KitDropRules.DropLine(item.Key, at.x, at.y, -1, -1, -1));
            return;
        }
        var reader = new StatusReader(status);
        var before = reader.GetValue(StatType.Durability);
        var max = reader.GetValue(StatType.MaxDurability);
        var target = KitDropRules.DropDurability(max, roll.NextDouble(), boss);
        if (max > 0) status.AddValue(StatType.Durability, (float)(target - before));
        DelversHost.Info(KitDropRules.DropLine(item.Key, at.x, at.y, before, max, reader.GetValue(StatType.Durability)));
    }
}

[HarmonyPatch(typeof(EntityDataContainer), nameof(EntityDataContainer.OnSpawnItem))]
internal static class KitDropSpawnPatch
{
    private static void Postfix(ItemEntity entity, Vector2Int position)
    {
        try { KitDrops.OnSpawned(entity, position); }
        catch (Exception ex) { ApocalypseHooks.Report("kit-drop", ex); }
    }
}
