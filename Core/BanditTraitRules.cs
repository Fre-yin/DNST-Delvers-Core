using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Every bandit's own hidden tactics (bandits as random as we can make them; visible skills, hidden tactics): one
// fixed roll from the campaign and the bandit's GUID, stream BanditTraits of CoreRandomRules, without the tick, so the same bandit
// rolls the same on every load and on every player. What varies: its focus, weighted by class; its courage, a band around the entry
// into the seed and the kiting distance; which of two equally hurt comrades a healer takes first; how readily it plays a combo.
// The boss guard's chain stays fixed (the player is to learn it). Switch bandits.vary: off, all as before.
// Rules without the game.
internal enum BanditFocus
{
    Healer,
    Closest,
    Weakest,
    TopDamage,
    HealerAttacker
}

internal readonly record struct BanditTraits(BanditFocus Focus, int KiteDelta, float CourageDelta, bool HealsMeleeFirst, float ComboTendency);

internal static class BanditTraitRules
{
    internal const string SwitchKey = "bandits.vary";
    internal const float CourageBand = 0.05f;
    internal const float TieShare = 0.02f;
    internal const float ComboMin = 0.5f;
    internal const int LogCap = 60;

    // As before the variation: the party's healer in focus, today's distances, nearest first, every combo chance taken at the middle.
    internal static readonly BanditTraits Plain = new(BanditFocus.Healer, 0, 0f, true, 1f);

    private static readonly (BanditFocus Focus, int Weight)[] MeleeWeights =
    {
        (BanditFocus.Weakest, 45), (BanditFocus.Closest, 35), (BanditFocus.TopDamage, 10), (BanditFocus.HealerAttacker, 5), (BanditFocus.Healer, 5)
    };

    private static readonly (BanditFocus Focus, int Weight)[] GuardWeights =
    {
        (BanditFocus.HealerAttacker, 40), (BanditFocus.Closest, 30), (BanditFocus.TopDamage, 20), (BanditFocus.Weakest, 10)
    };

    private static readonly (BanditFocus Focus, int Weight)[] RangedWeights =
    {
        (BanditFocus.Healer, 50), (BanditFocus.Weakest, 25), (BanditFocus.TopDamage, 15), (BanditFocus.Closest, 10)
    };

    private static readonly (BanditFocus Focus, int Weight)[] HealerWeights =
    {
        (BanditFocus.Closest, 50), (BanditFocus.HealerAttacker, 30), (BanditFocus.Weakest, 20)
    };

    // The weights of a class by its role word (the bandits' forms); melee for an unknown one.
    internal static (BanditFocus Focus, int Weight)[] WeightsOf(string role) => role switch
    {
        "guard" => GuardWeights,
        "archer" or "firemage" or "watermage" => RangedWeights,
        "healer" => HealerWeights,
        _ => MeleeWeights
    };

    internal static BanditFocus PickFocus(string role, double roll)
    {
        var weights = WeightsOf(role);
        var total = 0;
        foreach (var entry in weights) total += entry.Weight;
        var at = roll * total;
        foreach (var entry in weights)
        {
            at -= entry.Weight;
            if (at < 0) return entry.Focus;
        }
        return weights[^1].Focus;
    }

    // The bandit's traits from four rolls in [0, 1).
    internal static BanditTraits Of(string role, double focus, double courage, double heal, double combo)
    {
        var kite = courage < 1.0 / 3 ? 1 : courage < 2.0 / 3 ? 0 : -1;
        var band = (float)((courage - 0.5) * 2 * CourageBand);
        return new BanditTraits(PickFocus(role, focus), kite, -band, heal < 0.5, ComboMin + (float)combo * (1f - ComboMin));
    }

    internal static BanditTraits Roll(Guid campaign, Guid unit, string role)
    {
        double Draw(uint count) => CoreRandomRules.Value(campaign, 0, unit, CoreRandomRules.BanditTraits, count);
        return Of(role, Draw(0), Draw(1), Draw(2), Draw(3));
    }

    // The healer's order among comrades: the side she does not take first counts TieShare more hurt less, so only near ties turn.
    internal static float TieBias(bool healsMeleeFirst, bool melee) => healsMeleeFirst == melee ? 0f : TieShare;

    internal static string Line(string unit, string role, BanditTraits traits)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_TRAITS unit={0} role={1} focus={2} kite={3} courage={4:0.00} heals={5} combo={6:0.00}",
            unit ?? "-", role ?? "-", traits.Focus, traits.KiteDelta, traits.CourageDelta, traits.HealsMeleeFirst ? "melee" : "ranged", traits.ComboTendency);
}
