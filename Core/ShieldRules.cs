using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The honeycomb shield's rules, apart from the boss bar's: the tile, the band's place and rows, the layers from the 200 %
// cap, colours, the party's opacities,
// the wave and the small helpers of the band. Pure, tested without the game. The boss bar's own opacity stays in
// BanditBossBarRules (ShieldKey, ShieldAlpha, ShieldOpacity).
internal static class ShieldRules
{
    // The honeycomb tile: a regular honeycomb with flat tops, cells of radius 8 pixels, whose centres lie at (0, 0) and
    // (HoneyWidth / 2, HoneyHeight / 2) of a HoneyWidth x HoneyHeight tile, so the tile repeats without a seam. Per pixel
    // (rows from the bottom): 1 on a cell's edge (the two nearest centres almost equally far), 2 on the rim just inside it, 0
    // inside the cell.
    internal const int HoneyWidth = 24;
    internal const int HoneyHeight = 14;
    internal const double HoneyEdge = 1.4;
    internal const double HoneyRim = 2.8;

    internal static int HoneyKind(int x, int y)
    {
        var px = ((x % HoneyWidth) + HoneyWidth) % HoneyWidth + 0.5;
        var py = ((y % HoneyHeight) + HoneyHeight) % HoneyHeight + 0.5;
        double w = HoneyWidth, h = HoneyHeight;
        double nearest = double.MaxValue, second = double.MaxValue;
        foreach (var (cx, cy) in new[] { (0d, 0d), (w, 0d), (0d, h), (w, h), (w / 2, h / 2), (w / 2, -h / 2), (w / 2, 1.5 * h), (-w / 2, h / 2), (1.5 * w, h / 2) })
        {
            var d = Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            if (d < nearest) (nearest, second) = (d, nearest);
            else if (d < second) second = d;
        }
        var gap = second - nearest;
        return gap < HoneyEdge ? 1 : gap < HoneyRim ? 2 : 0;
    }

    // The shell's look as RGBA per kind: a grey, see-through cell, a light sand edge, a dark rim.
    internal static readonly byte[][] HoneyColours = { new byte[] { 140, 140, 150, 110 }, new byte[] { 222, 214, 180, 255 }, new byte[] { 55, 55, 60, 190 } };

    // The honeycomb is tiled in cells of a set height, not squeezed into the fill's 12 units. Cell height
    // in UI units as setting bandits.bossbar.shield.cell, ShieldCellDefault by default, ShieldCellMin to ShieldCellMax, the author
    // picks one. The band's height follows ShieldBand, its rows TileRows.
    internal const string ShieldCellKey = "bandits.bossbar.shield.cell";
    // (picture at cell 12, three rows):: 12 by default.
    internal const float ShieldCellDefault = 12f;
    internal const float ShieldCellMin = 8f;
    internal const float ShieldCellMax = 96f;

    // The
    // band lies over the segment's head and body bar together. From the lowest bottom and highest top edge of both bars (in the
    // band's parent space; from the bars' anchored positions it missed the bars): centre and height, at least
    // one cell high.
    internal static (float CentreY, float Height) ShieldBand(float bottom, float top, float cell)
    {
        if (top < bottom) (bottom, top) = (top, bottom);
        return ((top + bottom) / 2f, Math.Max(top - bottom, cell));
    }

    // Whole honeycomb rows over the band (the cell height rounded to fit), so the tiling does not cut a row and its place does
    // not shift with the cell (the motifs' height changed with the cell). A row is one HoneyHeight tile.
    internal static int TileRows(float bandHeight, float cell)
        => bandHeight > 0f && cell > 0f ? Math.Max(1, (int)Math.Round(bandHeight / cell, MidpointRounding.AwayFromZero)) : 1;

    // The band
    // meets the outer edges of head and body bar (their frames included; ShieldEdges), and a white frame of ShieldBorder units
    // lines it. Over the dark gap between head and body bar (setting bandits.bossbar.shield.gap, 1 by default) or one band per
    // bar (0); the author picks at the picture.
    internal const float ShieldBorder = 1f;
    internal const string ShieldGapKey = "bandits.bossbar.shield.gap";

