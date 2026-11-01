using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using DamageProperty = global::Refactor.Combat.DamageProperty;
using Execution = global::Refactor.Combat.Execution;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using HealExecutionLog = global::Refactor.Combat.HealExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using HealingTouchRoutine = global::Refactor.Combat.SkillRelated.SKILL_HealingTouchAttack._SkillRoutine_d__0;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using DamageProperty = Il2CppRefactor.Combat.DamageProperty;
using Execution = Il2CppRefactor.Combat.Execution;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using HealExecutionLog = Il2CppRefactor.Combat.HealExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using HealingTouchRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_HealingTouchAttack._SkillRoutine_d__0;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
#endif

namespace DungeonSettlersDelvers.Core;

// Lifebloom in game (rules: LifebloomRules). The row is always registered: the
// bandits' healer always has it, a party member only after Learn (the bandits' book; until then
// GrantLifebloomForTest).
// - The skill row is a copy of the healing touch (SKILL_HealingTouchAttack) with Lifebloom's range,
//   delay, cooldown and energy and the ointment's equipment; its class is the healing touch with our
//   key, whose routine Core takes over (LifebloomRoutinePatch), so no native heal runs.
// - Buffs live here, not in the game: Core's clock calls Tick once a real-time second, and each heal is measured in game
//   seconds since the last one (GameSeconds), so the game speed changes how often it runs, not how much heals; it blooms at
//   the end. The effect AFFECTER_DelversCoreLifebloom only shows the stacks in the bar.
// - Each tick and bloom goes out as the game's heal execution, published by that effect: heal number, heal modifiers, wound
// rules and meters as for any heal. At full health
//   nothing goes out (no 0); the overheal still takes the wound's buffer (Wounds.Overheal).
// - Charges replace the game's cooldown (LifebloomCooldownPatch); charges below full are kept in
//   Core's save data, buffs are not saved.
// - Core's own enemies heal a share of the target's maximum health (NpcPercent, NpcStatFactor).
// - Learn, BuffOn, ChargesOf and Status serve the runtime API and the group AI.
internal static class Lifebloom
{
    private sealed class Buff
    {
        internal LifebloomRules.State State;
        internal string Caster = "";
        internal double LastSeconds;
        internal double AppliedAt;
    }

    private const float LogSeconds = 5f;

    private static readonly Dictionary<Guid, Buff> buffs = new();
    private static readonly Dictionary<Guid, LifebloomRules.Charges> charges = new();
    private static readonly Dictionary<Guid, float> lastTickLog = new();

    // At most this many CORE_LIFEBLOOM_HEAL lines per campaign.
    private const int HealLogCap = 5000;

    private static int healLines;

    // Heals sent out as the game's heal execution, per target, until the game processed them
    // (OnAfterProcess), and what Lifebloom healed per target (LifebloomHealedForTest).
    private const int PendingCap = 32;
    private static readonly Dictionary<Guid, List<(string Caster, string Kind, double Amount)>> pending = new();
    private static readonly Dictionary<Guid, (double Healed, int Heals, int Full)> healed = new();
    private static int pendingCount;
    private static IExecutionPublisher publisher;
    private static bool publishWarned;

    internal static bool Active { get; private set; }

    // Set by CoreUnits from the settings file (bandits.lifebloom.*).
    internal static double NpcPercent { get; set; } = LifebloomRules.NpcPercentDefault;

    internal static double NpcStatFactor { get; set; } = LifebloomRules.NpcStatFactorDefault;

    // The players' shares (lifebloom.player.*), read at the campaign's start.
    internal static double PlayerPerStack { get; private set; } = LifebloomRules.PlayerPerStackDefault;

    internal static double PlayerBurst { get; private set; } = LifebloomRules.PlayerBurstDefault;

    internal static void ResetCampaign()
    {
        PlayerPerStack = LifebloomRules.PlayerPerStack(CoreSettings.Number(LifebloomRules.PlayerPerStackKey));
        PlayerBurst = LifebloomRules.PlayerBurst(CoreSettings.Number(LifebloomRules.PlayerBurstKey));
        // The tooltip shows the shares the players heal with in this campaign.
        var row = DataSheetManager.Instance?.GetSkillData(LifebloomRules.SkillKey);
        if (row != null) row.FloatValues = new Il2CppStructArray<float>(LifebloomRules.TooltipValues(PlayerPerStack, PlayerBurst));
        buffs.Clear();
        charges.Clear();
        LifebloomSlotIcon.Reset();
        LifebloomSlotCooldown.Reset();
        lastTickLog.Clear();
        healLines = 0;
        pending.Clear();
        healed.Clear();
        pendingCount = 0;
        publisher = null;
    }

