#if BEPINEX
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
#else
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
#endif

namespace DungeonSettlersDelvers.Core;

// The group AI only steers a party member the player left on auto attack: not while it waits for
// an order, and only with the game's auto attack option set.
internal static class GroupAiGate
{
    internal static bool Open(DecidePlayerCombatBehaviour decide)
    {
        var info = decide._entityInfoReader;
        return info != null && !info.IsBehaviourOption(BehaviourOption.WaitForOrder) && info.IsBehaviourOption(BehaviourOption.AutoAttack);
    }

    // The same gate for a unit outside its decision (an enemy area seen while it waits for its next one).
    internal static bool OpenFor(UnitEntity unit)
    {
        var info = unit == null ? null : CoreGame.Component<EntityInfoComponent>(unit.Cast<IEntity>());
        return info != null && !info.IsBehaviourOption(BehaviourOption.WaitForOrder) && info.IsBehaviourOption(BehaviourOption.AutoAttack);
    }
}
