using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// What sets a combo call off: holding a foe, weakening it, or bringing foes together.
internal enum BanditCallKind { None, Hold, Weaken, Bunch }

// One combo: the skills that start it, the follow-ups the partner takes first, how long that stays open, its weight, the call that
// warns of it and whether the hit waits for that call, how far the partner may walk, whether one bandit plays both parts, and
// whether the follow-up goes for the middle of the foes rather than the starter's target.
internal sealed class BanditComboRow
{
    internal BanditComboRow(int number, string[] starters, string[] follows, double window, double weight, BanditCallKind call,
        bool waitsForCall = true, int walk = 0, bool alone = false, bool aimsAtCrowd = false)
    {
        Number = number;
        Starters = starters;
        Follows = follows;
        Window = window;
        Weight = weight;
        Call = call;
        WaitsForCall = waitsForCall;
        Walk = walk;
        Alone = alone;
        AimsAtCrowd = aimsAtCrowd;
    }

    internal int Number { get; }
    internal string[] Starters { get; }
    internal string[] Follows { get; }
    internal double Window { get; }
    internal double Weight { get; }
    internal BanditCallKind Call { get; }
    internal bool WaitsForCall { get; }
    internal int Walk { get; }
    internal bool Alone { get; }
    internal bool AimsAtCrowd { get; }
}

// The bandits' combos, pure so the offline tests cover them. One board for all bandits (GroupAiBandits, BanditCombos):
// a partner reports its free follow-up in every decision; a starter opens a combo only with a partner ready and in reach and, but for
// the grace, never waits for one; the partner then takes the follow-up before all else while the window is open, a dodge still first; a missing
// partner lets the combo lapse silently. A bandit's tendency times the combo's weight gives the chance at each opening.
// No combo without a warning: a combo with a call starts only with the calls on (CallsKey, off by default until the texts are final).
// The starter then calls out, the hit comes CallDelay game seconds later, and a group calls at most every CallThrottle seconds; a
// throttled call starts no combo. A bandit alone (6, 11, 13) needs no call, its follow-up is a normal move.
// Left out for now: 14 (a shield, no target on the foes' side), Explosion (3, 8 with it) until its warning is shown, 9 until a second camp.
// The grace (a grace in which combos become possible, the skills kept when they overlap in time, the global
// cooldown counted, and no other skill shutting a combo skill out): when a starter chooses its skill and a partner's follow-up gets
// ready within the grace, the starter waits for the combo's moment and both hold that skill for the combo (a reservation) instead of
// spending it elsewhere; another action is left out while it would still bind the bandit at the combo's moment. Reserved is only
// what the starter's kit chose, and the start and the reservation weigh the partner on one path (FollowReady).
// The game gives bandits no global cooldown (catalog 02-skills-passive, point 8: only the player's side), so what binds them is the
// action itself, as measured in play: a skill SkillBusySeconds, the basic attack BasicBusySeconds. A global cooldown still counts
// the moment the game gives one, read from the game while it runs (BanditSituation). A reservation lives at most the grace plus the
// hit's delay, and the follow-up stays open a grace after the hit begins.
internal static class BanditComboRules
{
    internal const string CallsKey = "bandits.combo.calls";
    internal const string GraceKey = "bandits.combo.grace";
    internal const double GraceDefault = 3.0;
    // What one action binds a bandit until its next decision, measured (medians 0.2 to 1.6 s after a skill, 2.7 s after the basic attack).
    internal const double SkillBusySeconds = 1.0;
    internal const double BasicBusySeconds = 2.7;
    // The rule of a bandit that stands for its combo (GroupAiHealerRules.HoldsStill: the game gets nothing, it decides again soon).
    internal const string HoldRule = "combo-hold";
    // The slot states of a held combo skill and of a skill left out for it.
    internal const string HeldState = "combo-hold";
    internal const string HeldBackState = "combo-gcd";
    // Lines of combos that did not start, at most this many per campaign.
    internal const int NoStartCap = 400;

    // Weights: with a warning, focus and weakening, hard ones without a warning.
    internal const double WeightWarned = 1.0;
    internal const double WeightFocus = 0.8;
    internal const double WeightHard = 0.5;

