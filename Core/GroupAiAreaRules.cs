using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The party dodges the harmful areas of enemies (the threshold for 8: below 50 %, head and body together). Which areas, their shape when
// the game's own area of the
// skill is not at hand, the low health rule for the arrow rain, the dodge cell and the log lines. Rules without the game.
// Sources: (the enemies' areas), (areas the skills compute), probe 1a/1b, research table skills.tsv.
internal static class GroupAiAreaRules
{
    // Chain: the phantom chain's own shape (SKILL_PhantomChain.GetPositionsInCircleRange, per row dy in -r..r the cells with
    // |dx| <= r - max(0, |dy| - 1)), that is Manhattan up to r + 1 without its four axis tips.
    internal enum Shape { Cell, Square, Manhattan, Circle, Chain }

    internal sealed class Area
    {
        internal string Flag;
        internal string Source;
        internal int Vfx;
        internal Shape Shape;
        internal int Radius;
        internal bool Lane;
        internal bool LowHealthOnly;
        internal bool Dodge;
        // Around the user instead of the target (the stomp and the crash of the golem).
        internal bool CenterOnUser;
        // Rows the user's cells move up before the area spreads: the phantom chain adds (0, 1) to every cell it returns
        // (SKILL_PhantomChain.GetPositionsInCircleRange); the earth crash's routine carries its own lambda p => (p.x, p.y + 1)
        // (SKILL_EarthCrash <>c.<SkillRoutine>b__0_0): at the damage query the routine moves the caster's own cells with it and only
        // then spreads the Manhattan area around them, as Core does here; the push counts from the unmoved centre, and the drawn
        // cells keep no shift. F2 centres on the golem (CenterOnUser), so it always applies.
        internal int ShiftY;
        // Seconds from the execution's start: when the skill first asks who stands in it, and when it asks the last time. What
        // counts is where a unit stands at that query; walking off later saves nothing, unless the skill asks again (waves, a
        // charge on its way, the phantom chain's list that drops whoever leaves): then a later step out still helps (Repeats).
        internal float FirstHit;
        internal float LastHit;
        internal bool Repeats;
        // Seconds to hold the zone when the game's execution gives no end: the last query plus a margin.
        internal float HoldSeconds => LastHit + HoldMargin;
        // Waves from the skill row (the arrow rain:, SKILL_ArrowRain's shoot routine, one wave per round(FloatValues[1]),
        // 0.5 s apart): the index of the count in FloatValues, -1 for none, and the seconds between waves.
        internal int WavesIndex = -1;
        internal float WaveEvery;
    }

