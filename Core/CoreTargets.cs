namespace DungeonSettlersDelvers.Core;

// Where Core itself sets a unit's target (BanditDowned, BanditProtect, BanditTaunt): the game's own setter, then the unit's
// summons take the new target at once (SummonFollowTarget.OnOwnerTarget), an event instead of a frame of its own. A target the
// game sets from outside (a taunt) reaches the summons at their next decision.
internal static class CoreTargets
{
    internal static void SetCombat(UnitEntity unit, CombatInfoComponent info, IEntity target)
    {
        if (info == null) return;
        info.SetCombatTarget(target);
        SummonFollowTarget.OnOwnerTarget(unit);
    }

    internal static void SetForced(UnitEntity unit, CombatInfoComponent info, IEntity target)
    {
        if (info == null) return;
        info.SetForceAttackTarget(target);
        SummonFollowTarget.OnOwnerTarget(unit);
    }
}
