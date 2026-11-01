using DungeonSettlersDelvers.Core;
using static TestKit;
using Step = DungeonSettlersDelvers.Core.TacticalFormationSleepRules.Step;

// Prioritize Sleep and the formation (TacticalFormationSleepRules): sleep takes a member in the formation
// out, waking brings back only those, a switch by hand while asleep stands, and the sleepers stay in Core's block of the save.
internal static class TacticalSleepSection
{
    internal static void Run()
    {
        Rules();
        Save();
    }

    static void Rules()
    {
        Check(TacticalFormationSleepRules.OnRest(true, true, false) == Step.Leave,
            "tactical sleep: a member in the formation leaves it when it goes to sleep");
        Check(TacticalFormationSleepRules.OnRest(true, false, false) == Step.None,
            "tactical sleep: a member the player switched off before stays off and is no sleeper");
        Check(TacticalFormationSleepRules.OnRest(false, false, true) == Step.Return && TacticalFormationSleepRules.OnRest(false, true, true) == Step.Return,
            "tactical sleep: a sleeper comes back when it wakes");
        Check(TacticalFormationSleepRules.OnRest(false, false, false) == Step.None && TacticalFormationSleepRules.OnRest(true, false, true) == Step.None,
            "tactical sleep: waking leaves a member alone that sleep did not take out");
        Check(TacticalFormationSleepRules.OnLoad(true, false, true) == Step.None && TacticalFormationSleepRules.OnLoad(false, false, true) == Step.Return
                && TacticalFormationSleepRules.OnLoad(true, true, true) == Step.Forget && TacticalFormationSleepRules.OnLoad(false, true, false) == Step.None,
            "tactical sleep: after a load a sleeper still asleep stays out, one awake comes back, one switched on leaves the list");
        Check(TacticalFormationSleepRules.Line("switch", Guid.Empty, Step.Leave, true) == "CORE_TACTICAL_SLEEP cause=switch unit=00000000-0000-0000-0000-000000000000 step=Leave rest=1",
            "tactical sleep: the log line");
    }

    static void Save()
    {
        var campaign = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var data = CoreSaveData.Create(campaign);
        Check(!data.IsFormationSleep(a) && data.FormationSleepers().Count == 0 && !data.Serialize().Contains("formationSleep"),
            "tactical sleep save: no sleepers at first, no field");
        data.SetFormationOff(a, true);
        data.SetFormationSleep(a, true);
        data.SetFormationSleep(b, true);
        var read = CoreSaveData.Parse(data.Serialize(), campaign);
        Check(read.IsFormationSleep(a) && read.IsFormationSleep(b) && read.IsFormationOff(a) && read.FormationSleepers().Count == 2,
            "tactical sleep save: the sleepers come back from the save beside the switched off members");
        var dropped = read.DropFormationUnits(unit => unit != b);
        Check(dropped == 1 && read.IsFormationSleep(a) && !read.IsFormationSleep(b),
            "tactical sleep save: a member who is gone leaves the sleepers");
        read.SetFormationSleep(a, false);
        Check(!read.Serialize().Contains("formationSleep") && read.IsFormationOff(a),
            "tactical sleep save: the last sleeper gone, the field goes; the switch stays its own");
    }
}