    // F1 to F9 in Toolkit's order. The shape is the skill row's area (skills.tsv: area, radius); F9 has no skill row, its 3x3
    // comes from probe 1b (10 of 12 hits at distance 0 to 1, 0 of 36 from 2). The times follow the catalog's query table
    // (from the skill's start after its start delay; F2 has no catalog value, its first hit is measured in game):
    // the charge of the elder gloth runs from 1.5 s at 8 cells a second over up to 12 cells, the earth crash hits at 2.5 s
    // (measured in game) and pushes in rings up to 3.3 s (not confirmed: the measuring runs hit the target only once),
    // the stomp hits at about 0.62 s,
    // the metal charge runs from 2.75 s, the phantom chain fixes its list at 1.21 s and hits until 5.8 s, the arrow rain's six
    // waves come at 1.42 s and every 0.5 s after, each asking anew, the corpse bursts 1.5 s after the death.
    internal static readonly Area[] Areas =
    {
        new() { Flag = "F1", Source = "SKILL_ElderGlothCharge", Vfx = 200, Shape = Shape.Square, Radius = 1, Lane = true, Dodge = true, FirstHit = 1.5f, LastHit = 3.0f, Repeats = true },
        // F2 and F3 draw no cells before they hit (the earth crash marks only its centre, the stomp's cells
        // come with the damage), so the party stands in them. F7 is dodged after the author watched it against the boss ("the red field
        // announces the big area": the red area around the knight is its warning). The rule for any new area: dodged only where
        // the game warns first. Shapes, times and the shift stay for the record.
        new() { Flag = "F2", Source = "SKILL_EarthCrash", Vfx = 168, Shape = Shape.Manhattan, Radius = 3, CenterOnUser = true, ShiftY = 1, Dodge = false, FirstHit = 2.5f, LastHit = 3.3f, Repeats = true },
        new() { Flag = "F3", Source = "SKILL_StoneStomp", Vfx = 169, Shape = Shape.Square, Radius = 1, CenterOnUser = true, Dodge = false, FirstHit = 0.62f, LastHit = 0.62f },
        // F4 holds to 4.5 s, above the catalog's 4.25 s; the later end is the more careful side.
        new() { Flag = "F4", Source = "SKILL_MetalCharge", Vfx = 179, Shape = Shape.Cell, Radius = 0, Lane = true, Dodge = true, FirstHit = 2.75f, LastHit = 4.5f, Repeats = true },
        new() { Flag = "F5", Source = "SKILL_AcidSpread", Vfx = 192, Shape = Shape.Square, Radius = 7, Dodge = false },
        new() { Flag = "F6", Source = "SKILL_BloodRain", Vfx = 204, Shape = Shape.Square, Radius = 2, Dodge = false },
        // F7 around the knight, not its target (the zone was a circle 5 around the dodger's cell, so the dodge cell
        // 441,144 lay outside it but inside the chain's own area around the knight at 443,139).
        new() { Flag = "F7", Source = "SKILL_PhantomChain", Vfx = 214, Shape = Shape.Chain, Radius = 5, CenterOnUser = true, ShiftY = 1, Dodge = true, FirstHit = 1.21f, LastHit = 5.8f, Repeats = true },
        new() { Flag = "F8", Source = "SKILL_ArrowRain", Vfx = 84, Shape = Shape.Manhattan, Radius = 2, LowHealthOnly = true, Dodge = true, FirstHit = 1.42f, LastHit = 3.92f, Repeats = true, WavesIndex = 1, WaveEvery = 0.5f },
        new() { Flag = "F9", Source = "AFFECTER_ExplosiveCorpse", Vfx = 185, Shape = Shape.Square, Radius = 1, Dodge = true, FirstHit = 1.5f, LastHit = 1.5f },
        // AFFECTER_SporeExplosive draws the same area 185 at its
        // cell as the corpse (AffecterFX_SporeExplosive OnFXCall, only its hit differs: 190), so the same rule.
        new() { Flag = "F9", Source = "AFFECTER_SporeExplosive", Vfx = 185, Shape = Shape.Square, Radius = 1, Dodge = true, FirstHit = 1.5f, LastHit = 1.5f },
    };
    // (the party left the chain in time, then the game's own combat movement walked it back in
    // before the final blast): a unit that dodged an enemy area and walks back in while it holds is sent out again, at most
    // MaxDodgeAgain times per area and unit. done: dodges so far in this area; left: seen outside it since the last one.
    internal const int MaxDodgeAgain = 3;

    // While an enemy area holds, a unit outside it takes no action that leads it in. The
    // cells it walks to come in reach of a target (straight steps, as PathCells; reach is the game's range plus its extra cell,
    // GroupAiRules.InRange), up to the first cell in reach; empty when it is in reach already or the skill has no range.
    internal static List<(int X, int Y)> Approach(int fromX, int fromY, int toX, int toY, int range)
    {
        var cells = new List<(int X, int Y)>();
        if (range <= 0) return cells;
        var reach = range + 1;
        int x = fromX, y = fromY;
        while (Math.Max(Math.Abs(x - toX), Math.Abs(y - toY)) > reach)
        {
            x += Math.Sign(toX - x);
            y += Math.Sign(toY - y);
            cells.Add((x, y));
        }
        return cells;
    }

    internal static bool ApproachEntersZone(int fromX, int fromY, int toX, int toY, int range, Func<int, int, bool> inZone)
        => Approach(fromX, fromY, toX, toY, range).Any(c => inZone(c.X, c.Y));

    // ("yes, build"; Darius walked in and was sent out three times): the hold. Only for a unit
    // whose last target choice found nothing in reach (skip reason=zone), only while the zone it would cross holds, only while its
    // way still crosses it.
    internal static bool HoldApplies(bool skipped, float now, float until, bool approachEnters)
        => skipped && now < until && approachEnters;

    internal static string HoldLine(string unit, string skill, string target, float until)
        => "CORE_AI_ZONE_HOLD unit=" + unit + " skill=" + skill + " target=" + target + " until="
            + until.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    internal static string LockLine(string unit, string skill, string from, string to, string result)
        => "CORE_AI_ZONE_LOCK unit=" + unit + " skill=" + skill + " target=" + from + " to=" + to + " result=" + result;