    // ---- skill ----

    // The tooltip's numbers from the settings as they are now (the sheet is parsed before any campaign).
    private static float[] TooltipValuesNow()
        => LifebloomRules.TooltipValues(LifebloomRules.PlayerPerStack(CoreSettings.Number(LifebloomRules.PlayerPerStackKey)),
            LifebloomRules.PlayerBurst(CoreSettings.Number(LifebloomRules.PlayerBurstKey)));

    internal static void RegisterSkill(SkillSheet sheet)
    {
        if (sheet?._skillTable == null) return;
        if (sheet._skillTable.ContainsKey(LifebloomRules.SkillKey))
        {
            Active = true;
            return;
        }
        var t = sheet.GetData(LifebloomRules.SkillTemplate);
        if (t == null)
        {
            DelversHost.Warning("CORE_LIFEBLOOM_TEMPLATE_MISSING key=" + LifebloomRules.SkillTemplate);
            return;
        }
        var row = new SkillTableData
        {
            Key = LifebloomRules.SkillKey,
            DevComment_SkillName = LifebloomRules.SkillKey,
            DevComment_SkillFloatValues = "{0} % magic attack per stack and second, {1} s, {2} stacks, {3} % at the bloom per stack, {4} charges",
            SkillType = t.SkillType,
            IsBasicAttackSkill = false,
            // Only the tooltip reads them (LifebloomRules.TooltipValues); refreshed with the settings at each campaign start.
            FloatValues = new Il2CppStructArray<float>(TooltipValuesNow()),
            DamageType = t.DamageType,
            HitType = t.HitType,
            TargetType = t.TargetType,
            AffectTarget = t.AffectTarget,
            Range = LifebloomRules.Range,
            AreaType = t.AreaType,
            AreaRadius = t.AreaRadius,
            AllowTargetOutside = t.AllowTargetOutside,
            AllowOvercomeWall = t.AllowOvercomeWall,
            IsDelayAdjustedByAttackSpeed = LifebloomRules.AttackSpeedAdjusted,
            StartDelay = LifebloomRules.StartDelay,
            EndDelay = t.EndDelay,
            Cooldown = LifebloomRules.TableCooldown,
            Duration = t.Duration,
            EnergyCost = LifebloomRules.EnergyCost,
            DurabilityCost = 0,
            ProperDurabilityEquip = t.ProperDurabilityEquip,
            // The healer's tools, like the ointment; the healing touch's own when that row is missing.
            RequiredEquipment = sheet.GetData("SKILL_Ointment")?.RequiredEquipment ?? t.RequiredEquipment,
            AmmoType = t.AmmoType,
            AmmoAmount = t.AmmoAmount,
            CreatureKey = t.CreatureKey,
            CreatureAmount = t.CreatureAmount,
            RequiredSkillPoint = t.RequiredSkillPoint,
            RequiredLevel = t.RequiredLevel,
            PrerequisiteSkills = t.PrerequisiteSkills,
            Categories = t.Categories
        };
        sheet._skillTable.Add(LifebloomRules.SkillKey, row);
        Active = true;
        DelversHost.Info($"CORE_LIFEBLOOM_REGISTERED key={LifebloomRules.SkillKey} class={LifebloomRules.SkillTemplate} range={row.Range} cooldown={row.Cooldown.ToString("0", CultureInfo.InvariantCulture)}");
    }

    // The healing touch's class with our key and row.
    internal static Skill_Base CreateSkill()
    {
        var skill = Skill_Base.Create(LifebloomRules.SkillTemplate);
        var data = DataSheetManager.Instance?.GetSkillData(LifebloomRules.SkillKey);
        if (skill == null || data == null) return null;
        skill._Key_k__BackingField = LifebloomRules.SkillKey;
        skill._Data_k__BackingField = data;
        return skill;
    }

