#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiPolicy, the player's roles from the formation window (GroupAiRoleRules): what goes before the player's
// priority casts (RoleFirst) and the healer with the role tank (TankHeal). Before the priority casts: the tank's taunt for an attacked
// damage or support member, else its walk out to gather that attacker (GroupAiRoleMoves); the support's and the tank healer's burst
// heal on a member in the critical range (Healing Touch, else Lifebloom, else another single heal); the support's seed where the most
// hurt stand, renewed only when it runs out, or grown with the solar bloom when the most hurt in its area is below 67.5 %. A damage
// dealer's own choice skips tank skills (MarkRoleSkips).
internal static partial class GroupAiPolicy
{
    // The cast before the priority casts, recorded like Core's own choice; null leaves the order as it was. moved: the tank walks out
    // to gather, the game keeps its choice.
    internal static Choice RoleFirst(GroupAiSituation s, out bool moved)
    {
        moved = false;
        summoned = false;
        var player = GroupAiRoles.PlayerOf(s.Guid);
        GroupAiRoleMoves.NoteReach(s);
        if (player == TacticalRole.Neutral) return null;
        Choice choice = null;
        if (player == TacticalRole.Tank)
        {
            choice = GroupAiCounterTank.Guard(s, out var foe, out var victim);
            if (choice == null && foe != null && GroupAiRoleMoves.TryGather(s, foe, victim))
            {
                moved = true;
                return null;
            }
        }
        if (choice == null && s.Role == GroupAiRole.Heal && player is TacticalRole.Support or TacticalRole.Tank) choice = RoleHeal(s, player);
        // A water mage of the damage role: its toad and bubbleling (GroupAiPolicyWater); a support's wave comes in RoleHeal, here only
        // for a support whose kit reads as no healer.
        if (choice == null && !summoned && (player == TacticalRole.Damage || (player == TacticalRole.Support && s.Role != GroupAiRole.Heal)))
            choice = Water(s, player);
        if (summoned)
        {
            moved = true;
            return null;
        }
        return choice == null ? null : Record(s, choice);
    }

    // Core's own choice for a damage dealer: its tank skills read as not free (slots= shows "role").
    internal static void MarkRoleSkips(GroupAiSituation s)
    {
        var player = GroupAiRoles.PlayerOf(s.Guid);
        if (player != TacticalRole.Damage) return;
        foreach (var slot in s.Slots)
            if (slot.Free && GroupAiRoleRules.SkipsForDamage(player, slot.Key)) slot.State = "role";
    }

    private static Choice RoleHeal(GroupAiSituation s, TacticalRole player)
    {
        var word = TacticalRoles.Word(player);
        // The lowest critical member first; one no burst can reach (a Lifebloom at full stacks, out of range) passes to the next
        // (a summon below a member took the turn and the member got nothing).
        foreach (var critical in CriticalFigures(s))
        {
            var burst = SingleAlly(s, "SKILL_HealingTouchAttack", critical, "role-burst-hand")
                ?? Bloom(s, critical, "role-burst-lifebloom", emergency: true)
                ?? OtherSingleHeal(s, critical, "role-burst-heal");
            if (burst == null) continue;
            Log(GroupAiRoleRules.HealLine(s.Guid, word, "burst", burst.Skill, critical.Guid, critical.Share, 0, Tick()));
            return burst;
        }
        if (player != TacticalRole.Support) return null;
        // A support water mage: the wave and its bubblelings after the burst, before the seed (GroupAiPolicyWater).
        var water = Water(s, player);
        if (water != null || summoned) return water;
        var own = OwnSeed(s, out var standing, out var known, out var left);
        var lowest = standing != null ? LowestInSeedArea(s, standing) : null;
        var step = GroupAiRoleRules.SeedRenewal(own, known, left, lowest?.Share);
        if (step == GroupAiRoleRules.SeedStep.None) return null;
        if (step == GroupAiRoleRules.SeedStep.Upgrade)
        {
            var upgrade = HealUpgrade(s, standing);
            if (upgrade != null)
            {
                Log(GroupAiRoleRules.HealLine(s.Guid, word, "upgrade", upgrade.Skill, lowest.Guid, lowest.Share, 0, Tick()));
                return upgrade;
            }
        }
        var (anchor, hurt) = SupportSeedAnchor(s);
        var seed = hurt > 0 ? Seed(s, anchor, "support-seed") : null;
        if (seed != null) Log(GroupAiRoleRules.HealLine(s.Guid, word, "seed", seed.Skill, anchor.Guid.ToString(), 0f, hurt, Tick()));
        return seed;
    }

