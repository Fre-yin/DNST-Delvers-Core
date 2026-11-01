namespace DungeonSettlersDelvers.Core;

// The camp's boss party wakes when a party member enters the camp's room, for a camp file that has this wake
// (BanditCampWakeRules.WakesOnEnter; the whole room as the trigger is an interpretation, not confirmed; today's camp
// has it off): in combat Apocalypse at once, otherwise after the shorter lead. The camp's box, its floor's seed and whether it
// wakes stand in Core's block (CoreSaveData.CampBox, CampWakes), so the wake also follows a load. It wakes the living Core bandits inside the box through the game's
// own way, a CombatStateApplyData InCombat, each after its own short delay (BanditGroupWake.WakeLater); the first hit on a bandit
// still wakes its comrades there.
// Per tick, only while a camp waits on the shown floor: a look at the party's cells.
internal static class BanditCampWake
{
    private static CoreSaveData readFrom;
    private static (int MinX, int MinY, int MaxX, int MaxY)? box;
    private static int seed;
    private static float enteredAt = -1f;
    private static float lead;
    private static bool done;
    private static (string Unit, int X, int Y) trigger;

    internal static void ResetLifecycle()
    {
        readFrom = null;
        box = null;
        enteredAt = -1f;
        done = false;
    }

    // A camp was furnished: its block changed.
    internal static void Reload() => readFrom = null;

    internal static void OnGameTick()
    {
        var data = CoreSaveStore.Current;
        if (!ReferenceEquals(readFrom, data))
        {
            seed = 0;
            box = data != null && data.CampWakes() ? data.CampBox(out seed) : null;
            readFrom = data;
            enteredAt = -1f;
            done = false;
        }
        if (done || box == null || CoreGame.CurrentMap != MapType.Dungeon || seed != BanditRoomFeatureView.FloorSeed()) return;
        var now = UnityEngine.Time.time;
        if (enteredAt < 0f)
        {
            if (!PartyInside(box.Value, out trigger)) return;
            enteredAt = now;
            lead = BanditCampWakeRules.LeadSeconds(ApocalypseRules.InstantCombat, SurprisedSeconds());
        }
        if (!BanditCampWakeRules.Due(enteredAt, now, lead)) return;
        done = true;
        Wake(box.Value);
    }

    private static bool PartyInside((int MinX, int MinY, int MaxX, int MaxY) camp, out (string Unit, int X, int Y) who)
    {
        who = (null, 0, 0);
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead) continue;
            var at = CoreGame.Position(unit);
            if (!BanditCampWakeRules.Inside(camp, at.x, at.y)) continue;
            who = (unit.Guid.ToString(), at.x, at.y);
            return true;
        }
        return false;
    }

    private static float SurprisedSeconds()
    {
        try { return SystemVar.Combat.SurprisedDuration; }
        catch (Exception) { return BanditCampWakeRules.DefaultSurprisedSeconds; }
    }

    private static void Wake((int MinX, int MinY, int MaxX, int MaxY) camp)
    {
        var inMap = CoreGame.InMap;
        var alive = inMap?.GetAliveUnitsOnMap();
        var woken = 0;
        var nearest = -1;
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || unit.Faction != FactionType.Enemy || !CoreUnitRules.IsCoreUnit(unit.Key)) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            if (!BanditCampWakeRules.Inside(camp, at.x, at.y)) continue;
            var distance = BanditCampWakeRules.Chebyshev(at.x, at.y, trigger.X, trigger.Y);
            if (nearest < 0 || distance < nearest) nearest = distance;
            var state = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() ?? CombatState.Unawared;
            if (state == CombatState.InCombat) continue;
            BanditGroupWake.WakeLater(unit, MapType.Dungeon);
            woken++;
        }
        DelversHost.Info(BanditCampWakeRules.WakeLine(woken, lead, trigger.Unit, trigger.X, trigger.Y, nearest));
    }
}
