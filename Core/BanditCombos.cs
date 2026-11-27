namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the combo board (BanditComboRules): the partners' reports, the open combos by partner, the hook behind every
// decision that may start one and the follow-up before the kit's own choice. Off with bandits.vary, nothing stands on the board.
// The grace (bandits.combo.grace): a starter whose kit chose its skill while the partner's follow-up gets ready within it waits and
// reserves both for the combo (StartCombo); while it stands they hold those skills and leave out what would still bind them at the
// combo's moment (HoldCombo).
// Logs: CORE_BANDIT_COMBO, _FOLLOW, _LAPSE, _RESERVE, _RELEASE, _HOLDBACK, _NOSTART.
internal static partial class GroupAiBandits
{
    private sealed class OpenCombo
    {
        internal BanditComboRow Row;
        internal string Target = "";
        internal double HitFrom;
        internal double Until;
        // The combo's own window; Until goes past it by the grace (BanditComboRules.OpenUntil).
        internal double WindowEnd;
    }

    // A bandit's follow-ups as of its last decision, free now or when their cooldown ends, and until when its last action binds it.
    // A skill it chose itself goes out of the report until its next decision.
    private sealed class ComboReport
    {
        internal double At;
        internal UnityEngine.Vector2Int Position;
        internal double BusyUntil;
        // The taunter it heeds at its last decision, null for none: a taunted bandit takes no part in a new combo.
        internal string Taunter;
        internal readonly List<(string Key, int Range, double ReadyAt)> Skills = new();
    }

    // Two bandits holding their skills for one combo (BanditComboRules.Overlaps).
    private sealed class ComboReservation
    {
        internal BanditComboRow Row;
        internal string Starter = "";
        internal string Partner = "";
        internal string StartSkill = "";
        internal string FollowSkill = "";
        internal string Target = "";
        internal double At;
        internal double ComboAt;
        internal double HitDelay;
        internal double Ends;
    }

    private static readonly Dictionary<string, OpenCombo> comboBoard = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ComboReport> comboReports = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> comboCalledAt = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> comboRolledAt = new(StringComparer.Ordinal);
    // Both sides of every reservation, by bandit.
    private static readonly Dictionary<string, ComboReservation> reservations = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> holdbackLogged = new(StringComparer.Ordinal);
    private static int noStartLines;

    private static void ResetCombos()
    {
        comboBoard.Clear();
        comboReports.Clear();
        comboCalledAt.Clear();
        comboRolledAt.Clear();
        reservations.Clear();
        holdbackLogged.Clear();
        noStartLines = 0;
        BanditComboCall.Reset();
    }

    // Every decision: this bandit's follow-ups, when each can go, and where it stands.
    private static void ReportCombo(Situation s)
    {
        if (!CoreSettings.BanditsVary) return;
        if (!comboReports.TryGetValue(s.Guid, out var report))
        {
            if (comboReports.Count >= BanditComboRules.BoardCap) comboReports.Clear();
            comboReports[s.Guid] = report = new ComboReport();
        }
        var now = GameNow();
        report.At = now;
        report.Position = s.Self.Position;
        report.Taunter = s.Taunter?.Guid;
        report.Skills.Clear();
        // The bandit decides now, so its last action binds it no longer; a running global cooldown counts (the game gives bandits
        // none today, BanditSituation reads it only while one runs).
        var gcd = s.GlobalCooldownLeft;
        foreach (var slot in s.Slots)
        {
            if (slot.Data == null || !BanditComboRules.IsFollow(slot.Key)) continue;
            if (slot.Free) report.Skills.Add((slot.Key, slot.Data.Range, BanditComboRules.ReadyAt(now, 0.0, gcd, 0.0)));
            // One in its cooldown only when the game would let it go after it (energy, target), like a free one.
            else if (slot.State == "cd" && UsableAfterCooldown(s, slot)) report.Skills.Add((slot.Key, slot.Data.Range, BanditComboRules.ReadyAt(now, slot.CooldownLeft, gcd, 0.0)));
        }
    }

    // After the decision: a starter whose reserved skill goes out without its combo (no partner, out of reach, a call held back)
    // lets the reservation go (reason=spent), so the partner no longer holds its follow-up for it; what the chosen action binds
    // (nothing while it stands for its combo), and a combo skill it spends leaves the report.
    private static void NoteComboChoice(Situation s, Choice choice)
    {
        var now = GameNow();
        if (reservations.TryGetValue(s.Guid, out var r) && BanditComboRules.Spent(choice.Skill, r.StartSkill, r.Starter == s.Guid))
            Release(r, BanditComboRules.SpentReason, now);
        if (!CoreSettings.BanditsVary || !comboReports.TryGetValue(s.Guid, out var report)) return;
        report.BusyUntil = now + BanditComboRules.Busy(choice.Skill.Length > 0, choice.Rule == BanditComboRules.HoldRule || GroupAiMoveWatch.Holds(s.Unit));
        if (choice.Skill.Length > 0) report.Skills.RemoveAll(skill => skill.Key == choice.Skill);
    }

