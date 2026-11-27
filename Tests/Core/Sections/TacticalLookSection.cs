using DungeonSettlersDelvers.Core;
using static TestKit;

// The formation window's looks that are pure: the order of the list's rows and what a drop on a row means (TacticalFormationListOrder),
// the tooltip's title and text (TacticalFormationText), the path lines (TacticalFormationPathRules) and the summon places that go with a
// summoner taken out (TacticalFormationSummons.GoWith).
internal static class TacticalLookSection
{
    internal static void Run()
    {
        Order();
        Drop();
        Title();
        PathLines();
        RemovedSummoner();
        FrameRead();
        OnScreen();
        SummonCatalog();
        TooltipLift();
    }

    // The tooltip layers lifted above the open window go back when it closes: an added canvas at once, before Drop returns, so a
    // new opening in the same frame adds a fresh one and lifts it (final review of 0.5.2, S1); the game's own canvas keeps its sorting.
    static void TooltipLift()
    {
        var added = new object();
        var own = new object();
        var removed = new List<object>();
        var restored = new List<(object Layer, bool Override, int Order)>();
        var lift = new TacticalFormationTooltipLift<object>();
        lift.Note(added, true, false, 0);
        lift.Note(own, false, false, 10020);
        lift.Note(null, true, false, 0);
        lift.Drop(removed.Add, (layer, over, order) => restored.Add((layer, over, order)), null);
        Check(removed.Count == 1 && removed[0] == added && lift.Count == 0,
            "tactical look: closing removes the canvas the window added before it returns, so an opening in the same frame starts clean");
        Check(restored.Count == 1 && restored[0].Layer == own && !restored[0].Override && restored[0].Order == 10020,
            "tactical look: the game's own tooltip canvas gets its sorting back exactly");
        var reports = new List<string>();
        lift.Note(added, true, false, 0);
        lift.Note(own, false, true, 5);
        lift.Drop(_ => throw new InvalidOperationException("gone"), (layer, over, order) => restored.Add((layer, over, order)), reports.Add);
        Check(reports.Count == 1 && restored.Count == 2 && restored[1].Order == 5 && lift.Count == 0,
            "tactical look: one layer failing to go back leaves the others given back and is named once");
        Check(TacticalFormationTooltipLift<object>.Order(10020, 28920) == 28920 && TacticalFormationTooltipLift<object>.Order(30000, 28920) == 30000,
            "tactical look: a lifted layer sorts above the window and never lower than it was");
    }

    // Summons planned before one lives: the kinds come from the party's summoning skills, as many places per summoner as the game allows.
    static void SummonCatalog()
    {
        var kinds = TacticalFormationSummonCatalog.KindsFor(new[] { "SKILL_Fireball", "SKILL_SummonBubbleToad", "SKILL_SummonBubbling", "SKILL_SummonBubbling" }, out var unknown);
        Check(kinds.SequenceEqual(new[] { "UNIT_Bubbleling", "UNIT_BubbleToad" }) && unknown.Count == 0,
            "tactical look: a member's summoning skills offer their kinds, each once, other skills none");
        Check(TacticalFormationSummonCatalog.KindsFor(new[] { "SKILL_Fireball" }, out _).Count == 0 && TacticalFormationSummonCatalog.KindsFor(null, out _).Count == 0,
            "tactical look: a member without a summoning skill offers no summon");
        TacticalFormationSummonCatalog.KindsFor(new[] { "SKILL_SummonLadybug", "SKILL_SummonLadybug", "SKILL_SummonBubbling" }, out var strange);
        Check(strange.SequenceEqual(new[] { "SKILL_SummonLadybug" }), "tactical look: a summoning skill the catalogue lacks is named once, so a new kind shows up");
        Check(TacticalFormationSummonCatalog.CanPlanMore("UNIT_Bubbleling", 1) && !TacticalFormationSummonCatalog.CanPlanMore("UNIT_Bubbleling", 2)
                && TacticalFormationSummonCatalog.CanPlanMore("UNIT_BubbleToad", 0) && !TacticalFormationSummonCatalog.CanPlanMore("UNIT_BubbleToad", 1),
            "tactical look: a summoner plans at most two bubblelings and one toad, as the game lets one unit have");
        Check(TacticalFormationSummonCatalog.CanPlanMore("UNIT_Somebody", 9) && TacticalFormationSummonCatalog.SkillOf("UNIT_BubbleToad") == "SKILL_SummonBubbleToad"
                && TacticalFormationSummonCatalog.SkillOf("UNIT_Somebody") == null,
            "tactical look: a kind seen alive but not listed is bound by the rows alone; a listed kind knows its skill for the picture");
    }