    // The healing touch's routine, at its start, for our key: Core casts the buff instead. True when
    // the routine was taken over.
    internal static bool TakeOver(HealingTouchRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        if (routine.__4__this?.Key != LifebloomRules.SkillKey) return false;
        routine.__1__state = -1;
        var context = routine.context;
        var caster = context?.User?.TryCast<UnitEntity>();
        var target = context?.TargetEntity?.TryCast<UnitEntity>();
        if (caster == null || target == null || target.IsDead) return true;
        var now = GameSeconds();
        var guid = CoreGame.GuidOf(target);
        buffs.TryGetValue(guid, out var buff);
        var before = buff?.State ?? default;
        // Ended since the last tick: its rest and its bloom first.
        if (buff != null && LifebloomRules.Expired(before, now))
        {
            Heal(target, caster, LifebloomRules.HealFor(before, buff.LastSeconds, now), null);
            Heal(target, caster, LifebloomRules.Burst(before, before.Stacks), "end");
        }
        var npc = CoreUnitRules.IsCoreUnit(caster.Key);
        var percent = npc ? LifebloomRules.NpcEffectivePercent(NpcPercent, CoreGame.Stat(caster, StatType.Intelligence), CoreGame.Stat(caster, StatType.WillPower), NpcStatFactor) : 0;
        var power = npc
            ? LifebloomRules.NpcPower(CoreGame.Stat(target, StatType.MaxHealthHead) + CoreGame.Stat(target, StatType.MaxHealthBody), percent)
            : Math.Max(0f, CoreGame.Stat(caster, StatType.MagicAttackPower));
        // The players' shares from the settings, Core's enemies keep theirs.
        var perStack = npc ? LifebloomRules.NpcHealPerStack : PlayerPerStack;
        var burstShare = npc ? LifebloomRules.NpcBurstPerStack : PlayerBurst;
        var after = LifebloomRules.Cast(before, now, power, out var burstStacks, perStack, burstShare);
        if (burstStacks > 0) Heal(target, caster, LifebloomRules.Burst(before, burstStacks), "refresh");
        buffs[guid] = new Buff
        {
            State = after,
            Caster = CoreGame.GuidOf(caster).ToString("D"),
            LastSeconds = now,
            AppliedAt = now
        };
        Show(target, caster, after.Stacks);
        CoreSound.PlayAt(target, LifebloomSoundRules.CastFile, LifebloomSoundRules.CastVolume);
        DelversHost.Info($"CORE_LIFEBLOOM_CAST caster={CoreGame.GuidOf(caster):D} target={guid:D} stacks={before.Stacks}->{after.Stacks} power={power.ToString("0", CultureInfo.InvariantCulture)} until={after.Until.ToString("0.0", CultureInfo.InvariantCulture)} burst={burstStacks}"
            + (npc ? " npc=" + percent.ToString("0.##", CultureInfo.InvariantCulture) + "%" : ""));
        return true;
    }

    // The game sets the cooldown after a cast (LifebloomCooldownPatch): a charge is spent instead.
    // True when the game's cooldown should run; cooldown then lasts until the next charge.
    internal static bool SpendCharge(SkillComponent skills, ref float cooldown)
    {
        var caster = skills?._owner?.TryCast<UnitEntity>();
        if (caster == null) return true;
        var guid = CoreGame.GuidOf(caster);
        var now = GameSeconds();
        var table = cooldown;
        var before = charges.TryGetValue(guid, out var stored) ? LifebloomRules.Recharge(stored, now) : LifebloomRules.Charges.Full;
        var after = LifebloomRules.Spend(before, now, table, out var letCooldown);
        charges[guid] = after;
        if (letCooldown) cooldown = (float)LifebloomRules.BlockSeconds(after, now);
        DelversHost.Info($"CORE_LIFEBLOOM_CHARGES caster={guid:D} charges={before.Count}->{after.Count} cooldown={table.ToString("0.0", CultureInfo.InvariantCulture)} gameCooldown={(letCooldown ? cooldown.ToString("0.0", CultureInfo.InvariantCulture) : "no")}");
        LifebloomSlotIcon.OnCharges(caster, guid, after.Count, "charge");
        return letCooldown;
    }

    // The key of the skill that published the execution; null when it is no active skill.
    internal static string SkillKeyOf(SkillExecutionContext context)
        => context?.Publisher?.TryCast<ActiveSkill_Base>()?.Key;

    // Off the global cooldown (LifebloomRules.PassesCooldownCheck): true while our skill's own cooldown (no charge left)
    // runs; also when the caster's skills cannot be read, so the game's answer stands.
    internal static bool OwnCooldownRunning(SkillExecutionContext context)
    {
        var skills = context.User == null ? null : CoreGame.Component<SkillComponent>(context.User);
        return skills == null || skills.HasCooldown(LifebloomRules.SkillKey);
    }

