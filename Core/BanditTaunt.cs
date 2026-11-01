using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, taunts and the per-tick watch: bandits known from their decisions are looked at every game tick
// (OnGameTick); the taunt starts and ends on time, the healer's share is sampled for breaking free.
internal static partial class GroupAiBandits
{
    // Taunt (GroupAiRules.LookAtTaunt): per bandit its taunt, the healer's recent health shares (game
    // seconds), the settings read at the campaign's start, and how many taunts the bandits heeded.
    // Bandits known from their decisions are looked at every game tick (OnGameTick).
    private sealed class Watch
    {
        internal UnitEntity Unit;
        internal Kit Kit;
        internal UnitEntity Healer;
        internal string HealerGuid;
        // The bandit's focus at its last decision, taunter included (Build); the per-tick net turns there first.
        internal string FocusGuid;
    }

    private static readonly Dictionary<string, GroupAiRules.TauntTrack> taunts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Watch> watched = new(StringComparer.Ordinal);

    private static readonly HashSet<string> sampled = new(StringComparer.Ordinal);
    private static long lastWatchTick = -1;

    private static bool tickHookReported;

    private static readonly Dictionary<string, List<(float At, float Share)>> healerShares = new(StringComparer.Ordinal);
    private static double tauntFactor = GroupAiRules.TauntFactorDefault;
    private static double tauntBreakDrop = GroupAiRules.TauntBreakDropDefault;
    private static double tauntBreakBelow = GroupAiRules.TauntBreakBelowDefault;
    private static int taunted;

    // The healer's health share over the last game seconds, for the hard drop
    // (GroupAiRules.HealerDropReason); at most one sample per game tick.
    private static void NoteHealerShare(string healer, float share, double now)
    {
        if (!healerShares.TryGetValue(healer, out var shares)) healerShares[healer] = shares = new List<(float At, float Share)>();
        var at = (float)now;
        shares.RemoveAll(sample => at - sample.At > GroupAiRules.TauntBreakWindowSeconds);
        if (shares.Count > 0 && shares[shares.Count - 1].At == at) shares[shares.Count - 1] = (at, share);
        else shares.Add((at, share));
    }

    // below, loss or null (GroupAiRules.HealerDropReason).
    // breakDrop: the taunted bandit's own loss threshold (a weak bandit's, WeakBanditRules), else the setting.
    private static string HealerDrop(string healer, float share, double now, double? breakDrop = null)
    {
        if (healer == null || !healerShares.TryGetValue(healer, out var shares)) return null;
        return GroupAiRules.HealerDropReason(shares, (float)now, GroupAiRules.TauntBreakWindowSeconds, breakDrop ?? tauntBreakDrop, tauntBreakBelow, share);
    }

    // How long and how firmly a bandit heeds a player's taunt: a weak bandit's own values (WeakBanditRules), else the settings.
    private static (double Factor, double? BreakDrop) TauntOf(UnitEntity unit)
        => unit != null && CoreUnitRules.TryGet(unit.Key, out var d) && d.TauntFactor is double f ? (f, d.TauntBreakDrop) : (tauntFactor, null);

    // (two of three counter taunts ended in their first tick): whether a taunt on this bandit
    // would end at once because its healer fell hard (healer_drop); the bandits' rule itself stays.
    internal static bool TauntWouldBreak(UnitEntity bandit)
    {
        if (bandit == null || !watched.TryGetValue(bandit.Guid.ToString(), out var w) || w.Kit == Kit.Healer) return false;
        if (w.Healer == null || w.Healer.IsDead) return false;
        return HealerDrop(w.HealerGuid, GroupAiSituation.ShareOf(w.Healer), Tick() / WoundRules.TicksPerSecond, TauntOf(bandit).BreakDrop) != null;
    }

    // (under Darius' taunt the guard pulled Pamela 0.85 s later; the pull was decided before
    // the taunt and its cast was already running): when a foreign taunt starts, a running action aimed at anyone but the
    // taunter is dropped, so a taunted bandit casts nothing at another unit (as archer-ankle and the cone checks).
    // The chain's own-pull note goes with it. CORE_AI_BANDIT_TAUNT_CANCEL.
    private static void CancelForeignAction(string guid, UnitEntity unit, CombatInfoComponent info, string taunter)
    {
        var aim = info?.GetCombatBehaviour()?.TargetEntity?.Guid.ToString();
        if (!GroupAiRules.TauntCancels(aim, taunter, unit.Guid.ToString())) return;
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return;
        CoreGame.TickHandler(map)?._taskSystem?._performers?.GetEntityBrain(unit.Cast<IEntity>())?.InterruptActions(false, false, true);
        ownPulls.Remove(guid);
        pulled.Remove(guid);
        Log(GroupAiRules.TauntCancelLine(guid, taunter, aim, Tick()));
    }

    // The player unit whose taunt the bandit heeds now: the game's forced target (CombatInfoComponent,
    // set by AFFECTER_Taunt), as long as the bandit has not broken free.
    private static Figure TaunterOf(Situation s, UnitEntity unit, Kit kit, CombatInfoComponent info, double now)
    {
        var track = LookAtTaunt(s.Guid, unit, kit, info, s.Healer?.Guid, s.Healer?.Share ?? 1f, now);
        if (track == null) return null;
        foreach (var hostile in s.Hostiles)
            if (hostile.Guid == track.By) return hostile;
        return null;
    }