    // The healer with the role tank: the tanks first (itself among them), the others only in the critical range (RoleFirst); a
    // seed beside the lowest tank; otherwise damage.
    private static Choice TankHeal(GroupAiSituation s)
    {
        var tank = LowestTank(s);
        if (tank != null && tank.Share < GroupAiRules.HealShare)
        {
            var heal = Bloom(s, tank, "tankheal-lifebloom", emergency: false) ?? SingleAlly(s, "SKILL_HealingTouchAttack", tank, "tankheal-hand");
            if (heal != null) return heal;
        }
        var refresh = RefreshCandidate(s);
        if (refresh != null && TankHealAllows(s, refresh))
        {
            var bloom = Bloom(s, refresh, "tankheal-lifebloom-refresh", emergency: false);
            if (bloom != null) return bloom;
        }
        if (tank != null && tank.Share < GroupAiRules.SeedNearShare && OwnSeedDue(s))
        {
            var seed = Seed(s, EnemyNextTo(s, tank), "tankheal-seed");
            if (seed != null) return seed;
        }
        return Damage(s, "tankheal-damage", GroupAiRules.DamageReserve);
    }

    // Whether the healer with the role tank may heal the figure (GroupAiRoleRules.TankHealAllows).
    private static bool TankHealAllows(GroupAiSituation s, GroupAiSituation.Figure figure)
        => figure != null && GroupAiRoleRules.TankHealAllows(GroupAiRoles.PlayerOf(figure.Guid), figure.Guid == s.Guid, IsCritical(figure));

    // The one place that asks whether a member is in the critical range of the roles (the burst, the tank healer, the role line off
    // the global cooldown): lying at near-death, or below GroupAiRoleRules.CriticalShare with look ahead and buffer (GroupAiLow).
    private static bool IsCritical(GroupAiSituation.Figure figure)
        => figure != null && GroupAiRoleRules.Critical(GroupAiLow.Share(figure.Unit, figure.Share, LowLine.Critical), GroupAiFormation.Lying(figure.Unit));

    private static bool TankHealer(GroupAiSituation s) => s.Role == GroupAiRole.Heal && GroupAiRoles.PlayerOf(s.Guid) == TacticalRole.Tank;

    // The lowest member with the role tank, the unit itself included.
    private static GroupAiSituation.Figure LowestTank(GroupAiSituation s)
    {
        var lowest = GroupAiRoles.PlayerOf(s.Guid) == TacticalRole.Tank ? s.Self : null;
        foreach (var ally in s.Allies)
            if (GroupAiRoles.PlayerOf(ally.Guid) == TacticalRole.Tank && (lowest == null || ally.Share < lowest.Share)) lowest = ally;
        return lowest;
    }

    // The members in the critical range, the unit itself included, the lowest first.
    private static List<GroupAiSituation.Figure> CriticalFigures(GroupAiSituation s)
    {
        var result = new List<GroupAiSituation.Figure>();
        foreach (var figure in Party(s))
            if (IsCritical(figure)) result.Add(figure);
        result.Sort((x, y) => x.Share.CompareTo(y.Share));
        return result;
    }

    // Another free single heal on the figure in reach, after Healing Touch and Lifebloom.
    private static Choice OtherSingleHeal(GroupAiSituation s, GroupAiSituation.Figure figure, string rule)
    {
        foreach (var slot in s.Slots)
        {
            if (!slot.Free || slot.Data == null || slot.Data.AffectTarget != AffectTarget.SingleAlly || !GroupAiRules.HealSkills.Contains(slot.Key)) continue;
            if (slot.Key == LifebloomRules.SkillKey || slot.Key == SeedSkill || !InReach(s, figure.Distance, slot.Data.Range)) continue;
            return NewChoice(slot.Key, figure.Unit.Cast<IEntity>(), rule);
        }
        return null;
    }

