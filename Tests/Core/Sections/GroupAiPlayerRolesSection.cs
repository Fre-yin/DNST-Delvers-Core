using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: the player's roles from the formation window: which rules a role decides by, the tank's guard and
// gathering, the damage dealer's roaming, the support's seed and the healer with the role tank.
internal static class GroupAiPlayerRolesSection
{
    internal static void Run()
    {
        Mapping();
        TankGuard();
        TankGather();
        DamageRoam();
        SupportSeed();
        TankHealer();
        WaterMage();
        FightStart();
        Lines();
    }

    static void FightStart()
    {
        Check(GroupAiFightStartRules.InSight(GroupAiFightStartRules.SightCells, true) && !GroupAiFightStartRules.InSight(GroupAiFightStartRules.SightCells + 1, true)
                && !GroupAiFightStartRules.InSight(2, false),
            "fight start: a foe counts within the sight cells and only when the player can see it");
        Check(GroupAiFightStartRules.Hit(true, 10f, 8f) && !GroupAiFightStartRules.Hit(false, 10f, 8f) && !GroupAiFightStartRules.Hit(true, 10f, 6f)
                && !GroupAiFightStartRules.Hit(true, 10f, float.NegativeInfinity),
            "fight start: a member in combat counts only when a foe has just hit it, aggro alone starts nothing");
        Check(GroupAiFightStartRules.Line("sight", "UNIT_Goblin", 3) == "CORE_AI_FIGHT_START cause=sight foe=UNIT_Goblin members=3"
                && GroupAiFightStartRules.Line("hit", null, 2) == "CORE_AI_FIGHT_START cause=hit foe=- members=2",
            "fight start: the log line");
    }

    static void Mapping()
    {
        Check(GroupAiRoleRules.Effective(GroupAiRole.Melee, TacticalRole.Neutral, false, false) == GroupAiRole.Melee
                && GroupAiRoleRules.Effective(GroupAiRole.Heal, TacticalRole.Neutral, false, true) == GroupAiRole.Heal
                && GroupAiRoleRules.Effective(GroupAiRole.Shield, TacticalRole.Neutral, false, false) == GroupAiRole.Shield,
            "roles: neutral keeps the role read from the kit");
        Check(GroupAiRoleRules.Effective(GroupAiRole.Melee, TacticalRole.Tank, false, false) == GroupAiRole.Shield
                && GroupAiRoleRules.Effective(GroupAiRole.Ranged, TacticalRole.Tank, true, false) == GroupAiRole.Shield
                && GroupAiRoleRules.Effective(GroupAiRole.Heal, TacticalRole.Tank, false, true) == GroupAiRole.Heal,
            "roles: a tank decides by the shield's rules, with a kit of heals by the healer's");
        Check(GroupAiRoleRules.Effective(GroupAiRole.Shield, TacticalRole.Damage, false, false) == GroupAiRole.Melee
                && GroupAiRoleRules.Effective(GroupAiRole.Heal, TacticalRole.Damage, false, true) == GroupAiRole.Caster
                && GroupAiRoleRules.Effective(GroupAiRole.Heal, TacticalRole.Damage, true, false) == GroupAiRole.Ranged
                && GroupAiRoleRules.Effective(GroupAiRole.Ranged, TacticalRole.Damage, true, false) == GroupAiRole.Ranged,
            "roles: a damage dealer decides by its basic attack's damage role, never as healer or shield");
        Check(GroupAiRoleRules.Effective(GroupAiRole.Heal, TacticalRole.Support, false, true) == GroupAiRole.Heal
                && GroupAiRoleRules.Effective(GroupAiRole.Shield, TacticalRole.Support, false, false) == GroupAiRole.Melee
                && GroupAiRoleRules.Effective(GroupAiRole.Caster, TacticalRole.Support, false, true) == GroupAiRole.Caster,
            "roles: a support heals with a kit of heals, otherwise it deals damage");
        Check(GroupAiRoleRules.SkipsForDamage(TacticalRole.Damage, "SKILL_GuardiansTaunt") && GroupAiRoleRules.SkipsForDamage(TacticalRole.Damage, "SKILL_IronClad")
                && !GroupAiRoleRules.SkipsForDamage(TacticalRole.Damage, "SKILL_Slay") && !GroupAiRoleRules.SkipsForDamage(TacticalRole.Neutral, "SKILL_GuardiansTaunt")
                && !GroupAiRoleRules.SkipsForDamage(TacticalRole.Tank, "SKILL_ShieldUp") && !GroupAiRoleRules.SkipsForDamage(TacticalRole.Damage, null),
            "roles: Core's own choice for a damage dealer skips tank skills, other roles keep them");
        Check(GroupAiRoleRules.Critical(0.29f, false) && GroupAiRoleRules.Critical(0.9f, true) && !GroupAiRoleRules.Critical(0.3f, false)
                && !GroupAiRoleRules.Critical(0.8f, false),
            "roles: the critical range is below 30 % or lying at near-death");
    }