    // Every game tick (postfix of TickFlowHandler.Tick; once per tick however many maps tick): the taunt
    // of every bandit known from its decisions starts and ends on time, not only at its next decision,
    // and the healer's share is sampled. Nothing runs before a bandit has decided once.
    internal static void OnGameTick()
    {
        // Wakes queued by a first hit or a taunt run here, outside the game's applier.
        BanditGroupWake.Flush();
        if (watched.Count == 0) return;
        var tick = Tick();
        if (tick == lastWatchTick) return;
        lastWatchTick = tick;
        if (!tickHookReported)
        {
            tickHookReported = true;
            Log("CORE_AI_BANDIT_TICK_HOOK watched=" + watched.Count.ToString(CultureInfo.InvariantCulture) + " tick=" + tick.ToString(CultureInfo.InvariantCulture));
        }
        var now = tick / WoundRules.TicksPerSecond;
        sampled.Clear();
        List<string> gone = null;
        foreach (var pair in watched)
        {
            var w = pair.Value;
            try
            {
                if (w.Unit == null || w.Unit.IsDead)
                {
                    (gone ??= new List<string>()).Add(pair.Key);
                    continue;
                }
                var healerAlive = w.Healer != null && !w.Healer.IsDead;
                var healerShare = healerAlive ? GroupAiSituation.ShareOf(w.Healer) : 1f;
                if (healerAlive && sampled.Add(w.HealerGuid)) NoteHealerShare(w.HealerGuid, healerShare, now);
                var info = CoreGame.Component<CombatInfoComponent>(w.Unit.Cast<IEntity>());
                if (info == null)
                {
                    (gone ??= new List<string>()).Add(pair.Key);
                    continue;
                }
                LookAtTaunt(pair.Key, w.Unit, w.Kit, info, healerAlive ? w.HealerGuid : null, healerShare, now);
                SkipFallenTarget(pair.Key, w.Unit, info, w.FocusGuid, tick);
                if (spareDownedInCone) ConeGuardTick(pair.Key, w.Unit, info, w.FocusGuid, tick);
                SkipExcludedTarget(pair.Key, w.Unit, info, w.FocusGuid, tick);
                CheckHealPath(pair.Key, w.Unit, tick);
            }
            catch (Exception ex)
            {
                (gone ??= new List<string>()).Add(pair.Key);
                Warn("CORE_AI_BANDIT_TICK_FAILED unit=" + pair.Key + ": " + ex.Message);
            }
        }
        if (gone != null)
            foreach (var key in gone) watched.Remove(key);
    }

    // One look at a bandit's taunt (GroupAiRules.LookAtTaunt): logs its start and end and takes the
    // game's taunt off as soon as Core's time is up. The track the bandit heeds now, else null.
    private static GroupAiRules.TauntTrack LookAtTaunt(string guid, UnitEntity unit, Kit kit, CombatInfoComponent info, string healerGuid, float healerShare, double now)
    {
        var forced = info?.GetForceAttackTarget();
        var by = forced?.Guid.ToString();
        var forcedDowned = !finishDownedAlways && forced != null && IsDowned(forced.TryCast<UnitEntity>());
        taunts.TryGetValue(guid, out var track);
        var heed = TauntOf(unit);
        var drop = kit == Kit.Healer || by == null ? null : HealerDrop(healerGuid, healerShare, now, heed.BreakDrop);
        var ownPull = ownPulls.TryGetValue(guid, out var pull) && GroupAiRules.IsOwnPull(by, pull.Target, pull.Until, now);
        var look = GroupAiRules.LookAtTaunt(track, by, now, () => NativeTauntSeconds(unit), heed.Factor, kit == Kit.Healer, drop, ownPull, forcedDowned);
        // The end of the taunt another forced target replaced comes first.
        if (look.Replaced != null) Log(GroupAiRules.TauntEndLine(guid, look.Replaced, "replaced", null, Tick()));
        if (look.Track == null) taunts.Remove(guid);
        else taunts[guid] = look.Track;
        if (look.Started)
        {
            taunted++;
            Log(GroupAiRules.TauntedLine(guid, look.Track.By, look.Track.NativeEnd - now, heed.Factor, look.Track.Until - now, Tick()));
            CancelForeignAction(guid, unit, info, look.Track.By);
        }
        if (look.Own) Log(GroupAiRules.TauntOwnLine(guid, look.Track.By, Tick()));
        if (look.Release) Release(unit, info);
        if (look.End != null)
            Log(GroupAiRules.TauntEndLine(guid, (look.Track ?? track)?.By ?? "-", look.End, look.End == "healer_drop" ? drop : null, Tick()));
        return look.Track == null || look.Track.Ended || look.Track.Own ? null : look.Track;
    }

    // The taunt's full duration on the bandit; the shield's Guardian's Taunt when the game shows none.
    private static double NativeTauntSeconds(UnitEntity unit)
    {
        try
        {
            var affecters = CoreGame.Affecters(unit);
            var initial = affecters?.GetInitialDuration(GroupAiRules.TauntKey) ?? 0f;
            if (initial > 0f) return initial;
            var left = affecters?.GetDuration(GroupAiRules.TauntKey) ?? 0f;
            if (left > 0f) return left;
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_TAUNT_DURATION_FAILED: " + ex.Message);
        }
        return GroupAiRules.TauntFallbackSeconds;
    }

    // Breaking free: the game's forced target goes, and the taunt effect is taken off.
    private static void Release(UnitEntity unit, CombatInfoComponent info)
    {
        CoreTargets.SetForced(unit, info, null);
        if (CoreGame.Affecters(unit)?.Has(GroupAiRules.TauntKey) == true && CoreGame.TryMapOfUnit(unit, out var map))
            CoreGame.Apply(map, new AffecterApplyData(unit.Cast<IEntity>(), GroupAiRules.TauntKey, -9999, 0f, false).Cast<IApplyData>());
    }
}
