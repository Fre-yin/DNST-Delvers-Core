using UnityEngine;
#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// The player's roles that leave their place in the formation (GroupAiRoleRules), with the full group AI:
//   the tank gathers: an attacker of a damage or support member stands beyond its taunt, so it walks toward it until the taunt
//     reaches, at most to the edge of the tactical grid around the leader, and goes back once nothing attacked it for
//     GatherQuietSeconds. A tank that leads keeps the formation where it set out: the others stay on their places and the
//     formation does not turn; it walks back to that cell itself.
//   the damage dealer roams: in a fight it could strike no foe from where it stands for DamageIdleSeconds, so it goes to a foe
//     that attacks a member who is no tank, else to the nearest, and strikes it; it takes its place again after the fight.
// Neither is held on its place nor sent there while out (GroupAiFormation). CORE_AI_ROLE_TANK and CORE_AI_ROLE_DAMAGE per change.
internal static class GroupAiRoleMoves
{
    private sealed class Gather
    {
        internal float Since;
        internal (int X, int Y) From;
        internal bool Leader;
        internal bool Back;
        internal float BackAt;
    }

    private static readonly Dictionary<Guid, Gather> gathers = new();
    private static readonly Dictionary<Guid, float> idleSince = new();
    private static readonly Dictionary<Guid, Guid> roamFoe = new();
    private static readonly Dictionary<Guid, float> roamOrderAt = new();
    // The farthest reach of each member's basic attack and skills on foes, from its last decision.
    private static readonly Dictionary<Guid, int> reach = new();

    internal static void Reset()
    {
        gathers.Clear();
        idleSince.Clear();
        roamFoe.Clear();
        roamOrderAt.Clear();
        reach.Clear();
    }

    // Out of its place for its role: gathering, or walking back as a leader.
    internal static bool Out(Guid unit) => gathers.ContainsKey(unit);

    // "reach=<cells> out=<0|1> roam=<0|1>" for the Toolkit (TacticalFormationTestHooks.FormationRoleStatusForTest).
    internal static string Status(Guid unit)
        => $"reach={(reach.TryGetValue(unit, out var cells) ? cells : 0)} out={(gathers.ContainsKey(unit) ? 1 : 0)} roam={(roamFoe.ContainsKey(unit) ? 1 : 0)}";

    // The cell a leading tank set out from, while it is out: the formation stands around it.
    internal static (int X, int Y)? LeaderAnchor(Guid leader) => gathers.TryGetValue(leader, out var g) && g.Leader ? g.From : null;

    // The reach the damage dealer's idle check uses, from the decision's slots. A skill whose auto use is off is cast by neither Core
    // nor the game (TacticalFormation.CastTakesPart), so its reach does not hold the roam back.
    internal static void NoteReach(GroupAiSituation s)
    {
        if (s?.Owner == null) return;
        var range = Math.Max(1, GroupAiRoles.Sheet(s.BasicAttack)?.Range ?? 1);
        foreach (var slot in s.Slots)
            if (slot.Data?.AffectTarget is AffectTarget.SingleHostile or AffectTarget.MultipleHostile && TakesPart(s.Owner, slot.Key))
                range = Math.Max(range, slot.Data.Range);
        reach[CoreGame.GuidOf(s.Owner)] = range;
    }

    // A failing read of the auto use counts as taking part, the reach as before: the decision never fails for it.
    private static bool TakesPart(UnitEntity owner, string skill)
    {
        try { return TacticalFormation.CastTakesPart(owner, skill); }
        catch (Exception) { return true; }
    }

