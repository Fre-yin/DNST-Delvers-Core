using DungeonSettlersDelvers.Core;
using static TestKit;

// Revive scroll rules: count, sickness, price, and where a revived character appears.
internal static class ReviveRulesSection
{
    internal static void Run()
    {
        ReviveScrollRules();
        ReviveCell();
        ReviveShare();
        ScrollLook();
    }

    // (variant 4): paper gold-yellow, ribbon silver, outline and a near tone kept apart from others.
    static void ScrollLook()
    {
        Check(ScrollGlowRules.Turn(0xC1, 0xBD, 0xA1) == (0xF2, 0xCF, 0x5C) && ScrollGlowRules.Turn(0xCF, 0x2D, 0x27) == (0xC4, 0xCA, 0xD2)
                && ScrollGlowRules.Turn(0x91, 0x0F, 0x29) == (0x6C, 0x72, 0x7C) && ScrollGlowRules.Turn(0xC4, 0xBA, 0xA4) == (0xF2, 0xCF, 0x5C)
                && ScrollGlowRules.Turn(0x22, 0x20, 0x25) == (0x22, 0x20, 0x25) && ScrollGlowRules.Turn(0xD0, 0xBD, 0xA1) == (0xD0, 0xBD, 0xA1)
                && ScrollGlowRules.Turns.Length == 6 && ScrollGlowRules.Ring.A == 90,
            "revive scroll look: paper gold-yellow, ribbon silver, the outline kept, a tone within the tolerance turns, one further off stays");
    }

    static bool Near(double value, double expected, double tolerance = 1e-3) => Math.Abs(value - expected) < tolerance;

