using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: protected targets, the guard, the slowing ring and the healer's protection.
internal static class GroupAiProtectionSection
{
    internal static void Run()
    {
        FallenPlayerUnits();
        ProtectedTarget();
        HealerAimOrder();
        GuardChainTaunt();
        SlowingRing();
        HealerProtection();
        HerbalistReacts();
        ShieldUpOutsideBoss();
    }

    static void FallenPlayerUnits()
    {
        // Fallen player units (spec appendix B): attacked only once none capable is left or near.
        Check(GroupAiRules.FinishReason(0, 0) == "all-down" && GroupAiRules.FinishReason(2, 0) == "fled" && GroupAiRules.FinishReason(2, 1) == null
                && GroupAiRules.ChaseCells(null) == 8 && GroupAiRules.ChaseCells(0.0) == 1 && GroupAiRules.ChaseCells(99.0) == 30
                && GroupAiRules.SkipDownedLine("b", "f", "t", 5) == "CORE_AI_BANDIT_SKIP_DOWNED unit=b downed=f to=t tick=5"
                && GroupAiRules.FinishLine("b", "f", "fled", 6) == "CORE_AI_BANDIT_FINISH unit=b target=f reason=fled tick=6",
            "bandits: fallen units only once all are down or no capable one stands within 8 cells");
        {
            Func<double> downSix = () => 6.0;
            var downNew = GroupAiRules.LookAtTaunt(null, "F", 10.0, downSix, 0.5, false, null, ownPull: false, forcedDowned: true);
            var downLater = GroupAiRules.LookAtTaunt(downNew.Track, "F", 10.2, downSix, 0.5, false, null, forcedDowned: true);
            var downOwn = GroupAiRules.LookAtTaunt(new GroupAiRules.TauntTrack { By = "F", Own = true }, "F", 10.0, downSix, 0.5, false, null, forcedDowned: true);
            var downOwnNew = GroupAiRules.LookAtTaunt(null, "F", 10.0, downSix, 0.5, false, null, ownPull: true, forcedDowned: true);
            var upAgain = GroupAiRules.LookAtTaunt(null, "F", 10.0, downSix, 0.5, false, null);
            Check(downNew.Started && downNew.End == "downed" && downNew.Release && downNew.Track.Ended
                    && downLater.End == null && downLater.Release && !downLater.Started
                    && downOwn.End == "downed" && downOwn.Release && !downOwnNew.Own && downOwnNew.End == "downed"
                    && upAgain.Started && upAgain.End == null && !upAgain.Release,
                "taunt: a taunt by a fallen unit ends at once and Core takes it off, the chain's own one too");
        }
    }

    static void ProtectedTarget()
    {
        // One protected target for all bandits: the same pick whatever the order, held 1 s.
        var pa = ("A", 1f, 2, 0f, true);
        var pb = ("B", 1f, 2, 0f, true);
        var pc = ("C", 0.8f, 5, 0f, true);
        var pd = ("D", 1f, 1, 30f, false);
        var pe = ("E", 0.5f, 1, 0f, false);
        var pf = ("F", 1f, 1, 0f, true);
        // C aims at the healer from 5 cells without a hit and is no threat any more (was the pick by least health).
        Check(GroupAiRules.ProtectPick(new[] { pa, pb }) == "A" && GroupAiRules.ProtectPick(new[] { pb, pa }) == "A"
                && GroupAiRules.ProtectPick(new[] { pa, pb, pc }) == "A" && GroupAiRules.ProtectPick(new[] { pb, pc, pa }) == "A"
                && GroupAiRules.ProtectPick(new[] { pa, pf }) == "F"
                && GroupAiRules.ProtectPick(new[] { pa, pc, pd }) == "D"
                && GroupAiRules.ProtectPick(new[] { pe }) == null
                && GroupAiRules.ProtectPick(Array.Empty<(string, float, int, float, bool)>()) == null
                && GroupAiRules.KeepProtect("A", 11.0, 10.5, true) && !GroupAiRules.KeepProtect("A", 11.0, 11.0, true)
                && !GroupAiRules.KeepProtect("A", 11.0, 10.5, false) && !GroupAiRules.KeepProtect(null, 11.0, 10.5, true)
                && GroupAiRules.ProtectHoldSeconds == 1.0,
            "bandits: one protected target for all (damage, then nearest, least health, lowest id), held 1 s while it threatens");
    }

