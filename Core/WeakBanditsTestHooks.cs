using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the weak bandits' test hooks for the Toolkit's comparison of weak groups with parties of their strength:
// a group of a level set at a cell, built from the same class table as the spawn (WeakBanditRules: skills, weapon
// skill, factor, armour, kit), and the spawn's gate and hash over many seeds. Only in the loaded test campaign,
// internal (the Toolkit reaches them by reflection with NonPublic), so the public surface stays as it is.
public static partial class DelversCoreRuntime
{
    // The cells around the given one where the group's members stand, in this order.
    private static readonly (int Dx, int Dy)[] WeakGroupOffsets = { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1) };

    // level 1 to 3, classes as key ends separated by commas ("Guard,Healer"), cell as "x,y". Returns "ok level= classes= cell=
    // map= units= guids=" or "error=…"; CORE_TEST_WEAK_BANDITS names the same. The game spawns the units in its next tick with the
    // GUIDs given here, on the map shown: a spawn on the dungeon's map while the base is shown threw in the game's occupy data
    // and left no unit.
    internal static string SpawnWeakBanditsForTest(int level, string classes, string cell)
    {
        if (!LoadedTestCampaign()) return "error=no test campaign";
        if (level < 1 || level > WeakBanditRules.Levels) return "error=level " + level;
        var at = cell?.Split(',');
        if (at == null || at.Length != 2 || !int.TryParse(at[0].Trim(), out var x) || !int.TryParse(at[1].Trim(), out var y)) return "error=cell " + (cell ?? "-");
        var suffixes = (classes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (suffixes.Length == 0 || suffixes.Length > WeakGroupOffsets.Length) return "error=classes " + (classes ?? "-");
        var keys = new List<string>();
        foreach (var suffix in suffixes)
        {
            var c = Array.Find(WeakBanditRules.Classes, k => k.Suffix == suffix);
            if (c == null) return "error=class " + suffix;
            keys.Add(WeakBanditRules.KeyOf(level, c));
        }
        var guids = new List<string>();
        MapType map;
        try
        {
            map = CoreGame.CurrentMap;
            for (var i = 0; i < keys.Count; i++)
            {
                var (dx, dy) = WeakGroupOffsets[i];
                var guid = Il2CppSystem.Guid.NewGuid();
                guids.Add(guid.ToString());
                CoreGame.Enqueue(new SpawnUnitRequested(map, keys[i], new Vector2Int(x + dx, y + dy), FactionType.Enemy,
                    new Il2CppSystem.Nullable<Il2CppSystem.Guid>(guid), new Il2CppSystem.Collections.Generic.List<string>(),
                    new Il2CppSystem.Nullable<AnimationType>()).Cast<IEventData>());
            }
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_WEAK_BANDITS_FAILED: " + ex.Message);
            return "error=" + ex.Message;
        }
        var line = $"level={level} classes={string.Join(",", suffixes)} cell={x},{y} map={map} units={string.Join(",", keys)} guids={string.Join(",", guids)}";
        DelversHost.Info("CORE_TEST_WEAK_BANDITS " + line);
        return "ok " + line;
    }

    // The bandits' effect log (CORE_BANDIT_EFFECT, BanditEffectProbe) for the block of bandit skills: on starts it afresh, off
    // stops it. Returns on, off, notest, or nogroup when the probe group Effects (probes.effects) is off, whose patch the log needs.
    internal static string BanditEffectsForTest(bool on)
    {
        if (!LoadedTestCampaign()) return "notest";
        if (on && !CoreProbes.Effects) return "nogroup";
        BanditEffectProbe.Turn(on);
        DelversHost.Info("CORE_TEST_BANDIT_EFFECTS on=" + (on ? 1 : 0));
        return on ? "on" : "off";
    }

    // The loaded test campaign's combat difficulty for the in Apocalypse's real values: 0 Easy to
    // 4 Downfall, -1 puts back the value it had (CombatDifficultyTest). Returns "before= after=" or "error=…".
    internal static string SetCombatDifficultyForTest(int difficulty)
        => LoadedTestCampaign() ? CombatDifficultyTest.Set(difficulty) : "error=no test campaign";

    // Core's bandit difficulty row of level (0 Easy to 4 Downfall, BanditDifficultyRules) on every living Core unit and bandit summon
    // of the shown map, instead of the difficulty row it carries, the game's or Core's; for a camp that spawned before a measurement
    // set the difficulty. Returns "units=" or "error=…"; CORE_BANDIT_DIFFICULTY reason=test per unit.
    internal static string SetBanditDifficultyForTest(int level)
    {
        if (!LoadedTestCampaign()) return "error=no test campaign";
        var units = BanditDifficulty.SetForTest(level);
        return units < 0 ? "error=level " + level + " or no rows" : "units=" + units;
    }

    // The spawn's gate and hash for count seeds from firstSeed on one floor index (0 to 2), over the combat rooms of the game's
    // room table in the forest that roll their monsters on that floor (their table keys stand in for a floor's room ids). One
    // CORE_WEAKBANDIT_SCAN line per seed; returns "seeds= rooms= replaced=" or "error=…". Reads the percent setting, needs no floor.
    internal static string WeakBanditScanForTest(int firstSeed, int count, int floor)
    {
        if (!LoadedTestCampaign()) return "error=no test campaign";
        if (count < 1 || count > 10000) return "error=count " + count;
        var rows = DataSheetManager.Instance?._dungeonRoomTable?.tableDatas;
        if (rows == null) return "error=no room table";
        var rooms = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || (int)row.Region != WeakBanditSpawnRules.Region || (int)row.RoomType != WeakBanditSpawnRules.CombatRoomType) continue;
            if (row.SpawnMonster == null || !row.SpawnMonster.Contains(BanditCampRules.RandomSpawn)) continue;
            if (row.AvailablePhases != null && !row.AvailablePhases.Contains(floor + 1)) continue;
            rooms.Add(row.Key);
        }
        int totalRooms = 0, totalReplaced = 0;
        for (var seed = firstSeed; seed < firstSeed + count; seed++)
        {
            var (n, replaced, groups) = WeakBandits.Scan(seed, floor, rooms);
            totalRooms += n;
            totalReplaced += replaced;
            DelversHost.Info($"CORE_WEAKBANDIT_SCAN seed={seed} floor={floor} rooms={n} replaced={replaced} groups={groups}");
        }
        return $"seeds={count} rooms={totalRooms} replaced={totalReplaced}";
    }
}