    internal const double CallDelay = 1.5;
    internal const double CallThrottle = 6.0;
    // A partner's report counts this long.
    internal const double ReportSeconds = 1.0;
    // A starter rolls once per use, also when its decision comes again before the skill went off.
    internal const double RollSeconds = 1.0;

    // The fire mage's own combo (13): Incinerate first on a target with this many Burning stacks, one roll per BurnRollSeconds.
    internal const string BurningKey = "AFFECTER_Burning";
    internal const int BurnStacks = 3;
    internal const double BurnRollSeconds = 6.0;
    internal const int BurnNumber = 13;
    internal const double BurnWeight = WeightFocus;

    internal const string RulePrefix = "combo";
    // The board's lists hold at most this many bandits; a long run through many rooms starts them over.
    internal const int BoardCap = 128;
    // Arrow Rain's area.
    internal const int RainRadius = 2;

    internal static readonly BanditComboRow[] Rows =
    {
        new(1, new[] { "SKILL_StunCharge" }, new[] { BanditKitKeys.Slay }, 2.0, WeightHard, BanditCallKind.Hold, walk: 1),
        new(2, new[] { BanditChainRules.SkillKey }, new[] { BanditKitKeys.Slay, BanditKitKeys.DestructiveSplit }, 2.0, WeightHard, BanditCallKind.Hold, walk: 1),
        // Medium and melee only: the call comes without the wait.
        new(4, new[] { WeakBanditKitKeys.PommelStrike }, new[] { WeakBanditKitKeys.OpeningStrike }, 1.0, WeightHard, BanditCallKind.Hold, waitsForCall: false),
        new(5, new[] { WeakBanditKitKeys.OpeningStrike, WeakBanditKitKeys.VileInsult }, new[] { WeakBanditKitKeys.Incinerate, BanditKitKeys.SnipingShot }, 8.0, WeightFocus, BanditCallKind.Weaken),
        new(6, new[] { BanditFearRules.SkillKey }, new[] { BanditKitKeys.Slay }, 5.0, WeightFocus, BanditCallKind.None, waitsForCall: false, walk: 8, alone: true),
        new(7, new[] { WeakBanditKitKeys.TidalMark }, new[] { GroupAiWaterRules.WaterDive }, 5.0, WeightFocus, BanditCallKind.None, waitsForCall: false, alone: true),
        new(8, new[] { GroupAiRules.SlowRingSkill }, new[] { BanditKitKeys.ArrowRain }, 6.0, WeightWarned, BanditCallKind.Bunch, aimsAtCrowd: true),
        new(10, new[] { BanditKitKeys.DestructiveSplit }, new[] { WeakBanditKitKeys.BlazingEcho }, 2.0, WeightFocus, BanditCallKind.Bunch),
        new(11, new[] { BanditKitKeys.AnkleBreak }, new[] { BanditKitKeys.SnipingShot }, 2.0, WeightFocus, BanditCallKind.None, waitsForCall: false, alone: true),
        new(12, new[] { GroupAiWaterRules.GlacierDrop }, new[] { WeakBanditKitKeys.Incinerate }, 2.0, WeightHard, BanditCallKind.Hold),
    };

    // The class pairs a combo needs, for the weak groups' roll: a class that pairs with one already picked weighs a quarter more.
    internal const int PairWeight = 125;
    internal const int PlainWeight = 100;
    private static readonly (string A, string B)[] Pairs =
    {
        ("Guard", "Blade"), ("Sword", "Spear"), ("Spear", "FireMage"), ("Spear", "Archer"), ("Guard", "FireMage"), ("Guard", "Archer"), ("Blade", "FireMage"), ("WaterMage", "FireMage"),
    };

    internal static bool IsPair(string a, string b) => Array.Exists(Pairs, p => (p.A == a && p.B == b) || (p.A == b && p.B == a));

    internal static int PickWeight(string candidate, IEnumerable<string> picked)
        => picked.Any(other => IsPair(candidate, other)) ? PairWeight : PlainWeight;

    private static readonly Dictionary<string, BanditComboRow[]> byStarter = RowsByStarter();
    private static readonly HashSet<string> follows = AllFollows();

    private static Dictionary<string, BanditComboRow[]> RowsByStarter()
    {
        var map = new Dictionary<string, BanditComboRow[]>(StringComparer.Ordinal);
        foreach (var row in Rows)
            foreach (var key in row.Starters)
                map[key] = map.TryGetValue(key, out var rows) ? rows.Append(row).ToArray() : new[] { row };
        return map;
    }

