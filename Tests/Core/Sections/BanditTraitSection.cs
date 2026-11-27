using DungeonSettlersDelvers.Core;
using static DungeonSettlersDelvers.Core.BanditTraitRules;
using static TestKit;

// Every bandit's hidden tactics (BanditTraitRules): a fixed roll per bandit, focus by class, a small band of courage,
// the healer's near ties and the combo tendency between 50 and 100 %.
internal static class BanditTraitSection
{
    internal static void Run()
    {
        var campaign = Guid.Parse("4799f5bf-0000-4000-8000-000000000001");
        var unit = Guid.Parse("7777cd79-53b6-4675-8f0f-6ffd053b9d3f");
        Check(Roll(campaign, unit, "archer") == Roll(campaign, unit, "archer"), "traits: the same bandit rolls the same, without the tick");
        var sets = new HashSet<BanditTraits>();
        var focus = new Dictionary<BanditFocus, int>();
        for (var i = 0; i < 2000; i++)
        {
            var rolled = Roll(campaign, new Guid(i, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10), "blade");
            sets.Add(rolled);
            focus[rolled.Focus] = focus.TryGetValue(rolled.Focus, out var count) ? count + 1 : 1;
            if (rolled.ComboTendency < 0.5f || rolled.ComboTendency >= 1f || Math.Abs(rolled.CourageDelta) > CourageBand || Math.Abs(rolled.KiteDelta) > 1)
                Check(false, "traits: a roll left its band");
        }
        Check(sets.Count > 100 && focus[BanditFocus.Weakest] > focus[BanditFocus.Closest] && focus[BanditFocus.Closest] > focus[BanditFocus.TopDamage],
            "traits: bandits differ, and a melee class goes for the weakest more often than the closest");
        Check(PickFocus("archer", 0.0) == BanditFocus.Healer && PickFocus("guard", 0.0) == BanditFocus.HealerAttacker
                && PickFocus("healer", 0.0) == BanditFocus.Closest && PickFocus("blade", 0.999) == BanditFocus.Healer
                && PickFocus("unknown", 0.0) == BanditFocus.Weakest,
            "traits: each class has its own weights, an unknown role the melee's");
        Check(Of("guard", 0.5, 0.0, 0.2, 0.0).KiteDelta == 1 && Of("guard", 0.5, 0.5, 0.2, 0.0).KiteDelta == 0 && Of("guard", 0.5, 0.9, 0.2, 0.0).KiteDelta == -1
                && Of("guard", 0.5, 0.5, 0.2, 0.0).HealsMeleeFirst && !Of("guard", 0.5, 0.5, 0.7, 0.0).HealsMeleeFirst
                && Of("guard", 0.5, 0.5, 0.2, 0.0).ComboTendency == 0.5f,
            "traits: courage moves the distance by one cell at most, the healer's order and the tendency come from their own rolls");
        Check(Plain == new BanditTraits(BanditFocus.Healer, 0, 0f, true, 1f) && TieBias(true, true) == 0f && TieBias(true, false) == TieShare && TieBias(false, true) == TieShare,
            "traits: the switch off gives today's behaviour, and only near ties turn the healer's order");
        Check(Line("u", "blade", Plain) == "CORE_BANDIT_TRAITS unit=u role=blade focus=Healer kite=0 courage=0.00 heals=melee combo=1.00", "traits: the line names the roll");
    }
}
