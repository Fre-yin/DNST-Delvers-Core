using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using AffectTarget = global::Refactor.Combat.AffectTarget;
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using TargetType = global::Refactor.Combat.TargetType;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// The group AI of the party (the author's NPC AI, off by default): one rule set per role
// (GroupAiRoles). The healer keeps the party up with Lifebloom, the healing hand and the strange
// seed; the shield takes the boss back with a taunt and guards itself; damage dealers use their
// free quick slots with an energy reserve. Area skills that would hit party members are announced
// first (GroupAiZones), the party steps out, and the skill is skipped if they stay. Damage on the
// boss goes to an add next to it first when the setting asks for it. Each decision is logged
// (CORE_AI_RULE, capped); the counters feed the Toolkit's status line.
internal static partial class GroupAiPolicy
{
    internal sealed class Choice
    {
        internal string Skill = "";
        internal IEntity Target;
        internal string Rule = "";
        internal bool AddsFirst;
    }

    private static Choice NewChoice(string skill, IEntity target, string rule) => new Choice { Skill = skill, Target = target, Rule = rule };

    private const string SeedSkill = "SKILL_StrangeSeed";
    private const string SeedBuilding = "BUILDING_StrangeSeed";

    // The planned target of a chosen skill until the game asks for it (GroupAiDecideTargetPatch).
    private static readonly Dictionary<string, Choice> pending = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (GroupAiSituation Situation, float At)> recent = new(StringComparer.Ordinal);
    // When an area skill was announced, per caster and skill.
    private static readonly Dictionary<string, float> announced = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> buffUntil = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ironCladFight = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> ruleCounts = new(StringComparer.Ordinal);
    private static int decisions;
    private static int holds;
    private static int dodges;
    private static int friendlyFireSkips;
    private static int announcedCast;
    private static int announcedDropped;
    private static int shieldUpOutsideBoss;
    private static int tauntLate;
    private static int damageWhileAllyLow;
    private static int seeds;
    private static int lifeblooms;
    private static int logLines;
    private static int warnings;
    private static float lowestShare = 1f;
    private static string lastBossTarget = "";
    private static float bossTargetSince;

    internal static string Status()
        => GroupAiRules.Status(GroupAiSettings.Enabled, decisions, holds, dodges, friendlyFireSkips, shieldUpOutsideBoss, tauntLate, damageWhileAllyLow,
            lowestShare, seeds, lifeblooms, announcedCast, announcedDropped, ruleCounts);

    internal static void ResetCampaign()
    {
        pending.Clear();
        recent.Clear();
        seedChecks.Clear();
        nativeProbeAt.Clear();
        lastRules.Clear();
        GroupAiFocus.Reset();
        GroupAiStance.Reset();
        GroupAiCounterTank.Reset();
        GroupAiRangedReach.Reset();
        GroupAiOrderSource.Reset();
        GroupAiPartyHits.Reset();
        GroupAiBandageUse.Reset();
        GroupAiBandageSlots.Reset();
        GroupAiAftercare.Reset();
        GroupAiFormation.Reset();
        GroupAiPriority.Reset();
        GroupAiAreaProbe.Reset();
        GroupAiHealerSeed.Reset();
        GroupAiHealerSeedProbe.Reset();
        GroupAiVines.Reset();
        buffUntil.Clear();
        ironCladFight.Clear();
        logLines = warnings = 0;
        lastBossTarget = "";
        bossTargetSince = 0f;
        GroupAiRoles.Reset();
        GroupAiBandits.Reset();
        GroupAiSeedStay.Reset();
        ResetWindow();
    }

    // The Toolkit's measuring window: counters and announcements start anew.
    internal static void ResetWindow()
    {
        ruleCounts.Clear();
        announced.Clear();
        GroupAiZones.Reset();
        GroupAiAreaDodge.Reset();
        GroupAiMoveWatch.Reset();
        decisions = holds = dodges = friendlyFireSkips = shieldUpOutsideBoss = tauntLate = damageWhileAllyLow = seeds = lifeblooms = 0;
        announcedCast = announcedDropped = 0;
        lowestShare = 1f;
    }

    internal static void Forget(string guid) => pending.Remove(guid);

