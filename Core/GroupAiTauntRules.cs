using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// GroupAiRules, the taunt of the forest bandits: how long a bandit heeds it, when it breaks free, the chain's
// own taunt, and what a taunted bandit may still do.
internal static partial class GroupAiRules
{
    // ---- taunt of the forest bandits: a boss heeds it, but breaks free early ----

    internal const string TauntKey = "AFFECTER_Taunt";
    internal const string TauntFactorKey = "bandits.taunt.duration-factor";
    internal const string TauntBreakDropKey = "bandits.taunt.break-healer-drop";
    internal const string TauntBreakBelowKey = "bandits.taunt.break-healer-below";
    internal const double TauntFactorDefault = 0.5;
    internal const double TauntBreakDropDefault = 0.2;
    internal const double TauntBreakBelowDefault = 0.4;
    internal const float TauntBreakWindowSeconds = 3f;
    // When the game's taunt shows no duration: the shield's Guardian's Taunt (6 s).
    internal const double TauntFallbackSeconds = 6.0;

    // How long a bandit heeds a taunt of the given native duration.
    internal static double TauntSeconds(double nativeSeconds, double factor)
        => Math.Max(0.0, nativeSeconds) * Math.Clamp(factor, 0.0, 1.0);

    // Why a bandit stops heeding its taunt now, or null: the healer falling hard frees the others at
    // once (healer_drop), else the bandit's own time is up (early before the game's end, expired at it).
    internal static string TauntEndCause(double now, double until, double nativeEnd, bool isHealer, bool healerFellHard)
    {
        if (!isHealer && healerFellHard) return "healer_drop";
        if (now >= until) return until < nativeEnd ? "early" : "expired";
        return null;
    }

    // A bandit's taunt as Core tracks it, in game seconds.
    internal sealed class TauntTrack
    {
        internal string By = "";
        internal double Until;
        internal double NativeEnd;
        internal bool Ended;
        // The bandit's own taunt from its chain: recognised, left to the game.
        internal bool Own;
    }

    // The chain (SKILL_ChainGrap) taunts the guard itself onto the unit
    // it pulled, for 2 s, as the game means it. Such an own taunt is no player taunt and Core leaves it
    // alone: a taunt counts as own when it starts on the unit the bandit's own chain was cast at within
    // OwnPullSeconds (cast time, pull and the taunt's start).
    internal const double OwnPullSeconds = 6.0;

    internal static bool IsOwnPull(string forcedBy, string pullTarget, double pullUntil, double now)
        => !string.IsNullOrEmpty(forcedBy) && forcedBy == pullTarget && now <= pullUntil;

    // One look at a bandit's taunt: the track to keep (null: none), whether a taunt just started,
    // whether the game's taunt is to be taken off (Core's time is up while the game still forces the
    // target), and why it ended now (TauntEndCause; "expired" also when the game's taunt went first).
    internal readonly struct TauntLook
    {
        internal readonly TauntTrack Track;
        internal readonly bool Started;
        internal readonly bool Release;
        internal readonly string End;
        // An own taunt (IsOwnPull) was recognised in this look.
        internal readonly bool Own;
        // The taunter of a running track another forced target replaced in this look (its end, cause replaced).
        internal readonly string Replaced;

        internal TauntLook(TauntTrack track, bool started, bool release, string end, bool own = false, string replaced = null)
        {
            Replaced = replaced;
            Track = track;
            Started = started;
            Release = release;
            End = end;
            Own = own;
        }
    }