    static void HealerAimOrder()
    {
        // (Frieren picked by=targets dmg3s=0 from 7 cells and fell in 10 of 10 runs): aiming at the
        // healer counts only within 3 cells, a hit at any distance; without hits the nearest goes first, then the least health.
        var frieren = ("frieren", 0.6f, 7, 0f, true);
        var near3 = ("near3", 1f, 3, 0f, true);
        var far4 = ("far4", 1f, 4, 0f, true);
        var bow = ("bow", 1f, 8, 40f, false);
        var close = ("close", 0.9f, 1, 0f, true);
        var weak = ("weak", 0.2f, 3, 0f, true);
        var hitNear = ("hitNear", 1f, 2, 50f, false);
        var hitFar = ("hitFar", 1f, 6, 50f, false);
        Check(!GroupAiRules.ProtectThreat(0f, true, 7) && GroupAiRules.ProtectThreat(0f, true, 3) && !GroupAiRules.ProtectThreat(0f, true, 4)
                && GroupAiRules.ProtectThreat(40f, false, 8) && !GroupAiRules.ProtectThreat(0f, false, 1) && GroupAiRules.ProtectTargetsReach == 3
                && GroupAiRules.ProtectPick(new[] { frieren }) == null
                && GroupAiRules.ProtectPick(new[] { near3, far4 }) == "near3" && GroupAiRules.ProtectPick(new[] { far4 }) == null
                && GroupAiRules.ProtectPick(new[] { frieren, bow }) == "bow"
                && GroupAiRules.ProtectPick(new[] { weak, close }) == "close"
                && GroupAiRules.ProtectPick(new[] { hitFar, hitNear }) == "hitNear"
                && GroupAiRules.ProtectLine("h", "bow", "dmg3s", 40f, 1, 8, 5) == "CORE_AI_BANDIT_PROTECT healer=h target=bow by=dmg3s dmg3s=40 hitters=1 dist=8 tick=5",
            "bandits: protecting the healer from an aim only within 3 cells, from a hit at any distance; the nearest first");
    }

    static void GuardChainTaunt()
    {
        // The chain's own taunt on the guard: no player taunt, Core leaves it to the game.
        Func<double> chainTwo = () => 2.0;
        var chainOwn = GroupAiRules.LookAtTaunt(null, "P", 10.0, chainTwo, 0.5, false, null, ownPull: true);
        var chainLater = GroupAiRules.LookAtTaunt(chainOwn.Track, "P", 11.5, chainTwo, 0.5, false, "loss");
        var chainGone = GroupAiRules.LookAtTaunt(chainLater.Track, null, 12.0, chainTwo, 0.5, false, null);
        var chainPlayer = GroupAiRules.LookAtTaunt(chainOwn.Track, "Q", 11.0, () => 6.0, 0.5, false, null);
        Check(chainOwn.Own && chainOwn.Track.Own && !chainOwn.Started && !chainOwn.Release && chainOwn.End == null
                && !chainLater.Own && !chainLater.Started && !chainLater.Release && chainLater.End == null && chainLater.Track.Own
                && chainGone.Track == null && chainGone.End == null && !chainGone.Release
                && chainPlayer.Started && !chainPlayer.Track.Own && chainPlayer.Track.Until == 14.0
                && GroupAiRules.IsOwnPull("P", "P", 15.0, 15.0) && !GroupAiRules.IsOwnPull("P", "P", 15.0, 15.1)
                && !GroupAiRules.IsOwnPull("Q", "P", 15.0, 10.0) && !GroupAiRules.IsOwnPull(null, "P", 15.0, 10.0)
                && GroupAiRules.TauntOwnLine("u", "P", 9) == "CORE_AI_BANDIT_TAUNT_OWN unit=u by=P skill=ChainGrap tick=9",
            "taunt: the chain's own taunt on the guard is no player taunt; Core neither shortens nor ends it, a player taunt still counts");
    }

