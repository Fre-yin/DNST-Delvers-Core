using DungeonSettlersDelvers.Core;
using static TestKit;

// The mirrored camp building's footprint (BanditCampFootprintRules): its cells mirrored inside their common box, with the
// numbers of the Modding Toolkit's path check on the mirrored camp.
internal static class BanditCampFootprintSection
{
    internal static void Run()
    {
        // A wall piece three cells high with a foot to the right, at anchor (28, 3): blocked (28,3) (28,4) (28,5) (29,3).
        var blocked = new List<(int X, int Y)> { (28, 3), (28, 4), (28, 5), (29, 3) };
        var occupied = new List<(int X, int Y)> { (28, 3), (28, 4), (28, 5), (29, 3) };
        var hidden = new List<(int X, int Y)>();
        var mirrored = BanditCampFootprintRules.Mirror(new[] { blocked, occupied, hidden });
        Check(mirrored[0].SequenceEqual(new[] { (29, 3), (29, 4), (29, 5), (28, 3) }) && mirrored[1].SequenceEqual(mirrored[0]) && mirrored[2].Count == 0,
            "camp footprint: a wall's cells mirror inside their box, the high side moves from x 28 to 29, the foot from 29 to 28");
        var wide = BanditCampFootprintRules.Mirror(new[] { new List<(int, int)> { (10, 0), (11, 0), (12, 0) }, new List<(int, int)> { (10, 1) } });
        Check(wide[0].SequenceEqual(new[] { (12, 0), (11, 0), (10, 0) }) && wide[1].SequenceEqual(new[] { (12, 1) }),
            "camp footprint: one box over every list, so an interaction cell moves with the blocked ones");
        var symmetric = new List<(int, int)> { (5, 0), (6, 0), (5, 1), (6, 1) };
        Check(BanditCampFootprintRules.Mirror(new[] { symmetric })[0].OrderBy(c => c).SequenceEqual(symmetric.OrderBy(c => c)),
            "camp footprint: a symmetric footprint keeps its cells");
        // Only the camp's marked buildings, by GUID and on a dungeon map: another building with the same key elsewhere stays.
        var camp = Guid.NewGuid();
        var marked = new HashSet<Guid> { camp };
        Check(BanditCampFootprintRules.Applies(camp, true, false, marked) && !BanditCampFootprintRules.Applies(Guid.NewGuid(), true, false, marked)
                && !BanditCampFootprintRules.Applies(camp, false, false, marked) && !BanditCampFootprintRules.Applies(Guid.Empty, true, true, marked)
                && BanditCampFootprintRules.Applies(Guid.NewGuid(), true, true, marked) && !BanditCampFootprintRules.Applies(Guid.NewGuid(), false, true, marked)
                && !BanditCampFootprintRules.Applies(camp, true, false, null),
            "camp footprint: only a marked camp building on a dungeon map, or the one being spawned as such; the same key elsewhere, a preview without a building or another map stay unmirrored");
        var twice = BanditCampFootprintRules.Mirror(BanditCampFootprintRules.Mirror(new[] { blocked }));
        Check(twice[0].SequenceEqual(blocked) && BanditCampFootprintRules.Box(new[] { new List<(int, int)>() }) == null
                && BanditCampFootprintRules.MirrorX(28, 28, 29) == 29,
            "camp footprint: mirrored twice gives the cells back; no cells have no box");
    }
}
