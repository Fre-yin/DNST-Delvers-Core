using DungeonSettlersDelvers.Core;
using static TestKit;

// Bandit health, armour, tint, bars and the boss bar.
internal static class BanditLookSection
{
    internal static void Run()
    {
        HealthAndArmour();
        SeedColour();
        SeedBodyBar();
        BossBar();
    }

    static void HealthAndArmour()
    {
        // Bandit health and armour: neutral by default, per role and per party; the game's resist formula.
        Check(BanditArmorRules.HealthFactor(null) == 2 && BanditArmorRules.HealthFactor(0) == 2 && BanditArmorRules.HealthFactor(1) == 1 && BanditArmorRules.HealthFactor(99) == 20
                && BanditArmorRules.ScaledHealth(3060, 1.5) == 4590 && BanditArmorRules.ScaledHealth(1800, 1) == 1800
                && BanditArmorRules.Key(null, null, "physical") == "bandits.armor.physical" && BanditArmorRules.Key(null, "guard", "magic") == "bandits.armor.guard.magic"
                && BanditArmorRules.Key("scouts", "archer", "physical") == "scouts.armor.archer.physical"
                && BanditArmorRules.Resistance(null, null) == 50 && BanditArmorRules.Resistance(null, 0) == 0 && BanditArmorRules.Resistance(null, 30) == 30 && BanditArmorRules.Resistance(80, 30) == 80
                && BanditArmorRules.Resistance(-5, null) == 0 && BanditArmorRules.Resistance(5000, null) == 1000
                && BanditArmorRules.RowResistance(15, null, null) == 65 && BanditArmorRules.RowResistance(0, null, null) == 50
                && BanditArmorRules.RowResistance(10, null, 0) == 10 && BanditArmorRules.RowResistance(10, null, 40) == 50
                && BanditArmorRules.RowResistance(10, 20, 40) == 30 && BanditArmorRules.RowResistance(990, 50, null) == 1000
                && BanditArmorRules.DamageTaken(0) == 1 && BanditArmorRules.DamageTaken(100) == 0.5 && Math.Abs(BanditArmorRules.DamageTaken(-30) - 100.0 / 70.0) < 1e-9 && Math.Abs(BanditArmorRules.DamageTaken(50) - 2.0 / 3.0) < 1e-9,
            "bandit health and armour: stage B by default (factor 2, +50 resistance), settings override, role over party, damage x 100 / (100 + resistance)");
    }

    static void SeedColour()
    {
        // The bandits' seed and sprayer show orange where the game draws green.
        var h = BanditTintRules.DefaultHue;
        var green = BanditTintRules.Pixel(64, 128, 96, 200, h, 0);
        var light = BanditTintRules.Pixel(192, 192, 160, 255, h, 0);
        var flower = BanditTintRules.Pixel(128, 0, 64, 255, h, 0);
        var darker = BanditTintRules.Pixel(128, 0, 64, 255, h, 1);
        BanditTintRules.ToHsv(darker.R / 255.0, darker.G / 255.0, darker.B / 255.0, out var darkerHue, out _, out _);
        Check(green == (128, 95, 64, 200) && light.R > light.G && light.G > light.B && light.A == 255
                && BanditTintRules.Pixel(255, 255, 255, 255, h, 0) == (255, 255, 255, 255) && BanditTintRules.Pixel(30, 160, 60, 0, h, 0) == (30, 160, 60, 0)
                && flower == (128, 0, 64, 255) && darker.R < 128 && (darkerHue > 330.0 || darkerHue < 30.0) && darkerHue != 330.0
                && BanditTintRules.Pixel(32, 32, 32, 255, h, 0) == (32, 32, 32, 255) && BanditTintRules.Pixel(100, 100, 100, 255, h, 1).R < 100
                && BanditTintRules.Hue(null) == h && BanditTintRules.Hue(-1) == null && BanditTintRules.Hue(400) == 40
                && BanditTintRules.Plant(null) == 0 && BanditTintRules.Plant(3) == 1
                && BanditTintRules.IsBuilding("BUILDING_StrangeSeed") && BanditTintRules.IsBuilding("BUILDING_MucusSprayer") && BanditTintRules.IsBuilding("BUILDING_SolarBloom")
                && BanditTintRules.IsBuilding("BUILDING_SunHeartBloom") && !BanditTintRules.IsBuilding("BUILDING_WoodenWall")
                && BanditTintRules.IsBuildingVfx(121) && BanditTintRules.IsBuildingVfx(122) && BanditTintRules.IsBuildingVfx(128) && !BanditTintRules.IsBuildingVfx(129)
                && BanditTintRules.HitRecent(0.2) && !BanditTintRules.HitRecent(1.5) && !BanditTintRules.HitRecent(-0.1)
                && BanditTintRules.Watches(122) && BanditTintRules.Watches(128) && BanditTintRules.Watches(129) && !BanditTintRules.Watches(121)
                && BanditTintRules.OwnerKind(122, true, false) == "area" && BanditTintRules.OwnerKind(122, false, true) == null
                && BanditTintRules.OwnerKind(128, true, false) == "spray" && BanditTintRules.OwnerKind(128, false, false) == null
                && BanditTintRules.OwnerKind(129, false, true) == "hit" && BanditTintRules.OwnerKind(129, false, false) == null && BanditTintRules.OwnerKind(121, true, true) == null
                && BanditTintRules.BuildingPartKept("Sprite") && BanditTintRules.BuildingPartKept("Sprite/Area/Pivot/SkillRange_OutLine_StrangeSeed")
                && !BanditTintRules.BuildingPartKept("HUD/GroundLeftDown") && !BanditTintRules.BuildingPartKept("StrangeSeedArea(Clone)/Pivot/Outer")
                && !BanditTintRules.BuildingPartKept("Sprite/Selection") && !BanditTintRules.BuildingPartKept(null)
                && BanditTintRules.Line("spray", "S1", 1, 4, "F1") == "CORE_SEED_TINT at=spray owner=S1 renderers=1 tracked=4 fx=F1"
                && BanditTintRules.OwnerLine(122, "B", "BUILDING_StrangeSeed", false, "B", "BUILDING_StrangeSeed(Clone)", "area")
                    == "CORE_SEED_TINT_OWNER id=122 owner=B key=BUILDING_StrangeSeed byOwner=0 under=B parent=BUILDING_StrangeSeed(Clone) result=area"
                && BanditTintRules.OwnerLine(122, null, null, false, null, null, null) == "CORE_SEED_TINT_OWNER id=122 owner=- key=- byOwner=0 under=- parent=- result=none"
                && BanditTintRules.SourceLine(122, "F", "B", true) == "CORE_SEED_TINT_SOURCE id=122 fx=F building=B bandit=1"
                && BanditTintRules.SourceLine(128, "F", null, false) == "CORE_SEED_TINT_SOURCE id=128 fx=F building=- bandit=0",
            "bandit tint: green turns orange, white, grey and transparent stay; effects only by their owner, never by position; a building's HUD stays");
    }

