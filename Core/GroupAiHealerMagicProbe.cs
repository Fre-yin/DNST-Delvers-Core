namespace DungeonSettlersDelvers.Core;

// GroupAiHealerSeed, the magic probe: once per campaign, at the first herbalist's Life Burst check, the combat difficulty
// and the magic attack the herbalist has against her row. Observing only; GroupAiHealerSeed.Reset gives the probe its turn
// again. The seed's Life Burst check calls Run; it asks the group OneShot first (CoreProbes) and writes nothing while it is off.
internal static class GroupAiHealerMagicProbe
{
    internal static void Reset()
    {
        magicProbed = false;
    }

    // 2a (the healer's row said 40, the fight 16, the other bandits 16 vs 6.4):
    // once per campaign at the first herbalist, the combat difficulty, the game's monster affecter for it
    // (AFFECTER_MonsterDifficulty{level}; none for Hard) with its magic and physical attack adjustments, whether she
    // carries it, her row's magic attack and the one she has. CORE_UNIT_MAGIC_PROBE.
    private static bool magicProbed;

    internal static void Run(UnitEntity unit)
    {
        if (!CoreProbes.OneShot) return;
        if (magicProbed || unit == null) return;
        magicProbed = true;
        try
        {
            var level = (int)CoreGame.CombatLevel();
            var key = GroupAiHealerSeedRules.MonsterDifficultyKey(level);
            float? magicAdjust = null, physicalAdjust = null;
            var adjust = key != null ? CombatDataManager.Instance?.GetAffecterData(key)?.GetAdjustStats() : null;
            if (adjust != null)
            {
                if (adjust.ContainsKey(StatType.MagicAttackPowerPerModifier)) magicAdjust = adjust[StatType.MagicAttackPowerPerModifier];
                if (adjust.ContainsKey(StatType.PhysicalAttackPowerPerModifier)) physicalAdjust = adjust[StatType.PhysicalAttackPowerPerModifier];
            }
            var carries = key != null && (CoreGame.Affecters(unit)?.Has(key) ?? false);
            var row = DataSheetManager.Instance?.GetUnitData(unit.Key)?.MagicAttackPower ?? -1f;
            var actual = CoreGame.Stat(unit, StatType.MagicAttackPower);
            DelversHost.Info(GroupAiHealerSeedRules.MagicProbeLine(unit.Guid.ToString(), level, key, carries, magicAdjust, physicalAdjust, row, actual, CoreGame.Stat(unit, StatType.MagicAttackPowerPerModifier)));
            // 2b needs the Normal affecter while the tests play Downfall: every monster affecter's adjustments, one line each.
            foreach (var other in GroupAiHealerSeedRules.MonsterDifficultyKeys)
            {
                var stats = CombatDataManager.Instance?.GetAffecterData(other)?.GetAdjustStats();
                float? m = null, p = null;
                if (stats != null && stats.ContainsKey(StatType.MagicAttackPowerPerModifier)) m = stats[StatType.MagicAttackPowerPerModifier];
                if (stats != null && stats.ContainsKey(StatType.PhysicalAttackPowerPerModifier)) p = stats[StatType.PhysicalAttackPowerPerModifier];
                DelversHost.Info(GroupAiHealerSeedRules.MagicAffecterLine(other, stats != null, m, p));
            }
        }
        catch (Exception ex) { DelversHost.Warning("CORE_UNIT_MAGIC_PROBE_FAILED: " + ex.Message); }
    }
}