    // Before the kit's choice: a standing reservation is held or let go. The moment other actions are measured against, or null
    // without a reservation.
    private static double? HoldCombo(Situation s)
        => reservations.TryGetValue(s.Guid, out var held) ? HoldReservation(s, held, GameNow()) : null;

    // A reservation for both, set by the starter (StartCombo) after its roll.
    private static void Reserve(BanditComboRow row, string starter, string partner, string startSkill, string followSkill, string target,
        double comboAt, double delay, double now, double grace)
    {
        var reservation = new ComboReservation
        {
            Row = row, Starter = starter, Partner = partner, StartSkill = startSkill, FollowSkill = followSkill, Target = target,
            At = now, ComboAt = comboAt, HitDelay = delay, Ends = BanditComboRules.HoldEnds(now, grace, delay)
        };
        // A starter that reserves another combo lets its first one go, so that partner does not hold alone.
        if (reservations.TryGetValue(starter, out var before)) Release(before, BanditComboRules.ReplacedReason, now);
        if (reservations.Count >= BanditComboRules.BoardCap) reservations.Clear();
        reservations[starter] = reservations[partner] = reservation;
        var tick = Tick();
        Log(BanditComboRules.ReserveLine(row.Number, starter, "starter", startSkill, partner, target, comboAt - now, grace, tick));
        Log(BanditComboRules.ReserveLine(row.Number, partner, "partner", followSkill, starter, target, comboAt - now, grace, tick));
    }

    // The reservation goes on or ends with its reason; while it goes on the bandit's combo skill is held and a skill that would still
    // bind it at the moment is left out. The moment for this bandit, or null once it is let go.
    private static double? HoldReservation(Situation s, ComboReservation r, double now)
    {
        var starter = s.Guid == r.Starter;
        // A partner on the board follows that combo first (FollowCombo; only a combo of its own, the reserved one starts with a release
        // of both); the reservation's end still counts.
        if (!starter && comboBoard.ContainsKey(s.Guid))
        {
            if (BanditComboRules.Lapsed(now, r.Ends)) Release(r, "grace", now);
            return null;
        }
        // The other one among this bandit's allies first; the map's units only when it is not there (dead or gone).
        var otherId = starter ? r.Partner : r.Starter;
        var other = s.Allies.Find(ally => ally.Guid == otherId)?.Unit
            ?? (Guid.TryParse(otherId, out var otherGuid) ? CoreGame.FindUnit(otherGuid) : null);
        var reason = !CoreSettings.BanditsVary ? "vary"
            : BanditComboRules.Lapsed(now, r.Ends) ? "grace"
            : other == null || other.IsDead || IsDowned(other) ? "partner"
            : TargetGone(s, r.Target) ? "target"
            : BanditComboRules.TauntBreaks(s.Taunter?.Guid, r.Target) ? "taunt"
            : null;
        if (reason != null)
        {
            Release(r, reason, now);
            return null;
        }
        var own = starter ? r.StartSkill : r.FollowSkill;
        var moment = starter ? r.ComboAt : r.ComboAt + r.HitDelay;
        foreach (var slot in s.Slots)
        {
            if (!slot.Free) continue;
            if (slot.Key == own)
            {
                // The starter's skill waits until the partner's follow-up is ready for it; the follow-up waits for the start.
                if (!starter || now < r.ComboAt) slot.State = BanditComboRules.HeldState;
                continue;
            }
            if (BanditComboRules.Allowed(now, BanditComboRules.SkillBusySeconds, moment)) continue;
            slot.State = BanditComboRules.HeldBackState;
            NoteHoldback(s.Guid, slot.Key, BanditComboRules.SkillBusySeconds, moment - now);
        }
        return moment;
    }

    private static bool TargetGone(Situation s, string target)
    {
        var figure = s.Hostiles.Find(h => h.Guid == target);
        return figure == null || (s.Downed != null && s.Downed.Contains(figure));
    }

