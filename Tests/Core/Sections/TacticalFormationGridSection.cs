using System.Text.Json.Nodes;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The formation window's grid (TacticalFormationGrid) and the formations in Core's block of the save (CoreSaveData).
internal static class TacticalFormationGridSection
{
    internal static void Run()
    {
        Grid();
        List();
        Casts();
        Book();
        CastsKept();
        Saved();
        Texts();
    }

    static void Grid()
    {
        var group = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var middle = (TacticalFormationGrid.Center, TacticalFormationGrid.Center);
        var grid = TacticalFormationGrid.Create(group, Guid.Empty, null);
        Check(TacticalFormationGrid.Size == 9 && middle == (4, 4) && grid.Leader == group[0] && grid.CellOf(group[0]) == middle,
            "tactical grid: 9 by 9, without a saved leader the first member wears the crown in the middle cell");
        var slots = grid.Slots();
        Check(slots.Count == 3 && slots[0].X == 0 && slots[0].Y == -1 && slots[1].X == -1 && slots[2].X == 1,
            "tactical grid: the others stand on the default places behind the leader");

        var from = grid.CellOf(group[1]).Value;
        var to = grid.CellOf(group[2]).Value;
        Check(grid.Drop(from.Column, from.Row, to.Column, to.Row) && grid.CellOf(group[1]) == to && grid.CellOf(group[2]) == from,
            "tactical grid: a drop on an occupied cell swaps the two");
        Check(grid.Drop(to.Column, to.Row, 8, 8) && grid.CellOf(group[1]) == (8, 8) && grid.At(to.Column, to.Row) == Guid.Empty,
            "tactical grid: a drop on a free cell moves the member");
        Check(!grid.Drop(0, 8, 1, 8) && !grid.Drop(8, 8, 9, 8) && !grid.Drop(8, 8, 8, 8), "tactical grid: an empty cell, a cell outside or the same cell moves nothing");
        Check(!grid.Drop(4, 4, 0, 0) && !grid.Drop(8, 8, 4, 4) && grid.CellOf(group[0]) == middle && grid.CellOf(group[1]) == (8, 8),
            "tactical grid: the crown stays in the middle, nobody is dropped on it");

        var crownedAt = grid.CellOf(group[3]).Value;
        Check(grid.SetCrown(group[3]) && grid.Leader == group[3] && grid.CellOf(group[3]) == middle && grid.CellOf(group[0]) == crownedAt
                && !grid.SetCrown(Guid.NewGuid()),
            "tactical grid: a new crown takes the middle cell, the former leader its cell; a stranger gets none");
        var crowned = grid.Slots();
        Check(crowned.Count == 3 && !crowned.Exists(slot => slot.Unit == group[3]) && crowned.Exists(slot => slot.Unit == group[0]),
            "tactical grid: the places are counted from the new leader");

        grid.Reset();
        Check(grid.CellOf(group[3]) == middle && grid.Slots().Count == 3 && grid.Slots()[0].Y == -1,
            "tactical grid: reset leaves the crown in the middle and the group behind it");

        var saved = new List<TacticalFormationRules.Slot> { new(group[1], 2, 3), new(group[2], -4, -4), new(Guid.NewGuid(), 1, 1) };
        var restored = TacticalFormationGrid.Create(group, group[0], saved);
        var back = restored.Slots();
        Check(back.Exists(slot => slot.Unit == group[1] && slot.X == 2 && slot.Y == 3)
                && back.Exists(slot => slot.Unit == group[2] && slot.X == -4 && slot.Y == -4) && back.Count == 3
                && restored.CellOf(group[0]) == middle,
            "tactical grid: saved places come back around the middle, a member who left is ignored, a new member gets a default place");

        // An older 8 by 8 formation: places up to seven cells away are drawn to the edge, two on one cell part.
        var wide = TacticalFormationGrid.Create(group, group[0], new List<TacticalFormationRules.Slot>
            { new(group[1], -7, 0), new(group[2], 7, 6), new(group[3], -6, 0) });
        var drawn = wide.Slots();
        Check(drawn.Count == 3 && drawn.Exists(slot => slot.Unit == group[1] && slot.X == -4 && slot.Y == 0)
                && drawn.Exists(slot => slot.Unit == group[2] && slot.X == 4 && slot.Y == 4)
                && drawn.Exists(slot => slot.Unit == group[3] && Math.Abs(slot.X + 4) <= 1 && Math.Abs(slot.Y) <= 1 && !(slot.X == -4 && slot.Y == 0)),
            "tactical grid: an older formation's far places are drawn to the edge, a second one on the same cell takes the nearest free cell");
    }

