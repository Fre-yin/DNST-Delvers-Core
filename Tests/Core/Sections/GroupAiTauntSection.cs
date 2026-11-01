using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: area casts, the forest bandits' taunt and its state machine.
internal static class GroupAiTauntSection
{
    internal static void Run()
    {
        AreaCast();
        ForestTaunt();
        TauntStateMachine();
    }

    static void AreaCast()
    {
        // Group AI: an announced area skill is cast once its area is free, before the order of the bar;
        // otherwise it is dropped with the reason.
        Check(GroupAiRules.RedeemBlocker(true, true, true, true) == null && GroupAiRules.RedeemBlocker(false, true, true, true) == "cooldown"
                && GroupAiRules.RedeemBlocker(true, false, true, true) == "energy" && GroupAiRules.RedeemBlocker(true, true, false, true) == "target"
                && GroupAiRules.RedeemBlocker(true, true, true, false) == "range"
                && GroupAiRules.AreaPhaseOf(0, true, 0f, 1f) == GroupAiRules.AreaPhase.Cast
                && GroupAiRules.AreaPhaseOf(1, true, 0f, 1f) == GroupAiRules.AreaPhase.Wait
                && GroupAiRules.AreaPhaseOf(1, true, 0f, GroupAiRules.HoldSeconds + 0.1f) == GroupAiRules.AreaPhase.Skip
                && GroupAiRules.AreaPhaseOf(1, false, 0f, 9f) == GroupAiRules.AreaPhase.Wait,
            "group AI: an announcement is redeemed when its area is free, waits while allies are inside, is skipped after the hold");
    }

    static void ForestTaunt()
    {
        // Taunt of the forest bandits: heeded, but a boss breaks free early.
        Check(GroupAiRules.TauntSeconds(6.0, 0.5) == 3.0 && GroupAiRules.TauntSeconds(10.0, 0.5) == 5.0 && GroupAiRules.TauntSeconds(6.0, 2.0) == 6.0
                && GroupAiRules.TauntSeconds(6.0, -1.0) == 0.0 && GroupAiRules.TauntSeconds(-3.0, 0.5) == 0.0,
            "taunt: a bandit heeds at most the factor of the taunt's duration (half by default)");
        Check(GroupAiRules.TauntEndCause(2.9, 3.0, 6.0, false, false) == null && GroupAiRules.TauntEndCause(3.0, 3.0, 6.0, false, false) == "early"
                && GroupAiRules.TauntEndCause(6.0, 6.0, 6.0, false, false) == "expired" && GroupAiRules.TauntEndCause(1.0, 3.0, 6.0, false, true) == "healer_drop"
                && GroupAiRules.TauntEndCause(1.0, 3.0, 6.0, true, true) == null && GroupAiRules.TauntEndCause(3.5, 3.0, 6.0, true, true) == "early",
            "taunt: the time runs out early, the others break free at once when the healer falls hard, the healer herself does not");
        {
            var shares = new List<(float At, float Share)> { (7.5f, 0.95f), (8.5f, 0.80f), (9.5f, 0.70f) };
            Check(GroupAiRules.HealerDropReason(shares, 10f, 3f, 0.2, 0.4, 0.74f) == "loss" && GroupAiRules.HealerDropReason(shares, 10f, 3f, 0.2, 0.4, 0.76f) == null
                    && GroupAiRules.HealerDropReason(shares, 11f, 3f, 0.2, 0.4, 0.60f) == "loss" && GroupAiRules.HealerDropReason(shares, 11f, 3f, 0.2, 0.4, 0.61f) == null
                    && GroupAiRules.HealerDropReason(new List<(float At, float Share)>(), 1f, 3f, 0.2, 0.4, 0.39f) == "below"
                    && GroupAiRules.HealerDropReason(new List<(float At, float Share)>(), 1f, 3f, 0.2, 0.4, 0.41f) == null,
                "taunt: the healer falls hard when she loses 20 % of her maximum in 3 s or drops below 40 %");
        }
    }

    static void TauntStateMachine()
    {
        // The taunt's state machine, looked at every game tick (the end came 1 to 3 s
        // late at the next decision). Game seconds; native 6 s, factor 0.5.
        Func<double> six = () => 6.0;
        var start = GroupAiRules.LookAtTaunt(null, "D", 10.0, six, 0.5, false, null);
        var track = start.Track;
        var before = GroupAiRules.LookAtTaunt(track, "D", 12.99, six, 0.5, false, null);
        var atUntil = GroupAiRules.LookAtTaunt(track, "D", 13.0, six, 0.5, false, null);
        var after = GroupAiRules.LookAtTaunt(atUntil.Track, "D", 13.02, six, 0.5, false, null);
        var cleared = GroupAiRules.LookAtTaunt(after.Track, null, 13.04, six, 0.5, false, null);
        Check(start.Started && !start.Release && start.End == null && track.Until == 13.0 && track.NativeEnd == 16.0
                && !before.Started && !before.Release && before.End == null
                && atUntil.End == "early" && atUntil.Release && atUntil.Track.Ended
                && after.End == null && after.Release && !after.Started
                && cleared.Track == null && cleared.End == null && !cleared.Release,
            "taunt: it ends at until itself, Core takes the game's taunt off then and again while the game still forces it");

        // Per game tick (60 per second) the end comes at the first tick at or after until.
        var t = GroupAiRules.LookAtTaunt(null, "D", 120.0 / 60.0, six, 0.5, false, null).Track;
        var endTick = -1;
        for (var tick = 121; tick < 600 && endTick < 0; tick++)
        {
            var look = GroupAiRules.LookAtTaunt(t, "D", tick / 60.0, six, 0.5, false, null);
            t = look.Track;
            if (look.End != null) endTick = tick;
        }
        Check(endTick == 300, "taunt: looked at every game tick, a 3 s heed ends 180 ticks after its start, not at the next decision");

        var drop = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, six, 0.5, false, null).Track, "D", 10.5, six, 0.5, false, "loss");
        var healer = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, six, 0.5, true, null).Track, "D", 10.5, six, 0.5, true, "loss");
        var gameFirst = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, six, 0.5, false, null).Track, null, 11.0, six, 0.5, false, null);
        var full = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, six, 1.0, false, null).Track, "D", 16.0, six, 1.0, false, null);
        var other = GroupAiRules.LookAtTaunt(atUntil.Track, "E", 13.5, six, 0.5, false, null);
        var again = GroupAiRules.LookAtTaunt(atUntil.Track, "D", 16.0, six, 0.5, false, null);
        var instant = GroupAiRules.LookAtTaunt(null, "D", 5.0, () => 0.0, 0.5, false, null);
        Check(drop.End == "healer_drop" && drop.Release && healer.End == null && !healer.Release
                && gameFirst.Track == null && gameFirst.End == "expired" && !gameFirst.Release
                && full.End == "expired" && full.Release
                && other.Started && other.Track.By == "E" && other.End == null
                && again.Started && again.Track.Until == 19.0 && again.End == null
                && instant.Started && instant.End == "expired" && instant.Release,
            "taunt: the others break free at once when the healer falls hard; a new taunt or one after the game's end starts anew");
    }
}
