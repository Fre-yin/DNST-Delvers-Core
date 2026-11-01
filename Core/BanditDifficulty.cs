using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// Core's bandits carry Core's difficulty row instead of the game's (rules: BanditDifficultyRules).
// - The rows: one per combat difficulty, made from the game's Normal row, added with Core's other effects after the game parses its
//   effect table (ReviveEffectsSheetPatch). Not shown to the player, as the game's.
// - The swap: right after the game gave its row at the spawn (UnitSpawner.SetEnemyAffecter, which hands it to the map's tick handler
//   unforced), a Core unit's game row goes -1 stack and Core's +1, the way the game's skills and traits give effects (Toolkit, camp
//   run the old row 0 stacks, the new 1, health and its maximum change together). Both go into the same unforced queue
//   behind the game's row: the handler keeps forced events in a queue of their own, and a forced -1 sometimes ran before the game's
//   +1 and found nothing to take (game row and Core row both on the unit). Once per spawn; every
//   other enemy keeps the game's. The postfix also leaves the game's other rows of that call (awareness, weather) alone.
// - A bandit's summoned unit (its bubblelings and toad: game keys, faction Enemy) gets the game's row in the same call; the game
//   creates it with its summoner already set (EntityDataContainer.CreateUnit, before SetEnemyAffecter), so the swap finds the
//   owner and gives the summon the row its owner carries.
// - After a load: Core's effects never reach the save (CoreSaveEffects), so every living Core unit without a difficulty row gets
//   Core's back (ReviveCampaign.OnCampaignLoaded). A Core unit that still carries the game's row stood before this rule (a camp
//   already built) and keeps it.
// - Health: the game moves the current health neither when a row lowers its maximum nor when it raises it; it clamps only on a
//   change of the health itself. Behind each swap head and body go to their share of the maximum the rows leave
//   (BanditDifficultyRules.HealthAfter), with a hundredth less so the game clamps too; a unit spawns full, so it stands full at its
//   row's maximum, and a camp switched in a test keeps its share. After a load only the clamp. CORE_BANDIT_DIFFICULTY_HEALTH.
// Without Core's rows (the effect table refused them) the game's row stays. CORE_BANDIT_DIFFICULTY per swap, capped.
internal static class BanditDifficulty
{
    private static bool rowsReady;
    private static int lines;
    private static int healthLines;

    // The queue the game gives its difficulty row in (SetEnemyAffecter: EnqueueEvent with force false).
    private const bool Unforced = false;