    // ---- clock ----

    // Once a second from Core's clock: charges come back, buffs heal, ended buffs bloom.
    internal static void Tick()
    {
        if (!Active) return;
        var now = GameSeconds();
        foreach (var owner in new List<Guid>(charges.Keys))
            charges[owner] = LifebloomRules.Recharge(charges[owner], now);
        if (buffs.Count == 0) return;
        foreach (var guid in new List<Guid>(buffs.Keys)) Advance(guid, now);
    }

    // Every game tick (GroupAiBanditTickPatch): a buff whose time is up blooms right away, not with the next second of Core's
    // clock (the bloom sound came about a second after the buff ran out). Running buffs wait for Tick.
    internal static void OnGameTick()
    {
        if (!Active || buffs.Count == 0) return;
        var now = GameSeconds();
        foreach (var guid in new List<Guid>(buffs.Keys))
            if (LifebloomRules.Expired(buffs[guid].State, now)) Advance(guid, now);
    }

    // One buff up to now: its heal since the last time, then its bloom when it ends or its effect is gone.
    private static void Advance(Guid guid, double now)
    {
        var buff = buffs[guid];
        var unit = CoreGame.FindUnit(guid);
        if (unit == null || unit.IsDead)
        {
            buffs.Remove(guid);
            DelversHost.Info($"CORE_LIFEBLOOM_END target={guid:D} reason={(unit == null ? "gone" : "death")} burst=0");
            return;
        }
        var caster = Guid.TryParse(buff.Caster, out var casterGuid) ? CoreGame.FindUnit(casterGuid) : null;
        var heal = LifebloomRules.HealFor(buff.State, buff.LastSeconds, now);
        buff.LastSeconds = now;
        if (heal > 0)
        {
            Heal(unit, caster, heal, null);
            if (!lastTickLog.TryGetValue(guid, out var logged) || Time.unscaledTime - logged >= LogSeconds)
            {
                lastTickLog[guid] = Time.unscaledTime;
                DelversHost.Info($"CORE_LIFEBLOOM_TICK target={guid:D} stacks={buff.State.Stacks} heal={heal.ToString("0.0", CultureInfo.InvariantCulture)} left={(buff.State.Until - now).ToString("0.0", CultureInfo.InvariantCulture)}");
            }
        }
        // The effect needs a moment to show after a cast; gone later means it was removed.
        var effectGone = now - buff.AppliedAt > LifebloomRules.EndTolerance
            && CoreGame.Affecters(unit)?.Has(LifebloomRules.EffectKey) != true;
        var reason = LifebloomRules.BloomReason(buff.State, now, effectGone);
        if (reason != null)
        {
            buffs.Remove(guid);
            Heal(unit, caster, LifebloomRules.Burst(buff.State, buff.State.Stacks), reason);
            Show(unit, caster, 0);
        }
    }

    // Whether the unit's skill tree holds Lifebloom already (LifebloomBook refuses then).
    internal static bool Knows(UnitEntity unit)
        => unit != null && CoreGame.Flow?._campaignData?.PlayerUnitDataContainer?.SkillViewer?.GetData(unit.Guid)?.HasSkill(LifebloomRules.SkillKey) == true;

    // Gives a party member the skill as a tertiary skill, the way Frieren gets Zoltraak (FrierenMagicSkills): in its skill
    // tree's extra skills and in its skill component; other extra skills stay. Then the view updates the game sends after
    // adding a skill (EntityEventHandler.OnEvent(AddSkillRequested): EntityStatusChanged and
    // EntitySkillChanged), so the quick slots show it at once (the slot stayed empty). True when the tree
    // has it; refreshed says whether the updates went out.
    internal static bool Learn(UnitEntity unit) => Learn(unit, out _);

    internal static bool Learn(UnitEntity unit, out bool refreshed)
    {
        refreshed = false;
        if (!Active || unit == null) return false;
        var container = CoreGame.Flow?._campaignData?.PlayerUnitDataContainer;
        var tree = container?.SkillViewer?.GetData(unit.Guid);
        if (container != null && tree != null && !tree.HasSkill(LifebloomRules.SkillKey))
            container.OnAddExtraSkill(unit.Guid, LifebloomRules.SkillKey);
        var skills = CoreGame.Component<SkillComponent>(unit.Cast<IEntity>());
        if (skills != null && !skills.HasSkill(LifebloomRules.SkillKey)) skills.AddSkill(LifebloomRules.SkillKey);
        var learned = container?.SkillViewer?.GetData(unit.Guid)?.HasSkill(LifebloomRules.SkillKey) == true;
        if (learned) refreshed = Refresh(unit);
        return learned;
    }