    static void SlowingRing()
    {
        // The guard's slowing ring: Guardian's Taunt slows instead of forcing players.
        Check(GroupAiRules.SlowRingReason(true, 1, 2, true) == "protect" && GroupAiRules.SlowRingReason(true, 2, 2, false) == "group"
                && GroupAiRules.SlowRingReason(true, 1, 2, false) == null && GroupAiRules.SlowRingReason(false, 4, 2, true) == null
                && GroupAiRules.SlowRingReason(true, 0, 1, false) == null
                && GroupAiRules.Manhattan(0, 0, 3, 2) == 5 && GroupAiRules.Manhattan(0, 0, 3, 3) == 6 && GroupAiRules.Manhattan(4, -1, 1, 1) == 5
                && GroupAiRules.SlowSeconds(null) == 4.0 && GroupAiRules.SlowSeconds(0.1) == 0.5 && GroupAiRules.SlowSeconds(99.0) == 30.0
                && GroupAiRules.SlowMinTargets(null) == 2 && GroupAiRules.SlowMinTargets(0.0) == 1 && GroupAiRules.SlowMinTargets(2.6) == 3
                && GroupAiRules.SlowMinTargets(10.0) == 6
                && GroupAiRules.SlowRingLine("u", 3, 4.0, 77) == "CORE_AI_BANDIT_SLOW_RING unit=u targets=3 effect=AFFECTER_Slow seconds=4.0 tick=77"
                && GroupAiRules.SlowRingLine("u", 2, 4.0, 77, new[] { "a", "b" }, 1) == "CORE_AI_BANDIT_SLOW_RING unit=u targets=2 effect=AFFECTER_Slow seconds=4.0 tick=77 units=a+b renewed=1"
                && GroupAiRules.SlowRingLine("u", 0, 4.0, 77, new string[0], 0).EndsWith(" units=- renewed=0")
                && CoreUnitRules.Units.First(unit => unit.Role == "guard").Skills.Contains(GroupAiRules.SlowRingSkill)
                && !CoreUnitRules.Units.First(unit => unit.Role == "healer").Skills.Contains(GroupAiRules.SlowRingSkill),
            "bandits: the guard rings players near the healer's attacker or in a group, Manhattan 5, the game's slow for 4 s; the healer has no ring");
        Check(GroupAiRules.TauntAllows(true, "heal-ally") && GroupAiRules.TauntAllows(true, "self") && GroupAiRules.TauntAllows(true, "attack-taunter")
                && !GroupAiRules.TauntAllows(true, "move") && !GroupAiRules.TauntAllows(true, "attack-other")
                && !GroupAiRules.TauntAllows(false, "heal-ally") && !GroupAiRules.TauntAllows(false, "move") && GroupAiRules.TauntAllows(false, "attack-taunter")
                && GroupAiRules.TauntedLine("u", "t", 6.0, 0.5, 3.0, 12) == "CORE_AI_BANDIT_TAUNTED unit=u by=t native=6.0 factor=0.50 until=3.0 tick=12"
                && GroupAiRules.TauntEndLine("u", "t", "early", null, 13) == "CORE_AI_BANDIT_TAUNT_END unit=u by=t cause=early detail=- tick=13"
                && GroupAiRules.TauntEndLine("u", "t", "healer_drop", "below", 14) == "CORE_AI_BANDIT_TAUNT_END unit=u by=t cause=healer_drop detail=below tick=14",
            "taunt: a taunted healer heals on but does not run; nobody moves or attacks anyone but the taunter");
    }

    static void HealerProtection()
    {
        // Taunted, the healer took the flee skill against the taunter next to her.
        Check(GroupAiRules.GuardedAction(false, false, true) == "run" && GroupAiRules.GuardedAction(false, false, false) == "basic"
                && GroupAiRules.GuardedAction(true, true, true) == "basic" && GroupAiRules.GuardedAction(true, true, false) == "basic"
                && GroupAiRules.GuardedAction(true, false, true) == "hold" && GroupAiRules.GuardedAction(true, false, false) == "hold",
            "taunt: with a hostile next to her the taunted healer never runs, strikes only the taunter and otherwise holds");
        Check(GroupAiRules.HoldEndLine("players", "u", "SKILL_DestructiveSplit", true, 1.25, 4200)
                    == "CORE_AI_HOLD_END side=players unit=u skill=DestructiveSplit result=free waited=1.3 tick=4200"
                && GroupAiRules.AnnouncedLine("bandits", "u", "SKILL_ArrowRain", null, 7) == "CORE_AI_ANNOUNCED_CAST side=bandits unit=u skill=ArrowRain tick=7"
                && GroupAiRules.AnnouncedLine("players", "u", "SKILL_Slay", "range", 8) == "CORE_AI_ANNOUNCED_DROPPED side=players unit=u skill=Slay reason=range tick=8"
                && GroupAiRules.Status(true, 1, 2, 3, 4, 0, 0, 0, 1f, 0, 0, 5, 6, new Dictionary<string, int>()).Contains("announcedCast=5 announcedDropped=6 rules=-"),
            "group AI: the hold, redeem and drop lines carry side, unit, skill and the game tick; the status counts both");
        // The bandits' healer and her protection, the party's shield wall.
        Check(GroupAiRules.HealerKitesFirst(1, 3) && GroupAiRules.HealerKitesFirst(3, 3) && !GroupAiRules.HealerKitesFirst(4, 3)
                && GroupAiRules.PrebloomAllowed(0) && !GroupAiRules.PrebloomAllowed(1) && !GroupAiRules.PrebloomAllowed(3),
            "healer: a hostile within 3 cells means vines and a step first; Lifebloom on herself only one stack");
    }

