using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Aftercare after a fight (a bonus for 0.5.0): once a bleeding party unit has been out of combat for
// QuietSeconds, it dresses its own wound, the most urgent unit first, one order per look and at most one bandage per unit every
// GroupAiBandageRules.CooldownSeconds. It takes the smallest bandage that covers the blood loss, so nothing is wasted: a lethal
// wound wants the aid kit, running bleeding stacks a bandage of first aid. Pure, so the offline tests cover it; GroupAiAftercare
// reads the game.
internal static class GroupAiAftercareRules
{
    // Out of combat this long before the first bandage, so a fight that only pauses does not count.
    internal const float QuietSeconds = 3f;
    // How often the party is looked at, in game seconds.
    internal const float TickSeconds = 1f;

    private static readonly string[] LethalOrder =
        { GroupAiBandageRules.AidKit, GroupAiBandageRules.Salve, GroupAiBandageRules.Herbal, GroupAiBandageRules.Heavy, GroupAiBandageRules.Light };
    private static readonly string[] StacksOrder =
        { GroupAiBandageRules.Herbal, GroupAiBandageRules.Salve, GroupAiBandageRules.Heavy, GroupAiBandageRules.Light };
    private static readonly string[] SmallOrder =
        { GroupAiBandageRules.Light, GroupAiBandageRules.Heavy, GroupAiBandageRules.Herbal, GroupAiBandageRules.Salve };
    private static readonly string[] MiddleOrder =
        { GroupAiBandageRules.Heavy, GroupAiBandageRules.Herbal, GroupAiBandageRules.Salve, GroupAiBandageRules.Light };
    private static readonly string[] LargeOrder =
        { GroupAiBandageRules.Herbal, GroupAiBandageRules.Salve, GroupAiBandageRules.Heavy, GroupAiBandageRules.Light };

    // The kinds from the most fitting to the last stand-in: a light bandage covers a blood loss up to its share of the whole
    // health, a heavy one up to its share, beyond that the two of first aid close the wound.
    internal static string[] Order(WoundRules.Stage stage, double buffer, double maxLife, bool stacksRunning)
    {
        if (stage == WoundRules.Stage.Fatal) return LethalOrder;
        if (stacksRunning) return StacksOrder;
        if (!(maxLife > 0)) return LargeOrder;
        if (buffer <= BandageRules.ShareOf(BandageRules.Kind.Light) * maxLife) return SmallOrder;
        return buffer <= BandageRules.ShareOf(BandageRules.Kind.Heavy) * maxLife ? MiddleOrder : LargeOrder;
    }

    // The first ready kind in that order, else null.
    internal static string Pick(WoundRules.Stage stage, double buffer, double maxLife, bool stacksRunning, Func<string, bool> ready)
    {
        foreach (var kind in Order(stage, buffer, maxLife, stacksRunning))
            if (ready(kind)) return kind;
        return null;
    }

    // The most urgent first: the higher stage, then the greater blood loss.
    internal static int Urgency((WoundRules.Stage Stage, double Buffer) a, (WoundRules.Stage Stage, double Buffer) b)
        => a.Stage != b.Stage ? b.Stage.CompareTo(a.Stage) : b.Buffer.CompareTo(a.Buffer);

    internal static bool Quiet(float now, float lastCombat) => now - lastCombat >= QuietSeconds;

    // CORE_AI_AFTERCARE: one order or why none (none, none:noslot, failed:...); from: q, extra1, extra2 or bag.
    internal static string Line(string unit, string item, string stage, double buffer, float pool, string result, string from, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_AFTERCARE unit={0} item={1} stage={2} buffer={3:0.0} pool={4:0.00} result={5} from={6} tick={7}",
            unit, item ?? "-", stage, buffer, pool, result, from ?? "-", tick);

    // CORE_AI_AFTERCARE_DONE: the aftercare's end once no party unit bleeds: game seconds since its first order and bandages used.
    internal static string DoneLine(float seconds, int used)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_AFTERCARE_DONE seconds={0:0.0} used={1}", seconds, used);
}
