namespace DungeonSettlersDelvers.Core;

// Core's own rows in the game's item table (ItemSheet, ItemTableData's columns as public fields): a row copied
// from a game item, and a new row added to the table and its type lists. Used by the bandages and the boss items.
internal static class CoreItemRows
{
    // Every column of the template under a new key.
    internal static ItemTableData CopyOf(ItemTableData t, string key, string comment) => new()
    {
        Key = key,
        DevComment1 = key,
        DevComment2 = comment,
        DevIndex = t.DevIndex,
        Rarity = t.Rarity,
        ItemType = t.ItemType,
        ItemSubType = t.ItemSubType,
        StandardPrice = t.StandardPrice,
        RandomTags = t.RandomTags,
        RandomTagAmount = t.RandomTagAmount,
        MaxStack = t.MaxStack,
        Durability = t.Durability,
        ExpireDuration = t.ExpireDuration,
        Weight = t.Weight,
        HungerRestore = t.HungerRestore,
        EquipmentType = t.EquipmentType,
        IsEdible = t.IsEdible,
        IsUsable = t.IsUsable,
        IsWeapon = t.IsWeapon,
        UseSkill = t.UseSkill,
        Equipment_OnEquipAffecters = t.Equipment_OnEquipAffecters,
        Equipment_ItemDefaultSkills = t.Equipment_ItemDefaultSkills,
        Equipment_MaxHealthTotal = t.Equipment_MaxHealthTotal,
        Equipment_PhysicalAttackPower = t.Equipment_PhysicalAttackPower,
        Equipment_MagicAttackPower = t.Equipment_MagicAttackPower,
        Equipment_AdditionalStatuses = t.Equipment_AdditionalStatuses,
        RequiredAmmoType = t.RequiredAmmoType,
        RequiredItemsRepair = t.RequiredItemsRepair
    };

    // The row in the table and in its type's list, as the game's own parse leaves them.
    internal static void Add(ItemSheet sheet, ItemTableData row)
    {
        sheet._itemTable.Add(row.Key, row);
        sheet._itemTypes.Add(row.Key, row.ItemType);
        if (!sheet._itemsByType.ContainsKey(row.ItemType))
            sheet._itemsByType.Add(row.ItemType, new Il2CppSystem.Collections.Generic.List<ItemTableData>());
        sheet._itemsByType[row.ItemType].Add(row);
    }
}
