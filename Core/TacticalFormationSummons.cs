using System.Security.Cryptography;
using System.Text;

namespace DungeonSettlersDelvers.Core;

// Summons in the formation: a place belongs to its summoner, the summon's kind (its unit key, e.g. UNIT_Bubbleling) and a
// number, not to the summon itself, which gets a new GUID with every cast. When a summon dies its place stays reserved, and the
// next one of that kind and summoner takes the lowest free number, so a new Bubbleling takes the old one's place. Pure, so the
// offline tests cover it; the game side keeps the numbers of the living summons for the session (TacticalFormation). Each summon
// place is one row of the window's list (one place per summon, out of the eight).
internal static class TacticalFormationSummons
{
    internal readonly struct Place
    {
        internal Place(Guid summoner, string kind, int number, int x, int y)
        {
            Summoner = summoner;
            Kind = kind ?? "";
            Number = Math.Max(1, number);
            X = Math.Clamp(x, -TacticalFormationRules.MaxOffset, TacticalFormationRules.MaxOffset);
            Y = Math.Clamp(y, -TacticalFormationRules.MaxOffset, TacticalFormationRules.MaxOffset);
        }

        internal Guid Summoner { get; }
        internal string Kind { get; }
        internal int Number { get; }
        internal int X { get; }
        internal int Y { get; }
        internal Guid Id => IdOf(Summoner, Kind, Number);
    }

    // A fixed id for a summon place, the same in every session: the window and the grid treat it like a member's GUID.
    internal static Guid IdOf(Guid summoner, string kind, int number)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("summon|" + summoner.ToString("D") + "|" + kind + "|" + number));
        return new Guid(hash);
    }

    // The places that go with a member taken out of a set: its own; without members left, all of them (the grid drops them too).
    internal static List<Guid> GoWith(IEnumerable<Place> places, Guid removed, bool membersLeft)
    {
        var result = new List<Guid>();
        foreach (var place in places)
            if (!membersLeft || place.Summoner == removed) result.Add(place.Id);
        return result;
    }

    // The number of a new place of a summoner and kind: the lowest one no place of the set has yet.
    internal static int NextNumber(IEnumerable<Place> places, Guid summoner, string kind)
    {
        var used = new HashSet<int>();
        foreach (var place in places ?? Array.Empty<Place>())
            if (place.Summoner == summoner && place.Kind == kind) used.Add(place.Number);
        var next = 1;
        while (used.Contains(next)) next++;
        return next;
    }

    // The numbers of the living summons of one summoner and kind: those already numbered keep theirs; a new one takes the lowest
    // number that is free now, also one a summon that died has left. live is in the order the summons are found.
    internal static Dictionary<Guid, int> Number(IReadOnlyDictionary<Guid, int> known, IReadOnlyList<Guid> live)
    {
        var result = new Dictionary<Guid, int>();
        var used = new HashSet<int>();
        foreach (var unit in live)
            if (known != null && known.TryGetValue(unit, out var number) && used.Add(number)) result[unit] = number;
        foreach (var unit in live)
        {
            if (result.ContainsKey(unit)) continue;
            var next = 1;
            while (used.Contains(next)) next++;
            used.Add(next);
            result[unit] = next;
        }
        return result;
    }
}