    internal static void Register(AffecterSheet sheet)
    {
        if (sheet?._affecterTable == null) return;
        var template = sheet.GetData(BanditDifficultyRules.Template);
        var ready = template != null;
        foreach (var row in BanditDifficultyRules.Table)
        {
            if (sheet.GetData(row.Key) != null) continue;
            var stats = BanditDifficultyRules.StatsOf(row);
            var (ok, added) = CoreAffecterRows.Add(sheet, row.Key, BanditDifficultyRules.Template, "bandit difficulty " + row.Name, null, false,
                template?.MaxStack ?? 1, false, null, false, false);
            if (added != null)
            {
                added.IsPositive = template.IsPositive;
                added.AdjustStatsData = new Il2CppStringArray(stats);
                foreach (var text in stats)
                {
                    var cut = text.LastIndexOf('_');
                    if (Enum.TryParse<StatType>(text[..cut], out var stat)
                        && float.TryParse(text[(cut + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        added.AdjustStats[stat] = value;
                    else ok = false;
                }
            }
            ready &= ok;
            DelversHost.Info(BanditDifficultyRules.RegisteredLine(row.Key, stats.Length == 0 ? "-" : string.Join("|", stats), ok));
        }
        rowsReady = ready;
    }

    private static int Level()
    {
        var setting = CoreGame.Flow?._campaignSetting?.CampaignStartSetting;
        return setting != null ? (int)setting.GetCombatDifficulty() : BanditDifficultyRules.Downfall;
    }

    internal static void OnSpawn(UnitEntity unit, MapType map, bool summon)
    {
        if (!rowsReady) return;
        var level = Level();
        Swap(unit, map, summon ? OwnerLevel(unit, level) : level, BanditDifficultyRules.GameKeyOf(level), summon ? "summon" : "spawn");
    }

    // The level of the Core row the summon's owner carries; fallback when the owner carries none or is not found.
    private static int OwnerLevel(UnitEntity summon, int fallback)
    {
        var owner = DelversSummons.OwnerUnitOf(summon.Cast<IEntity>());
        var affecters = owner == null ? null : CoreGame.Affecters(owner);
        if (affecters == null) return fallback;
        foreach (var row in BanditDifficultyRules.Table)
            if (affecters.Has(row.Key)) return row.Level;
        return fallback;
    }

    // The row the unit carries -1 (the game's, or another of Core's in a test), Core's row of the level +1; removeKey null: nothing
    // to take away (Hard).
    private static void Swap(UnitEntity unit, MapType map, int level, string removeKey, string reason)
    {
        var own = BanditDifficultyRules.RowOf(level);
        if (own == null) return;
        var entity = unit.Cast<IEntity>();
        // What the row taken away gives now: nothing while the game's row still waits in the queue (a spawn).
        var removed = removeKey != null && CoreGame.Affecters(unit)?.Has(removeKey) == true ? HealthPercentOf(removeKey) : 0f;
        if (removeKey != null)
            CoreGame.Apply(map, new AffecterApplyData(entity, removeKey, -1, 0f, false, null).Cast<IApplyData>(), Unforced);
        CoreGame.Apply(map, new AffecterApplyData(entity, own.Key, 1, 0f, false, null).Cast<IApplyData>(), Unforced);
        KeepHealthShare(unit, map, reason, removed, own.Health);
        if (lines++ < BanditDifficultyRules.LogCap)
            DelversHost.Info(BanditDifficultyRules.SwapLine(unit.Key, reason, level, removeKey, own.Key, map.ToString()));
    }

    // Behind the rows, in the same queue: head and body go to their share of the maximum the rows leave (HealthAfter), plus the
    // touch, so the game also clamps whatever its own row did meanwhile. removed and added: the health percent of the row taken away
    // and of Core's row. The line names the values before the queue runs and the ones Core sends them to.
    private static void KeepHealthShare(UnitEntity unit, MapType map, string reason, float removed, float added)
    {
        var total = CoreGame.Stat(unit, StatType.MaxHealthTotalPerModifier);
        var head = Part(unit, map, StatType.HealthHead, StatType.MaxHealthHead, total, removed, added);
        var body = Part(unit, map, StatType.HealthBody, StatType.MaxHealthBody, total, removed, added);
        if (healthLines++ < BanditDifficultyRules.LogCap)
            DelversHost.Info(BanditDifficultyRules.HealthLine(unit.Key, reason, head.Now, head.Max, head.To, body.Now, body.Max, body.To));
    }

    private static (float Now, float Max, float To) Part(UnitEntity unit, MapType map, StatType value, StatType max, float total, float removed, float added)
    {
        var now = CoreGame.Stat(unit, value);
        var top = CoreGame.Stat(unit, max);
        var to = BanditDifficultyRules.HealthAfter(now, top, total, removed, added);
        CoreGame.Apply(map, new StatusApplyData(unit.Cast<IEntity>(), value, to - now + BanditDifficultyRules.HealthTouch, StatusApplyPersistence.Normal).Cast<IApplyData>(), Unforced);
        return (now, top, to);
    }

    // The MaxHealthTotalPerModifier a row of the effect table gives, 0 without one.
    private static float HealthPercentOf(string key)
    {
        var stats = DataSheetManager.Instance?.GetAffecterData(key)?.AdjustStats;
        return stats != null && stats.TryGetValue(StatType.MaxHealthTotalPerModifier, out var value) ? value : 0f;
    }

    // After a load: Core's row back on every living Core unit and bandit summon that has no difficulty row (a summon its owner's);
    // one with the game's keeps it.
    internal static void RestoreLoaded()
    {
        if (!rowsReady) return;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        var level = Level();
        int units = 0, given = 0, kept = 0;
        foreach (var pair in maps)
        {
            var alive = pair.Value?.GetAliveUnitsOnMap();
            for (var i = 0; alive != null && i < alive.Count; i++)
            {
                var unit = alive[i];
                if (unit == null || !CoreFaction.IsBandit(unit)) continue;
                units++;
                var affecters = CoreGame.Affecters(unit);
                if (affecters == null || CarriesOwn(affecters)) continue;
                if (CarriesGame(affecters))
                {
                    kept++;
                    continue;
                }
                var own = BanditDifficultyRules.RowOf(CoreUnitRules.IsCoreUnit(unit.Key) ? level : OwnerLevel(unit, level));
                if (own == null) continue;
                CoreGame.Apply(pair.Key, new AffecterApplyData(unit.Cast<IEntity>(), own.Key, 1, 0f, false, null).Cast<IApplyData>(), Unforced);
                // The game loads health as it was saved, under the same row (a campaign's combat difficulty never changes), so
                // the share stands once the row is back; only what the load put above the row's maximum is clamped.
                KeepHealthShare(unit, pair.Key, "restore", 0f, 0f);
                given++;
            }
        }
        if (units > 0) DelversHost.Info(BanditDifficultyRules.RestoreLine(units, given, kept, level));
    }

    // Test only (SetBanditDifficultyForTest): every living Core unit and bandit summon of the shown map takes the row of level instead of the
    // difficulty row it carries, the game's or Core's. Returns the number of units.
    internal static int SetForTest(int level)
    {
        if (!rowsReady || BanditDifficultyRules.RowOf(level) == null) return -1;
        var alive = CoreGame.InMap?.GetAliveUnitsOnMap();
        var map = CoreGame.CurrentMap;
        var count = 0;
        for (var i = 0; alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || !CoreFaction.IsBandit(unit)) continue;
            var affecters = CoreGame.Affecters(unit);
            if (affecters == null) continue;
            var carried = Carried(affecters);
            if (carried == BanditDifficultyRules.RowOf(level).Key) continue;
            Swap(unit, map, level, carried, "test");
            count++;
        }
        return count;
    }

    private static bool CarriesOwn(AffecterComponent affecters)
    {
        foreach (var row in BanditDifficultyRules.Table)
            if (affecters.Has(row.Key)) return true;
        return false;
    }

    private static bool CarriesGame(AffecterComponent affecters)
    {
        foreach (var key in GroupAiHealerSeedRules.MonsterDifficultyKeys)
            if (affecters.Has(key)) return true;
        return false;
    }

    // The difficulty row a unit carries, Core's first; null for none.
    private static string Carried(AffecterComponent affecters)
    {
        foreach (var row in BanditDifficultyRules.Table)
            if (affecters.Has(row.Key)) return row.Key;
        foreach (var key in GroupAiHealerSeedRules.MonsterDifficultyKeys)
            if (affecters.Has(key)) return key;
        return null;
    }

    internal static void ResetLifecycle()
    {
        lines = 0;
        healthLines = 0;
    }
}

// Only Core's units and their summons: a key compare first, then the summoner field; the game's enemies go on untouched.
[HarmonyPatch(typeof(UnitSpawner), "SetEnemyAffecter")]
internal static class BanditDifficultyPatch
{
    private static bool reported;

    private static void Postfix(UnitEntity entity, MapType map)
    {
        if (entity == null) return;
        try
        {
            var core = CoreUnitRules.IsCoreUnit(entity.Key);
            if (!core && !DelversSummons.IsCoreOwned(entity.Cast<IEntity>())) return;
            if (core) WeakBandits.NoteSpawn(entity);
            BanditDifficulty.OnSpawn(entity, map, !core);
        }
        catch (Exception ex)
        {
            if (reported) return;
            reported = true;
            DelversHost.Warning("CORE_BANDIT_DIFFICULTY_FAILED (the bandit keeps the game's row): " + ex.Message);
        }
    }
}
