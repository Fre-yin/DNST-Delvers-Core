namespace DungeonSettlersDelvers.Core;

// The revive scroll's look (variant 4 of the preview): the return scroll's paper gold-yellow, its red ribbon
// silver in three greys, and one faint, half transparent ring around it. Pure, so the offline tests cover it; ScrollGlow applies
// it to the game's sprite at runtime. A tone counts when every channel lies within Tolerance of the game's colour, so a slightly
// different import of the icon still turns; every other pixel (outline, transparent) stays.
internal static class ScrollGlowRules
{
    internal const int Tolerance = 6;

    // The game's tone, then the tone it becomes: paper light, middle, dark; ribbon dark, light, middle.
    internal static readonly ((byte R, byte G, byte B) From, (byte R, byte G, byte B) To)[] Turns =
    {
        ((0xC1, 0xBD, 0xA1), (0xF2, 0xCF, 0x5C)),
        ((0x8D, 0x82, 0x71), (0xC9, 0x96, 0x2C)),
        ((0x6E, 0x5F, 0x57), (0x8C, 0x60, 0x1E)),
        ((0x91, 0x0F, 0x29), (0x6C, 0x72, 0x7C)),
        ((0xCF, 0x2D, 0x27), (0xC4, 0xCA, 0xD2)),
        ((0xCA, 0x33, 0x2D), (0xA6, 0xAD, 0xB7)),
    };

    // The ring on the transparent pixels touching the scroll; none further out.
    internal static readonly (byte R, byte G, byte B, byte A) Ring = (255, 236, 140, 90);

    internal static (byte R, byte G, byte B) Turn(byte r, byte g, byte b)
    {
        foreach (var (from, to) in Turns)
            if (Near(r, from.R) && Near(g, from.G) && Near(b, from.B)) return to;
        return (r, g, b);
    }

    private static bool Near(byte value, byte wanted) => Math.Abs(value - wanted) <= Tolerance;
}