    // The left and right edge of a segment's bars: the outermost of body and head bar (the author's marks, the bars
    // showed left of the band, and right of the shieldbearer's).
    internal static (float Left, float Right) ShieldEdges(float bodyLeft, float bodyRight, bool hasHead, float headLeft, float headRight)
        => hasHead ? (Math.Min(bodyLeft, headLeft), Math.Max(bodyRight, headRight)) : (bodyLeft, bodyRight);

    // The shields together are capped at 2 x MaxHealthTotal (the
    // game's patch notes, quoted), so the share is taken of
    // MaxHealthTotal and the layers follow from the cap: 100 % each, every one from the left in its own colour over the one
    // below, full.
    internal const float ShieldCapShare = 2f;
    internal static readonly int ShieldLayerCount = (int)Math.Ceiling(ShieldCapShare);

    // A layer's colour setting, as an RGB number (decimal in the settings file): the honeycomb's own sand white, then light blue;
    // a layer past the list takes the last one. The author picks at the picture.
    internal static readonly int[] ShieldColourDefaults = { 0xFFFFFF, 0x8FD3FF };

    internal static string ShieldColourKey(int layer) => "ui.shield.colour." + (layer + 1).ToString(CultureInfo.InvariantCulture);

    // The Blood Warden's shield is a shield of its own (its rest heals; it counts apart from the others), so
    // its part of the band shows in a red of the bleeding band's (WoundText's light red), setting ui.shield.colour.blood; the author
    // picks at the picture. Its section stands left of the other shields, never over them (BloodSections).
    internal const string BloodColourKey = "ui.shield.colour.blood";
    internal const int BloodColourDefault = 0xF04848;

    // With a blood shield the band shows two sections side by side, never over each other (no colour
    // overwrites another, nothing shows through): the blood shield from the left, as the game uses it up first (BossEffects.
    // PutFirst), then the other shields. Together above the band's width they share it by their shares. The others' section takes
    // the overshield colour (layer 1) when they alone pass 100 %, else the first. Without a blood shield the band is as before.
    internal static (float BloodWidth, float OtherOffset, float OtherWidth, int OtherLayer) BloodSections(float fullWidth, float otherShare,
        float bloodShare)
    {
        var full = float.IsNaN(fullWidth) ? 0f : Math.Max(0f, fullWidth);
        var blood = float.IsNaN(bloodShare) ? 0f : Math.Max(0f, bloodShare);
        var others = float.IsNaN(otherShare) ? 0f : Math.Max(0f, otherShare);
        var scale = blood + others > 1f ? 1f / (blood + others) : 1f;
        var bloodWidth = full * blood * scale;
        return (bloodWidth, bloodWidth, full * others * scale, others > 1f ? 1 : others > 0f ? 0 : -1);
    }

    internal static int ShieldColourDefault(int layer) => ShieldColourDefaults[Math.Clamp(layer, 0, ShieldColourDefaults.Length - 1)];

    // The layer below shown full (-1: none), the current layer (-1: no shield) and its share of the width.
    internal static (int Below, int Layer, float Part) ShieldLayers(float share, int layers)
    {
        if (layers <= 0 || float.IsNaN(share) || share <= 0f) return (-1, -1, 0f);
        if (share >= layers) return (layers - 2, layers - 1, 1f);
        var whole = (int)Math.Floor(share);
        var part = share - whole;
        if (part <= 1e-6f) return (whole - 2, whole - 1, 1f);
        return (whole - 1, whole, part);
    }