    // ("bricht früher ab", "reißt sich sofort los"; the end came 1 to
    // 3 s late at the next decision): one look per game tick and at each decision. forcedBy: the game's
    // forced target, null when none; native: the taunt's full duration in game seconds, read only when a
    // taunt starts; drop: why the healer fell hard (HealerDropReason), null when she did not.
    // ownPull: the forced target is the unit the bandit's own chain was just cast at (IsOwnPull); such a
    // taunt is kept as an own track and never ended or shortened by Core.
    internal static TauntLook LookAtTaunt(TauntTrack track, string forcedBy, double now, Func<double> native, double factor, bool isHealer, string drop,
        bool ownPull = false, bool forcedDowned = false)
    {
        if (string.IsNullOrEmpty(forcedBy))
            return new TauntLook(null, false, false, track != null && !track.Ended && !track.Own ? "expired" : null);
        var started = false;
        // (134505: the guard's own chain replaced Darius' taunt and no end was logged): a running foreign
        // track that another forced target replaces ends with cause replaced.
        var replaced = track != null && !track.Ended && !track.Own && track.By != forcedBy ? track.By : null;
        if (track == null || track.By != forcedBy || (track.Ended && now >= track.NativeEnd))
        {
            if (ownPull && !forcedDowned) return new TauntLook(new TauntTrack { By = forcedBy, Own = true }, false, false, null, own: true, replaced: replaced);
            var seconds = Math.Max(0.0, native());
            track = new TauntTrack { By = forcedBy, NativeEnd = now + seconds, Until = now + TauntSeconds(seconds, factor) };
            started = true;
        }
        // A taunt by a fallen unit ends at once, the chain's own too: bandits turn to capable ones.
        if (forcedDowned)
        {
            if (track.Ended) return new TauntLook(track, false, true, null);
            track.Ended = true;
            return new TauntLook(track, started, true, "downed", replaced: replaced);
        }
        if (track.Own) return new TauntLook(track, false, false, null);
        if (track.Ended) return new TauntLook(track, false, true, null);
        var cause = TauntEndCause(now, track.Until, track.NativeEnd, isHealer, drop != null);
        if (cause == null) return new TauntLook(track, started, false, null, replaced: replaced);
        track.Ended = true;
        return new TauntLook(track, started, true, cause, replaced: replaced);
    }

    // Why the healer falls hard, or null: below (her share is under below) or loss (in the window she
    // lost at least drop of her maximum). Shares are head plus body over the maximum (GroupAiSituation.ShareOf).
    internal static string HealerDropReason(IEnumerable<(float At, float Share)> samples, float now, float window, double drop, double below, float current)
    {
        if (current < below) return "below";
        var highest = current;
        foreach (var (at, share) in samples)
            if (now - at <= window && share > highest) highest = share;
        // Shares are floats: 0.8f - 0.6f is a hair under 0.2, which still counts as the full drop.
        return (double)highest - current + DropTolerance >= drop ? "loss" : null;
    }

    private const double DropTolerance = 1e-4;

    // What a taunted bandit may still do: no moves (formation, kite, intercept), skills only on the
    // taunter or itself; the healer may still heal her comrades.
    // A taunt that starts drops the bandit's running action when it is aimed at someone other than the taunter (or itself).
    internal static bool TauntCancels(string aim, string taunter, string self) => aim != null && aim != taunter && aim != self;

    internal static string TauntCancelLine(string unit, string taunter, string aim, long tick)
        => $"CORE_AI_BANDIT_TAUNT_CANCEL unit={unit} by={taunter} aim={aim ?? "-"} tick={tick.ToString(CultureInfo.InvariantCulture)}";

    internal static bool TauntAllows(bool healer, string action) => action switch
    {
        "attack-taunter" => true,
        "self" => true,
        "heal-ally" => healer,
        _ => false
    };

    // The healer with a hostile next to her (GroupAiBandits.Guarded): "run" (the game's flee skill), "basic" (a
    // basic attack on that neighbour) or "hold". Taunted she does not run and attacks only the taunter
    // (she heals on but does not run; healer-run under taunt).
    internal static string GuardedAction(bool taunted, bool neighbourIsTaunter, bool runReady)
    {
        if (!taunted) return runReady ? "run" : "basic";
        if (runReady && TauntAllows(true, "move")) return "run";
        return neighbourIsTaunter && TauntAllows(true, "attack-taunter") ? "basic" : "hold";
    }
}
