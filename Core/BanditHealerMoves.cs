using System.Globalization;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the herbalist's moves: the step away (Kite, KiteTile) and the vines, the heal path to a comrade,
// moving up to support, and the game's move check (HealerKeepsAway). Her order of decisions is in BanditHealer.
internal static partial class GroupAiBandits
{
    internal const int KiteStep = 3;
    internal const float KiteSeconds = 1f;

    internal const string VinesSkill = BanditKitKeys.Vines;

    private static readonly Dictionary<string, float> kiteSince = new(StringComparer.Ordinal);

    // The healer under attack (or with a hostile close by) keeps her distance like a real group would
    // (order (a): distance and vines first, Lifebloom after): vines on whoever comes within
    // VinesNearCells on one of the game's lines, else a step away (KiteTile) that keeps her comrades within
    // Lifebloom's reach, the game's flee skill as a supplement, Lifebloom on herself only when no step is left.
    // Held (stunned, bound, unconscious) she gets no move order (133 orders in 2.5 s).
    private static Choice Kite(Situation s)
    {
        if (!ProtectHealer || s.Kit != Kit.Healer) return null;
        // (she kited on after the wipe, hostiles=0): no step away without a capable hostile left.
        if (!GroupAiReachRules.KiteAllowed(s.Hostiles.Count - DownedAmongHostiles(s))) return null;
        if (s.Taunter != null && !GroupAiRules.TauntAllows(true, "move")) return null;
        var closest = s.ClosestHostile;
        var threat = s.HealerAttacker;
        if (threat == null)
        {
            threat = closest;
            if (threat == null || threat.Distance > KiteDistance) return null;
        }
        var vines = s.Find(VinesSkill);
        if (vines != null && vines.Free && vines.Data != null && closest != null && closest.Distance <= GroupAiRules.VinesNearCells)
        {
            var cast = VinesChoice(s, vines, threat);
            if (cast != null) return cast;
        }
        var now = GameClock.Now;
        var unmoved = GroupAiMoveWatch.LastUnmoved(s.Guid);
        var throttled = kiteSince.TryGetValue(s.Guid, out var since) && !GameClockRules.Elapsed(now, since, KiteSeconds)
            && !GroupAiRules.KiteAgainNow(unmoved, closest?.Distance ?? int.MaxValue);
        var held = GroupAiMoveWatch.IsHeld(s.Unit);
        if (throttled || held) NoteKiteSkip(s, held ? "held" : "throttled");
        if (!throttled && !held)
        {
            // No flee skill; always Core's escape cell (EscapeRules: away, ways out, the
            // leash as a weight, never past a second foe). The move order holds her decisions until it ends.
            var tile = KiteTile(s, threat);
            if (tile.HasValue)
            {
                kiteSince[s.Guid] = now;
                if (GroupAiBanditMoves.TryOrder(s.Unit, tile.Value, "kite"))
                {
                    kiteSkips.Remove(s.Guid);
                    return NewChoice("", null, "kite");
                }
            }
            else
            {
                // Only when no cell around is walkable at all.
                NoteKiteSkip(s, "blocked");
            }
        }
        // No step now (just stepped, held, no cell): Lifebloom on herself, not with a hostile next to her.
        var bloom = s.Find(LifebloomSkill);
        if (bloom != null && bloom.Free && s.Self.Share < SelfHealShare && LifebloomUseful(s.Self)
            && GroupAiRules.SelfBloomAllowed(closest?.Distance ?? int.MaxValue, 1))
        {
            return NewChoice(bloom.Key, s.Self.Unit.Cast<IEntity>(), "kite-lifebloom-self");
        }
        return null;
    }

    // The cells foes stand on, weak (below WeakShare) or held ones marked (EscapeRules).
    private static List<EscapeRules.Foe> FoesOf(Situation s)
        => s.Hostiles.FindAll(h => s.Downed == null || !s.Downed.Contains(h))
            .ConvertAll(h => new EscapeRules.Foe(h.Position.x, h.Position.y, h.Share < WeakShare || GroupAiMoveWatch.IsHeld(h.Unit)));

    private static HashSet<(int, int)> Occupied(Situation s)
    {
        var occupied = new HashSet<(int, int)>();
        foreach (var h in s.Hostiles) occupied.Add((h.Position.x, h.Position.y));
        foreach (var a in s.Allies) occupied.Add((a.Position.x, a.Position.y));
        return occupied;
    }

    private static int FightSeed(Situation s)
        => EscapeRules.SeedOf(s.Guid, combatSince.TryGetValue(s.Guid, out var since) ? (long)(since * 1000) : 0);

