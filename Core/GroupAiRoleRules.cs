using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The player's roles in the group AI, the pure part: a role set in the formation window (TacticalRole, each
// formation its own) replaces the role read from the kit (GroupAiRules.DetectRole) for the skill choice; Neutral keeps the kit's.
// Only with the full group AI and while a formation applies. The tank taunts for an attacked damage or support member before the
// priority casts and walks out to gather an attacker it cannot reach, at most to the edge of the tactical grid around the leader;
// the damage dealer leaves its place after DamageIdleSeconds without a foe in reach; the support heals the critical first and keeps
// a seed where the most hurt stand; a tank with heals heals the tanks first.
internal static class GroupAiRoleRules
{
    // A party member in the critical range: below this share or lying at near-death.
    internal const float CriticalShare = 0.3f;
    // Below this share a member counts as hurt for the support's seed.
    internal const float HurtShare = 0.99f;
    // A damage dealer that could strike nothing from its place for this long goes to the foes (3 to 4 s).
    internal const float DamageIdleSeconds = 3.5f;
    // A gathering tank returns once nothing attacked it for this long (3 s).
    internal const float GatherQuietSeconds = 3f;
    // The support renews her seed when it has this little left, not earlier.
    internal const float SeedRenewSeconds = 2f;
    // The farthest a gathering tank goes from the leader: the edge of the tactical grid (Chebyshev, 4 on the 9 by 9 grid).
    internal const int GridReach = TacticalFormationRules.GridSize / 2;

    // The role the group AI decides by. Tank: the shield's rules, with a kit of heals the healer's for the tanks. Damage: the kit's
    // damage role, from the basic attack when the kit reads as healer or shield. Support: the healer's rules with a kit of heals,
    // else damage. Neutral: the kit's role.
    internal static GroupAiRole Effective(GroupAiRole kit, TacticalRole player, bool basicRanged, bool basicMagic)
    {
        var damage = basicRanged ? GroupAiRole.Ranged : basicMagic ? GroupAiRole.Caster : GroupAiRole.Melee;
        return player switch
        {
            TacticalRole.Tank => kit == GroupAiRole.Heal ? GroupAiRole.Heal : GroupAiRole.Shield,
            TacticalRole.Damage => kit is GroupAiRole.Heal or GroupAiRole.Shield ? damage : kit,
            TacticalRole.Support => kit == GroupAiRole.Heal ? GroupAiRole.Heal : kit == GroupAiRole.Shield ? damage : kit,
            _ => kit
        };
    }

    // Core's own choice for a damage dealer takes no tank skill; the player's priority casts may still hold one.
    internal static bool SkipsForDamage(TacticalRole player, string skill)
        => player == TacticalRole.Damage && skill != null && GroupAiRules.ShieldSkills.Contains(skill);

    internal static bool Critical(float share, bool lying) => lying || share < CriticalShare;

    // The tank defends members with the role damage or support while a foe attacks them: the lowest of them.
    internal static string GuardVictim(IReadOnlyList<(string Guid, TacticalRole Role, float Share, int Attackers)> allies)
    {
        if (allies == null) return null;
        string pick = null;
        var pickShare = float.MaxValue;
        foreach (var ally in allies)
        {
            if (ally.Attackers <= 0 || ally.Role is not (TacticalRole.Damage or TacticalRole.Support)) continue;
            if (ally.Share < pickShare)
            {
                pick = ally.Guid;
                pickShare = ally.Share;
            }
        }
        return pick;
    }

    // The cell a gathering tank walks to: step by step (diagonals count as one) from its cell toward the foe, the first cell from
    // which the taunt reaches the foe (Manhattan, as the taunt's area). It never leaves GridReach around the leader's cell; at that
    // edge it stops on the last free cell inside. Null when it cannot get a step nearer.
    internal static (int X, int Y)? GatherCell((int X, int Y) from, (int X, int Y) foe, int tauntReach, (int X, int Y) leader,
        Func<int, int, bool> free)
    {
        var reach = Math.Max(1, tauntReach);
        (int X, int Y)? best = null;
        var at = from;
        while (GroupAiRules.Manhattan(at.X, at.Y, foe.X, foe.Y) > reach)
        {
            var next = (X: at.X + Math.Sign(foe.X - at.X), Y: at.Y + Math.Sign(foe.Y - at.Y));
            if (next == foe || GroupAiRules.Distance(next.X, next.Y, leader.X, leader.Y) > GridReach) break;
            at = next;
            if (free == null || free(at.X, at.Y)) best = at;
        }
        return best;
    }

    // The cell a roaming damage dealer walks to: step by step toward the foe, the first cell within its reach (Chebyshev, as the
    // skills' range), or the last free one before it. Null when it cannot get a step nearer.
    internal static (int X, int Y)? ApproachCell((int X, int Y) from, (int X, int Y) foe, int range, Func<int, int, bool> free)
    {
        var reach = Math.Max(1, range);
        (int X, int Y)? best = null;
        var at = from;
        while (GroupAiRules.Distance(at.X, at.Y, foe.X, foe.Y) > reach)
        {
            at = (at.X + Math.Sign(foe.X - at.X), at.Y + Math.Sign(foe.Y - at.Y));
            if (free == null || free(at.X, at.Y)) best = at;
        }
        return best;
    }

