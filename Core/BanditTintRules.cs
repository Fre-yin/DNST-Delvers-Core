using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The bandits' Strange Seed and Mucus Sprayer, and their effects, show orange
// where the game draws green, so a player tells them from their own. Only the instances a Core summoner set; the game's
// sprites stay untouched, Core draws a recoloured copy at runtime (BanditTint). Per pixel: a green pixel (hue GreenHueMin..
// GreenHueMax, saturation at least MinSaturation) takes the hue of HueKey (#FF9A3C by default) and keeps its saturation,
// brightness and alpha; grey, white and transparent stay. PlantKey (0..1, 0 by default) makes the other coloured parts
// (the sprayer's red-magenta flower, the dark stems) darker and redder. Rules without the game.
internal static class BanditTintRules
{
    internal const string HueKey = "bandits.tint.hue";
    internal const string PlantKey = "bandits.tint.plant";
    // #FF9A3C: hue 28.9 degrees.
    internal const double DefaultHue = 28.9;
    internal const double GreenHueMin = 55.0;
    internal const double GreenHueMax = 170.0;
    internal const double MinSaturation = 0.15;
    // At PlantKey 1 the plant's hue moves PlantHueShare of the way to red and its brightness drops by PlantDarken.
    internal const double PlantHueShare = 0.5;
    internal const double PlantDarken = 0.35;

    internal const string SeedBuilding = "BUILDING_StrangeSeed";
    internal const string SprayerBuilding = "BUILDING_MucusSprayer";
    // The seed's upgrades (SKILL_SolarBloom on a seed, SKILL_SunHeartBloom on a Solar Bloom; orange like
    // seed and sprayer, their area too).
    internal const string SolarBuilding = "BUILDING_SolarBloom";
    internal const string SunHeartBuilding = "BUILDING_SunHeartBloom";
    // The effects at a bandit building: StrangeSeedEvolving (121, the upgrade), StrangeSeedArea (122), MucusSprayerAttack (128).
    internal const int EvolvingVfx = 121;
    internal const int AreaVfx = 122;
    internal const int SprayVfx = 128;
    // MucusSprayerHit (129) is bound to the unit that was hit (AffecterFX_MucusSprayer.OnDamageFX, SetOwner); it is the
    // bandits' when a Core sprayer hit that very unit within HitSeconds (GroupAiHealerSeed.OnSprayerDamage).
    internal const int HitVfx = 129;
    internal const double HitSeconds = 1.0;
    // Effects waiting for their owner (VFXView.SetVFXOwner follows CreateVFX).
    internal const int PendingCap = 256;
    // New copies per frame, so a burst of new sprites spreads over a few frames.
    internal const int CopiesPerFrame = 8;
    internal const int LogCap = 400;
    // The summons' tint lines count apart, so the skill effects and restores of a long campaign never use up their lines
    // (no summon line left on level 3, the shared cap full).
    internal const int SummonLogCap = 200;
    // The skill effects' lines (BanditTintSkill once per caster and effect id, CORE_SEED_TINT at=skill), apart from the buildings'.
    internal const int SkillLogCap = 400;

    internal static bool IsBuilding(string key) => key == SeedBuilding || key == SprayerBuilding || key == SolarBuilding || key == SunHeartBuilding;

    // A bandit's summoned unit (the bubblelings orange like seed and sprayer). The bubbleling is blue, so the
    // green rule leaves it as it is; for a summon the water hues (WaterHueMin..WaterHueMax, cyan to violet blue) take the tint's hue,
    // keeping saturation, brightness and alpha; grey, white and every other hue stay.
    internal const double WaterHueMin = 170.0;
    internal const double WaterHueMax = 270.0;