    private static bool Refresh(UnitEntity unit)
    {
        var dispatcher = CoreGame.Flow?._viewDispatcher;
        if (dispatcher == null || !CoreGame.TryMapOfPartyMember(unit, out var map)) return false;
        var entity = unit.Cast<IEntity>();
        dispatcher.AddViewUpdateCall(new ViewEntityData(UpdateType.EntityStatusChanged, map, entity).Cast<IViewUpdateData>());
        dispatcher.AddViewUpdateCall(new ViewEntityData(UpdateType.EntitySkillChanged, map, entity).Cast<IViewUpdateData>());
        return true;
    }

    // ---- save ----

    // Before the game saves: only casters below full charges are written.
    internal static void SyncToSave()
    {
        var data = CoreSaveStore.Current;
        if (data == null) return;
        var now = GameSeconds();
        var open = new Dictionary<Guid, LifebloomRules.Charges>();
        foreach (var pair in charges)
        {
            var current = LifebloomRules.Recharge(pair.Value, now);
            if (current.Count < LifebloomRules.MaxCharges) open[pair.Key] = current;
        }
        data.SetLifebloomCharges(open);
    }

    internal static void OnLoaded()
    {
        var saved = CoreSaveStore.Current?.LifebloomCharges();
        if (saved == null || saved.Count == 0) return;
        foreach (var pair in saved) charges[pair.Key] = pair.Value;
        DelversHost.Info("CORE_LIFEBLOOM_CHARGES_RESTORED casters=" + saved.Count);
    }

    // ---- reading ----

    internal static int ChargesOf(Guid caster)
    {
        if (!charges.TryGetValue(caster, out var stored)) return LifebloomRules.MaxCharges;
        return LifebloomRules.Recharge(stored, GameSeconds()).Count;
    }

    // The caster's charges as of now (count, next charge's game second, recharge seconds) and the game second.
    internal static LifebloomRules.Charges ChargeState(Guid caster, out double now)
    {
        now = GameSeconds();
        return charges.TryGetValue(caster, out var stored) ? LifebloomRules.Recharge(stored, now) : LifebloomRules.Charges.Full;
    }

    // Game seconds until the caster holds need charges again (the group AI's heal-wait).
    internal static double SecondsUntilCharges(Guid caster, int need)
        => charges.TryGetValue(caster, out var stored) ? LifebloomRules.SecondsUntilCharges(stored, GameSeconds(), need) : 0;

    internal static bool BuffOn(Guid target, out int stacks, out double secondsLeft)
    {
        stacks = 0;
        secondsLeft = 0;
        if (!buffs.TryGetValue(target, out var buff)) return false;
        stacks = buff.State.Stacks;
        secondsLeft = Math.Max(0, buff.State.Until - GameSeconds());
        return true;
    }

    // For tests: "enabled=true charges=2 stacks=1 left=12.3".
    internal static string Status(Guid unit)
    {
        BuffOn(unit, out var stacks, out var secondsLeft);
        return "enabled=" + (Active ? "true" : "false") + " charges=" + ChargesOf(unit).ToString(CultureInfo.InvariantCulture)
            + " stacks=" + stacks.ToString(CultureInfo.InvariantCulture) + " left=" + secondsLeft.ToString("0.0", CultureInfo.InvariantCulture);
    }

    // ---- the game ----