    // After the kit's choice: a basic attack that would still bind the bandit at the moment is left out; it stands and decides again
    // soon. A move, a dodge or the combo's own steps go on.
    private static Choice HoldBackBasic(Situation s, Choice choice, double? moment)
    {
        if (moment is not double at || choice.Skill.Length > 0 || BanditComboRules.IsFollowChoice(choice.Rule) || GroupAiMoveWatch.Holds(s.Unit)) return choice;
        var now = GameNow();
        if (BanditComboRules.Allowed(now, BanditComboRules.BasicBusySeconds, at)) return choice;
        NoteHoldback(s.Guid, "basic", BanditComboRules.BasicBusySeconds, at - now);
        return NewChoice("", null, BanditComboRules.HoldRule);
    }

    private static void NoteHoldback(string unit, string skill, double busy, double inSeconds)
    {
        if (holdbackLogged.TryGetValue(unit, out var last) && last == skill) return;
        if (holdbackLogged.Count >= BanditComboRules.BoardCap) holdbackLogged.Clear();
        holdbackLogged[unit] = skill;
        Log(BanditComboRules.HoldbackLine(unit, skill, busy, inSeconds, Tick()));
    }

    private static void Release(ComboReservation r, string reason, double now) => Release(r, reason, reason, now);

    private static void Release(ComboReservation r, string starterReason, string partnerReason, double now)
    {
        var tick = Tick();
        var (held, late) = BanditComboRules.HeldOf(r.At, r.Ends, now);
        foreach (var (unit, why) in new[] { (r.Starter, starterReason), (r.Partner, partnerReason) })
        {
            if (!reservations.TryGetValue(unit, out var own) || own != r) continue;
            reservations.Remove(unit);
            holdbackLogged.Remove(unit);
            Log(BanditComboRules.ReleaseLine(r.Row.Number, unit, why, held, late, tick));
        }
    }

    // The partner's follow-up, before all else while the window is open; waiting for the call its skills stay out of the kit's hands.
    private static Choice FollowCombo(Situation s)
    {
        if (!comboBoard.TryGetValue(s.Guid, out var open)) return null;
        var row = open.Row;
        var stage = BanditComboRules.StageOf(GameNow(), open.HitFrom, open.Until);
        if (stage == BanditComboRules.Stage.Closed) return LapseCombo(s, row, BanditComboRules.LapseReason(open.WindowEnd, open.Until));
        if (stage == BanditComboRules.Stage.Waiting)
        {
            foreach (var key in row.Follows)
            {
                var held = s.Find(key);
                if (held != null && held.Free) held.State = "combo-wait";
            }
            return null;
        }
        var target = s.Hostiles.Find(h => h.Guid == open.Target);
        if (target == null || (s.Downed != null && s.Downed.Contains(target))) return LapseCombo(s, row, "target");
        if (s.Taunter != null && s.Taunter.Guid != open.Target) return null;
        var entity = target.Unit.Cast<IEntity>();
        var anyFree = false;
        foreach (var key in row.Follows)
        {
            var slot = s.Find(key);
            if (slot == null || !slot.Free || slot.Data == null) continue;
            anyFree = true;
            if (!(key == GroupAiWaterRules.WaterDive ? GroupAiRules.InRangeExact(target.Distance, slot.Data.Range) : GroupAiRules.InRange(target.Distance, slot.Data.Range))) continue;
            if (areaIgnoresDowned ? AreaLoses(s, slot, target.Position, row.Follows) : HitsDowned(s, slot, target.Position)) continue;
            comboBoard.Remove(s.Guid);
            Log(BanditComboRules.FollowLine(row.Number, s.Guid, key, open.Target, Tick()));
            if (!GroupAiRules.CanHitAllies((int)slot.Data.AffectTarget)) return NewChoice(key, entity, BanditComboRules.RulePrefix + row.Number + "-follow");
            // A skill that can hit bandits goes through the kit's own warning (Damage), aimed at the combo's target.
            s.Focus = entity;
            return null;
        }
        return anyFree && row.Walk > 0 ? NewChoice("", entity, BanditComboRules.RulePrefix + row.Number + "-walk") : null;
    }

    private static Choice LapseCombo(Situation s, BanditComboRow row, string reason)
    {
        comboBoard.Remove(s.Guid);
        Log(BanditComboRules.LapseLine(row.Number, s.Guid, reason, Tick()));
        return null;
    }