    // Whether a gathering tank goes back: the fight is over, or nothing attacked it for GatherQuietSeconds since it set out.
    internal static bool GatherEnds(bool fight, float now, float since, float lastHit)
        => !fight || GameClockRules.Elapsed(now, Math.Max(since, lastHit), GatherQuietSeconds);

    // Whether a gathering leader is sent back to where it set out: not while it stands there or the player's attack order of it
    // stands (the player's order goes first).
    internal static bool GathersBack(bool leader, bool atStart, bool ordered) => leader && !atStart && !ordered;

    // Whether a damage dealer leaves its place: in a fight it could strike no foe for DamageIdleSeconds.
    internal static bool RoamDue(bool fight, float now, float idleSince) => fight && GameClockRules.Elapsed(now, idleSince, DamageIdleSeconds);

    // The foe a roaming damage dealer goes to: the nearest that attacks a member who is no tank, else the nearest. -1 for none.
    internal static int RoamTarget(IReadOnlyList<(int Distance, bool OnNonTank)> foes)
    {
        var pick = -1;
        for (var i = 0; foes != null && i < foes.Count; i++)
        {
            if (pick < 0) { pick = i; continue; }
            var better = foes[i].OnNonTank != foes[pick].OnNonTank ? foes[i].OnNonTank : foes[i].Distance < foes[pick].Distance;
            if (better) pick = i;
        }
        return pick;
    }

    // The support's running out seed: with the most hurt member in its area nearer EmergencyShare than full health
    // (below this share) it is grown with the heal upgrade, the solar bloom, which sets off the Life Burst; otherwise a new seed comes.
    internal const float SeedUpgradeShare = (GroupAiRules.EmergencyShare + 1f) / 2f;

    internal enum SeedStep { None, Seed, Upgrade }

    // What the support does about her seed: a new one when none of hers stands; when hers runs out (SeedDue), the heal upgrade while the
    // most hurt member in its area is below SeedUpgradeShare, else a new one; nothing while it runs.
    internal static SeedStep SeedRenewal(bool ownSeed, bool timeKnown, float secondsLeft, float? lowestInArea)
    {
        if (!SeedDue(ownSeed, timeKnown, secondsLeft)) return SeedStep.None;
        return ownSeed && lowestInArea is { } share && share < SeedUpgradeShare ? SeedStep.Upgrade : SeedStep.Seed;
    }

    // The support's seed: none of hers stands, or hers has at most SeedRenewSeconds left. A seed whose time is not known yet (its aura
    // not seen) counts as fresh.
    internal static bool SeedDue(bool ownSeed, bool timeKnown, float secondsLeft)
        => !ownSeed || (timeKnown && secondsLeft <= SeedRenewSeconds);

    // The foe to plant the seed at: the one with the most hurt members around it, then the most members, then the nearest.
    // -1 when no member stands by any foe.
    internal static int SeedFoe(IReadOnlyList<(int Hurt, int Members, int Distance)> foes)
    {
        var pick = -1;
        for (var i = 0; foes != null && i < foes.Count; i++)
        {
            var foe = foes[i];
            if (foe.Members <= 0) continue;
            if (pick < 0) { pick = i; continue; }
            var best = foes[pick];
            if (foe.Hurt != best.Hurt ? foe.Hurt > best.Hurt : foe.Members != best.Members ? foe.Members > best.Members : foe.Distance < best.Distance)
                pick = i;
        }
        return pick;
    }

    // A tank with heals heals the tanks (itself among them) and, of the others, only the critical (GroupAiPolicy.IsCritical).
    internal static bool TankHealAllows(TacticalRole role, bool self, bool critical)
        => self || role == TacticalRole.Tank || critical;

    // CORE_AI_PLAYER_ROLE: once per change of a member's role or kit.
    internal static string RoleLine(string unit, TacticalRole player, GroupAiRole kit, GroupAiRole acts)
        => $"CORE_AI_PLAYER_ROLE unit={unit} role={TacticalRoles.Word(player) ?? "neutral"} kit={kit} acts={acts}";

    // CORE_AI_ROLE_TANK: action taunt, gather or back.
    internal static string TankLine(string unit, string action, string victim, string attacker, (int X, int Y)? cell, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_ROLE_TANK unit={0} action={1} victim={2} attacker={3} cell={4} tick={5}",
            unit, action, victim ?? "-", attacker ?? "-", cell is { } c ? c.X + "," + c.Y : "-", tick);

    // CORE_AI_ROLE_DAMAGE: action roam (with the foe and whether it attacks a non-tank) or back.
    internal static string DamageLine(string unit, string action, string foe, bool onNonTank, float idle, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_ROLE_DAMAGE unit={0} action={1} foe={2} nontank={3} idle={4:0.0} tick={5}",
            unit, action, foe ?? "-", onNonTank ? 1 : 0, idle, tick);

    // CORE_AI_ROLE_HEAL: the support's or the tank healer's cast before the priority casts (burst, seed, upgrade) and what it was for.
    internal static string HealLine(string unit, string role, string action, string skill, string target, float share, int hurt, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_ROLE_HEAL unit={0} role={1} action={2} skill={3} target={4} share={5:0.00} hurt={6} tick={7}",
            unit, role, action, GroupAiRules.Short(skill), target ?? "-", share, hurt, tick);
}