    // A heal of a tick or a bloom (burst: "end", "refresh", "dispel"; null is a tick). It goes out as
    // the game's heal execution; at full health as no heal at all, the overheal only takes the wound's
    // buffer. Without the game's execution system the old way: changes with the caster as source.
    private static void Heal(UnitEntity target, UnitEntity caster, double amount, string burst)
    {
        if (!(amount > 0) || !TryMapOf(target, out var map)) return;
        if (LifebloomSoundRules.BloomPlays(burst, amount))
            CoreSound.PlayAt(target, LifebloomSoundRules.BloomFile, LifebloomSoundRules.BloomVolumeFor(LifebloomSlotCooldown.Selected(CoreGame.GuidOf(target))));
        var guid = CoreGame.GuidOf(target);
        var kind = burst ?? "tick";
        var casterId = caster != null ? CoreGame.GuidOf(caster).ToString("D") : "-";
        var missingHead = CoreGame.Stat(target, StatType.MaxHealthHead) - CoreGame.Stat(target, StatType.HealthHead);
        var missingBody = CoreGame.Stat(target, StatType.MaxHealthBody) - CoreGame.Stat(target, StatType.HealthBody);
        if (burst != null)
            DelversHost.Info($"CORE_LIFEBLOOM_BURST target={guid:D} reason={burst} heal={amount.ToString("0.0", CultureInfo.InvariantCulture)}");
        if (!LifebloomRules.HealsThroughGame(missingHead, missingBody))
        {
            Wounds.Overheal(target, amount, "lifebloom");
            Count(guid, 0, full: true);
            LogHeal(casterId, guid, kind, amount, 0, 0, 0, "full");
            return;
        }
        // A tick that really heals shows the game's small heal effect, as the seed's aura does.
        if (burst == null)
        {
            try { WoundFx.PlayHeal(target); }
            catch (Exception ex) { ApocalypseHooks.Report("lifebloom-heal-fx", ex); }
        }
        if (Publish(map, target, caster, amount))
        {
            if (!pending.TryGetValue(guid, out var queue)) pending[guid] = queue = new List<(string Caster, string Kind, double Amount)>();
            if (queue.Count >= PendingCap)
            {
                queue.RemoveAt(0);
                pendingCount--;
            }
            queue.Add((casterId, kind, amount));
            pendingCount++;
            return;
        }
        var (head, body) = LifebloomRules.Split(amount, missingHead, missingBody);
        var from = caster?.Cast<IEntity>();
        if (body > 0) CoreGame.Apply(map, new StatusApplyData(target.Cast<IEntity>(), from, StatType.HealthBody, (float)body).Cast<IApplyData>());
        if (head > 0) CoreGame.Apply(map, new StatusApplyData(target.Cast<IEntity>(), from, StatType.HealthHead, (float)head).Cast<IApplyData>());
        Count(guid, head + body, full: false);
        LogHeal(casterId, guid, kind, amount, head + body, head, body, "direct");
    }

    // The heal as the game's heal execution, published by Lifebloom's effect. False when the game's
    // execution system or the publisher is missing (then the old way, warned once).
    private static bool Publish(MapType map, UnitEntity target, UnitEntity caster, double amount)
    {
        try
        {
            var system = CoreGame.TickHandler(map)?._executionSystem;
            var source = Publisher();
            if (system == null || source == null) throw new InvalidOperationException("execution system or publisher missing");
            var builder = system.GetBuilder();
            builder.CreateExecution(source, caster?.Cast<IEntity>(), target.Cast<IEntity>(), null, null, -1);
            builder.Add(Execution.Heal(target.Cast<IEntity>(), DataValue.FixedValue((float)amount), (DamageProperty)0));
            system.Publish(builder.Build());
            return true;
        }
        catch (Exception ex)
        {
            if (!publishWarned)
            {
                publishWarned = true;
                DelversHost.Warning("CORE_LIFEBLOOM_PUBLISH_FAILED (old way from now): " + ex.Message);
            }
            return false;
        }
    }

    // The game's affecter object of Lifebloom's effect; the game's own when it has none for our key.
    private static IExecutionPublisher Publisher()
    {
        if (publisher != null) return publisher;
        var affecter = CombatDataManager.Instance?.GetAffecterData(LifebloomRules.EffectKey) ?? Affecter.Create(LifebloomRules.EffectKey);
        publisher = affecter?.Cast<IExecutionPublisher>();
        return publisher;
    }