// The combat difficulty of the loaded test campaign, set for a measurement (SetCombatDifficultyForTest). The game reads it when it
// spawns an enemy (CampaignStartSetting._combatDifficulty; UnitSpawner.SetEnemyAffecter takes AFFECTER_MonsterDifficulty of it), so
// enemies spawned after it carry the value set; standing ones keep theirs, and so does the party its player difficulty
// affecter, which the game gives at the start and repairs at a load (PlayerCombatDifficultySaveRepair). A save never holds the value
// set: before every save it is put back (BeforeSave, from DelversCampaignSaveGuardPatch), so the iron mode, which the game derives
// from Downfall at start and load, stays as it was. CORE_TEST_COMBAT_DIFFICULTY per change.
internal static class CombatDifficultyTest
{
    private const int Easiest = 0;
    private const int Hardest = 4;
    private static int? original;
    // The campaign setting the value was set on; another campaign's setting forgets it.
    private static IntPtr setOn;

    internal static void ResetLifecycle()
    {
        original = null;
        setOn = IntPtr.Zero;
    }

    internal static string Set(int difficulty)
    {
        var setting = CoreGame.Flow?._campaignSetting?.CampaignStartSetting;
        if (setting == null) return "error=no campaign setting";
        if (setting.Pointer != setOn) ResetLifecycle();
        var before = (int)setting._combatDifficulty;
        if (difficulty == -1)
        {
            if (original is not int back) return $"before={before} after={before}";
            setting._combatDifficulty = (CombatDifficulty)back;
            ResetLifecycle();
            return Line(before, back, "reset");
        }
        if (difficulty < Easiest || difficulty > Hardest) return "error=difficulty " + difficulty;
        original ??= before;
        setOn = setting.Pointer;
        setting._combatDifficulty = (CombatDifficulty)difficulty;
        return Line(before, difficulty, "set");
    }

    internal static void BeforeSave()
    {
        if (original is not int back) return;
        var setting = CoreGame.Flow?._campaignSetting?.CampaignStartSetting;
        if (setting == null || setting.Pointer != setOn)
        {
            ResetLifecycle();
            return;
        }
        var before = (int)setting._combatDifficulty;
        setting._combatDifficulty = (CombatDifficulty)back;
        ResetLifecycle();
        Line(before, back, "save");
    }

    private static string Line(int before, int after, string reason)
    {
        DelversHost.Info($"CORE_TEST_COMBAT_DIFFICULTY before={before} after={after} reason={reason}");
        return $"before={before} after={after}";
    }
}
