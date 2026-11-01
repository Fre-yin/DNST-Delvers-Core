namespace DungeonSettlersDelvers.Core;

// WoundRules, the bandages: what a bandage takes from the wound's buffer, what it costs, and the share of the game's
// bleeding stacks it takes at once. Pure, like the rest of WoundRules.
internal static partial class WoundRules
{
    // What a bandage takes, as a share of the unit's whole health (set C): light 10 %, heavy 20 %; the herbal
    // and the salve bandage close every wound (ClosesWound).
    internal const double LightBandageShare = 0.10;
    internal const double HeavyBandageShare = 0.20;
    internal const double ClosesWound = double.PositiveInfinity;

    // Merchant prices of the bandages. Herbal and salve sit just below the game's first aid kit
    // (ITEM_AidKit, 300), the salve above the herbal one.
    internal const int LightBandagePrice = 20;
    internal const int HeavyBandagePrice = 45;
    internal const int HerbalBandagePrice = 250;
    internal const int SalveBandagePrice = 280;
    internal const int AidKitPrice = 300;

    // The herbal and salve bandages on a lethal wound: like the light and heavy one they take a share of
    // the whole health from the buffer, the herbal one 30 %, the salve 40 %. With FatalBandageClosesKey on they close it, as on
    // every other stage.
    internal const string FatalBandageClosesKey = "wounds.fatal.bandagecloses";
    internal const double HerbalFatalShare = 0.30;
    internal const double SalveFatalShare = 0.40;

    internal static double FatalBandage(double buffer, bool salve, double maxLife, bool closes)
        => closes ? 0 : Bandage(buffer, salve ? SalveFatalShare : HerbalFatalShare, maxLife);

    // The name of a stage the party's bandage takes at once, whatever the pool (GroupAiBandageRules.Trigger); null below.
    internal static string SevereName(Stage stage) => stage == Stage.Fatal ? "lethal" : stage == Stage.Heavy ? "heavy" : null;

    // The share of the game's bleeding stacks a bandage of first aid takes at once, besides closing
    // the wound (herbal "ca 33%", salve "alles um 50%"; the salve then heals on
    // against the rest).
    internal const double HerbalBleedingCut = 1.0 / 3.0;
    internal const double SalveBleedingCut = 0.5;

    // Stacks to take from the game's bleeding for the given share, rounded up and never more than
    // there are.
    internal static int BleedingCut(int stacks, double share)
    {
        if (stacks > 0 && share > 0) return Math.Min(stacks, (int)Math.Ceiling(stacks * share - 1e-9));
        return 0;
    }

    // The buffer after a bandage taking the given share of the unit's whole health.
    internal static double Bandage(double buffer, double share, double maxLife)
        => double.IsPositiveInfinity(share) ? 0 : Math.Max(0, buffer - Math.Max(0, share) * MaxLifeOr(maxLife));
}