    // The dragged window stays wholly on the screen; one larger than the screen keeps its left and top edge (the title bar) on it.
    static void OnScreen()
    {
        Check(TacticalFormationScreenRules.Shift(100f, 900f, 1280f, false) == 0f, "tactical look: a window inside the screen stays where it was dragged");
        Check(TacticalFormationScreenRules.Shift(-40f, 760f, 1280f, false) == 40f && TacticalFormationScreenRules.Shift(600f, 1400f, 1280f, false) == -120f,
            "tactical look: a window dragged over the left or right edge comes back just inside");
        Check(TacticalFormationScreenRules.Shift(-30f, 500f, 720f, true) == 30f && TacticalFormationScreenRules.Shift(300f, 800f, 720f, true) == -80f,
            "tactical look: a window dragged over the bottom or top edge comes back just inside");
        Check(TacticalFormationScreenRules.Shift(-50f, 1350f, 1280f, false) == 50f && TacticalFormationScreenRules.Shift(-100f, 800f, 720f, true) == -80f,
            "tactical look: a window larger than the screen keeps its left edge and its title bar on the screen");
    }

    // Reading the game's frame for the window's looks: any failure, also one a game update brings, gives the drawn look and one line.
    static void FrameRead()
    {
        var lines = new List<string>();
        var found = TacticalFormationFrameRead.Try(() => "frame", "portrait", lines.Add);
        Check(found == "frame" && lines.Count == 0, "tactical look: a frame read without trouble is taken and logs no failure");
        var gone = TacticalFormationFrameRead.Try<string>(() => throw new MissingFieldException("EntitySummaryUIView", "_imgPortrait"), "portrait", lines.Add);
        Check(gone == null && lines.Count == 1 && lines[0].StartsWith("CORE_TACTICAL_FRAMES_FAILED part=portrait: MissingFieldException", StringComparison.Ordinal),
            "tactical look: a field gone after a game update gives the drawn frame and names the part in one line");
        var broken = TacticalFormationFrameRead.Try<string>(() => throw new TypeInitializationException("Il2Cpp", new InvalidOperationException()), "portrait", lines.Add);
        Check(broken == null && lines.Count == 2, "tactical look: a reader that cannot start gives the drawn frame too");
        var quiet = TacticalFormationFrameRead.Try<string>(() => throw new InvalidOperationException(), "portrait", _ => throw new IOException());
        Check(quiet == null, "tactical look: a failing log line does not keep the window shut");
        Check(TacticalFormationFrameRead.Try<string>(null, "portrait", lines.Add) == null && lines.Count == 2, "tactical look: no reader means the drawn frame");
    }

    static void Order()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        var summoner = new Dictionary<Guid, Guid> { [a1] = a, [b1] = b, [b2] = b, [orphan] = stranger };
        Guid Of(Guid id) => summoner.TryGetValue(id, out var s) ? s : Guid.Empty;

        var rows = TacticalFormationListOrder.Rows(new[] { a, b, c }, new[] { b1, orphan, a1, b2 }, Of);
        Check(rows.Select(r => r.Unit).SequenceEqual(new[] { a, a1, b, b1, b2, c, orphan }),
            "tactical look: each member with its summon places right below it, a place of a stranger at the end");
        Check(rows[0].Member && rows[0].MemberIndex == 0 && !rows[0].UnderSummoner
                && !rows[1].Member && rows[1].MemberIndex == 0 && rows[1].UnderSummoner && rows[1].LastUnder
                && rows[3].UnderSummoner && !rows[3].LastUnder && rows[4].UnderSummoner && rows[4].LastUnder
                && rows[5].Member && rows[5].MemberIndex == 2,
            "tactical look: a place under its summoner is indented, the last one ends the line");
        Check(!rows[6].Member && rows[6].MemberIndex == -1 && !rows[6].UnderSummoner,
            "tactical look: a place without its summoner in the set stands alone, not indented");
        Check(rows.Count == 7 && rows.Select(r => r.Unit).Distinct().Count() == 7,
            "tactical look: every member and place shows once, so the rows still count as the grid does");

