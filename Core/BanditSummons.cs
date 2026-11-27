using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The game side of BanditSummonRules: once per LookSeconds of game time, in the dungeon with bandits.vary on, the summons of Core's
// water mages stand beside their mage out of a fight (SummonControl.Hold) and go back to the game in one. The party's summons stay
// with GroupAiSummonWalk, which keeps these (AddHeld).
internal static class BanditSummons
{
    private static readonly Dictionary<string, (UnitEntity Unit, (int X, int Y) Cell)> places = new(StringComparer.Ordinal);
    private static float nextLook;

    internal static void Reset()
    {
        places.Clear();
        nextLook = 0f;
    }

    // The summons this class holds stay held when the party's look lets its own go.
    internal static void AddHeld(HashSet<string> keep)
    {
        foreach (var guid in places.Keys) keep.Add(guid);
    }

    internal static void OnGameTick()
    {
        // The time first: the settings and the map are asked only when a look is due.
        var now = GameClock.Now;
        if (now < nextLook || !CoreSettings.BanditsVary || CoreGame.CurrentMap != MapType.Dungeon) return;
        nextLook = now + BanditSummonRules.LookSeconds;
        if (CoreGame.InMap is not { } inMap) return;
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead && BanditSummonRules.IsMage(unit.Key) && CoreUnitRules.IsCoreUnit(unit.Key)) Look(unit, inMap, wanted);
        LetGo(wanted);
        SummonControl.Tally(now);
    }

    private static void Look(UnitEntity mage, IInMapDataSupplier inMap, HashSet<string> wanted)
    {
        var mageFights = InCombat(mage);
        var at = inMap.GetLogicalPosition(mage.Guid);
        var cell = (at.x, at.y);
        foreach (var summon in DelversSummons.SummonsOf(mage.Guid.ToString()))
        {
            var unit = summon.TryCast<UnitEntity>();
            if (unit == null || unit.IsDead || !BanditSummonRules.IsSummon(unit.Key) || !BanditSummonRules.Holds(mageFights, InCombat(unit))) continue;
            var guid = unit.Guid.ToString();
            if (!places.TryGetValue(guid, out var place) || !BanditSummonRules.KeepsCell(place.Cell, cell))
            {
                if (FreeBeside(inMap, cell) is not { } free) continue;
                place = (unit, free);
                places[guid] = place;
            }
            SummonControl.Hold(unit, new Vector2Int(place.Cell.X, place.Cell.Y), "bandit-mage");
            wanted.Add(guid);
        }
    }

    // A place taken by nobody else; the mage's own cell and the other summons' places count as taken.
    private static (int X, int Y)? FreeBeside(IInMapDataSupplier inMap, (int X, int Y) mage)
    {
        var taken = new HashSet<(int, int)> { mage };
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead) { var p = CoreGame.Position(unit); taken.Add((p.x, p.y)); }
        foreach (var place in places.Values) taken.Add(place.Cell);
        foreach (var (x, y) in WeakBanditMageRules.BubblingCells(mage, mage, taken))
        {
            var cell = new Vector2Int(x, y);
            if (inMap.GetNearMovablePosition(cell) == cell) return (x, y);
        }
        return null;
    }

    private static void LetGo(HashSet<string> wanted)
    {
        List<string> gone = null;
        foreach (var guid in places.Keys)
            if (!wanted.Contains(guid)) (gone ??= new List<string>()).Add(guid);
        if (gone == null) return;
        foreach (var guid in gone)
        {
            SummonControl.Release(places[guid].Unit.Cast<IEntity>(), "bandit-fight");
            places.Remove(guid);
        }
    }

    private static bool InCombat(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;
}
