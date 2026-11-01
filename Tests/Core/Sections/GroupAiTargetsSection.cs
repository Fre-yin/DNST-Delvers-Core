using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: fallen units, excluded targets and the party focus order.
internal static class GroupAiTargetsSection
{
    internal static void Run()
    {
        FallenAndExcluded();
        PartyFocusOrder();
    }

    static void FallenAndExcluded()
    {
        // Fallen last before protection: a fallen one is no protect target while it is skipped.
        Check(GroupAiRules.ProtectCandidate(true, false, false) && !GroupAiRules.ProtectCandidate(true, true, false)
                && GroupAiRules.ProtectCandidate(true, true, true) && !GroupAiRules.ProtectCandidate(false, false, false)
                && GroupAiRules.KeepProtect("A", 11.0, 10.5, true) && !GroupAiRules.KeepProtect("A", 11.0, 10.5, false),
            "bandits: a fallen unit is no protect target while fallen units are skipped; a held one that stopped threatening goes");
        // Fallen last between decisions too: turn away while a capable one stands within reach.
        Check(GroupAiRules.SkipsFallenNow(true, false, 1, 1) && !GroupAiRules.SkipsFallenNow(true, false, 1, 0)
                && !GroupAiRules.SkipsFallenNow(true, false, 0, 0) && !GroupAiRules.SkipsFallenNow(true, true, 2, 2)
                && !GroupAiRules.SkipsFallenNow(false, false, 2, 2),
            "bandits: a fallen combat target is dropped between decisions while a capable unit stands within reach");
        // A combat target excluded for tests is dropped like a fallen one; the ring counts no fallen unit.
        Check(GroupAiRules.CombatTargetAction(false, false, false) == "keep" && GroupAiRules.CombatTargetAction(false, true, false) == "skip-downed"
                && GroupAiRules.CombatTargetAction(false, true, true) == "finish" && GroupAiRules.CombatTargetAction(true, false, false) == "skip-excluded"
                && GroupAiRules.CombatTargetAction(true, true, true) == "skip-excluded"
                && GroupAiRules.SlowRingCounts(true, false, 5) && !GroupAiRules.SlowRingCounts(true, true, 1)
                && !GroupAiRules.SlowRingCounts(true, false, 6) && !GroupAiRules.SlowRingCounts(false, false, 1),
            "bandits: the Toolkit's dummy as combat target is turned away from, a fallen one too unless it may be finished; the ring counts only capable players in reach");
    }

