using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The player's priority casts (since then five places): the learned active skills laid in a
// unit's places, as many as the tactical menu keeps, cast in the laid
// order whenever one is ready, before the AI's own choice; the game picks the target. A place whose skill is switched off in the
// game's auto choice, or a unit without auto attack, takes no part ("autooff"). With the full group AI, the healer in the author's
// emergency casts the Strange Seed or Lifebloom from her places first, if one lies there. Pure, so the offline tests cover it;
// GroupAiPriority reads the game.
internal static class GroupAiPriorityRules
{
    // The healer's emergency casts among her places.
    internal static readonly string[] EmergencyCasts = { GroupAiHealerSeedRules.SeedSkill, LifebloomRules.SkillKey };

    // The order in which the places are tried: in an emergency of the healer her emergency casts first, each in its own place's
    // order, then the others; else the places as laid. Empty places are skipped.
    internal static List<int> Order(IReadOnlyList<string> places, bool emergency)
    {
        var order = new List<int>();
        if (places == null) return order;
        if (emergency)
            for (var i = 0; i < places.Count; i++)
                if (Array.IndexOf(EmergencyCasts, places[i]) >= 0) order.Add(i);
        for (var i = 0; i < places.Count; i++)
            if (!string.IsNullOrEmpty(places[i]) && !order.Contains(i)) order.Add(i);
        return order;
    }

    // CORE_AI_PRIORITY: one place tried; result cast, autooff, notlearned, noreach or the game's step that held it (gcd, cd, check:...).
    internal static string Line(string unit, int place, string skill, string result, bool emergency)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_PRIORITY unit={0} slot={1} skill={2} result={3}{4}",
            unit, place + 1, skill, result, emergency ? " emergency=1" : "");
}
