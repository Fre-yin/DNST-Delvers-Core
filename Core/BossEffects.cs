using HarmonyLib;
#if BEPINEX
using HealSimulator = global::Refactor.Combat.ExecutionProcess.HealSimulator;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageProperty = global::Refactor.Combat.DamageProperty;
using Execution = global::Refactor.Combat.Execution;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using SummonedBuildingResolver = global::Refactor.Combat.AffecterRelated.SummonedBuildingResolver;
#else
using HealSimulator = Il2CppRefactor.Combat.ExecutionProcess.HealSimulator;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageProperty = Il2CppRefactor.Combat.DamageProperty;
using Execution = Il2CppRefactor.Combat.Execution;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using SummonedBuildingResolver = Il2CppRefactor.Combat.AffecterRelated.SummonedBuildingResolver;
#endif

namespace DungeonSettlersDelvers.Core;

// The boss parts' effects in the game (BossEffectRules). Who wears a part is read from its equipment effect on the unit.
// - Blood Warden: every BloodGuardTicks game ticks the bearers in combat look at their party (BossEffectRules); a shield
//   given is followed every tick until it ends, its rest heals the member. The use comes back out of combat.
// - Bloodletting: a landed hit of the greatsword's bearer adds bleeding on the target, beside the bandits' bleeding in the
//   same hook (EnemyAbilityHitPatch). The stacks it added are counted per target (BossEffectRules.BloodLedger); each tick of
//   that target's bleeding, in the same hook, heals the bearer its share as the game's heal execution.
// - Overgrowth: the health changes the game's heal execution makes for a heal of the staff's bearer (HealSimulator.
//   Simulate; skills and Lifebloom go this way) heal 20 % more and are marked, so the bleeding wound eases its reduction
//   for them (WoundHealPatch, Wounds.Heal). Her own heals and those of what she summons, as the Strange Seed (yes; the
//   summoner through SummonedBuildingResolver).
//   CalculateValue is not reached for these heals (ease 19 times, no bonus line).
internal static class BossEffects
{
    // Log lines per effect name, so frequent heals leave room for the rarer effects.
    private const int LineCapPerName = 60;
    private const int MarkCap = 256;
    private static readonly HashSet<IntPtr> staffMarks = new();
    private static readonly Dictionary<string, int> lines = new(StringComparer.Ordinal);

    internal static void ResetLifecycle() => staffMarks.Clear();

    private const int BloodGuardTicks = 10;
    private static int tick;
    private static readonly HashSet<Guid> guardUsed = new();
    private static readonly List<(UnitEntity Target, UnitEntity Bearer, MapType Map, long StartTick, long EndTick, int Stacks, bool Read)> shields = new();

    private static readonly BossEffectRules.BloodLedger bloodLedger = new();
    private static readonly BossEffectRules.HealBudget healBudget = new();
    private static IExecutionPublisher swordPublisher;
    // What Bloodletting healed per bearer since the last sum line (bloodletting-heal-sum, every HealSumSeconds of game time).
    private const double HealSumSeconds = 10.0;
    private static readonly Dictionary<string, (double Heal, int Ticks)> healSums = new(StringComparer.Ordinal);
    private static long healSumFrom = -1;

    internal static void ResetCampaign()
    {
        guardUsed.Clear();
        shields.Clear();
        bloodLedger.Clear();
        healBudget.Clear();
        healSums.Clear();
        healSumFrom = -1;
    }