    internal static (byte R, byte G, byte B, byte A) SummonPixel(byte r, byte g, byte b, byte a, double hue)
    {
        if (a == 0) return (r, g, b, a);
        ToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out var v);
        if (s < MinSaturation || h < WaterHueMin || h > WaterHueMax) return (r, g, b, a);
        FromHsv(hue, s, v, out var nr, out var ng, out var nb);
        return (ToByte(nr), ToByte(ng), ToByte(nb), a);
    }

    // A bandit's skill effect (BanditTintSkill): every
    // coloured pixel takes the tint's hue, keeping saturation, brightness and alpha, whatever its hue (vines green, bubbles and
    // ice blue, a stomp's dust brown); grey, white and transparent stay, so the light of an effect stays light.
    internal static (byte R, byte G, byte B, byte A) SkillPixel(byte r, byte g, byte b, byte a, double hue)
    {
        if (a == 0) return (r, g, b, a);
        ToHsv(r / 255.0, g / 255.0, b / 255.0, out _, out var s, out var v);
        if (s < MinSaturation) return (r, g, b, a);
        FromHsv(hue, s, v, out var nr, out var ng, out var nb);
        return (ToByte(nr), ToByte(ng), ToByte(nb), a);
    }

    internal static bool IsBuildingVfx(int id) => id == EvolvingVfx || id == AreaVfx || id == SprayVfx;

    // The hue in use: the setting's, else the default; a negative setting turns the tint off (null).
    internal static double? Hue(double? setting)
    {
        if (setting is not double v || double.IsNaN(v)) return DefaultHue;
        if (v < 0.0) return null;
        return v % 360.0;
    }

    internal static double Plant(double? setting)
        => setting is double v && !double.IsNaN(v) ? Math.Clamp(v, 0.0, 1.0) : 0.0;

    // The effects Core waits on an owner for. StrangeSeedEvolving (121) gets no owner from the game (SkillFX_MucusSprayer.
    // OnSpawnFX), so it stays as it is.
    internal static bool Watches(int id) => id == AreaVfx || id == SprayVfx || id == HitVfx;

    internal static bool HitRecent(double secondsSince) => secondsSince >= 0.0 && secondsSince <= HitSeconds;

    // What an effect bound to its owner is for the tint, or null to leave it: area and spray only on a bandit building, the
    // hit only on a unit a Core sprayer just hit. No position is used (nothing of the players' side).
    internal static string OwnerKind(int id, bool ownerIsBanditBuilding, bool ownerJustHitByCoreSprayer) => id switch
    {
        AreaVfx when ownerIsBanditBuilding => "area",
        SprayVfx when ownerIsBanditBuilding => "spray",
        HitVfx when ownerJustHitByCoreSprayer && !ownerIsBanditBuilding => "hit",
        _ => null
    };

    // The renderers of a bandit building Core recolours: its sprite and the area drawn under it (the probe's tree:
    // Sprite, Sprite/Area/Pivot/SkillRange_*); not the building HUD nor effects hung under the building.
    internal static bool BuildingPartKept(string path)
        => path != null && (path == "Sprite" || path.StartsWith("Sprite/Area/", StringComparison.Ordinal));

    // One pixel, 0..255 per channel.
    internal static (byte R, byte G, byte B, byte A) Pixel(byte r, byte g, byte b, byte a, double hue, double plant)
    {
        if (a == 0) return (r, g, b, a);
        ToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out var v);
        if (s < MinSaturation)
        {
            if (plant <= 0.0) return (r, g, b, a);
            v *= 1.0 - PlantDarken * plant;
        }
        else if (h >= GreenHueMin && h <= GreenHueMax) h = hue;
        else
        {
            if (plant <= 0.0) return (r, g, b, a);
            // The shorter way round to red (0 degrees).
            var toRed = h > 180.0 ? 360.0 - h : -h;
            h = (h + toRed * PlantHueShare * plant + 360.0) % 360.0;
            v *= 1.0 - PlantDarken * plant;
        }
        FromHsv(h, s, v, out var nr, out var ng, out var nb);
        return (ToByte(nr), ToByte(ng), ToByte(nb), a);
    }

    internal static void ToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        v = max;
        s = max <= 0.0 ? 0.0 : d / max;
        if (d <= 0.0) h = 0.0;
        else if (max == r) h = 60.0 * (((g - b) / d) % 6.0);
        else if (max == g) h = 60.0 * ((b - r) / d + 2.0);
        else h = 60.0 * ((r - g) / d + 4.0);
        if (h < 0.0) h += 360.0;
    }

    internal static void FromHsv(double h, double s, double v, out double r, out double g, out double b)
    {
        var c = v * s;
        var x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        var m = v - c;
        (r, g, b) = (h % 360.0) switch
        {
            < 60.0 => (c, x, 0.0),
            < 120.0 => (x, c, 0.0),
            < 180.0 => (0.0, c, x),
            < 240.0 => (0.0, x, c),
            < 300.0 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        r += m;
        g += m;
        b += m;
    }

    internal static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v * 255.0), 0.0, 255.0);

    // CORE_SEED_TINT: what Core recoloured (at=building|area|evolving|spray|hit|restore), with the renderers it tracks now.
    internal static string Line(string at, string owner, int renderers, int tracked, string fx = null)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TINT at={0} owner={1} renderers={2} tracked={3}{4}",
            at, owner ?? "-", renderers, tracked, fx == null ? "" : " fx=" + fx);

    // CORE_SEED_TINT_OWNER: why an area (122) or spray (128) was or was not recoloured when the game bound it, capped.
    internal const int OwnerLogCap = 60;

    internal static string OwnerLine(int id, string owner, string key, bool byOwner, string underBuilding, string parent, string at)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TINT_OWNER id={0} owner={1} key={2} byOwner={3} under={4} parent={5} result={6}",
            id, owner ?? "-", key ?? "-", byOwner ? 1 : 0, underBuilding ?? "-", parent ?? "-", at ?? "none");

    // CORE_SEED_TINT_SOURCE: an area (122) or spray (128) the game created, the building whose passive made it (BanditTintSource)
    // and whether that one is a confirmed bandit building. Capped like the owner line.
    internal static string SourceLine(int id, string fx, string building, bool bandit)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TINT_SOURCE id={0} fx={1} building={2} bandit={3}", id, fx ?? "-", building ?? "-", bandit ? 1 : 0);

    // CORE_SEED_TINT_SKILL: an effect a bandit's skill or affecter asked for (BanditTintSkill), with the caster.
    internal static string SkillLine(int id, string fx, string caster, string key)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TINT_SKILL id={0} fx={1} caster={2} key={3}", id, fx ?? "-", caster ?? "-", key ?? "-");

    internal static string CopyLine(string sprite, int width, int height, int cached)
        => string.Format(CultureInfo.InvariantCulture, "CORE_SEED_TINT_COPY sprite={0} size={1}x{2} cached={3}", sprite ?? "-", width, height, cached);
}
