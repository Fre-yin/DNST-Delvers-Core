using DungeonSettlersDelvers.Core;
using static DungeonSettlersDelvers.Core.BanditComboRules;
using static TestKit;

// The bandits' combos (BanditComboRules): the table, the chance, the wait for a call, the throttle and the
// class pairs of the weak groups' roll.
internal static class BanditComboSection
{
    internal static void Run()
    {
        Check(Rows.Select(r => r.Number).SequenceEqual(new[] { 1, 2, 4, 5, 6, 7, 8, 10, 11, 12 }), "combos: the table holds every combo without Explosion, a second camp or the shield");
        Check(Rows.All(r => !r.Follows.Contains("SKILL_Explosion") && !r.Starters.Contains("SKILL_Explosion")), "combos: Explosion stays out until its warning is shown");
        Check(Rows.Where(r => r.Call != BanditCallKind.None).All(r => !r.WaitsForCall || r.Window > CallDelay),
            "combos: every window outlasts the wait for the call");
        Check(Rows.Single(r => r.Number == 4) is { WaitsForCall: false, Window: 1.0 } && Rows.Single(r => r.Number == 1).WaitsForCall,
            "combos: Pommel Strike's call comes without the wait, the others with it");
        Check(Rows.Where(r => r.Alone).All(r => r.Call == BanditCallKind.None), "combos: a bandit alone does not call its own follow-up");
        Check(Of(WeakBanditKitKeys.OpeningStrike).Single().Number == 5 && Of(BanditKitKeys.DestructiveSplit).Single().Number == 10 && Of("SKILL_Unknown").Length == 0 && Of(null).Length == 0,
            "combos: a starter finds its combo, another skill none");
        Check(IsFollow(BanditKitKeys.Slay) && !IsFollow(WeakBanditKitKeys.PommelStrike) && IsFollowChoice("combo8-follow") && !IsFollowChoice("guard-slowring"),
            "combos: the follow-ups and their rules are known");
        Check(Rolls(0.29, WeightHard, 0.6f) && !Rolls(0.31, WeightHard, 0.6f) && Rolls(0.79, WeightFocus, 1f) && !Rolls(0.8, WeightFocus, 1f),
            "combos: weight times tendency gives the chance");
        Check(Rows.Where(r => !MayStart(r, callsOn: false)).Select(r => r.Number).SequenceEqual(new[] { 1, 2, 4, 5, 8, 10, 12 })
                && Rows.All(r => MayStart(r, callsOn: true)),
            "combos: no combo without a warning: with the calls off only a bandit alone plays one");
        var row = Rows.Single(r => r.Number == 1);
        var pommel = Rows.Single(r => r.Number == 4);
        Check(HitFrom(10, row, callsOn: false) == 10 && HitFrom(10, row, callsOn: true) == 11.5 && HitFrom(10, pommel, callsOn: true) == 10 && !Calls(Rows.Single(r => r.Number == 6), true),
            "combos: only a call delays the hit, and only where the combo waits for it");
        Check(StageOf(10, 11.5, 12) == Stage.Waiting && StageOf(11.5, 11.5, 12) == Stage.Open && StageOf(12, 11.5, 12) == Stage.Open && StageOf(12.1, 11.5, 12) == Stage.Closed,
            "combos: waiting, open, closed");
        Check(MayCall(10, null) && MayCall(16, 10) && !MayCall(15.9, 10) && MayCall(5, 10), "combos: a group calls every 6 s at most, and a jump back of the clock frees it");
        Check(Fresh(10, 9.5) && !Fresh(10, 8.9) && !Fresh(10, 10.5), "combos: a report counts for a second");
        Check(Reaches(2, 1, 1) && !Reaches(3, 1, 1) && Reaches(5, 5, 0) && !Reaches(6, 5, 0), "combos: the partner's reach counts its walk: Slay from two cells at most");
        Check(MiddleOf(new[] { (0, 0), (5, 5), (5, 6), (6, 5) }, 2) == 1 && MiddleOf(Array.Empty<(int, int)>(), 2) == -1 && MiddleOf(new[] { (1, 1) }, 2) == 0,
            "combos: the rain falls where the most foes stand");
        Check(IsPair("Blade", "Guard") && IsPair("Guard", "Blade") && !IsPair("Blade", "Healer") && PickWeight("Spear", new[] { "Sword" }) == PairWeight && PickWeight("Spear", new[] { "Healer" }) == PlainWeight,
            "combos: a class that pairs with one picked weighs more");
        var classes = WeakBanditSpawnRules.ClassesOf();
        var plain = 0;
        var paired = 0;
        var same = true;
        for (var i = 0; i < 4000; i++)
        {
            var a = WeakBanditSpawnRules.GroupOf(i, 0, 2, "room", 3, classes);
            var b = WeakBanditSpawnRules.GroupOf(i, 0, 2, "room", 3, classes, pairs: true);
            if (a[0] != b[0] || b.Distinct().Count() != 3) same = false;
            if (a.Contains("Guard") && a.Contains("Blade")) plain++;
            if (b.Contains("Guard") && b.Contains("Blade")) paired++;
        }
        Check(same && paired > plain && paired < plain * 1.6, "combos: the pair roll leaves the first class and favours pairs a little");
        Check(BanditComboCallText.Pick(3, 1, 0.0) == 0 && BanditComboCallText.Pick(3, 1, 0.99) == 2 && BanditComboCallText.Pick(3, -1, 0.99) == 2 && BanditComboCallText.Pick(1, 0, 0.5) == 0,
            "combo calls: at random, never the same sentence twice in a row");
        var rows = BanditComboCallText.TextRows().ToArray();
        Check(rows.Length == 9 && rows.Select(r => r.Key).Distinct().Count() == 9 && rows.All(r => CoreTextRows.Registered.Any(x => x.Key == r.Key)), "combo calls: three sentences per kind in the game's text table");
        Check(StartLine(5, "a", "b", "c", 8, false, 7) == "CORE_BANDIT_COMBO combo=5 starter=a partner=b target=c window=8.0 called=no tick=7"
                && FollowLine(5, "b", "s", "c", 8) == "CORE_BANDIT_COMBO_FOLLOW combo=5 unit=b skill=s target=c tick=8"
                && LapseLine(5, "b", "window", 9) == "CORE_BANDIT_COMBO_LAPSE combo=5 unit=b reason=window tick=9",
            "combos: the lines name them");
        GraceChecks();
    }