    static void HerbalistReacts()
    {
        // The herbalist reacts when someone walks up to her and kites on her own.
        Check(GroupAiRules.SelfBloomAllowed(4, 3) && !GroupAiRules.SelfBloomAllowed(3, 3) && !GroupAiRules.SelfBloomAllowed(1, 3)
                && GroupAiRules.SelfBloomAllowed(int.MaxValue, 3) && GroupAiRules.SelfBloomAllowed(2, 1) && !GroupAiRules.SelfBloomAllowed(1, 1)
                && GroupAiRules.HealerKeepsAway(true) && !GroupAiRules.HealerKeepsAway(false)
                && GroupAiRules.HealerBreaksMove(true, 1) && !GroupAiRules.HealerBreaksMove(false, 1) && !GroupAiRules.HealerBreaksMove(true, 2)
                && GroupAiRules.KiteAgainNow(true, 5) && GroupAiRules.KiteAgainNow(false, 1) && !GroupAiRules.KiteAgainNow(false, 2)
                && GroupAiRules.HoldsStill(GroupAiRules.HoldRule) && !GroupAiRules.HoldsStill("healer-guarded") && !GroupAiRules.HoldsStill(null)
                && GroupAiRules.PullPeelsHealer(3, 5) && GroupAiRules.PullPeelsHealer(1, 2) && !GroupAiRules.PullPeelsHealer(4, 6)
                && !GroupAiRules.PullPeelsHealer(2, 2) && !GroupAiRules.PullPeelsHealer(3, 1)
                && GroupAiRules.GuardComboSeconds > 5.0,
            "healer: Lifebloom on herself only without a hostile within 3 cells, never the game's step toward a hostile, a stuck order breaks, no walk into melee; the chain only to pull someone within 3 cells away from her");
        {
            var hits = new List<(string Attacker, float Amount, float At)>
            {
                ("archer", 400f, 9.5f), ("mage", 115f, 9.8f), ("mage", 115f, 10.4f), ("archer", 250f, 10.9f), ("old", 2000f, 6.0f)
            };
            var top = GroupAiRules.TopAttacker(hits, 11f, GroupAiRules.ProtectWindowSeconds, out var dealt, out var hitters);
            var late = GroupAiRules.TopAttacker(hits, 13.2f, GroupAiRules.ProtectWindowSeconds, out var lateDealt, out _);
            var none = GroupAiRules.TopAttacker(hits, 30f, GroupAiRules.ProtectWindowSeconds, out var noDamage, out var noHitters);
            var tie = GroupAiRules.TopAttacker(new List<(string Attacker, float Amount, float At)> { ("b", 100f, 1f), ("a", 100f, 1f) }, 1f, 3f, out _, out _);
            Check(top == "archer" && dealt == 650f && hitters == 2 && late == "archer" && lateDealt == 250f
                    && none == null && noDamage == 0f && noHitters == 0 && tie == "a"
                    && GroupAiRules.ProtectLine("h", "archer", "dmg3s", 650f, 2, 4, 77) == "CORE_AI_BANDIT_PROTECT healer=h target=archer by=dmg3s dmg3s=650 hitters=2 dist=4 tick=77",
                "protect: chain and counter-attack go for the most damage on the healer in the last 3 s; older hits drop out");
        }
        Check(GroupAiRules.ShieldWallReason(true, true, false) == "boss" && GroupAiRules.ShieldWallReason(true, false, false) == "leader"
                && GroupAiRules.ShieldWallReason(false, false, true) == "elite" && GroupAiRules.ShieldWallReason(true, false, true) == "leader"
                && GroupAiRules.ShieldWallReason(false, true, false) == null
                && GroupAiRules.ShieldWallLine("s", "elite", "e", 0.42f, 5) == "CORE_AI_SHIELDWALL unit=s reason=elite enemy=e hp=0.42 tick=5"
                && GroupAiRules.HealerOrderLine("h", true, 1, true, "kite-vines", 0, 9) == "CORE_AI_BANDIT_HEALER_ORDER unit=h near=true closest=1 attacked=true rule=kite-vines stacks=0 tick=9",
            "shield wall: the boss or leader on the shield as before, now also an elite enemy on it; the reason is logged");
    }

    static void ShieldUpOutsideBoss()
    {
        // Could not fail, nothing raised shieldUpOutsideBoss.
        Check(!GroupAiRules.ShieldUpOutsideBoss("SKILL_ShieldUp", "boss") && !GroupAiRules.ShieldUpOutsideBoss("SKILL_ShieldUp", "elite")
                && GroupAiRules.ShieldUpOutsideBoss("SKILL_ShieldUp", "leader") && GroupAiRules.ShieldUpOutsideBoss("SKILL_ShieldUp", null)
                && !GroupAiRules.ShieldUpOutsideBoss("SKILL_GuardiansTaunt", null)
                && GroupAiRules.ShieldUpOutsideLine("s", null, 7) == "CORE_AI_SHIELDUP_OUTSIDE unit=s reason=- tick=7",
            "shield wall: a shield wall without a boss or an elite on the unit counts as outside the boss, from Core or from the game");
    }
}
