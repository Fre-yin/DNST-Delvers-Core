using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// (the blade's basic attack on Darius hit the fallen Frieren beside him 3×,
// one tick later each time): the greatsword's basic attack is no single hit. SKILL_BasicGreatswordAttack.SkillRoutine
// takes ExecutionArea.ConeExceptMe.GetArea(user, target, 1) and deals 100 % to the target, 40 % to the
// cone. While fallen units are skipped, a bandit with such a basic attack turns to a foe in reach whose cone holds no
// fallen unit; without one it strikes anyway, else it would stand still (as GroupAiRules.AvoidsDownedArea for skills).
internal static class BanditConeRules
{
    internal const int ConeRadius = 1;

    // Multiple hits on the fallen are fine; the sparing below runs only with this setting on.
    internal const string SpareKey = "bandits.sparedownedincone";

    // A taunted bandit stays on its taunter; the cone check turns only a free one.
    internal static bool ConeMayTurn(bool taunted) => !taunted;

    // The basic attacks that hit the game's cone (the skill key without SKILL_).
    internal static bool ConeBasic(string basicShort) => basicShort == "BasicGreatswordAttack";

    // candidates in order (the current target first, then the focus, then by distance), each with whether its cone
    // holds a fallen unit. "keep": the current cone is clear or fallen units may be finished; "switch": pick is the first
    // clear one; "none": no clear one, the bandit strikes the current target anyway.
    internal static (string Action, string Pick) ConePick(bool finishAllowed, IReadOnlyList<(string Guid, bool HitsDowned)> candidates)
    {
        if (finishAllowed || candidates == null || candidates.Count == 0 || !candidates[0].HitsDowned) return ("keep", candidates != null && candidates.Count > 0 ? candidates[0].Guid : null);
        for (var i = 1; i < candidates.Count; i++)
            if (!candidates[i].HitsDowned) return ("switch", candidates[i].Guid);
        return ("none", candidates[0].Guid);
    }

    internal static string ConeLine(string unit, string skill, string action, string from, string to, int downed, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_AREA_DOWNED unit={0} skill={1} downed={2} action={3} from={4} to={5} tick={6}",
            unit, skill, downed, action, from ?? "-", to ?? "-", tick);
}