    // The target step comes right after the skill step; the situation of that decision is reused
    // for a second.
    internal static GroupAiSituation SituationFor(DecidePlayerCombatBehaviour decide)
    {
        var key = decide._owner.Guid.ToString();
        if (recent.TryGetValue(key, out var cached) && !GameClock.Elapsed(cached.At, 1f)) return cached.Situation;
        var situation = GroupAiSituation.Build(decide, withSlots: false);
        recent[key] = (situation, GameClock.Now);
        return situation;
    }

    internal static Choice Decide(GroupAiSituation s)
    {
        var now = GameClock.Now;
        decisions++;
        recent[s.Guid] = (s, now);
        TrackBossTarget(s);
        lowestShare = Math.Min(lowestShare, Math.Min(s.Self.Share, s.LowestAlly?.Share ?? 1f));
        var choice = EscapeVines(s) ?? (s.Role switch
        {
            GroupAiRole.Heal => Heal(s),
            GroupAiRole.Shield => Shield(s),
            GroupAiRole.Ranged => Ranged(s),
            GroupAiRole.Caster => Damage(s, "caster", GroupAiRules.DamageReserve),
            _ => Damage(s, "melee", GroupAiRules.DamageReserve)
        }) ?? NewChoice("", null, "basic");
        ruleCounts[choice.Rule] = ruleCounts.TryGetValue(choice.Rule, out var count) ? count + 1 : 1;
        lastRules[s.Guid] = choice.Rule;
        if (choice.Skill == SeedSkill) seeds++;
        var chosen = choice.Skill.Length > 0 ? s.Find(choice.Skill) : null;
        if (chosen?.Data != null && chosen.Data.TargetType == TargetType.SelfTarget && chosen.Data.Duration > 0f)
            buffUntil[s.Guid + "|" + chosen.Key] = now + chosen.Data.Duration;
        if (choice.Skill.Length > 0) pending[s.Guid] = choice;
        else pending.Remove(s.Guid);
        var slots = new List<string>();
        foreach (var slot in s.Slots) slots.Add(GroupAiRules.Short(slot.Key) + ":" + slot.State);
        Log(GroupAiRules.PartyRuleLine(s.Guid, s.Role, choice.Rule, choice.Skill,
            choice.Target != null ? choice.Target.Guid.ToString() : "-", s.Self.Share, s.LowestAlly?.Share ?? 1f, s.Energy, s.MaxEnergy,
            s.GlobalCooldown, s.State.ToString(), string.Join("+", slots), s.LowestAlly?.Guid ?? "-",
            s.SkippedSkill, s.SkippedAlly, s.SkippedFlag));
        return choice;
    }

    // The target Core planned with the skill. A damage skill on the boss goes to the nearest add
    // within AddsRadius instead (not for the shield, which holds the boss).
    internal static IEntity Target(GroupAiSituation s, ActiveSkill_Base skill, IEntity gameChoice)
    {
        var target = gameChoice;
        var addsFirst = true;
        if (skill != null && pending.TryGetValue(s.Guid, out var planned) && planned.Skill == skill.Key && planned.Target != null)
        {
            pending.Remove(s.Guid);
            target = planned.Target;
            addsFirst = planned.AddsFirst;
        }
        else if (skill != null)
        {
            // The basic attack and every unplanned hostile skill go to the focused foe too.
            target = GroupAiFocus.Choose(s, skill.Range, skill.AffectTarget, target, lastRules.TryGetValue(s.Guid, out var last) ? last : null);
        }
        if (!addsFirst || !GroupAiSettings.AddsFirst || target == null || s.Boss == null || s.Role == GroupAiRole.Shield) return target;
        var boss = s.Boss.Guid.ToString();
        if (target.Guid.ToString() != boss) return target;
        if (skill != null && !(skill.AffectTarget is AffectTarget.SingleHostile or AffectTarget.MultipleHostile)) return target;
        GroupAiSituation.Figure add = null;
        foreach (var enemy in s.Enemies)
        {
            // Among the adds a healer first, then the nearest.
            if (enemy.Guid == boss || enemy.Distance > GroupAiRules.AddsRadius) continue;
            var heals = GroupAiFocus.RoleOf(enemy.Unit) == GroupAiRules.EnemyHeal;
            var addHeals = add != null && GroupAiFocus.RoleOf(add.Unit) == GroupAiRules.EnemyHeal;
            if (add == null || (heals && !addHeals) || (heals == addHeals && enemy.Distance < add.Distance))
                add = enemy;
        }
        if (add == null) return target;
        Log(GroupAiRules.AddsFirstLine(s.Guid, add.Guid, boss));
        return add.Unit.Cast<IEntity>();
    }

