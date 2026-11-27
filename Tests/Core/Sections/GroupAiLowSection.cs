using DungeonSettlersDelvers.Core;
using static DungeonSettlersDelvers.Core.GroupAiLowRules;
using static TestKit;

// The AI's health lines with look ahead and buffer (GroupAiLowRules), for the group AI, the bandits and the roles.
internal static class GroupAiLowSection
{
    internal static void Run()
    {
        var net = Read(null, null, null, null, true);
        var damage = Read(null, null, null, null, false);
        Check(net == new Settings(3f, 2, 2f, 2f, true) && Read(-1, 99, 2.5, double.NaN, false) == new Settings(0.5f, 10, 2.5f, 2f, false),
            "low: the defaults are 3 s window, 2 hits, 2 s ahead, 2 s buffer, and the settings stay in range");
        Check(Threshold(LowLine.Emergency, 0.3) == GroupAiRules.EmergencyShare && Threshold(LowLine.SeedStay, 0.25) == 0.25f
                && Threshold(LowLine.Critical, 0.3) == 0.30f && Threshold(LowLine.Sacrifice, 0.3) == 0.50f
                && Threshold(LowLine.Guard, 0.3) == 0.70f,
            "low: the lines are the emergency share, seeds.stay.below and the roles' critical share");

        // 200 damage in two hits within 3 s on 1000 health: 0.0667 a second, so 0.45 reaches 0.35 within 2 s (0.317).
        var hits = new[] { new Change(9f, 100f, false), new Change(10f, 100f, false) };
        var loss = LossPerSecond(hits, 10f, net, 1000f, out var count);
        Check(Math.Abs(loss - 200f / 1000f / 3f) < 0.0001f && count == 2, "low: the loss per second is the window's damage over the maximum");
        var single = LossPerSecond(new[] { new Change(10f, 400f, false) }, 10f, net, 1000f, out var one);
        var old = LossPerSecond(new[] { new Change(1f, 100f, false), new Change(2f, 100f, false) }, 10f, net, 1000f, out _);
        Check(single == 0f && one == 1 && old == 0f, "low: a single hit, however big, and hits outside the window make no look ahead");
        var healed = new[] { new Change(9f, 100f, false), new Change(10f, 100f, false), new Change(10f, 150f, true) };
        Check(Math.Abs(LossPerSecond(healed, 10f, net, 1000f, out _) - 50f / 1000f / 3f) < 0.0001f
                && Math.Abs(LossPerSecond(healed, 10f, damage, 1000f, out _) - loss) < 0.0001f
                && LossPerSecond(new[] { new Change(9f, 50f, false), new Change(10f, 50f, false), new Change(10f, 200f, true) }, 10f, net, 1000f, out _) == 0f,
            "low: net takes the heals off the damage (the bar's course), damage only ignores them, more healing than damage is no loss");

        var hold = 0f;
        Check(Below(0.45f, 0.35f, loss, 10f, net, ref hold) == "ahead" && hold == 12f, "low: a line the health crosses within 2 s counts as crossed");
        Check(Below(0.60f, 0.35f, loss, 10.5f, net, ref hold) == "buffer" && Below(0.60f, 0.35f, 0f, 12f, net, ref hold) == null,
            "low: after rising above the line the figure counts as below for the 2 s buffer, then no longer");
        var fresh = 0f;
        Check(Below(0.30f, 0.30f, 0f, 5f, net, ref fresh) == "now" && fresh == 7f && Below(0.31f, 0.30f, 0f, 5f, net, ref fresh) == "buffer"
                && Below(0.50f, 0.30f, 0f, 1f, net, ref fresh) == "buffer",
            "low: at the line it is below now, and the buffer starts again from there");
        Check(Adjusted(0.45f, 0.35f, "ahead") < 0.35f && Adjusted(0.45f, 0.35f, "buffer") < 0.35f && Adjusted(0.20f, 0.35f, "ahead") == 0.20f
                && Adjusted(0.45f, 0.35f, null) == 0.45f && Adjusted(0.30f, 0.35f, "now") == 0.30f,
            "low: the share the sites compare moves just under the line by look ahead or buffer, never up");
        // Heal seeking: only without a heal skill and never the healer herself; her reach a step short of Lifebloom's.
        var reach = SeedStayRules.ReachCells(10, 10, SeedStayRules.HealerReach);
        Check(SeedStayRules.SeeksHealer(false, false) && !SeedStayRules.SeeksHealer(true, false) && !SeedStayRules.SeeksHealer(false, true)
                && SeedStayRules.HealerReach == LifebloomRules.Range - 1 && reach.Count == 80 && !reach.Contains((10, 10))
                && reach.Contains((14, 6)) && !reach.Contains((15, 10)) && SeedStayRules.InArea(reach, 12, 9),
            "low: a unit without a heal skill seeks the healer, into the cells within her reach, her own cell left out");
        // One walk per state: an ended stay is freed only above the seed line with look ahead and buffer,
        // and a stay never starts at or above the exit share.
        Check(!SeedStayRules.Freed(0.299f, 0.30) && SeedStayRules.Freed(0.46f, 0.30) && !SeedStayRules.Freed(Adjusted(0.46f, 0.30f, "buffer"), 0.30)
                && SeedStayRules.EntryShare(0.299f, 0.50f, 0.50) == 0.50f && SeedStayRules.EntryShare(0.299f, 0.45f, 0.50) == 0.299f
                && !SeedStayRules.Stays(false, SeedStayRules.EntryShare(0.299f, 0.50f, 0.50), 0.30, 0.50),
            "low: a figure held below by look ahead or buffer is not freed for a second walk, and no stay starts at the exit share");
        Check(SeedStayRules.HealSeekLine("bandit", "u", "h", "walk", 0.28f, 3, 4, 5) == "CORE_AI_HEAL_SEEK side=bandit unit=u healer=h step=walk share=0.28 at=3,4 tick=5",
            "low: the heal seeking line names side, unit, healer, step and share");
        Check(Line("u", LowLine.SeedStay, "ahead", 0.45f, 0.0667f, 2, 9) == "CORE_AI_LOW unit=u line=seedstay state=ahead share=0.45 loss=0.067 hits=2 tick=9"
                && Line("u", LowLine.Emergency, null, 0.6f, 0f, 0, 9).Contains("state=above"),
            "low: the line names the figure, the line, why and the numbers");
    }
}