    internal static void OnGameTick()
    {
        FollowShields();
        FlushHealSums();
        if (++tick % BloodGuardTicks != 0) return;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        foreach (var pair in maps)
        {
            var members = pair.Value?.GetAllPlayerUnits(false);
            if (members == null) continue;
            for (var i = 0; i < members.Count; i++)
            {
                var bearer = members[i];
                if (bearer == null || bearer.IsDead || !Wears(bearer.Cast<IEntity>(), BossItemRules.ShieldKey)) continue;
                var guid = CoreGame.GuidOf(bearer);
                if (!InCombat(bearer))
                {
                    guardUsed.Remove(guid);
                    continue;
                }
                if (guardUsed.Contains(guid)) continue;
                var candidates = new List<(string Guid, bool Alive, double Share)>();
                for (var j = 0; j < members.Count; j++)
                {
                    var member = members[j];
                    if (member == null) continue;
                    var max = CoreGame.Stat(member, StatType.MaxHealthTotal);
                    candidates.Add((CoreGame.GuidOf(member).ToString("D"), !member.IsDead,
                        max > 0f ? CoreGame.Stat(member, StatType.HealthTotal) / max : double.NaN));
                }
                var target = BossEffectRules.BloodGuardTarget(guid.ToString("D"), candidates);
                if (target == null) continue;
                for (var j = 0; j < members.Count; j++)
                    if (members[j] != null && CoreGame.GuidOf(members[j]).ToString("D") == target)
                    {
                        Guard(pair.Key, bearer, members[j]);
                        guardUsed.Add(guid);
                        break;
                    }
            }
        }
    }

