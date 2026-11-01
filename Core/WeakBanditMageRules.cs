namespace DungeonSettlersDelvers.Core;

// The weak water mage's summons (BanditWeakClasses.WaterMageSummon), pure. It holds one bubbling and, from
// level 3, the toad. The game's SummonBubbling and SummonBubbleToad summon on a cell within their range (SkillTable: TargetType 2,
// Range 4), so the mage casts them on a free cell next to itself, the one nearest its closest foe first; a taken cell is skipped.
internal static class WeakBanditMageRules
{
    internal const int HeldBubblings = 1;
    // The toad (level 3) eats the nearest bubbling (SkillTable SummonBubbleToad: CreatureKey UNIT_Bubbleling, 1) and one stands per
    // caster; so the mage summons it while it has a bubbling and no toad, then replaces the bubbling.
    internal const int HeldToads = 1;
    internal const int ToadEats = 1;

    internal static bool WantsToad(int toads, int bubblings) => toads < HeldToads && bubblings >= ToadEats;

    // With a bandit below the wave's share the wave spent every bubbling first and the toad never came. The
    // toad goes before the wave, unless a bandit of the group is critical (below CriticalShare): then the wave first.
    internal const float CriticalShare = 0.3f;

    internal static bool ToadFirst(bool wantsToad, float lowestShare) => wantsToad && !(lowestShare < CriticalShare);

    // The eight neighbours, in a fixed order for equal distances.
    private static readonly (int Dx, int Dy)[] Around = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) };

    internal static bool WantsBubbling(int own) => own < HeldBubblings;

    // The neighbour cells of self, not taken, nearest the foe first (Chebyshev, then the fixed order).
    internal static List<(int X, int Y)> BubblingCells((int X, int Y) self, (int X, int Y) foe, ISet<(int, int)> taken)
    {
        var cells = new List<(int X, int Y, int Distance, int Order)>();
        for (var i = 0; i < Around.Length; i++)
        {
            var cell = (self.X + Around[i].Dx, self.Y + Around[i].Dy);
            if (taken != null && taken.Contains(cell)) continue;
            cells.Add((cell.Item1, cell.Item2, Math.Max(Math.Abs(cell.Item1 - foe.X), Math.Abs(cell.Item2 - foe.Y)), i));
        }
        cells.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Order.CompareTo(b.Order));
        var result = new List<(int X, int Y)>();
        foreach (var c in cells) result.Add((c.X, c.Y));
        return result;
    }
}
