#if BEPINEX
using CombatState = global::Refactor.CombatState;
using CombatStateApplyData = global::Refactor.CombatStateApplyData;
using IApplyData = global::Refactor.IApplyData;
using InMapDataInterface = global::Refactor.InMapDataInterface;
#else
using CombatState = Il2CppRefactor.CombatState;
using CombatStateApplyData = Il2CppRefactor.CombatStateApplyData;
using IApplyData = Il2CppRefactor.IApplyData;
using InMapDataInterface = Il2CppRefactor.InMapDataInterface;
#endif

namespace DungeonSettlersDelvers.Core;

// The party's fight begins together, not when the game notices each member on its own, with the group AI on and a formation in place
// (GroupAiFormation's look): on a foe in sight or a hit on a member in combat (GroupAiFightStartRules), every member whose gate is open
// enters combat through the game's own way, a CombatStateApplyData, once per start. Off the fight the game's state stays as it is.
// NoteTarget: the combat target of a party member follows Core's choice in DecideTarget, not the game's.
internal static class GroupAiFightStart
{
    private static readonly HashSet<Guid> started = new();

    internal static void Reset() => started.Clear();

    // The party's fight ended (GroupAiFormation's look): the next start may take every member again, also while a foe stays in sight
    // across the end (a second group standing by).
    internal static void FightEnded() => started.Clear();

    internal static void Look(List<UnitEntity> party, float now, Func<Guid, float> lastHit)
    {
        if (!GroupAiSettings.Enabled)
        {
            started.Clear();
            return;
        }
        var outside = party.FindAll(unit => !unit.IsDead && !InCombat(unit));
        if (outside.Count == 0) return;
        string foe = null;
        var cause = party.Exists(unit => !unit.IsDead && GroupAiFightStartRules.Hit(InCombat(unit), now, lastHit(CoreGame.GuidOf(unit)))) ? "hit" : null;
        if (cause == null && (foe = FoeInSight(party)) != null) cause = "sight";
        if (cause == null)
        {
            started.Clear();
            return;
        }
        var members = 0;
        foreach (var unit in outside)
        {
            if (!GroupAiGate.OpenFor(unit) || GroupAiExcluded.Has(unit.Guid.ToString()) || !started.Add(CoreGame.GuidOf(unit))) continue;
            CoreGame.Apply(MapType.Dungeon, new CombatStateApplyData(unit.Cast<IEntity>(), CombatState.InCombat, false).Cast<IApplyData>());
            members++;
        }
        if (members > 0) DelversHost.Info(GroupAiFightStartRules.Line(cause, foe, members));
    }

    internal static void NoteTarget(UnitEntity unit, IEntity target)
    {
        var foe = target?.TryCast<UnitEntity>();
        if (foe == null || foe.IsDead || foe.Faction != FactionType.Enemy) return;
        var info = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>());
        if (info == null || info.GetCombatTarget()?.Guid.ToString() == foe.Guid.ToString()) return;
        CoreTargets.SetCombat(unit, info, target);
    }

    private static bool InCombat(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;

    // The first foe within sight of a member that the player can see; the sight is asked last, it is the dearer look.
    private static string FoeInSight(List<UnitEntity> party)
    {
        var inMap = CoreGame.InMap;
        var view = inMap?.TryCast<InMapDataInterface>();
        var alive = inMap?.GetAliveUnitsOnMap();
        if (view == null || alive == null) return null;
        var cells = new List<UnityEngine.Vector2Int>();
        foreach (var unit in party)
            if (!unit.IsDead) cells.Add(CoreGame.Position(unit));
        for (var i = 0; i < alive.Count; i++)
        {
            var foe = alive[i];
            if (foe == null || foe.IsDead || foe.Faction != FactionType.Enemy || GroupAiExcluded.Has(foe.Guid.ToString()) || GroupAiBandits.IsDowned(foe)) continue;
            var at = CoreGame.Position(foe);
            var nearest = int.MaxValue;
            foreach (var cell in cells) nearest = Math.Min(nearest, GroupAiSituation.Distance(cell, at));
            if (GroupAiFightStartRules.InSight(nearest, true) && view.IsEntityInPlayerVision(foe.Guid)) return foe.Key;
        }
        return null;
    }
}
