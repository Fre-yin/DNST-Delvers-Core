using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Prioritize Sleep and the formation, the game's side (TacticalFormationSleepRules). The button is the game's BehaviourOption
// RestFirst: a click raises ToggleBehaviourOption and PlayerControlEventHandler.OnEvent sets it on the target or the selection.
// Around that handler Core notes every party member's RestFirst before and acts on the members whose bit changed, so only the
// player's switch counts and nothing runs per tick. The members sleep took out stand in Core's block ("formationSleep") beside
// the switched off ones, both in the save; after a load OnLoaded mends a block that differs from the game's bit.
internal static class TacticalFormationSleep
{
    internal static bool IsRestFirst(UnitEntity unit)
        => CoreGame.Component<EntityInfoComponent>(unit?.Cast<IEntity>())?.IsBehaviourOption(BehaviourOption.RestFirst) == true;

    internal static Dictionary<Guid, bool> Before()
    {
        var result = new Dictionary<Guid, bool>();
        foreach (var unit in TacticalFormation.MapMembers()) result[CoreGame.GuidOf(unit)] = IsRestFirst(unit);
        return result;
    }

    internal static void After(Dictionary<Guid, bool> before)
    {
        var data = CoreSaveStore.Current;
        if (data == null || before == null) return;
        var changed = false;
        foreach (var unit in TacticalFormation.MapMembers())
        {
            var guid = CoreGame.GuidOf(unit);
            var rest = IsRestFirst(unit);
            if (!before.TryGetValue(guid, out var was) || was == rest) continue;
            // The comfort option off: nobody leaves, but a sleeper still comes back.
            var step = TacticalFormationSleepRules.OnRest(rest, TacticalFormation.IsOn(guid), data.IsFormationSleep(guid));
            if (step == TacticalFormationSleepRules.Step.Leave && !CoreSettings.TacticalEnabled) continue;
            changed |= Apply(data, guid, step);
            if (step != TacticalFormationSleepRules.Step.None) DelversHost.Info(TacticalFormationSleepRules.Line("switch", guid, step, rest));
        }
        if (changed) TacticalFormationButton.RefreshAll();
    }

    // After a load, once: the sleepers of the block against the game's bit.
    internal static void OnLoaded()
    {
        var data = CoreSaveStore.Current;
        if (data == null) return;
        foreach (var guid in data.FormationSleepers())
        {
            var unit = CoreGame.FindUnit(guid);
            if (unit == null) continue;
            var rest = IsRestFirst(unit);
            var step = TacticalFormationSleepRules.OnLoad(rest, TacticalFormation.IsOn(guid), true);
            Apply(data, guid, step);
            DelversHost.Info(TacticalFormationSleepRules.Line("load", guid, step, rest));
        }
    }

    private static bool Apply(CoreSaveData data, Guid unit, TacticalFormationSleepRules.Step step)
    {
        switch (step)
        {
            case TacticalFormationSleepRules.Step.Leave:
                data.SetFormationOff(unit, true);
                data.SetFormationSleep(unit, true);
                return true;
            case TacticalFormationSleepRules.Step.Return:
                data.SetFormationOff(unit, false);
                data.SetFormationSleep(unit, false);
                return true;
            case TacticalFormationSleepRules.Step.Forget:
                data.SetFormationSleep(unit, false);
                return false;
            default:
                return false;
        }
    }
}

[HarmonyPatch(typeof(PlayerControlEventHandler), nameof(PlayerControlEventHandler.OnEvent), typeof(ToggleBehaviourOption))]
internal static class TacticalFormationSleepPatch
{
    private static void Prefix(ToggleBehaviourOption msg, out Dictionary<Guid, bool> __state)
    {
        __state = null;
        try
        {
            if (msg != null && msg.Option == BehaviourOption.RestFirst && CoreSaveStore.Current != null) __state = TacticalFormationSleep.Before();
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_SLEEP_FAILED: " + ex.Message); }
    }

    private static void Postfix(Dictionary<Guid, bool> __state)
    {
        if (__state == null) return;
        try { TacticalFormationSleep.After(__state); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_SLEEP_FAILED: " + ex.Message); }
    }
}
