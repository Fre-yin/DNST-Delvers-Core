using DungeonSettlersDelvers.Core;
using static TestKit;

// Compatible load patches of known mods.
internal static class LoadPatchCompatibilitySection
{
    internal static void Run()
    {
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.1.0",
                "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
            "Extended Hotbar container restore is recognized as an exact compatible load patch");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.0.1.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "Extended Hotbar quick-slot restore is recognized as an exact compatible load patch");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlersHotbar.BepInEx", "1.0.1.0",
                "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
            "BepInEx Extended Hotbar container restore is recognized as an exact compatible load patch");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlersHotbar.BepInEx", "1.0.1.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "BepInEx Extended Hotbar quick-slot restore is recognized as an exact compatible load patch");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.2.0",
                "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix"),
            "Extended Hotbar 1.0.2 keeps the reviewed container restore");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlersHotbar.BepInEx", "1.0.2.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "BepInEx Extended Hotbar 1.0.2 keeps the reviewed quick-slot restore");
        Check(LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "UnitQuickSlotContainer.Deserialize", "postfix", "DungeonSettlersHotbar.BepInEx", "1.0.3.0",
                "DungeonSettlers10Slots.ContainerSlotsLoaded", "Postfix")
                && LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlersHotbar.BepInEx", "1.0.3.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix")
                && LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.0.3.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "Extended Hotbar 1.0.3 keeps both reviewed load patches");
        Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.0.4.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "an unreviewed Extended Hotbar patch release remains a foreign patch");
        Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "DungeonSettlers10Slots", "1.1.0.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "an unreviewed Extended Hotbar version remains a foreign patch");
        Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "postfix", "DungeonSettlers10Slots", "1.0.1.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "a changed Extended Hotbar patch phase remains a foreign patch");
        Check(!LoadPatchCompatibilityRules.IsKnownExtendedHotbar(
                "QuickSlotData.Deserialize", "prefix", "OtherHotbar", "1.0.1.0",
                "DungeonSettlers10Slots.LoadSlots", "Prefix"),
            "an unrelated assembly cannot impersonate the known Hotbar integration");
        CoreOwners();
    }

    // The audit counts Core's own id and every id below it as Core: the probe classes' ids and the enemy skill test's.
    static void CoreOwners()
    {
        const string core = "fre-yin.dungeonsettlers.delvers.core";
        Check(LoadPatchCompatibilityRules.IsCoreOwner(core, core)
                && LoadPatchCompatibilityRules.IsCoreOwner(core + ".enemy-skill-test", core)
                && LoadPatchCompatibilityRules.IsCoreOwner(core + ".probes.DungeonSettlersDelvers.Core.GroupAiOrderSourceProbe", core),
            "the load patch audit counts Core's id and the ids below it as Core's own");
        Check(!LoadPatchCompatibilityRules.IsCoreOwner(core + "x", core)
                && !LoadPatchCompatibilityRules.IsCoreOwner(core + "-other", core)
                && !LoadPatchCompatibilityRules.IsCoreOwner("other." + core, core)
                && !LoadPatchCompatibilityRules.IsCoreOwner(core.Substring(0, core.Length - 1), core)
                && !LoadPatchCompatibilityRules.IsCoreOwner(null, core)
                && !LoadPatchCompatibilityRules.IsCoreOwner(core, null)
                && !LoadPatchCompatibilityRules.IsCoreOwner(core, ""),
            "a similar id without the dot, a shorter id and a missing id stay foreign in the load patch audit");
    }
}