    // Her own seed (BUILDING_StrangeSeed she summoned) grown at once into the mucus sprayer or the solar bloom when
    // someone next to it needs the Life Burst (GroupAiRules.SeedBurstNow); the upgrade skills target the seed.
    private static readonly string[] SeedUpgrades = { "SKILL_MucusSprayer", "SKILL_SolarBloom" };

    // Per caster: the stage last logged and when (game seconds).
    private static readonly Dictionary<string, (string Stage, float At)> seedChecks = new(StringComparer.Ordinal);

    // The rule of each unit's last decision; the focus decides a basic attack only in a damage rule (GroupAiFocus.Applies).
    private static readonly Dictionary<string, string> lastRules = new(StringComparer.Ordinal);

    // The healer's stance looks at whether her last decision was a heal (GroupAiStance).
    internal static string LastRule(string guid) => lastRules.TryGetValue(guid, out var rule) ? rule : null;

    // An elite enemy on the shield counts like the boss, for the shield wall only.
    private static string WallReason(GroupAiSituation s)
    {
        var tanking = s.BossTarget != null && s.BossTarget.Guid.ToString() == s.Guid;
        return GroupAiRules.ShieldWallReason(tanking, s.Boss != null, s.EliteOnMe != null);
    }

    // The first free quick slot that keeps the energy reserve and reaches its target; skills for
    // allies stay with the healer's rules. Line skills aim at the leader without a combat target.
    private static Choice Damage(GroupAiSituation s, string rule, float reserve = GroupAiRules.DamageReserve, params string[] skip)
    {
        if (s.Role == GroupAiRole.Heal && s.AnyAllyBelow(GroupAiRules.HealShare)) damageWhileAllyLow++;
        var closest = s.ClosestEnemy?.Unit.Cast<IEntity>();
        var leader = s.Leader?.Cast<IEntity>();
        var redeemed = Redeem(s, rule, reserve, leader, closest, skip, out var waitingOn);
        if (redeemed != null) return redeemed;
        foreach (var slot in s.Slots)
        {
            // The vines are no damage skill (EscapeVines).
            if (!slot.Free || slot.Data == null || Array.IndexOf(skip, slot.Key) >= 0 || slot.Key == GroupAiRules.VinesSkill) continue;
            if (slot.Data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly || !HasReserve(s, slot, reserve)) continue;
            var self = slot.Data.TargetType == TargetType.SelfTarget;
            IEntity target = null;
            Vector2Int at;
            if (self)
            {
                if (BuffRunning(s, slot)) continue;
                // No Guardian's Taunt that every foe in its reach would break at once (healer_drop).
                if (GroupAiCounterTank.TauntWasted(s, slot.Key, slot.Data)) continue;
                at = s.Self.Position;
            }
            else
            {
                // The focus (GroupAiFocus) picks the foe; a line skill keeps aiming along the leader.
                target = GroupAiRules.IsLine((int)slot.Data.AreaType)
                    ? s.CombatTarget ?? leader ?? closest
                    : GroupAiFocus.Choose(s, slot.Data.Range, slot.Data.AffectTarget, s.CombatTarget ?? closest, rule);
                if (target == null) continue;
                at = s.PositionOf(target);
                if (!InReach(s, GroupAiSituation.Distance(s.Self.Position, at), slot.Data.Range)) continue;
            }
            if (GroupAiRules.CanHitAllies((int)slot.Data.AffectTarget))
            {
                // Already checked by Redeem this decision: the hold goes on.
                if (slot.Key == waitingOn)
                {
                    return NewChoice("", null, GroupAiRules.RuleFor(rule + "-wait", slot.Key));
                }
                switch (CheckArea(s, slot, at))
                {
                    case GroupAiRules.AreaPhase.Wait:
                        return NewChoice("", null, GroupAiRules.RuleFor(rule + "-wait", slot.Key));
                    case GroupAiRules.AreaPhase.Skip:
                        continue;
                }
            }
            return new Choice
            {
                Skill = slot.Key,
                Target = target,
                Rule = GroupAiRules.RuleFor(rule, slot.Key),
                AddsFirst = true
            };
        }
        return NewChoice("", null, rule + "-basic");
    }