    private static bool InCombat(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat;

    private static long GameTick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;

    private static void Guard(MapType map, UnitEntity bearer, UnitEntity target)
    {
        var (head, body, shield) = BossEffectRules.Sacrifice(CoreGame.Stat(bearer, StatType.HealthHead), CoreGame.Stat(bearer, StatType.HealthBody));
        var self = bearer.Cast<IEntity>();
        var barrierBefore = CoreGame.Stat(target, StatType.Barrier);
        if (head > 0) CoreGame.Apply(map, new StatusApplyData(self, self, StatType.HealthHead, (float)-head).Cast<IApplyData>());
        if (body > 0) CoreGame.Apply(map, new StatusApplyData(self, self, StatType.HealthBody, (float)-body).Cast<IApplyData>());
        var start = GameTick();
        var raise = 0f;
        if (shield > 0)
        {
            var member = target.Cast<IEntity>();
            raise = BossEffectRules.CapRaisePercent(CoreGame.Stat(target, StatType.MaxHealthTotal), CoreGame.Stat(target, StatType.MaxHealthTotalPerModifier),
                ShieldStacks(target), shield);
            if (raise > 0f) CoreGame.Apply(map, new StatusApplyData(member, member, StatType.MaxHealthTotalPerModifier, raise).Cast<IApplyData>());
            CoreGame.Apply(map, new AffecterApplyData(member, BossEffectRules.BloodGuardKey, shield, BossEffectRules.ShieldSeconds, false, null).Cast<IApplyData>());
            if (raise > 0f) CoreGame.Apply(map, new StatusApplyData(member, member, StatType.MaxHealthTotalPerModifier, -raise).Cast<IApplyData>());
            shields.Add((target, bearer, map, start, start + (long)Math.Round(BossEffectRules.ShieldSeconds * WoundRules.TicksPerSecond), 0, false));
        }
        // barrier = the target's Barrier status (10), which the game needs above 0 for a shield to take hits.
        Line("blood-warden", self, FormattableString.Invariant(
            $"target={CoreGame.GuidOf(target):D} sacrificeHead={head:0.#} sacrificeBody={body:0.#} requested={shield} barrierBefore={barrierBefore:0.#} barrierAfter={CoreGame.Stat(target, StatType.Barrier):0.#} capRaise={raise:0.#} start={start}"));
    }

    // The Blood Warden's shield is used up before the others. The game takes a hit from the shields in the
    // order of the unit's list of category Barrier (CalculateApplyDamageToUnitShield over GetAffectersBy(801), which is
    // AffecterComponent._affecterByCategories[801]); a key added comes last, so a fresh Blood Warden went last. Once, when Core
    // first sees it on the member, its key moves to the front; shields added later come behind it. blood-warden-first.
    private static void PutFirst(UnitEntity target, AffecterComponent affecters)
    {
        var lists = affecters._affecterByCategories;
        if (lists == null || !lists.TryGetValue(AffecterCategory.Barrier, out var keys) || keys == null) return;
        var at = keys.IndexOf(BossEffectRules.BloodGuardKey);
        // Already in front (a second shield given to the same member): nothing to move, no line.
        if (at <= 0) return;
        keys.RemoveAt(at);
        keys.Insert(0, BossEffectRules.BloodGuardKey);
        Line("blood-warden-first", target.Cast<IEntity>(), FormattableString.Invariant($"index={at}->0 shields={keys.Count}"));
    }

    // The stacks of all shields on a unit (category Barrier, 801), as the game's cap adds them up.
    internal static float ShieldStacks(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        var keys = affecters?.GetAllByCategory(AffecterCategory.Barrier);
        var sum = 0f;
        for (var i = 0; keys != null && i < keys.Count; i++) sum += affecters.GetStack(keys[i]);
        return sum;
    }

    // Each shield given, every tick: its own stacks while it lasts; once gone at its end, half the rest heals the member
    // (BossEffectRules.ShieldRest).
    private static void FollowShields()
    {
        if (shields.Count == 0) return;
        var now = GameTick();
        for (var i = shields.Count - 1; i >= 0; i--)
        {
            var (target, bearer, map, startTick, endTick, last, read) = shields[i];
            var affecters = target != null && !target.IsDead ? CoreGame.Affecters(target) : null;
            var on = affecters != null && affecters.Has(BossEffectRules.BloodGuardKey);
            if (on)
            {
                if (!read) PutFirst(target, affecters);
                var stacks = affecters.GetStack(BossEffectRules.BloodGuardKey);
                // applied: the stacks the game really put on, at the first read after the shield was given.
                Line(read ? "blood-warden-read" : "blood-warden-applied", target.Cast<IEntity>(), FormattableString.Invariant(
                    $"tick={now} stacks={stacks} barrier={CoreGame.Stat(target, StatType.Barrier):0.#}"));
                shields[i] = (target, bearer, map, startTick, endTick, stacks, true);
                continue;
            }
            if (target != null && !target.IsDead && BossEffectRules.StillPending(read, now, startTick)) continue;
            shields.RemoveAt(i);
            var rest = BossEffectRules.ShieldRest(now, endTick, last);
            var heal = BossEffectRules.RestHeal(rest);
            if (heal > 0 && target != null && !target.IsDead)
                CoreGame.Apply(map, new StatusApplyData(target.Cast<IEntity>(), bearer?.Cast<IEntity>(), StatType.HealthBody, (float)heal).Cast<IApplyData>());
            Line("blood-warden-end", target?.Cast<IEntity>(), FormattableString.Invariant(
                $"start={startTick} end={now} planned={endTick} reason={BossEffectRules.ShieldEndReason(now, endTick)} last={last} rest={rest} heal={heal:0.#}"));
        }
    }

    // A processed execution: a landed hit of the greatsword's bearer adds its bleeding (BossEffectRules.BloodlettingStacks); a
    // tick of the game's bleeding heals the bearers who put stacks there. The tick is told by its publisher, the bleeding's
    // passive (key AFFECTER_Bleeding), not by a missing user: the first check in game healed nobody that way.
    internal static void OnHit(IExecutionData data)
    {
        var damage = data?.TryCast<DamageExecutionData>();
        if (damage == null) return;
        if (damage.BaseExecution?.Context?.Publisher?.Key == EnemyAbilityRules.BleedingKey)
        {
            OnBleedTick(damage);
            return;
        }
        var attacker = damage.User;
        if (attacker == null) return;
        if (!Wears(attacker, BossItemRules.GreatSwordKey)) return;
        var target = damage.Target?.TryCast<UnitEntity>();
        if (target == null || target.IsDead) return;
        var skill = damage.BaseExecution?.Context?.Publisher?.TryCast<ActiveSkill_Base>();
        var log = damage.GetLog()?.TryCast<DamageExecutionLog>();
        if (log == null || !EnemyAbilityRules.IsLandedHit(skill != null, (int)damage.AttackType, (int)log.ResultType, log.TotalDamage)) return;
        var partyMember = CoreGame.TryMapOfPartyMember(target, out var map);
        if (!partyMember && !CoreGame.TryMapOfUnit(target, out map)) return;
        var values = DataSheetManager.Instance?.GetAffecterData(EnemyAbilityRules.BleedingKey)?.FloatValues;
        var perStack = values != null && values.Length > 0 ? values[0] : 0f;
        var stacks = BossEffectRules.BloodlettingStacks(log.TotalDamage, perStack, EnemyAbilityRules.BleedSeconds);
        if (stacks <= 0) return;
        var before = BleedStacks(target);
        CoreGame.Apply(map, new AffecterApplyData(target.Cast<IEntity>(), EnemyAbilityRules.BleedingKey, stacks, EnemyAbilityRules.BleedSeconds, true, attacker).Cast<IApplyData>());
        // The game applies through its queue, so the stacks after are the rule's (capped at 99), as for the bandits' bleeding.
        // A party member's bleeding feeds its wound and is not counted (BossEffectRules.BloodLedger).
        if (!partyMember)
            bloodLedger.Add(CoreGame.GuidOf(target).ToString("D"), attacker.Guid.ToString("D"), before, EnemyAbilityRules.StacksAfter(before, stacks));
        Line("bloodletting", attacker, FormattableString.Invariant(
            $"target={CoreGame.GuidOf(target):D} skill={damage.BaseExecution?.Context?.Publisher?.Key ?? "-"} damage={log.TotalDamage:0.#} stacks={stacks} perStack={perStack:0.##} before={before}"));
    }

    // One tick of a target's bleeding: each bearer still wearing the greatsword heals its share (BossEffectRules.BloodLedger).
    private static void OnBleedTick(DamageExecutionData damage)
    {
        if (bloodLedger.Count == 0) return;
        var target = damage.Target?.TryCast<UnitEntity>();
        if (target == null) return;
        var id = CoreGame.GuidOf(target).ToString("D");
        var tickDamage = damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f;
        var stacks = BleedStacks(target);
        var heals = bloodLedger.Heals(id, stacks, tickDamage);
        Line("bloodletting-tick", target.Cast<IEntity>(), FormattableString.Invariant(
            $"user={(damage.User != null ? damage.User.Guid.ToString("D") : "-")} tick={tickDamage:0.#} stacks={stacks} heals={heals.Count}"));
        if (target.IsDead) bloodLedger.Forget(id);
        foreach (var (bearerId, heal) in heals)
        {
            var bearer = Guid.TryParse(bearerId, out var guid) ? CoreGame.FindUnit(guid) : null;
            if (bearer == null || bearer.IsDead || !Wears(bearer.Cast<IEntity>(), BossItemRules.GreatSwordKey)) continue;
            if (!CoreGame.TryMapOfPartyMember(bearer, out var map) && !CoreGame.TryMapOfUnit(bearer, out map)) continue;
            var granted = healBudget.Take(bearerId, (long)(GameTick() / WoundRules.TicksPerSecond), heal);
            if (!(granted > 0)) continue;
            var via = HealBearer(map, bearer, granted) ? "execution" : "direct";
            var sum = healSums.TryGetValue(bearerId, out var before) ? before : (0.0, 0);
            healSums[bearerId] = (sum.Item1 + granted, sum.Item2 + 1);
            if (healSumFrom < 0) healSumFrom = GameTick();
            Line("bloodletting-heal", bearer.Cast<IEntity>(), FormattableString.Invariant(
                $"target={id} tick={tickDamage:0.#} stacks={stacks} own={bloodLedger.Stacks(id, bearerId):0.##} asked={heal:0.#} heal={granted:0.#} via={via}"));
        }
    }

    // One line per bearer and window: the heal and the ticks it came from, game seconds of the window.
    private static void FlushHealSums()
    {
        if (healSumFrom < 0) return;
        var now = GameTick();
        var seconds = (now - healSumFrom) / WoundRules.TicksPerSecond;
        if (seconds < HealSumSeconds) return;
        foreach (var pair in healSums)
            Line("bloodletting-heal-sum", null, FormattableString.Invariant(
                $"bearer={pair.Key} seconds={seconds:0.#} heal={pair.Value.Heal:0.#} ticks={pair.Value.Ticks} perSecond={pair.Value.Heal / seconds:0.##}"));
        healSums.Clear();
        healSumFrom = -1;
    }

    private static int BleedStacks(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        return affecters != null && affecters.Has(EnemyAbilityRules.BleedingKey) ? affecters.GetStack(EnemyAbilityRules.BleedingKey) : 0;
    }

    // The heal as the game's heal execution, published by the greatsword's effect, so heal boni and the wound's reduction work on
    // it as on any heal (as Lifebloom heals). Without the execution system the rest heal's way: health on the body.
    private static bool HealBearer(MapType map, UnitEntity bearer, double amount)
    {
        try
        {
            var system = CoreGame.TickHandler(map)?._executionSystem;
            var key = BossItemRules.EffectKey(BossItemRules.GreatSwordKey);
            swordPublisher ??= (CombatDataManager.Instance?.GetAffecterData(key) ?? Affecter.Create(key))?.Cast<IExecutionPublisher>();
            if (system == null || swordPublisher == null) throw new InvalidOperationException("execution system or publisher missing");
            var builder = system.GetBuilder();
            builder.CreateExecution(swordPublisher, bearer.Cast<IEntity>(), bearer.Cast<IEntity>(), null, null, -1);
            builder.Add(Execution.Heal(bearer.Cast<IEntity>(), DataValue.FixedValue((float)amount), (DamageProperty)0));
            system.Publish(builder.Build());
            return true;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("boss-bloodletting-heal", ex);
            CoreGame.Apply(map, new StatusApplyData(bearer.Cast<IEntity>(), bearer.Cast<IEntity>(), StatType.HealthBody, (float)amount).Cast<IApplyData>());
            return false;
        }
    }

    internal static bool Wears(IEntity entity, string partKey)
    {
        var unit = entity?.TryCast<UnitEntity>();
        return unit != null && CoreGame.Affecters(unit)?.Has(BossItemRules.EffectKey(partKey)) == true;
    }

    internal static void Line(string name, IEntity unit, string detail)
    {
        lines.TryGetValue(name, out var count);
        if (count >= LineCapPerName) return;
        lines[name] = count + 1;
        DelversHost.Info(BossEffectRules.Line(name, unit != null ? unit.Guid.ToString("D") : "-", detail));
    }

    // The unit a heal comes from: the healer itself, or the unit that summoned the healing building (the Strange Seed).
    private static IEntity HealerOf(IEntity user)
    {
        if (user == null || user.TryCast<BuildingEntity>() == null) return user;
        var inMap = CoreGame.InMap;
        return inMap == null ? null : SummonedBuildingResolver.GetSummonerUnit(inMap, user)?.Cast<IEntity>();
    }

    // The bearer's heal, or her summon's: each health change 20 % more, and marked for the wound (TakeStaffHeal).
    internal static void RaiseStaffHeals(HealExecutionData data, Il2CppSystem.Collections.Generic.List<IApplyData> changes)
    {
        var fromSummon = data?.User?.TryCast<BuildingEntity>() != null;
        var user = HealerOf(data?.User);
        var source = BossEffectRules.StaffHealSource(fromSummon, Wears(user, BossItemRules.StaffKey));
        if (changes == null || source == null) return;
        if (staffMarks.Count > MarkCap) staffMarks.Clear();
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i]?.TryCast<StatusApplyData>();
            if (change == null || !(change.Amount > 0f) || (change.StatType != StatType.HealthBody && change.StatType != StatType.HealthHead)) continue;
            var before = change.Amount;
            var raised = BossEffectRules.HealWithBonus(before, true);
            change._Amount_k__BackingField = raised;
            staffMarks.Add(change.Pointer);
            Line("overgrowth-heal", user, FormattableString.Invariant($"source={source} heal={before:0.#}->{raised:0.#}"));
        }
    }

    // True once for a health change a staff bearer's heal made, or for a change with the bearer as source.
    internal static bool TakeStaffHeal(StatusApplyData data)
        => data != null && (staffMarks.Remove(data.Pointer) || Wears(data.From, BossItemRules.StaffKey));
}

[HarmonyPatch(typeof(HealSimulator), nameof(HealSimulator.Simulate))]
internal static class BossEffectHealMarkPatch
{
    private static void Postfix(HealExecutionData data, ref Il2CppSystem.Collections.Generic.List<IApplyData> applyDatas)
    {
        try { BossEffects.RaiseStaffHeals(data, applyDatas); }
        catch (Exception ex) { ApocalypseHooks.Report("boss-effect-heal-mark", ex); }
    }
}
