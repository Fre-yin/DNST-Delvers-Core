using DungeonSettlersDelvers.Core;
using static TestKit;

// Blood of a wound: pools, ground stains, body marks.
internal static class WoundBloodSection
{
    internal static void Run()
    {
        PoolOnTheGround();
        PoolOnlyStanding();
        PoolRenewed();
        GroundStainOrder();
        BodyMarks();
    }

    static void PoolOnTheGround()
    {
        // (the pool walked along with Frieren): the pool from a deep wound on stays on the ground after the
        // unit moved a cell, 6 s each, at most 3 per unit; never bound to the unit. No small stains on the ground any more,
        // they sit on the body instead, 1, 2 or 3 by stage.
        Check(WoundRules.BloodSpot(WoundRules.Stage.Light, false, 0) == null && WoundRules.BloodSpot(WoundRules.Stage.Deep, false, 0) == "pool"
                && WoundRules.BloodSpot(WoundRules.Stage.Heavy, true, 1) == "pool" && WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 0) == null
                && WoundRules.BloodSpot(WoundRules.Stage.None, false, 0) == null
                && WoundRules.BodyMarks(WoundRules.Stage.Light) == 1 && WoundRules.BodyMarks(WoundRules.Stage.Deep) == 2
                && WoundRules.BodyMarks(WoundRules.Stage.Heavy) == 3 && WoundRules.BodyMarks(WoundRules.Stage.None) == 0
                && WoundRules.SpotSeconds == 6f && WoundRules.SpotMaxPerUnit == 3 && WoundRules.SpotMinCells == 1,
            "wound: the pool stays on the ground from deep on, the scratches sit on the body by stage");
    }

    static void PoolOnlyStanding()
    {
        // The pool only after the unit stood still 2 s (setting), walking leaves none.
        Check(WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 1, 1.0, 2.0) == null && WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 1, 2.0, 2.0) == "pool"
                && WoundRules.BloodSpot(WoundRules.Stage.Fatal, false, 0, 0.0, 2.0) == null && WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 0, 9.0, 2.0) == null
                && WoundRules.StillSeconds(true, 0, 1.0, 1.0) == 2.0 && WoundRules.StillSeconds(true, 1, 5.0, 1.0) == 0 && WoundRules.StillSeconds(false, 0, 5.0, 1.0) == 0
                && WoundRules.PoolStill(null) == 2.0 && WoundRules.PoolStill(-1) == 0 && WoundRules.PoolStill(99) == 30 && WoundRules.PoolStill(double.NaN) == 2.0
                && WoundRules.PoolStill(3.5) == 3.5 && WoundRules.PoolStillKey == "wounds.fx.poolstillseconds",
            "wound: a new pool only after 2 s on the same cell (wounds.fx.poolstillseconds, 0-30); a step resets the time");
    }

    static void PoolRenewed()
    {
        // Standing, the pool is renewed before its 6 s run out, so one always lies under the unit.
        Check(WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 0, 9.0, 2.0, 4.0) == null && WoundRules.BloodSpot(WoundRules.Stage.Deep, true, 0, 9.0, 2.0, 4.5) == "pool"
                && WoundRules.BloodSpot(WoundRules.Stage.Light, true, 0, 9.0, 2.0, 9.0) == null && WoundRules.BloodSpot(WoundRules.Stage.Heavy, true, 0, 1.0, 2.0, 9.0) == null
                && WoundRules.PoolRenewSeconds > 0 && WoundRules.PoolRenewSeconds + 1.0 <= WoundRules.SpotSeconds,
            "wound: standing on its pool's cell the unit gets a new pool 4.5 s after the last, before the 6 s run out; walking still none");
        Check(!WoundRules.LyingMarksAllowed(0.0, 2.0) && !WoundRules.LyingMarksAllowed(1.9, 2.0) && WoundRules.LyingMarksAllowed(2.0, 2.0) && WoundRules.LyingMarksAllowed(0.0, 0.0),
        "wound: a lying unit's marks only once it has lain still 2 s, none on the way while carried");
    }

    static void GroundStainOrder()
    {
        // The author's sorting: the ground uses HitBloodGround_red_B and _C (45, 46) in turn, never HitBloodGround_red (44).
        Check(WoundRules.GroundStain(0) == 45 && WoundRules.GroundStain(1) == 46 && WoundRules.GroundStain(2) == 45 && WoundRules.GroundStain(7) == 46,
            "wound: the ground stains are the _B and _C pictures only");
    }

    static void BodyMarks()
    {
        // A lying unit's body marks are HitBloodGround_red, _B, _C (44-46) in turn as before.
        Check(WoundRules.BodyMarkPicture(0) == 44 && WoundRules.BodyMarkPicture(1) == 45
                && WoundRules.BodyMarkPicture(2) == 46 && WoundRules.BodyMarkPicture(3) == 44,
            "wound: the lying body marks are 03-05");
        Check(WoundRules.LeavesTrail(WoundRules.Stage.Heavy, false, 0) && WoundRules.LeavesTrail(WoundRules.Stage.Heavy, true, 2)
                && !WoundRules.LeavesTrail(WoundRules.Stage.Heavy, true, 1) && !WoundRules.LeavesTrail(WoundRules.Stage.Deep, false, 0)
                && !WoundRules.LeavesTrail(WoundRules.Stage.Light, true, 9),
            "wound trail: only a lethal wound leaves stains, the first at once, then only after two cells of walking");
        Check(WoundRules.TrailStainSeconds > 0 && WoundRules.TrailMaxStains >= 1,
            "wound trail: stains on the ground fade after a while and a unit keeps only a few");
        static bool Near((float From, float To)? band, float from, float to)
            => band is { } b && Math.Abs(b.From - from) < 1e-5 && Math.Abs(b.To - to) < 1e-5;
        Check(Near(WoundRules.BandShare(false, WoundRules.Stage.Deep, 30, 100, 50, 0.8f), 0.56f, 0.8f)
                && WoundRules.BandShare(true, WoundRules.Stage.Deep, 30, 100, 50, 0.8f) == null
                && Near(WoundRules.BandShare(true, WoundRules.Stage.Heavy, 130, 100, 50, 1f), 0.4f, 1f)
                && Near(WoundRules.BandShare(true, WoundRules.Stage.Light, 20, 0, 50, 1f), 0.6f, 1f)
                && Near(WoundRules.BandShare(false, WoundRules.Stage.Light, 200, 100, 50, 1f), 0f, 1f)
                && WoundRules.BandShare(false, WoundRules.Stage.None, 30, 100, 50, 1f) == null
                && WoundRules.BandShare(false, WoundRules.Stage.Deep, 0, 100, 50, 1f) == null
                && WoundRules.BandShare(false, WoundRules.Stage.Deep, 30, 100, 50, 0f) == null
                && WoundRules.BandShare(true, WoundRules.Stage.Deep, 30, 0, 0, 1f) == null,
            "wound bars: the body bar shows the whole buffer, the head bar only what the body cannot take");
        Check(WoundRules.TickAfter(1000, 2.5) == 1150 && WoundRules.TickAfter(1000, -5) == 1000
                && WoundRules.TickAfter(0, WoundRules.TrailStainSeconds) == 3600,
            "wound: an end tick lies the game seconds times 60 ticks ahead, never behind");
        Check(WoundRules.BleedingCut(9, WoundRules.HerbalBleedingCut) == 3 && WoundRules.BleedingCut(10, WoundRules.HerbalBleedingCut) == 4
                && WoundRules.BleedingCut(1, WoundRules.HerbalBleedingCut) == 1 && WoundRules.BleedingCut(7, WoundRules.SalveBleedingCut) == 4
                && WoundRules.BleedingCut(8, WoundRules.SalveBleedingCut) == 4 && WoundRules.BleedingCut(0, WoundRules.SalveBleedingCut) == 0
                && WoundRules.BleedingCut(5, 0) == 0 && WoundRules.BleedingCut(3, 2) == 3,
            "bandages: herbal takes a third of the game's bleeding stacks, salve half, rounded up and never more than there are");
        Check(WoundRules.LightBandagePrice == 20 && WoundRules.HeavyBandagePrice == 45
                && WoundRules.HerbalBandagePrice == 250 && WoundRules.SalveBandagePrice == 280
                && WoundRules.HerbalBandagePrice < WoundRules.SalveBandagePrice && WoundRules.SalveBandagePrice < WoundRules.AidKitPrice,
            "bandages: herbal 250 and salve 280, just below the game's first aid kit (300), the salve the dearer one");
        Check(WoundRules.Heal(WoundRules.Stage.Deep, WoundRules.HealKind.Regeneration, 30, 50, 134, WoundRules.HealFactor(WoundRules.Stage.Deep)) == (0, 0)
                && WoundRules.Heal(WoundRules.Stage.Deep, WoundRules.HealKind.Portal, 40, 50, 134, WoundRules.HealFactor(WoundRules.Stage.Deep)) == (40, 40)
                && WoundRules.Heal(WoundRules.Stage.None, WoundRules.HealKind.Regeneration, 30, 50, 134, WoundRules.HealFactor(WoundRules.Stage.None)) == (30, 0),
            "wound: an open wound stops the body's regeneration, the portal heals and closes it, a closed wound regenerates again");
    }
}