    // The tank walks toward the attacker beyond its taunt (GroupAiCounterTank.Guard found it). True when the order went out; the game
    // keeps its own skill choice, as with a dodge.
    internal static bool TryGather(GroupAiSituation s, GroupAiSituation.Figure foe, string victim)
    {
        if (s?.Owner == null || foe == null || !GroupAiFormation.Active) return false;
        var guid = CoreGame.GuidOf(s.Owner);
        gathers.TryGetValue(guid, out var g);
        // A tank staying in a seed's aura is not sent out to gather, a gather already running neither (GroupAiSeedStay; the
        // formation leaves it alone too; Look ends that gather).
        if (GroupAiSeedStay.Staying(s.Owner) || (g == null && !GroupAiFormation.Holds(s.Owner)) || GroupAiMoveWatch.Holds(s.Owner)) return false;
        var taunt = s.Find(GroupAiCounterTank.TauntSkill);
        if (taunt?.Data == null) return false;
        var leader = guid == GroupAiFormation.Leader;
        var self = (s.Self.Position.x, s.Self.Position.y);
        var center = g != null && g.Leader ? g.From : leader ? self : GroupAiFormation.Center;
        var free = GroupAiFormation.Walkable(new HashSet<(int, int)>(), s.InMap);
        var cell = GroupAiRoleRules.GatherCell(self, (foe.Position.x, foe.Position.y), taunt.Data.AreaRadius, center, free);
        if (cell == null || !GroupAiZones.OrderMove(s.Owner, new Vector2Int(cell.Value.X, cell.Value.Y), "gather")) return false;
        var now = GameClock.Now;
        if (g == null) gathers[guid] = new Gather { Since = now, From = self, Leader = leader };
        else
        {
            g.Since = now;
            g.Back = false;
        }
        GroupAiPolicy.Log(GroupAiRoleRules.TankLine(s.Guid, "gather", victim, foe.Guid, cell, Tick()));
        return true;
    }

    // One look of the formation (GroupAiFormation.OnGameTick): gathers end after their quiet, a leader walks back to where it set out;
    // roams end with the fight.
    internal static void Look(List<UnitEntity> party, bool fight, float now, Func<Guid, float> lastHit)
    {
        foreach (var guid in new List<Guid>(gathers.Keys))
        {
            var g = gathers[guid];
            var unit = party.Find(member => CoreGame.GuidOf(member) == guid);
            // Gone, or staying in a seed now: the gather ends without a way back, the stay keeps the tank in the aura.
            if (unit == null || unit.IsDead || GroupAiSeedStay.Staying(unit))
            {
                gathers.Remove(guid);
                continue;
            }
            var at = CoreGame.Position(unit);
            if (g.Back)
            {
                if ((at.x, at.y) == g.From || (!GroupAiMoveWatch.Holds(unit) && now - g.BackAt >= 2f * GroupAiFormationRules.LookSeconds))
                    gathers.Remove(guid);
                continue;
            }
            if (!GroupAiRoleRules.GatherEnds(fight, now, g.Since, lastHit(guid))) continue;
            // The gather ends; the line names the cell only when the leader is really sent back (not while the player's order stands).
            var sent = GroupAiRoleRules.GathersBack(g.Leader, (at.x, at.y) == g.From, GroupAiChase.HasOrder(guid))
                && GroupAiZones.OrderMove(unit, new Vector2Int(g.From.X, g.From.Y), "gather-back");
            GroupAiPolicy.Log(GroupAiRoleRules.TankLine(guid.ToString("D"), "back", null, null, sent ? g.From : null, Tick()));
            if (!sent)
            {
                gathers.Remove(guid);
                continue;
            }
            g.Back = true;
            g.BackAt = now;
        }
        if (fight) return;
        idleSince.Clear();
        foreach (var guid in new List<Guid>(roamFoe.Keys))
            GroupAiPolicy.Log(GroupAiRoleRules.DamageLine(guid.ToString("D"), "back", null, false, 0f, Tick()));
        roamFoe.Clear();
        roamOrderAt.Clear();
    }