    static void SeedBodyBar()
    {
        // (only the body bar): the bandits' seed and sprayer show the enemy body bar.
        Check(BanditBarRules.ShowKey == "bandits.buildingbar" && BanditBarRules.ClipProperty == "_ClipUvRight"
                && BanditBarRules.Show(null) && BanditBarRules.Show(1) && !BanditBarRules.Show(0)
                && BanditBarRules.Clip(150, 150) == 0f && BanditBarRules.Clip(75, 150) == 0.5f && BanditBarRules.Clip(0, 200) == 1f
                && BanditBarRules.Clip(300, 200) == 0f && BanditBarRules.Clip(-5, 200) == 1f
                && BanditBarRules.Clip(float.NaN, 200) == null && BanditBarRules.Clip(50, 0) == null && BanditBarRules.Clip(50, float.NaN) == null
                && BanditBarRules.Line("made", "B1", 150, 150) == "CORE_BANDIT_BUILDING_BAR at=made building=B1 durability=150/150"
                && BanditBarRules.Line("source", "U", float.NaN, float.NaN, "a/b").EndsWith(" source=a/b", StringComparison.Ordinal),
            "bandit building bar: always on unless 0, fill 1 - durability / max like the game's unit bar, nothing on unreadable values");
    }

    static void BossBar()
    {
        Check(BanditBossBarRules.BarFill(50f, 200f, false) == 0.25f && BanditBossBarRules.BarFill(50f, 200f, true) == 0f
                && BanditBossBarRules.BarFill(-5f, 100f, false) == 0f && BanditBossBarRules.BarFill(300f, 200f, false) == 1f
                && BanditBossBarRules.BarFill(10f, 0f, false) == 0f && BanditBossBarRules.BarFill(float.NaN, 100f, false) == 0f
                && Math.Abs(BanditBossBarRules.HeadWidth(250f, 500f, 4) - BanditBossBarRules.SegmentWidth(4) / 2f) < 0.001f
                && Math.Abs(BanditBossBarRules.HeadWidth(500f, 500f, 2) - BanditBossBarRules.SegmentWidth(2)) < 0.001f,
        "boss bar: each segment fills head and body apart as the game's boss HUD (value / max), the head bar scaled to the segment");
        Check(Math.Abs(BanditBossBarRules.LabelWidth(4) * BanditBossBarRules.LabelScale - BanditBossBarRules.SegmentWidth(4)) < 0.001f
                && BanditBossBarRules.LabelScale > 0f && BanditBossBarRules.LabelScale < 1f
                && CoreUnitRules.NameKey("UNIT_DelversCoreForestBanditArcher") == "TEXTKEY_UNIT_DelversCoreForestBanditArcher_NAME",
        "boss bar: each segment's name label is a scaled copy of the title as wide as its segment, with the bandit's own name key");
        Check(BanditBossBarRules.HugeRing == "SelectionMarker_Huge" && BanditBossBarRules.NormalLowerY == -0.359375f,
        "boss bar: the bandits keep the normal ring and pivot height");
        Check(ShieldRules.HoneyColours.Length == 3 && BanditBossBarRules.ShieldAlpha > 0f && BanditBossBarRules.ShieldAlpha < 1f
                && Math.Abs(BanditBossBarRules.LabelCenterY(10f, 2f) - 11.3f) < 0.001f && BanditBossBarRules.LabelCenterY(10f, 2f) - 1f > 10f,
            "boss bar: the shield is a half transparent honeycomb band; each name stands fully above its bars with a small gap");
    }
}
