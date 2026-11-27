using DungeonSettlersDelvers.Core;
using static DungeonSettlersDelvers.Core.GroupAiChaseRules;
using static TestKit;

// The group AI's movement (GroupAiChaseRules): the walk toward a target by role, the idle gather without a formation.
internal static class GroupAiChaseSection
{
    internal static void Run()
    {
        Check(Cells(null, ChaseDefault) == 6 && Cells(null, GatherDefault) == 4 && Cells(0, ChaseDefault) == 1 && Cells(99, GatherDefault) == 20,
            "chase: 6 cells of chase and 4 of gather by default, settings within 1 to 20");
        Check(Hold(GroupAiRole.Melee, true, 1, 6, 6, false) == null && Hold(GroupAiRole.Shield, true, 1, 7, 6, false) == "far"
                && Hold(GroupAiRole.Melee, true, 1, 7, 6, false) == "far",
            "chase: melee and shield walk to a foe up to 6 cells from the group's middle, not farther");
        Check(Hold(GroupAiRole.Ranged, true, 5, 3, 6, false) == null && Hold(GroupAiRole.Caster, true, 1, 2, 6, false) == "ranged-melee"
                && Hold(GroupAiRole.Ranged, true, 5, 8, 6, false) == "far",
            "chase: ranged and casters walk only into reach of a ranged skill, never into melee, and not beyond the chase cells");
        // Holding every walk of a heal role stopped the support water mage and the seed at the focus.
        Check(Hold(GroupAiRole.Heal, true, 1, 1, 6, false) == "healer-melee" && Hold(GroupAiRole.Heal, true, 5, 3, 6, false) == null
                && Hold(GroupAiRole.Heal, true, 5, 8, 6, false) == "far" && Hold(GroupAiRole.Heal, false, 5, 9, 6, false) == null
                && Hold(GroupAiRole.Melee, false, 1, 9, 6, false) == null,
            "chase: a heal role walks toward a foe only into reach of a ranged skill, never into melee; a walk to a comrade stays as before");
        // The rain on a target in reach died at the move step until the target's fromMiddle fell to 6.
        Check(Hold(GroupAiRole.Melee, true, 1, 14, 6, true) == null && Hold(GroupAiRole.Shield, true, 1, 14, 6, true) == null
                && Hold(GroupAiRole.Ranged, true, 7, 14, 6, true) == null && Hold(GroupAiRole.Caster, true, 5, 14, 6, true) == null
                && Hold(GroupAiRole.Heal, true, 5, 14, 6, true) == null,
            "chase: a target in the skill's reach is never held, however far from the group's middle");
        // (the group stands, the player orders the advance):, every attack order held as far.
        Check(Hold(GroupAiRole.Shield, true, 1, 13, 6, false, true) == null && Hold(GroupAiRole.Caster, true, 1, 2, 6, false, true) == null
                && Hold(GroupAiRole.Heal, true, 1, 1, 6, false, true) == null && Hold(GroupAiRole.Ranged, true, 7, 13, 6, false, true) == null
                && Hold(GroupAiRole.Shield, true, 1, 13, 6, false, false) == "far" && Hold(GroupAiRole.Caster, true, 1, 2, 6, false, false) == "ranged-melee",
            "chase: the player's order lifts far, ranged-melee and healer-melee");
        var order = NextOrder(null, true, OrderTarget(true, true, true, "foe"), false, false, false);
        Check(Ordered(order, "foe") && !Ordered(order, "other") && !Ordered(null, "foe"), "chase: only the ordered target is let through, another stays held");
        Check(OrderTarget(true, true, true, "foe") == "foe" && OrderTarget(true, false, true, "foe") == null && OrderTarget(true, true, false, "ally") == null,
            "chase: an order is the player's attack task on a foe; a move or a task on a comrade is none");
        Check(OrderTarget(false, true, true, "foe") == null, "chase: a taunt's forced target is no order");
        Check(NextOrder(order, true, null, false, false, false) == null && NextOrder(Ordering(), true, "other", false, false, false)?.Target == "other",
            "chase: a new order of the player replaces the old, a move ends it");
        Check(NextOrder(Ordering(), false, null, true, false, false) == null, "chase: the order ends with its target's death");
        var fought = NextOrder(Ordering(), false, null, false, true, false);
        Check(fought.Fought && NextOrder(fought, false, null, false, false, true) == null && NextOrder(Ordering(), false, null, false, false, true)?.Target == "foe",
            "chase: the order ends with the end of a fight the member fought, not before its fight began");
        // Out of a fight the formation let a hand go after 5 s and fetched the member back from its order.
        Check(GroupAiFormationRules.IdleHandRelease(true, false, 60f) == null && GroupAiFormationRules.IdleHandRelease(true, true, 0f) == null
                && GroupAiFormationRules.IdleHandRelease(false, true, 0f) == "foe-gone" && GroupAiFormationRules.IdleHandRelease(false, false, 5f) == "no-fight"
                && GroupAiFormationRules.IdleHandRelease(false, false, 4.9f) == null,
            "chase: out of a fight a standing order keeps the hand; without one it goes with its foe or after 5 s as before");
        Check(GroupAiFormationRules.HeldByPlayer(false, true) && GroupAiFormationRules.HeldByPlayer(true, false) && !GroupAiFormationRules.HeldByPlayer(false, false),
            "chase: a member with a standing order is not free for the formation, as one placed by hand");
        Check(GroupAiRoleRules.GathersBack(true, false, false) && !GroupAiRoleRules.GathersBack(true, false, true) && !GroupAiRoleRules.GathersBack(true, true, false)
                && !GroupAiRoleRules.GathersBack(false, false, false),
            "chase: a gathering leader is not sent back while the player's order of it stands");
        // One remembered pointer per unit read Core's first of two orders in a tick as the player's.
        var own = new OwnTasks();
        own.Add(new IntPtr(11), 5f);
        own.Add(new IntPtr(12), 5f);
        Check(own.Take(new IntPtr(11), 5f) && own.Take(new IntPtr(12), 5.02f) && !own.Take(new IntPtr(11), 5.02f) && !own.Take(new IntPtr(13), 5.02f)
                && !own.Take(IntPtr.Zero, 5.02f),
            "chase: both of Core's orders in one tick are Core's; each is taken out once it passed, a later task is the player's");
        for (var i = 1; i <= OwnTasks.Cap + 1; i++) own.Add(new IntPtr(100 + i), 6f);
        Check(own.Count == OwnTasks.Cap && !own.Take(new IntPtr(101), 6f) && own.Take(new IntPtr(102), 6f) && own.Take(new IntPtr(100 + OwnTasks.Cap + 1), 6f),
            "chase: Core's own tasks keep at most their cap, the oldest goes first");
        // A pointer whose order never passed goes after a second, so the game handing it out again later is the player's.
        own.Clear();
        own.Add(new IntPtr(21), 7f);
        own.Add(new IntPtr(22), 7f);
        Check(own.Take(new IntPtr(21), 8f) && !own.Take(new IntPtr(22), 8.1f) && own.Count == 0,
            "chase: Core's own task that never passed is forgotten after a second");
        Check(Hold(GroupAiRole.Caster, true, 1, 2, 6, true) == null && Hold(GroupAiRole.Heal, true, 1, 1, 6, true) == null,
            "chase: the walk into melee is held only as a walk");
        Check(InReach(7, 7) && !InReach(8, 7) && InReach(1, 0) && !InReach(2, 1)
                && Hold(GroupAiRole.Ranged, true, 7, 8, 6, InReach(7, 7)) == null && Hold(GroupAiRole.Ranged, true, 7, 8, 6, InReach(8, 7)) == "far",
            "chase: the reach is the game's, one cell beyond it is a walk, held like any other");
        var refused = new CastRefusal { Skill = "rain", Target = "foe", At = 10f };
        Check(SkillRefused(refused, "rain", 13f) && !SkillRefused(refused, "rain", 13.1f) && !SkillRefused(refused, "shot", 11f)
                && !SkillRefused(refused, "rain", 9f) && !SkillRefused(null, "rain", 11f),
            "chase: a refused skill counts for the cancel seconds, for that skill alone");
        Check(Blocks(refused, "foe", false, 12f) && !Blocks(refused, "foe", true, 12f) && !Blocks(refused, "other", false, 12f) && !Blocks(refused, "foe", false, 14f),
            "chase: a held cast blocks its target while the skill does not reach it");
        Check(Blocks(new CastRefusal { Skill = "rain", Target = "foe", At = 10f, Stuck = true }, "foe", true, 12f),
            "chase: a stuck cast blocks its target even in reach");
        var foes = new[] { ("foe", 3), ("other", 9) };
        Check(SlotRefused(refused, "rain", 7, foes, 11f) && !SlotRefused(refused, "shot", 7, foes, 11f) && !SlotRefused(refused, "rain", 7, foes, 14f),
            "chase: a refused skill with no other foe in reach leaves the choice");
        Check(!SlotRefused(refused, "rain", 9, foes, 11f), "chase: a refused skill stays free for another foe it reaches");
        Check(Stuck(Watching()) && Watching().Decisions == 13, "chase: one choice coming back for 2 seconds without a cast is stuck");
        Check(!Stuck(Step(null, "rain", "foe", (1, 1), 0f)), "chase: a first choice is not stuck");
        CastWatch quick = null;
        for (var i = 0; i < 20; i++) quick = Step(quick, "rain", "foe", (1, 1), i * 0.05f);
        Check(!Stuck(quick), "chase: many decisions within less than 2 seconds are not stuck yet");
        CastWatch slow = null;
        for (var i = 0; i < 4; i++) slow = Step(slow, "rain", "foe", (1, 1), i * 0.9f);
        Check(!Stuck(slow) && slow.Last - slow.Since >= StuckSeconds, "chase: a few decisions over 2 seconds are not stuck");
        Check(!Stuck(Step(Watching(), "rain", "foe", (1, 2), 3.2f)) && !Stuck(Step(Watching(), "rain", "other", (1, 1), 3.2f)) && !Stuck(Step(Watching(), "shot", "foe", (1, 1), 3.2f)),
            "chase: a step, another target or another skill starts the count over");
        // A running cast or channel makes no decisions: the gap ends the count.
        Check(!Stuck(Step(Watching(), "rain", "foe", (1, 1), 4.1f)), "chase: a long cast between two choices starts the count over");
        Check(RefusalLine("u", "Ranged", "SKILL_ArrowRain", "foe", "far", 5, 7, 9, 4, 12)
                == "CORE_AI_CAST_REJECTED unit=u role=Ranged skill=SKILL_ArrowRain target=foe reason=far distance=5 range=7 fromMiddle=9 count=4 tick=12",
            "chase: the refused cast's line names skill, target, reason, cells and count");
        Check(Gather((10, 10), (14, 10), 4) == null && Gather((10, 10), (15, 10), 4) == (15, 10),
            "chase: idle without a formation the member stands within 4 cells of the middle, farther it walks back to it");
        Check(Middle(new[] { (0, 0), (4, 2), (2, 7) }) == (2, 3) && Middle(System.Array.Empty<(int, int)>()) == null,
            "chase: the middle is the members' mean cell, none without members");
        Check(ChaseLine("u", "Melee", "hold", "far", 7, 9) == "CORE_AI_CHASE unit=u role=Melee step=hold reason=far fromMiddle=7 tick=9"
                && GatherLine("u", (1, 2), (5, 6), 9) == "CORE_AI_IDLE_GATHER unit=u from=1,2 to=5,6 tick=9",
            "chase: the lines name unit, role, step, reason and cells");
    }

    // A fresh order on "foe".
    private static PlayerOrder Ordering() => NextOrder(null, true, "foe", false, false, false);

    // One choice from 1 s to 3 s of game time at cell 1,1.
    private static CastWatch Watching()
    {
        CastWatch watch = null;
        for (var i = 0; i <= 12; i++) watch = Step(watch, "rain", "foe", (1, 1), 1f + i * 2f / 12f);
        return watch;
    }
}