    static void TankGuard()
    {
        var allies = new List<(string Guid, TacticalRole Role, float Share, int Attackers)>
        {
            ("neutral", TacticalRole.Neutral, 0.1f, 2),
            ("tank", TacticalRole.Tank, 0.2f, 1),
            ("damage", TacticalRole.Damage, 0.7f, 1),
            ("support", TacticalRole.Support, 0.5f, 1),
            ("quiet", TacticalRole.Support, 0.05f, 0)
        };
        Check(GroupAiRoleRules.GuardVictim(allies) == "support",
            "tank: guards the lowest attacked damage or support member, not neutral, tank or unattacked members");
        Check(GroupAiRoleRules.GuardVictim(allies.GetRange(0, 2)) == null && GroupAiRoleRules.GuardVictim(null) == null,
            "tank: no guard without an attacked damage or support member");
    }

    static void TankGather()
    {
        Func<int, int, bool> open = (_, _) => true;
        // Taunt reach 2 (Manhattan): from (0,0) toward a foe at (6,0) the first cell in reach is (4,0).
        Check(GroupAiRoleRules.GatherCell((0, 0), (6, 0), 2, (0, 0), open) == (4, 0),
            "tank: walks toward the attacker until the taunt reaches it");
        // The leader stands at (-2,0): the grid's edge is 4 cells from it, so the walk stops at (2,0).
        Check(GroupAiRoleRules.GatherCell((0, 0), (9, 0), 2, (-2, 0), open) == (2, 0) && GroupAiRoleRules.GridReach == 4,
            "tank: gathers at most to the edge of the tactical grid measured from the leader");
        Check(GroupAiRoleRules.GatherCell((4, 0), (9, 0), 2, (0, 0), open) == null,
            "tank: no walk when already at the grid's edge");
        Check(GroupAiRoleRules.GatherCell((0, 0), (6, 0), 2, (0, 0), (x, y) => x != 4) == (3, 0),
            "tank: a taken cell in reach leaves it on the last free cell before it");
        Check(GroupAiRoleRules.GatherCell((0, 0), (2, 0), 2, (0, 0), open) == null,
            "tank: no walk when the taunt already reaches");
        Check(!GroupAiRoleRules.GatherEnds(true, 10f, 9f, 8f) && GroupAiRoleRules.GatherEnds(true, 12f, 9f, 8f)
                && !GroupAiRoleRules.GatherEnds(true, 12f, 9f, 10f) && GroupAiRoleRules.GatherEnds(false, 9.1f, 9f, 9f),
            "tank: goes back after 3 s without an attack since it set out, or when the fight ends");
    }

    static void DamageRoam()
    {
        Check(!GroupAiRoleRules.RoamDue(true, 13.4f, 10f) && GroupAiRoleRules.RoamDue(true, 13.5f, 10f) && !GroupAiRoleRules.RoamDue(false, 20f, 10f),
            "damage: leaves its place after 3.5 s without a foe in reach, only in a fight");
        var foes = new List<(int Distance, bool OnNonTank)> { (2, false), (5, true), (4, true) };
        Check(GroupAiRoleRules.RoamTarget(foes) == 2, "damage: goes to the nearest foe that attacks a member who is no tank");
        Check(GroupAiRoleRules.RoamTarget(new List<(int Distance, bool OnNonTank)> { (3, false), (2, false) }) == 1
                && GroupAiRoleRules.RoamTarget(new List<(int Distance, bool OnNonTank)>()) == -1,
            "damage: else the nearest foe; none without foes");
        Check(GroupAiFormationRules.PlacesFollow(false, false) && GroupAiFormationRules.PlacesFollow(true, true) && !GroupAiFormationRules.PlacesFollow(true, false),
            "formation: in a fight the places follow the leader only while a player's order moves it, out of it always");
        Func<int, int, bool> open = (_, _) => true;
        Check(GroupAiRoleRules.ApproachCell((0, 0), (5, 5), 1, open) == (4, 4) && GroupAiRoleRules.ApproachCell((0, 0), (6, 0), 3, open) == (3, 0)
                && GroupAiRoleRules.ApproachCell((0, 0), (1, 1), 1, open) == null,
            "damage: walks until the foe is in its reach, not when it already is");
    }

