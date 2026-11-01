namespace DungeonSettlersDelvers.Core;

// The game's skills that the bandits' decisions look up by key (GroupAiBandits: the fighters, the offensive pull, the herbalist
// and her moves), as constants in one place. The role lists are what the offline check holds against each role's kit in
// CoreUnitRules: a key that is no skill of its role's kit (a typo) finds no slot and its rule never fires.
internal static class BanditKitKeys
{
    internal const string Slay = "SKILL_Slay";
    internal const string DestructiveSplit = "SKILL_DestructiveSplit";

    internal const string Hammering = "SKILL_Hammering";
    internal const string WildFlurry = "SKILL_WildFlurry";
    internal const string ShieldUp = "SKILL_ShieldUp";
    internal const string IronClad = "SKILL_IronClad";

    internal const string SnipingShot = "SKILL_SnipingShot";
    internal const string ArrowRain = "SKILL_ArrowRain";
    internal const string AnkleBreak = "SKILL_AnkleBreak";
    internal const string HeadAim = "SKILL_HeadAim";

    internal const string Vines = "SKILL_ClingingVines";
    internal const string Lifebloom = LifebloomRules.SkillKey;

    internal static readonly string[] Blade = { Slay, DestructiveSplit };
    internal static readonly string[] Guard = { Hammering, WildFlurry, ShieldUp, IronClad };
    internal static readonly string[] Archer = { SnipingShot, ArrowRain, AnkleBreak, HeadAim };
    internal static readonly string[] Healer = { Vines, Lifebloom };

    // The keys the decisions of a role (CoreUnitRules.Definition.Role) look up; none for an unknown role.
    internal static string[] ForRole(string role) => role switch
    {
        "blade" => Blade,
        "guard" => Guard,
        "archer" => Archer,
        "healer" => Healer,
        _ => new string[0]
    };
}
