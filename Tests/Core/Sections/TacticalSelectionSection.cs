using DungeonSettlersDelvers.Core;
using static TestKit;
using Place = DungeonSettlersDelvers.Core.TacticalFormationSummons.Place;
using Slot = DungeonSettlersDelvers.Core.TacticalFormationRules.Slot;

// The formation window's fixed selection: each set holds its own rows, at most eight, members and summon
// places; "+" adds, a right click takes a row out; summon places keep their summoner, kind and number.
internal static class TacticalSelectionSection
{
    internal static void Run()
    {
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var mage = Guid.NewGuid();

        var first = new Place(mage, "UNIT_Bubbleling", 1, 0, -2);
        var second = new Place(mage, "UNIT_Bubbleling", 2, 1, -2);
        Check(first.Id == new Place(mage, "UNIT_Bubbleling", 1, 3, 3).Id && first.Id != second.Id && first.Id != new Place(a, "UNIT_Bubbleling", 1, 0, -2).Id,
            "selection: a summon place's id is its summoner, kind and number, not its cell");
        Check(TacticalFormationSummons.NextNumber(new[] { first }, mage, "UNIT_Bubbleling") == 2
                && TacticalFormationSummons.NextNumber(new[] { second }, mage, "UNIT_Bubbleling") == 1
                && TacticalFormationSummons.NextNumber(new[] { first }, mage, "UNIT_BubbleToad") == 1,
            "selection: a new place takes the lowest number its summoner and kind leave free");
        var numbers = TacticalFormationSummons.Number(new Dictionary<Guid, int> { [b] = 2 }, new[] { a, b });
        Check(numbers[b] == 2 && numbers[a] == 1, "selection: a living summon keeps its number, a new one takes the lowest free");

        var many = new List<Slot>();
        for (var i = 0; i < 12; i++) many.Add(new Slot(Guid.NewGuid(), i % 4 - 2, -1 - i / 4));
        var big = new TacticalFormationBook.Set(leader, many, new[] { first });
        Check(big.Count == 14 && big.Places.Count == 12 && big.Summons.Count == 1,
            "selection: no cap of eight rows (Fre-yin, 02.11.2026: packs allow bigger groups)");
        var crowd = new List<Slot>();
        for (var i = 0; i < 100; i++) crowd.Add(new Slot(Guid.NewGuid(), 0, 0));
        Check(new TacticalFormationBook.Set(leader, crowd).Count == TacticalFormationBook.Rows && TacticalFormationBook.Rows == 81,
            "selection: the grid is the bound, one cell each, 81 rows with the crown");

        var set = new TacticalFormationBook.Set(mage, new[] { new Slot(a, 1, 0) }, new[] { first, second });
        var bubble = Guid.NewGuid();
        Check(TacticalFormationBook.Apply(set, new[] { mage, a }, out var crown, out var slots, new Dictionary<Guid, Guid> { [first.Id] = bubble })
                && crown == mage && slots.Count == 2 && slots[1].Unit == bubble && slots[1].X == 0 && slots[1].Y == -2,
            "selection: a living summon stands on its place, a place without its summon stays empty");

        var data = CoreSaveData.Create(Guid.NewGuid());
        var book = new TacticalFormationBook();
        book.Sets[2] = set;
        data.SetFormationBook(book);
        var read = data.FormationBook().Sets[2];
        Check(read != null && read.Summons.Count == 2 && read.Summons[1].Id == second.Id && read.Summons[1].X == 1 && read.Summons[1].Y == -2,
            "selection save: the summon places come back from the save");
        Check(data.DropFormationUnits(unit => unit != mage) > 0 && data.FormationBook().Sets[2] is { } kept && kept.Leader == a && kept.Summons.Count == 0,
            "selection save: a summoner who is gone takes its summon places along, the next member wears the crown");

        Grid();
        Roles();
    }