    // The grace (3 s): a starter whose kit chose its skill waits for a follow-up ready within it and both hold
    // their combo skill, the global cooldown counted, and no other action shuts the combo skill out.
    private static void GraceChecks()
    {
        Check(Grace(null) == 3.0 && Grace(double.NaN) == 3.0 && Grace(0) == 0.0 && Grace(-1) == 0.0 && Grace(12) == 10.0 && Grace(4.5) == 4.5,
            "combo grace: 3 s by default, 0 (as before) to 10");
        Check(ReadyAt(10, 2, 0, 0) == 12 && ReadyAt(10, 2, 3, 0) == 13 && ReadyAt(10, 0, 0, 2.7) == 12.7 && ReadyAt(10, -1, 0, 0) == 10,
            "combo grace: a skill is ready at the latest of its cooldown, a global cooldown and what binds the bandit");
        var stun = Rows.Single(r => r.Number == 1);
        var pommel = Rows.Single(r => r.Number == 4);
        Check(HitDelay(stun, true) == CallDelay && HitDelay(stun, false) == 0 && HitDelay(pommel, true) == 0,
            "combo grace: the hit waits for the call only where the combo waits for it");
        // Starter ready in 1 s, the partner's Slay in 4 s, the hit 1.5 s after the start: the combo at 2.5 s, within 3 s.
        Check(ComboAt(1, 4, 1.5) == 2.5 && Overlaps(0, ComboAt(1, 4, 1.5), 3) && ComboAt(0, 0, 1.5) == 0 && Overlaps(0, 0, 3),
            "combo grace: both ready within the grace overlap, the follow-up counted at its hit");
        Check(!Overlaps(0, ComboAt(1, 6, 1.5), 3) && !Overlaps(0, ComboAt(4, 0, 0), 3) && !Overlaps(0, 0, 0),
            "combo grace: a skill ready later than the grace, or a grace of 0, reserves nothing");
        // The global cooldown pushes the start past the grace: no reservation.
        Check(Overlaps(0, ComboAt(ReadyAt(0, 2, 0, 0), 2, 0), 3) && !Overlaps(0, ComboAt(ReadyAt(0, 2, 3.5, 0), 2, 0), 3),
            "combo grace: the later of cooldown and global cooldown decides the overlap");
        Check(HoldEnds(10, 3, 1.5) == 14.5, "combo grace: a reservation lives at most the grace and the hit's delay");
        // The author's case: another skill shortly before the combo's moment is left out, an earlier one goes.
        Check(!Allowed(0, SkillBusySeconds, 0.5) && Allowed(0, SkillBusySeconds, 2.0) && Allowed(1.0, SkillBusySeconds, 2.0)
                && !Allowed(1.5, SkillBusySeconds, 2.0),
            "combo grace: a skill that would still bind the bandit at the combo's moment waits, one that ends before goes");
        Check(Busy(true, false) == 1.0 && Busy(false, false) == 2.7 && Busy(false, true) == 0 && !Allowed(0, Busy(false, false), 2.0)
                && Allowed(0, Busy(false, true), 0.1),
            "combo grace: the basic attack binds longest, a move not at all");
        Check(Allowed(3.0, BasicBusySeconds, 2.0), "combo grace: once the moment has passed only the reserved skill waits");
        Check(OpenUntil(0, 1.5, 2.0, 3.0) == 4.5 && OpenUntil(0, 0, 8.0, 3.0) == 8.0 && OpenUntil(0, 1.5, 2.0, 0) == 2.0,
            "combo grace: the follow-up stays open a grace after its hit begins, never shorter than the combo's window");
        Check(LapseReason(2.0, 4.5) == "grace" && LapseReason(8.0, 8.0) == "window", "combo grace: a lapse after the grace names it");
        // A partner on the board for another combo kept its reservation 8.6 s; its end counts there too, and held stops at the end.
        Check(!Lapsed(14.5, 14.5) && Lapsed(14.6, HoldEnds(10, 3, 1.5)),
            "combo grace: a reservation ends after the grace and the hit's delay, also for a partner on the board of another combo");
        Check(HeldOf(10, 14.5, 12) == (2.0, 0.0) && HeldOf(10, 14.5, 15) == (4.5, 0.5) && HeldOf(10, 14.5, 9) == (0.0, 0.0),
            "combo grace: a release counts the time held up to the reservation's end and names how late it came");
        // StunCharge set while Slay gets free 1.13 s later, the hit 1.5 s after the start: the start and the reservation agree.
        Check(ComboAt(0, FollowReady(1.13, 0, 0, 3), 1.5) == 0 && ComboAt(0, FollowReady(0, 2.5, 0, 3), 1.5) == 1.0
                && FollowReady(1.13, 0, 0, 0) == double.PositiveInfinity && FollowReady(0, 2.5, 0, 0) == 0,
            "combo grace: a follow-up ready by its hit counts for the start, what binds the partner counted; without a grace as before");
        Check(TauntBreaks("t", "c") && !TauntBreaks("c", "c") && !TauntBreaks(null, "c"),
            "combo grace: a taunt on another figure ends the reservation on both sides and keeps a taunted partner out");
        Check(Spent("SKILL_StunCharge", "SKILL_StunCharge", true) && !Spent("SKILL_StunCharge", "SKILL_StunCharge", false)
                && !Spent("", "SKILL_StunCharge", true) && !Spent(BanditKitKeys.Slay, "SKILL_StunCharge", true),
            "combo grace: a starter's reserved skill going out without its combo lets the reservation go");
        Check(Counts(10, 7.5, 3) && !Counts(10, 6.5, 3) && Counts(10, 9.5, 0) && !Counts(10, 8.5, 0),
            "combo grace: a partner's report counts as long as the grace, without one as before");
        // Reserved only when the starter's kit chose the skill: start, wait for it, or keep the reservation while the combo fits.
        Check(StartPlan(0, 0, 3, null, 1.5) == PlanStart && StartPlan(0, 2, 3, null, 0) == PlanReserve && StartPlan(0, 4, 3, null, 0) == null
                && StartPlan(0, 2, 0, null, 0) == null && StartPlan(1, 2.5, 3, 4.5, 1.5) == PlanWait && StartPlan(1, 3.5, 3, 4.5, 1.5) == null
                && StartPlan(2.5, 2.5, 3, 4.5, 1.5) == PlanStart,
            "combo grace: the starter's choice starts the combo, reserves it within the grace or waits for its reservation");
        Check(GroupAiRules.HoldsStill(HoldRule), "combo grace: a bandit standing for its combo gives the game nothing and decides again");
        Check(ReserveLine(1, "a", "starter", "SKILL_StunCharge", "b", "c", 2.5, 3, 7)
                == "CORE_BANDIT_COMBO_RESERVE combo=1 unit=a part=starter skill=SKILL_StunCharge other=b target=c in=2.50 grace=3.0 tick=7"
                && ReleaseLine(1, "b", "taunt", 1.25, 0.5, 8) == "CORE_BANDIT_COMBO_RELEASE combo=1 unit=b reason=taunt held=1.25 late=0.50 tick=8"
                && HoldbackLine("a", "basic", 2.7, 0.5, 9) == "CORE_BANDIT_COMBO_HOLDBACK unit=a skill=basic busy=2.7 in=0.50 tick=9"
                && NoStartLine(10, "a", "reach", 10) == "CORE_BANDIT_COMBO_NOSTART combo=10 unit=a reason=reach tick=10",
            "combo grace: the lines name reservation, release, hold back and a combo that did not start");
    }
}
