using System.Globalization;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// The enemy abilities in game (rules: EnemyAbilityRules), with the option ApocalypseRules.EnemyAbilities.
// After the game processed a hit (EnemyAbilityHitPatch), a landed skill hit of a listed enemy on a
// party member rolls the enemy's ability: bleeding stacks, silence (the target's task is aborted) or
// sleep, applied through the game's tick queue. Damage wakes a sleeper (EnemySleepWakePatch). The
// self-test can force a roll or change chance and stacks (DelversCoreRuntime); the counts of hits and
// procs are for its status line.
internal static class EnemyAbilities
{
    private sealed class TestSetting
    {
        internal double? Chance;
        internal int? Stacks;
        internal bool? Forced;
    }

    // The game's icons the two effects show (ReviveEffectsIcon).
    internal const string SilenceIconKey = "AFFECTER_Panic";
    internal const string SleepIconKey = "AFFECTER_Sleep";
    private const float SkipLogSeconds = 60f;
    // The sleep effect goes through the tick queue; for this long a sleeper may not show it yet.
    private const double PendingSleepSeconds = 1.0;
    private static readonly Dictionary<EnemyAbilityRules.Kind, TestSetting> tests = new();
    private static readonly Dictionary<EnemyAbilityRules.Kind, int> hits = new();
    private static readonly Dictionary<EnemyAbilityRules.Kind, int> procs = new();
    private static readonly Dictionary<string, float> lastSkipLog = new();
    private static readonly Dictionary<IntPtr, long> sleepers = new();
    private static readonly HashSet<IntPtr> waking = new();
    private static readonly System.Random dice = new();
    private static bool unitsReported;
    private static int rollLines;

    internal static void ResetCampaign()
    {
        hits.Clear();
        procs.Clear();
        lastSkipLog.Clear();
        sleepers.Clear();
        waking.Clear();
    }

    // Once: how many units each ability lists, and which of them the game does not know.
    internal static void ReportUnits()
    {
        if (unitsReported) return;
        var sheets = DataSheetManager.Instance;
        if (sheets == null) return;
        unitsReported = true;
        var missing = new List<string>();
        var counts = new Dictionary<EnemyAbilityRules.Kind, int>();
        foreach (var kind in new[] { EnemyAbilityRules.Kind.Bleed, EnemyAbilityRules.Kind.Silence, EnemyAbilityRules.Kind.Sleep })
        {
            foreach (var unit in EnemyAbilityRules.Units(kind))
            {
                counts[kind] = counts.TryGetValue(kind, out var count) ? count + 1 : 1;
                if (sheets.GetUnitData(unit) == null) missing.Add(unit);
            }
        }
        DelversHost.Info($"CORE_ENEMY_ABILITIES bleed={Count(counts, EnemyAbilityRules.Kind.Bleed)} silence={Count(counts, EnemyAbilityRules.Kind.Silence)} sleep={Count(counts, EnemyAbilityRules.Kind.Sleep)} missing={string.Join(",", missing)} bandits={CoreUnitRules.Units.Length}");
    }