    // After the game processed an execution (LifebloomHealPatch): a heal of Lifebloom's is counted and
    // logged with what really arrived (HealExecutionLog).
    internal static void OnAfterProcess(IExecutionData data)
    {
        if (pendingCount == 0 || data == null) return;
        var heal = data.TryCast<HealExecutionData>();
        if (heal == null) return;
        if (heal.BaseExecution?.Context?.Publisher?.TryCast<Affecter>()?.Key != LifebloomRules.EffectKey) return;
        var target = heal.Target?.TryCast<UnitEntity>();
        if (target == null) return;
        var guid = CoreGame.GuidOf(target);
        var log = heal.GetLog()?.TryCast<HealExecutionLog>();
        double head = 0, body = 0;
        var changes = log?._applyDatas;
        for (var i = 0; changes != null && i < changes.Count; i++)
        {
            var change = changes[i]?.TryCast<StatusApplyData>();
            if (change == null) continue;
            if (change.StatType == StatType.HealthHead) head += change.Amount;
            else if (change.StatType == StatType.HealthBody) body += change.Amount;
        }
        // (effective 1451 over amount 1225): the pending entry was taken first come, first served
        // per target, so with two casters or a lost heal the requested amount of another heal was logged. The amount
        // and the caster now come from the execution itself; the pending entry is the one with that amount
        // (LifebloomRules.PendingMatch), only for its kind. Effective is what arrived on head and body; the game's
        // TotalHeal stays apart as total=.
        var total = log != null ? log.TotalHeal : head + body;
        double amount = heal.Value?._fixedValue ?? 0f;
        var user = heal.User?.TryCast<UnitEntity>();
        var casterId = user != null ? CoreGame.GuidOf(user).ToString("D") : "-";
        var kind = "?";
        if (pending.TryGetValue(guid, out var queue) && queue.Count > 0)
        {
            var amounts = queue.ConvertAll(entry => entry.Amount);
            var index = LifebloomRules.PendingMatch(amounts, amount);
            if (index >= 0)
            {
                kind = queue[index].Kind;
                if (amount <= 0) amount = queue[index].Amount;
                queue.RemoveAt(index);
                pendingCount--;
            }
        }
        var effective = LifebloomRules.Effective(head, body);
        Count(guid, effective, full: false);
        LogHeal(casterId, guid, kind, amount, effective, head, body, "exec", total);
    }

    private static void Count(Guid target, double effective, bool full)
    {
        healed.TryGetValue(target, out var sum);
        healed[target] = (sum.Healed + Math.Max(0, effective), sum.Heals + 1, sum.Full + (full ? 1 : 0));
    }

    private static void LogHeal(string caster, Guid target, string kind, double amount, double effective, double head, double body, string via, double? total = null)
    {
        if (healLines++ < HealLogCap)
            DelversHost.Info($"CORE_LIFEBLOOM_HEAL caster={caster} target={target:D} kind={kind} amount={amount.ToString("0.0", CultureInfo.InvariantCulture)} effective={effective.ToString("0.0", CultureInfo.InvariantCulture)} head={head.ToString("0.0", CultureInfo.InvariantCulture)} body={body.ToString("0.0", CultureInfo.InvariantCulture)} via={via}"
                + (total is { } t ? " total=" + t.ToString("0.0", CultureInfo.InvariantCulture) : ""));
    }

    // For tests: "targets=2 <guid>=123.4/15/3;…" (healed, heals, of them at full health) this campaign.
    internal static string HealedStatus()
    {
        var parts = new List<string>();
        foreach (var pair in healed)
            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:D}={1:0.0}/{2}/{3}", pair.Key, pair.Value.Healed, pair.Value.Heals, pair.Value.Full));
        return "targets=" + healed.Count.ToString(CultureInfo.InvariantCulture) + (parts.Count == 0 ? "" : " " + string.Join(";", parts));
    }

    // The effect in the bar with the stacks as its count; 0 removes it.
    private static void Show(UnitEntity target, UnitEntity caster, int stacks)
    {
        if (TryMapOf(target, out var map))
        {
            ReviveEffects.Ensure();
            var affecters = CoreGame.Affecters(target);
            var have = affecters != null && affecters.Has(LifebloomRules.EffectKey) ? affecters.GetStack(LifebloomRules.EffectKey) : 0;
            if (have > 0)
                CoreGame.Apply(map, new AffecterApplyData(target.Cast<IEntity>(), LifebloomRules.EffectKey, -have, 0f, false).Cast<IApplyData>());
            if (stacks > 0)
                CoreGame.Apply(map, new AffecterApplyData(target.Cast<IEntity>(), LifebloomRules.EffectKey, stacks, (float)LifebloomRules.DurationSeconds, true, caster?.Cast<IEntity>()).Cast<IApplyData>());
        }
    }

    // Game time in seconds (ticks, so pause and speed count).
    private static double GameSeconds() => (CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0) / WoundRules.TicksPerSecond;

    private static bool TryMapOf(UnitEntity unit, out MapType map)
        => CoreGame.TryMapOfPartyMember(unit, out map) || CoreGame.TryMapOfUnit(unit, out map);
}