    // A colour setting as RGB 0 to 1: a whole number from 0 to 0xFFFFFF, else the fallback.
    internal static (float R, float G, float B) ShieldColour(double? setting, int fallback)
    {
        var rgb = setting is double v && !double.IsNaN(v) && v >= 0 && v <= 0xFFFFFF && v == Math.Floor(v) ? (int)v : fallback;
        return (((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }

    // The shield's share of MaxHealthTotal, the cap's measure, not cut at 100 % (the layers show what lies above).
    internal static float ShieldShare(float barrier, float maxTotal, bool dead)
        => dead || float.IsNaN(maxTotal) || maxTotal <= 0f || float.IsNaN(barrier) || barrier <= 0f ? 0f : barrier / maxTotal;

    // Then at the picture:
    // "Spielerleiste muss deutlich schwächer sein, da ich die HP quasi nicht mehr lesen kann". The party's band sets its lines
    // (honeycomb edges and white frame) and its cells apart: ui.shield.opacity for the lines (PartyShieldAlpha by default),
    // ui.shield.fill for the cells (PartyFillAlpha, nearly clear), both 0 to 1; the boss bar keeps ShieldKey and its tile.
    // , later the same day, once the layers lay side by side and the frame outside::
    // the defaults rise (lines 0.55, cells 0.1), and a layer above the first shows stronger by ui.shield.boost (1 to 2, its
    // opacity times this, at most 1). Toolkit shows the variants; the author picks.
    internal const string PartyShieldKey = "ui.shield.opacity";
    internal const float PartyShieldAlpha = 0.55f;
    internal const string PartyFillKey = "ui.shield.fill";
    internal const float PartyFillAlpha = 0.1f;
    internal const string PartyBoostKey = "ui.shield.boost";
    internal const float PartyBoostDefault = 1.3f;

    // The opacity of a layer: the first as set, every one above it times the boost, at most 1.
    internal static float LayerOpacity(float alpha, int layer, double? boost)
    {
        var factor = boost is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 1.0, 2.0) : PartyBoostDefault;
        return layer <= 0 || float.IsNaN(alpha) ? alpha : Math.Min(1f, alpha * factor);
    }

    internal static float PartyShieldOpacity(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 0.0, 1.0) : PartyShieldAlpha;

    internal static float PartyFillOpacity(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 0.0, 1.0) : PartyFillAlpha;

    // The cells' own alpha in the tile so that, under the band's opacity for the lines, they show at the fill's opacity.
    internal static byte CellAlpha(float fill, float lines)
        => lines <= 0f || float.IsNaN(lines) || float.IsNaN(fill) ? (byte)0 : (byte)Math.Round(255f * Math.Clamp(fill / lines, 0f, 1f));

    // (the wave GIFs: "passt"): a wave runs from left to right over the band; at its crest the opacity is
    // exactly the chosen one, never brighter, in its trough the trough's share of it, a cosine in between. x counts from the
    // band's left edge, so the wave runs through both layers without a jump; t is game time (it stands still in a pause).
    internal static float WaveFactor(float x, float t, float wavelength, float period, float trough)
    {
        if (float.IsNaN(x) || float.IsNaN(t) || wavelength <= 0f || period <= 0f || float.IsNaN(trough)) return 1f;
        var low = Math.Clamp(trough, 0f, 1f);
        var phase = 2.0 * Math.PI * (x / wavelength - t / period);
        return (float)(low + (1.0 - low) * (0.5 + 0.5 * Math.Cos(phase)));
    }

    // The wave's settings per band: the party's (ui.shield.wave...) and the boss bar's (bandits.bossbar.shield.wave...), on (1)
    // or off (0), trough (0 to 1), wavelength in UI units (16 to 240), period in seconds (0.5 to 6). The author's values: party
    // 0.55 / 60 / 1.6, boss 0.35 / 48 / 1.6.
    internal const string PartyWaveKey = "ui.shield.wave";
    internal const string BossWaveKey = "bandits.bossbar.shield.wave";
    internal const float PartyWaveTrough = 0.55f;
    internal const float PartyWaveLength = 60f;
    internal const float BossWaveTrough = 0.35f;
    internal const float BossWaveLength = 48f;
    internal const float WavePeriod = 1.6f;
    // Width of one strip of the band whose opacity follows the wave (UI units).
    internal const float WaveStripWidth = 4f;

    // After this many failures of the wave's frame step the wave goes off for the session and every
    // band stays calm at its chosen opacity (D); the log says so once.
    internal const int WaveFailureLimit = 3;

    // Whether two rectangles (min x, min y, max x, max y) overlap with an area; touching edges do not count.
    internal static bool Overlaps(float aMinX, float aMinY, float aMaxX, float aMaxY, float bMinX, float bMinY, float bMaxX, float bMaxY)
        => aMinX < bMaxX && bMinX < aMaxX && aMinY < bMaxY && bMinY < aMaxY;

    internal static (bool On, float Trough, float Length, float Period) WaveSettings(bool boss, double? on, double? trough, double? length, double? period)
        => (BanditBossBarRules.ViewOn(on),
            trough is double tr && !double.IsNaN(tr) ? (float)Math.Clamp(tr, 0.0, 1.0) : boss ? BossWaveTrough : PartyWaveTrough,
            length is double l && !double.IsNaN(l) ? (float)Math.Clamp(l, 16.0, 240.0) : boss ? BossWaveLength : PartyWaveLength,
            period is double pe && !double.IsNaN(pe) ? (float)Math.Clamp(pe, 0.5, 6.0) : WavePeriod);

    // The strips of a band: count and width so they cover it, at least one.
    internal static int WaveStrips(float fullWidth)
        => fullWidth > 0f && !float.IsNaN(fullWidth) ? Math.Max(1, (int)Math.Ceiling(fullWidth / WaveStripWidth)) : 1;

    // The lower layer's place when the upper one covers part of the band: from the upper one's end (offset) over the rest.
    internal static (float Offset, float Remain) LowerSpan(float fullWidth, float upperPart)
    {
        var upper = ShieldWidth(fullWidth, upperPart);
        return (upper, Math.Max(0f, (float.IsNaN(fullWidth) ? 0f : fullWidth) - upper));
    }

    // The author picks at the picture: the frame's line where the upper layer ends inside the band (1, by default) or none (0).
    internal const string DividerKey = "ui.shield.divider";

    // A band narrower than its frame on both sides shows only a frame stroke; it stays hidden.
    internal static bool ShieldShown(float width) => !float.IsNaN(width) && width > 2f * ShieldBorder;

    // (yes to the party's shield): the honeycomb on the party's bars (portraits and status window) as
    // setting ui.shield.honeycomb (1 by default, 0 off). ui.shield.preview: a share shown instead of the real one, only in a
    // test campaign, for the pictures (0 to ShieldLayerCount).
    internal const string HoneycombKey = "ui.shield.honeycomb";
    internal const string PreviewKey = "ui.shield.preview";

    internal static float? ShieldPreview(double? setting, bool testCampaign)
        => testCampaign && setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, 0.0, ShieldLayerCount) : null;

    internal static float ShieldCell(double? setting)
        => setting is double v && !double.IsNaN(v) ? (float)Math.Clamp(v, ShieldCellMin, ShieldCellMax) : ShieldCellDefault;

    // The pixelsPerUnitMultiplier of a tiled image that puts rows tiles of the sprite over the band's height (UI units):
    // a tile is spriteHeight / (spritePpu / referencePpu * multiplier) high. 1 when a value is unknown.
    internal static float TileMultiplier(float spriteHeight, float spritePpu, float referencePpu, float bandHeight, float rows)
        => spriteHeight > 0f && spritePpu > 0f && referencePpu > 0f && bandHeight > 0f && rows > 0f
            ? spriteHeight * referencePpu * rows / (spritePpu * bandHeight) : 1f;

    // A tiled band shows its share by its width from the left edge (a tiled image has no fill amount).
    internal static float ShieldWidth(float fullWidth, float share)
        => fullWidth > 0f && !float.IsNaN(share) ? fullWidth * Math.Clamp(share, 0f, 1f) : 0f;
}
