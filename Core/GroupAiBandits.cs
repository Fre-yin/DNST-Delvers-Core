using System.Globalization;
using UnityEngine;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using AffectTarget = global::Refactor.Combat.AffectTarget;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = global::Refactor.Combat.IExecutionPublisher;
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using TargetType = global::Refactor.Combat.TargetType;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using IExecutionPublisher = Il2CppRefactor.Combat.IExecutionPublisher;
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// The group AI of Core's bandits (CoreUnitRules units; CoreUnitCombat asks it for every decision of
// their combat pattern). The kit comes from the unit key: blade, guard, archer, healer. They focus a
// player healer near them, protect their own healer (pull its attacker with the chain, stun it,
// step in between), keep a formation behind the guard at the start of a fight, and the healer kites
// and keeps Lifebloom up. Area skills that would hit other bandits are announced and dodged like the
// party's (GroupAiZones side "bandits"). Moves go through GroupAiBanditMoves. Each decision is
// logged (CORE_AI_BANDIT_RULE, capped); ProtectHealer is a Toolkit switch. In a marked test campaign the
// setting bandits.basic-only leaves them the basic attack only (GroupAiRules.BasicOnlyActive).
internal static partial class GroupAiBandits
{
    internal enum Kit
    {
        None,
        Blade,
        Guard,
        Archer,
        Healer,
        // The weak bandits' new classes (WeakBanditRules, BanditWeakClasses).
        Spear,
        Sword,
        WaterMage,
        FireMage
    }

    // The melee kits: they dodge an area by stepping out, not back, and protect the healer up close.
    internal static bool IsMelee(Kit kit) => kit is Kit.Blade or Kit.Guard or Kit.Spear or Kit.Sword;

    internal sealed class Choice
    {
        internal string Skill = "";
        internal IEntity Target;
        internal string Rule = "";
        // A skill on a cell aims here instead of at the target's cell (the water mage's bubbling).
        internal Vector2Int? Cell;
    }

    private static Choice NewChoice(string skill, IEntity target, string rule) => new Choice { Skill = skill, Target = target, Rule = rule };

    private sealed class Figure
    {
        internal UnitEntity Unit;
        internal string Guid = "";
        internal float Share;
        internal Vector2Int Position;
        internal int Distance;
        // A player unit; otherwise another bandit.
        internal bool Player;
    }

    private sealed class Slot
    {
        internal string Key = "";
        internal SkillTableData Data;
        internal ActiveSkill_Base Skill;
        internal string State = "";
        // Seconds until the game's cooldown of the slot ends (State "cd"), else 0 (the blade's Deadly Fear, BanditFearRules).
        internal float CooldownLeft;

        internal bool Free => State == "free";
    }

    private sealed class Situation
    {
        internal UnitEntity Unit;
        internal IInMapDataSupplier InMap;
        internal string Guid = "";
        internal Kit Kit;
        internal string BasicAttack = "";
        internal readonly List<Slot> Slots = new();
        internal readonly List<Figure> Allies = new();
        internal readonly List<Figure> Hostiles = new();
        internal Figure Self;
        internal IEntity CombatTarget;
        // A player healer within FocusRadius.
        internal IEntity Focus;
        internal bool GlobalCooldown;
        internal CombatState State;
        internal Figure Healer;
        internal Figure Guard;
        // The player unit attacking the bandits' healer; the chain pulls it too.
        internal Figure HealerAttacker;
        // The attacker was chosen by damage on the healer (GroupAiRules.TopAttacker).
        internal bool AttackerByDamage;
        internal bool Protecting;
        // The player unit whose taunt this bandit heeds right now (TaunterOf), else null.
        internal Figure Taunter;
        // Fallen player units among the hostiles (category Unconscious), and why they may be attacked now
        // (GroupAiRules.FinishReason); null while they are skipped and left out of Hostiles.
        internal List<Figure> Downed;
        internal string FinishReason;
        internal string SkippedSkill;
        internal string SkippedAlly;
        internal string SkippedFlag;

