using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// DIAGNOSTIC 1a: the game
// has no shared warning object; each skill draws its own red area, mostly a VFX_AffectArea whose VFXID ends in "Area"
// (not every one, AcidSpread_Area is a VFX_Sprite; StoneStompArea is never made; some ids serve several
// skills, and Explosion_Area belongs to the player's SKILL_Explosion). The probe logs each such area as it is
// created, its cell and the party units standing in it, and its duration once the game sets it; and, once per id, every
// other VFX first seen (whether a death explosion draws an area at all). Observing only.
internal static class GroupAiAreaProbeRules
{
    internal const int AreaCap = 300;
    internal const int SeenCap = 64;
    // The same area on the same cell again within this many game ticks (3 s; the Strange Seed pulses its area every
    // second) is only counted; the next line for it says how many were skipped.
    internal const long RepeatTicks = 180;

    internal static bool Repeat(long lastTick, long tick) => lastTick >= 0 && tick >= lastTick && tick - lastTick < RepeatTicks;

    internal static bool IsArea(string vfxName)
        => !string.IsNullOrEmpty(vfxName) && vfxName.EndsWith("Area", StringComparison.Ordinal);

    // The cell under a world position, from a unit whose cell and world position are known (one world unit per cell).
    internal static (int X, int Y) CellOf(float worldX, float worldY, int refCellX, int refCellY, float refWorldX, float refWorldY)
        => (refCellX + (int)Math.Round(worldX - refWorldX, MidpointRounding.AwayFromZero),
            refCellY + (int)Math.Round(worldY - refWorldY, MidpointRounding.AwayFromZero));

    // A unit stands in the area's cell when its world position lies within half a cell of the area's.
    internal static bool Inside(float unitX, float unitY, float areaX, float areaY)
        => Math.Abs(unitX - areaX) < 0.5f && Math.Abs(unitY - areaY) < 0.5f;

    internal static string CreateLine(string fx, string vfx, int id, float x, float y, string cell, string inside, long tick, int skipped = 0)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_AREA_FX_PROBE kind=create fx={0} vfx={1} id={2} at={3:0.00},{4:0.00} cell={5} inside={6} tick={7}",
            fx, vfx, id, x, y, cell ?? "-", string.IsNullOrEmpty(inside) ? "-" : inside, tick)
            + (skipped > 0 ? " skipped=" + skipped.ToString(CultureInfo.InvariantCulture) : "");

    internal static string DurationLine(string fx, string vfx, float duration, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_AREA_FX_PROBE kind=duration fx={0} vfx={1} duration={2:0.00} tick={3}",
            fx, vfx, duration, tick);

    internal static string SeenLine(string vfx, int id, float x, float y, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_VFX_SEEN vfx={0} id={1} at={2:0.00},{3:0.00} tick={4}", vfx, id, x, y, tick);

    // The effect id in short: its first eight hex digits, enough to pair create and duration lines.
    internal static string Short(string guid) => string.IsNullOrEmpty(guid) ? "-" : guid.Length > 8 ? guid.Substring(0, 8) : guid;
}