    // The group list: the top member wears the crown, at most eight, the order comes back from the save.
    static void List()
    {
        var group = new List<Guid>();
        for (var i = 0; i < 10; i++) group.Add(Guid.NewGuid());
        var grid = TacticalFormationGrid.Create(group, Guid.Empty, null);
        Check(grid.Members.Count == TacticalFormationGrid.MaxMembers && grid.Left == 2 && grid.Leader == group[0],
            "tactical list: at most eight members, the rest is counted");
        var cell = grid.CellOf(group[3]);
        Check(grid.MoveInList(group[3], 0) && grid.Leader == group[3] && grid.Members[1] == group[0]
                && grid.CellOf(group[3]) == (TacticalFormationGrid.Center, TacticalFormationGrid.Center) && grid.CellOf(group[0]) == cell,
            "tactical list: a member dragged to the top wears the crown in the middle, the former leader takes the cell");
        Check(grid.MoveInList(group[3], 2) && grid.Leader == group[0] && grid.Members[2] == group[3],
            "tactical list: dragged down again, the next member on top wears the crown");
        Check(!grid.MoveInList(group[9], 0) && !grid.MoveInList(group[0], 8) && !grid.MoveInList(group[0], 0),
            "tactical list: a member outside the list, a row past the end or the same row moves nothing");

        var small = group.GetRange(0, 4);
        var saved = new List<TacticalFormationRules.Slot> { new(small[3], 1, 0), new(small[1], -1, 0) };
        var restored = TacticalFormationGrid.Create(small, small[2], saved);
        Check(restored.Members[0] == small[2] && restored.Members[1] == small[3] && restored.Members[2] == small[1] && restored.Members[3] == small[0],
            "tactical list: the saved leader on top, then the saved places in their order, then the rest");
        Check(restored.Slots()[0].Unit == small[3] && restored.Slots()[1].Unit == small[1],
            "tactical list: the places are saved in the list's order");
    }

    static void Casts()
    {
        var group = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var grid = TacticalFormationGrid.Create(group, Guid.Empty, null);
        Check(grid.Casts(group[0]).Count == TacticalFormationGrid.CastSlots && grid.Casts(group[0])[0] == "" && grid.AllCasts().Count == 0,
            "tactical casts: five empty places at first");
        Check(grid.SetCast(group[0], 0, "Fireball") && grid.SetCast(group[0], 2, "Heal") && grid.Casts(group[0])[0] == "Fireball"
                && grid.Casts(group[0])[2] == "Heal",
            "tactical casts: a skill goes on a place");
        Check(grid.SetCast(group[0], 1, "Fireball") && grid.Casts(group[0])[0] == "" && grid.Casts(group[0])[1] == "Fireball",
            "tactical casts: the same skill on another place leaves the first one");
        Check(grid.SetCast(group[0], 2, null) && grid.Casts(group[0])[2] == "" && !grid.SetCast(group[0], TacticalFormationGrid.CastSlots, "Heal")
                && !grid.SetCast(Guid.NewGuid(), 0, "Heal"),
            "tactical casts: a place can be emptied; a sixth place or a stranger gets none");
        Check(grid.AllCasts().Count == 1 && grid.AllCasts()[group[0]][1] == "Fireball", "tactical casts: only members with a cast are saved");

        var reloaded = TacticalFormationGrid.Create(group, Guid.Empty, null,
            new Dictionary<Guid, string[]> { [group[1]] = new[] { "Taunt", "", "Shield" }, [Guid.NewGuid()] = new[] { "X", "", "" } });
        Check(reloaded.Casts(group[1])[0] == "Taunt" && reloaded.Casts(group[1])[2] == "Shield" && reloaded.Casts(group[1])[4] == ""
                && reloaded.AllCasts().Count == 1,
            "tactical casts: saved casts come back for the group's members only");
    }

