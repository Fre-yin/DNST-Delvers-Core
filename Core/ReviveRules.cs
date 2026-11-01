namespace DungeonSettlersDelvers.Core;

// Rules of reviving a dead party member with the revive scroll. Pure, so
// the offline tests cover them; the game side reads the campaign's difficulty and calls these.
// Revive count and sickness follow the combat difficulty; the price adds a part per difficulty.
internal static class ReviveRules
{
    internal const int Unlimited = -1;
    internal const double DaysPerWeek = 7.0;
    internal const double MaxSicknessWeeks = 4.0;

    // The revived character starts with this share of its health; the sickness caps health and
    // energy at the same share and lifts the cap evenly until it ends.
    internal const double StartHealthShare = 0.10;

    // The game's difficulty values Easy 0 to Downfall 4; Apocalypse is Core's step above.
    internal enum Level { Easy = 0, Normal = 1, Hard = 2, VeryHard = 3, Downfall = 4, Apocalypse = 5 }

    internal static Level FromGame(int difficulty, bool apocalypse)
    {
        var level = (Level)Math.Clamp(difficulty, 0, 4);
        return apocalypse && level == Level.Downfall ? Level.Apocalypse : level;
    }

    internal static int RevivesAllowed(Level combat) => combat switch
    {
        Level.Apocalypse or Level.Downfall => 2,
        Level.VeryHard => 4,
        _ => Unlimited
    };

    internal static int RevivesLeft(Level combat, int revivesDone)
    {
        var allowed = RevivesAllowed(combat);
        return allowed == Unlimited ? Unlimited : Math.Max(0, allowed - Math.Max(0, revivesDone));
    }

    // Sickness of the given revive (1 for the first): it doubles with every further revive and
    // stops at four weeks.
    internal static double SicknessWeeks(Level combat, int revive)
    {
        if (revive < 1) throw new ArgumentOutOfRangeException(nameof(revive));
        var first = combat switch
        {
            Level.Apocalypse => 2.0,
            Level.Downfall or Level.VeryHard => 1.0,
            _ => 0.5
        };
        return Math.Min(MaxSicknessWeeks, first * Math.Pow(2, Math.Min(revive - 1, 8)));
    }

    internal static double SicknessDays(Level combat, int revive) => SicknessWeeks(combat, revive) * DaysPerWeek;

    // The game's effect durations count down 0.017 per tick, 45 ticks per game minute
    // (toolkit research, artifacts\recherche-nachwirkung-20261005): one game day is 1101.6 units.
    internal const double DurationUnitsPerDay = 24.0 * 60.0 * 45.0 * 0.017;

    // After a down (rescued from near-death) health and energy start at 65 %
    // and rise to full while the game's own after-effect runs out.
    internal const double DownStartShare = 0.65;

    // The share of health and energy a sick character may have: StartHealthShare right after
    // the revive, rising evenly to 1 when the sickness ends.
    internal static double SicknessCap(double left, double total) => Cap(StartHealthShare, left, total);

    internal static double DownCap(double left, double total) => Cap(DownStartShare, left, total);

    private static double Cap(double start, double left, double total)
    {
        if (!(total > 0) || !(left > 0)) return 1.0;
        var progress = 1.0 - Math.Clamp(left / total, 0.0, 1.0);
        return start + (1.0 - start) * progress;
    }

    internal static int PricePart(Level level) => level switch
    {
        Level.Apocalypse => 6000,
        Level.Downfall => 5000,
        Level.VeryHard => 4000,
        Level.Hard => 3000,
        _ => 2000
    };

    // Combat part plus management part: Normal and Normal 4000, Apocalypse and Apocalypse 12000.
    internal static int Price(Level combat, Level management) => PricePart(combat) + PricePart(management);

    // A snapshot keeps the permanent traits a death removes. Newer data wins (load, every dungeon
    // floor, near-death), except a list taken while the character is already dying: its traits
    // may be leaving stack by stack, so it never replaces one that exists. An empty list never
    // replaces anything either; every character has permanent traits.
    internal static bool ShouldReplaceSnapshot(bool hasSnapshot, int freshCount, bool dying)
        => freshCount > 0 && !(dying && hasSnapshot);

    // Where a revived character appears (the caster must not block): on the
    // body's cell when no living unit stands there, otherwise on the first free neighbour in this
    // fixed order: N, E, S, W, NE, SE, SW, NW (x grows to the east, y to the north). The caster
    // walks onto the body's cell to cast, so its own cell must never block the revive.
    internal static readonly int[] NeighbourOffsets = { 0, 1, 1, 0, 0, -1, -1, 0, 1, 1, 1, -1, -1, -1, -1, 1 };

    internal enum ReviveCellKind { None, Corpse, Neighbour }

    internal static ReviveCellKind ChooseReviveCell(int x, int y, bool bodyCellFree, Func<int, int, bool> isFree,
        out int cellX, out int cellY)
    {
        cellX = x;
        cellY = y;
        if (bodyCellFree) return ReviveCellKind.Corpse;
        for (var i = 0; i < NeighbourOffsets.Length; i += 2)
        {
            var nx = x + NeighbourOffsets[i];
            var ny = y + NeighbourOffsets[i + 1];
            if (!isFree(nx, ny)) continue;
            cellX = nx;
            cellY = ny;
            return ReviveCellKind.Neighbour;
        }
        return ReviveCellKind.None;
    }

    internal static string Describe(ReviveCellKind kind) => kind switch
    {
        ReviveCellKind.Corpse => "corpse",
        ReviveCellKind.Neighbour => "neighbor",
        _ => "none"
    };
}

// One permanent trait of a snapshot, written "KEY:Stacks" in the API and the save.
internal readonly struct TraitStack : IEquatable<TraitStack>
{
    internal TraitStack(string key, int stacks)
    {
        Key = key;
        Stacks = stacks;
    }

    internal string Key { get; }
    internal int Stacks { get; }

    public override string ToString() => Key + ":" + Stacks.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static bool TryParse(string text, out TraitStack entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var colon = text.LastIndexOf(':');
        if (colon <= 0 || colon == text.Length - 1) return false;
        var key = text.Substring(0, colon).Trim();
        if (key.Length == 0 || !int.TryParse(text.Substring(colon + 1), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var stacks) || stacks < 1) return false;
        entry = new TraitStack(key, stacks);
        return true;
    }

    public bool Equals(TraitStack other) => Key == other.Key && Stacks == other.Stacks;
    public override bool Equals(object obj) => obj is TraitStack other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Key, Stacks);
}
