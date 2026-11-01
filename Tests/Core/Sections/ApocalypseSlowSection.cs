using DungeonSettlersDelvers.Core;
using static TestKit;

// Apocalypse's slow motion on screen.
internal static class ApocalypseSlowSection
{
    internal static void Run()
    {
        // Apocalypse's slow motion on screen (ApocalypseSlowRules, sketch B, rim B2).
        bool SlowNear(float a, float b) => Math.Abs(a - b) < 1e-4f;
        bool SlowSame(ApocalypseSlowRules.Rgb a, ApocalypseSlowRules.Rgb b) => SlowNear(a.R, b.R) && SlowNear(a.G, b.G) && SlowNear(a.B, b.B);
        const float SlowMax = 5f, SlowStart = 0.5f;
        Check(ApocalypseSlowRules.ModeOf(false, 5f, SlowMax, SlowStart, -1f, -1f) == ApocalypseSlowRules.Mode.Full
                && ApocalypseSlowRules.ModeOf(false, 4f, SlowMax, SlowStart, -1f, -1f) == ApocalypseSlowRules.Mode.Refill
                && ApocalypseSlowRules.ModeOf(false, 0.4f, SlowMax, SlowStart, -1f, -1f) == ApocalypseSlowRules.Mode.Low
                && ApocalypseSlowRules.ModeOf(true, 2f, SlowMax, SlowStart, -1f, -1f) == ApocalypseSlowRules.Mode.Running
                && ApocalypseSlowRules.ModeOf(false, 0.2f, SlowMax, SlowStart, 0.3f, -1f) == ApocalypseSlowRules.Mode.Denied
                && ApocalypseSlowRules.ModeOf(false, 0.2f, SlowMax, SlowStart, 0.6f, -1f) == ApocalypseSlowRules.Mode.Low
                && ApocalypseSlowRules.ModeOf(false, 5f, SlowMax, SlowStart, -1f, 0.2f) == ApocalypseSlowRules.Mode.Flash
                && ApocalypseSlowRules.ModeOf(true, 5f, SlowMax, SlowStart, -1f, 0.2f) == ApocalypseSlowRules.Mode.Running
                && SlowNear(ApocalypseSlowRules.FillShare(2.5f, SlowMax), 0.5f) && ApocalypseSlowRules.FillShare(7f, SlowMax) == 1f
                && ApocalypseSlowRules.FillShare(1f, 0f) == 0f,
            "slow motion: the bar is full, refills, is grey below the start amount, runs, blinks when refused and flashes when full");
        var deniedNow = ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Denied, 0f);
        var deniedOver = ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Denied, 0.6f);
        var flashTop = ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Flash, 0.25f);
        var runStart = ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Running, 0f);
        Check(SlowSame(ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Full, 0f).Fill, ApocalypseSlowRules.Tan)
                && SlowSame(ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Refill, 0f).Back, ApocalypseSlowRules.Cream)
                && SlowSame(ApocalypseSlowRules.BarColours(ApocalypseSlowRules.Mode.Low, 0f).Fill, ApocalypseSlowRules.Grey)
                && SlowSame(deniedNow.Fill, ApocalypseSlowRules.Red)
                && SlowSame(deniedNow.Back, ApocalypseSlowRules.Mix(ApocalypseSlowRules.GreyEmpty, ApocalypseSlowRules.Red, 0.85f))
                && SlowSame(deniedOver.Fill, ApocalypseSlowRules.Grey) && SlowSame(deniedOver.Back, ApocalypseSlowRules.GreyEmpty)
                && SlowSame(flashTop.Fill, ApocalypseSlowRules.Mix(ApocalypseSlowRules.Tan, ApocalypseSlowRules.White, 0.85f))
                && SlowSame(runStart.Fill, ApocalypseSlowRules.Mix(ApocalypseSlowRules.Tan, ApocalypseSlowRules.White, 0.12f))
                && ApocalypseSlowRules.Tan.Hex == "D9BE8F" && ApocalypseSlowRules.Rim.Hex == "E8C27A" && ApocalypseSlowRules.Gold.Hex == "FFE08A",
            "slow motion: the bar's colours follow the sketch (tan on cream, grey below the start, red blink, white flash)");
        Check(SlowNear(ApocalypseSlowRules.ShimmerCentre(0f, 50f), -12f) && SlowNear(ApocalypseSlowRules.ShimmerCentre(0.5f, 50f), 25f)
                && SlowNear(ApocalypseSlowRules.ShimmerAt(25f, 25f), 0.85f) && SlowNear(ApocalypseSlowRules.ShimmerAt(31f, 25f), 0.425f)
                && ApocalypseSlowRules.ShimmerAt(37f, 25f) == 0f
                && ApocalypseSlowRules.Shake(0f) == 0f && ApocalypseSlowRules.Shake(0.35f) == 0f && ApocalypseSlowRules.Shake(-1f) == 0f
                && ApocalypseSlowRules.Shake(0.03f) == 3f
                && SlowNear(ApocalypseSlowRules.Flash(0.25f), 1f) && ApocalypseSlowRules.Flash(0f) == 0f && ApocalypseSlowRules.Flash(0.5f) == 0f
                && SlowNear(ApocalypseSlowRules.Glow(0f), 0.3f) && SlowNear(ApocalypseSlowRules.FrameAlpha(0.25f), 1f)
                && SlowNear(ApocalypseSlowRules.FrameAlpha(0.75f), 0.35f),
            "slow motion: the light band runs across the fill once a second, the shake dies out, the flash rises and falls once");
        Check(SlowNear(ApocalypseSlowRules.Approach(0f, 1f, 0.15f), 0.5f) && ApocalypseSlowRules.Approach(0.5f, 0f, 0.3f) == 0f
                && ApocalypseSlowRules.Approach(1f, 1f, 1f) == 1f
                && SlowNear(ApocalypseSlowRules.RimWeight(1f, 0f), 0.8f) && SlowNear(ApocalypseSlowRules.RimWeight(1f, 0.5f), 1f)
                && ApocalypseSlowRules.RimWeight(0f, 0.5f) == 0f && SlowNear(ApocalypseSlowRules.RimWeight(0.5f, 1.5f), 0.3f)
                && ApocalypseSlowRules.RimStrength(null) == 0.42 && ApocalypseSlowRules.RimStrength(2.0) == 1.0 && ApocalypseSlowRules.RimStrength(-1.0) == 0.0
                && ApocalypseSlowRules.Calm(ApocalypseSlowRules.Mode.Full, 0f) && !ApocalypseSlowRules.Calm(ApocalypseSlowRules.Mode.Full, 0.1f)
                && !ApocalypseSlowRules.Calm(ApocalypseSlowRules.Mode.Refill, 0f),
            "slow motion: tint and rim fade over 0.3 s, the rim breathes between 60 and 100 %, B2 strength 0.42, calm only when full and faded");
    }
}