    // A member with the role damage in a fight: whether it is out roaming, starting once it could strike no foe for DamageIdleSeconds.
    // While out it is sent toward its foe again every ReorderSeconds until in reach; a fallen foe is replaced.
    internal static bool Roams(UnitEntity unit, Guid guid, bool fight, float now)
    {
        if (!fight || GroupAiRoles.PlayerOf(guid.ToString("D")) != TacticalRole.Damage)
        {
            idleSince.Remove(guid);
            // A role changed in the fight ends the roam at once; the member takes its place again.
            if (fight && roamFoe.Remove(guid))
            {
                roamOrderAt.Remove(guid);
                GroupAiPolicy.Log(GroupAiRoleRules.DamageLine(guid.ToString("D"), "back", null, false, 0f, Tick()));
            }
            return false;
        }
        var range = reach.TryGetValue(guid, out var known) ? known : 1;
        if (!roamFoe.ContainsKey(guid))
        {
            if (GroupAiFormation.FoeInReach(unit, range))
            {
                idleSince.Remove(guid);
                return false;
            }
            if (!idleSince.TryGetValue(guid, out var since)) idleSince[guid] = since = now;
            if (!GroupAiRoleRules.RoamDue(fight, now, since)) return false;
        }
        var foe = FoeOf(guid, unit, out var onNonTank);
        if (foe == null) return roamFoe.ContainsKey(guid);
        var first = !roamFoe.TryGetValue(guid, out var before) || before != CoreGame.GuidOf(foe);
        roamFoe[guid] = CoreGame.GuidOf(foe);
        if (first)
            GroupAiPolicy.Log(GroupAiRoleRules.DamageLine(guid.ToString("D"), "roam", CoreGame.GuidOf(foe).ToString("D"), onNonTank,
                idleSince.TryGetValue(guid, out var idle) ? now - idle : 0f, Tick()));
        var from = CoreGame.Position(unit);
        var to = CoreGame.Position(foe);
        if (GroupAiFormationRules.InReach(from.x, from.y, to.x, to.y, range) || GroupAiMoveWatch.Holds(unit)) return true;
        if (!first && roamOrderAt.TryGetValue(guid, out var sent) && now - sent < GroupAiFormationRules.ReorderSeconds) return true;
        var cell = GroupAiRoleRules.ApproachCell((from.x, from.y), (to.x, to.y), range,
            GroupAiFormation.Walkable(new HashSet<(int, int)>(), CoreGame.InMap));
        if (cell != null && GroupAiZones.OrderMove(unit, new Vector2Int(cell.Value.X, cell.Value.Y), "roam")) roamOrderAt[guid] = now;
        return true;
    }

    // The foe a roaming member strikes: its own while alive; the game's choice of a hostile skill turns to it. A member that went to
    // stay in a seed roams no more: its roam foe goes, and it strikes what is in reach of the aura.
    internal static IEntity Retarget(UnitEntity owner, IEntity target)
    {
        if (owner == null || target == null || target.Faction == owner.Faction || !roamFoe.TryGetValue(CoreGame.GuidOf(owner), out var foe)) return null;
        if (GroupAiSeedStay.Staying(owner))
        {
            roamFoe.Remove(CoreGame.GuidOf(owner));
            return null;
        }
        if (target.Guid.ToString() == foe.ToString("D")) return null;
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead && CoreGame.GuidOf(unit) == foe) return unit.Cast<IEntity>();
        return null;
    }

    // The roaming member's foe: its own while alive, else the nearest that attacks a member who is no tank, else the nearest; only
    // foes in the fight (as GroupAiFormation.NoteTargeted), never a sleeping or unconcerned one elsewhere on the map.
    private static UnitEntity FoeOf(Guid member, UnitEntity self, out bool onNonTank)
    {
        onNonTank = false;
        var at = CoreGame.Position(self);
        var foes = new List<UnitEntity>();
        var marks = new List<(int Distance, bool OnNonTank)>();
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit == null || unit.IsDead || unit.Faction != FactionType.Enemy) continue;
            var info = CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>());
            if (info?.GetCombatState() != CombatState.InCombat) continue;
            var target = info.GetCombatTarget()?.TryCast<UnitEntity>();
            var nonTank = target != null && target.Faction == FactionType.Player
                          && GroupAiRoles.PlayerOf(CoreGame.GuidOf(target).ToString("D")) != TacticalRole.Tank;
            if (roamFoe.TryGetValue(member, out var kept) && CoreGame.GuidOf(unit) == kept)
            {
                onNonTank = nonTank;
                return unit;
            }
            var p = CoreGame.Position(unit);
            foes.Add(unit);
            marks.Add((GroupAiRules.Distance(at.x, at.y, p.x, p.y), nonTank));
        }
        var pick = GroupAiRoleRules.RoamTarget(marks);
        if (pick < 0) return null;
        onNonTank = marks[pick].OnNonTank;
        return foes[pick];
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}
