namespace DungeonSettlersDelvers.Core;

// Prioritize Sleep and the formation: a member who goes to sleep leaves the formation, and on waking only the members sleep took out come back. The leader
// too: the formation is saved, so it is as before when the leader returns. A switch by hand while asleep is
// the player's will: that member leaves the sleepers and does not come back by itself.
// Rules without the game; TacticalFormationSleep applies them.
internal static class TacticalFormationSleepRules
{
    internal enum Step { None, Leave, Return, Forget }

    // Prioritize Sleep switched: on takes a member in the formation out (not one the player switched off before), off brings
    // back a member sleep took out.
    internal static Step OnRest(bool restFirst, bool formationOn, bool asleep)
    {
        if (restFirst) return formationOn && !asleep ? Step.Leave : Step.None;
        return asleep ? Step.Return : Step.None;
    }

    // After a load: a sleeper whose Prioritize Sleep is off again comes back; one whose formation is on again leaves the list.
    // INTERPRETATION (not the author's words): the game's bit and Core's list are saved together, so this only mends a save where
    // they differ.
    internal static Step OnLoad(bool restFirst, bool formationOn, bool asleep)
    {
        if (!asleep) return Step.None;
        if (!restFirst) return Step.Return;
        return formationOn ? Step.Forget : Step.None;
    }

    internal static string Line(string cause, Guid unit, Step step, bool restFirst)
        => $"CORE_TACTICAL_SLEEP cause={cause} unit={unit:D} step={step} rest={(restFirst ? 1 : 0)}";
}
