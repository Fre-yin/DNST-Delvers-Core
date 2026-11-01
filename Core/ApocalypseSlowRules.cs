namespace DungeonSettlersDelvers.Core;

// How Apocalypse's slow motion (ApocalypseTime) shows itself, pure so the offline tests cover it
// (sketch B with the rim of B2). ApocalypseSlowDisplay draws it.
// - A bar under the speed buttons shows the charge: tan on cream from the start amount on, grey below
//   it. While slow motion runs, the fill pulses and a light band runs along it once a second.
// - The pause button glows in the same beat and gets a gold frame; clock icon and time take the bar's
//   colour, fading in and out over 0.3 s.
// - Pressed with too little charge: one red blink and a short shake. Full again: one flash.
// - A warm rim over the screen (a copy of the game's vignette) breathes while slow motion runs.
// Everything runs in real time, at the same pace during slow motion. Sizes are the canvas's reference
// pixels: at 2560x1080 the speed buttons end at row 78, rows 79 to 83 are free, the bar takes 80 to 83.
internal static class ApocalypseSlowRules
{
    internal enum Mode { Full, Running, Low, Refill, Denied, Flash }

    internal readonly struct Rgb
    {
        internal readonly float R;
        internal readonly float G;
        internal readonly float B;

        internal Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        internal static Rgb Of(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        internal string Hex => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}",
            (int)Math.Round(R * 255f), (int)Math.Round(G * 255f), (int)Math.Round(B * 255f));
    }

    // Colours of the sketch, taken from the game's HUD: the fill of its bars, their empty rest, the
    // clock panel, the red of its warnings; gold for the frame, a warm sand for the rim.
    internal static readonly Rgb Tan = Rgb.Of(0xD9, 0xBE, 0x8F);
    internal static readonly Rgb Cream = Rgb.Of(0xFF, 0xFE, 0xF3);
    internal static readonly Rgb Panel = Rgb.Of(0x24, 0x21, 0x27);
    internal static readonly Rgb Red = Rgb.Of(0xF1, 0x60, 0x71);
    internal static readonly Rgb White = Rgb.Of(0xFF, 0xFF, 0xFF);
    internal static readonly Rgb Gold = Rgb.Of(0xFF, 0xE0, 0x8A);
    internal static readonly Rgb Rim = Rgb.Of(0xE8, 0xC2, 0x7A);
    internal static readonly Rgb Grey = Mix(Tan, Panel, 0.5f);
    internal static readonly Rgb GreyEmpty = Mix(Cream, Panel, 0.35f);

    internal const float BarHeight = 4f;
    internal const float BarGap = 1f;
    internal const float ShimmerPeriod = 1f;
    internal const float ShimmerHalfWidth = 12f;
    internal const float ShimmerPeak = 0.85f;
    internal const float PulsePeriod = 1f;
    internal const float PulseBase = 0.12f;
    internal const float GlowPeak = 0.6f;
    internal const float FrameLow = 0.35f;
    internal const float FadeSeconds = 0.3f;
    internal const float BlinkSeconds = 0.6f;
    internal const float BlinkBack = 0.85f;
    internal const float ShakeSeconds = 0.35f;
    internal const float ShakePixels = 3f;
    internal const float ShakeSpeed = 45f;
    internal const float FlashSeconds = 0.5f;
    internal const float FlashPeak = 0.85f;
    internal const float BreathPeriod = 2f;
    internal const float BreathBase = 0.8f;
    internal const float BreathSwing = 0.2f;

    // Core's settings file: the bar and its effects (on by default) and the rim's strength, the
    // vignette's intensity from 0 (off) to 1 (B2: 0.42).
    internal const string BarKey = "apocalypse.slow.bar";
    internal const string RimKey = "apocalypse.slow.rim";
    internal const double RimDefault = 0.42;

    internal static double RimStrength(double? setting) => Math.Clamp(setting ?? RimDefault, 0.0, 1.0);

    // sinceDenied, sinceFull: seconds since the refused press and since the charge became full again,
    // negative when there was none.
    internal static Mode ModeOf(bool slow, float charge, float max, float start, float sinceDenied, float sinceFull)
    {
        if (sinceDenied >= 0f && sinceDenied < BlinkSeconds) return Mode.Denied;
        if (slow) return Mode.Running;
        if (sinceFull >= 0f && sinceFull < FlashSeconds) return Mode.Flash;
        if (charge < start) return Mode.Low;
        return charge < max ? Mode.Refill : Mode.Full;
    }

    internal static float FillShare(float charge, float max) => max > 0f ? Math.Clamp(charge / max, 0f, 1f) : 0f;

    internal static Rgb Mix(Rgb a, Rgb b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    // 0 to 1 in a beat of the given period; 0.5 at its start, rising like a sine.
    internal static float Beat(float t, float period) => 0.5f + 0.5f * (float)Math.Sin(t * 2.0 * Math.PI / period);

    // Fill and empty rest of the bar; t is the seconds in the mode (the run time while it runs).
    internal static (Rgb Fill, Rgb Back) BarColours(Mode mode, float t)
    {
        switch (mode)
        {
            case Mode.Low:
                return (Grey, GreyEmpty);
            case Mode.Denied:
                var blink = 1f - Math.Clamp(t / BlinkSeconds, 0f, 1f);
                return (Mix(Grey, Red, blink), Mix(GreyEmpty, Red, BlinkBack * blink));
            case Mode.Flash:
                return (Mix(Tan, White, FlashPeak * Flash(t)), Cream);
            case Mode.Running:
                return (Mix(Tan, White, PulseBase + PulseBase * (float)Math.Sin(t * 2.0 * Math.PI / PulsePeriod)), Cream);
            default:
                return (Tan, Cream);
        }
    }

    // The light band's centre in pixels from the bar's left end: it enters left of the fill and leaves
    // right of it once per period.
    internal static float ShimmerCentre(float run, float filledWidth)
        => run % ShimmerPeriod / ShimmerPeriod * (filledWidth + 2f * ShimmerHalfWidth) - ShimmerHalfWidth;

    // The band's strength at x pixels from the left end: peak in its centre, nothing at its edges.
    internal static float ShimmerAt(float x, float centre)
    {
        var d = Math.Abs(x - centre);
        return d < ShimmerHalfWidth ? ShimmerPeak * (1f - d / ShimmerHalfWidth) : 0f;
    }

    // Sideways offset in pixels after a refused press, dying out.
    internal static float Shake(float since)
        => since < 0f || since >= ShakeSeconds ? 0f : (float)Math.Round(ShakePixels * Math.Sin(since * ShakeSpeed) * (1f - since / ShakeSeconds));

    // The flash when full again: up and down once.
    internal static float Flash(float since)
        => since < 0f || since >= FlashSeconds ? 0f : 1f - Math.Abs(since / FlashSeconds * 2f - 1f);

    // The pause button's glow (alpha of white over its selected look) and its frame's alpha.
    internal static float Glow(float run) => GlowPeak * Beat(run, PulsePeriod);

    internal static float FrameAlpha(float run) => FrameLow + (1f - FrameLow) * Beat(run, PulsePeriod);

    // Fade of clock tint and rim towards target (1 while slow motion runs, else 0).
    internal static float Approach(float current, float target, float seconds)
    {
        var step = Math.Max(0f, seconds) / FadeSeconds;
        return current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
    }

    // The rim's weight: faded in and breathing.
    internal static float RimWeight(float fade, float run)
        => Math.Clamp(fade, 0f, 1f) * (BreathBase + BreathSwing * (float)Math.Sin(run * 2.0 * Math.PI / BreathPeriod));

    // Nothing moves any more: the display's routine may end.
    internal static bool Calm(Mode mode, float fade) => mode == Mode.Full && fade <= 0f;
}