    private static HashSet<string> AllFollows()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Rows) keys.UnionWith(row.Follows);
        return keys;
    }

    internal static BanditComboRow[] Of(string skill) => byStarter.TryGetValue(skill ?? "", out var rows) ? rows : Array.Empty<BanditComboRow>();

    internal static bool IsFollow(string skill) => follows.Contains(skill ?? "");

    internal static bool IsFollowChoice(string rule) => (rule ?? "").StartsWith(RulePrefix, StringComparison.Ordinal);

    internal static bool Rolls(double roll, double weight, float tendency) => roll < weight * tendency;

    internal static bool Fresh(double now, double reported) => now >= reported && now - reported <= ReportSeconds;

    internal static bool Reaches(int distance, int range, int walk) => GroupAiRules.InRangeExact(Math.Max(0, distance - walk), range);

    internal static bool Calls(BanditComboRow row, bool callsOn) => callsOn && row.Call != BanditCallKind.None;

    internal static bool MayStart(BanditComboRow row, bool callsOn) => callsOn || row.Call == BanditCallKind.None;

    internal static double HitFrom(double now, BanditComboRow row, bool callsOn) => Calls(row, callsOn) && row.WaitsForCall ? now + CallDelay : now;

    internal static bool MayCall(double now, double? last) => last is not double at || now < at || now - at >= CallThrottle;

    // The grace in seconds from its setting, 0 (off, as before) to 10; missing or unreadable: GraceDefault.
    internal static double Grace(double? setting) => setting is double value && !double.IsNaN(value) ? Math.Clamp(value, 0.0, 10.0) : GraceDefault;

    // When a skill can go: its own cooldown, a global cooldown and what the bandit's last action still binds, the latest of them.
    internal static double ReadyAt(double now, double cooldownLeft, double globalCooldownLeft, double busyLeft)
        => now + Math.Max(0.0, Math.Max(cooldownLeft, Math.Max(globalCooldownLeft, busyLeft)));

    // From the start to the follow-up's hit: the call's delay when the hit waits for it.
    internal static double HitDelay(BanditComboRow row, bool callsOn) => HitFrom(0.0, row, callsOn);

    // The combo's moment: the start once both are ready for it, the follow-up coming HitDelay after the start.
    internal static double ComboAt(double startReadyAt, double followReadyAt, double hitDelay) => Math.Max(startReadyAt, followReadyAt - hitDelay);

    // Whether the two overlap within the grace from now; a grace of 0 reserves nothing.
    internal static bool Overlaps(double now, double comboAt, double grace) => grace > 0.0 && comboAt - now <= grace;

    // The reservation's end, set at `at`: the grace and the hit's delay, then both act as before.
    internal static double HoldEnds(double at, double grace, double hitDelay) => at + grace + hitDelay;

    // What a release reports: the time held, counted at most to the reservation's end, and how late after the end the release came
    // (a reservation is let go in a decision).
    internal static (double Held, double Late) HeldOf(double at, double ends, double now) => (Math.Max(0.0, Math.Min(now, ends) - at), Math.Max(0.0, now - ends));

    // A taunt on another figure than the combo's target ends a reservation on either side and keeps a partner out of a new one: the
    // taunted bandit turns to its taunter (a taunted starter stood up to BasicBusySeconds for a combo it could no longer start).
    internal static bool TauntBreaks(string taunter, string target) => taunter != null && taunter != target;

    // The starter's reserved skill goes out without its combo (a started combo or a wait lets it go or holds it first).
    internal const string SpentReason = "spent";
    // The starter reserved another combo; its first reservation goes for both.
    internal const string ReplacedReason = "replaced";
    internal static bool Spent(string chosen, string startSkill, bool starter) => starter && !string.IsNullOrEmpty(chosen) && chosen == startSkill;

    // A reservation past its end: the grace and the hit's delay are over (HoldEnds).
    internal static bool Lapsed(double now, double ends) => now > ends;

    // A partner's report counts for a combo: with a grace as old as the grace, else as fresh as ReportSeconds (as before).
    internal static bool Counts(double now, double reported, double grace) => grace > 0.0 ? now >= reported && now - reported <= grace : Fresh(now, reported);

    // When a partner's follow-up is ready for the combo, the same for the start and the reservation: with a grace the later of its
    // cooldown and what the partner's last action binds; without one only a follow-up free at the report (as before), at once.
    internal static double FollowReady(double readyAt, double busyUntil, double reported, double grace)
        => grace > 0.0 ? Math.Max(readyAt, busyUntil) : readyAt <= reported ? reported : double.PositiveInfinity;

    internal const string PlanStart = "start";
    internal const string PlanReserve = "reserve";
    internal const string PlanWait = "wait";

    // What the starter's chosen skill does with the partner's moment: start now when the follow-up is ready by its hit; with the
    // starter's own reservation wait while the combo still fits into it (ends); without one reserve when it lies within the grace;
    // else null.
    internal static string StartPlan(double now, double comboAt, double grace, double? ends, double hitDelay)
        => comboAt <= now ? PlanStart
            : ends is double end ? (comboAt + hitDelay <= end ? PlanWait : null)
            : Overlaps(now, comboAt, grace) ? PlanReserve : null;

    // What an action binds the bandit: nothing for a move, else a skill's or the basic attack's time.
    internal static double Busy(bool skill, bool moving) => moving ? 0.0 : skill ? SkillBusySeconds : BasicBusySeconds;

    // Whether another action may go while a reservation waits for `moment`: it ends before, or the moment has passed (then only
    // the reserved skill waits, so the bandit does not stand).
    internal static bool Allowed(double now, double busy, double moment) => now >= moment || now + busy <= moment;

    // The follow-up's window: the combo's own, and at least a grace after its hit begins.
    internal static double OpenUntil(double now, double hitFrom, double window, double grace) => Math.Max(now + window, hitFrom + grace);

    // A lapse after the combo's own window ran out names the grace that kept it open longer.
    internal static string LapseReason(double windowEnd, double until) => until > windowEnd ? "grace" : "window";

    internal enum Stage { Waiting, Open, Closed }

    internal static Stage StageOf(double now, double hitFrom, double until) => now > until ? Stage.Closed : now < hitFrom ? Stage.Waiting : Stage.Open;

    // The foe with the most others within the area's Manhattan radius, the first of equals; -1 for none.
    internal static int MiddleOf(IReadOnlyList<(int X, int Y)> cells, int radius)
    {
        var best = -1;
        var bestCount = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            var count = 0;
            for (var j = 0; j < cells.Count; j++)
                if (GroupAiRules.Manhattan(cells[i].X, cells[i].Y, cells[j].X, cells[j].Y) <= radius) count++;
            if (count > bestCount)
            {
                best = i;
                bestCount = count;
            }
        }
        return best;
    }

    internal static string StartLine(int number, string starter, string partner, string target, double window, bool called, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO combo={0} starter={1} partner={2} target={3} window={4:0.0} called={5} tick={6}",
            number, starter, partner, target, window, called ? "yes" : "no", tick);

    internal static string FollowLine(int number, string unit, string skill, string target, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_FOLLOW combo={0} unit={1} skill={2} target={3} tick={4}", number, unit, skill, target, tick);

    internal static string ReserveLine(int number, string unit, string part, string skill, string other, string target, double inSeconds, double grace, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_RESERVE combo={0} unit={1} part={2} skill={3} other={4} target={5} in={6:0.00} grace={7:0.0} tick={8}",
            number, unit, part, skill, other, target, inSeconds, grace, tick);

    internal static string ReleaseLine(int number, string unit, string reason, double held, double late, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_RELEASE combo={0} unit={1} reason={2} held={3:0.00} late={4:0.00} tick={5}",
            number, unit, reason, held, late, tick);

    internal static string HoldbackLine(string unit, string skill, double busy, double inSeconds, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_HOLDBACK unit={0} skill={1} busy={2:0.0} in={3:0.00} tick={4}", unit, skill, busy, inSeconds, tick);

    internal static string NoStartLine(int number, string unit, string reason, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_NOSTART combo={0} unit={1} reason={2} tick={3}", number, unit, reason, tick);

    internal static string LapseLine(int number, string unit, string reason, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_COMBO_LAPSE combo={0} unit={1} reason={2} tick={3}", number, unit, reason, tick);
}