    // During the global cooldown: the healer's Lifebloom only, which is off the cooldown.
    // (Lifebloom off the global cooldown, spammable while charges last): without Core's group AI
    // the game's own decision puts no skill during the global cooldown; for a player unit with Lifebloom on its bar and
    // its auto mark on, Core offers Lifebloom there (any role), the target comes from the same rules as DecideOffGcd.
    internal static ActiveSkill_Base NativeLifebloomOffGcd(DecidePlayerCombatBehaviour decide)
    {
        var owner = decide?._owner;
        if (owner == null || owner.Faction != FactionType.Player || !Lifebloom.Active || GroupAiExcluded.Has(owner.Guid.ToString())) return null;
        if (!GroupAiGate.Open(decide) || decide._skillReader == null || !decide._skillReader.HasGlobalCooldown()) return null;
        if (CoreGame.Component<SkillComponent>(owner.Cast<IEntity>())?.GetAutoSkillData(LifebloomRules.SkillKey) != true) return null;
        var s = GroupAiSituation.Build(decide, withSlots: true);
        if (s == null) return null;
        var choice = DecideOffGcd(s, anyRole: true, source: "offgcd-ai");
        return choice == null ? null : CombatDataManager.Instance?.GetActiveSkillData(choice.Skill);
    }

    // The target of that native off-GCD Lifebloom for the game's DecideTarget (group AI off).
    internal static IEntity NativeOffGcdTarget(string unit, ActiveSkill_Base skill)
    {
        if (skill == null || skill.Key != LifebloomRules.SkillKey || !pending.TryGetValue(unit, out var planned) || planned.Skill != skill.Key) return null;
        pending.Remove(unit);
        return planned.Target;
    }

    // (the game walked Frieren to a Lifebloom target one cell too far): the
    // healer takes a skill only with its target in the exact range; closing in is the stance's job (mode=in, never nearer
    // than 5). Every other role keeps the game's one extra cell (GroupAiRules.InRange).
    private static bool InReach(GroupAiSituation s, int distance, int range)
        => s.Role == GroupAiRole.Heal ? GroupAiRules.InRangeExact(distance, range) : GroupAiRules.InRange(distance, range);

    private static bool HasReserve(GroupAiSituation s, GroupAiSituation.Slot slot, float reserve)
        => slot.Data == null || GroupAiRules.HasReserve(s.Energy, s.MaxEnergy, slot.Data.EnergyCost, reserve);

    private static bool BuffRunning(GroupAiSituation s, GroupAiSituation.Slot slot)
        => buffUntil.TryGetValue(s.Guid + "|" + slot.Key, out var until) && GroupAiRules.BuffRunning(until, GameClock.Now, slot.Data?.Duration ?? 0f);