    // None of the unit's seeds stands, or the one with most time left has at most GroupAiRoleRules.SeedRenewSeconds. A seed grown into
    // the mucus sprayer or the solar bloom heals no more and so counts as none: the next seed is due at once.
    private static bool OwnSeedDue(GroupAiSituation s)
    {
        var own = OwnSeed(s, out _, out var known, out var left);
        return GroupAiRoleRules.SeedDue(own, known, left);
    }

    // The unit's standing seed with the most time left (a grown one is no seed building any more); known is false while the aura of one
    // of them was not seen yet, then the seeds count as fresh.
    private static bool OwnSeed(GroupAiSituation s, out IEntity seed, out bool known, out float left)
    {
        seed = null;
        known = true;
        left = 0f;
        var buildings = s.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || building.IsDead || building.Key != SeedBuilding || CoreGame.SummonerGuid(building.Cast<IEntity>()) != s.Guid) continue;
            var seconds = GroupAiHealerSeed.SecondsLeft(building.Guid.ToString(), out var life, out _);
            if (life < 0f) known = false;
            if (seed != null && seconds <= left) continue;
            seed = building.Cast<IEntity>();
            left = seconds;
        }
        return seed != null;
    }

    // The most hurt member, the unit itself included, standing in the seed's area (GroupAiHealerSeed.AuraCells); null for nobody.
    private static GroupAiSituation.Figure LowestInSeedArea(GroupAiSituation s, IEntity seed)
    {
        var at = s.PositionOf(seed);
        var cells = new HashSet<(int X, int Y)>(GroupAiHealerSeed.AuraCells(seed.Guid.ToString(), at.x, at.y));
        GroupAiSituation.Figure lowest = null;
        foreach (var figure in Party(s))
            if (cells.Contains((figure.Position.x, figure.Position.y)) && (lowest == null || figure.Share < lowest.Share)) lowest = figure;
        return lowest;
    }

    // The heal upgrade of the running out seed: the solar bloom (it sets off the Life Burst as any growth does, and heals), never the
    // mucus sprayer; null when it is not free or the seed is out of its range.
    private static Choice HealUpgrade(GroupAiSituation s, IEntity seed)
    {
        var slot = s.Find(SolarBloomSkill);
        if (slot == null || slot.Data == null || !slot.Free || seed == null) return null;
        if (!InReach(s, GroupAiSituation.Distance(s.Self.Position, s.PositionOf(seed)), slot.Data.Range)) return null;
        return NewChoice(slot.Key, seed, "support-seed-upgrade");
    }

    private const string SolarBloomSkill = "SKILL_SolarBloom";

    // The foe in the seed's range with the most hurt members beside it; a member counts by where it stands or by its place in the
    // formation, so the seed also lands where the members stand once back on their places.
    private static (IEntity Anchor, int Hurt) SupportSeedAnchor(GroupAiSituation s)
    {
        var slot = s.Find(SeedSkill);
        if (slot?.Data == null) return (null, 0);
        var foes = new List<GroupAiSituation.Figure>();
        var marks = new List<(int Hurt, int Members, int Distance)>();
        foreach (var enemy in s.Enemies)
        {
            if (!InReach(s, enemy.Distance, slot.Data.Range)) continue;
            var hurt = 0;
            var members = 0;
            foreach (var figure in Party(s))
            {
                var place = GroupAiFormation.PlaceOf(CoreGame.GuidOf(figure.Unit));
                var near = GroupAiSituation.Distance(enemy.Position, figure.Position) <= 1
                           || (place is { } p && GroupAiRules.Distance(enemy.Position.x, enemy.Position.y, p.X, p.Y) <= 1);
                if (!near) continue;
                members++;
                if (figure.Share < GroupAiRoleRules.HurtShare) hurt++;
            }
            foes.Add(enemy);
            marks.Add((hurt, members, enemy.Distance));
        }
        var pick = GroupAiRoleRules.SeedFoe(marks);
        return pick < 0 ? (null, 0) : (foes[pick].Unit.Cast<IEntity>(), marks[pick].Hurt);
    }

    private static IEnumerable<GroupAiSituation.Figure> Party(GroupAiSituation s)
    {
        yield return s.Self;
        foreach (var ally in s.Allies) yield return ally;
    }
}