    // The heal path (GroupAiRules.HealPath): a cell within Lifebloom's reach of the comrade, normal at most 8 steps
    // and never past a foe, emergency at most 12 and past one at most (EscapeRules.Approach). CORE_AI_BANDIT_HEALPATH.
    private static Choice HealPathMove(Situation s, Figure lowest, Figure closest)
    {
        if (lowest == null || lowest == s.Self || GroupAiMoveWatch.IsHeld(s.Unit)) return null;
        var slot = s.Find(LifebloomSkill);
        if (slot?.Data == null) return null;
        var owner = CoreGame.GuidOf(s.Unit);
        var healSoon = (slot.Free && Lifebloom.ChargesOf(owner) > 0) || Lifebloom.SecondsUntilCharges(owner, 1) <= GroupAiRules.HealWaitSeconds;
        var reach = (int)slot.Data.Range;
        var mode = GroupAiRules.HealPath(lowest.Share, healBelow, healSoon, lowest.Distance, reach, s.HealerAttacker != null,
            closest?.Distance ?? int.MaxValue, s.Taunter != null);
        if (mode == null) return null;
        var (steps, passFoes, minFoe) = GroupAiRules.HealPathLimits(mode);
        var occupied = Occupied(s);
        var cell = EscapeRules.Approach(s.Self.Position.x, s.Self.Position.y, lowest.Position.x, lowest.Position.y, reach, FoesOf(s), steps,
            passFoes, minFoe, (x, y) => !occupied.Contains((x, y)) && Walkable(s, x, y), FightSeed(s), closer: true);
        if (cell == null || !GroupAiBanditMoves.TryOrder(s.Unit, new Vector2Int(cell.X, cell.Y), "healpath")) return null;
        StartHealPath(s, lowest, cell.X, cell.Y, cell.Steps, reach);
        Log(string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_HEALPATH unit={0} target={1} share={2:0.00} mode={3} from={4},{5} to={6},{7} steps={8} distance={9} tick={10}",
            s.Guid, lowest.Guid, lowest.Share, mode, s.Self.Position.x, s.Self.Position.y, cell.X, cell.Y, cell.Steps, lowest.Distance, Tick()));
        return NewChoice("", null, "healer-healpath");
    }

    // The running heal path per herbalist, until it ends (GroupAiRules.HealPathResult).
    private sealed class HealPathRun
    {
        internal UnitEntity Target;
        internal string TargetGuid;
        internal int ToX, ToY, FromX, FromY, Steps, Reach;
        internal double Start;
    }

    private static readonly Dictionary<string, HealPathRun> healPaths = new(StringComparer.Ordinal);

    private static void StartHealPath(Situation s, Figure target, int toX, int toY, int steps, int reach)
    {
        var now = GameNow();
        if (healPaths.TryGetValue(s.Guid, out var running)) EndHealPath(s.Guid, running, "replaced", s.Self.Position.x, s.Self.Position.y, now);
        healPaths[s.Guid] = new HealPathRun
        {
            Target = target.Unit, TargetGuid = target.Guid, ToX = toX, ToY = toY,
            FromX = s.Self.Position.x, FromY = s.Self.Position.y, Steps = steps, Reach = reach, Start = now
        };
    }

    // Every game tick: whether the herbalist's heal path has ended, and how (CORE_AI_BANDIT_HEALPATH_END).
    private static void CheckHealPath(string guid, UnitEntity unit, long tick)
    {
        if (!healPaths.TryGetValue(guid, out var run)) return;
        try
        {
            var at = CoreGame.Position(unit);
            var gone = run.Target == null || run.Target.IsDead || IsDowned(run.Target);
            var distance = gone ? int.MaxValue : GroupAiSituation.Distance(at, CoreGame.Position(run.Target));
            var now = GameNow();
            var result = GroupAiRules.HealPathResult(at.x == run.ToX && at.y == run.ToY, distance, run.Reach, gone,
                !gone && GroupAiSituation.ShareOf(run.Target) >= healBelow, GroupAiMoveWatch.Holds(unit), (float)(now - run.Start));
            if (result != null) EndHealPath(guid, run, result, at.x, at.y, now, tick);
        }
        catch (Exception ex)
        {
            healPaths.Remove(guid);
            Warn("CORE_AI_BANDIT_HEALPATH_END_FAILED unit=" + guid + ": " + ex.Message);
        }
    }

    private static void EndHealPath(string guid, HealPathRun run, string result, int x, int y, double now, long tick = -1)
    {
        healPaths.Remove(guid);
        Log(GroupAiRules.HealPathEndLine(guid, run.TargetGuid, result, run.Steps, GroupAiRules.Distance(run.FromX, run.FromY, x, y),
            (float)(now - run.Start), tick < 0 ? Tick() : tick));
    }

    // (bound aggro, follow fights further away): safe (GroupAiRules.SupportSafe) and more than reach away
    // from the comrade to support (GroupAiRules.SupportTarget, else the band's centre), she moves up to within reach,
    // at least SupportMinFoeDistance from every foe, at most one order every SupportSeconds. CORE_AI_BANDIT_SUPPORT.
    private static readonly Dictionary<string, double> supportSince = new(StringComparer.Ordinal);

    private static Choice SupportMove(Situation s, Figure closest, bool execute)
    {
        if (s.Allies.Count == 0) return null;
        if (!GroupAiRules.SupportSafe(closest?.Distance ?? int.MaxValue, s.Taunter != null, GroupAiMoveWatch.IsHeld(s.Unit))) return null;
        var now = GameNow();
        if (execute && supportSince.TryGetValue(s.Guid, out var last) && !GameClockRules.Elapsed((float)now, (float)last, (float)GroupAiRules.SupportSeconds)) return null;
        var comrades = s.Allies.ConvertAll(a => (a.Guid, a.Share,
            CoreGame.Component<CombatInfoComponent>(a.Unit.Cast<IEntity>())?.GetCombatState() == CombatState.InCombat));
        var targetGuid = GroupAiRules.SupportTarget(comrades, healBelow);
        Vector2Int at;
        var target = targetGuid != null ? s.Allies.Find(a => a.Guid == targetGuid) : null;
        if (target != null) at = target.Position;
        else
        {
            var sx = 0;
            var sy = 0;
            foreach (var a in s.Allies)
            {
                sx += a.Position.x;
                sy += a.Position.y;
            }
            at = new Vector2Int((int)Math.Round((double)sx / s.Allies.Count), (int)Math.Round((double)sy / s.Allies.Count));
        }
        var reach = (int)(s.Find(LifebloomSkill)?.Data?.Range ?? LifebloomRules.Range);
        if (GroupAiSituation.Distance(s.Self.Position, at) <= reach) return null;
        var occupied = Occupied(s);
        var cell = EscapeRules.Approach(s.Self.Position.x, s.Self.Position.y, at.x, at.y, reach, FoesOf(s), GroupAiRules.HealPathNormalSteps,
            0, GroupAiRules.SupportMinFoeDistance, (x, y) => !occupied.Contains((x, y)) && Walkable(s, x, y), FightSeed(s), closer: true);
        if (cell == null) return null;
        if (!execute) return NewChoice("", null, "healer-regroup");
        if (!GroupAiBanditMoves.TryOrder(s.Unit, new Vector2Int(cell.X, cell.Y), "support")) return null;
        supportSince[s.Guid] = now;
        Log(string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDIT_SUPPORT unit={0} target={1} from={2},{3} to={4},{5} steps={6} tick={7}",
            s.Guid, targetGuid ?? "centre", s.Self.Position.x, s.Self.Position.y, cell.X, cell.Y, cell.Steps, Tick()));
        return NewChoice("", null, "healer-regroup");
    }

    // For CoreUnitMovePatch: the bandits' healer never takes the tree's own step toward the closest hostile.
    internal static bool HealerKeepsAway(IEntity owner)
    {
        var unit = owner?.TryCast<UnitEntity>();
        return unit != null && ProtectHealer && CoreUnitRules.IsCoreUnit(unit.Key) && GroupAiRules.HealerKeepsAway(KitOf(unit) == Kit.Healer);
    }

    // Why the healer took no step away, one line per change of reason (CORE_AI_BANDIT_KITE_SKIP).
    private static readonly Dictionary<string, string> kiteSkips = new(StringComparer.Ordinal);

    private static void NoteKiteSkip(Situation s, string reason)
    {
        if (kiteSkips.TryGetValue(s.Guid, out var last) && last == reason) return;
        kiteSkips[s.Guid] = reason;
        Log($"CORE_AI_BANDIT_KITE_SKIP unit={s.Guid} reason={reason} tick={Tick().ToString(CultureInfo.InvariantCulture)}");
    }

    // The vines only to get away from hostiles near her, never as damage and never blind
    // (GroupAiVines); none while provoked.
    private static Choice VinesChoice(Situation s, Slot vines, Figure threat)
    {
        var tick = Tick();
        if (!GroupAiRules.VinesAllowed(vines.Free, GroupAiVines.Provoked(s.Unit)))
        {
            Log(GroupAiRules.VinesLine(false, s.Guid, "provoked", 0, 0, "-", false, 0, tick));
            return null;
        }
        var near = new List<(IEntity Entity, Vector2Int Position)>();
        foreach (var hostile in s.Hostiles)
        {
            if (hostile.Distance > GroupAiRules.VinesNearCells || (s.Downed != null && s.Downed.Contains(hostile))) continue;
            near.Add((hostile.Unit.Cast<IEntity>(), hostile.Position));
        }
        var friends = s.Allies.ConvertAll(ally => ally.Position);
        var pick = GroupAiVines.Choose(s.InMap, s.Self.Position, near, threat?.Unit.Cast<IEntity>(), friends);
        if (pick == null)
        {
            Log(GroupAiRules.VinesLine(false, s.Guid, "none", 0, 0, "-", false, near.Count, tick));
            return null;
        }
        Log(GroupAiRules.VinesLine(false, s.Guid, "cast", pick.Direction.X, pick.Direction.Y, pick.Target.Guid.ToString(),
            pick.HitsThreat, pick.NearHits, tick));
        return NewChoice(vines.Key, pick.Target, "kite-vines");
    }

    // A walkable cell within KiteStep of the healer, as far as possible from the nearest hostile, that
    // keeps every living bandit within Lifebloom's reach; when no cell does, at least the most hurt
    // one (keep the distance, stay in reach of the comrades). Among equal cells
    // the guard between it and the threat, then the tighter group, then the shorter step win. Slowed she
    // kites shorter (GroupAiRules.KiteStepFor). Not bound to the tank (the cover step behind the guard made it worse; the others pull and stun).
    // The escape cell from the shared EscapeRules. Foes are the capable
    // hostiles, weak (below WeakShare) or held ones marked; the leash is Lifebloom's reach to the nearest comrade, a
    // weight, not a limit; a true tie is chosen by the fight's seed. Null only when no cell around is walkable.
    internal const float WeakShare = 0.3f;

    private static Vector2Int? KiteTile(Situation s, Figure threat)
    {
        var reach = s.Find(LifebloomSkill)?.Data?.Range ?? LifebloomRules.Range;
        var here = NearestHostile(s, s.Self.Position);
        var speed = GroupAiRules.MoveSpeedFactor(CoreGame.Stat(s.Unit, StatType.MovementSpeedPerModifier));
        var maxStep = GroupAiRules.KiteStepFor(speed, KiteStep);
        var foes = FoesOf(s);
        var friends = s.Allies.ConvertAll(a => (a.Position.x, a.Position.y));
        // Comrades below heal-below, lowest first, weigh the cells and pull the leash.
        var hurt = s.Allies.FindAll(a => GroupAiRules.HealerHeals(a.Share, healBelow));
        hurt.Sort((a, b) => a.Share.CompareTo(b.Share));
        var healTargets = hurt.ConvertAll(a => (a.Position.x, a.Position.y));
        var occupied = Occupied(s);
        var pick = EscapeRules.Choose(s.Self.Position.x, s.Self.Position.y, foes, friends, (int)reach, maxStep,
            (x, y) => !occupied.Contains((x, y)) && Walkable(s, x, y), FightSeed(s), healTargets);
        if (pick == null) return null;
        var tile = new Vector2Int(pick.X, pick.Y);
        Log(string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDIT_KITE unit={0} from={1},{2} to={3},{4} hostile={5}->{6} exits={7} leash={8} healTargets={9}/{10} reach={11} step={12} speed={13:0.00} mode={14}",
            s.Guid, s.Self.Position.x, s.Self.Position.y, tile.x, tile.y, here, pick.Away, pick.Exits, pick.Leash, pick.HealTargets, healTargets.Count,
            reach, maxStep, speed, pick.Mode));
        return tile;
    }

    private static bool Walkable(Situation s, int x, int y)
    {
        try
        {
            var tile = new Vector2Int(x, y);
            return s.InMap.GetNearMovablePosition(tile) == tile;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Cells to the nearest hostile from a cell; int.MaxValue without hostiles.
    private static int NearestHostile(Situation s, Vector2Int at)
    {
        var nearest = int.MaxValue;
        foreach (var hostile in s.Hostiles)
            nearest = Math.Min(nearest, GroupAiSituation.Distance(at, hostile.Position));
        return nearest;
    }
}