    // The forest bandits' bleeding belongs to their fight (a war of attrition):
    // it works wherever they appear, without the option for the other enemies.
    internal static void OnAfterProcess(IExecutionData data)
    {
        if (data == null || !EnemyAbilityRules.MayRoll(ApocalypseRules.EnemyAbilities, CoreUnits.Active)) return;
        var damage = data.TryCast<DamageExecutionData>();
        var attacker = damage?.User?.TryCast<UnitEntity>();
        if (attacker == null || !EnemyAbilityRules.TryGet(attacker.Key, out var trigger)) return;
        if (!EnemyAbilityRules.Rolls(ApocalypseRules.EnemyAbilities, CoreUnits.Active, CoreUnitRules.IsCoreUnit(attacker.Key))) return;
        var target = damage.Target?.TryCast<UnitEntity>();
        if (target == null || target.IsDead) return;
        var skill = damage.BaseExecution?.Context?.Publisher?.TryCast<ActiveSkill_Base>();
        var log = damage.GetLog()?.TryCast<DamageExecutionLog>();
        if (log == null || !EnemyAbilityRules.IsLandedHit(skill != null, (int)damage.AttackType, (int)log.ResultType, log.TotalDamage)
            || !EnemyAbilityRules.Matches(trigger, skill.Key) || !CoreGame.TryMapOfPartyMember(target, out var map)) return;
        var kind = trigger.Kind;
        var affecters = CoreGame.Affecters(target);
        var skip = SkipReason(kind, attacker, affecters);
        if (skip != null)
        {
            LogSkip(kind, skip);
            return;
        }
        var setting = tests.TryGetValue(kind, out var found) ? found : null;
        // The author's Serrated Blade (BanditSerrated): the blade's one bleeding roll is 75 % for 3 stacks instead of the bandits' common one.
        var serrated = EnemyAbilityRules.UsesSerrated(kind, attacker.Key == BanditSerratedRules.BladeUnitKey) ? BanditSerrated.Extra : null;
        if (serrated != null) BanditSerrated.ProbeUnit(attacker);
        var chance = EnemyAbilityRules.ChanceOf(setting?.Chance, serrated, trigger);
        var roll = dice.NextDouble();
        hits[kind] = (hits.TryGetValue(kind, out var hitCount) ? hitCount : 0) + 1;
        var fires = EnemyAbilityRules.Fires(roll, chance, setting?.Forced);
        // Every bleeding roll, a miss too, so the quota is measured per hit, capped per session.
        if (kind == EnemyAbilityRules.Kind.Bleed && rollLines < EnemyAbilityRules.RollLogCap)
        {
            rollLines++;
            DelversHost.Info(EnemyAbilityRules.RollLine(attacker.Key, skill.Key, (int)damage.AttackType, (int)log.ResultType, roll, chance, fires, serrated != null)
                + (rollLines == EnemyAbilityRules.RollLogCap ? " capped=1" : ""));
        }
        if (fires)
        {
            procs[kind] = (procs.TryGetValue(kind, out var procCount) ? procCount : 0) + 1;
            var entity = target.Cast<IEntity>();
            var user = attacker.Cast<IEntity>();
            var stacks = 1;
            var before = 0;
            switch (kind)
            {
                case EnemyAbilityRules.Kind.Bleed:
                    stacks = EnemyAbilityRules.StacksPerProc(setting?.Stacks, serrated, trigger);
                    before = affecters != null && affecters.Has(EnemyAbilityRules.BleedingKey) ? affecters.GetStack(EnemyAbilityRules.BleedingKey) : 0;
                    CoreGame.Apply(map, new AffecterApplyData(entity, EnemyAbilityRules.BleedingKey, stacks, EnemyAbilityRules.BleedSeconds, true, user).Cast<IApplyData>());
                    break;
                case EnemyAbilityRules.Kind.Silence:
                    CoreGame.Apply(map, new TaskAbortData(entity).Cast<IApplyData>());
                    CoreGame.Apply(map, new AffecterApplyData(entity, EnemyAbilityRules.SilenceKey, 1, EnemyAbilityRules.SilenceSeconds, true, user).Cast<IApplyData>());
                    break;
                default:
                    CoreGame.Apply(map, new AffecterApplyData(entity, EnemyAbilityRules.SleepKey, 1, EnemyAbilityRules.SleepSeconds, true, user).Cast<IApplyData>());
                    sleepers[target.Pointer] = Tick();
                    waking.Remove(target.Pointer);
                    break;
            }
            var after = kind == EnemyAbilityRules.Kind.Bleed ? EnemyAbilityRules.StacksAfter(before, stacks) : 1;
            if (kind != EnemyAbilityRules.Kind.Bleed) FirstHints.Request(FirstHintText.EnemyAbilities);
            DelversHost.Info(FormattableString.Invariant($"CORE_ENEMY_ABILITY kind={EnemyAbilityRules.Describe(kind)} attacker={attacker.Key} attackerUnit={CoreGame.GuidOf(attacker):D} target={CoreGame.GuidOf(target):D} stacks={before}->{after} roll={roll:0.000} chance={chance:0.00} stacksPerProc={stacks}") + (serrated != null ? " serrated=1" : ""));
        }
    }

    // Why a hit rolls nothing: an attacker under the abyss rebound makes nobody bleed, a target
    // already silenced or asleep gets no new roll, and stun immunity keeps a target awake.
    private static string SkipReason(EnemyAbilityRules.Kind kind, UnitEntity attacker, AffecterComponent targetAffecters)
    {
        switch (kind)
        {
            case EnemyAbilityRules.Kind.Bleed:
            {
                var own = CoreGame.Affecters(attacker);
                if (own != null)
                {
                    foreach (var key in EnemyAbilityRules.ReboundKeys)
                        if (own.Has(key)) return "abyss";
                }
                return null;
            }
            case EnemyAbilityRules.Kind.Silence:
                if (targetAffecters == null || !targetAffecters.Has(EnemyAbilityRules.SilenceKey)) return null;
                return "silenced";
            default:
                if (targetAffecters == null) return null;
                if (targetAffecters.Has(EnemyAbilityRules.SleepKey)) return "asleep";
                if (!targetAffecters.HasCategory(AffecterCategory.StunImmune)) return null;
                return "immune";
        }
    }