        internal Figure LowestAlly
        {
            get
            {
                Figure lowest = null;
                foreach (var ally in Allies)
                    if (lowest == null || ally.Share < lowest.Share) lowest = ally;
                return lowest;
            }
        }

        internal Figure ClosestHostile
        {
            get
            {
                Figure closest = null;
                foreach (var hostile in Hostiles)
                    if (closest == null || hostile.Distance < closest.Distance) closest = hostile;
                return closest;
            }
        }

        internal IEntity Intended => Focus ?? CombatTarget ?? ClosestHostile?.Unit.Cast<IEntity>();

        internal Slot Find(string key)
        {
            foreach (var slot in Slots)
                if (slot.Key == key) return slot;
            return null;
        }

        internal Vector2Int PositionOf(IEntity entity)
        {
            try
            {
                return InMap.GetLogicalPosition(entity.Guid);
            }
            catch (Exception)
            {
                return Self.Position;
            }
        }
    }

    internal const float EmergencyShare = 0.35f;

    internal const float FormationSeconds = 5f;
    internal const int FormationBehind = 2;
    internal const int ArcherBehind = 1;
    internal const float FormationWindowSeconds = 3f;

    internal static bool ProtectHealer = true;

    private static readonly Dictionary<string, float> holdSince = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> buffUntil = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, float> formationSince = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> combatSince = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> ruleCounts = new(StringComparer.Ordinal);
    private static int decisions;
    private static int holds;
    private static int dodges;
    private static int friendlyFireSkips;
    private static int announcedCast;
    private static int announcedDropped;

    // The switch "basic attack only" as read at the campaign's start, and whether its start line is written (BasicOnlyNow).
    private static bool basicOnlySetting;
    private static bool basicOnlyLogged;

    // Whether the warning for a global cooldown on a bandit is written already (NoteGlobalCooldown).
    private static bool gcdSeenWritten;

    internal static string Status()
    {
        var sorted = new List<KeyValuePair<string, int>>(ruleCounts);
        sorted.Sort(CompareRuleCounts);
        var parts = new List<string>();
        foreach (var rule in sorted) parts.Add(rule.Key + ":" + rule.Value.ToString(CultureInfo.InvariantCulture));
        return string.Format(CultureInfo.InvariantCulture, "decisions={0} holds={1} dodges={2} friendlyFireSkips={3} announcedCast={4} announcedDropped={5} taunted={6} rules={7}",
            decisions, holds, dodges, friendlyFireSkips, announcedCast, announcedDropped, taunted, parts.Count == 0 ? "-" : string.Join(",", parts));
    }