    static void SupportSeed()
    {
        Check(GroupAiRoleRules.SeedDue(false, false, 0f) && GroupAiRoleRules.SeedDue(true, true, 2f) && !GroupAiRoleRules.SeedDue(true, true, 2.1f)
                && !GroupAiRoleRules.SeedDue(true, false, 0f),
            "support: a new seed when hers is gone or has at most 2 s left, not earlier and not while its time is unknown");
        Check(GroupAiRoleRules.SeedRenewal(false, false, 0f, null) == GroupAiRoleRules.SeedStep.Seed
                && GroupAiRoleRules.SeedRenewal(true, true, 5f, 0.2f) == GroupAiRoleRules.SeedStep.None
                && GroupAiRoleRules.SeedRenewal(true, false, 0f, 0.2f) == GroupAiRoleRules.SeedStep.None,
            "support: a seed when none of hers stands, nothing while hers runs");
        Check(GroupAiRoleRules.SeedRenewal(true, true, 1.5f, 0.6f) == GroupAiRoleRules.SeedStep.Upgrade
                && GroupAiRoleRules.SeedRenewal(true, true, 1.5f, 0.7f) == GroupAiRoleRules.SeedStep.Seed
                && GroupAiRoleRules.SeedRenewal(true, true, 1.5f, null) == GroupAiRoleRules.SeedStep.Seed
                && Math.Abs(GroupAiRoleRules.SeedUpgradeShare - 0.675f) < 0.0001f,
            "support: a running out seed is grown while the most hurt in its area is nearer 35 % than full (below 67.5 %), else a new one");
        var foes = new List<(int Hurt, int Members, int Distance)> { (1, 3, 1), (2, 2, 4), (2, 2, 3), (0, 0, 1) };
        Check(GroupAiRoleRules.SeedFoe(foes) == 2, "support: the seed goes where the most hurt stand, the nearer foe on a tie");
        Check(GroupAiRoleRules.SeedFoe(new List<(int Hurt, int Members, int Distance)> { (0, 2, 3), (0, 3, 5) }) == 1
                && GroupAiRoleRules.SeedFoe(new List<(int Hurt, int Members, int Distance)> { (0, 0, 1) }) == -1,
            "support: with as many hurt the most members count; no foe with no member beside it");
    }

    static void TankHealer()
    {
        Check(GroupAiRoleRules.TankHealAllows(TacticalRole.Tank, false, false) && GroupAiRoleRules.TankHealAllows(TacticalRole.Neutral, true, false)
                && GroupAiRoleRules.TankHealAllows(TacticalRole.Damage, false, true) && GroupAiRoleRules.TankHealAllows(TacticalRole.Support, false, true)
                && !GroupAiRoleRules.TankHealAllows(TacticalRole.Damage, false, false) && !GroupAiRoleRules.TankHealAllows(TacticalRole.Neutral, false, false),
            "tank healer: heals the tanks and itself, the others only in the critical range");
    }