    static void PartyFocusOrder()
    {
        // The party focuses healer, ranged, melee, guard last for physical attackers
        // (the guard counts as melee against magic); in reach first, then share, distance and the fight's seed.
        var c = (Func<string, int, float, int, bool, GroupAiRules.FocusCandidate>)((g, r, sh, d, re) =>
            new GroupAiRules.FocusCandidate { Guid = g, Rank = r, Share = sh, Distance = d, Reachable = re });
        var tieA = GroupAiRules.FocusPick(new[] { c("a", 2, 0.5f, 3, true), c("b", 2, 0.5f, 3, true) }, 42);
        var tieB = GroupAiRules.FocusPick(new[] { c("b", 2, 0.5f, 3, true), c("a", 2, 0.5f, 3, true) }, 42);
        Check(GroupAiRules.FocusRank("heal", true) == 0 && GroupAiRules.FocusRank("ranged", true) == 1 && GroupAiRules.FocusRank("melee", true) == 2
                && GroupAiRules.FocusRank("guard", true) == 3 && GroupAiRules.FocusRank("guard", false) == 2
                && GroupAiRules.EnemyRoleFrom("healer", false, false, 1) == "heal" && GroupAiRules.EnemyRoleFrom("archer", false, false, 1) == "ranged"
                && GroupAiRules.EnemyRoleFrom("guard", false, false, 1) == "guard" && GroupAiRules.EnemyRoleFrom("blade", true, false, 5) == "melee"
                && GroupAiRules.EnemyRoleFrom(null, true, true, 5) == "heal" && GroupAiRules.EnemyRoleFrom(null, false, true, 5) == "guard"
                && GroupAiRules.EnemyRoleFrom(null, false, false, 4) == "ranged" && GroupAiRules.EnemyRoleFrom(null, false, false, 1) == "melee"
                && GroupAiRules.HealsAllies(7, "SKILL_GlothHeal") && GroupAiRules.HealsAllies(8, "SKILL_ResurrectionGoblin")
                && !GroupAiRules.HealsAllies(1, "SKILL_Heal") && !GroupAiRules.HealsAllies(7, "SKILL_Buff")
                && GroupAiRules.HealerPattern("Pattern_FemaleGloth") && GroupAiRules.HealerPattern("GoblinChiefShaman") && !GroupAiRules.HealerPattern("Pattern_GoblinShaman")
                && GroupAiRules.FocusPick(new[] { c("guard", 3, 0.1f, 1, true), c("blade", 2, 0.9f, 2, true), c("healer", 0, 1f, 5, true) }, 1) == "healer"
                && GroupAiRules.FocusPick(new[] { c("healer", 0, 1f, 8, false), c("blade", 2, 0.9f, 2, true) }, 1) == "blade"
                && GroupAiRules.FocusPick(new[] { c("healer", 0, 1f, 8, false), c("guard", 3, 1f, 9, false) }, 1) == "healer"
                && GroupAiRules.FocusPick(new[] { c("x", 2, 0.8f, 2, true), c("y", 2, 0.4f, 4, true) }, 1) == "y"
                && GroupAiRules.FocusPick(new[] { c("x", 2, 0.5f, 2, true), c("y", 2, 0.5f, 4, true) }, 1) == "x"
                && tieA == tieB && tieA != null && GroupAiRules.FocusPick(null, 1) == null,
            "party focus: healer, ranged, melee, guard last for physical attackers; reachable, share, distance, then the fight's seed");
        Check(GroupAiRules.FocusReachable(false, 4, 4, 1) && !GroupAiRules.FocusReachable(false, 5, 4, 1) && !GroupAiRules.FocusReachable(false, 7, 8, 1)
                && GroupAiRules.FocusReachable(true, 4, 1, 1) && !GroupAiRules.FocusReachable(true, 5, 1, 1)
                && GroupAiRules.MeleeSwitchAllowed(false, 0.5f, false) && !GroupAiRules.MeleeSwitchAllowed(true, 0.5f, true)
                && !GroupAiRules.MeleeSwitchAllowed(false, 0.2f, false) && GroupAiRules.MeleeSwitchAllowed(false, 0.2f, true) && GroupAiRules.MeleeSwitchAllowed(false, null, false)
                && GroupAiRules.CellsBetween(0, 0, 3, 0).SequenceEqual(new[] { (1, 0), (2, 0) }) && GroupAiRules.CellsBetween(0, 0, 1, 1).Count == 0
                && GroupAiRules.FoeOnLine(0, 0, 4, 0, new[] { (2, 1) }) && !GroupAiRules.FoeOnLine(0, 0, 4, 0, new[] { (2, 3) })
                && !GroupAiRules.FoeOnLine(0, 0, 1, 0, new[] { (0, 1) })
                && GroupAiRules.FocusHolds(11.9f, 10f, true, false) && !GroupAiRules.FocusHolds(12f, 10f, true, false)
                && !GroupAiRules.FocusHolds(11f, 10f, false, false) && !GroupAiRules.FocusHolds(11f, 10f, true, true) && !GroupAiRules.FocusHolds(3f, 10f, true, false)
                && GroupAiRules.FocusLine("u", "t", null, 0, "heal", "rank", 5) == "CORE_AI_TARGET unit=u rule=focus rank=0 role=heal target=t previous=- reason=rank tick=5"
                && GroupAiRules.FocusNativeHealersKey == "groupai.focus.native-healers",
            "party focus: exact skill range, melee at most 3 cells further and not past a foe, keeps a target below 25 %, holds 2 game seconds");
    }
}