    // The formation book (Core): eight sets per campaign, the active one counts.
    static void Book()
    {
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var set = new TacticalFormationBook.Set(leader, new[] { new TacticalFormationRules.Slot(a, 1, 0), new TacticalFormationRules.Slot(b, 0, 1),
            new TacticalFormationRules.Slot(a, 2, 2), new TacticalFormationRules.Slot(leader, 1, 1) });
        Check(set.Places.Count == 2, "tactical book: a set holds each member once and not its leader");

        Check(TacticalFormationBook.Apply(set, new[] { a, leader, c }, out var crown, out var slots) && crown == leader
                && slots.Count == 2 && slots[0].Unit == a && slots[0].X == 1 && slots[1].Unit == c && slots[1].X == 0 && slots[1].Y == -1,
            "tactical book: a member who is missing loses the place, a new member takes the first free default place");
        var crowded = new TacticalFormationBook.Set(leader, new[] { new TacticalFormationRules.Slot(a, 0, -1) });
        Check(TacticalFormationBook.Apply(crowded, new[] { leader, a, c }, out _, out var shifted) && shifted[1].Unit == c && shifted[1].X == -1 && shifted[1].Y == -1,
            "tactical book: a default place already taken is passed over");
        Check(!TacticalFormationBook.Apply(set, new[] { a, b }, out _, out _) && !TacticalFormationBook.Apply(null, new[] { a }, out _, out _),
            "tactical book: without its leader or without a set nothing applies");

        var merged = TacticalFormationBook.Merge(set, leader, new[] { new TacticalFormationRules.Slot(a, -1, -1) }, new[] { leader, a });
        Check(merged.Places.Count == 2 && merged.Places[0].Unit == a && merged.Places[0].X == -1 && merged.Places[1].Unit == b,
            "tactical book: a window that showed part of the group keeps the others' places");
        var clash = TacticalFormationBook.Merge(set, leader, new[] { new TacticalFormationRules.Slot(a, 0, 1) }, new[] { leader, a });
        Check(clash.Places.Count == 1 && clash.Places[0].Unit == a, "tactical book: a kept place whose cell is taken now goes");

        var book = new TacticalFormationBook { Active = 12 };
        Check(book.Active == TacticalFormationBook.Size - 1 && book.IsEmpty, "tactical book: eight sets, the active index stays within them");
        book.Sets[0] = set;
        book.Sets[3] = new TacticalFormationBook.Set(c, new[] { new TacticalFormationRules.Slot(a, 1, 1) });
        Check(book.Drop(unit => unit != c && unit != b) == 3 && book.Sets[3] == null && book.Sets[0].Places.Count == 1,
            "tactical book: members who are gone leave the sets, a set without its leader becomes empty");
    }

    // Casts he had set were gone after use. The window's way with the real classes, step by step: open,
    // set, close, open again, turn a page and back, save and load; the casts must stay at every step.
    static void CastsKept()
    {
        var campaign = Guid.NewGuid();
        var group = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var data = CoreSaveData.Create(campaign);
        var steps = new List<string>();

        TacticalFormationGrid Open(int page)
        {
            var book = data.FormationBook();
            return TacticalFormationBook.Apply(book.Sets[page], group, out var leader, out var slots)
                ? TacticalFormationGrid.Create(group, leader, slots, data.PriorityCasts())
                : TacticalFormationGrid.Create(group, Guid.Empty, null, data.PriorityCasts());
        }
        void Close(TacticalFormationGrid grid, int page)
        {
            var book = data.FormationBook();
            book.Sets[page] = TacticalFormationBook.Merge(book.Sets[page], grid.Leader, grid.Slots(), grid.Members);
            book.Active = page;
            data.SetFormationBook(book);
            data.SetPriorityCasts(grid.Members, grid.AllCasts());
            data.DropFormationUnits(group.Contains);
        }
        bool Kept(string step)
        {
            var casts = data.PriorityCasts();
            var ok = casts.TryGetValue(group[1], out var keys) && keys[0] == "Heal" && keys[1] == "Shield"
                && casts.TryGetValue(group[2], out var other) && other[0] == "Taunt";
            if (!ok) steps.Add(step);
            return ok;
        }

        var first = Open(0);
        first.SetCast(group[1], 0, "Heal");
        first.SetCast(group[1], 1, "Shield");
        first.SetCast(group[2], 0, "Taunt");
        Close(first, 0);
        Kept("first close");
        Close(Open(0), 0);
        Kept("reopened and closed without a change");
        var again = Open(0);
        again.SetCast(group[0], 0, "Fireball");
        Close(again, 0);
        Kept("another member changed");
        var turned = Open(0);
        var other = TacticalFormationGrid.Create(group, Guid.Empty, null, turned.AllCasts());
        Close(other, 1);
        Kept("turned to the second set");
        Close(Open(0), 0);
        Kept("back on the first set");
        data = CoreSaveData.Parse(data.Serialize(), campaign);
        Kept("saved and loaded");
        Close(Open(0), 0);
        Kept("opened and closed after loading");
        Check(steps.Count == 0, "tactical casts: set casts stay through closing, opening, turning pages, saving and loading (lost at: "
            + string.Join(", ", steps) + ")");
    }