    internal static bool DodgeAllowed(bool enemyArea, int done, bool left)
        => done == 0 || (enemyArea && left && done <= MaxDodgeAgain);

    // Below this share of the whole health (head and body) a unit also dodges the arrow rain.
    internal const double LowHealthShare = 0.5;

    // With less time left than this before the skill asks who stands in it, a unit does not start to walk any more.
    internal const float LateSeconds = 0.4f;

    internal static Area BySource(string source) => Areas.FirstOrDefault(a => a.Source == source);

    // Whether a unit with this health share steps out of the area: never for F5 and F6, F8 only below LowHealthShare.
    internal static bool Dodges(Area area, double healthShare)
        => area != null && area.Dodge && (!area.LowHealthOnly || (!double.IsNaN(healthShare) && healthShare < LowHealthShare));

    // The reason a unit stays, or null when it steps out; elapsed counts from the execution's start. Late: a skill that asks
    // once, with less than LateSeconds to its query. Over: the last query has passed.
    internal static string SkipReason(Area area, double healthShare, float elapsed, float? lastHit = null)
        => area == null || !area.Dodge ? "harmless" : !Dodges(area, healthShare) ? "hp" : elapsed > (lastHit ?? area.LastHit) ? "over"
            : !area.Repeats && area.FirstHit - elapsed < LateSeconds ? "late" : null;

    // The last query for this cast: with waves from the row, the first wave plus WaveEvery for each further one; else the rule's.
    internal static float LastHitFor(Area area, float[] values)
    {
        if (area == null) return 0f;
        if (area.WavesIndex < 0 || values == null || values.Length <= area.WavesIndex) return area.LastHit;
        var waves = (int)Math.Round(values[area.WavesIndex], MidpointRounding.ToEven);
        return waves < 1 ? area.FirstHit : area.FirstHit + area.WaveEvery * (waves - 1);
    }

    // Seconds a zone is held past the area's last query when the game's execution gives no end.
    internal const float HoldMargin = 0.3f;

    internal static float HoldFor(Area area, float[] values) => LastHitFor(area, values) + HoldMargin;

    // The end of a zone that starts at startedAt and whose area asks the last time after last seconds. The sum runs in this order, not
    // as startedAt + HoldFor(...): the other order can differ from it in the last bit of the float.
    internal static float HoldUntil(float startedAt, float last) => startedAt + last + HoldMargin;

    // The cells of a shape around a centre (the rule's fallback; the game's own area of the skill comes first).
    internal static List<(int X, int Y)> Cells(Shape shape, int radius, int x, int y)
    {
        var cells = new List<(int X, int Y)>();
        var r = Math.Max(0, radius);
        var reach = shape == Shape.Chain ? r + 1 : r;
        for (var dy = -reach; dy <= reach; dy++)
            for (var dx = -reach; dx <= reach; dx++)
            {
                var inside = shape switch
                {
                    Shape.Cell => dx == 0 && dy == 0,
                    Shape.Square => true,
                    Shape.Manhattan => Math.Abs(dx) + Math.Abs(dy) <= r,
                    Shape.Circle => dx * dx + dy * dy <= r * r,
                    Shape.Chain => Math.Abs(dy) <= r && Math.Abs(dx) <= r - Math.Max(0, Math.Abs(dy) - 1),
                    _ => false
                };
                if (inside) cells.Add((x + dx, y + dy));
            }
        return cells;
    }

    // The cells a straight walk from one cell to another passes before it arrives (one step a turn, diagonals allowed).
    internal static List<(int X, int Y)> PathCells(int fromX, int fromY, int toX, int toY)
    {
        var cells = new List<(int X, int Y)>();
        int x = fromX, y = fromY;
        while (x != toX || y != toY)
        {
            x += Math.Sign(toX - x);
            y += Math.Sign(toY - y);
            if (x != toX || y != toY) cells.Add((x, y));
        }
        return cells;
    }