    // At most one line per kind and reason in SkipLogSeconds.
    private static void LogSkip(EnemyAbilityRules.Kind kind, string reason)
    {
        var key = EnemyAbilityRules.Describe(kind) + "/" + reason;
        var now = Time.unscaledTime;
        if (!lastSkipLog.TryGetValue(key, out var last) || !(now - last < SkipLogSeconds))
        {
            lastSkipLog[key] = now;
            DelversHost.Info("CORE_ENEMY_ROLL_SKIPPED kind=" + EnemyAbilityRules.Describe(kind) + " reason=" + reason);
        }
    }

    // Damage on a sleeping party member removes the sleep, once per sleep. A sleeper whose effect is
    // gone is forgotten after PendingSleepSeconds.
    internal static void OnStatusApplied(StatusApplyData data)
    {
        if (sleepers.Count == 0 || data == null || !(data.Amount < 0f) || (data.StatType != StatType.HealthHead && data.StatType != StatType.HealthBody)) return;
        var unit = data.Target?.TryCast<UnitEntity>();
        if (unit == null || !sleepers.TryGetValue(unit.Pointer, out var since)) return;
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null || !affecters.Has(EnemyAbilityRules.SleepKey))
        {
            if (!((Tick() - since) / WoundRules.TicksPerSecond <= PendingSleepSeconds))
            {
                sleepers.Remove(unit.Pointer);
                waking.Remove(unit.Pointer);
            }
        }
        else if (waking.Add(unit.Pointer) && CoreGame.TryMapOfPartyMember(unit, out var map))
        {
            CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), EnemyAbilityRules.SleepKey, -9999, 0f, false).Cast<IApplyData>());
            DelversHost.Info(FormattableString.Invariant($"CORE_ENEMY_SLEEP_WAKE target={CoreGame.GuidOf(unit):D} reason=damage amount={-data.Amount:0.0}"));
        }
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;

    internal static void SetForced(EnemyAbilityRules.Kind kind, bool? forced) => Setting(kind).Forced = forced;

    internal static void SetChance(EnemyAbilityRules.Kind kind, double? chance) => Setting(kind).Chance = chance;

    internal static void SetStacks(EnemyAbilityRules.Kind kind, int? stacks) => Setting(kind).Stacks = stacks;

    // The self-test's status line: the chance and stacks in use, the forced roll, hits and procs.
    internal static string Status(EnemyAbilityRules.Kind kind)
    {
        var setting = tests.TryGetValue(kind, out var found) ? found : null;
        var chance = setting?.Chance;
        var chanceText = chance.HasValue ? chance.GetValueOrDefault().ToString("0.00", CultureInfo.InvariantCulture)
            : kind == EnemyAbilityRules.Kind.Bleed ? WoundRules.TraitBleedChance.ToString("0.00", CultureInfo.InvariantCulture)
            : kind == EnemyAbilityRules.Kind.Sleep ? EnemyAbilityRules.SleepChance.ToString("0.00", CultureInfo.InvariantCulture)
            : "caster";
        var stacks = kind == EnemyAbilityRules.Kind.Bleed ? setting?.Stacks ?? WoundRules.TraitBleedStacks : 1;
        var roll = setting?.Forced switch
        {
            null => "none",
            true => "always",
            false => "never"
        };
        var bandits = EnemyAbilityRules.BanditBleed;
        var banditText = kind == EnemyAbilityRules.Kind.Bleed
            ? $" bandits={bandits.Chance.ToString("0.00", CultureInfo.InvariantCulture)}x{EnemyAbilityRules.StacksOf(bandits)}"
            : "";
        return $"chance={chanceText} stacks={stacks} roll={roll} hits={Count(hits, kind)} procs={Count(procs, kind)}" + banditText;
    }

    private static TestSetting Setting(EnemyAbilityRules.Kind kind)
    {
        if (!tests.TryGetValue(kind, out var setting)) tests[kind] = setting = new TestSetting();
        return setting;
    }

    private static int Count(Dictionary<EnemyAbilityRules.Kind, int> counts, EnemyAbilityRules.Kind kind)
        => counts.TryGetValue(kind, out var count) ? count : 0;
}