    static void Saved()
    {
        var campaign = Guid.NewGuid();
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var data = CoreSaveData.Create(campaign);
        Check(data.FormationBook().IsEmpty && !data.Serialize().Contains("formations"), "tactical save: a new block has no formations");

        var book = new TacticalFormationBook { Active = 2 };
        book.Sets[0] = new TacticalFormationBook.Set(leader, new[] { new TacticalFormationRules.Slot(a, -1, -1), new TacticalFormationRules.Slot(b, 2, 0) });
        book.Sets[2] = new TacticalFormationBook.Set(a, new[] { new TacticalFormationRules.Slot(leader, 0, 1) });
        data.SetFormationBook(book);
        var read = CoreSaveData.Parse(data.Serialize(), campaign).FormationBook();
        Check(read.Active == 2 && read.Sets[0].Leader == leader && read.Sets[0].Places.Count == 2 && read.Sets[0].Places[0].Unit == a
                && read.Sets[0].Places[1].X == 2 && read.Sets[1] == null && read.Sets[2].Leader == a && read.Sets[2].Places[0].Y == 1,
            "tactical save: the book goes into the block with its eight sets and the active one and comes back");
        var json = JsonNode.Parse(data.Serialize())!.AsObject();
        Check(json["formations"]?["active"]?.GetValue<int>() == 2 && json["formations"]?["sets"]?.AsArray().Count == TacticalFormationBook.Size
                && json["formations"]?["sets"]?[0]?["places"]?[0]?["unit"]?.GetValue<string>() == a.ToString("D")
                && json["formations"]?["sets"]?[1]?.AsObject().Count == 0,
            "tactical save: formations hold active and eight sets, an empty set is {}, a place is unit, x and y");

        Check(data.DropFormationUnits(unit => unit != a) == 3 && data.FormationBook().Sets[2] == null
                && data.FormationBook().Sets[0].Places.Count == 1 && data.FormationBook().Sets[0].Places[0].Unit == b,
            "tactical save: members and leaders who are gone leave the book");
        Check(data.DropFormationUnits(unit => false) == 2 && !data.Serialize().Contains("formations"), "tactical save: an empty book leaves the block");

        // The first format: { "<leader>": [ places ] } loads as the first set, active, and is written in the new one.
        var old = "{\"version\":1,\"campaign\":\"" + campaign.ToString("D") + "\",\"formations\":{\"" + leader.ToString("D")
            + "\":[{\"unit\":\"" + a.ToString("D") + "\",\"x\":-1,\"y\":-1},{\"unit\":\"" + b.ToString("D") + "\",\"x\":1,\"y\":-1}]}}";
        var first = CoreSaveData.Parse(old, campaign);
        var converted = first.FormationBook();
        Check(converted.Active == 0 && converted.Sets[0].Leader == leader && converted.Sets[0].Places.Count == 2 && converted.Sets[1] == null,
            "tactical save: a formation in the first format loads as set 1, active");
        first.SetFormationBook(converted);
        var again = CoreSaveData.Parse(first.Serialize(), campaign).FormationBook();
        Check(first.Serialize().Contains("\"sets\"") && again.Active == 0 && again.Sets[0].Leader == leader && again.Sets[0].Places.Count == 2
                && again.Sets[0].Places[0].Unit == a && again.Sets[0].Places[0].X == -1 && again.Sets[0].Places[1].Unit == b && again.Sets[0].Places[1].X == 1,
            "tactical save: read in the first format, written in the new one and read again, the formation is the same");

        // The switch on the button: only the members switched off are kept, on is the default.
        var switched = CoreSaveData.Create(campaign);
        Check(!switched.IsFormationOff(a) && !switched.Serialize().Contains("formationOff"), "tactical save: every member has the formation on at first");
        switched.SetFormationOff(a, true);
        switched.SetFormationOff(b, true);
        switched.SetFormationOff(b, false);
        var reread = CoreSaveData.Parse(switched.Serialize(), campaign);
        Check(reread.IsFormationOff(a) && !reread.IsFormationOff(b), "tactical save: a member switched off stays off after a save, one switched on again is not kept");
        Check(switched.DropFormationUnits(unit => unit != a) == 1 && !switched.IsFormationOff(a) && !switched.Serialize().Contains("formationOff"),
            "tactical save: a member who is gone leaves the switched off list");

        var castData = CoreSaveData.Create(campaign);
        castData.SetPriorityCasts(new[] { a, b }, new Dictionary<Guid, string[]> { [a] = new[] { "Fireball", "", "Heal" } });
        var casts = CoreSaveData.Parse(castData.Serialize(), campaign).PriorityCasts();
        Check(casts.Count == 1 && casts[a][0] == "Fireball" && casts[a][1] == "" && casts[a][2] == "Heal",
            "tactical save: a member's casts go into the block by place and come back");
        var three = "{\"version\":1,\"campaign\":\"" + campaign.ToString("D") + "\",\"casts\":{\"" + a.ToString("D") + "\":[\"Fireball\",\"\",\"Heal\"]}}";
        var older = CoreSaveData.Parse(three, campaign);
        var padded = older.PriorityCasts()[a];
        older.SetPriorityCasts(new[] { a }, older.PriorityCasts());
        Check(padded.Length == TacticalFormationGrid.CastSlots && padded[0] == "Fireball" && padded[2] == "Heal" && padded[3] == "" && padded[4] == ""
                && older.Serialize().Contains("[\"Fireball\",\"\",\"Heal\",\"\",\"\"]"),
            "tactical save: an entry of three places is read and written with five, the new ones empty");
        castData.SetPriorityCasts(new[] { a }, new Dictionary<Guid, string[]>());
        Check(castData.PriorityCasts().Count == 0 && !castData.Serialize().Contains("casts"), "tactical save: a member without casts leaves the block");
        castData.SetPriorityCasts(new[] { a, b }, new Dictionary<Guid, string[]> { [a] = new[] { "Heal" }, [b] = new[] { "Taunt", "", "" } });
        Check(castData.DropFormationUnits(unit => unit == b) == 1 && castData.PriorityCasts().ContainsKey(b) && !castData.PriorityCasts().ContainsKey(a),
            "tactical save: the casts of a member who is gone leave the block");

        var damaged = "{\"version\":1,\"campaign\":\"" + campaign.ToString("D") + "\",\"formations\":{\"active\":1,\"sets\":[{},{\"leader\":\""
            + leader.ToString("D") + "\",\"places\":[{\"unit\":\"bad\",\"x\":1,\"y\":1},{\"unit\":\"" + a.ToString("D")
            + "\",\"x\":40,\"y\":\"no\"},{\"unit\":\"" + b.ToString("D") + "\",\"x\":40,\"y\":-1}]},{\"leader\":\"bad\"}]}}";
        var broken = CoreSaveData.Parse(damaged, campaign).FormationBook();
        Check(broken.Active == 1 && broken.Sets[0] == null && broken.Sets[2] == null && broken.Sets[1].Places.Count == 1
                && broken.Sets[1].Places[0].X == TacticalFormationRules.MaxOffset,
            "tactical save: broken entries are skipped and a place too far is drawn to the edge, four cells");
    }

    static void Texts()
    {
        var problems = CoreTextRules.Problems(TacticalFormationText.All).Concat(CoreTextRules.HyphenProblems(TacticalFormationText.All)).ToList();
        Check(problems.Count == 0, "tactical texts: ten languages, no dash, no hyphen (" + string.Join("; ", problems) + ")");
        var registered = 0;
        foreach (var (key, _) in CoreTextRows.Registered)
            if (key == TacticalFormationText.NameKey || key == TacticalFormationText.DescKey) registered++;
        Check(registered == 2 && TacticalFormationText.NameKey == "TEXTKEY_SummaryOptionUI_DelversFormation_NAME",
            "tactical texts: the button's name and text are rows of the game's text table under the option row's tooltip key");
    }
}