    // Behind the decision: a starter skill opens its combo when a partner is ready by the hit and the roll says so; the skill goes off
    // either way. With the grace a partner ready within it makes the starter wait (a reservation for both, BanditComboRules.StartPlan);
    // a reservation of this starter for the combo stands for the roll and comes back at its moment. A combo that does not start names
    // why (CORE_BANDIT_COMBO_NOSTART): call, target, partner, reach, roll, grace (its reservation ran out) or busy (the starter is
    // reserved for another combo).
    private static Choice StartCombo(Situation s, Choice choice)
    {
        if (!CoreSettings.BanditsVary || choice.Skill.Length == 0 || BanditComboRules.IsFollowChoice(choice.Rule)) return choice;
        var rows = BanditComboRules.Of(choice.Skill);
        if (rows.Length == 0) return choice;
        var now = GameNow();
        reservations.TryGetValue(s.Guid, out var reserved);
        var reservedElsewhere = reserved != null && reserved.Starter != s.Guid;
        if (reservedElsewhere) reserved = null;
        var rolled = s.Guid + "|" + choice.Skill;
        if (reserved == null && comboRolledAt.TryGetValue(rolled, out var at) && now >= at && now - at < BanditComboRules.RollSeconds) return choice;
        NoteRoll(rolled, now);
        var callsOn = CoreSettings.ComboCalls;
        var grace = CoreSettings.ComboGrace;
        foreach (var row in rows)
        {
            if (!BanditComboRules.MayStart(row, callsOn))
            {
                NoStart(row, s.Guid, "call");
                continue;
            }
            var target = row.AimsAtCrowd ? CrowdMiddle(s) : s.Hostiles.Find(h => choice.Target != null && h.Guid == choice.Target.Guid.ToString());
            if (target == null)
            {
                NoStart(row, s.Guid, "target");
                continue;
            }
            var mine = reserved != null && reserved.Row == row ? reserved : null;
            var delay = BanditComboRules.HitDelay(row, callsOn);
            var missing = "partner";
            var comboAt = now;
            string follow = null;
            var partner = row.Alone ? (Array.Exists(row.Follows, key => s.Find(key) != null) ? s.Guid : null)
                : FindPartner(s, row, target, now, grace, delay, mine, out comboAt, out follow, out missing);
            if (partner == null)
            {
                NoStart(row, s.Guid, missing);
                continue;
            }
            var plan = BanditComboRules.StartPlan(now, comboAt, grace, mine?.Ends, delay);
            if (plan == null || (plan == BanditComboRules.PlanReserve && (reservedElsewhere || comboBoard.ContainsKey(s.Guid))))
            {
                if (plan == null && mine != null) Release(mine, "grace", now);
                NoStart(row, s.Guid, plan != null ? "busy" : mine != null ? "grace" : "partner");
                continue;
            }
            if (mine == null && !BanditComboRules.Rolls(CoreRandom.Next01(CoreRandomRules.BanditCombos, s.Guid), row.Weight, TraitsOf(s).ComboTendency))
            {
                NoStart(row, s.Guid, "roll");
                return choice;
            }
            if (plan != BanditComboRules.PlanStart)
            {
                if (mine != null) mine.ComboAt = comboAt;
                else Reserve(row, s.Guid, partner, choice.Skill, follow, target.Guid, comboAt, delay, now, grace);
                return Wait(s, choice.Skill);
            }
            var calls = BanditComboRules.Calls(row, callsOn);
            if (calls && !BanditComboRules.MayCall(now, LastGroupCall(s)))
            {
                NoStart(row, s.Guid, "call");
                return choice;
            }
            if (mine != null) Release(mine, "started", mine.Partner == partner ? "started" : "partner", now);
            if (comboBoard.Count >= BanditComboRules.BoardCap) comboBoard.Clear();
            var hitFrom = BanditComboRules.HitFrom(now, row, callsOn);
            comboBoard[partner] = new OpenCombo
            {
                Row = row, Target = target.Guid, HitFrom = hitFrom, WindowEnd = now + row.Window,
                Until = BanditComboRules.OpenUntil(now, hitFrom, row.Window, grace)
            };
            if (calls)
            {
                if (comboCalledAt.Count >= BanditComboRules.BoardCap) comboCalledAt.Clear();
                comboCalledAt[s.Guid] = now;
                BanditComboCall.Queue(s.Unit, row.Call);
            }
            Log(BanditComboRules.StartLine(row.Number, s.Guid, partner, target.Guid, row.Window, calls, Tick()));
            return choice;
        }
        return choice;
    }

    // The starter stands for its combo: the chosen skill waits (HoldReservation holds it until the combo's moment).
    private static Choice Wait(Situation s, string skill)
    {
        var slot = s.Find(skill);
        if (slot != null) slot.State = BanditComboRules.HeldState;
        return NewChoice("", null, BanditComboRules.HoldRule);
    }

    private static void NoStart(BanditComboRow row, string unit, string reason)
    {
        if (noStartLines++ < BanditComboRules.NoStartCap) Log(BanditComboRules.NoStartLine(row.Number, unit, reason, Tick()));
    }