    // A charge's lane (the sign-only direction missed the target, affectInCells 0 of 1): the
    // straight line from the user through the target, on to the skill's range; one cell a step, the target exactly on it.
    internal static List<(int X, int Y)> LaneCells(int fromX, int fromY, int toX, int toY, int range)
    {
        var cells = new List<(int X, int Y)>();
        int dx = toX - fromX, dy = toY - fromY, length = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (length == 0) return cells;
        for (var k = 1; k <= Math.Max(range, length); k++)
            cells.Add((fromX + (int)Math.Round(dx * (double)k / length, MidpointRounding.AwayFromZero),
                fromY + (int)Math.Round(dy * (double)k / length, MidpointRounding.AwayFromZero)));
        return cells;
    }

    // The cells a unit of this size may cover around its logical cell (the golem's Size 2x2 hit outside its one cell, Frieren
    // slowed outside the zone). Which corner the logical cell is, is not known, so every covering is counted.
    internal static List<(int X, int Y)> Footprint(int x, int y, int width, int height)
    {
        var cells = new List<(int X, int Y)>();
        int w = Math.Max(1, width), h = Math.Max(1, height);
        for (var dy = -(h - 1); dy <= h - 1; dy++)
            for (var dx = -(w - 1); dx <= w - 1; dx++) cells.Add((x + dx, y + dy));
        return cells;
    }

    // How far a unit looks for a way out: past the area's radius for the wide ones (the chain's circle 5).
    internal static int SearchRing(Area area, int baseRing) => Math.Max(baseRing, area == null ? 0 : Math.Min(7, area.Radius + 2));

    // The dodge cell: usable (free, walkable, in no zone), not through another zone on the way (crossing the unit's own zone is
    // its way out; EAD67420: every way out of a lane crossed it, to=none), not next to an enemy unless the unit fights in melee;
    // a melee unit keeps next to its target when it can; across a charge lane before along it; the nearest ring. When no cell
    // keeps off the enemies, a cell next to one is better than staying in the area.
    internal static bool PickCell(int x, int y, bool melee, bool hasAnchor, int ax, int ay, (int X, int Y)? laneDirection,
        Func<int, int, bool> usable, Func<int, int, bool> inZone, Func<int, int, bool> nextToEnemy, int maxRing, out int tx, out int ty)
        => Pick(x, y, melee, hasAnchor, ax, ay, laneDirection, usable, inZone, nextToEnemy, maxRing, out tx, out ty)
            || (!melee && Pick(x, y, true, false, ax, ay, laneDirection, usable, inZone, nextToEnemy, maxRing, out tx, out ty));

    private static bool Pick(int x, int y, bool melee, bool hasAnchor, int ax, int ay, (int X, int Y)? laneDirection,
        Func<int, int, bool> usable, Func<int, int, bool> inZone, Func<int, int, bool> nextToEnemy, int maxRing, out int tx, out int ty)
    {
        tx = x;
        ty = y;
        var bestScore = int.MaxValue;
        for (var ring = 1; ring <= maxRing; ring++)
            for (var dy = ring; dy >= -ring; dy--)
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                    int cx = x + dx, cy = y + dy;
                    if (!usable(cx, cy)) continue;
                    if (PathCells(x, y, cx, cy).Any(c => inZone(c.X, c.Y))) continue;
                    if (!melee && nextToEnemy(cx, cy)) continue;
                    // In this order: a melee unit next to its target, across a charge lane, the nearest ring, the straightest step.
                    var score = ring * 1000 + dx * dx + dy * dy;
                    if (melee && hasAnchor && GroupAiRules.Distance(cx, cy, ax, ay) != 1) score += 1000000;
                    if (laneDirection is { } lane && (lane.X != 0 || lane.Y != 0) && dx * lane.Y - dy * lane.X == 0) score += 100000;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        tx = cx;
                        ty = cy;
                    }
                }
        return bestScore != int.MaxValue;
    }

    // A unit in a zone does not start a bandage; the step out comes first.
    internal static bool BandageAllowed(bool inZone) => !inZone;

    internal static string ZoneLine(string flag, string unit, string source, string via, int cells, float until, string exec)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_ZONE side=players area={0} unit={1} source={2} via={3} cells={4} until={5:0.0} exec={6}",
            flag ?? "-", unit ?? "-", source ?? "-", via ?? "-", cells, until, exec ?? "-");

    internal static string SkipLine(string unit, string flag, string reason, double healthShare)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_AUSWEICHEN_SKIP unit={0} area={1} reason={2} hp={3:0.00}", unit ?? "-", flag ?? "-", reason, healthShare);
}