    // "Ansage einlösen" (GroupAiRules.RedeemBlocker): the skill this unit announced comes first. Cast
    // once its area is free; dropped (with the reason) when it is not ready or its target is out of
    // reach; still waiting: waitingOn names it, and the order of the bar goes on as before.
    private static Choice Redeem(GroupAiSituation s, string rule, float reserve, IEntity leader, IEntity closest, string[] skip, out string waitingOn)
    {
        waitingOn = null;
        var prefix = s.Guid + "|";
        string key = null;
        foreach (var pending in announced.Keys)
        {
            if (!pending.StartsWith(prefix, StringComparison.Ordinal)) continue;
            key = pending;
            break;
        }
        if (key == null) return null;
        var skill = key.Substring(prefix.Length);
        // A skill this call leaves out stays announced for the call that uses it.
        if (Array.IndexOf(skip, skill) >= 0) return null;
        var slot = s.Find(skill);
        var ready = slot != null && slot.Free && slot.Data != null;
        var energy = ready && HasReserve(s, slot, reserve);
        IEntity target = null;
        var at = s.Self.Position;
        var hasTarget = false;
        var inRange = false;
        if (energy)
        {
            if (slot.Data.TargetType == TargetType.SelfTarget) hasTarget = inRange = true;
            else
            {
                target = s.CombatTarget ?? (GroupAiRules.IsLine((int)slot.Data.AreaType) ? leader : null) ?? closest;
                hasTarget = target != null;
                if (hasTarget)
                {
                    at = s.PositionOf(target);
                    inRange = InReach(s, GroupAiSituation.Distance(s.Self.Position, at), slot.Data.Range);
                }
            }
        }
        var blocker = GroupAiRules.RedeemBlocker(ready, energy, hasTarget, inRange);
        if (blocker != null)
        {
            announced.Remove(key);
            GroupAiZones.Clear(s.Guid, skill);
            announcedDropped++;
            Log(GroupAiRules.AnnouncedLine(GroupAiZones.Players, s.Guid, skill, blocker, Tick()));
            return null;
        }
        switch (CheckArea(s, slot, at))
        {
            case GroupAiRules.AreaPhase.Wait:
                waitingOn = skill;
                return null;
            case GroupAiRules.AreaPhase.Skip:
                return null;
        }
        announcedCast++;
        Log(GroupAiRules.AnnouncedLine(GroupAiZones.Players, s.Guid, skill, null, Tick()));
        return new Choice
        {
            Skill = skill,
            Target = target,
            Rule = GroupAiRules.RuleFor(rule, skill),
            AddsFirst = true
        };
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;

    // Friendly fire check of an area skill at the target cell, from the cell the unit will cast it
    // from. A new hold is announced once: the cells are marked and the allies inside dodge.
    private static GroupAiRules.AreaPhase CheckArea(GroupAiSituation s, GroupAiSituation.Slot slot, Vector2Int at)
    {
        var allies = s.AlliesInArea(slot, at);
        var (castX, castY) = GroupAiRules.CastCell(s.Self.Position.x, s.Self.Position.y, at.x, at.y, slot.Data.Range);
        var from = new Vector2Int(castX, castY);
        var cells = GroupAiZones.AreaCells(slot.Data, from, at);
        if (from != s.Self.Position)
        {
            foreach (var ally in s.AlliesOn(cells))
                if (!allies.Contains(ally)) allies.Add(ally);
        }
        GroupAiZones.LogArea(GroupAiZones.Players, s.Guid, slot.Data, from, at, allies.Count);
        var key = s.Guid + "|" + slot.Key;
        var now = GameClock.Now;
        var marked = announced.TryGetValue(key, out var since) && since <= now;
        var phase = GroupAiRules.AreaPhaseOf(allies.Count, marked, since, now);
        var flag = GroupAiRules.FlagText((int)slot.Data.AffectTarget);
        if (phase != GroupAiRules.AreaPhase.Wait)
        {
            if (marked) Log(GroupAiRules.HoldEndLine(GroupAiZones.Players, s.Guid, slot.Key, phase == GroupAiRules.AreaPhase.Cast, now - since, Tick()));
            announced.Remove(key);
            GroupAiZones.Clear(s.Guid, slot.Key);
            if (phase == GroupAiRules.AreaPhase.Cast && cells != null)
                GroupAiZones.Mark(GroupAiZones.Players, s.Guid, slot.Key, flag, cells, now + GroupAiRules.HoldSeconds);
            if (phase == GroupAiRules.AreaPhase.Skip)
            {
                friendlyFireSkips++;
                if (s.SkippedSkill == null)
                {
                    s.SkippedSkill = slot.Key;
                    s.SkippedAlly = allies[0].Guid;
                    s.SkippedFlag = flag;
                }
            }
            return phase;
        }
        if (marked) return phase;
        announced[key] = now;
        holds++;
        var guids = new List<string>();
        var positions = new List<Vector2Int>();
        foreach (var ally in allies)
        {
            guids.Add(ally.Guid);
            positions.Add(ally.Position);
        }
        Log(GroupAiRules.AnnounceLine(false, s.Guid, slot.Key, guids, flag));
        var zone = cells ?? GroupAiZones.AroundCells(positions);
        GroupAiZones.Mark(GroupAiZones.Players, s.Guid, slot.Key, flag, zone, now + GroupAiRules.HoldSeconds);
        foreach (var ally in allies)
            if (GroupAiZones.TryDodge(GroupAiZones.Players, ally.Unit, s.InMap, CombatTargetOf(ally.Unit), IsMelee(ally.Guid))) dodges++;
        return phase;
    }

    internal static bool TryDodge(DecidePlayerCombatBehaviour decide)
    {
        var owner = decide._owner;
        if (!GroupAiZones.TryDodge(GroupAiZones.Players, owner, decide._inMapData, CombatTargetOf(owner), IsMelee(owner.Guid.ToString()))) return false;
        dodges++;
        return true;
    }

    private static IEntity CombatTargetOf(UnitEntity unit)
        => CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatTarget();

    internal static bool IsMelee(string guid)
    {
        switch (GroupAiRoles.Known(guid))
        {
            case GroupAiRole.Melee:
            case GroupAiRole.Shield:
                return true;
            default:
                return false;
        }
    }

    // When the boss last changed its target, for the late taunt count.
    private static void TrackBossTarget(GroupAiSituation s)
    {
        var target = s.BossTarget != null ? s.BossTarget.Guid.ToString() : "-";
        if (target == lastBossTarget) return;
        lastBossTarget = target;
        bossTargetSince = GameClock.Now;
    }

    // With the group AI off, a player unit with Lifebloom on its quick slots logs at most
    // every NativeProbeSeconds how the game checks it (slotNative) next to the own check and what the game chose. The skill hook
    // calls it; it asks the group OneShot first (CoreProbes) and reads nothing of the game while the group is off.
    private const float NativeProbeSeconds = 5f;
    private static readonly Dictionary<string, float> nativeProbeAt = new(StringComparer.Ordinal);

    internal static void NoteNativeLifebloom(DecidePlayerCombatBehaviour decide, ActiveSkill_Base result)
    {
        if (!CoreProbes.OneShot) return;
        var owner = decide?._owner;
        if (owner == null || owner.Faction != FactionType.Player || !Lifebloom.Active) return;
        var guid = owner.Guid.ToString();
        var now = GameClock.Now;
        if (nativeProbeAt.TryGetValue(guid, out var at) && !GameClockRules.Elapsed(now, at, NativeProbeSeconds)) return;
        var slots = decide._quickSlotViewer?.GetAll(owner.Guid);
        var hasBloom = false;
        for (var i = 0; slots != null && i < slots.Length; i++)
            if (slots[i] == LifebloomRules.SkillKey) hasBloom = true;
        if (!hasBloom) return;
        nativeProbeAt[guid] = now;
        var s = GroupAiSituation.Build(decide, withSlots: true);
        var own = s.Find(LifebloomRules.SkillKey)?.State ?? "-";
        var native = GroupAiSituation.NativeStateOf(decide, owner, LifebloomRules.SkillKey);
        Log(GroupAiRules.NativeProbeLine(guid, own, native, result?.Key, Tick()));
    }

    // (shieldUpOutsideBoss): every shield wall the skill step ends with, Core's or the game's, while
    // neither a boss nor an elite is on the unit (GroupAiRules.ShieldUpOutsideBoss).
    internal static void NoteShieldUp(DecidePlayerCombatBehaviour decide, ActiveSkill_Base result)
    {
        if (result == null || result.Key != "SKILL_ShieldUp") return;
        var s = SituationFor(decide);
        var reason = WallReason(s);
        if (!GroupAiRules.ShieldUpOutsideBoss(result.Key, reason)) return;
        shieldUpOutsideBoss++;
        Log(GroupAiRules.ShieldUpOutsideLine(s.Guid, reason, Tick()));
    }

    internal static void Log(string line)
    {
        if (logLines++ < GroupAiRules.LogCap) DelversHost.Info(line);
    }

    internal static void Warn(string line)
    {
        if (warnings++ < GroupAiRules.WarningCap) DelversHost.Warning(line);
    }
}