    static void WaterMage()
    {
        Check(GroupAiWaterRules.IsWaterMage(new[] { "SKILL_Slay", "SKILL_SummonBubbling" }) && GroupAiWaterRules.IsWaterMage(new[] { "SKILL_EmbracingWave" })
                && !GroupAiWaterRules.IsWaterMage(new[] { "SKILL_TidalMark", "SKILL_StrangeSeed" }) && !GroupAiWaterRules.IsWaterMage(null),
            "water mage: a member with the bubbleling's summon or the embracing wave");
        Check(GroupAiWaterRules.Support(true, true, 1, true) == GroupAiWaterRules.Step.Wave
                && GroupAiWaterRules.Support(true, true, 0, true) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Support(false, true, 2, true) == GroupAiWaterRules.Step.None
                && GroupAiWaterRules.Support(true, false, 1, true) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Support(true, false, 2, true) == GroupAiWaterRules.Step.None
                && GroupAiWaterRules.Support(true, false, 1, false) == GroupAiWaterRules.Step.None,
            "water mage, support: the wave sacrifices a bubbleling for a member below 50 %, else it keeps two bubblelings");
        // Damage(bubblings, toads, toadFree, summonFree, glacierFree, glacierReach, diveFree, diveFoes)
        Check(GroupAiWaterRules.Damage(1, 0, true, true, true, true, true, 3) == GroupAiWaterRules.Step.Toad
                && GroupAiWaterRules.Damage(0, 0, true, true, true, true, true, 3) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Damage(1, 0, false, true, true, true, false, 0) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Damage(2, 0, false, true, true, true, false, 0) == GroupAiWaterRules.Step.Glacier
                && GroupAiWaterRules.Damage(1, 1, true, true, true, true, false, 0) == GroupAiWaterRules.Step.Glacier
                && GroupAiWaterRules.Damage(2, 1, true, true, false, true, false, 0) == GroupAiWaterRules.Step.None
                && GroupAiWaterRules.Damage(1, 1, true, true, true, false, false, 0) == GroupAiWaterRules.Step.Bubbling,
            "water mage, damage: the toad first, Glacier Drop on the focus with a bubbleling left over (one held for a missing toad), then two bubblelings");
        Check(GroupAiWaterRules.Damage(2, 1, true, true, true, true, true, 2) == GroupAiWaterRules.Step.Dive
                && GroupAiWaterRules.Damage(2, 1, true, true, true, true, true, 1) == GroupAiWaterRules.Step.Glacier
                && GroupAiWaterRules.Damage(2, 0, false, true, true, true, true, 3) == GroupAiWaterRules.Step.Glacier,
            "water mage, damage: Water Dive only with the toad standing and two foes on or next to the tile; never the wave");
        Check(GroupAiWaterRules.Guards(0.69f) && !GroupAiWaterRules.Guards(0.7f) && GroupAiWaterRules.SacrificeShare == 0.5f,
            "water mage, support: the bubblelings guard the most hurt below 70 %, the sacrifice line is 50 %");
        Check(GroupAiWaterRules.Refill(true, 1, 0, true, true) == GroupAiWaterRules.Step.Toad && GroupAiWaterRules.Refill(true, 0, 1, true, true) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Refill(true, 2, 1, true, true) == GroupAiWaterRules.Step.None && GroupAiWaterRules.Refill(false, 1, 0, true, true) == GroupAiWaterRules.Step.Bubbling
                && GroupAiWaterRules.Refill(false, 2, 0, true, true) == GroupAiWaterRules.Step.None && GroupAiWaterRules.Refill(true, 1, 0, false, false) == GroupAiWaterRules.Step.None,
            "water mage: out of a fight it summons up to a toad and two bubblelings (damage) or two bubblelings (support)");
        Check(GroupAiWaterRules.Line("u", "support", "wave", "t", 0.42f, 1, 0, 5) == "CORE_AI_ROLE_WATER unit=u role=support action=wave target=t share=0.42 bubblings=1 toads=0 tick=5",
            "water mage: the log line");
    }

    static void Lines()
    {
        Check(GroupAiRoleRules.RoleLine("u1", TacticalRole.Damage, GroupAiRole.Shield, GroupAiRole.Melee) == "CORE_AI_PLAYER_ROLE unit=u1 role=damage kit=Shield acts=Melee"
                && GroupAiRoleRules.TankLine("u1", "gather", "v", "a", (3, -1), 7) == "CORE_AI_ROLE_TANK unit=u1 action=gather victim=v attacker=a cell=3,-1 tick=7"
                && GroupAiRoleRules.TankLine("u1", "back", null, null, null, 8) == "CORE_AI_ROLE_TANK unit=u1 action=back victim=- attacker=- cell=- tick=8"
                && GroupAiRoleRules.DamageLine("u2", "roam", "f", true, 3.5f, 9) == "CORE_AI_ROLE_DAMAGE unit=u2 action=roam foe=f nontank=1 idle=3.5 tick=9"
                && GroupAiRoleRules.HealLine("u3", "support", "seed", "SKILL_StrangeSeed", "f", 0f, 2, 10)
                == "CORE_AI_ROLE_HEAL unit=u3 role=support action=seed skill=StrangeSeed target=f share=0.00 hurt=2 tick=10",
            "roles: the log lines of the roles");
    }
}
