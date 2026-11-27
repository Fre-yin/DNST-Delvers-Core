namespace DungeonSettlersDelvers.Core;

// The campaign's formation book (up to eight formations to keep; Core, one book per campaign,
// since only one expedition is in the dungeon at a time and the game knows no key of an expedition). Eight sets, each a fixed
// selection: its leader, the places of its members in the order of the window's list and the places of
// its summons (TacticalFormationSummons), together at most Rows rows (one cell of the grid each), whoever is selected or on the map. The active one is the
// one the dungeon follows. An empty set is null. The priority casts stay per member (CoreSaveData.PriorityCasts). Pure, so the
// offline tests cover it.
internal sealed class TacticalFormationBook
{
    internal const int Size = 8;
    // The rows of a set: the leader, the members and the summon places; no cap of eight, the grid's cells are
    // the bound.
    internal const int Rows = TacticalFormationGrid.MaxRows;

    internal sealed class Set
    {
        internal Set(Guid leader, IEnumerable<TacticalFormationRules.Slot> places, IEnumerable<TacticalFormationSummons.Place> summons = null,
            IReadOnlyDictionary<Guid, TacticalRole> roles = null)
        {
            Leader = leader;
            foreach (var place in places ?? Array.Empty<TacticalFormationRules.Slot>())
                if (Count < Rows && place.Unit != Guid.Empty && place.Unit != leader && !Places.Exists(known => known.Unit == place.Unit)) Places.Add(place);
            foreach (var summon in summons ?? Array.Empty<TacticalFormationSummons.Place>())
                if (Count < Rows && summon.Summoner != Guid.Empty && summon.Kind.Length > 0 && !Summons.Exists(known => known.Id == summon.Id)) Summons.Add(summon);
            foreach (var pair in roles ?? new Dictionary<Guid, TacticalRole>())
                if (pair.Value != TacticalRole.Neutral && (pair.Key == leader || Places.Exists(place => place.Unit == pair.Key))) Roles[pair.Key] = pair.Value;
        }

        // The members' roles in this formation (each formation keeps its own), Neutral left out.
        internal Dictionary<Guid, TacticalRole> Roles { get; } = new();

        internal TacticalRole RoleOf(Guid unit) => Roles.TryGetValue(unit, out var role) ? role : TacticalRole.Neutral;

        internal Guid Leader { get; }
        internal List<TacticalFormationRules.Slot> Places { get; } = new();
        internal List<TacticalFormationSummons.Place> Summons { get; } = new();

        // The set's rows: the leader, its members, its summon places.
        internal int Count => 1 + Places.Count + Summons.Count;

        // The leader, then the members, in the list's order.
        internal List<Guid> Members()
        {
            var members = new List<Guid> { Leader };
            foreach (var place in Places) members.Add(place.Unit);
            return members;
        }
    }

    private int active;

    internal Set[] Sets { get; } = new Set[Size];

    internal int Active
    {
        get => active;
        set => active = Math.Clamp(value, 0, Size - 1);
    }

    internal bool IsEmpty => Array.TrueForAll(Sets, set => set == null);

    // The active set applied to the members there now (present): only the set's own rows count, a member there now that the set
    // does not hold walks without the formation. The middle is the leader, or when it is away the top member of the list that is
    // there; the places keep their cells relative to the leader's, so they are shifted onto the new middle.
    // A member who is away keeps its place reserved and empty. summons: the living summons there now under their place's id
    // (TacticalFormationSummons.Place.Id) and their own GUID; a place of the set takes its summon, a summon without a place of the
    // set walks without the formation. False without a set or without any of its members there.
    internal static bool Apply(Set set, IReadOnlyCollection<Guid> present, out Guid leader, out List<TacticalFormationRules.Slot> slots,
        IReadOnlyDictionary<Guid, Guid> summons = null)
    {
        leader = Guid.Empty;
        slots = new List<TacticalFormationRules.Slot>();
        if (set == null || present == null) return false;
        var middle = (X: 0, Y: 0);
        if (Contains(present, set.Leader)) leader = set.Leader;
        else
            foreach (var place in set.Places)
                if (Contains(present, place.Unit))
                {
                    leader = place.Unit;
                    middle = (place.X, place.Y);
                    break;
                }
        if (leader == Guid.Empty) return false;
        foreach (var place in set.Places)
            if (place.Unit != leader && Contains(present, place.Unit))
                slots.Add(new TacticalFormationRules.Slot(place.Unit, place.X - middle.X, place.Y - middle.Y));
        if (summons != null)
            foreach (var summon in set.Summons)
                if (summons.TryGetValue(summon.Id, out var unit))
                    slots.Add(new TacticalFormationRules.Slot(unit, summon.X - middle.X, summon.Y - middle.Y));
        return true;
    }

    // Members who are gone leave every set, so do the summon places of a summoner who is gone; a set whose leader is gone keeps its
    // next member as the leader, the places shifted onto it, and becomes empty without one. Returns how many entries went.
    internal int Drop(Func<Guid, bool> present)
    {
        var removed = 0;
        for (var i = 0; i < Size; i++)
        {
            var set = Sets[i];
            if (set == null) continue;
            removed += set.Places.RemoveAll(place => !present(place.Unit));
            foreach (var unit in new List<Guid>(set.Roles.Keys))
                if (!present(unit)) set.Roles.Remove(unit);
            removed += set.Summons.RemoveAll(summon => !present(summon.Summoner));
            if (present(set.Leader)) continue;
            removed++;
            if (set.Places.Count == 0)
            {
                removed += set.Summons.Count;
                Sets[i] = null;
                continue;
            }
            var next = set.Places[0];
            var places = new List<TacticalFormationRules.Slot>();
            foreach (var place in set.Places)
                if (place.Unit != next.Unit) places.Add(new TacticalFormationRules.Slot(place.Unit, place.X - next.X, place.Y - next.Y));
            var summons = new List<TacticalFormationSummons.Place>();
            foreach (var summon in set.Summons)
                summons.Add(new TacticalFormationSummons.Place(summon.Summoner, summon.Kind, summon.Number, summon.X - next.X, summon.Y - next.Y));
            Sets[i] = new Set(next.Unit, places, summons, set.Roles);
        }
        return removed;
    }

    private static bool Contains(IEnumerable<Guid> units, Guid unit)
    {
        foreach (var known in units)
            if (known == unit) return true;
        return false;
    }
}
