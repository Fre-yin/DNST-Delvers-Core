namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the bandits' rules: fallen player units, the chase reach, waking the camp, areas that hit a
// fallen unit, the guard's slowing ring, the test campaign's basic attack only switch and the warning for a
// global cooldown on an enemy.
internal static partial class GroupAiRules
{
    // ---- fallen player units (stage 2 spec, appendix B) ----

    internal const string FinishDownedAlwaysKey = "bandits.finish-downed-always";

    internal const string ChaseCellsKey = "bandits.chase-cells";
    internal const int ChaseCellsDefault = 8;

    internal static int ChaseCells(double? setting) => (int)Math.Clamp(Math.Round(setting ?? ChaseCellsDefault), 1.0, 30.0);

    // Why fallen units may be attacked now, or null while a capable one stands within the chase reach:
    // all-down (no capable player unit left) or fled (none within reach).
    internal static string FinishReason(int capableTotal, int capableInReach)
        => capableTotal <= 0 ? "all-down" : capableInReach <= 0 ? "fled" : null;

    // Between decisions too, a bandit whose combat target has fallen turns away while a capable
    // one stands within reach (unless fallen units may always be finished).
    internal static bool SkipsFallenNow(bool targetFallen, bool finishAlways, int capableTotal, int capableInReach)
        => targetFallen && !finishAlways && FinishReason(capableTotal, capableInReach) == null;

    // Whom the per-tick net turns a bandit to from a fallen target: its focus when that one is capable, else the
    // nearest capable one, as RetargetFromDowned does between decisions (the net took the
    // nearest while the basic attack went to the focus). capable is ordered nearest first; null when empty.
    // b' (SKIP_DOWNED to=Frieren, then guard-basic on Korgas): a bandit's basic
    // attack goes to its focus, else its current combat target while that one is still a capable hostile (the one the
    // per-tick skip just set), else the nearest; so SKIP_DOWNED to= and the following attack agree, a tie keeps the old.
    internal static string BasicTargetPick(string focus, string current, bool currentCapable, string closest)
        => focus ?? (current != null && currentCapable ? current : closest);

    internal static string SkipTargetPick(string focus, IReadOnlyList<string> capable)
    {
        if (capable == null || capable.Count == 0) return null;
        if (focus != null)
            for (var i = 0; i < capable.Count; i++)
                if (capable[i] == focus) return focus;
        return capable[0];
    }

    // What a bandit does with the game's combat target at a decision (GroupAiBandits.RetargetFromDowned): keep it,
    // note a finish, or turn away from a unit excluded for tests or from a fallen one (the
    // guard without a focus took the adjacent dummy as its combat target).
    internal static string CombatTargetAction(bool excluded, bool fallen, bool finishAllowed)
        => excluded ? "skip-excluded" : !fallen ? "keep" : finishAllowed ? "finish" : "skip-downed";

    // (12,313 AlertDTO exceptions from a bandit on the dummy): a bandit's target
    // excluded for tests is replaced by another player unit, or dropped when there is none (the bandit holds).
    internal static string ExcludedTargetAction(bool excluded, bool hasOther) => !excluded ? "keep" : hasOther ? "replace" : "drop";

    // The guard's slowing ring counts a player unit in its reach only while it can still fight (after a wipe it showed
    // targets=4 and slowed nobody).
    internal static bool SlowRingCounts(bool player, bool downed, int manhattan)
        => player && !downed && manhattan <= SlowRingRadius;

    // ---- (bound aggro, one attacks and the whole camp wakes) ----

    internal const string GroupWakeKey = "bandits.groupWake";
    internal const int GroupWakeCells = 12;

    // (asked): the woken bandits start a little apart, so not everything falls in one tick. Each
    // bandit Core wakes (group wake, camp wake) enters combat after its own random delay of game time; the bandit hit first stays
    // the game's and reacts at once. r is a uniform draw in [0, 1].
    internal const float WakeDelayMin = 0.1f;
    internal const float WakeDelayMax = 0.3f;

