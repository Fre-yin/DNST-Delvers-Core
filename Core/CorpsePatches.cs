using HarmonyLib;
#if BEPINEX
using Interaction = global::Refactor.Control.Interaction;
using InteractionFactory = global::Refactor.Control.InteractionFactory;
using ItemControlProcessor = global::Refactor.Main.Processor.ItemControlProcessor;
#else
using Interaction = Il2CppRefactor.Control.Interaction;
using InteractionFactory = Il2CppRefactor.Control.InteractionFactory;
using ItemControlProcessor = Il2CppRefactor.Main.Processor.ItemControlProcessor;
#endif

namespace DungeonSettlersDelvers.Core;

// Equipment stays on the body (CorpseKeep). Arguments bound by name; every target
// with overloads is named with its argument types. Failures are reported and the game's way stays.

// The game's death of a unit: CorpseKeep knows which unit dies while its handling runs, the finalizer
// clears it whatever happens.
[HarmonyPatch(typeof(EntityEventHandler), nameof(EntityEventHandler.OnEvent), new[] { typeof(DeadUnitRequested) })]
internal static class CorpseDeathPatch
{
    private static void Prefix(DeadUnitRequested msg)
    {
        try { CorpseKeep.DeathStarts(msg?.Target); }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-death", ex); }
    }

    private static Exception Finalizer(Exception __exception)
    {
        CorpseKeep.DeathEnds();
        return __exception;
    }
}

// A dying party member keeps its equipment: the game's drop of all equipment slots is skipped.
[HarmonyPatch(typeof(ItemControlProcessor), nameof(ItemControlProcessor.DropAllEquipItem))]
internal static class CorpseKeepPatch
{
    private static bool Prefix(IEntity entity)
    {
        try { return !CorpseKeep.Keeps(entity); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("corpse-keep", ex);
            return true;
        }
    }
}

// Probe (step 2): the game's actions at a dead unit, and at any unit while the user carries someone. A probe class only: it
// carries the group Corpse and is patched only while that group is on (CoreProbes).
[ProbeGroup(CoreProbeRules.Corpse)]
[HarmonyPatch(typeof(InteractionFactory), "GenerateUnitInteraction")]
internal static class CorpseUnitOptionsProbe
{
    private static void Postfix(UnitEntity userEntity, UnitEntity targetEntity, Il2CppSystem.Collections.Generic.List<Interaction> __result)
    {
        try { CorpseKeep.ProbeOptions("unit", userEntity, targetEntity?.Cast<IEntity>(), targetEntity != null && targetEntity.IsDead, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-probe-unit", ex); }
    }
}

// Core's own option at a dressed corpse in the game's list. Not a probe class: the option is its work, so it stays whatever
// the group Corpse says (it sat in the probe above and went with the group's patch, the).
[HarmonyPatch(typeof(InteractionFactory), "GenerateUnitInteraction")]
internal static class CorpseUndressOptionAdd
{
    private static void Postfix(UnitEntity userEntity, UnitEntity targetEntity, Il2CppSystem.Collections.Generic.List<Interaction> __result)
    {
        try { CorpseKeep.AddUndressOption(userEntity, targetEntity, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-undress-option", ex); }
    }
}

// Core's option chosen in the game's option menu (InteractionOptionUI.OnSelect, the click on an entry): the corpse is
// undressed at once and the menu closes; every other option runs as the game has it.
[HarmonyPatch(typeof(InteractionOptionUI), nameof(InteractionOptionUI.OnSelect))]
internal static class CorpseUndressOptionSelect
{
    private static bool Prefix(InteractionOptionUI __instance, Interaction interaction)
    {
        // A throw while asking leaves the option to the game.
        bool ours;
        try { ours = CorpseKeep.IsUndressOption(interaction); }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("corpse-undress-select", ex);
            return true;
        }
        if (!ours) return true;
        try
        {
            CorpseKeep.Undress(interaction.TargetEntity?.TryCast<UnitEntity>(), interaction.UserEntity?.TryCast<UnitEntity>());
            __instance.Hide();
        }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-undress-select", ex); }
        return false;
    }
}

// The entry's words: the game names an entry by its interaction type, which Core's own type does not have.
[HarmonyPatch(typeof(InteractionOptionElementUI), nameof(InteractionOptionElementUI.SetUI))]
internal static class CorpseUndressOptionText
{
    private static void Postfix(InteractionOptionElementUI __instance)
    {
        try
        {
            if (!CorpseKeep.IsUndressOption(__instance._interaction)) return;
            var text = MenuText.Pick(CorpseText.Undress);
            if (__instance._txtBasic != null) __instance._txtBasic.text = text;
            if (__instance._txtNormal != null) __instance._txtNormal.text = text;
        }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-undress-text", ex); }
    }
}

// Probe (step 3): the game's actions at a building (grave, base portal) while the user carries someone. A probe class like the
// one above, in the group Corpse.
[ProbeGroup(CoreProbeRules.Corpse)]
[HarmonyPatch(typeof(InteractionFactory), "GenerateBuildingInteraction")]
internal static class CorpseBuildingOptionsProbe
{
    private static void Postfix(UnitEntity userEntity, BuildingEntity buildingEntity, Il2CppSystem.Collections.Generic.List<Interaction> __result)
    {
        try { CorpseKeep.ProbeOptions("building", userEntity, buildingEntity?.Cast<IEntity>(), false, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-probe-building", ex); }
    }
}

// Step 3: the floor portal of a dungeon, which opens the game's result window: the probe line and the warning when
// dressed fallen stay behind. Not a probe class: the warning is its work, so the patch stays whatever the group Corpse says;
// only the probe line asks the group (CorpseKeep.ProbeLeave).
[HarmonyPatch(typeof(DungeonPortalHandler), nameof(DungeonPortalHandler.Process), new[] { typeof(InteractBuildingRequested) })]
internal static class CorpseLeaveDungeonProbe
{
    private static void Prefix(InteractBuildingRequested msg)
    {
        try
        {
            if (msg == null) return;
            CorpseKeep.ProbeLeave("dungeon-portal", msg.MapType);
            CorpseKeep.LeaveWarning("dungeon-portal", msg.MapType);
        }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-leave", ex); }
    }
}

// Step 3: a portal interaction of the party: the probe line and the warning (only when a dungeon map is left). Not a probe class,
// like the one above: only the probe line asks the group Corpse.
[HarmonyPatch(typeof(PlayerControlEventHandler), nameof(PlayerControlEventHandler.OnEvent), new[] { typeof(InteractPortalRequested) })]
internal static class CorpseLeavePortalProbe
{
    private static void Prefix(InteractPortalRequested msg)
    {
        try
        {
            if (msg == null) return;
            CorpseKeep.ProbeLeave("portal", msg.Map);
            CorpseKeep.LeaveWarning("portal", msg.Map);
        }
        catch (Exception ex) { ApocalypseHooks.Report("corpse-leave", ex); }
    }
}