    static void ReviveScrollRules()
    {
        // Revive scroll rules: count and sickness by combat difficulty, price per part.
        Check(ReviveRules.FromGame(4, true) == ReviveRules.Level.Apocalypse && ReviveRules.FromGame(4, false) == ReviveRules.Level.Downfall
                && ReviveRules.FromGame(3, true) == ReviveRules.Level.VeryHard && ReviveRules.FromGame(9, false) == ReviveRules.Level.Downfall
                && ReviveRules.FromGame(-1, false) == ReviveRules.Level.Easy,
            "Apocalypse counts only on top of Downfall, and unknown values stay inside the game's range");
        Check(ReviveRules.RevivesAllowed(ReviveRules.Level.Apocalypse) == 2 && ReviveRules.RevivesAllowed(ReviveRules.Level.Downfall) == 2
                && ReviveRules.RevivesAllowed(ReviveRules.Level.VeryHard) == 4
                && new[] { ReviveRules.Level.Hard, ReviveRules.Level.Normal, ReviveRules.Level.Easy }.All(level => ReviveRules.RevivesAllowed(level) == ReviveRules.Unlimited),
            "revives per character: Apocalypse and Downfall 2, VeryHard 4, unlimited below");
        Check(ReviveRules.RevivesLeft(ReviveRules.Level.VeryHard, 1) == 3 && ReviveRules.RevivesLeft(ReviveRules.Level.Apocalypse, 2) == 0
                && ReviveRules.RevivesLeft(ReviveRules.Level.Apocalypse, 5) == 0 && ReviveRules.RevivesLeft(ReviveRules.Level.Hard, 40) == ReviveRules.Unlimited,
            "revives left count down to zero and never below; unlimited stays unlimited");
        double[] Weeks(ReviveRules.Level level, int count) => Enumerable.Range(1, count).Select(n => ReviveRules.SicknessWeeks(level, n)).ToArray();
        Check(Weeks(ReviveRules.Level.Apocalypse, 2).SequenceEqual(new[] { 2.0, 4.0 })
                && Weeks(ReviveRules.Level.Downfall, 2).SequenceEqual(new[] { 1.0, 2.0 })
                && Weeks(ReviveRules.Level.VeryHard, 4).SequenceEqual(new[] { 1.0, 2.0, 4.0, 4.0 })
                && Weeks(ReviveRules.Level.Normal, 5).SequenceEqual(new[] { 0.5, 1.0, 2.0, 4.0, 4.0 })
                && ReviveRules.SicknessWeeks(ReviveRules.Level.Easy, 1000) == 4.0
                && ReviveRules.SicknessDays(ReviveRules.Level.Hard, 1) == 3.5,
            "sickness doubles with every revive from 2, 1 or half a week and stops at four weeks");
        var noRevive = false;
        try { ReviveRules.SicknessWeeks(ReviveRules.Level.Hard, 0); } catch (ArgumentOutOfRangeException) { noRevive = true; }
        Check(noRevive, "a sickness needs a revive: revive 0 is rejected");
        Check(Math.Abs(ReviveRules.SicknessCap(7, 7) - 0.10) < 1e-9 && Math.Abs(ReviveRules.SicknessCap(3.5, 7) - 0.55) < 1e-9
                && ReviveRules.SicknessCap(0, 7) == 1.0 && ReviveRules.SicknessCap(5, 0) == 1.0 && Math.Abs(ReviveRules.SicknessCap(9, 7) - 0.10) < 1e-9,
            "the sickness cap rises evenly from 10 % to full, and no sickness means full");
        Check(Math.Abs(ReviveRules.DownCap(600, 600) - 0.65) < 1e-9 && Math.Abs(ReviveRules.DownCap(300, 600) - 0.825) < 1e-9
                && ReviveRules.DownCap(0, 600) == 1.0,
            "after a down health and energy start at 65 % and rise to full as the game's after-effect runs out");
        Check(ReviveText.All.All(entry => entry.Text.AllLanguages.Count == 10
                && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('–') < 0 && text.IndexOf('—') < 0))
                && ReviveText.SicknessKey.StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal)
                && ReviveText.PanicKey.StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal)
                && !ReviveText.SicknessKey.StartsWith("AFFECTER_DelversElf", StringComparison.Ordinal),
            "revive effects: words in all ten languages, keys under Core's own prefix so saves keep them out");
        // The game picks the tooltip type from the second-to-last part of a key split at '_'; with a
        // second underscore our effects showed a plain text tooltip without values.
        Check(new[] { ReviveText.SicknessKey, ReviveText.PanicKey, ReviveText.ScrollKey }.All(key => key.Split('_').Length == 2)
                && ReviveText.ScrollKey.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal),
            "Core's own keys have exactly one underscore, so the game shows its full tooltips");
        Check(Math.Abs(ReviveRules.DurationUnitsPerDay * 3.5 - 3855.6) < 0.01 && Math.Abs(ReviveRules.DurationUnitsPerDay * 7 - 7711.2) < 0.01,
            "half a week and a week in the game's duration units match the toolkit's measured 3856 and 7711");
        Check(ReviveRules.Price(ReviveRules.Level.Easy, ReviveRules.Level.Normal) == 4000
                && ReviveRules.Price(ReviveRules.Level.Hard, ReviveRules.Level.VeryHard) == 7000
                && ReviveRules.Price(ReviveRules.Level.Downfall, ReviveRules.Level.Downfall) == 10000
                && ReviveRules.Price(ReviveRules.Level.Apocalypse, ReviveRules.Level.Apocalypse) == 12000,
            "the price adds a combat and a management part: 2000 to 6000 each");
        Check(ReviveRules.ShouldReplaceSnapshot(false, 5, false) && ReviveRules.ShouldReplaceSnapshot(true, 5, false)
                && ReviveRules.ShouldReplaceSnapshot(false, 5, true) && !ReviveRules.ShouldReplaceSnapshot(true, 4, true)
                && !ReviveRules.ShouldReplaceSnapshot(true, 0, false) && !ReviveRules.ShouldReplaceSnapshot(false, 0, false),
            "newer snapshots win, but a dying character's list never replaces one, and an empty list never counts");
        Check(TraitStack.TryParse("AFFECTER_Elf:2", out var elf) && elf.Key == "AFFECTER_Elf" && elf.Stacks == 2 && elf.ToString() == "AFFECTER_Elf:2"
                && !TraitStack.TryParse("AFFECTER_Elf", out _) && !TraitStack.TryParse("AFFECTER_Elf:0", out _)
                && !TraitStack.TryParse(":3", out _) && !TraitStack.TryParse("AFFECTER_Elf:", out _) && !TraitStack.TryParse(null, out _),
            "snapshot entries read and write as KEY:Stacks and reject broken entries");
    }

    static void ReviveCell()
    {
        // Where a revived character appears (the caster must not block): the body's
        // cell when free, otherwise the first free neighbour N, E, S, W, NE, SE, SW, NW; none blocks.
        var corpseKind = ReviveRules.ChooseReviveCell(10, 20, true, (x, y) => false, out var cx, out var cy);
        Check(corpseKind == ReviveRules.ReviveCellKind.Corpse && cx == 10 && cy == 20,
            "a free body cell keeps the revived character where the body lay");
        var northKind = ReviveRules.ChooseReviveCell(10, 20, false, (x, y) => true, out var nx, out var ny);
        Check(northKind == ReviveRules.ReviveCellKind.Neighbour && nx == 10 && ny == 21,
            "a body cell with the caster on it moves the revived character to the north neighbour first");
        var visited = new List<string>();
        var lastKind = ReviveRules.ChooseReviveCell(10, 20, false, (x, y) => { visited.Add(x + "," + y); return x == 9 && y == 21; },
            out var lx, out var ly);
        Check(lastKind == ReviveRules.ReviveCellKind.Neighbour && lx == 9 && ly == 21
                && string.Join(" ", visited) == "10,21 11,20 10,19 9,20 11,21 11,19 9,19 9,21",
            "neighbours are tried in the fixed order N, E, S, W, NE, SE, SW, NW");
        var noneKind = ReviveRules.ChooseReviveCell(10, 20, false, (x, y) => false, out var ox, out var oy);
        Check(noneKind == ReviveRules.ReviveCellKind.None && ox == 10 && oy == 20
                && ReviveRules.Describe(noneKind) == "none" && ReviveRules.Describe(northKind) == "neighbor"
                && ReviveRules.Describe(corpseKind) == "corpse",
            "without any free cell the revive is refused, and the log words are corpse, neighbor and none");
    }

    // The locked share of health and energy: the share that holds a cap, the cap a share stands for,
    // and the full maximum without the share.
    static void ReviveShare()
    {
        float[] caps = { 0.10f, 0.55f, 0.65f, 0.9f };
        float[] foreigns = { 0f, 20f, -30f };
        Check(caps.All(cap => foreigns.All(foreign => Near(ReviveShareRules.CapOfShare(ReviveShareRules.ShareForCap(cap, foreign), foreign), cap))),
            "the share that holds a cap stands for exactly that cap, with and without percent values from traits");
        Check(Near(ReviveShareRules.ShareForCap(0.10f, 0f), -90) && Near(ReviveShareRules.ShareForCap(0.65f, 20f), -42)
                && ReviveShareRules.ShareForCap(1f, 20f) == 0f,
            "the share is the locked part of the maximum: 90 at a cap of 10 %, 35 percent of 120 at 65 %, none at full");
        Check(Near(ReviveShareRules.CapOfShare(-0.5f, -150f), 0.5) && Near(ReviveShareRules.CapOfShare(-35f, 0f), 0.65),
            "a maximum already pushed to its floor by other percent values reads the share against 1, never a zero or negative divisor");
        Check(ReviveShareRules.LockedCap(0f, 0f) == null && ReviveShareRules.LockedCap(0.005f, 0.005f) == null
                && ReviveShareRules.LockedCap(-0.005f, 30f) == null,
            "a mirrored share below one hundredth counts as no share, so the bars draw nothing locked");
        Check(Near(ReviveShareRules.LockedCap(-35f, -35f).Value, 0.65) && Near(ReviveShareRules.LockedCap(-42f, -22f).Value, 0.65)
                && ReviveShareRules.LockedCap(10f, 10f) == 1f && ReviveShareRules.LockedCap(-200f, -200f) == 0f,
            "the bars' cap is the cap of the share with foreign percent values left out, and stays between 0 and 1");
        Check(ReviveShareRules.IsLifted(1f) && ReviveShareRules.IsLifted(0.995f) && !ReviveShareRules.IsLifted(0.9949f)
                && ReviveShareRules.HasShare(0.01f) && ReviveShareRules.HasShare(-35f) && !ReviveShareRules.HasShare(-0.0099f),
            "a cap from 99.5 % is lifted and a share from one hundredth is there");

        // A down's cap of 65 % leaves a share of -35 on a maximum of 100; the maximum read now is 65.
        var downShare = ReviveShareRules.ShareForCap((float)ReviveRules.DownStartShare, 0f);
        var maxWithDownShare = 100f * (100f + downShare) / 100f;
        Check(Near(ReviveShareRules.FullMax(maxWithDownShare, downShare, downShare), 100),
            "a corpse that still carries a down's share gets its full maximum back");
        Check(Near(ReviveRules.StartHealthShare * ReviveShareRules.FullMax(maxWithDownShare, downShare, downShare), 10)
                && !Near(ReviveRules.StartHealthShare * maxWithDownShare, 10),
            "a revive starts at 10 % of the full maximum, not at 6.5 % of the maximum a down's share left");
        var foreignShare = ReviveShareRules.ShareForCap((float)ReviveRules.DownStartShare, 20f);
        var maxWithForeign = 100f * (100f + 20f + foreignShare) / 100f;
        Check(Near(ReviveShareRules.FullMax(maxWithForeign, foreignShare, 20f + foreignShare), 120)
                && Near(ReviveShareRules.FullMax(120f, 0f, 20f), 120),
            "the full maximum keeps the percent values of traits and gear and drops only Core's share");
        Check(ReviveShareRules.FullMax(40f, -99f, -99f) == 40f && ReviveShareRules.FullMax(40f, -120f, -120f) == 40f,
            "a total percent that leaves no divisor keeps the maximum as read");
    }
}