    internal static float WakeDelay(double r)
        => WakeDelayMin + (WakeDelayMax - WakeDelayMin) * (float)(double.IsNaN(r) ? 0.0 : Math.Min(1.0, Math.Max(0.0, r)));

    internal static bool WakeDue(float due, float now) => now >= due;

    // P4 (weak bandits, several groups on one map): a bandit's allies are the bandits within GroupWakeCells, the reach of its own
    // camp; a group in another room is no ally (no protection, no heals planned across the map).
    internal static bool IsGroupAlly(int distance) => distance <= GroupWakeCells;

    // Whom a bandit entering combat wakes: living, not fallen comrades within GroupWakeCells that are not fighting yet.
    // A player's taunt is an attack too and wakes the camp. candidates: (id, distance, awake, fallen).
    internal static List<string> WakeGroup(IReadOnlyList<(string Guid, int Distance, bool Awake, bool Fallen)> candidates)
    {
        var woken = new List<string>();
        if (candidates == null) return woken;
        foreach (var (guid, distance, awake, fallen) in candidates)
            if (!awake && !fallen && distance <= GroupWakeCells) woken.Add(guid);
        return woken;
    }

    // A skill whose area hits a fallen unit is not taken
    // while fallen units are skipped and another skill is possible (the basic attack always is); without one it stays.
    internal static bool AvoidsDownedArea(int downedHit, bool finishAllowed, bool alternative)
        => downedHit > 0 && !finishAllowed && alternative;

    // ---- the guard's slowing ring (BanditSlowRing) ----

    internal const string SlowRingSkill = "SKILL_GuardiansTaunt";
    internal const string SlowKey = "AFFECTER_Slow";
    // The skill row's AreaType 3 (ExecutionAreaType.Manhattan) and AreaRadius; for the decision only, the
    // skill itself finds its targets.
    internal const int SlowRingRadius = 5;
    internal const string SlowSecondsKey = "bandits.guard.slow-seconds";
    internal const string SlowMinTargetsKey = "bandits.guard.slow-min-targets";
    internal const double SlowSecondsDefault = 4.0;
    internal const int SlowMinTargetsDefault = 2;

    internal static double SlowSeconds(double? setting) => Math.Clamp(setting ?? SlowSecondsDefault, 0.5, 30.0);

    internal static int SlowMinTargets(double? setting) => (int)Math.Clamp(Math.Round(setting ?? SlowMinTargetsDefault), 1.0, 6.0);

    internal static int Manhattan(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);

    // Why the guard rings now, or null: the skill is free and a player hitting the healer stands in the
    // ring (protect), or at least minTargets players do (group).
    internal static string SlowRingReason(bool free, int playersInRing, int minTargets, bool healerAttackerInRing)
        => !free ? null : healerAttackerInRing ? "protect" : playersInRing >= minTargets ? "group" : null;

    // ---- the basic attack only, for a test campaign ----

    // Read at the campaign's start (GroupAiBandits.Reset); off by default, so every bandit decides as before.
    internal const string BasicOnlyKey = "bandits.basic-only";
    internal const string BasicOnlyRule = "bandit-basic-only";
    internal const string BasicOnlyStartLine = "CORE_AI_BANDIT_START basic=1";

    // The switch works only in a marked test campaign; in any other campaign the setting does nothing.
    internal static bool BasicOnlyActive(bool setting, bool testCampaign) => setting && testCampaign;

    // With the switch active a bandit's decision keeps no skill: the basic attack (an empty skill) is the only choice left.
    internal static bool DropsSkill(bool basicOnly, string skill) => basicOnly && !string.IsNullOrEmpty(skill);

    // ---- a global cooldown on an enemy ----

    // The game gives bandits no global cooldown, so their decisions ignore it. A decision that still reports one
    // (gcd=1) means a game update changed that; the warning is written once per campaign start and decides nothing. It names
    // no behaviour, only that the removed branch would have been reached.
    internal const string GcdSeenLine = "CORE_AI_BANDIT_GCD_SEEN note=gcd-branch-reached";

    internal static bool GcdSeenDue(bool globalCooldown, bool alreadyWritten) => globalCooldown && !alreadyWritten;
}
