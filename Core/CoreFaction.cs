namespace DungeonSettlersDelvers.Core;

// Core's side "bandits" for an entity of the game (CoreFactionRules): a Core unit, or a summon whose owner is one
// (DelversSummons.IsCoreOwned: the summoner field, for units and buildings alike). Whose skill it was stays a question of the
// caster's key (CoreUnitRules.IsCoreUnit); who belongs to the bandits asks here.
internal static class CoreFaction
{
    internal static bool IsBandit(IEntity entity)
    {
        if (entity == null) return false;
        var key = CoreUnitRules.IsCoreUnit(entity.Key);
        // The summoner is read only when the key does not settle it.
        return CoreFactionRules.IsBandit(key, !key && DelversSummons.IsCoreOwned(entity));
    }

    internal static bool IsBandit(UnitEntity unit) => unit != null && IsBandit(unit.Cast<IEntity>());
}
