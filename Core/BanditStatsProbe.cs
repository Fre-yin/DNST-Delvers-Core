namespace DungeonSettlersDelvers.Core;

// Bandit health and armour, step 0: what
// the game's data says, so the numbers are measured, not guessed. Once a session: AdjustStatsData, AdjustStatsPerStackData and
// FloatValues of the four difficulty monster affecters and of AFFECTER_EliteMonster (MaxHealthTotalPerModifier, stat 80, among
// them), and the row health of every unit with at least BossHealth (the region bosses, UNIT_GiantStoneGolem first). Once per
// role: health, the resistances, dodge and guard of the first bandit that decides, with hit chance and damage dealt bonus (a
// blade that used Serrated Blade before the buff fix carried -30 / -50 each; shows whether that came along in a save).
// CORE_BANDIT_STATS_PROBE. The bandits' decision code calls both entry points; each asks the group OneShot first (CoreProbes) and
// writes nothing while it is off.
internal static class BanditStatsProbe
{
    internal const float BossHealth = 10000f;
    private static bool staticDone;
    private static readonly HashSet<string> rolesDone = new(StringComparer.Ordinal);

    private static readonly string[] Affecters =
    {
        "AFFECTER_MonsterDifficultyEasy", "AFFECTER_MonsterDifficultyNormal", "AFFECTER_MonsterDifficultyVeryHard",
        "AFFECTER_MonsterDifficultyDownfall", CoreUnitRules.EliteTrait,
        BanditDifficultyRules.KeyPrefix + "Easy", BanditDifficultyRules.KeyPrefix + "Normal", BanditDifficultyRules.KeyPrefix + "Hard",
        BanditDifficultyRules.KeyPrefix + "VeryHard", BanditDifficultyRules.KeyPrefix + "Downfall"
    };

    internal static void Static()
    {
        if (!CoreProbes.OneShot) return;
        if (staticDone) return;
        var sheet = DataSheetManager.Instance;
        if (sheet == null) return;
        staticDone = true;
        foreach (var key in Affecters)
        {
            var data = sheet.GetAffecterData(key);
            DelversHost.Info("CORE_BANDIT_STATS_PROBE affecter=" + key + (data == null ? " missing=1"
                : " adjust=" + Join(data.AdjustStatsData) + " perStack=" + Join(data.AdjustStatsPerStackData) + " values=" + Join(data.FloatValues)));
        }
        var table = sheet._unit?._unitTable;
        if (table == null) return;
        var e = table.GetEnumerator();
        var lines = 0;
        while (e.MoveNext() && lines < 60)
        {
            var row = e.Current.Value;
            if (row == null || row.MaxHealthTotal < BossHealth) continue;
            lines++;
            DelversHost.Info(FormattableString.Invariant($"CORE_BANDIT_STATS_PROBE unit={row.Key} health={row.MaxHealthTotal:0} faction={row.Faction}"));
        }
    }

    // The first bandit of each role that decides: its live values (difficulty, elite and armour already applied).
    internal static void Unit(string role, UnitEntity unit)
    {
        if (!CoreProbes.OneShot) return;
        if (unit == null || role == null || rolesDone.Count >= 8 || !rolesDone.Add(role)) return;
        DelversHost.Info(FormattableString.Invariant(
            $"CORE_BANDIT_STATS_PROBE role={role} health={CoreGame.Stat(unit, StatType.MaxHealthTotal):0} healthMod={CoreGame.Stat(unit, StatType.MaxHealthTotalPerModifier):0.#} phys={CoreGame.Stat(unit, StatType.PhysicalResistance):0.#} magic={CoreGame.Stat(unit, StatType.MagicResistance):0.#} dodge={CoreGame.Stat(unit, StatType.DodgeChance):0.#} guard={CoreGame.Stat(unit, StatType.GuardDamageReduction):0.#} hit={CoreGame.Stat(unit, StatType.HitChance):0.#} dealt={CoreGame.Stat(unit, StatType.DamageDealtBonus):0.#}"));
    }

    // (balance only on measured values): once per bandit key, at its first decision (the fight's start), what its
    // hits reckon from: the row's physical and magic attack, the live values with the difficulty's and the gear's share already in, the
    // modifiers (stats 81 and 83), crit chance and crit damage, Core's template and damage factor of the key (CoreUnitRules), the resistances, and which of the difficulty monster affecters it
    // carries, Core's bandit difficulty rows among them (BanditDifficulty); with the speed, dodge and move modifiers, so a swap
    // shows that none of Downfall's stays. CORE_BANDIT_ATTACK_PROBE, capped.
    private static readonly HashSet<string> keysDone = new(StringComparer.Ordinal);
    private const int KeysCap = 40;

    internal static void Attack(UnitEntity unit)
    {
        if (!CoreProbes.OneShot || unit == null || keysDone.Count >= KeysCap || !keysDone.Add(unit.Key ?? "")) return;
        var row = DataSheetManager.Instance?.GetUnitData(unit.Key);
        CoreUnitRules.TryGet(unit.Key, out var definition);
        var affecters = CoreGame.Affecters(unit);
        var carried = new List<string>();
        foreach (var key in Affecters)
            if (affecters != null && affecters.Has(key)) carried.Add(key.Replace("AFFECTER_", ""));
        DelversHost.Info(FormattableString.Invariant(
            $"CORE_BANDIT_ATTACK_PROBE key={unit.Key} template={definition?.Template ?? "-"} damageFactor={definition?.DamageFactor ?? -1f:0.###} rowPatk={row?.PhysicalAttackPower ?? -1f:0.#} rowMatk={row?.MagicAttackPower ?? -1f:0.#} patk={CoreGame.Stat(unit, StatType.PhysicalAttackPower):0.#} patkMod={CoreGame.Stat(unit, StatType.PhysicalAttackPowerPerModifier):0.#} matk={CoreGame.Stat(unit, StatType.MagicAttackPower):0.#} matkMod={CoreGame.Stat(unit, StatType.MagicAttackPowerPerModifier):0.#} crit={CoreGame.Stat(unit, StatType.CriticalChance):0.#} critDmg={CoreGame.Stat(unit, StatType.CriticalDamageBonus):0.#} aspdMod={CoreGame.Stat(unit, StatType.AttackSpeedPerModifier):0.#} moveMod={CoreGame.Stat(unit, StatType.MovementSpeedPerModifier):0.#} dodge={CoreGame.Stat(unit, StatType.DodgeChance):0.#} healthMod={CoreGame.Stat(unit, StatType.MaxHealthTotalPerModifier):0.#} phys={CoreGame.Stat(unit, StatType.PhysicalResistance):0.#} magic={CoreGame.Stat(unit, StatType.MagicResistance):0.#} affecters={(carried.Count == 0 ? "-" : string.Join("+", carried))}"));
    }

    private static string Join(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray values)
    {
        if (values == null || values.Length == 0) return "-";
        var parts = new List<string>();
        for (var i = 0; i < values.Length; i++) parts.Add(values[i]);
        return string.Join(",", parts);
    }

    private static string Join(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> values)
    {
        if (values == null || values.Length == 0) return "-";
        var parts = new List<string>();
        for (var i = 0; i < values.Length; i++) parts.Add(values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join("/", parts);
    }
}