    // The roles: each formation keeps its own, every member starts neutral.
    static void Roles()
    {
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        Check(TacticalRoles.Parse("tank") == TacticalRole.Tank && TacticalRoles.Parse("later") == TacticalRole.Neutral
                && TacticalRoles.Word(TacticalRole.Support) == "support" && TacticalRoles.Word(TacticalRole.Neutral) == null,
            "roles: a role is a word in the save, an unknown word reads as neutral, neutral is never written");
        var roles = new Dictionary<Guid, TacticalRole> { [leader] = TacticalRole.Tank, [a] = TacticalRole.Damage, [stranger] = TacticalRole.Support };
        var first = new TacticalFormationBook.Set(leader, new[] { new Slot(a, 1, 0) }, null, roles);
        var second = new TacticalFormationBook.Set(leader, new[] { new Slot(a, 1, 0) }, null, new Dictionary<Guid, TacticalRole> { [a] = TacticalRole.Support });
        Check(first.RoleOf(leader) == TacticalRole.Tank && first.RoleOf(a) == TacticalRole.Damage && first.RoleOf(stranger) == TacticalRole.Neutral
                && second.RoleOf(a) == TacticalRole.Support && second.RoleOf(leader) == TacticalRole.Neutral,
            "roles: each formation keeps its own roles, only for its members, neutral where none was given");
        var data = CoreSaveData.Create(Guid.NewGuid());
        var book = new TacticalFormationBook();
        book.Sets[0] = first;
        book.Sets[1] = second;
        data.SetFormationBook(book);
        var read = data.FormationBook();
        Check(read.Sets[0].RoleOf(a) == TacticalRole.Damage && read.Sets[1].RoleOf(a) == TacticalRole.Support && read.Sets[1].Roles.Count == 1
                && data.Serialize().Contains("\"roles\""),
            "roles save: the roles come back per formation");
        data.DropFormationUnits(unit => unit != leader);
        Check(data.FormationBook().Sets[0] is { } kept && kept.Leader == a && kept.RoleOf(a) == TacticalRole.Damage && kept.Roles.Count == 1,
            "roles save: a member who is gone takes its role along, the others keep theirs");

        var grid = TacticalFormationGrid.Create(new[] { leader, a }, leader, new[] { new Slot(a, 1, 0) }, null, null, roles);
        Check(grid.RoleOf(leader) == TacticalRole.Tank && grid.SetRole(a, TacticalRole.Support) && !grid.SetRole(a, TacticalRole.Support)
                && !grid.SetRole(stranger, TacticalRole.Tank) && grid.SetRole(leader, TacticalRole.Neutral) && grid.AllRoles().Count == 1,
            "roles grid: a member's role changes, neutral clears it, a stranger has none");
        Check(grid.Remove(a) && grid.AllRoles().Count == 0 && grid.Add(a) && grid.RoleOf(a) == TacticalRole.Neutral,
            "roles grid: a member taken out and added again starts neutral");
    }

    static void Grid()
    {
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var place = Guid.NewGuid();
        var grid = TacticalFormationGrid.Create(new[] { leader }, leader, null);
        Check(grid.Add(a) && !grid.Add(a) && grid.Count == 2 && grid.CellOf(a) != null, "selection grid: a member is added once, on a free place");
        Check(grid.AddSummon(place) && grid.Summons.Count == 1 && grid.SummonSlots().Count == 1 && grid.Slots().Count == 1,
            "selection grid: a summon place stands in the grid below the members");
        for (var i = 0; i < 100; i++) grid.Add(Guid.NewGuid());
        var cells = new HashSet<(int, int)?>();
        foreach (var unit in grid.Members) cells.Add(grid.CellOf(unit));
        foreach (var unit in grid.Summons) cells.Add(grid.CellOf(unit));
        Check(grid.Count == TacticalFormationGrid.MaxRows && cells.Count == grid.Count && !cells.Contains(null),
            "selection grid: more than eight rows, each on its own cell, up to the 81 cells of the grid");
        Check(grid.Remove(leader) && grid.Leader == a && grid.CellOf(a) == (TacticalFormationGrid.Center, TacticalFormationGrid.Center),
            "selection grid: taking the crown's row out hands the crown to the next member in the middle");
        Check(grid.Remove(place) && grid.Summons.Count == 0 && !grid.Remove(place), "selection grid: a summon place goes with a right click");
        var empty = TacticalFormationGrid.Create(Array.Empty<Guid>(), Guid.Empty, null);
        Check(!empty.AddSummon(place) && empty.Add(a) && empty.Leader == a && empty.CellOf(a) == (TacticalFormationGrid.Center, TacticalFormationGrid.Center),
            "selection grid: an empty set takes no summon first, its first member wears the crown");
    }
}