    private static void NoteRoll(string key, double now)
    {
        if (comboRolledAt.Count >= BanditComboRules.BoardCap) comboRolledAt.Clear();
        comboRolledAt[key] = now;
    }

    // The ally whose follow-up comes first for the combo, ready by its hit (the combo's moment now) or within the grace, in reach of
    // the target in its report; the starter's reserved partner first of equals, an ally reserved for another combo left out. Else
    // null and why: "partner" (none ready) or "reach" (ready, but too far).
    private static string FindPartner(Situation s, BanditComboRow row, Figure target, double now, double grace, double delay, ComboReservation mine,
        out double comboAt, out string follow, out string missing)
    {
        comboAt = double.PositiveInfinity;
        follow = null;
        missing = "partner";
        string best = null;
        foreach (var ally in s.Allies)
        {
            if (comboBoard.ContainsKey(ally.Guid) || IsDowned(ally.Unit)) continue;
            if (reservations.TryGetValue(ally.Guid, out var other) && other != mine) continue;
            if (!comboReports.TryGetValue(ally.Guid, out var report) || !BanditComboRules.Counts(now, report.At, grace)
                || BanditComboRules.TauntBreaks(report.Taunter, target.Guid)) continue;
            var distance = GroupAiSituation.Distance(report.Position, target.Position);
            foreach (var (key, range, readyAt) in report.Skills)
            {
                if (Array.IndexOf(row.Follows, key) < 0) continue;
                var moment = BanditComboRules.ComboAt(now, BanditComboRules.FollowReady(readyAt, report.BusyUntil, report.At, grace), delay);
                if (moment > now && !BanditComboRules.Overlaps(now, moment, grace)) continue;
                if (!BanditComboRules.Reaches(distance, range, row.Walk))
                {
                    missing = "reach";
                    continue;
                }
                if (best != null && !(moment < comboAt || (moment == comboAt && mine != null && ally.Guid == mine.Partner))) continue;
                best = ally.Guid;
                comboAt = moment;
                follow = key;
            }
        }
        return best;
    }

    // The player in the guard's ring with the most others around it, where the rain falls.
    private static Figure CrowdMiddle(Situation s)
    {
        var here = s.Self.Position;
        var inRing = new List<Figure>();
        foreach (var hostile in s.Hostiles)
        {
            var downed = s.Downed != null && s.Downed.Contains(hostile);
            if (GroupAiRules.SlowRingCounts(hostile.Player, downed, GroupAiRules.Manhattan(here.x, here.y, hostile.Position.x, hostile.Position.y))) inRing.Add(hostile);
        }
        var index = BanditComboRules.MiddleOf(inRing.ConvertAll(f => (f.Position.x, f.Position.y)), BanditComboRules.RainRadius);
        return index >= 0 ? inRing[index] : null;
    }

    private static double? LastGroupCall(Situation s)
    {
        double? last = comboCalledAt.TryGetValue(s.Guid, out var own) ? own : null;
        foreach (var ally in s.Allies)
            if (comboCalledAt.TryGetValue(ally.Guid, out var called) && (last == null || called > last)) last = called;
        return last;
    }

    // The fire mage alone (13): Incinerate before its other skills on a target with enough Burning stacks.
    private static Choice BurnFinish(Situation s)
    {
        if (!CoreSettings.BanditsVary) return null;
        var slot = s.Find(WeakBanditKitKeys.Incinerate);
        var target = s.Intended;
        var unit = target?.TryCast<UnitEntity>();
        if (slot == null || !slot.Free || slot.Data == null || unit == null) return null;
        if (!GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, s.PositionOf(target)), slot.Data.Range)) return null;
        var affecters = CoreGame.Affecters(unit);
        var stacks = affecters != null && affecters.Has(BanditComboRules.BurningKey) ? affecters.GetStack(BanditComboRules.BurningKey) : 0;
        if (stacks < BanditComboRules.BurnStacks) return null;
        var now = GameNow();
        var rolled = s.Guid + "|burn";
        if (comboRolledAt.TryGetValue(rolled, out var at) && now >= at && now - at < BanditComboRules.BurnRollSeconds) return null;
        NoteRoll(rolled, now);
        if (!BanditComboRules.Rolls(CoreRandom.Next01(CoreRandomRules.BanditCombos, s.Guid), BanditComboRules.BurnWeight, TraitsOf(s).ComboTendency)) return null;
        Log(BanditComboRules.FollowLine(BanditComboRules.BurnNumber, s.Guid, slot.Key, target.Guid.ToString(), Tick()));
        return NewChoice(slot.Key, target, BanditComboRules.RulePrefix + BanditComboRules.BurnNumber + "-incinerate");
    }
}