    private static int CompareRuleCounts(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
    {
        var byCount = b.Value.CompareTo(a.Value);
        if (byCount == 0) return string.CompareOrdinal(a.Key, b.Key);
        return byCount;
    }

    internal static void Reset()
    {
        kits.Clear();
        holdSince.Clear();
        buffUntil.Clear();
        healers.Clear();
        ironCladFight.Clear();
        kiteSince.Clear();
        seedUpgradeNotes.Clear();
        kiteSkips.Clear();
        pulled.Clear();
        stunnedAfterPull.Clear();
        ResetOffensive();

        taunts.Clear();
        healerShares.Clear();
        watched.Clear();
        ownPulls.Clear();
        lastWatchTick = -1;
        taunted = 0;
        tauntFactor = CoreSettings.Number(GroupAiRules.TauntFactorKey) ?? GroupAiRules.TauntFactorDefault;
        tauntBreakDrop = CoreSettings.Number(GroupAiRules.TauntBreakDropKey) ?? GroupAiRules.TauntBreakDropDefault;
        tauntBreakBelow = CoreSettings.Number(GroupAiRules.TauntBreakBelowKey) ?? GroupAiRules.TauntBreakBelowDefault;
        slowMinTargets = GroupAiRules.SlowMinTargets(CoreSettings.Number(GroupAiRules.SlowMinTargetsKey));
        BanditSlowRing.Reset(GroupAiRules.SlowSeconds(CoreSettings.Number(GroupAiRules.SlowSecondsKey)));
        BanditVines.Reset();
        BanditSpearPierce.Reset();
        BanditSolarBloom.Reset();
        BanditPassives.Reset();
        BanditEffectProbe.Reset();
        supportSince.Clear();
        BanditGroupWake.Reset();
        healBelow = GroupAiRules.HealerHealBelow(CoreSettings.Number(GroupAiRules.HealerHealBelowKey));
        formationSince.Clear();
        combatSince.Clear();
        ruleCounts.Clear();
        healerHits.Clear();
        protectTarget.Clear();
        protectHeld.Clear();
        skipLogged.Clear();
        finishLogged.Clear();
        tickSkipped.Clear();
        coneLogged.Clear();
        healPaths.Clear();
        finishDownedAlways = CoreSettings.FinishDownedAlways;
        spareDownedInCone = CoreSettings.SpareDownedInCone;
        areaIgnoresDowned = CoreSettings.AreaIgnoresDowned;
        areaValueLogged.Clear();
        LogSkillValues();
        chaseCells = GroupAiRules.ChaseCells(CoreSettings.Number(GroupAiRules.ChaseCellsKey));
        basicOnlySetting = CoreSettings.BanditsBasicOnly;
        basicOnlyLogged = false;
        gcdSeenWritten = false;
        decisions = holds = dodges = friendlyFireSkips = logLines = warnings = 0;
        announcedCast = announcedDropped = 0;
        ResetSeedStay();
    }

    // The skill (empty: basic attack) and target of a bandit's next action; the rule names why.
    internal static (string Skill, IEntity Target, string Rule, Vector2Int? Cell) Decide(UnitEntity unit, IInMapDataSupplier inMap)
    {
        try
        {
            if (unit == null || inMap == null) return ("", null, "bandit-nounit", null);
            if (CoreGame.TryMapOfUnit(unit, out var passiveMap)) BanditPassives.Check(unit, passiveMap);
            var kit = KitOf(unit);
            if (kit == Kit.None) return ("", null, "bandit-nokit", null);
            if (GroupAiZones.TryDodge(GroupAiZones.Bandits, unit, inMap, CombatTargetOf(unit), IsMelee(kit)))
            {
                dodges++;
                return ("", null, "bandit-dodge", null);
            }
            // No new decision while Core's own move order runs (a dodge may still
            // replace it, above); CoreUnitCombat gives the game nothing.
            // A healer whose order has not moved her yet decides again as soon as a
            // hostile stands next to her (the order ends as "threat").
            if (GroupAiMoveWatch.Holds(unit))
            {
                if (kit != Kit.Healer || !GroupAiRules.HealerBreaksMove(GroupAiMoveWatch.Unmoved(unit), ClosestHostileDistance(unit, inMap)))
                    return ("", null, "bandit-moving", null);
                GroupAiMoveWatch.End(unit, "threat");
            }
            var s = Build(unit, inMap, kit);
            if (s == null) return ("", null, "bandit-idle", null);
            decisions++;
            NoteGlobalCooldown(s);
            NoteFightDecision(s.State == CombatState.InCombat);
            BanditStatsProbe.Unit(kit.ToString(), unit);
            BanditStatsProbe.Attack(unit);
            BanditSerrated.ResetOldPenalty(unit);
            // The game gives bandits no global cooldown, so the kit's rules always decide; GlobalCooldown stays as the
            // signal in the log line (gcd=).
            // "Im Samen bleiben" before the kit: a staying bandit walks in, attacks in reach or holds (BanditSeedStay).
            var choice = SeedStay(s) ?? (kit switch
            {
                Kit.Blade => Blade(s),
                Kit.Guard => Guard(s),
                Kit.Archer => Archer(s),
                Kit.Healer => Healer(s),
                Kit.Spear => Spear(s),
                Kit.Sword => Sword(s),
                Kit.WaterMage => WaterMage(s),
                Kit.FireMage => FireMage(s),
                _ => null
            }) ?? Basic(s, "bandit-basic");
            if (spareDownedInCone) choice = SpareDownedInCone(s, choice);
            if (GroupAiRules.DropsSkill(BasicOnlyNow(), choice.Skill)) choice = Basic(s, GroupAiRules.BasicOnlyRule);
            ruleCounts[choice.Rule] = ruleCounts.TryGetValue(choice.Rule, out var count) ? count + 1 : 1;
            NoteFinishChoice(s, choice);
            var chosen = choice.Skill.Length > 0 ? s.Find(choice.Skill) : null;
            if (chosen?.Data != null && chosen.Data.TargetType == TargetType.SelfTarget && chosen.Data.Duration > 0f)
                buffUntil[s.Guid + "|" + chosen.Key] = GameClock.Now + chosen.Data.Duration;
            var slots = new List<string>();
            foreach (var slot in s.Slots) slots.Add(GroupAiRules.Short(slot.Key) + ":" + slot.State);
            var lowest = s.LowestAlly;
            Log(GroupAiRules.BanditRuleLine(s.Guid, s.Kit.ToString(), choice.Rule, choice.Skill, TargetText(s, choice.Target),
                s.Self.Share, lowest?.Share ?? 1f, s.Hostiles.Count - DownedAmongHostiles(s), s.Downed?.Count ?? 0,
                s.Focus != null ? s.Focus.Guid.ToString() : "-", s.GlobalCooldown, s.State.ToString(), string.Join("+", slots),
                s.SkippedSkill, s.SkippedAlly, s.SkippedFlag, lowest?.Guid ?? "-", Tick()));
            return (choice.Skill, choice.Target, choice.Rule, choice.Cell);
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_FAILED: " + ex);
            return ("", null, "bandit-error", null);
        }
    }

    private static Choice Formation(Situation s) => Formation(s, FormationBehind);

    // Before the fight and in its first FormationWindowSeconds: stand the given number of cells behind
    // the guard, seen from the closest hostile.
    private static Choice Formation(Situation s, int behind)
    {
        if (!ProtectHealer || s.Guard == null) return null;
        var now = GameClock.Now;
        // The start of the fight is noted before the taunt check: taunted from the first moment, the window
        // would otherwise open only after the taunt, in the middle of the fight.
        var inCombat = s.State == CombatState.InCombat;
        var since = now;
        if (inCombat && !combatSince.TryGetValue(s.Guid, out since))
        {
            since = now;
            combatSince[s.Guid] = now;
        }
        if (s.Taunter != null && !GroupAiRules.TauntAllows(s.Kit == Kit.Healer, "move")) return null;
        if (inCombat && (GameClockRules.Since(now, since) > FormationWindowSeconds || s.HealerAttacker != null)) return null;
        var closest = s.ClosestHostile;
        if (closest == null) return null;
        if (formationSince.TryGetValue(s.Guid, out var last) && !GameClockRules.Elapsed(now, last, FormationSeconds)) return null;
        var tile = new Vector2Int(s.Guard.Position.x + behind * Math.Sign(s.Guard.Position.x - closest.Position.x),
            s.Guard.Position.y + behind * Math.Sign(s.Guard.Position.y - closest.Position.y));
        if (tile == s.Self.Position || tile == s.Guard.Position) return null;
        try
        {
            if (s.InMap.GetNearMovablePosition(tile) != tile) return null;
        }
        catch (Exception)
        {
            return null;
        }
        formationSince[s.Guid] = now;
        if (!GroupAiBanditMoves.TryOrder(s.Unit, tile, "formation")) return null;
        return NewChoice("", null, "formation");
    }

    // The first usable skill of the kit's order; skills for allies are left to the healer's rules.
    private static Choice Damage(Situation s, string prefix, params string[] order)
    {
        var intended = s.Intended;
        var redeemed = Redeem(s, prefix, order, out var waitingOn);
        if (redeemed != null) return redeemed;
        foreach (var key in order)
        {
            var slot = s.Find(key);
            if (slot == null || !slot.Free || slot.Data == null) continue;
            if (slot.Data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly) continue;
            IEntity target;
            Vector2Int at;
            if (slot.Data.TargetType == TargetType.SelfTarget)
            {
                if (BuffRunning(s, slot)) continue;
                target = s.Self.Unit.Cast<IEntity>();
                at = s.Self.Position;
            }
            else
            {
                if (intended == null) continue;
                target = intended;
                at = s.PositionOf(intended);
                if (!GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, at), slot.Data.Range)) continue;
            }
            if (areaIgnoresDowned ? AreaLoses(s, slot, at, order) : HitsDowned(s, slot, at)) continue;
            if (GroupAiRules.CanHitAllies((int)slot.Data.AffectTarget))
            {
                // Already checked by Redeem this decision: the hold goes on.
                if (slot.Key == waitingOn) return Basic(s, GroupAiRules.RuleFor(prefix + "-wait", slot.Key));
                switch (CheckArea(s, slot, at))
                {
                    case GroupAiRules.AreaPhase.Wait:
                        return Basic(s, GroupAiRules.RuleFor(prefix + "-wait", slot.Key));
                    case GroupAiRules.AreaPhase.Skip:
                        continue;
                }
            }
            return NewChoice(slot.Key, target, GroupAiRules.RuleFor(prefix, slot.Key));
        }
        return Basic(s, prefix + "-basic");
    }

    // "Ansage einlösen" as in GroupAiPolicy.Redeem, for the bandits' side.
    private static Choice Redeem(Situation s, string prefix, string[] order, out string waitingOn)
    {
        waitingOn = null;
        var start = s.Guid + "|";
        string key = null;
        foreach (var pending in holdSince.Keys)
        {
            if (!pending.StartsWith(start, StringComparison.Ordinal)) continue;
            key = pending;
            break;
        }
        if (key == null) return null;
        var skill = key.Substring(start.Length);
        // Only a skill of this call's order; another kit rule keeps it announced.
        if (Array.IndexOf(order, skill) < 0) return null;
        var slot = s.Find(skill);
        var ready = slot != null && slot.Free && slot.Data != null;
        IEntity target = null;
        var at = s.Self.Position;
        var hasTarget = false;
        var inRange = false;
        if (ready)
        {
            if (slot.Data.TargetType == TargetType.SelfTarget)
            {
                target = s.Self.Unit.Cast<IEntity>();
                hasTarget = inRange = true;
            }
            else
            {
                target = s.Intended;
                hasTarget = target != null;
                if (hasTarget)
                {
                    at = s.PositionOf(target);
                    inRange = GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, at), slot.Data.Range);
                }
            }
        }
        // Bandits pay no energy (CoreUnitEnergyPatch).
        var blocker = GroupAiRules.RedeemBlocker(ready, true, hasTarget, inRange);
        if (blocker == null && !areaIgnoresDowned && HitsDowned(s, slot, at)) blocker = "downed";
        if (blocker == null && areaIgnoresDowned && AreaLoses(s, slot, at, order)) blocker = "value";
        if (blocker != null)
        {
            holdSince.Remove(key);
            GroupAiZones.Clear(s.Guid, skill);
            announcedDropped++;
            Log(GroupAiRules.AnnouncedLine(GroupAiZones.Bandits, s.Guid, skill, blocker, Tick()));
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
        Log(GroupAiRules.AnnouncedLine(GroupAiZones.Bandits, s.Guid, skill, null, Tick()));
        return NewChoice(skill, target, GroupAiRules.RuleFor(prefix, skill));
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;

    private static Choice Basic(Situation s, string rule)
    {
        // While fallen units are skipped, never the game's "closest hostile": it could pick a fallen one.
        // b': the current combat target stays while it is a capable hostile (GroupAiRules.BasicTargetPick).
        // Only while fallen units are skipped (the case of SKIP_DOWNED); otherwise the game's own choice as before.
        var skipping = s.Downed != null && s.FinishReason == null;
        var current = skipping && s.CombatTarget != null ? s.Hostiles.Find(h => h.Guid == s.CombatTarget.Guid.ToString()) : null;
        var closest = skipping ? s.ClosestHostile : null;
        var pick = GroupAiRules.BasicTargetPick(s.Focus?.Guid.ToString(), current?.Guid, current != null, closest?.Guid);
        IEntity target = s.Focus;
        if (target == null && pick != null) target = (current != null && pick == current.Guid ? current : closest)?.Unit.Cast<IEntity>();
        return NewChoice("", target, rule);
    }

    // Once per campaign start: the warning that a bandit's decision shows a global cooldown, which the game never gives
    // them (GroupAiRules.GcdSeenLine). It has its own once flag and goes past the shared warning cap (Warn), so it shows
    // even when the cap is spent; it changes no decision.
    private static void NoteGlobalCooldown(Situation s)
    {
        if (!GroupAiRules.GcdSeenDue(s.GlobalCooldown, gcdSeenWritten)) return;
        gcdSeenWritten = true;
        DelversHost.Warning(GroupAiRules.GcdSeenLine);
    }

    // Whether the switch "basic attack only" works now: its setting is on and the loaded campaign is a marked test campaign
    // (the campaign is bound after the start, so this is asked at the decision). The first time it works, its start line is written.
    private static bool BasicOnlyNow()
    {
        if (!basicOnlySetting) return false;
        var data = CoreSaveStore.Current;
        var active = GroupAiRules.BasicOnlyActive(true, data != null && CampaignPresence.IsTestCampaign(data.Campaign));
        if (active && !basicOnlyLogged)
        {
            basicOnlyLogged = true;
            Log(GroupAiRules.BasicOnlyStartLine);
        }
        return active;
    }

    // Like GroupAiSituation's slot check, with the skill's natural target: the lowest bandit for an
    // ally skill, the unit itself for a self skill, else the intended hostile.
    private static string StateOf(Situation s, SkillComponent skills, Slot slot)
    {
        try
        {
            if (slot.Data == null) return "nodata";
            if (skills == null || !skills.HasSkill(slot.Key)) return "noskill";
            if (skills.HasCooldown(slot.Key))
            {
                try { slot.CooldownLeft = skills.GetCooldown(slot.Key); }
                catch (Exception) { slot.CooldownLeft = 0f; }
                return "cd";
            }
            slot.Skill = CombatDataManager.Instance?.GetActiveSkillData(slot.Key);
            if (slot.Skill == null) return "noactive";
            var ally = slot.Data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly;
            var context = SkillExecutionContext.Convert(
                target: ally
                    ? (s.LowestAlly ?? s.Self).Unit.Cast<IEntity>()
                    : slot.Data.TargetType != TargetType.SelfTarget ? s.Intended : s.Self.Unit.Cast<IEntity>(),
                publisher: slot.Skill.Cast<IExecutionPublisher>(),
                user: s.Unit.Cast<IEntity>(),
                useItem: null);
            var result = slot.Skill.CheckForUseSkill(s.InMap, context, execeptCooldown: true);
            return result == ExecutionCheckResult.Successed ? "free" : "check:" + result.ToString().Replace("Failed_", "");
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_SLOT_FAILED slot=" + slot.Key + ": " + ex.Message);
            return "err";
        }
    }

    private static double GameNow() => Tick() / WoundRules.TicksPerSecond;

    private static bool BuffRunning(Situation s, Slot slot)
        => buffUntil.TryGetValue(s.Guid + "|" + slot.Key, out var until) && GroupAiRules.BuffRunning(until, GameClock.Now, slot.Data?.Duration ?? 0f);

    private static string TargetText(Situation s, IEntity target)
        => target == null ? "-" : target.Pointer == s.Unit.Pointer ? "self" : target.Guid.ToString();
}
