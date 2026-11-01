using System.Globalization;
using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: the base rules of GroupAiRules that had no check of their own (roles, cells on the grid, reserves, area flags,
// the status line), pinned as they decide today.
internal static class GroupAiRulesSection
{
    internal static void Run()
    {
        RoleDetection();
        SkillNames();
        CastCells();
        DodgeCells();
        DodgeCellsWithAnchor();
        CellKeysAndShapes();
        ReservesAndShares();
        StatusLine();
    }

    static GroupAiRole Role(string[] slots, string basicAttack = null, bool basicRanged = false, bool basicMagic = false, string ally = null)
        => GroupAiRules.DetectRole(slots, basicAttack, slot => slot == ally, basicRanged, basicMagic);

    static void RoleDetection()
    {
        // A heal makes a healer, a shield skill a shield; otherwise the basic attack decides (ranged before magic before melee).
        Check(Role(new[] { "SKILL_DelversLifebloom" }) == GroupAiRole.Heal && Role(new[] { "SKILL_StrangeSeed" }) == GroupAiRole.Heal
                && Role(new[] { "SKILL_HealingTouchAttack" }) == GroupAiRole.Heal,
            "role: each heal skill on the bar makes a healer");
        Check(Role(new string[0], "SKILL_HealingTouchAttack") == GroupAiRole.Heal && Role(new string[0], "SKILL_Slash") == GroupAiRole.Melee,
            "role: a heal as the basic attack makes a healer, another basic attack does not");
        Check(Role(new[] { "SKILL_Mend" }, ally: "SKILL_Mend") == GroupAiRole.Heal && Role(new[] { "SKILL_Mend" }) == GroupAiRole.Melee,
            "role: a skill the game marks as an ally skill makes a healer");
        Check(Role(new[] { "SKILL_ShieldUp" }) == GroupAiRole.Shield && Role(new[] { "SKILL_GuardiansTaunt" }) == GroupAiRole.Shield
                && Role(new[] { "SKILL_IronClad" }) == GroupAiRole.Shield && Role(new[] { "SKILL_BraceStance" }) == GroupAiRole.Shield
                && Role(new[] { "SKILL_ShieldHandling" }) == GroupAiRole.Shield && Role(new[] { "SKILL_GuardBarrier" }) == GroupAiRole.Shield,
            "role: each shield skill on the bar makes a shield");
        // Counter-case: a bar with a heal and a shield skill is a healer, in either order.
        Check(Role(new[] { "SKILL_ShieldUp", "SKILL_DelversLifebloom" }) == GroupAiRole.Heal
                && Role(new[] { "SKILL_DelversLifebloom", "SKILL_ShieldUp" }) == GroupAiRole.Heal
                && Role(new[] { "SKILL_ShieldUp", "SKILL_Mend" }, ally: "SKILL_Mend") == GroupAiRole.Heal,
            "role: a bar with a heal and a shield skill is a healer, in either order");
        Check(Role(new[] { "SKILL_ShieldUp" }, basicRanged: true, basicMagic: true) == GroupAiRole.Shield
                && Role(new[] { "SKILL_ShieldUp" }, "SKILL_HealingTouchAttack") == GroupAiRole.Heal,
            "role: a shield skill beats the basic attack's kind, a healing basic attack beats the shield");
        Check(Role(new string[0], basicRanged: true) == GroupAiRole.Ranged && Role(new string[0], basicMagic: true) == GroupAiRole.Caster
                && Role(new string[0], basicRanged: true, basicMagic: true) == GroupAiRole.Ranged && Role(new string[0]) == GroupAiRole.Melee,
            "role: the basic attack decides without heal or shield: ranged, then magic, else melee");
        Check(Role(new string[] { null, "SKILL_ShieldUp", null }) == GroupAiRole.Shield && Role(new string[] { null, null }) == GroupAiRole.Melee,
            "role: empty bar slots are skipped");
        Check(GroupAiRules.HealSkills.Count == 3 && GroupAiRules.ShieldSkills.Count == 6
                && !GroupAiRules.HealSkills.Overlaps(GroupAiRules.ShieldSkills),
            "role: three heal skills and six shield skills, none in both lists");
    }

    static void SkillNames()
    {
        Check(GroupAiRules.Short("SKILL_ArrowRain") == "ArrowRain" && GroupAiRules.Short("ArrowRain") == "ArrowRain"
                && GroupAiRules.Short(null) == "" && GroupAiRules.Short("") == "",
            "skill name: Short drops the SKILL_ prefix and takes null as empty");
        Check(GroupAiRules.RuleFor("bandit", "SKILL_ArrowRain") == "bandit-arrowrain" && GroupAiRules.RuleFor("heal", "SKILL_DelversLifebloom") == "heal-delverslifebloom"
                && GroupAiRules.RuleFor("bandit", null) == "bandit-",
            "skill name: a rule is prefix-skill in lower case");
    }

    static void CastCells()
    {
        // The unit walks straight towards the target (diagonal first, as far as it helps) until the skill's range reaches it.
        Check(GroupAiRules.CastCell(0, 0, 10, 0, 3) == (7, 0) && GroupAiRules.CastCell(0, 0, -6, 0, 2) == (-4, 0)
                && GroupAiRules.CastCell(0, 0, 0, 9, 4) == (0, 5),
            "cast cell: a straight line stops where the range reaches the target");
        Check(GroupAiRules.CastCell(0, 0, 5, 3, 2) == (3, 3) && GroupAiRules.CastCell(4, 4, 0, 0, 1) == (1, 1) && GroupAiRules.CastCell(0, 0, 2, 7, 3) == (2, 4),
            "cast cell: a diagonal step wherever both axes still differ, then straight on (Chebyshev distance)");
        Check(GroupAiRules.CastCell(3, 3, 5, 5, 2) == (3, 3) && GroupAiRules.CastCell(3, 3, 3, 3, 1) == (3, 3) && GroupAiRules.CastCell(3, 3, 6, 3, 3) == (3, 3),
            "cast cell: a target already in range keeps the unit where it is");
        Check(GroupAiRules.CastCell(2, 2, 9, 9, 0) == (2, 2) && GroupAiRules.CastCell(2, 2, 9, 9, -1) == (2, 2),
            "cast cell: no range (0 or less) means no walking");
        Check(GroupAiRules.CastCell(0, 0, 100, 0, 1) == (64, 0),
            "cast cell: at most 64 steps");
    }

    static bool Dodge(int x, int y, bool hasAnchor, int ax, int ay, Func<int, int, bool> usable, int maxRing, out int tx, out int ty)
        => GroupAiRules.PickDodgeCell(x, y, hasAnchor, ax, ay, usable, maxRing, out tx, out ty);

    static void DodgeCells()
    {
        // The nearest usable cell in rings of Chebyshev distance 1 to maxRing; a ring is scanned from the top row down, left to right.
        Check(!Dodge(5, 5, false, 0, 0, (cx, cy) => false, 3, out var nx, out var ny) && nx == 5 && ny == 5,
            "dodge cell: no usable cell gives false and leaves the unit's own cell");
        Check(Dodge(5, 5, false, 0, 0, (cx, cy) => true, 3, out var ax, out var ay) && ax == 4 && ay == 6,
            "dodge cell: all cells free, the first of ring 1 is the top left one");
        Check(Dodge(5, 5, false, 0, 0, (cx, cy) => (cx, cy) == (3, 5) || (cx, cy) == (6, 5), 3, out var rx, out var ry) && rx == 6 && ry == 5,
            "dodge cell: a cell of ring 1 beats a cell of ring 2");
        Check(Dodge(0, 0, false, 0, 0, (cx, cy) => (cx, cy) == (1, 1) || (cx, cy) == (-1, 0), 3, out var fx, out var fy) && fx == 1 && fy == 1,
            "dodge cell: within a ring the first one scanned wins, top rows before lower ones");
        Check(!Dodge(0, 0, false, 0, 0, (cx, cy) => Math.Max(Math.Abs(cx), Math.Abs(cy)) == 4, 3, out _, out _)
                && Dodge(0, 0, false, 0, 0, (cx, cy) => Math.Max(Math.Abs(cx), Math.Abs(cy)) == 4, 4, out var gx, out var gy) && Math.Max(Math.Abs(gx), Math.Abs(gy)) == 4
                && GroupAiRules.MaxDodgeRing == 3,
            "dodge cell: rings beyond maxRing are not looked at; the standing limit is ring 3");
        var asked = new List<(int, int)>();
        Dodge(10, 20, false, 0, 0, (cx, cy) => { asked.Add((cx, cy)); return false; }, 1, out _, out _);
        Check(asked.Count == 8 && !asked.Contains((10, 20)) && asked.Contains((9, 21)) && asked.Contains((11, 19)),
            "dodge cell: ring 1 offers the eight cells around the unit, in absolute coordinates, never its own");
    }

    static void DodgeCellsWithAnchor()
    {
        // A melee unit with an anchor (its target) prefers a cell next to the anchor, even from a farther ring.
        Func<int, int, bool> twoCells = (cx, cy) => (cx, cy) == (2, 0) || (cx, cy) == (-1, 0);
        Check(Dodge(0, 0, true, 3, 0, twoCells, 3, out var nearX, out var nearY) && nearX == 2 && nearY == 0,
            "dodge cell: with an anchor a cell of ring 2 next to it beats a cell of ring 1 without");
        Check(Dodge(0, 0, false, 3, 0, twoCells, 3, out var plainX, out var plainY) && plainX == -1 && plainY == 0,
            "dodge cell: without an anchor the same two cells give the one of ring 1");
        Check(Dodge(0, 0, true, 2, 0, (cx, cy) => (cx, cy) == (1, 1) || (cx, cy) == (2, 1), 3, out var bx, out var by) && bx == 1 && by == 1,
            "dodge cell: two cells next to the anchor, the nearer ring wins");
        Check(Dodge(0, 0, true, 2, 0, (cx, cy) => (cx, cy) == (1, 1) || (cx, cy) == (-2, 0), 3, out var cx1, out var cy1) && cx1 == 1 && cy1 == 1,
            "dodge cell: a cell next to the anchor in ring 1 beats a cell without in ring 2");
        Check(Dodge(0, 0, true, 2, 0, (cx, cy) => (cx, cy) == (1, 1) || (cx, cy) == (1, 0) || (cx, cy) == (1, -1), 3, out var dx, out var dy) && dx == 1 && dy == 1,
            "dodge cell: several cells next to the anchor in one ring, the first scanned wins");
        Check(Dodge(0, 0, true, 9, 9, (cx, cy) => (cx, cy) == (-1, 0) || (cx, cy) == (-2, 0), 3, out var ex, out var ey) && ex == -1 && ey == 0,
            "dodge cell: no cell next to the anchor, the nearest ring decides as without");
        // Next to the anchor means Chebyshev distance 1; the anchor's own cell does not count.
        Check(Dodge(0, 0, true, 3, 0, (cx, cy) => (cx, cy) == (3, 0) || (cx, cy) == (-1, 0), 3, out var hx, out var hy) && hx == -1 && hy == 0,
            "dodge cell: the anchor's own cell is not next to the anchor");
    }

    static void CellKeysAndShapes()
    {
        var keys = new HashSet<long>();
        for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                keys.Add(GroupAiRules.CellKey(x, y));
        Check(keys.Count == 49 && GroupAiRules.CellKey(1, 0) == 1L << 32 && GroupAiRules.CellKey(0, 1) == 1L && GroupAiRules.CellKey(2, 3) != GroupAiRules.CellKey(3, 2),
            "cell key: every cell of a 7 by 7 square, negative ones included, has its own key");
        Check(GroupAiRules.CanHitAllies(2) && GroupAiRules.CanHitAllies(4) && GroupAiRules.CanHitAllies(5)
                && !GroupAiRules.CanHitAllies(0) && !GroupAiRules.CanHitAllies(1) && !GroupAiRules.CanHitAllies(3) && !GroupAiRules.CanHitAllies(6),
            "friendly fire: SingleAny, MultipleAny and MultipleAnyExceptMe hit the caster's side");
        Check(GroupAiRules.FlagText(2) == "ffFlag=AffectTarget:SingleAny" && GroupAiRules.FlagText(4) == "ffFlag=AffectTarget:MultipleAny"
                && GroupAiRules.FlagText(5) == "ffFlag=AffectTarget:MultipleAnyExceptMe" && GroupAiRules.FlagText(3) == "ffFlag=AffectTarget:3",
            "friendly fire: the flag text names the three area values and prints any other number");
        Check(GroupAiRules.IsLine(7) && GroupAiRules.IsLine(8) && !GroupAiRules.IsLine(6) && !GroupAiRules.IsLine(9) && !GroupAiRules.IsLine(0),
            "area shape: only types 7 and 8 are lines");
    }

    static void ReservesAndShares()
    {
        // Energy must stay at or above reserve times maximum after the cost; a free skill is always possible.
        Check(GroupAiRules.HasReserve(8f, 20f, 3f, 0.25f) && !GroupAiRules.HasReserve(8f, 20f, 4f, 0.25f)
                && GroupAiRules.HasReserve(0f, 20f, 0f, 0.25f) && GroupAiRules.HasReserve(0f, 20f, -1f, 0.25f) && !GroupAiRules.HasReserve(0f, 20f, 1f, 0f),
            "reserve: the cost leaves at least the reserve of the maximum, a skill without cost always passes");
        Check(GroupAiRules.Share(20f, 30f, 100f) == 0.5f && GroupAiRules.Share(80f, 80f, 100f) == 1f && GroupAiRules.Share(-5f, 0f, 100f) == 0f
                && GroupAiRules.Share(10f, 10f, 0f) == 1f && GroupAiRules.Share(10f, 10f, -3f) == 1f,
            "share: head and body of the maximum, clamped to 0..1, a missing maximum counts as full");
        Check(GroupAiRules.Distance(0, 0, 3, -4) == 4 && GroupAiRules.Distance(2, 2, 2, 2) == 0 && GroupAiRules.Distance(-1, -1, 1, 1) == 2,
            "distance: Chebyshev, diagonals count as one cell");
    }

    static void StatusLine()
    {
        var rules = new Dictionary<string, int> { ["b-rule"] = 2, ["a-rule"] = 2, ["top-rule"] = 5 };
        var status = GroupAiRules.Status(false, 10, 1, 2, 3, 4, 5, 6, 0.25f, 7, 8, 9, 10, rules);
        Check(status == "enabled=false decisions=10 holds=1 dodges=2 friendlyFireSkips=3 shieldUpOutsideBoss=4 tauntLate=5 damageWhileAllyLow=6 lowestShare=0.25 seeds=7 lifeblooms=8 announcedCast=9 announcedDropped=10 rules=top-rule:5,a-rule:2,b-rule:2",
            "status line: field order, rules by count then by name");
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Check(GroupAiRules.Status(true, 1, 0, 0, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, rules).Contains(" lowestShare=0.50 ", StringComparison.Ordinal)
                    && GroupAiRules.Status(true, 1, 0, 0, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, rules).EndsWith("rules=top-rule:5,a-rule:2,b-rule:2", StringComparison.Ordinal),
                "status line under the German thread culture: the share keeps its point");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}