        var plain = TacticalFormationListOrder.Rows(new[] { a, b }, Array.Empty<Guid>(), Of);
        Check(plain.Select(r => r.Unit).SequenceEqual(new[] { a, b }) && plain.All(r => r.Member && !r.UnderSummoner),
            "tactical look: without summon places the list is the members in their order");
        Check(TacticalFormationListOrder.Rows(Array.Empty<Guid>(), Array.Empty<Guid>(), Of).Count == 0, "tactical look: an empty set shows no rows");
    }

    static void Drop()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        var a1 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        var members = new[] { a, b, c, d };
        var summoner = new Dictionary<Guid, Guid> { [a1] = a, [c1] = c };
        var rows = TacticalFormationListOrder.Rows(members, new[] { a1, c1, orphan }, id => summoner.TryGetValue(id, out var s) ? s : Guid.Empty);
        // Shown: a, a1, b, c, c1, d, orphan. The order after the drop, as the window applies it (TacticalFormationGrid.MoveInList).
        List<Guid> After(Guid dragged, int row)
        {
            var grid = TacticalFormationGrid.Create(members, Guid.Empty, null);
            var from = grid.Members.ToList().IndexOf(dragged);
            grid.MoveInList(dragged, TacticalFormationListOrder.DropIndex(rows, row, grid.Members.Count, from));
            return grid.Members.ToList();
        }
        Check(After(a, 1).SequenceEqual(new[] { a, b, c, d }),
            "tactical look: the leader dropped on its own summon place stays where it is and keeps the crown");
        Check(After(b, 4).SequenceEqual(new[] { a, c, b, d }),
            "tactical look: a member dragged from above onto a summon place lands right behind its summoner");
        Check(After(d, 4).SequenceEqual(new[] { a, b, c, d }) && After(d, 1).SequenceEqual(new[] { a, d, b, c }),
            "tactical look: a member dragged from below onto a summon place lands right behind its summoner, never on the crown");
        Check(After(c, 4).SequenceEqual(new[] { a, b, c, d }),
            "tactical look: the summoner dropped on its own summon place changes nothing");
        Check(After(c, 0)[0] == c && After(b, 3).SequenceEqual(new[] { a, c, b, d }),
            "tactical look: a drop on a member's row takes that member's place, the top row the crown");
        Check(After(b, 6).SequenceEqual(new[] { a, c, d, b }) && After(b, 9).SequenceEqual(new[] { a, c, d, b }),
            "tactical look: a drop on a lone place or the add row means the last member's place, as before");
        Check(TacticalFormationListOrder.DropHint(rows, 1, 4, 0) == new TacticalFormationListOrder.Hint(0, false, false)
                && TacticalFormationListOrder.DropHint(rows, 4, 4, 2) == new TacticalFormationListOrder.Hint(2, false, false),
            "tactical look: a drop that changes nothing frames nothing and gilds nothing (the leader or the summoner on its own place)");
        Check(TacticalFormationListOrder.DropHint(rows, 0, 4, 2) == new TacticalFormationListOrder.Hint(0, true, true)
                && TacticalFormationListOrder.DropHint(rows, 3, 4, 1).Changes && !TacticalFormationListOrder.DropHint(rows, 3, 4, 1).Crown,
            "tactical look: the gold title only for a drop that gives the dragged member the crown, the frame for every drop that changes");
        Check(TacticalFormationListOrder.DropHint(rows, 3, 4, 0) is { Changes: true, Crown: false }
                && TacticalFormationListOrder.DropHint(rows, -1, 4, 1).To == -1,
            "tactical look: the leader dragged away changes the order without the gold title; no row under the pointer shows nothing");        Check(TacticalFormationListOrder.DropIndex(rows, 0, 0, 0) == -1, "tactical look: without members a drop means nothing");
    }
    static void Title()
    {
        var on = TacticalFormationText.TitleWithState("Formation", "an", true);
        var off = TacticalFormationText.TitleWithState("Formation", "aus", false);
        Check(on.StartsWith("Formation  ", StringComparison.Ordinal) && on.Contains("#6FB85A") && on.EndsWith("an</color>", StringComparison.Ordinal)
                && off.Contains("#8C8590") && off.EndsWith("aus</color>", StringComparison.Ordinal),
            "tactical look: the tooltip's title names the state after the word, on green and off grey");
        var d = TacticalFormationText.Description;
        var all = new[] { d.English, d.Korean, d.French, d.German, d.Russian, d.ChineseSimplified, d.ChineseTraditional, d.Japanese, d.Spanish, d.PortugueseBrazil };
        Check(all.All(text => text.Split('\n').Length == 3 && text.Split("<color=#C98A55>").Length == 3 && text.Split("</color>").Length == 3),
            "tactical look: the tooltip's text has a line for each click, the mouse button coloured, in every language");
    }

    static void PathLines()
    {
        var lead = Guid.NewGuid();
        var follow = Guid.NewGuid();
        var off = Guid.NewGuid();
        var outside = Guid.NewGuid();
        // The set places lead (middle), follow and off; off is switched off; outside is on but has no place of the set.
        var followers = TacticalFormationPathRules.Followers(lead, new[] { follow, off, lead }, unit => unit != off);
        Check(followers.SetEquals(new[] { follow }),
            "tactical look: the followers are the set's placed members with the formation on, not the middle, not one without a place");
        TacticalFormationPathRules.Line Of(Guid unit, bool enabled = true, bool dungeon = true, Guid? leader = null, IReadOnlyCollection<Guid> set = null)
            => TacticalFormationPathRules.LineOf(enabled, dungeon, unit, leader ?? lead, set ?? followers);
        Check(Of(lead) == TacticalFormationPathRules.Line.Leader && Of(follow) == TacticalFormationPathRules.Line.Follower,
            "tactical look: with the formation on the leader's line is the strong one, a follower's the faint dashed one");
        Check(Of(off) == TacticalFormationPathRules.Line.Game && Of(outside) == TacticalFormationPathRules.Line.Game,
            "tactical look: a member switched off and a member on without a place of the set keep the game's line");
        Check(Of(lead, enabled: false) == TacticalFormationPathRules.Line.Game && Of(follow, dungeon: false) == TacticalFormationPathRules.Line.Game,
            "tactical look: without the comfort option or outside the dungeon every line is the game's");
        Check(Of(follow, leader: Guid.Empty) == TacticalFormationPathRules.Line.Game,
            "tactical look: without a middle every line is the game's");
        Check(Of(lead, set: new HashSet<Guid>()) == TacticalFormationPathRules.Line.Game,
            "tactical look: a middle nobody follows keeps the game's line");
        var offMiddle = TacticalFormationPathRules.Followers(off, new[] { follow, lead }, unit => unit != off);
        Check(TacticalFormationPathRules.LineOf(true, true, off, off, offMiddle) == TacticalFormationPathRules.Line.Leader
                && TacticalFormationPathRules.LineOf(true, true, follow, off, offMiddle) == TacticalFormationPathRules.Line.Follower,
            "tactical look: a middle switched off stays the middle, its line strong while others follow it");
        // As the path look reads them (TacticalFormationPathRules.Read): a member there with its formation on but without a place of
        // the set follows nobody.
        var set = new TacticalFormationBook.Set(lead, new[] { new TacticalFormationRules.Slot(follow, 0, -1), new TacticalFormationRules.Slot(off, 1, -1) });
        var (middle, read) = TacticalFormationPathRules.Read(set, new[] { lead, follow, off, outside }, unit => unit != off);
        Check(middle == lead && read.SetEquals(new[] { follow }),
            "tactical look: read from the applied set, a member there without a place of the set is no follower");
        var (none, nobody) = TacticalFormationPathRules.Read(null, new[] { lead }, unit => true);
        Check(none == Guid.Empty && nobody.Count == 0, "tactical look: without an active set nobody leads or follows");        Check(TacticalFormationPathRules.LeaderWidth > 1f && TacticalFormationPathRules.FollowerAlpha is > 0f and < 1f,
            "tactical look: the leader's line wider, a follower's fainter");
    }
    static void RemovedSummoner()
    {
        var mage = Guid.NewGuid();
        var other = Guid.NewGuid();
        var places = new[]
        {
            new TacticalFormationSummons.Place(mage, "UNIT_Bubbleling", 1, 0, 0),
            new TacticalFormationSummons.Place(mage, "UNIT_Bubbleling", 2, 0, 0),
            new TacticalFormationSummons.Place(other, "UNIT_Bubbleling", 1, 0, 0)
        };
        var gone = TacticalFormationSummons.GoWith(places, mage, true);
        Check(gone.Count == 2 && gone.Contains(places[0].Id) && gone.Contains(places[1].Id) && !gone.Contains(places[2].Id),
            "tactical look: a summoner taken out takes its own summon places along, another's stay");
        Check(TacticalFormationSummons.GoWith(places, mage, false).Count == 3 && TacticalFormationSummons.GoWith(places, Guid.NewGuid(), true).Count == 0,
            "tactical look: without members left every place goes, a stranger takes none");
    }
}