namespace DungeonSettlersDelvers.Core;

// The summons the party can plan in the formation before one lives (P08e of the 0.5.2 hand test; plan agreed with Core): a member
// who has learned a summoning skill offers its kind in "+ Hinzufügen". Only the party's own summoning
// skills; SKILL_SummonLadybug is a monster's and not in the party's skill tree. PerSummoner is how many of a kind the game lets one
// unit have at once (2 bubblelings, 1 toad; Core reads that limit nowhere, see SummonLimit), so no more places of a kind are offered
// for one summoner. A summoning skill not listed here is named once in the log by the window, so a new kind shows up. Pure, so the
// offline tests cover it.
internal static class TacticalFormationSummonCatalog
{
    internal readonly record struct Kind(string Skill, string Unit, int PerSummoner);

    internal static readonly IReadOnlyList<Kind> Kinds = new[]
    {
        new Kind("SKILL_SummonBubbling", "UNIT_Bubbleling", 2),
        new Kind("SKILL_SummonBubbleToad", "UNIT_BubbleToad", 1)
    };

    private const string SummonPrefix = "SKILL_Summon";

    // The skill that summons a kind, null for a kind not listed.
    internal static string SkillOf(string unit)
    {
        foreach (var kind in Kinds)
            if (string.Equals(kind.Unit, unit, StringComparison.Ordinal)) return kind.Skill;
        return null;
    }

    // How many places of a kind one summoner may have; a kind not listed (seen alive only) is bound by the window's rows alone.
    internal static int PerSummoner(string unit)
    {
        foreach (var kind in Kinds)
            if (string.Equals(kind.Unit, unit, StringComparison.Ordinal)) return kind.PerSummoner;
        return int.MaxValue;
    }

    // The kinds a member's skills let it summon, in the catalogue's order; unknown: its summoning skills the catalogue lacks.
    internal static List<string> KindsFor(IEnumerable<string> skills, out List<string> unknown)
    {
        var kinds = new List<string>();
        unknown = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(skill)) continue;
            var found = false;
            foreach (var kind in Kinds)
                if (string.Equals(kind.Skill, skill, StringComparison.Ordinal))
                {
                    found = true;
                    known.Add(kind.Unit);
                }
            if (!found && skill.StartsWith(SummonPrefix, StringComparison.Ordinal) && !unknown.Contains(skill)) unknown.Add(skill);
        }
        foreach (var kind in Kinds)
            if (known.Contains(kind.Unit)) kinds.Add(kind.Unit);
        return kinds;
    }

    // Whether one more place of a kind may be planned for a summoner that has placed of them already.
    internal static bool CanPlanMore(string unit, int placed) => placed < PerSummoner(unit);
}
