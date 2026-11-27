namespace DungeonSettlersDelvers.Core;

// The game's skills of the weak bandits' new classes and the weapon skills the four old roles gain (WeakBanditRules), as
// constants in one place for their rules (BanditWeakClasses). Kept apart from BanditKitKeys, whose lists the boss party's kits
// are checked against; the offline check holds these against the weak kits. All row 0 of the game's skill trees or a
// weapon's own skill, but for the water mage's toad.
internal static class WeakBanditKitKeys
{
    internal const string SpearBasic = "SKILL_BasicSpearAttack";
    internal const string PiercingThrust = "SKILL_PiercingThrust";
    internal const string OpeningStrike = "SKILL_OpeningStrike";
    internal const string TwinThrust = "SKILL_TwinThrust";

    internal const string MagicBasic = "SKILL_BasicMagicMissile";
    internal const string TidalMark = "SKILL_TidalMark";
    internal const string EmbracingWave = "SKILL_EmbracingWave";
    // The water staff's own skill and its unit; the toad from level 3 on, the one skill outside row 0.
    internal const string SummonBubbling = "SKILL_SummonBubbling";
    internal const string Bubbling = "UNIT_Bubbleling";
    internal const string SummonBubbleToad = "SKILL_SummonBubbleToad";
    internal const string BubbleToad = "UNIT_BubbleToad";

    internal const string SwordBasic = "SKILL_BasicSwordAttack";
    internal const string SweepingCleave = "SKILL_SweepingCleave";
    internal const string ChargeAttack = "SKILL_ChargeAttack";
    internal const string SwordCounter = "SKILL_SwordCounter";

    internal const string FireMissile = "SKILL_FireMissile";
    internal const string Incinerate = "SKILL_Incinerate";
    internal const string BlazingEcho = "SKILL_BlazingEcho";

    internal const string WhirlwindSlash = "SKILL_WhirlwindSlash";
    internal const string RapidFire = "SKILL_RapidFire";

    // The kit variants and level 3 skills (WeakBanditRules.Variants, Level3Skills).
    internal const string Anchor = "SKILL_Anchor";
    internal const string SpearBash = "SKILL_SpearBash";
    internal const string VerticalSlash = "SKILL_VerticalSlash";
    internal const string PommelStrike = "SKILL_PommelStrike";
    internal const string FireCloak = "SKILL_FireCloak";
    internal const string VileInsult = "SKILL_VileInsult";
    internal const string GuardiansTaunt = "SKILL_GuardiansTaunt";
    internal const string Hammering = "SKILL_Hammering";
    internal const string ArrowRain = "SKILL_ArrowRain";
    internal const string GraspingVines = "SKILL_ClingingVines";
    internal const string HealingTouch = "SKILL_HealingTouchAttack";

    // The keys each new class's rule looks up, and the weapon skills the old roles' rules gain for the weak ones.
    internal static readonly string[] Spear = { PiercingThrust, OpeningStrike, TwinThrust };
    internal static readonly string[] WaterMage = { SummonBubbling, TidalMark, EmbracingWave };
    internal static readonly string[] Sword = { SweepingCleave, ChargeAttack, SwordCounter };
    internal static readonly string[] FireMage = { FireMissile, Incinerate, BlazingEcho };
    internal static readonly string[] BladeWeapon = { WhirlwindSlash };
    internal static readonly string[] ArcherWeapon = { RapidFire };

    internal static string[] ForRole(string role) => role switch
    {
        "spear" => Spear,
        "watermage" => WaterMage,
        "sword" => Sword,
        "firemage" => FireMage,
        "blade" => BladeWeapon,
        "archer" => ArcherWeapon,
        _ => new string[0]
    };
}
