using System.Globalization;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the herbalist: her order of decisions (Healer), Lifebloom and her answers when attacked
// (SafeBasic, Guarded). Her moves (the step away, the vines, the heal path, moving up to support) are in
// BanditHealerMoves.
internal static partial class GroupAiBandits
{
    internal const int KiteDistance = 3;

    internal const float SelfHealShare = 0.6f;

    // The herbalist heals only below this share (bandits.healer.heal-below).
    private static double healBelow = GroupAiRules.HealerHealBelowDefault;

    internal const string LifebloomSkill = BanditKitKeys.Lifebloom;
    internal const double LifebloomRefreshBelow = 3.0;
    internal const int LifebloomMaxStacks = 3;

    // Lifebloom helps while the target has fewer than full stacks or they are about to run out.
    private static bool LifebloomUseful(Figure target)
    {
        try
        {
            if (!Lifebloom.BuffOn(CoreGame.GuidOf(target.Unit), out var stacks, out var secondsLeft)) return true;
            return stacks < LifebloomMaxStacks || secondsLeft <= LifebloomRefreshBelow;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static Choice Healer(Situation s)
    {
        // Test tool first (DelversCoreRuntime.PlantBanditSeedForTest).
        var test = TestSeed(s);
        if (test != null) return test;
        var formation = Formation(s);
        if (formation != null) return formation;
        // (a): a hostile within KiteDistance: vines and a step away first, Lifebloom
        // on herself after that and one stack only; attacked from farther: the stack first, then away.
        var closest = s.ClosestHostile;
        var near = closest != null && GroupAiRules.HealerKitesFirst(closest.Distance, KiteDistance);
        var first = near ? Kite(s) : Prebloom(s);
        var chosen = first ?? (near ? Prebloom(s) : Kite(s));
        if (chosen != null)
        {
            Lifebloom.BuffOn(CoreGame.GuidOf(s.Self.Unit), out var stacks, out _);
            Log(GroupAiRules.HealerOrderLine(s.Guid, near, closest?.Distance ?? -1, s.HealerAttacker != null, chosen.Rule, stacks, Tick()));
            return chosen;
        }
        // Lifebloom only: the group stays alive through it and through keeping distance (the
        // healing touch works against that). The lowest of the group below EmergencyShare first,
        // herself included, then the lowest below heal-below (0.9); never on anyone above it, in exact range.
        var lowest = s.LowestAlly;
        if (lowest == null || s.Self.Share < lowest.Share) lowest = s.Self;
        // A comrade below EmergencyShare next to her own seed: the seed grows first (Life Burst).
        var burst = HealerSeedUpgrade(s, burstOnly: true);
        if (burst != null) return burst;
        if (lowest.Share < EmergencyShare)
        {
            var emergency = SingleAlly(s, LifebloomSkill, lowest, "healer-emergency-lifebloom", withRange: true, exact: true);
            if (emergency != null) return emergency;
        }
        if (GroupAiRules.HealerHeals(lowest.Share, healBelow))
        {
            var heal = SingleAlly(s, LifebloomSkill, lowest, "healer-lifebloom", withRange: true, exact: true);
            if (heal != null) return heal;
        }
        // Her own seed into the mucus sprayer shortly before it ends, then a seed next to comrades below 0.9.
        var seedUpgrade = HealerSeedUpgrade(s);
        if (seedUpgrade != null) return seedUpgrade;
        var seed = HealerSeed(s);
        if (seed != null) return seed;
        // A comrade in need out of reach with a heal free: she walks up first, before any attack.
        var path = HealPathMove(s, lowest, closest);
        if (path != null) return path;
        // Fallen units only when she may finish them (GroupAiRules.HealerMayFinish); otherwise they are
        // no targets for her, even while the band may finish them.
        if (s.FinishReason != null && s.Downed != null && s.FinishReason != "always")
        {
            var allyBelow = GroupAiRules.HealerHeals(lowest.Share, healBelow);
            var mayFinish = GroupAiRules.HealerMayFinish(s.FinishReason == "all-down", false, allyBelow, SupportMove(s, closest, execute: false) != null);
            if (!mayFinish)
            {
                s.Hostiles.RemoveAll(s.Downed.Contains);
                closest = s.ClosestHostile;
            }
        }
        // A hostile next to her: the answers from where she stands (Guarded, with 's taunt rules).
        if (closest != null && closest.Distance <= 1) return Guarded(s, closest);
        // No Fire Missile any more; her basic attack (the green staff) when it is safe.
        var basic = SafeBasic(s, closest);
        if (basic != null) return basic;
        // (bound aggro, she follows the fight): safe and away from the band, she moves up.
        var support = SupportMove(s, closest, execute: true);
        if (support != null) return support;
        // (healer-guarded walked her into melee and held her 3.6 s): attacked or with a
        // hostile near she answers from where she stands or holds (Guarded).
        if (closest != null && (s.HealerAttacker != null || closest.Distance <= KiteDistance)) return Guarded(s, closest);
        return Hold();
    }

    // Her basic attack when no hostile stands within HealerBasicSafeCells and a target is in exact range: her attacker,
    // then the bandits' focus, then the nearest capable unit; taunted only the taunter (GroupAiRules.HealerBasicPick).
    // Fallen units are no hostiles while they are skipped. It is no reason to walk up (HealerKeepsAway stays).
    private static Choice SafeBasic(Situation s, Figure closest)
    {
        if (closest == null || !GroupAiRules.HealerBasicSafe(closest.Distance)) return null;
        var range = s.Find(s.BasicAttack)?.Data?.Range ?? 5;
        var ordered = new List<(string Guid, int Distance)>();
        if (s.Taunter != null)
            ordered.Add((s.Taunter.Guid, s.Taunter.Distance));
        else
        {
            if (s.HealerAttacker != null) ordered.Add((s.HealerAttacker.Guid, s.HealerAttacker.Distance));
            var focus = s.Focus != null ? s.Hostiles.Find(h => h.Guid == s.Focus.Guid.ToString()) : null;
            if (focus != null) ordered.Add((focus.Guid, focus.Distance));
            ordered.Add((closest.Guid, closest.Distance));
        }
        var pick = GroupAiRules.HealerBasicPick(ordered, (int)range);
        var target = pick != null ? s.Hostiles.Find(h => h.Guid == pick) : null;
        if (target == null) return null;
        return NewChoice("", target.Unit.Cast<IEntity>(), "healer-basic-safe");
    }

    // The healer attacked or with a hostile near and nothing else to do: a step away or a basic attack only on a
    // hostile next to her, else she holds (GroupAiRules.HoldRule). Taunted she neither runs nor attacks anyone but the
    // taunter (GroupAiRules.GuardedAction).
    private static Choice Guarded(Situation s, Figure closest)
    {
        var taunted = s.Taunter != null;
        if (closest != null && closest.Distance <= 1)
        {
            // No flee skill any more; Core's own move to the escape cell (EscapeRules).
            var escape = !taunted && !GroupAiMoveWatch.IsHeld(s.Unit) && GroupAiReachRules.KiteAllowed(s.Hostiles.Count - DownedAmongHostiles(s)) ? KiteTile(s, closest) : null;
            var action = GroupAiRules.GuardedAction(taunted, taunted && closest.Guid == s.Taunter.Guid, escape.HasValue);
            if (action == "run" && GroupAiBanditMoves.TryOrder(s.Unit, escape.Value, "kite"))
            {
                kiteSince[s.Guid] = GameClock.Now;
                return NewChoice("", null, "healer-escape");
            }
            if (action == "run") action = "basic";
            if (action == "basic")
            {
                return NewChoice("", closest.Unit.Cast<IEntity>(), "healer-guarded");
            }
        }
        // Nothing else left (no step away, no vines, no flee): Lifebloom on herself before holding.
        var bloom = s.Find(LifebloomSkill);
        if (bloom != null && bloom.Free && s.Self.Share < SelfHealShare && LifebloomUseful(s.Self))
        {
            return NewChoice(bloom.Key, s.Self.Unit.Cast<IEntity>(), "healer-guarded-bloom");
        }
        return Hold();
    }

    // She stands and decides again soon (GroupAiRules.HoldRule; CoreUnitCombat gives the game nothing).
    private static Choice Hold() => NewChoice("", null, GroupAiRules.HoldRule);

    // Lifebloom on itself as soon as the healer is attacked, one stack only (GroupAiRules.PrebloomAllowed), and
    // only while no hostile stands within KiteDistance (GroupAiRules.SelfBloomAllowed).
    private static Choice Prebloom(Situation s)
    {
        if (!ProtectHealer || s.HealerAttacker == null) return null;
        // No Lifebloom at or above heal-below, also not ahead of time on herself.
        if (!GroupAiRules.HealerHeals(s.Self.Share, healBelow)) return null;
        if (!GroupAiRules.SelfBloomAllowed(s.ClosestHostile?.Distance ?? int.MaxValue, KiteDistance)) return null;
        var bloom = s.Find(LifebloomSkill);
        if (bloom == null || !bloom.Free) return null;
        Lifebloom.BuffOn(CoreGame.GuidOf(s.Self.Unit), out var stacks, out _);
        if (!GroupAiRules.PrebloomAllowed(stacks)) return null;
        return NewChoice(bloom.Key, s.Self.Unit.Cast<IEntity>(), "healer-prebloom");
    }

    // exact: the skill's range without the game's step closer (the herbalist never walks up to heal).
    private static Choice SingleAlly(Situation s, string key, Figure ally, string rule, bool withRange, bool exact = false)
    {
        var slot = s.Find(key);
        if (slot == null || slot.Data == null || !slot.Free || ally == null) return null;
        if (withRange && (exact ? ally.Distance > slot.Data.Range : !GroupAiRules.InRange(ally.Distance, slot.Data.Range))) return null;
        if (key == LifebloomSkill && !LifebloomUseful(ally)) return null;
        Log(string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_HEAL unit={0} skill={1} target={2} share={3:0.00} rule={4}",
            s.Guid, GroupAiRules.Short(key), ally.Guid == s.Guid ? "self" : ally.Guid, ally.Share, rule));
        return NewChoice(key, ally.Unit.Cast<IEntity>(), rule);
    }
}
