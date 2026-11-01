namespace DungeonSettlersDelvers.Core;

// The campaign's formation book (up to eight formations to keep; Core, one book per campaign,
// since only one expedition is in the dungeon at a time and the game knows no key of an expedition). Eight sets, each with its
// leader and the places in the order of the window's list; the active one is the one the dungeon follows. An empty set is null.
// The priority casts stay per member (CoreSaveData.PriorityCasts). Pure, so the offline tests cover it.
internal sealed class TacticalFormationBook
{
    internal const int Size = 8;

    internal sealed class Set
    {
        internal Set(Guid leader, IEnumerable<TacticalFormationRules.Slot> places)
        {
            Leader = leader;
            foreach (var place in places ?? Array.Empty<TacticalFormationRules.Slot>())
                if (place.Unit != Guid.Empty && place.Unit != leader && !Places.Exists(known => known.Unit == place.Unit)) Places.Add(place);
        }

        internal Guid Leader { get; }
        internal List<TacticalFormationRules.Slot> Places { get; } = new();
    }

    private int active;

    internal Set[] Sets { get; } = new Set[Size];

    internal int Active
    {
        get => active;
        set => active = Math.Clamp(value, 0, Size - 1);
    }

    internal bool IsEmpty => Array.TrueForAll(Sets, set => set == null);

    // The active set applied to the members there now: false without the set's leader among them (the player sets a crown
    // first). A member of the set who is missing loses the place; a member the set does not know takes the next default place.
    internal static bool Apply(Set set, IReadOnlyList<Guid> group, out Guid leader, out List<TacticalFormationRules.Slot> slots)
    {
        leader = Guid.Empty;
        slots = new List<TacticalFormationRules.Slot>();
        if (set == null || group == null || !Contains(group, set.Leader)) return false;
        leader = set.Leader;
        var taken = new HashSet<(int, int)> { (0, 0) };
        foreach (var place in set.Places)
        {
            if (!Contains(group, place.Unit)) continue;
            slots.Add(place);
            taken.Add((place.X, place.Y));
        }
        var defaults = TacticalFormationRules.DefaultOffsets(TacticalFormationRules.GridSize * TacticalFormationRules.GridSize);
        var next = 0;
        foreach (var unit in group)
        {
            if (unit == leader || unit == Guid.Empty || slots.Exists(slot => slot.Unit == unit)) continue;
            while (next < defaults.Count && taken.Contains(defaults[next])) next++;
            if (next >= defaults.Count) break;
            slots.Add(new TacticalFormationRules.Slot(unit, defaults[next].X, defaults[next].Y));
            taken.Add(defaults[next]);
        }
        return true;
    }

    // The window's set written back: the places of the members it showed, in its list's order, then the places of the set's
    // members it did not show (a village selection plans part of the group), unless their cell is taken now.
    internal static Set Merge(Set old, Guid leader, IReadOnlyList<TacticalFormationRules.Slot> shown, IReadOnlyCollection<Guid> shownMembers)
    {
        var places = new List<TacticalFormationRules.Slot>(shown ?? Array.Empty<TacticalFormationRules.Slot>());
        var taken = new HashSet<(int, int)> { (0, 0) };
        foreach (var place in places) taken.Add((place.X, place.Y));
        foreach (var place in old?.Places ?? new List<TacticalFormationRules.Slot>())
        {
            if (place.Unit == leader || (shownMembers != null && Contains(shownMembers, place.Unit)) || !taken.Add((place.X, place.Y))) continue;
            places.Add(place);
        }
        return new Set(leader, places);
    }

    // Members who are gone leave every set; a set whose leader is gone becomes empty. Returns how many entries went.
    internal int Drop(Func<Guid, bool> present)
    {
        var removed = 0;
        for (var i = 0; i < Size; i++)
        {
            var set = Sets[i];
            if (set == null) continue;
            if (!present(set.Leader))
            {
                removed += 1 + set.Places.Count;
                Sets[i] = null;
                continue;
            }
            removed += set.Places.RemoveAll(place => !present(place.Unit));
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
